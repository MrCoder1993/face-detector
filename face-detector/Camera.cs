using DomainLayer;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using Recognition;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using UltraFace;

namespace face_detector
{
    public partial class Camera : Form
    {
        private CameraFrameProcessor? _frameProcessor;
        private CancellationTokenSource? _cts;
        private Task? _loopTask;
         private Mat? _latestFrameForProcessing;
        private VideoCapture? _capture;
        //private IFaceEmbedder? _embedder; 
        private Task? _procTask;

        private int _lastReportedW;
        private int _lastReportedH;




        public Camera()
        {
            InitializeComponent();

            //var detModelPath = Path.Combine(AppContext.BaseDirectory, "Models", "10g_bnkps.onnx");
            var detModelPath = ResolveModelPath();
            var recModelPath = Path.Combine(AppContext.BaseDirectory, "Models", "arc.onnx");
            //var genderAgeModelPath = Path.Combine(AppContext.BaseDirectory, "Models", "genderage.onnx");

            _frameProcessor = new CameraFrameProcessor(
                detModelPath,
                recModelPath,
                Path.Combine(AppContext.BaseDirectory));
        }




        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try { _frameProcessor?.Dispose(); } catch { }
            Application.Exit();
            base.OnFormClosed(e);
        }


        private void Camera_Load(object sender, EventArgs e)
        {

            RefreshSources();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

        }
        private void Camera_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopCapture();
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            ProcessFrame(_cts.Token);
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            StartCapture();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            StopCapture();
        }

        private void RefreshSources()
        {
            var current = cmbSources.SelectedItem as ComboBoxItem;
            cmbSources.BeginUpdate();
            try
            {
                cmbSources.Items.Clear();

                for (var i = 0; i < 10; i++)
                {
                    using var cap = new VideoCapture(i);
                    if (cap.IsOpened())
                        cmbSources.Items.Add(new ComboBoxItem($"Webcam {i}", i.ToString()));
                }

                if (cmbSources.Items.Count == 0)
                    cmbSources.Items.Add(new ComboBoxItem("No webcams found", "-1"));

                if (current is not null)
                {
                    var match = cmbSources.Items.Cast<ComboBoxItem>().FirstOrDefault(x => x.Value == current.Value);
                    cmbSources.SelectedItem = match ?? cmbSources.Items[0];
                }
                else
                {
                    cmbSources.SelectedIndex = 0;
                }
            }
            finally
            {
                cmbSources.EndUpdate();
            }
        }

        private void StartCapture()
        {
            if (_cts is not null) return;

            var source = GetSelectedSource();
            if (string.IsNullOrWhiteSpace(source))
            {
                MessageBox.Show(this, "Select a camera source or enter a URL.", "Face Detector", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }


            try
            {
                //if (_detector is null || _embedder is null)
                //    throw new InvalidOperationException("Detector/embedder not initialized.");

                _capture = new VideoCapture();

                if (int.TryParse(source, out var camIndex))
                    _capture.Open(camIndex);
                else
                    _capture.Open(source);

                // Reduce internal buffering/latency for RTSP streams and improve UI responsiveness.
                // (No-op on some backends.)
                try
                {
                    _capture.Set(VideoCaptureProperties.BufferSize, 1);
                }
                catch { }

                if (!_capture.IsOpened())
                    throw new InvalidOperationException($"Cannot open capture: {source}");

                // Initialize first frame so UI can show something immediately.
                using (var first = new Mat())
                {
                    if (_capture.Read(first) && !first.Empty())
                    {
                        using var bmp = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(first);
                        SetPreviewImage((Bitmap)bmp.Clone());

                        try
                        {
                            var dir = Path.Combine(AppContext.BaseDirectory, "Frames");
                            Directory.CreateDirectory(dir);
                            var path = Path.Combine(dir, $"first_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg");
                            using var tmp = first.Clone();
                            Cv2.ImWrite(path, tmp);
                        }
                        catch { }
                    }
                }

                btnStart.Enabled = false;
                btnStop.Enabled = true;
                btnRefresh.Enabled = true;
                cmbSources.Enabled = false;
                txtUrl.Enabled = false;

                _cts = new CancellationTokenSource();
                _loopTask = Task.Factory.StartNew(
                    () => CaptureLoop(_cts.Token),
                    _cts.Token,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
                _procTask = Task.Factory.StartNew(
     () => ProcessFrame(_cts.Token),
     _cts.Token,
     TaskCreationOptions.LongRunning,
     TaskScheduler.Default).Unwrap();

            }
            catch (Exception ex)
            {
                StopCapture();
                MessageBox.Show(this, ex.Message, "Face Detector", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopCapture()
        {
            var cts = _cts;
            _cts = null;

            var cap = _capture;
            var loop = _loopTask;

            if (cts != null)
            {
                cts.Cancel();

                try
                {
                    // Avoid freezing UI if capture thread doesn't stop immediately.
                    loop?.Wait(1000);
                }
                catch { }

                cts.Dispose();
            }

            _loopTask = null;
            //_tracker.Clear();



            // Ensure capture is released to unblock any pending Read() call.
            // CaptureLoop always reads from a local reference of _capture, so null it first.
            _capture = null;
            try { cap?.Release(); } catch { }
            try { cap?.Dispose(); } catch { }


            // Don't dispose detector/embedder here; they are reused for the next Start.

            btnStart.Enabled = true;
            btnStop.Enabled = false;
            btnRefresh.Enabled = false;
            cmbSources.Enabled = true;
            txtUrl.Enabled = true;

        }

        private void CaptureLoop(CancellationToken ct)
        {
            using var frame = new Mat();


            while (!ct.IsCancellationRequested)
            {
                if (_capture is null || _frameProcessor is null)
                    break;

                // Keep a local reference to avoid races with StopCapture nulling _capture.
                var cap = _capture;
                if (cap is null)
                    break;


                if (!cap.Read(frame) || frame.Empty())
                {
                    //Thread.Sleep(30);
                    continue;
                }

                if (frame.Width != _lastReportedW || frame.Height != _lastReportedH)
                {
                    _lastReportedW = frame.Width;
                    _lastReportedH = frame.Height;
                    try
                    {
                        BeginInvoke((Action)(() => Text = $"Camera ({_lastReportedW}x{_lastReportedH})"));
                    }
                    catch { }
                }
                _latestFrameForProcessing = frame;

                try
                {
                    var bmp = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(frame);
                    BeginInvoke((Action)(() =>
                    {
                        picturePreview.Image?.Dispose();
                        picturePreview.Image = bmp;
                    }));
                }
                catch
                {
                    // ignore preview failures
                }


            }
        }




        private async Task ProcessFrame(CancellationToken ct)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));

            try
            {
                while (await timer.WaitForNextTickAsync(ct))
                {
                    try
                    {
                        var frame = _latestFrameForProcessing;
                        if (frame is null || frame.Empty() || _frameProcessor is null)
                            continue;

                        await Task.Run(() =>
                        {
                            foreach (var face in _frameProcessor.Process(frame))
                                AddFaceToList(face.Id);
                        }, ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        break;
                    }
                    catch
                    {
                        // ignore per-tick failures
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when capture is stopped.
            }
            catch
            {
                // ignore per-tick failures
            }
        }

        private void AddFaceToList(string label)
        {
            if (IsDisposed)
                return;

            BeginInvoke((Action)(() =>
            {
                if (!lstFaces.Items.Contains(label))
                    lstFaces.Items.Add(label);
            }));
        }

        private void SetPreviewImage(Bitmap next)
        {
            // Keep method for initial frame display; push to latest-frame buffer.
            if (IsDisposed)
            {
                next.Dispose();
                return;
            }

        }

        private string? GetSelectedSource()
        {
            var url = txtUrl.Text?.Trim();

            if (!string.IsNullOrWhiteSpace((comboCamera.SelectedItem as string)))
                return (comboCamera.SelectedItem as string);
            if (!string.IsNullOrWhiteSpace(url))
                return url;

            return (cmbSources.SelectedItem as ComboBoxItem)?.Value;
        }

        private string? ResolveModelPath()
        {
            // Prefer a local "model.onnx" next to the app, fallback to project root if running from bin\Debug.
            var baseDir = AppContext.BaseDirectory;
            var candidates = new[]
            {

            Path.Combine(AppContext.BaseDirectory, "Models", "34g_gnkps.onnx")
            };

            var modelPath = candidates.FirstOrDefault(File.Exists);
            if (modelPath is not null)
                return modelPath;

            MessageBox.Show(this,
                "model.onnx was not found. Copy the model file next to the .exe (bin folder) or the project root.",
                "Face Detector",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return null;
        }



     

        private void lstFaces_Click(object sender, EventArgs e)
        {
            if (!String.IsNullOrEmpty(lstFaces.Text))
            {
                var dir = Path.Combine(AppContext.BaseDirectory, "Faces");
                Directory.CreateDirectory(dir);
                var framePath = Path.Combine(dir, $"frame_{lstFaces.Text}.jpg");
                picturePreview.Image = Image.FromFile(framePath);
                picturePreview.SizeMode = PictureBoxSizeMode.Zoom;
            }
        }
    }
}
