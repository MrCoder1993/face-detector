using OpenCvSharp;
using Recognition;

public sealed class FaceTracker
{
    private readonly IFaceEmbedder _embedder;

    private readonly Dictionary<Guid, float[]> _faces = new();

    // Keep only a bounded number of identities to reduce false matches / memory growth.
    private readonly Queue<Guid> _insertionOrder = new();
    private readonly int _maxFaces = 50;

    // ArcFace cosine similarity
    private readonly float _similarityThreshold = 0.68f;

    // If best and second-best are too close, treat as ambiguous and create a new ID.
    // This helps when 2+ faces are in view and embeddings can be similar.
    private readonly float _minMargin = 0.03f;

    // Helps prevent ID explosion from noisy detections by requiring a few consecutive
    // frames before accepting a brand new identity.
    private readonly int _minStableFramesForNewId = 8;
    private readonly Dictionary<int, (float[] Embedding, int Count)> _pendingNew = new();
    private readonly int _maxPending = 3;


    public FaceTracker(IFaceEmbedder embedder)
    {
        _embedder = embedder;
    }

    public string GetSimHashId(Mat faceCrop)
    {
        if (faceCrop.Empty())
            throw new ArgumentException("Face crop is empty");

        var embedding = _embedder.GetEmbedding(faceCrop);

        // If we have seen a similar embedding before, reuse its SimHash id.
        // This prevents minor variations from producing a brand-new id.
        const float reuseThreshold = 0.70f;
        string? bestId = null;
        var bestSim = float.NegativeInfinity;
        foreach (var face in _faces)
        {
            var sim = CosineSimilarity(embedding, face.Value);
            if (sim > bestSim)
            {
                bestSim = sim;
                bestId = face.Key.ToString("N");
            }
        }

        if (bestId is not null && bestSim >= reuseThreshold)
            return bestId;

        var simId = SimHash64(embedding).ToString("X16");

        // Store this embedding under a new Guid so subsequent frames can be matched.
        var newId = Guid.NewGuid();
        _faces[newId] = embedding;
        _insertionOrder.Enqueue(newId);
        TrimOldFaces();

        return simId;
    }

    private static ulong SimHash64(float[] v)
    {
        // 64-bit SimHash via random hyperplanes derived from feature index.
        // Deterministic and fast; works well with L2-normalized embeddings.
        Span<float> acc = stackalloc float[64];

        for (var i = 0; i < v.Length; i++)
        {
            var x = v[i];
            if (x == 0) continue;

            var h = SplitMix64((ulong)i + 0x9E3779B97F4A7C15UL);
            for (var b = 0; b < 64; b++)
            {
                var w = ((h >> b) & 1UL) == 0 ? -1f : 1f;
                acc[b] += x * w;
            }
        }

        ulong sig = 0;
        for (var b = 0; b < 64; b++)
        {
            if (acc[b] >= 0)
                sig |= 1UL << b;
        }

        return sig;
    }

    private static ulong SplitMix64(ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        return x ^ (x >> 31);
    }


    public Guid GetOrCreateId(Mat faceCrop)
    {
        if (faceCrop.Empty())
            throw new ArgumentException("Face crop is empty");


        // 1- Create embedding
        var embedding = _embedder.GetEmbedding(faceCrop);


        // 2- Search existing identities (best-match + margin)
        Guid bestId = default;
        var bestSim = float.NegativeInfinity;
        var secondBestSim = float.NegativeInfinity;

        foreach (var face in _faces)
        {
            var sim = CosineSimilarity(embedding, face.Value);

            if (sim > bestSim)
            {
                secondBestSim = bestSim;
                bestSim = sim;
                bestId = face.Key;
            }
            else if (sim > secondBestSim)
            {
                secondBestSim = sim;
            }
        }



        if (_faces.Count > 0 && bestSim >= _similarityThreshold && (bestSim - secondBestSim) >= _minMargin)
        {
            _faces[bestId] = Blend(_faces[bestId], embedding, 0.90f);
            return bestId;
        }


        // 3- New person (stabilize before committing)
        if (TryGetStablePendingId(embedding, out var pendingId))
            return pendingId;

        return Guid.Empty;
    }


    private bool TryGetStablePendingId(float[] embedding, out Guid id)
    {
        id = Guid.Empty;

        // Find best pending candidate
        var bestKey = -1;
        var bestSim = float.NegativeInfinity;
        foreach (var kv in _pendingNew)
        {
            var sim = CosineSimilarity(embedding, kv.Value.Embedding);
            if (sim > bestSim)
            {
                bestSim = sim;
                bestKey = kv.Key;
            }
        }

        // Update existing pending candidate if similar enough
        if (bestKey >= 0 && bestSim >= _similarityThreshold)
        {
            var current = _pendingNew[bestKey];
            var nextEmbedding = Blend(current.Embedding, embedding, 0.85f);
            var nextCount = current.Count + 1;
            _pendingNew[bestKey] = (nextEmbedding, nextCount);

            if (nextCount >= _minStableFramesForNewId)
            {
                var newId = Guid.NewGuid();
                _faces[newId] = nextEmbedding;
                _insertionOrder.Enqueue(newId);
                TrimOldFaces();
                _pendingNew.Remove(bestKey);
                id = newId;
                return true;
            }

            return false;
        }

        // Create a new pending slot
        if (_pendingNew.Count >= _maxPending)
            return false;

        var key = _pendingNew.Count == 0 ? 0 : (_pendingNew.Keys.Max() + 1);
        _pendingNew[key] = (embedding, 1);
        return false;
    }



    private static float CosineSimilarity(
        float[] a,
        float[] b)
    {
        if (a.Length != b.Length)
            return 0;


        float dot = 0;
        float normA = 0;
        float normB = 0;


        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];

            normA += a[i] * a[i];

            normB += b[i] * b[i];
        }


        if (normA == 0 || normB == 0)
            return 0;


        return dot /
            (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }


    private static float[] Blend(float[] existing, float[] incoming, float keepExisting)
    {
        if (existing.Length != incoming.Length)
            return incoming;

        var result = new float[existing.Length];
        var keepIncoming = 1f - keepExisting;
        for (var i = 0; i < existing.Length; i++)
            result[i] = (existing[i] * keepExisting) + (incoming[i] * keepIncoming);

        return result;
    }



    public void Clear()
    {
        _faces.Clear();
        _insertionOrder.Clear();
        _pendingNew.Clear();
    }


    private void TrimOldFaces()
    {
        while (_faces.Count > _maxFaces && _insertionOrder.Count > 0)
        {
            var id = _insertionOrder.Dequeue();
            _faces.Remove(id);
        }
    }
}