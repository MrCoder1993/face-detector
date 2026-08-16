using OpenCvSharp;

namespace Recognition;

public static class PerceptualHash
{
    public const int DHashWidth = 9;
    public const int DHashHeight = 8;

    public static ulong ComputeDHash(Mat bgrFace)
    {
        if (bgrFace is null) throw new ArgumentNullException(nameof(bgrFace));
        if (bgrFace.Empty()) return 0;

        using var gray = new Mat();
        Cv2.CvtColor(bgrFace, gray, ColorConversionCodes.BGR2GRAY);

        using var resized = new Mat();
        Cv2.Resize(gray, resized, new Size(DHashWidth, DHashHeight));

        ulong hash = 0;
        var bit = 0;
        for (var y = 0; y < DHashHeight; y++)
        {
            for (var x = 0; x < DHashWidth - 1; x++)
            {
                var left = resized.At<byte>(y, x);
                var right = resized.At<byte>(y, x + 1);

                if (left > right)
                    hash |= 1UL << bit;

                bit++;
            }
        }

        return hash;
    }

    public static int HammingDistance(ulong a, ulong b)
    {
        var x = a ^ b;
        var count = 0;
        while (x != 0)
        {
            x &= x - 1;
            count++;
        }
        return count;
    }
}
