namespace UltraFace;

public sealed class FaceDetection
{
    public int X1 { get; init; }
    public int Y1 { get; init; }
    public int X2 { get; init; }
    public int Y2 { get; init; }

    public float Confidence { get; init; }

    public int Width => X2 - X1;
    public int Height => Y2 - Y1;
}