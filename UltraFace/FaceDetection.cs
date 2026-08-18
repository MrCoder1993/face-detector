namespace UltraFace;

public sealed class FaceDetection
{
    public int X1 { get; init; }
    public int Y1 { get; init; }
    public int X2 { get; init; }
    public int Y2 { get; init; }
    public string id { get; set; }

    public float Confidence { get; init; }

    // 5-point landmarks (x,y) in image coordinates: left eye, right eye, nose, left mouth, right mouth.
    // Present only when the model provides keypoints.
    public IReadOnlyList<(float X, float Y)>? Landmarks5 { get; init; }

    public bool HasLandmarks5 => Landmarks5 is { Count: 5 };

    public int Width => X2 - X1;
    public int Height => Y2 - Y1;
}