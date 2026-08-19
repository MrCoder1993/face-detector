using DomainLayer;
using OpenCvSharp;
using Recognition;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace face_detector
{
    public partial class Master : Form
    {
        private List<SourceDto> sources = new List<SourceDto>();
        private readonly string sourcesFilePath = Path.Combine(AppContext.BaseDirectory, "sources.txt");
        private CancellationTokenSource? liveCts;
        private readonly List<PictureBox> livePictureBoxes = new();
        private readonly List<Task> liveTasks = new();
        private readonly object frameProcessorLock = new();
        private CameraFrameProcessor? _frameProcessor;
        private readonly HashSet<string> hiddenFaceIds = new(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyList<CameraFrameProcessor.ProcessedFace> detectedFaces = [];

        public Master()
        {
            InitializeComponent();
            _frameProcessor = new CameraFrameProcessor(
                Path.Combine(AppContext.BaseDirectory, "Models", "34g_gnkps.onnx"),
                Path.Combine(AppContext.BaseDirectory, "Models", "arc.onnx"),
                AppContext.BaseDirectory);
            LoadSources();
            UpdateSourcesGrid();
            _ = RefreshSources();
            RefreshLiveSources();
        }

        private void webcam_checkbox_CheckedChanged(object sender, EventArgs e)
        {
            if ((sender as CheckBox).Checked)
            {
                source_lbl.Text = "انتخاب وبکم";
                rstp_address.Visible = false;
                cmbSources.Visible = true;

            }
            else
            {
                source_lbl.Text = "لینک RSTP";
                rstp_address.Visible = true;
                cmbSources.Visible = false;
            }


        }

        private void add_btn_Click(object sender, EventArgs e)
        {
            sources.Add(new SourceDto()
            {
                IsWebcam = webcam_checkbox.Checked,
                Title = title_txtbox.Text,
                Link = webcam_checkbox.Checked ? (cmbSources.SelectedItem as ComboBoxItem).Value : rstp_address.Text
            });

            SaveSources();
            UpdateSourcesGrid();
            RefreshLiveSources();
        }



        private void LoadSources()
        {
            if (!File.Exists(sourcesFilePath))
                return;

            var fileContent = File.ReadAllText(sourcesFilePath);
            sources = JsonSerializer.Deserialize<List<SourceDto>>(fileContent) ?? new List<SourceDto>();
        }

        private void SaveSources()
        {
            var fileContent = JsonSerializer.Serialize(sources, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(sourcesFilePath, fileContent);
        }

        private void UpdateSourcesGrid()
        {
            dataGridView1.Rows.Clear();

            foreach (var source in sources)
            {
                dataGridView1.Rows.Add(source.Title, source.Link, source.IsWebcam);
            }
        }

        private void RefreshLiveSources()
        {
            liveCts?.Cancel();
            liveCts = new CancellationTokenSource();

            foreach (var pictureBox in livePictureBoxes)
            {
                pictureBox.Image?.Dispose();
                pictureBox.Dispose();
            }

            livePictureBoxes.Clear();
            liveTasks.Clear();
            groupBox_live.Controls.Clear();

            if (sources.Count == 0)
                return;

            var columns = (int)Math.Ceiling(Math.Sqrt(sources.Count));
            var rows = (int)Math.Ceiling((double)sources.Count / columns);
            var spacing = 8;
            var availableWidth = groupBox_live.ClientSize.Width - spacing * (columns + 1);
            var availableHeight = groupBox_live.ClientSize.Height - spacing * (rows + 1) - 25;
            var cellWidth = Math.Max(1, availableWidth / columns);
            var cellHeight = Math.Max(1, availableHeight / rows);

            for (var i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                var pictureBox = new PictureBox
                {
                    Name = $"livePictureBox_{i}",
                    BorderStyle = BorderStyle.FixedSingle,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Location = new System.Drawing.Point(
                        spacing + (i % columns) * (cellWidth + spacing),
                        25 + spacing + (i / columns) * (cellHeight + spacing)),
                    Size = new System.Drawing.Size(cellWidth, cellHeight),
                    Tag = sources[i].Title
                };

                groupBox_live.Controls.Add(pictureBox);
                livePictureBoxes.Add(pictureBox);
                var token = liveCts.Token;
                liveTasks.Add(Task.Run(() => PlayLiveSource(source.Link, pictureBox, token), token));
            }
        }

        private void groupBox_live_Resize(object? sender, EventArgs e)
        {
            if (livePictureBoxes.Count == 0)
                return;

            var columns = (int)Math.Ceiling(Math.Sqrt(livePictureBoxes.Count));
            var rows = (int)Math.Ceiling((double)livePictureBoxes.Count / columns);
            var spacing = 8;
            var availableWidth = groupBox_live.ClientSize.Width - spacing * (columns + 1);
            var availableHeight = groupBox_live.ClientSize.Height - spacing * (rows + 1) - 25;
            var cellWidth = Math.Max(1, availableWidth / columns);
            var cellHeight = Math.Max(1, availableHeight / rows);

            for (var i = 0; i < livePictureBoxes.Count; i++)
            {
                livePictureBoxes[i].Location = new System.Drawing.Point(
                    spacing + (i % columns) * (cellWidth + spacing),
                    25 + spacing + (i / columns) * (cellHeight + spacing));
                livePictureBoxes[i].Size = new System.Drawing.Size(cellWidth, cellHeight);
            }
        }

        private void PlayLiveSource(string source, PictureBox pictureBox, CancellationToken cancellationToken)
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

            while (!cancellationToken.IsCancellationRequested)
            {
                if (!capture.Read(frame) || frame.Empty())
                    continue;

                using var bitmap = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(frame);
                var nextImage = (Bitmap)bitmap.Clone();

                var now = Environment.TickCount64;
                if (now >= nextProcessingAt && (processingTask is null || processingTask.IsCompleted))
                {
                    var processFrame = frame.Clone();
                    processingTask = Task.Run(() => ProcessLiveFrame(processFrame, cancellationToken), cancellationToken);
                    nextProcessingAt = now + 2000;
                }

                try
                {
                    pictureBox.BeginInvoke((Action)(() =>
                    {
                        var oldImage = pictureBox.Image;
                        pictureBox.Image = nextImage;
                        oldImage?.Dispose();
                    }));
                }
                catch
                {
                    nextImage.Dispose();
                    break;
                }
            }
        }

        private void ProcessLiveFrame(Mat frame, CancellationToken cancellationToken)
        {
            try
            {
                using (frame)
                lock (frameProcessorLock)
                {
                    if (!cancellationToken.IsCancellationRequested && _frameProcessor is not null)
                    {
                        var newFaces = _frameProcessor.Process(frame).ToList();
                        if (!IsDisposed && IsHandleCreated)
                        {
                            BeginInvoke((Action)(() => UpdateDetectedFaces(newFaces)));
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private void UpdateDetectedFaces(IReadOnlyList<CameraFrameProcessor.ProcessedFace> newFaces)
        {
            detectedFaces = newFaces;
            live_tab.SuspendLayout();
            try
            {
                live_tab.Controls.Clear();

                var visibleFaces = newFaces
                    .Where(face => !hiddenFaceIds.Contains(face.Id))
                    .ToList();

                if (visibleFaces.Count == 0)
                    return;

                var columns = (int)Math.Ceiling(Math.Sqrt(visibleFaces.Count));
                var rows = (int)Math.Ceiling((double)visibleFaces.Count / columns);
                var spacing = 12;
                var availableWidth = Math.Max(1, live_tab.ClientSize.Width - spacing * (columns + 1));
                var cardWidth = Math.Min(300, Math.Max(1, availableWidth / columns));
                var cardHeight = cardWidth + 90;

                for (var i = 0; i < visibleFaces.Count; i++)
                {
                    var face = visibleFaces[i];
                    var card = new Panel
                    {
                        BorderStyle = BorderStyle.FixedSingle,
                        Size = new System.Drawing.Size(cardWidth, cardHeight),
                        Location = new System.Drawing.Point(
                            spacing + (i % columns) * (cardWidth + spacing),
                            spacing + (i / columns) * (cardHeight + spacing))
                    };

                    var imageSize = Math.Max(1, cardWidth - 2);
                    var faceImage = new PictureBox
                    {
                        Size = new System.Drawing.Size(imageSize, imageSize),
                        Location = new System.Drawing.Point(1, 1),
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Image = LoadFaceImage(face.Id)
                    };

                    var idLabel = new Label
                    {
                        AutoSize = false,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Text = $"ID: {face.Id}",
                        Location = new System.Drawing.Point(1, imageSize + 4),
                        Size = new System.Drawing.Size(imageSize, 22)
                    };

                    var fullnameLabel = new Label
                    {
                        AutoSize = false,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Text = string.IsNullOrWhiteSpace(face.fullname) ? "نام ثبت نشده" : face.fullname,
                        Location = new System.Drawing.Point(1, imageSize + 26),
                        Size = new System.Drawing.Size(imageSize, 22)
                    };

                    var deleteButton = new Button
                    {
                        Text = "حذف از لیست",
                        Location = new System.Drawing.Point(1, imageSize + 50),
                        Size = new System.Drawing.Size(imageSize, 30),
                        Tag = face.Id
                    };
                    deleteButton.Click += DeleteDetectedFace_Click;

                    card.Controls.Add(faceImage);
                    card.Controls.Add(idLabel);
                    card.Controls.Add(fullnameLabel);
                    card.Controls.Add(deleteButton);
                    live_tab.Controls.Add(card);
                }
            }
            finally
            {
                live_tab.ResumeLayout(true);
            }
        }

        private static Image? LoadFaceImage(string id)
        {
            var imagePath = Path.Combine(AppContext.BaseDirectory, "Faces", $"{id}.jpg");
            if (!File.Exists(imagePath))
                return null;

            using var source = Image.FromFile(imagePath);
            return new Bitmap(source);
        }

        private void DeleteDetectedFace_Click(object? sender, EventArgs e)
        {
            if (sender is not Button { Tag: string faceId })
                return;

            hiddenFaceIds.Add(faceId);
            UpdateDetectedFaces(detectedFaces);
        }

        private async Task RefreshSources()
        {
            var current = cmbSources.SelectedItem as ComboBoxItem;

            var webcams = await Task.Run(() =>
            {
                var result = new List<ComboBoxItem>();

                for (var i = 0; i < 10; i++)
                {
                    using var cap = new VideoCapture(i);
                    if (cap.IsOpened())
                        result.Add(new ComboBoxItem($"Webcam {i}", i.ToString()));
                }

                return result;
            });

            cmbSources.BeginUpdate();
            try
            {
                cmbSources.Items.Clear();

                foreach (var webcam in webcams)
                    cmbSources.Items.Add(webcam);

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

     

        private void delete_btn_Click(object sender, DataGridViewRowCancelEventArgs e)
        {
            if (dataGridView1.SelectedRows.Count == 0)
                return;

            var rowIndex = dataGridView1.SelectedRows[0].Index;
            if (rowIndex < 0 || rowIndex >= sources.Count)
                return;

            sources.RemoveAt(rowIndex);
            SaveSources();
            UpdateSourcesGrid();
            RefreshLiveSources();
        }
    }
}
