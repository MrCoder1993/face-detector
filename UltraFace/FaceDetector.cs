using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System.Text;

namespace UltraFace;

public sealed class FaceDetector : IDisposable
{
    private readonly int _modelInputWidth;
    private readonly int _modelInputHeight;
    private const float MinScoreThreshold = 0.1f;

    private readonly InferenceSession _session;
    private readonly string _modelInfo;

    public FaceDetector(string modelPath)
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException("Model file not found.", modelPath);

        _session = new InferenceSession(modelPath);
        _modelInfo = BuildModelInfo();

        // Determine model input size from metadata when available. Falls back to 640x640.
        var firstInput = _session.InputMetadata.Values.FirstOrDefault();
        if (firstInput is not null && firstInput.Dimensions.Length >= 4)
        {
            // Expect dims like [N,C,H,W]
            _modelInputHeight = firstInput.Dimensions[2] > 0 ? firstInput.Dimensions[2] : 640;
            _modelInputWidth = firstInput.Dimensions[3] > 0 ? firstInput.Dimensions[3] : 640;
        }
        else
        {
            _modelInputWidth = 640;
            _modelInputHeight = 640;
        }
    }

    public string GetModelInfo()
    {
        return _modelInfo;
    }

    private string BuildModelInfo()
    {
        var result = new StringBuilder();

        result.AppendLine("INPUTS:");
        foreach (var input in _session.InputMetadata)
        {
            result.AppendLine($"Name: {input.Key}");
            result.AppendLine($"Type: {input.Value.ElementType}");
            result.AppendLine($"Dimensions: {string.Join(" x ", input.Value.Dimensions)}");
            result.AppendLine();
        }

        result.AppendLine("OUTPUTS:");
        foreach (var output in _session.OutputMetadata)
        {
            result.AppendLine($"Name: {output.Key}");
            result.AppendLine($"Type: {output.Value.ElementType}");
            result.AppendLine($"Dimensions: {string.Join(" x ", output.Value.Dimensions)}");
            result.AppendLine();
        }

        return result.ToString();
    }

    public IReadOnlyList<FaceDetection> Detect(
        Mat bgrImage,
        float scoreThreshold = 0.6f,
        float nmsThreshold = 0.3f)
    {
        if (bgrImage is null) throw new ArgumentNullException(nameof(bgrImage));
        if (bgrImage.Empty()) return Array.Empty<FaceDetection>();

        scoreThreshold = Math.Max(MinScoreThreshold, scoreThreshold);

        var originalW = bgrImage.Width;
        var originalH = bgrImage.Height;

        // Optional pre-scale for low-res RTSP frames: upscale a bit before letterbox
        // to help the model when faces are small.
        using var pre = new Mat();
        var preScale = 1f;
        if (originalW > 0 && originalW < 900)
        {
            preScale = 1.5f;
            Cv2.Resize(bgrImage, pre, new Size((int)MathF.Round(originalW * preScale), (int)MathF.Round(originalH * preScale)));
        }
        else
        {
            bgrImage.CopyTo(pre);
        }

        // Letterbox to preserve aspect ratio; improves detection accuracy on non-square camera frames.
        using var resized = new Mat();
        using var letterboxed = Letterbox(pre, new Size(_modelInputWidth, _modelInputHeight), out var scale, out var padX, out var padY);
        letterboxed.CopyTo(resized);

        // tensor: [1,3,H,W]
        var inputTensor = new DenseTensor<float>(new[] { 1, 3, _modelInputHeight, _modelInputWidth });

        // SCRFD/InsightFace: normalize to [-1, 1] with (x - 127.5) / 128.
        for (var y = 0; y < _modelInputHeight; y++)
        {
            for (var x = 0; x < _modelInputWidth; x++)
            {
                var pixel = resized.At<Vec3b>(y, x); // BGR
                inputTensor[0, 0, y, x] = (pixel.Item2 - 127.5f) / 128f; // R
                inputTensor[0, 1, y, x] = (pixel.Item1 - 127.5f) / 128f; // G
                inputTensor[0, 2, y, x] = (pixel.Item0 - 127.5f) / 128f; // B
            }
        }

        var inputName = _session.InputMetadata.Keys.Single();
        var inputs = new[]
        {
            NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
        };

        using var results = _session.Run(inputs);

        // SCRFD (det_10g): decode bbox deltas (l,t,r,b) relative to anchor centers.
        // scale returned from Letterbox maps model->pre. Combine with preScale to map to original.
        var rawDetections = DecodeScrfdDet10g(results, scoreThreshold, originalW, originalH, scale * preScale, padX, padY);


        var kept = Nms.Run(rawDetections, nmsThreshold);
        return kept.Select(i => rawDetections[i]).ToArray();
    }

    private static Mat Letterbox(Mat srcBgr, Size dstSize, out float scale, out int padX, out int padY)
    {
        var srcW = srcBgr.Width;
        var srcH = srcBgr.Height;

        var rW = dstSize.Width / (float)srcW;
        var rH = dstSize.Height / (float)srcH;
        scale = Math.Min(rW, rH);

        var newW = Math.Max(1, (int)MathF.Round(srcW * scale));
        var newH = Math.Max(1, (int)MathF.Round(srcH * scale));

        padX = (dstSize.Width - newW) / 2;
        padY = (dstSize.Height - newH) / 2;

        using var resized = new Mat();
        Cv2.Resize(srcBgr, resized, new Size(newW, newH));

        var dst = new Mat(dstSize, MatType.CV_8UC3, new Scalar(114, 114, 114));
        resized.CopyTo(new Mat(dst, new Rect(padX, padY, resized.Width, resized.Height)));
        return dst;
    }

    private List<FaceDetection> DecodeScrfdDet10g(
        IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results,
        float scoreThreshold,
        int originalW,
        int originalH,
        float scale,
        int padX,
        int padY)
    {
        var (clsTensors, bboxTensors) = GetInsightFaceDet10gOutputs(results);
        var kpsTensors = GetInsightFaceDet10gKpsOutputs(results);
        var rawDetections = new List<FaceDetection>(capacity: clsTensors.Sum(t => t.Dimensions[0]));

        // det_10g head sizes match (W/stride * H/stride):
        // 320x240 => stride 8: 40x30=1200, stride16: 20x15=300, stride32: 10x8=80
        // With 2 anchors per cell: 2400 / 600 / 160 (matches your model outputs).
        for (var head = 0; head < clsTensors.Count; head++)
        {
            var scores = clsTensors[head];
            var boxes = bboxTensors[head];
            var kps = head < kpsTensors.Count ? kpsTensors[head] : null;

            var stride = GuessStride(scores.Dimensions[0]);
            if (stride == 0)
                continue;

            var featW = _modelInputWidth / stride;
            var featH = _modelInputHeight / stride;
            var anchorsPerCell = scores.Dimensions[0] / (featW * featH);
            if (anchorsPerCell <= 0)
                continue;

            var count = Math.Min(scores.Dimensions[0], boxes.Dimensions[0]);
            for (var idx = 0; idx < count; idx++)
            {
                var score = Sigmoid(scores[idx, 0]);
                if (score < scoreThreshold)
                    continue;

                var cellIndex = idx / anchorsPerCell;
                var xCell = cellIndex % featW;
                var yCell = cellIndex / featW;

                var cx = (xCell + 0.5f) * stride;
                var cy = (yCell + 0.5f) * stride;

                var l = boxes[idx, 0];
                var t = boxes[idx, 1];
                var r = boxes[idx, 2];
                var b = boxes[idx, 3];

                if (!float.IsFinite(l) || !float.IsFinite(t) || !float.IsFinite(r) || !float.IsFinite(b))
                    continue;

                // SCRFD bbox decode (l,t,r,b) distances relative to center.
                var x1m = cx - l * stride;
                var y1m = cy - t * stride;
                var x2m = cx + r * stride;
                var y2m = cy + b * stride;
                // Map from model input -> original image using letterbox padding/scale.
                // 'scale' parameter is combined: (letterboxScale * preScale).
                // Convert model coords -> original: x_original = (x_model - padX) / scale
                var x1f = (x1m - padX) / scale;
                var y1f = (y1m - padY) / scale;
                var x2f = (x2m - padX) / scale;
                var y2f = (y2m - padY) / scale;

                var x1 = ClampToInt(x1f, 0, originalW - 1);
                var y1 = ClampToInt(y1f, 0, originalH - 1);
                var x2 = ClampToInt(x2f, 0, originalW - 1);
                var y2 = ClampToInt(y2f, 0, originalH - 1);

                if (x2 <= x1 || y2 <= y1)
                    continue;

                IReadOnlyList<(float X, float Y)>? landmarks5 = null;
                if (kps is not null && idx < kps.Dimensions[0] && kps.Dimensions[1] >= 10)
                {
                    // Keypoints are absolute coordinates on model input (0..W/H).
                    // Map to original image coordinates using letterbox pad/scale.
                    var pts = new (float X, float Y)[5];
                    var anyFinite = false;
                    for (var p = 0; p < 5; p++)
                    {
                        var xk = kps[idx, p * 2 + 0];
                        var yk = kps[idx, p * 2 + 1];
                        if (!float.IsFinite(xk) || !float.IsFinite(yk))
                            continue;

                        anyFinite = true;

                        var xpF = (xk - padX) / scale;
                        var ypF = (yk - padY) / scale;
                        var xp = ClampToInt(xpF, 0, originalW - 1);
                        var yp = ClampToInt(ypF, 0, originalH - 1);
                        pts[p] = (xp, yp);
                    }

                    if (anyFinite)
                        landmarks5 = pts;
                }

                rawDetections.Add(new FaceDetection { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Confidence = score, Landmarks5 = landmarks5,id= LandmarkId(landmarks5) });
            }
        }

        return rawDetections;
    }

    private static List<Tensor<float>> GetInsightFaceDet10gKpsOutputs(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results)
    {
        // kps: [N,10]
        var kps = new List<(int n, Tensor<float> t)>(capacity: 3);

        foreach (var r in results)
        {
            var t = r.AsTensor<float>();
            if (t.Rank != 2)
                continue;

            var n = t.Dimensions[0];
            var d = t.Dimensions[1];
            if (d == 10)
                kps.Add((n, t));
        }

        kps.Sort((a, b) => b.n.CompareTo(a.n));
        return kps.Select(x => x.t).ToList();
    }

    private static int GuessStride(int n)
    {
        return n switch
        {
            12800 => 8,
            3200 => 16,
            800 => 32,
            _ => 0
        };
    }

    private static int ClampToInt(float value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return (int)MathF.Round(value);
    }

    private static float Sigmoid(float x)
    {
        // numerically stable sigmoid
        if (x >= 0)
        {
            var z = MathF.Exp(-x);
            return 1f / (1f + z);
        }
        else
        {
            var z = MathF.Exp(x);
            return z / (1f + z);
        }
    }

    private static Tensor<float>? TryGetTensor(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results, string name)
    {
        foreach (var r in results)
        {
            if (string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
                return r.AsTensor<float>();
        }

        return null;
    }

    private static (List<Tensor<float>> cls, List<Tensor<float>> bbox) GetInsightFaceDet10gOutputs(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results)
    {
        // We identify heads by their last dimension:
        // cls:   [N,1]
        // bbox:  [N,4]
        // kps:   [N,10]
        var cls = new List<(int n, Tensor<float> t)>(capacity: 3);
        var bbox = new List<(int n, Tensor<float> t)>(capacity: 3);

        foreach (var r in results)
        {
            var t = r.AsTensor<float>();
            if (t.Rank != 2)
                continue;

            var n = t.Dimensions[0];
            var d = t.Dimensions[1];

            if (d == 1)
                cls.Add((n, t));
            else if (d == 4)
                bbox.Add((n, t));
        }

        cls.Sort((a, b) => b.n.CompareTo(a.n));
        bbox.Sort((a, b) => b.n.CompareTo(a.n));

        if (cls.Count == 0 || bbox.Count == 0)
            throw new InvalidOperationException($"det_10g outputs were not detected. Outputs: {string.Join(", ", results.Select(r => r.Name))}\r\nModelInfo:\r\n");

        // Expected to have 3 heads; but tolerate partial.
        var headCount = Math.Min(cls.Count, bbox.Count);
        var clsTensors = cls.Take(headCount).Select(x => x.t).ToList();
        var bboxTensors = bbox.Take(headCount).Select(x => x.t).ToList();

        return (clsTensors, bboxTensors);
    }
    private static string LandmarkId(IReadOnlyList<(float X, float Y)> lm)
    {
        // Geometric (landmark-only) id: normalize by translation/scale/rotation and quantize.
        var (lEx, lEy) = lm[0];
        var (rEx, rEy) = lm[1];
        var (nx, ny) = lm[2];
        var (lMx, lMy) = lm[3];
        var (rMx, rMy) = lm[4];

        var cx = (lEx + rEx) * 0.5f;
        var cy = (lEy + rEy) * 0.5f;

        var dx = rEx - lEx;
        var dy = rEy - lEy;
        var dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist < 1e-3f)
            dist = 1e-3f;

        // rotate so eye line becomes horizontal
        var ang = -MathF.Atan2(dy, dx);
        var cos = MathF.Cos(ang);
        var sin = MathF.Sin(ang);

        (float X, float Y) Norm(float x, float y)
        {
            var tx = (x - cx) / dist;
            var ty = (y - cy) / dist;
            return (tx * cos - ty * sin, tx * sin + ty * cos);
        }

        var p0 = Norm(lEx, lEy);
        var p1 = Norm(rEx, rEy);
        var p2 = Norm(nx, ny);
        var p3 = Norm(lMx, lMy);
        var p4 = Norm(rMx, rMy);

        const float q = 0.05f; // quantization step (larger => less sensitive)
        static int Q(float v) => (int)MathF.Round(v / q);

        var key = string.Join(",",
            Q(p0.X), Q(p0.Y),
            Q(p1.X), Q(p1.Y),
            Q(p2.X), Q(p2.Y),
            Q(p3.X), Q(p3.Y),
            Q(p4.X), Q(p4.Y));

        // compact stable id
        var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash.AsSpan(0, 8)); // 16 hex chars
    }
    public void Dispose() => _session.Dispose();
}