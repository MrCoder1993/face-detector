using OpenCvSharp;

namespace UltraFace;

public static class FaceDetectionExtensions
{
    public static Point2f Center(this FaceDetection d)
        => new((d.X1 + d.X2) / 2f, (d.Y1 + d.Y2) / 2f);

    public static float DistanceTo(this FaceDetection a, FaceDetection b)
    {
        var ac = a.Center();
        var bc = b.Center();
        var dx = ac.X - bc.X;
        var dy = ac.Y - bc.Y;
        return MathF.Sqrt(dx * dx + dy * dy); // pixels
    }
}