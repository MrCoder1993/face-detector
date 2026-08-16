using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System.Text;

namespace UltraFace;

public sealed class FaceDetector : IDisposable
{
    private const int ModelInputWidth = 320;
    private const int ModelInputHeight = 240;

    private readonly InferenceSession _session;

    public FaceDetector(string modelPath)
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException("Model file not found.", modelPath);

        _session = new InferenceSession(modelPath);
    }

    public string GetModelInfo()
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
        float scoreThreshold = 0.7f,
        float nmsThreshold = 0.3f)
    {
        if (bgrImage is null) throw new ArgumentNullException(nameof(bgrImage));
        if (bgrImage.Empty()) return [];

        var originalW = bgrImage.Width;
        var originalH = bgrImage.Height;

        using var resized = new Mat();
        Cv2.Resize(bgrImage, resized, new Size(ModelInputWidth, ModelInputHeight));

        using var rgb = new Mat();
        Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

        // tensor: [1,3,240,320]
        var inputTensor = new DenseTensor<float>([1, 3, ModelInputHeight, ModelInputWidth]);

        // Normalize: (pixel - 127) / 128
        for (var y = 0; y < ModelInputHeight; y++)
        {
            for (var x = 0; x < ModelInputWidth; x++)
            {
                var pixel = rgb.At<Vec3b>(y, x); // RGB
                inputTensor[0, 0, y, x] = (pixel.Item0 - 127f) / 128f; // R
                inputTensor[0, 1, y, x] = (pixel.Item1 - 127f) / 128f; // G
                inputTensor[0, 2, y, x] = (pixel.Item2 - 127f) / 128f; // B
            }
        }

        var inputName = _session.InputMetadata.Keys.Single(); // "input"
        var inputs = new[]
        {
            NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
        };

        using var results = _session.Run(inputs);

        var scores = results.First(r => r.Name == "scores").AsTensor<float>(); // [1,4420,2]
        var boxes = results.First(r => r.Name == "boxes").AsTensor<float>();   // [1,4420,4]

        var rawDetections = new List<FaceDetection>(capacity: scores.Dimensions[1]);

        var count = scores.Dimensions[1]; // 4420
        for (var i = 0; i < count; i++)
        {
            var faceScore = scores[0, i, 1];
            if (faceScore < scoreThreshold)
                continue;

            var x1n = boxes[0, i, 0];
            var y1n = boxes[0, i, 1];
            var x2n = boxes[0, i, 2];
            var y2n = boxes[0, i, 3];

            // Convert normalized -> original pixels (clamped)
            var x1 = ClampToInt(x1n * originalW, 0, originalW - 1);
            var y1 = ClampToInt(y1n * originalH, 0, originalH - 1);
            var x2 = ClampToInt(x2n * originalW, 0, originalW - 1);
            var y2 = ClampToInt(y2n * originalH, 0, originalH - 1);

            // Ensure proper ordering
            if (x2 <= x1 || y2 <= y1)
                continue;

            rawDetections.Add(new FaceDetection
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Confidence = faceScore
            });
        }

        var kept = Nms.Run(rawDetections, nmsThreshold);
        return kept.Select(i => rawDetections[i]).ToArray();
    }

    private static int ClampToInt(float value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return (int)MathF.Round(value);
    }

    public void Dispose() => _session.Dispose();
}