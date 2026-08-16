using OpenCvSharp;

namespace Recognition;

public sealed class FaceIdTracker
{
    private readonly List<Entry> _entries = [];

    public int MaxHammingDistance { get; set; } = 20;
    public int MaxAverageColorDistance { get; set; } = 40;

    public Guid GetOrCreateId(Mat bgrFace)
    {
        var hash = PerceptualHash.ComputeDHash(bgrFace);
        var avg = ComputeAverageBgr(bgrFace);

        Guid? bestId = null;
        var bestDist = int.MaxValue;

        foreach (var e in _entries)
        {
            var d = PerceptualHash.HammingDistance(hash, e.Hash);
            var cd = ColorDistance(avg, e.AverageBgr);
            if (d < bestDist)
            {
                if (cd <= MaxAverageColorDistance)
                {
                    bestDist = d;
                    bestId = e.Id;
                }
            }
        }

        if (bestId is not null && bestDist <= MaxHammingDistance)
            return bestId.Value;

        var id = Guid.NewGuid();
        _entries.Add(new Entry(id, hash, avg));
        return id;
    }

    public void Clear() => _entries.Clear();

    private static Vec3f ComputeAverageBgr(Mat bgr)
    {
        using var resized = new Mat();
        Cv2.Resize(bgr, resized, new Size(32, 32));
        var mean = Cv2.Mean(resized);
        return new Vec3f((float)mean.Val0, (float)mean.Val1, (float)mean.Val2);
    }

    private static int ColorDistance(Vec3f a, Vec3f b)
    {
        var db = a.Item0 - b.Item0;
        var dg = a.Item1 - b.Item1;
        var dr = a.Item2 - b.Item2;
        return (int)MathF.Sqrt(db * db + dg * dg + dr * dr);
    }

    private sealed record Entry(Guid Id, ulong Hash, Vec3f AverageBgr);
}
