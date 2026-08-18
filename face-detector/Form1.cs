using UltraFace;
using OpenCvSharp;
using System.Runtime.InteropServices;
using Recognition;
using System.Text;

namespace face_detector
{
    public partial class Form1 : Form
    {
        private readonly FaceDetector _detector;
        //private readonly FaceTracker _tracker;
        //private readonly IFaceEmbedder _embedder;
        //private readonly InsightFaceGenderAgeEstimator _genderAge;
        public Form1()
        {
            InitializeComponent();

            //var detModelPath = Path.Combine(AppContext.BaseDirectory, "Models", "10g_bnkps.onnx");
            var detModelPath = Path.Combine(AppContext.BaseDirectory, "Models", "arc.onnx");
            //var recModelPath = Path.Combine(AppContext.BaseDirectory, "Models", "arc.onnx");
            //var genderAgeModelPath = Path.Combine(AppContext.BaseDirectory, "Models", "genderage.onnx");

            _detector = new FaceDetector(detModelPath);
            //_embedder = new InsightFaceEmbedder(recModelPath);
            //_genderAge = new InsightFaceGenderAgeEstimator(genderAgeModelPath);
            //_tracker = new FaceTracker(_embedder);
            //MessageBox.Show(_detector.GetModelInfo());
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
            _detector.Dispose();
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
            var detections = _detector.Detect(annotated);

            foreach (var det in detections)
            {
                using var faceCrop = CropWithPadding(src, det, paddingRatio: 0.15f);
                var simId = det.id;
                //var ga = _genderAge.Predict(faceCrop);
                //var raw = _genderAge.PredictRaw(faceCrop);

                var label = $"{simId}";

                if (det.Landmarks5 is { Count: 5 })
                {
                    var area = ComputePolygonArea(det.Landmarks5);
                    var anyLandmarkVisible = false;
                    foreach (var (lx, ly) in det.Landmarks5)
                    {
                        anyLandmarkVisible = true;
                        Cv2.Circle(
                            annotated,
                            new OpenCvSharp.Point((int)MathF.Round(lx), (int)MathF.Round(ly)),
                            3,
                            new Scalar(0, 0, 255),
                            -1);
                    }

                    // Show polygon area under the label (px^2)
                    label = anyLandmarkVisible ? $"{simId} | A={area:0}" : $"{simId} | A=?";
                }
                else
                {
                    var noseX = det.X1 + (det.Width / 2);
                    var noseY = det.Y1 + (det.Height / 2);
                    Cv2.Circle(
                        annotated,
                        new OpenCvSharp.Point(noseX, noseY),
                        5,
                        new Scalar(0, 0, 255),
                        -1);
                }

                //var rawText = raw.Length == 3
                //    ? $"[{raw[0]:0.###},{raw[1]:0.###},{raw[2]:0.###}]"
                //    : "[]";
                var y = Math.Min(annotated.Height - 5, det.Y2 + 20);

                var origin = new OpenCvSharp.Point(det.X1, y);
                const HersheyFonts font = HersheyFonts.HersheySimplex;
                const double fontScale = 0.7;
                const int thickness = 2;

                Cv2.GetTextSize(label, font, fontScale, thickness, out var baseline);
                var textRect = new Rect(
                    origin.X,
                    origin.Y - (baseline + 5) - 2,
                    Math.Min(annotated.Width - origin.X, Math.Max(1, (int)Math.Ceiling(label.Length * 12.0))),
                    baseline + 5 + 6);

                // Safer rect based on measured text size
                var size = Cv2.GetTextSize(label, font, fontScale, thickness, out baseline);
                //textRect = new Rect(
                //    Math.Clamp(origin.X, 0, annotated.Width - 1),
                //    Math.Clamp(origin.Y - size.Height - baseline - 4, 0, annotated.Height - 1),
                //    Math.Clamp(size.Width + 8, 1, annotated.Width - Math.Clamp(origin.X, 0, annotated.Width - 1)),
                //    Math.Clamp(size.Height + baseline + 8, 1, annotated.Height - Math.Clamp(origin.Y - size.Height - baseline - 4, 0, annotated.Height - 1)));

                // Clamp rect to image bounds to avoid OpenCV exceptions (can break rendering).
                var clampedRect = new Rect(
                    Math.Clamp(textRect.X, 0, annotated.Width - 1),
                    Math.Clamp(textRect.Y, 0, annotated.Height - 1),
                    Math.Clamp(textRect.Width, 1, annotated.Width - Math.Clamp(textRect.X, 0, annotated.Width - 1)),
                    Math.Clamp(textRect.Height, 1, annotated.Height - Math.Clamp(textRect.Y, 0, annotated.Height - 1)));

                Cv2.Rectangle(annotated, clampedRect, new Scalar(0, 0, 0), -1);
                Cv2.PutText(
                    annotated,
                    label,
                    new OpenCvSharp.Point(clampedRect.X + 4, clampedRect.Y + size.Height + 4),
                    font,
                    fontScale,
                    new Scalar(0, 255, 255),
                    thickness);
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

        private static Mat CropWithPadding(Mat srcBgr, FaceDetection det, float paddingRatio)
        {
            var padX = (int)MathF.Round(det.Width * paddingRatio);
            var padY = (int)MathF.Round(det.Height * paddingRatio);

            var x1 = Math.Max(0, det.X1 - padX);
            var y1 = Math.Max(0, det.Y1 - padY);
            var x2 = Math.Min(srcBgr.Width - 1, det.X2 + padX);
            var y2 = Math.Min(srcBgr.Height - 1, det.Y2 + padY);

            var w = Math.Max(1, x2 - x1);
            var h = Math.Max(1, y2 - y1);

            return new Mat(srcBgr, new Rect(x1, y1, w, h)).Clone();
        }

        private void lstFaces_Click(object sender, EventArgs e)
        {

        }
    }
}