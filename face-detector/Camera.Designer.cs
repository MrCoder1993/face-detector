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
            cmbSources = new ComboBox();
            txtUrl = new TextBox();
            btnRefresh = new Button();
            btnStart = new Button();
            btnStop = new Button();
            picturePreview = new PictureBox();
            panelTop.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)picturePreview).BeginInit();
            SuspendLayout();
            // 
            // panelTop
            // 
            panelTop.Controls.Add(cmbSources);
            panelTop.Controls.Add(txtUrl);
            panelTop.Controls.Add(btnRefresh);
            panelTop.Controls.Add(btnStart);
            panelTop.Controls.Add(btnStop);
            panelTop.Dock = DockStyle.Top;
            panelTop.Location = new Point(0, 0);
            panelTop.Name = "panelTop";
            panelTop.Padding = new Padding(8);
            panelTop.Size = new Size(979, 84);
            panelTop.TabIndex = 0;
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
            txtUrl.Size = new Size(210, 23);
            txtUrl.TabIndex = 1;
            txtUrl.Text = "rtsp://admin:@Coin@123@192.168.40.12:554/cam/realmonitor?channel=1&subtype=1";
            // 
            // btnRefresh
            // 
            btnRefresh.Location = new Point(227, 10);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(75, 23);
            btnRefresh.TabIndex = 2;
            btnRefresh.Text = "Refresh";
            btnRefresh.UseVisualStyleBackColor = true;
            btnRefresh.Click += btnRefresh_Click;
            // 
            // btnStart
            // 
            btnStart.Location = new Point(308, 10);
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
            btnStop.Location = new Point(374, 10);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(60, 23);
            btnStop.TabIndex = 4;
            btnStop.Text = "Stop";
            btnStop.UseVisualStyleBackColor = true;
            btnStop.Click += btnStop_Click;
            // 
            // picturePreview
            // 
            picturePreview.Dock = DockStyle.Fill;
            picturePreview.Location = new Point(0, 84);
            picturePreview.Name = "picturePreview";
            picturePreview.Size = new Size(979, 535);
            picturePreview.SizeMode = PictureBoxSizeMode.Zoom;
            picturePreview.TabIndex = 1;
            picturePreview.TabStop = false;
            // 
            // Camera
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(979, 619);
            Controls.Add(picturePreview);
            Controls.Add(panelTop);
            Name = "Camera";
            Text = "Camera";
            FormClosing += Camera_FormClosing;
            Load += Camera_Load;
            panelTop.ResumeLayout(false);
            panelTop.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)picturePreview).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Panel panelTop;
        private ComboBox cmbSources;
        private TextBox txtUrl;
        private Button btnRefresh;
        private Button btnStart;
        private Button btnStop;
        private PictureBox picturePreview;
    }
}