using PersianDate.Standard;

namespace face_detector;

public interface IFaceGalleryService
{
    IReadOnlyList<string> GetImageFiles();
    Image? LoadImageCopy(string path);
    string GetPersianCreationDate(string path);
}

public sealed class FaceGalleryService : IFaceGalleryService
{
    private readonly string directory;

    public FaceGalleryService(string baseDirectory)
    {
        directory = Path.Combine(baseDirectory, "Faces");
        Directory.CreateDirectory(directory);
    }

    public IReadOnlyList<string> GetImageFiles()
    {
        return Directory.EnumerateFiles(directory)
            .Where(path => IsFaceImage(path) && !Path.GetFileName(path).StartsWith(".probe_", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Image? LoadImageCopy(string path)
    {
        try
        {
            using var source = Image.FromFile(path);
            return new Bitmap(source);
        }
        catch
        {
            return null;
        }
    }

    public string GetPersianCreationDate(string path)
    {
        return ConvertDate.ToFa(
            File.GetCreationTime(path),
            "yyyy/MM/dd HH:mm");
    }

    private static bool IsFaceImage(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }
}
