namespace Recognition;

internal static class VectorMath
{
    public static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vector lengths must match.");

        double dot = 0;
        double na = 0;
        double nb = 0;

        for (var i = 0; i < a.Length; i++)
        {
            var av = a[i];
            var bv = b[i];
            dot += av * bv;
            na += av * av;
            nb += bv * bv;
        }

        if (na <= 0 || nb <= 0)
            return 0;

        return (float)(dot / (Math.Sqrt(na) * Math.Sqrt(nb)));
    }

    public static void L2NormalizeInPlace(Span<float> v)
    {
        double sum = 0;
        for (var i = 0; i < v.Length; i++)
            sum += v[i] * v[i];

        if (sum <= 0)
            return;

        var inv = (float)(1.0 / Math.Sqrt(sum));
        for (var i = 0; i < v.Length; i++)
            v[i] *= inv;
    }
}
