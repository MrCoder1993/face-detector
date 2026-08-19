using OpenCvSharp;
using DomainLayer;

namespace face_detector;

public interface IWebcamDiscoveryService
{
    IReadOnlyList<ComboBoxItem> Find();
}

public sealed class WebcamDiscoveryService : IWebcamDiscoveryService
{
    public IReadOnlyList<ComboBoxItem> Find()
    {
        var webcams = new List<ComboBoxItem>();
        for (var index = 0; index < 10; index++)
        {
            using var capture = new VideoCapture(index);
            if (capture.IsOpened())
                webcams.Add(new ComboBoxItem($"Webcam {index}", index.ToString()));
        }

        return webcams;
    }
}
