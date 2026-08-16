using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
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
        private readonly object _bitmapLock = new();
        private CancellationTokenSource? _cts;
        private Task? _loopTask;
        private VideoCapture? _capture;
        private FaceDetector? _detector;

        private const float DefaultScoreThreshold = 0.4f;
        private const float DefaultNmsThreshold = 0.7f;
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Application.Exit();
            base.OnFormClosed(e);
        }
        public Camera()
        {
            InitializeComponent();
        }

        private void Camera_Load(object sender, EventArgs e)
        {
            RefreshSources();
        }

        private void Camera_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopCapture();
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            RefreshSources();
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

                // OpenCV doesn't reliably enumerate device names on Windows.
                // We probe a range of indices and list the ones that open.
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

            var modelPath = ResolveModelPath();
            if (modelPath is null)
                return;

            try
            {
                _detector = new FaceDetector(modelPath);
                _capture = new VideoCapture();

                if (int.TryParse(source, out var camIndex))
                    _capture.Open(camIndex);
                else
                    _capture.Open(source);

                if (!_capture.IsOpened())
                    throw new InvalidOperationException($"Cannot open capture: {source}");

                btnStart.Enabled = false;
                btnStop.Enabled = true;
                btnRefresh.Enabled = false;
                cmbSources.Enabled = false;
                txtUrl.Enabled = false;

                _cts = new CancellationTokenSource();
                _loopTask = Task.Run(() => CaptureLoop(_cts.Token));
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
            if (cts is not null)
            {
                try { cts.Cancel(); } catch { }
                try { _loopTask?.Wait(500); } catch { }
                cts.Dispose();
            }

            _loopTask = null;

            _capture?.Dispose();
            _capture = null;

            _detector?.Dispose();
            _detector = null;

            btnStart.Enabled = true;
            btnStop.Enabled = false;
            btnRefresh.Enabled = true;
            cmbSources.Enabled = true;
            txtUrl.Enabled = true;

            lock (_bitmapLock)
            {
                var old = picturePreview.Image;
                picturePreview.Image = null;
                old?.Dispose();
            }
        }

        private void CaptureLoop(CancellationToken ct)
        {
            using var frame = new Mat();

            while (!ct.IsCancellationRequested)
            {
                if (_capture is null || _detector is null)
                    break;

                if (!_capture.Read(frame) || frame.Empty())
                {
                    Thread.Sleep(30);
                    continue;
                }

                var detections = _detector.Detect(frame, DefaultScoreThreshold, DefaultNmsThreshold);
                foreach (var d in detections)
                {
                    var rect = new Rect(d.X1, d.Y1, d.X2 - d.X1, d.Y2 - d.Y1);
                    Cv2.Rectangle(frame, rect, Scalar.LimeGreen, 2);
                    Cv2.PutText(
                        frame,
                        $"{d.Confidence:0.00}",
                        new OpenCvSharp.Point(d.X1, Math.Max(d.Y1 - 6, 10)),
                        HersheyFonts.HersheySimplex,
                        0.5,
                        Scalar.Yellow,
                        1);
                }

                using var bmp = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(frame);
                SetPreviewImage((Bitmap)bmp.Clone());
            }
        }

        private void SetPreviewImage(Bitmap next)
        {
            if (IsDisposed)
            {
                next.Dispose();
                return;
            }

            BeginInvoke((Action)(() =>
            {
                lock (_bitmapLock)
                {
                    var old = picturePreview.Image;
                    picturePreview.Image = next;
                    old?.Dispose();
                }
            }));
        }

        private string? GetSelectedSource()
        {
            var url = txtUrl.Text?.Trim();
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

           Path.Combine(
                AppContext.BaseDirectory,
                "Models",
                "version-RFB-320.onnx"
            )
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

        private sealed class ComboBoxItem
        {
            public ComboBoxItem(string text, string value)
            {
                Text = text;
                Value = value;
            }

            public string Text { get; }
            public string Value { get; }

            public override string ToString() => Text;
        }

    }
}
