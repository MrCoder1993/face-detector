using OpenCvSharp;
using UltraFace;

namespace Recognition;

public sealed class CameraFrameProcessor : IDisposable
{
    private const float ScoreThreshold = 0.65f;
    private const float NmsThreshold = 0.4f;
    private const double MatchThresholdPercent = 70;

    private readonly FaceDetector _detector;
    private readonly FaceFolderMatcher _folderMatcher;
    private readonly HashSet<string> _seenIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _facesDirectory;
    private readonly string _framesDirectory;
    public List<ProcessedFace> newFaces = new List<ProcessedFace>();
    public CameraFrameProcessor(
        string detectorModelPath,
        string recognitionModelPath,
        string baseDirectory)
    {
        _detector = new FaceDetector(detectorModelPath);
        _folderMatcher = new FaceFolderMatcher(detectorModelPath, recognitionModelPath);
        _facesDirectory = Path.Combine(baseDirectory, "Faces");
        _framesDirectory = Path.Combine(baseDirectory, "Frames");
        Directory.CreateDirectory(_facesDirectory);
    }

    public IReadOnlyList<ProcessedFace> Process(Mat frame)
    {
        newFaces.Clear();

        if (frame is null || frame.Empty())
            return [];

       
        var detections = _detector.Detect(frame, ScoreThreshold, NmsThreshold);
        foreach (var detection in detections)
        {
            var rect = new Rect(detection.X1, detection.Y1, detection.X2 - detection.X1, detection.Y2 - detection.Y1);
            if (rect.Width <= 0 || rect.Height <= 0)
                continue;

            using var faceCrop = CropWithPadding(frame, detection, GetDynamicPaddingRatio(frame, detection));
            var id = ResolveFaceId(faceCrop, detection.id);
            newFaces.Add(new ProcessedFace(id, rect, detection.Landmarks5));

            SaveFaceSnapshot(id, faceCrop, frame, rect);
            if (!_seenIds.Add(id))
                continue;

        }

        return newFaces;
    }

    public sealed record ProcessedFace(
        string Id,
        Rect Bounds,
        IReadOnlyList<(float X, float Y)>? Landmarks5,string fullname="");

    private string ResolveFaceId(Mat faceCrop, string detectorId)
    {
        var probePath = Path.Combine(_facesDirectory, $".probe_{Guid.NewGuid():N}.jpg");
        try
        {
            if (!Cv2.ImWrite(probePath, faceCrop))
                return detectorId;
            var bestMatch = _folderMatcher.FindBestMatch(
                probePath,
                _facesDirectory,
                MatchThresholdPercent);
            if (bestMatch != null)
                _seenIds.Add(bestMatch);

            return (bestMatch ?? detectorId).Replace("probe_", "").Split(".")[0];
        }
        finally
        {
            try
            {
                File.Delete(probePath);
                foreach (var item in Directory.EnumerateFiles(_facesDirectory).Where(x => x.Contains(".probe_")).ToList())
                {
                    File.Delete(item);
                }

            }
            catch { }
        }
    }

    private static Mat CropWithPadding(Mat frame, FaceDetection detection, float paddingRatio)
    {
        var padX = (int)MathF.Round(detection.Width * paddingRatio);
        var padY = (int)MathF.Round(detection.Height * paddingRatio);
        var x1 = Math.Max(0, detection.X1 - padX);
        var y1 = Math.Max(0, detection.Y1 - padY);
        var x2 = Math.Min(frame.Width - 1, detection.X2 + padX);
        var y2 = Math.Min(frame.Height - 1, detection.Y2 + padY);

        return new Mat(frame, new Rect(x1, y1, Math.Max(1, x2 - x1), Math.Max(1, y2 - y1))).Clone();
    }

    private static float GetDynamicPaddingRatio(Mat frame, FaceDetection detection)
    {
        var frameArea = Math.Max(1d, (double)frame.Width * frame.Height);
        var faceArea = Math.Max(1d, (double)detection.Width * detection.Height);
        var ratio = faceArea / frameArea;
        const float maxPadding = 0.90f;
        const float minPadding = 0.15f;
        const float smallFace = 0.002f;
        const float largeFace = 0.03f;

        if (ratio <= smallFace) return maxPadding;
        if (ratio >= largeFace) return minPadding;

        var t = (float)((ratio - smallFace) / (largeFace - smallFace));
        return maxPadding + (minPadding - maxPadding) * t;
    }

    private void SaveFaceSnapshot(string id, Mat faceCrop, Mat frame, Rect faceRect)
    {
        try
        {
            Cv2.ImWrite(Path.Combine(_facesDirectory, $"{id}.jpg"), faceCrop);
            using var annotated = frame.Clone();
            var x = Math.Clamp(faceRect.X - 15, 0, Math.Max(0, annotated.Width - 1));
            var y = Math.Clamp(faceRect.Y, 0, Math.Max(0, annotated.Height - 1));
            var w = Math.Clamp(faceRect.Width + 15, 1, annotated.Width - x);
            var h = Math.Clamp(faceRect.Height, 1, annotated.Height - y);
            var clamped = new Rect(x, y, w, h);
            Cv2.Rectangle(annotated, clamped, Scalar.DodgerBlue, 2);
            Cv2.PutText(annotated, id, new Point(clamped.Left, Math.Max(clamped.Y - 10, 10)), HersheyFonts.HersheySimplex, 0.8, Scalar.DodgerBlue, 2);
            Cv2.ImWrite(Path.Combine(_framesDirectory, $"{id}.jpg"), annotated);
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        _folderMatcher.Dispose();
        _detector.Dispose();
    }
}