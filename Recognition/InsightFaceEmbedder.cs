using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System.Text;

namespace Recognition;

public sealed class InsightFaceEmbedder : IFaceEmbedder
{
    private const int ModelInputSize = 112;

    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly string _outputName;
    private readonly int _embeddingSize;
    private readonly string _modelInfo;

    public int EmbeddingSize => _embeddingSize;

    public InsightFaceEmbedder(string modelPath)
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException("Model file not found.", modelPath);

        _session = new InferenceSession(modelPath);
        _modelInfo = BuildModelInfo();

        _inputName = _session.InputMetadata.Keys.First();
        _outputName = _session.OutputMetadata.Keys.First();

        var dims = _session.OutputMetadata[_outputName].Dimensions;
        _embeddingSize = dims.Last() > 0 ? dims.Last() : 512;
    }
    public string GetModelInfo()
    {
        return _modelInfo;
    }

    private string BuildModelInfo()
    {
        var result = new StringBuilder();

        result.AppendLine("INPUTS:");
        foreach (var input in _session.InputMetadata)
        {
            result.AppendLine($"Name: {input.Key}");
            result.AppendLine($"Type: {input.Value.ElementType}");
            result.AppendLine($"Dimensions: {string.Join(" x ", input.Value.Dimensions)}");
            result.AppendLine();
        }

        result.AppendLine("OUTPUTS:");
        foreach (var output in _session.OutputMetadata)
        {
            result.AppendLine($"Name: {output.Key}");
            result.AppendLine($"Type: {output.Value.ElementType}");
            result.AppendLine($"Dimensions: {string.Join(" x ", output.Value.Dimensions)}");
            result.AppendLine();
        }

        return result.ToString();
    }

    public float[] GetEmbedding(Mat bgrFace)
    {
        if (bgrFace is null) throw new ArgumentNullException(nameof(bgrFace));
        if (bgrFace.Empty()) return new float[_embeddingSize];

        using var resized = new Mat();
        Cv2.Resize(bgrFace, resized, new Size(ModelInputSize, ModelInputSize));

        using var rgb = new Mat();
        Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

        // This ArcFace model expects NHWC float32: [-1,112,112,3].
        // Common preprocessing: normalize to [-1,1] using (x - 127.5) / 128.
        var input = new DenseTensor<float>(new[] { 1, ModelInputSize, ModelInputSize, 3 });
        for (var y = 0; y < ModelInputSize; y++)
        {
            for (var x = 0; x < ModelInputSize; x++)
            {
                var p = rgb.At<Vec3b>(y, x);

                // rgb is Vec3b with Item0=R, Item1=G, Item2=B
                input[0, y, x, 0] = (p.Item0 - 127.5f) / 128f;
                input[0, y, x, 1] = (p.Item1 - 127.5f) / 128f;
                input[0, y, x, 2] = (p.Item2 - 127.5f) / 128f;
            }
        }

        using var results = _session.Run(new[] { NamedOnnxValue.CreateFromTensor(_inputName, input) });
        var embedding = results.First(r => r.Name == _outputName).AsTensor<float>();

        var vec = embedding.ToArray();
        L2NormalizeInPlace(vec);
        return vec;
    }

    private static void L2NormalizeInPlace(float[] v)
    {
        double sum = 0;
        for (var i = 0; i < v.Length; i++)
            sum += v[i] * v[i];

        var norm = (float)Math.Sqrt(sum);
        if (norm <= 1e-12f) return;

        for (var i = 0; i < v.Length; i++)
            v[i] /= norm;
    }

    public void Dispose() => _session.Dispose();
}
