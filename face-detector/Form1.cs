using OpenCvSharp;
using System.Runtime.InteropServices;
using Recognition;
using System.Text;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace face_detector
{
    public partial class Form1 : Form
    {
        private readonly CameraFrameProcessor _frameProcessor;
        //private readonly FaceTracker _tracker;
        //private readonly IFaceEmbedder _embedder;
        //private readonly InsightFaceGenderAgeEstimator _genderAge;
        public Form1()
        {
            InitializeComponent();

            var detModelPath = Path.Combine(AppContext.BaseDirectory, "Models", "34g_gnkps.onnx");
            var recModelPath = Path.Combine(AppContext.BaseDirectory, "Models", "arc.onnx");
            //var genderAgeModelPath = Path.Combine(AppContext.BaseDirectory, "Models", "genderage.onnx");

            _frameProcessor = new CameraFrameProcessor(
                detModelPath,
                recModelPath,
                Path.Combine(AppContext.BaseDirectory));

            //Clipboard.SetText(_embedder.GetModelInfo());
            //proccess("C:\\face-detector-benchmark\\4cc68eb9-3ea4-48cd-ae7c-54d65663b9d2.jpg");
        }

        private void btnCamera_Click(object sender, EventArgs e)
        {
            using var frm = new Camera();

            frm.ShowDialog(this);
            Close();

        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _frameProcessor.Dispose();
            //_embedder.Dispose();
            //_genderAge.Dispose();
            base.OnFormClosed(e);
        }

        private void proccess_btn_Click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = "Image Files|*.bmp;*.png;*.jpg;*.jpeg;*.webp|All Files|*.*";
            openFileDialog1.Title = "Select an image";

            if (openFileDialog1.ShowDialog(this) != DialogResult.OK)
                return;

            var filePath = openFileDialog1.FileName;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return;

            proccess(filePath);
        }

        private void proccess(string filePath)
        {
            using var src = Cv2.ImRead(filePath, ImreadModes.Color);
            if (src.Empty())
            {
                MessageBox.Show(this, "Unable to read image.");
                return;
            }

            using var annotated = src.Clone();
            foreach (var face in _frameProcessor.Process(src))
            {
                var bounds = face.Bounds;
                var simId = face.Id;

                var label = $"{simId}";
                var extraHeight = (int)Math.Round(bounds.Height * 0.40);
                var extraWidth = (int)Math.Round(bounds.Width * 0.30);
                var left = Math.Max(0, bounds.Left - extraWidth);
                var top = Math.Max(0, bounds.Top - extraHeight);
                var right = Math.Min(annotated.Width - 1, bounds.Right);
                var bottom = Math.Min(annotated.Height - 1, bounds.Bottom);
                var displayBounds = new Rect(
                    left,
                    top,
                    Math.Max(1, right - left),
                    Math.Max(1, bottom - top));

                Cv2.Rectangle(annotated, displayBounds, Scalar.Blue, 1);
                Cv2.PutText(
                    annotated,
                    label,
                    new OpenCvSharp.Point(displayBounds.Left, Math.Max(displayBounds.Top - 8, 15)),
                    HersheyFonts.HersheySimplex,
                    0.5,
                    Scalar.Blue,
                    1,
                    LineTypes.AntiAlias);
            }

            var bmp = MatToBitmap(annotated);
            pictureBox1.Image?.Dispose();
            pictureBox1.Image = bmp;
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
        }


        private static double ComputePolygonArea(IReadOnlyList<(float X, float Y)> pts)
        {
            if (pts is null || pts.Count < 3)
                return 0;

            double sum = 0;
            for (var i = 0; i < pts.Count; i++)
            {
                var (x1, y1) = pts[i];
                var (x2, y2) = pts[(i + 1) % pts.Count];
                sum += x1 * y2 - x2 * y1;
            }

            return Math.Abs(sum) * 0.5;
        }
        private static Bitmap MatToBitmap(Mat mat)
        {
            using var bgr = mat.Channels() == 3 ? mat : mat.CvtColor(ColorConversionCodes.GRAY2BGR);

            var bmp = new Bitmap(bgr.Width, bgr.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, bmp.PixelFormat);
            try
            {
                var bytes = bgr.Rows * (int)bgr.Step();
                var buffer = new byte[bytes];
                Marshal.Copy(bgr.Data, buffer, 0, bytes);

                // Copy row-by-row into Bitmap (handles stride differences).
                for (var y = 0; y < bgr.Rows; y++)
                {
                    Marshal.Copy(
                        buffer,
                        y * (int)bgr.Step(),
                        data.Scan0 + y * data.Stride,
                        bgr.Cols * 3);
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            return bmp;
        }

        private void lstFaces_Click(object sender, EventArgs e)
        {

        }
    }
}