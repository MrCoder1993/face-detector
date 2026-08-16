using UltraFace;
using OpenCvSharp;
using System.Runtime.InteropServices;
using Recognition;

namespace face_detector
{
    public partial class Form1 : Form
    {
        private readonly FaceDetector _detector;
        private readonly FaceIdTracker _tracker = new();

        public Form1()
        {
            InitializeComponent();

            var modelPath = Path.Combine(
                AppContext.BaseDirectory,
                "Models",
                "version-RFB-320.onnx"
            );

            _detector = new FaceDetector(modelPath);

            var info = _detector.GetModelInfo();

          
        }

        private void btnCamera_Click(object sender, EventArgs e)
        {
            using var frm = new Camera();
            frm.ShowDialog(this);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _detector.Dispose();
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
                var id = _tracker.GetOrCreateId(faceCrop);

                Cv2.Rectangle(
                    annotated,
                    new Rect(det.X1, det.Y1, det.Width, det.Height),
                    new Scalar(0, 255, 0),
                    2);

                var label = id.ToString("N")[..8];
                var y = Math.Min(annotated.Height - 5, det.Y2 + 20);
                Cv2.PutText(
                    annotated,
                    label,
                    new OpenCvSharp.Point(det.X1, y),
                    HersheyFonts.HersheySimplex,
                    0.7,
                    new Scalar(0, 255, 255),
                    2);
            }

            var bmp = MatToBitmap(annotated);
            pictureBox1.Image?.Dispose();
            pictureBox1.Image = bmp;
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
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
    }
}