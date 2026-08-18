using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System.Text;

namespace UltraFace;

public sealed class InsightFaceGenderAgeEstimator : IDisposable
{
    private const int ModelInputSize = 96;

    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly string _outputName;

    public InsightFaceGenderAgeEstimator(string modelPath)
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException("Model file not found.", modelPath);

        _session = new InferenceSession(modelPath);

        _inputName = _session.InputMetadata.ContainsKey("data")
            ? "data"
            : _session.InputMetadata.Keys.First();

        _outputName = _session.OutputMetadata.ContainsKey("fc1")
            ? "fc1"
            : _session.OutputMetadata.Keys.First();
        BuildModelInfo();
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

    public GenderAge Predict(Mat bgrFace)
    {
        if (bgrFace is null) throw new ArgumentNullException(nameof(bgrFace));
        if (bgrFace.Empty()) return new GenderAge(Gender.Unknown, 0);

        using var resized = new Mat();
        Cv2.Resize(bgrFace, resized, new Size(ModelInputSize, ModelInputSize));

        using var rgb = new Mat();
        Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

        var input = new DenseTensor<float>(new[] { 1, 3, ModelInputSize, ModelInputSize });

        // Common preprocessing for InsightFace genderage: normalize to [-1,1]
        for (var y = 0; y < ModelInputSize; y++)
        {
            for (var x = 0; x < ModelInputSize; x++)
            {
                var p = rgb.At<Vec3b>(y, x);
                input[0, 0, y, x] = (p.Item0 / 255f - 0.5f) / 0.5f;
                input[0, 1, y, x] = (p.Item1 / 255f - 0.5f) / 0.5f;
                input[0, 2, y, x] = (p.Item2 / 255f - 0.5f) / 0.5f;
            }
        }

        using var results = _session.Run(new[] { NamedOnnxValue.CreateFromTensor(_inputName, input) });

        var outTensor = results.First(r => string.Equals(r.Name, _outputName, StringComparison.OrdinalIgnoreCase))
            .AsTensor<float>();

        if (outTensor.Length < 3)
            return new GenderAge(Gender.Unknown, 0);

        // Common export: [female_score, male_score, age]
        // Observed output example: [0.206, -0.206, 0.35]
        // This appears to be logits for 2 classes + an age-like value.
        var s0 = outTensor.GetValue(0);
        var s1 = outTensor.GetValue(1);
        var ageVal = outTensor.GetValue(2);

        // If the model always yields the same sign pattern, the preprocessing might be wrong.
        // For now: treat greater logit as predicted class. Map by typical order: [female, male].
        var gender = s1 >= s0 ? Gender.Male : Gender.Female;

        var age = (int)MathF.Round(ageVal <= 1.0f ? (ageVal * 100f) : ageVal);
        age = (int)Math.Clamp(age, 0, 120);

        return new GenderAge(gender, age);
    }

    public float[] PredictRaw(Mat bgrFace)
    {
        if (bgrFace is null) throw new ArgumentNullException(nameof(bgrFace));
        if (bgrFace.Empty()) return [];

        using var resized = new Mat();
        Cv2.Resize(bgrFace, resized, new Size(ModelInputSize, ModelInputSize));

        using var rgb = new Mat();
        Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

        // Try two common normalizations and return the one with stronger separation between the first two logits.
        var outA = RunWithNormalization(rgb, Normalization.Minus1To1);
        var outB = RunWithNormalization(rgb, Normalization.ZeroTo1);

        if (outA.Length >= 2 && outB.Length >= 2)
        {
            var sepA = MathF.Abs(outA[0] - outA[1]);
            var sepB = MathF.Abs(outB[0] - outB[1]);
            return sepB > sepA ? outB : outA;
        }

        return outA.Length > 0 ? outA : outB;
    }

    private enum Normalization
    {
        Minus1To1,
        ZeroTo1
    }

    private float[] RunWithNormalization(Mat rgb96, Normalization normalization)
    {
        var input = new DenseTensor<float>(new[] { 1, 3, ModelInputSize, ModelInputSize });
        for (var y = 0; y < ModelInputSize; y++)
        {
            for (var x = 0; x < ModelInputSize; x++)
            {
                var p = rgb96.At<Vec3b>(y, x);

                var r = p.Item0 / 255f;
                var g = p.Item1 / 255f;
                var b = p.Item2 / 255f;

                if (normalization == Normalization.Minus1To1)
                {
                    r = (r - 0.5f) / 0.5f;
                    g = (g - 0.5f) / 0.5f;
                    b = (b - 0.5f) / 0.5f;
                }

                input[0, 0, y, x] = r;
                input[0, 1, y, x] = g;
                input[0, 2, y, x] = b;
            }
        }

        using var results = _session.Run(new[] { NamedOnnxValue.CreateFromTensor(_inputName, input) });
        var outTensor = results.First(r => string.Equals(r.Name, _outputName, StringComparison.OrdinalIgnoreCase))
            .AsTensor<float>();
        return outTensor.ToArray();
    }

    public void Dispose() => _session.Dispose();
}

public enum Gender
{
    Unknown = 0,
    Female = 1,
    Male = 2
}

public readonly record struct GenderAge(Gender Gender, int Age);
