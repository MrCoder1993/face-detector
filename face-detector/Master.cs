using DomainLayer;
using Recognition;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
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
        private readonly ISourceRepository sourceRepository;
        private readonly IFaceGalleryService faceGalleryService;
        private readonly ILiveStreamService liveStreamService;
        private readonly IWebcamDiscoveryService webcamDiscoveryService;
        private readonly List<PictureBox> livePictureBoxes = new();
        private readonly object liveFrameLock = new();
        private readonly Dictionary<int, Bitmap> pendingLiveFrames = new();
        private readonly HashSet<int> scheduledLiveFrames = new();
        private readonly object facesUpdateLock = new();
        private readonly HashSet<string> hiddenFaceIds = new(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyList<CameraFrameProcessor.ProcessedFace> detectedFaces = [];
        private IReadOnlyList<CameraFrameProcessor.ProcessedFace> pendingDetectedFaces = [];
        private bool facesUpdateScheduled;
        private DateTime nextDetectedFacesCleanup = DateTime.UtcNow.AddMinutes(1);
        private readonly CancellationTokenSource facesScanCts = new();
        private string facesFilesSignature = string.Empty;
        private bool liveSourcesInitialized;

        public Master()
        {
            InitializeComponent();
            sourceRepository = new JsonSourceRepository(Path.Combine(AppContext.BaseDirectory, "sources.txt"));
            faceGalleryService = new FaceGalleryService(AppContext.BaseDirectory);
            liveStreamService = new LiveStreamService(AppContext.BaseDirectory);
            webcamDiscoveryService = new WebcamDiscoveryService();
            LoadSources();
            UpdateSourcesGrid();
            RefreshFacesTab();
            _ = ScanFacesDirectoryAsync(facesScanCts.Token);
            _ = RefreshSources();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (liveSourcesInitialized)
                return;

            liveSourcesInitialized = true;
            RefreshLiveSources();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            facesScanCts.Cancel();
            liveStreamService.Dispose();
            base.OnFormClosed(e);
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
            sources = sourceRepository.Load();
        }

        private void SaveSources()
        {
            sourceRepository.Save(sources);
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
            Debug.WriteLine($"[Live] RefreshLiveSources started. sources={sources.Count}, pictureBoxes={livePictureBoxes.Count}, controls={groupBox_live.Controls.Count}");
            liveStreamService.Stop();

            lock (liveFrameLock)
            {
                foreach (var image in pendingLiveFrames.Values)
                    image.Dispose();
                pendingLiveFrames.Clear();
                scheduledLiveFrames.Clear();
            }

            Debug.WriteLine("[Live] Pending frames and scheduled frames cleared.");

            foreach (var pictureBox in livePictureBoxes)
            {
                pictureBox.Image?.Dispose();
                pictureBox.Dispose();
            }

            groupBox_live.Controls.Clear();
            livePictureBoxes.Clear();

            Debug.WriteLine($"[Live] Old controls cleared. sources={sources.Count}");

            if (sources.Count == 0)
            {
                Debug.WriteLine("[Live] No sources found. RefreshLiveSources finished.");
                return;
            }

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

                Debug.WriteLine($"[Live] PictureBox created. index={i}, name={pictureBox.Name}, size={pictureBox.Size}, controls={groupBox_live.Controls.Count}");
            }

            Debug.WriteLine($"[Live] Starting stream. sources={sources.Count}, pictureBoxes={livePictureBoxes.Count}, controls={groupBox_live.Controls.Count}");
            liveStreamService.Start(sources, OnLiveFrameReceived, OnFacesDetected);
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

        private void OnLiveFrameReceived(int sourceIndex, Bitmap nextImage)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                nextImage.Dispose();
                return;
            }

            var shouldSchedule = false;
            lock (liveFrameLock)
            {
                if (pendingLiveFrames.TryGetValue(sourceIndex, out var previousImage))
                    previousImage.Dispose();
                pendingLiveFrames[sourceIndex] = nextImage;
                shouldSchedule = scheduledLiveFrames.Add(sourceIndex);
            }

            if (shouldSchedule)
            {
                try
                {
                    BeginInvoke((Action)(() => RenderLatestLiveFrame(sourceIndex)));
                }
                catch
                {
                    lock (liveFrameLock)
                        scheduledLiveFrames.Remove(sourceIndex);
                }
            }
        }

        private void RenderLatestLiveFrame(int sourceIndex)
        {
            Bitmap? nextImage = null;
            lock (liveFrameLock)
            {
                if (pendingLiveFrames.TryGetValue(sourceIndex, out nextImage))
                    pendingLiveFrames.Remove(sourceIndex);
            }

            if (nextImage is null)
                return;

            if (sourceIndex >= livePictureBoxes.Count || livePictureBoxes[sourceIndex].IsDisposed)
            {
                nextImage.Dispose();
            }
            else
            {
                var pictureBox = livePictureBoxes[sourceIndex];
                var oldImage = pictureBox.Image;
                pictureBox.Image = nextImage;
                oldImage?.Dispose();
            }

            var shouldSchedule = false;
            lock (liveFrameLock)
            {
                if (pendingLiveFrames.ContainsKey(sourceIndex))
                    shouldSchedule = true;
                else
                    scheduledLiveFrames.Remove(sourceIndex);
            }

            if (shouldSchedule)
                BeginInvoke((Action)(() => RenderLatestLiveFrame(sourceIndex)));
        }

        private void OnFacesDetected(IReadOnlyList<CameraFrameProcessor.ProcessedFace> faces)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            var shouldSchedule = false;
            lock (facesUpdateLock)
            {
                pendingDetectedFaces = faces.ToList();
                if (!facesUpdateScheduled)
                {
                    facesUpdateScheduled = true;
                    shouldSchedule = true;
                }
            }

            if (!shouldSchedule)
                return;

            try
            {
                BeginInvoke((Action)UpdatePendingDetectedFaces);
            }
            catch
            {
                lock (facesUpdateLock)
                    facesUpdateScheduled = false;
            }
        }

        private void UpdatePendingDetectedFaces()
        {
            IReadOnlyList<CameraFrameProcessor.ProcessedFace> faces;
            lock (facesUpdateLock)
            {
                faces = pendingDetectedFaces;
                pendingDetectedFaces = [];
                facesUpdateScheduled = false;
            }

            UpdateDetectedFaces(faces);

            lock (facesUpdateLock)
            {
                if (pendingDetectedFaces.Count == 0 || facesUpdateScheduled || IsDisposed)
                    return;

                facesUpdateScheduled = true;
            }

            try
            {
                BeginInvoke((Action)UpdatePendingDetectedFaces);
            }
            catch
            {
                lock (facesUpdateLock)
                    facesUpdateScheduled = false;
            }
        }

        private void UpdateDetectedFaces(IReadOnlyList<CameraFrameProcessor.ProcessedFace> newFaces)
        {
            detectedFaces = newFaces;
            live_tab.SuspendLayout();
            try
            {
                var visibleFaces = newFaces
                    .Where(face => !hiddenFaceIds.Contains(face.Id))
                    .ToList();

                if (DateTime.UtcNow >= nextDetectedFacesCleanup)
                {
                    foreach (Control control in live_tab.Controls.Cast<Control>().ToArray())
                        control.Dispose();
                    live_tab.Controls.Clear();
                    nextDetectedFacesCleanup = DateTime.UtcNow.AddMinutes(1);
                }

                if (visibleFaces.Count == 0)
                    return;

                var spacing = 12;
                var cardCount = live_tab.Controls.Count;
                var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(Math.Max(visibleFaces.Count, cardCount + 1))));
                var availableWidth = Math.Max(1, live_tab.ClientSize.Width - spacing * (columns + 1));
                var cardWidth = Math.Min(100, Math.Max(1, availableWidth / columns));
                var cardHeight = cardWidth + 68;

                foreach (var face in visibleFaces)
                {
                    if (live_tab.Controls.OfType<Panel>().Any(card =>
                        string.Equals(card.Tag as string, face.Id, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var index = live_tab.Controls.Count;
                    var card = new Panel
                    {
                        BorderStyle = BorderStyle.FixedSingle,
                        Size = new System.Drawing.Size(cardWidth, cardHeight),
                        Location = new System.Drawing.Point(
                            spacing + (index % columns) * (cardWidth + spacing),
                            spacing + (index / columns) * (cardHeight + spacing)),
                        Tag = face.Id
                    };

                    var imageSize = Math.Max(1, cardWidth - 2);
                    var faceImage = new PictureBox
                    {
                        Size = new System.Drawing.Size(imageSize, imageSize),
                        Location = new System.Drawing.Point(1, 1),
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Image = LoadFaceImage(face.Id)
                    };

                    var fullnameLabel = new Label
                    {
                        AutoSize = false,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Text = face.fullname,
                        Location = new System.Drawing.Point(1, imageSize + 4),
                        Size = new System.Drawing.Size(imageSize, 22)
                    };

                    var deleteButton = new Button
                    {
                        Text = "حذف از لیست",
                        Location = new System.Drawing.Point(1, imageSize + 28),
                        Size = new System.Drawing.Size(imageSize, 30),
                        Tag = face.Id
                    };
                    deleteButton.Click += DeleteDetectedFace_Click;

                    card.Controls.Add(faceImage);
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

        private Image? LoadFaceImage(string id)
        {
            var imagePath = Path.Combine(AppContext.BaseDirectory, "Faces", $"{id}.jpg");
            return faceGalleryService.LoadImageCopy(imagePath);
        }

        private void DeleteDetectedFace_Click(object? sender, EventArgs e)
        {
            if (sender is not Button { Tag: string faceId })
                return;

            hiddenFaceIds.Add(faceId);

            var card = live_tab.Controls
                .OfType<Panel>()
                .FirstOrDefault(x => string.Equals(x.Tag as string, faceId, StringComparison.OrdinalIgnoreCase));
            card?.Dispose();

            UpdateDetectedFaces(detectedFaces);
        }

        private void RefreshFacesTab(IReadOnlyList<string>? imageFiles = null)
        {
            var facesDirectory = Path.Combine(AppContext.BaseDirectory, "Faces");
            Directory.CreateDirectory(facesDirectory);

            imageFiles ??= faceGalleryService.GetImageFiles();
            facesFilesSignature = CreateFacesFilesSignature(imageFiles);
            var panel = faces_tab.Controls.OfType<FlowLayoutPanel>().FirstOrDefault();
            if (panel is null)
            {
                panel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    WrapContents = true,
                    FlowDirection = FlowDirection.LeftToRight,
                    Padding = new Padding(8, 38, 8, 8)
                };
                faces_tab.Controls.Add(panel);
            }

            foreach (Control control in panel.Controls)
                control.Dispose();
            panel.Controls.Clear();

            foreach (var imagePath in imageFiles)
            {
                var card = new Panel
                {
                    Width = 150,
                    Height = 210,
                    Margin = new Padding(6),
                    BorderStyle = BorderStyle.FixedSingle
                };

                var image = new PictureBox
                {
                    Size = new System.Drawing.Size(100, 100),
                    Location = new System.Drawing.Point(24, 6),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = faceGalleryService.LoadImageCopy(imagePath)
                };

                var nameTextBox = new TextBox
                {
                    PlaceholderText = "نام کامل",
                    Text = liveStreamService.GetFullName(Path.GetFileNameWithoutExtension(imagePath)),
                    Location = new System.Drawing.Point(5, 152),
                    Size = new System.Drawing.Size(138, 23)
                };

                var registeredAtLabel = new Label
                {
                    AutoEllipsis = true,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Text = $"ثبت: {faceGalleryService.GetPersianCreationDate(imagePath)}",
                    Location = new System.Drawing.Point(5, 130),
                    Size = new System.Drawing.Size(138, 20)
                };

                var saveButton = new Button
                {
                    Text = "ثبت",
                    Location = new System.Drawing.Point(5, 178),
                    Size = new System.Drawing.Size(65, 23),
                    Tag = imagePath
                };
                saveButton.Click += (_, _) => UpdateFaceFullName((string)saveButton.Tag, nameTextBox.Text);

                var deleteButton = new Button
                {
                    Text = "حذف",
                    Location = new System.Drawing.Point(78, 178),
                    Size = new System.Drawing.Size(65, 23),
                    Tag = imagePath
                };
                deleteButton.Click += (_, _) => DeleteFaceImage((string)deleteButton.Tag);

                card.Controls.Add(image);
                card.Controls.Add(registeredAtLabel);
                card.Controls.Add(nameTextBox);
                card.Controls.Add(saveButton);
                card.Controls.Add(deleteButton);
                panel.Controls.Add(card);
            }

            refresh_btn.BringToFront();
        }

        private async Task ScanFacesDirectoryAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    var imageFiles = await Task.Run(faceGalleryService.GetImageFiles, cancellationToken);
                    var signature = CreateFacesFilesSignature(imageFiles);

                    if (signature == facesFilesSignature || IsDisposed || !IsHandleCreated)
                        continue;

                    BeginInvoke((Action)(() => RefreshFacesTab(imageFiles)));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                }
            }
        }

        private void refresh_btn_Click(object? sender, EventArgs e)
        {
            RefreshFacesTab();
        }

        private static string CreateFacesFilesSignature(IEnumerable<string> imageFiles)
        {
            return string.Join("|", imageFiles);
        }

        private async void UpdateFaceFullName(string imagePath, string fullname)
        {
            fullname = fullname.Trim();
            var id = Path.GetFileNameWithoutExtension(imagePath);
            if (string.IsNullOrWhiteSpace(fullname))
            {
                MessageBox.Show(this, "نام کامل را وارد کنید.", "خطا", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                await Task.Run(() => liveStreamService.SetFullName(id, fullname));

                detectedFaces = detectedFaces
                    .Select(face => face.Id.Equals(id, StringComparison.OrdinalIgnoreCase)
                        ? face with { fullname = fullname }
                        : face)
                    .ToList();
                RefreshFacesTab();
                UpdateDetectedFaces(detectedFaces);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "خطا در ثبت نام کامل", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeleteFaceImage(string imagePath)
        {
            if (MessageBox.Show(this, "تصویر به‌صورت فیزیکی حذف شود؟", "حذف تصویر", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try
            {
                if (File.Exists(imagePath))
                    File.Delete(imagePath);

                RefreshFacesTab();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "خطا در حذف تصویر", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task RefreshSources()
        {
            var current = cmbSources.SelectedItem as ComboBoxItem;

            var webcams = await Task.Run(webcamDiscoveryService.Find);

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
