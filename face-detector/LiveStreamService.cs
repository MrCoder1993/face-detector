using DomainLayer;
using OpenCvSharp;
using Recognition;
using System.Drawing;

namespace face_detector;

public interface ILiveStreamService : IDisposable
{
    void Start(IReadOnlyList<SourceDto> sources, Action<int, Bitmap> frameReceived, Action<IReadOnlyList<CameraFrameProcessor.ProcessedFace>> facesDetected);
    void Stop();
    string GetFullName(string id);
    void SetFullName(string id, string fullname);
}

public sealed class LiveStreamService : ILiveStreamService
{
    private readonly CameraFrameProcessor frameProcessor;
    private readonly object processorLock = new();
    private readonly List<Task> captureTasks = new();
    private CancellationTokenSource? cancellation;

    public LiveStreamService(string baseDirectory)
    {
        frameProcessor = new CameraFrameProcessor(
            Path.Combine(baseDirectory, "Models", "34g_gnkps.onnx"),
            Path.Combine(baseDirectory, "Models", "arc.onnx"),
            baseDirectory);
    }

    public void Start(
        IReadOnlyList<SourceDto> sources,
        Action<int, Bitmap> frameReceived,
        Action<IReadOnlyList<CameraFrameProcessor.ProcessedFace>> facesDetected)
    {
        Stop();
        cancellation = new CancellationTokenSource();

        for (var index = 0; index < sources.Count; index++)
        {
            var source = sources[index];
            var sourceIndex = index;
            var token = cancellation.Token;
            captureTasks.Add(Task.Run(
                () => CaptureLoop(sourceIndex, source.Link, token, frameReceived, facesDetected),
                token));
        }
    }

    public void Stop()
    {
        var currentCancellation = cancellation;
        var currentTasks = captureTasks.ToArray();

        currentCancellation?.Cancel();
        cancellation = null;
        captureTasks.Clear();

        if (currentTasks.Length == 0)
            return;

        try
        {
            Task.WaitAll(currentTasks, TimeSpan.FromMilliseconds(500));
        }
        catch (AggregateException)
        {
        }
        catch (Exception)
        {
        }
    }

    public string GetFullName(string id) => frameProcessor.GetFullName(id);

    public void SetFullName(string id, string fullname)
    {
        lock (processorLock)
            frameProcessor.SetFullName(id, fullname);
    }

    private void CaptureLoop(
        int index,
        string source,
        CancellationToken token,
        Action<int, Bitmap> frameReceived,
        Action<IReadOnlyList<CameraFrameProcessor.ProcessedFace>> facesDetected)
    {
        using var capture = new VideoCapture();
        if (int.TryParse(source, out var cameraIndex))
            capture.Open(cameraIndex);
        else
            capture.Open(source);

        try
        {
            capture.Set(VideoCaptureProperties.BufferSize, 1);
        }
        catch
        {
        }

        if (!capture.IsOpened())
            return;

        using var frame = new Mat();
        Task? processingTask = null;
        var nextProcessingAt = 0L;

        while (!token.IsCancellationRequested)
        {
            if (!capture.Read(frame) || frame.Empty())
                continue;

            using var bitmap = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(frame);
            var nextImage = (Bitmap)bitmap.Clone();
            try
            {
                frameReceived(index, nextImage);
            }
            catch
            {
                nextImage.Dispose();
                break;
            }

            var now = Environment.TickCount64;
            if (now < nextProcessingAt || processingTask is { IsCompleted: false })
                continue;

            var processFrame = frame.Clone();
            processingTask = Task.Run(() => ProcessFrame(
                processFrame, token, facesDetected), token);
            nextProcessingAt = now + 1000;
        }
    }

    private void ProcessFrame(
        Mat frame,
        CancellationToken token,
        Action<IReadOnlyList<CameraFrameProcessor.ProcessedFace>> facesDetected)
    {
        using (frame)
        {
            if (token.IsCancellationRequested)
                return;

            try
            {
                lock (processorLock)
                {
                    if (token.IsCancellationRequested)
                        return;

                    var faces = frameProcessor.Process(frame).ToList();
                    facesDetected(faces);
                }
            }
            catch
            {
            }
        }
    }

    public void Dispose()
    {
        Stop();
        frameProcessor.Dispose();
    }
}
