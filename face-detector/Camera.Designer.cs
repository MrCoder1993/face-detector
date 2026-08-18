namespace face_detector
{
    partial class Camera
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            panelTop = new Panel();
            comboCamera = new ComboBox();
            cmbSources = new ComboBox();
            txtUrl = new TextBox();
            btnRefresh = new Button();
            btnStart = new Button();
            btnStop = new Button();
            groupBox1 = new GroupBox();
            picturePreview = new PictureBox();
            groupFaces = new GroupBox();
            preview_pic = new PictureBox();
            lstFaces = new ListBox();
            panelTop.SuspendLayout();
            groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)picturePreview).BeginInit();
            groupFaces.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)preview_pic).BeginInit();
            SuspendLayout();
            // 
            // panelTop
            // 
            panelTop.Controls.Add(comboCamera);
            panelTop.Controls.Add(cmbSources);
            panelTop.Controls.Add(txtUrl);
            panelTop.Controls.Add(btnRefresh);
            panelTop.Controls.Add(btnStart);
            panelTop.Controls.Add(btnStop);
            panelTop.Dock = DockStyle.Top;
            panelTop.Location = new Point(0, 0);
            panelTop.Name = "panelTop";
            panelTop.Padding = new Padding(8);
            panelTop.Size = new Size(969, 84);
            panelTop.TabIndex = 0;
            // 
            // comboCamera
            // 
            comboCamera.FormattingEnabled = true;
            comboCamera.Items.AddRange(new object[] { "rtsp://admin:@Coin@123@192.168.40.11:554/cam/realmonitor?channel=1&subtype=1", "rtsp://admin:@Coin@123@192.168.40.12:554/cam/realmonitor?channel=1&subtype=1", "rtsp://admin:@Coin@123@192.168.40.13:554/cam/realmonitor?channel=1&subtype=1", "rtsp://admin:@Coin@123@192.168.40.14:554/cam/realmonitor?channel=1&subtype=1", "rtsp://admin:@Coin@123@192.168.40.15:554/cam/realmonitor?channel=1&subtype=1", "rtsp://admin:@Coin@123@192.168.40.16:554/cam/realmonitor?channel=1&subtype=1", "rtsp://admin:@Coin@123@192.168.40.17:554/cam/realmonitor?channel=1&subtype=1", "rtsp://admin:@Coin@123@192.168.40.18:554/cam/realmonitor?channel=1&subtype=1", "rtsp://admin:@Coin@123@192.168.40.19:554/cam/realmonitor?channel=1&subtype=1", "rtsp://admin:@Coin@123@192.168.40.20:554/cam/realmonitor?channel=1&subtype=1", "https://newlive.nasimrezvan.com/hls/Rawzeh-ye-Monavvareh/720p/index.m3u8", "https://t1-cdn.sepehrtv.ir/securelive3/irinnhd/720p.m3u8?s=mut6dybx72VfE3-hoqxdOA&t=1786906751" });
            comboCamera.Location = new Point(227, 12);
            comboCamera.Name = "comboCamera";
            comboCamera.Size = new Size(504, 23);
            comboCamera.TabIndex = 5;
            comboCamera.Text = "rtsp://admin:@Coin@123@192.168.40.12:554/cam/realmonitor?channel=1&subtype=1";
            // 
            // cmbSources
            // 
            cmbSources.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbSources.FormattingEnabled = true;
            cmbSources.Location = new Point(11, 11);
            cmbSources.Name = "cmbSources";
            cmbSources.Size = new Size(210, 23);
            cmbSources.TabIndex = 0;
            // 
            // txtUrl
            // 
            txtUrl.Location = new Point(11, 45);
            txtUrl.Name = "txtUrl";
            txtUrl.PlaceholderText = "RTSP/HTTP URL (optional)";
            txtUrl.Size = new Size(720, 23);
            txtUrl.TabIndex = 1;
            // 
            // btnRefresh
            // 
            btnRefresh.Enabled = false;
            btnRefresh.Location = new Point(737, 50);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(75, 23);
            btnRefresh.TabIndex = 2;
            btnRefresh.Text = "Process";
            btnRefresh.UseVisualStyleBackColor = true;
            btnRefresh.Click += btnRefresh_Click;
            // 
            // btnStart
            // 
            btnStart.Location = new Point(818, 50);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(60, 23);
            btnStart.TabIndex = 3;
            btnStart.Text = "Start";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;
            // 
            // btnStop
            // 
            btnStop.Enabled = false;
            btnStop.Location = new Point(884, 50);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(60, 23);
            btnStop.TabIndex = 4;
            btnStop.Text = "Stop";
            btnStop.UseVisualStyleBackColor = true;
            btnStop.Click += btnStop_Click;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(picturePreview);
            groupBox1.Location = new Point(0, 90);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(711, 628);
            groupBox1.TabIndex = 1;
            groupBox1.TabStop = false;
            groupBox1.Text = "Camera";
            // 
            // picturePreview
            // 
            picturePreview.Dock = DockStyle.Fill;
            picturePreview.Location = new Point(3, 19);
            picturePreview.Name = "picturePreview";
            picturePreview.Size = new Size(705, 606);
            picturePreview.SizeMode = PictureBoxSizeMode.Zoom;
            picturePreview.TabIndex = 2;
            picturePreview.TabStop = false;
            // 
            // groupFaces
            // 
            groupFaces.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            groupFaces.Controls.Add(preview_pic);
            groupFaces.Controls.Add(lstFaces);
            groupFaces.Location = new Point(717, 90);
            groupFaces.Name = "groupFaces";
            groupFaces.Size = new Size(240, 628);
            groupFaces.TabIndex = 2;
            groupFaces.TabStop = false;
            groupFaces.Text = "Faces";
            // 
            // preview_pic
            // 
            preview_pic.Location = new Point(3, 239);
            preview_pic.Name = "preview_pic";
            preview_pic.Size = new Size(231, 233);
            preview_pic.TabIndex = 1;
            preview_pic.TabStop = false;
            // 
            // lstFaces
            // 
            lstFaces.FormattingEnabled = true;
            lstFaces.Location = new Point(3, 19);
            lstFaces.Name = "lstFaces";
            lstFaces.Size = new Size(234, 214);
            lstFaces.TabIndex = 0;
            lstFaces.Click += lstFaces_Click;
            // 
            // Camera
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(969, 730);
            Controls.Add(groupFaces);
            Controls.Add(groupBox1);
            Controls.Add(panelTop);
            Name = "Camera";
            Text = "Camera";
            FormClosing += Camera_FormClosing;
            Load += Camera_Load;
            panelTop.ResumeLayout(false);
            panelTop.PerformLayout();
            groupBox1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)picturePreview).EndInit();
            groupFaces.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)preview_pic).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Panel panelTop;
        private ComboBox cmbSources;
        private TextBox txtUrl;
        private Button btnRefresh;
        private Button btnStart;
        private Button btnStop;
        private ComboBox comboCamera;
        private GroupBox groupBox1;
        private PictureBox picturePreview;
        private GroupBox groupFaces;
        private ListBox lstFaces;
        private PictureBox preview_pic;
    }
}