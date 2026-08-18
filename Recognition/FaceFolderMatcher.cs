using OpenCvSharp;
using UltraFace;
namespace Recognition;

public sealed class FaceFolderMatcher : IDisposable
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp", ".jpg", ".jpeg", ".png", ".webp"
    };

    private readonly FaceDetector _detector;
    private readonly IFaceEmbedder _embedder;

    public FaceFolderMatcher(string detectorModelPath, string recognitionModelPath)
    {
        _detector = new FaceDetector(detectorModelPath);
        _embedder = new InsightFaceEmbedder(recognitionModelPath);
    }

    public string? FindBestMatch(
        string imagePath,
        string folderPath,
        double minimumSimilarityPercent)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            throw new ArgumentException("Image path is required.", nameof(imagePath));

        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Source image was not found.", imagePath);

        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException(folderPath);

        if (minimumSimilarityPercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(minimumSimilarityPercent), "The value must be between 0 and 100.");

        var sourceFullPath = Path.GetFullPath(imagePath);
        using var source = LoadFirstFace(imagePath);
        if (source is null)
            return null;

        var sourceEmbedding = _embedder.GetEmbedding(source);
        string? bestFileName = null;
        var bestSimilarityPercent = double.MinValue;

        foreach (var candidatePath in Directory.EnumerateFiles(folderPath).Where(x=> !x.Contains("frame") && !x.Contains("probe_")))
        {
            if (!ImageExtensions.Contains(Path.GetExtension(candidatePath)) ||
                string.Equals(Path.GetFullPath(candidatePath), sourceFullPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var candidate = LoadFirstFace(candidatePath);
            if (candidate is null)
                continue;

            var candidateEmbedding = _embedder.GetEmbedding(candidate);
            var cosineSimilarity = CosineSimilarity(sourceEmbedding, candidateEmbedding);
            var similarityPercent = Math.Clamp((cosineSimilarity + 1d) * 50d, 0d, 100d);

            if (similarityPercent > bestSimilarityPercent)
            {
                bestSimilarityPercent = similarityPercent;
                bestFileName = Path.GetFileName(candidatePath);
            }
        }

        return bestSimilarityPercent >= minimumSimilarityPercent ? bestFileName : null;
    }

    private Mat? LoadFirstFace(string filePath)
    {
        using var image = Cv2.ImRead(filePath, ImreadModes.Color);
        if (image.Empty())
            return null;

        var detections = _detector.Detect(image);
        return detections.Count == 0 ? null : CropWithPadding(image, detections[0], 0.15f);
    }

    private static float CosineSimilarity(ReadOnlySpan<float> first, ReadOnlySpan<float> second)
    {
        if (first.Length != second.Length)
            throw new ArgumentException("Embedding sizes must match.");

        double dot = 0;
        double firstNorm = 0;
        double secondNorm = 0;
        for (var i = 0; i < first.Length; i++)
        {
            dot += first[i] * second[i];
            firstNorm += first[i] * first[i];
            secondNorm += second[i] * second[i];
        }

        return firstNorm <= 0 || secondNorm <= 0
            ? 0
            : (float)(dot / (Math.Sqrt(firstNorm) * Math.Sqrt(secondNorm)));
    }

    private static Mat CropWithPadding(Mat image, FaceDetection detection, float paddingRatio)
    {
        var padX = (int)MathF.Round(detection.Width * paddingRatio);
        var padY = (int)MathF.Round(detection.Height * paddingRatio);
        var x1 = Math.Max(0, detection.X1 - padX);
        var y1 = Math.Max(0, detection.Y1 - padY);
        var x2 = Math.Min(image.Width - 1, detection.X2 + padX);
        var y2 = Math.Min(image.Height - 1, detection.Y2 + padY);

        return new Mat(
            image,
            new Rect(x1, y1, Math.Max(1, x2 - x1), Math.Max(1, y2 - y1))).Clone();
    }

    public void Dispose()
    {
        _embedder.Dispose();
        _detector.Dispose();
    }
}