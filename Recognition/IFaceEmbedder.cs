using OpenCvSharp;

namespace Recognition;

public interface IFaceEmbedder : IDisposable
{
    int EmbeddingSize { get; }
    float[] GetEmbedding(Mat bgrFace);
    string GetModelInfo();
}
