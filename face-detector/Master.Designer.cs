namespace face_detector
{
    partial class Master
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
            tabs = new TabControl();
            live_tab = new TabPage();
            camera_process_tab = new TabPage();
            groupBox_live = new GroupBox();
            camera_list_tab = new TabPage();
            cmbSources = new ComboBox();
            add_btn = new Button();
            rstp_address = new TextBox();
            source_lbl = new Label();
            webcam_checkbox = new CheckBox();
            title_txtbox = new TextBox();
            title_lbl = new Label();
            dataGridView1 = new DataGridView();
            title = new DataGridViewTextBoxColumn();
            address = new DataGridViewTextBoxColumn();
            webcam = new DataGridViewCheckBoxColumn();
            faces_tab = new TabPage();
            refresh_btn = new Button();
            tabs.SuspendLayout();
            camera_process_tab.SuspendLayout();
            camera_list_tab.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            faces_tab.SuspendLayout();
            SuspendLayout();
            // 
            // tabs
            // 
            tabs.Controls.Add(live_tab);
            tabs.Controls.Add(camera_process_tab);
            tabs.Controls.Add(camera_list_tab);
            tabs.Controls.Add(faces_tab);
            tabs.Location = new Point(12, 12);
            tabs.Name = "tabs";
            tabs.RightToLeft = RightToLeft.Yes;
            tabs.RightToLeftLayout = true;
            tabs.SelectedIndex = 0;
            tabs.Size = new Size(1028, 645);
            tabs.TabIndex = 0;
            tabs.Tag = "";
            // 
            // live_tab
            // 
            live_tab.Location = new Point(4, 24);
            live_tab.Name = "live_tab";
            live_tab.Size = new Size(1020, 617);
            live_tab.TabIndex = 4;
            live_tab.Text = "افراد حاضر";
            live_tab.UseVisualStyleBackColor = true;
            // 
            // camera_process_tab
            // 
            camera_process_tab.Controls.Add(groupBox_live);
            camera_process_tab.Location = new Point(4, 24);
            camera_process_tab.Name = "camera_process_tab";
            camera_process_tab.Padding = new Padding(3);
            camera_process_tab.Size = new Size(1020, 599);
            camera_process_tab.TabIndex = 1;
            camera_process_tab.Text = "پردازش دوربین";
            camera_process_tab.UseVisualStyleBackColor = true;
            // 
            // groupBox_live
            // 
            groupBox_live.Location = new Point(8, 6);
            groupBox_live.Name = "groupBox_live";
            groupBox_live.Size = new Size(1006, 587);
            groupBox_live.TabIndex = 0;
            groupBox_live.TabStop = false;
            groupBox_live.Text = "پخش زنده";
            groupBox_live.Resize += groupBox_live_Resize;
            // 
            // camera_list_tab
            // 
            camera_list_tab.Controls.Add(cmbSources);
            camera_list_tab.Controls.Add(add_btn);
            camera_list_tab.Controls.Add(rstp_address);
            camera_list_tab.Controls.Add(source_lbl);
            camera_list_tab.Controls.Add(webcam_checkbox);
            camera_list_tab.Controls.Add(title_txtbox);
            camera_list_tab.Controls.Add(title_lbl);
            camera_list_tab.Controls.Add(dataGridView1);
            camera_list_tab.Location = new Point(4, 24);
            camera_list_tab.Name = "camera_list_tab";
            camera_list_tab.Padding = new Padding(3);
            camera_list_tab.Size = new Size(1020, 599);
            camera_list_tab.TabIndex = 2;
            camera_list_tab.Text = "لیست دوربین ها";
            camera_list_tab.UseVisualStyleBackColor = true;
            // 
            // cmbSources
            // 
            cmbSources.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbSources.FormattingEnabled = true;
            cmbSources.Location = new Point(12, 140);
            cmbSources.Name = "cmbSources";
            cmbSources.Size = new Size(252, 23);
            cmbSources.TabIndex = 9;
            cmbSources.Visible = false;
            // 
            // add_btn
            // 
            add_btn.Location = new Point(12, 191);
            add_btn.Name = "add_btn";
            add_btn.Size = new Size(252, 23);
            add_btn.TabIndex = 8;
            add_btn.Text = "افزودن";
            add_btn.UseVisualStyleBackColor = true;
            add_btn.Click += add_btn_Click;
            // 
            // rstp_address
            // 
            rstp_address.Location = new Point(12, 140);
            rstp_address.Name = "rstp_address";
            rstp_address.Size = new Size(252, 23);
            rstp_address.TabIndex = 4;
            // 
            // source_lbl
            // 
            source_lbl.AutoSize = true;
            source_lbl.ForeColor = Color.Black;
            source_lbl.Location = new Point(205, 122);
            source_lbl.Name = "source_lbl";
            source_lbl.Size = new Size(59, 15);
            source_lbl.TabIndex = 6;
            source_lbl.Text = "لینک RSTP";
            // 
            // webcam_checkbox
            // 
            webcam_checkbox.AutoSize = true;
            webcam_checkbox.Location = new Point(215, 91);
            webcam_checkbox.Name = "webcam_checkbox";
            webcam_checkbox.Size = new Size(49, 19);
            webcam_checkbox.TabIndex = 3;
            webcam_checkbox.Text = "وبکم";
            webcam_checkbox.UseVisualStyleBackColor = true;
            webcam_checkbox.CheckedChanged += webcam_checkbox_CheckedChanged;
            // 
            // title_txtbox
            // 
            title_txtbox.Location = new Point(12, 48);
            title_txtbox.Name = "title_txtbox";
            title_txtbox.Size = new Size(252, 23);
            title_txtbox.TabIndex = 2;
            // 
            // title_lbl
            // 
            title_lbl.AutoSize = true;
            title_lbl.Location = new Point(229, 30);
            title_lbl.Name = "title_lbl";
            title_lbl.Size = new Size(35, 15);
            title_lbl.TabIndex = 1;
            title_lbl.Text = "عنوان";
            // 
            // dataGridView1
            // 
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Columns.AddRange(new DataGridViewColumn[] { title, address, webcam });
            dataGridView1.Location = new Point(270, 15);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.ReadOnly = true;
            dataGridView1.Size = new Size(743, 578);
            dataGridView1.TabIndex = 0;
            dataGridView1.UserDeletingRow += delete_btn_Click;
            // 
            // title
            // 
            title.HeaderText = "عنوان";
            title.MinimumWidth = 200;
            title.Name = "title";
            title.ReadOnly = true;
            title.Width = 200;
            // 
            // address
            // 
            address.HeaderText = "آدرس";
            address.MinimumWidth = 400;
            address.Name = "address";
            address.ReadOnly = true;
            address.Width = 400;
            // 
            // webcam
            // 
            webcam.HeaderText = "وبکم";
            webcam.Name = "webcam";
            webcam.ReadOnly = true;
            // 
            // faces_tab
            // 
            faces_tab.Controls.Add(refresh_btn);
            faces_tab.Location = new Point(4, 24);
            faces_tab.Name = "faces_tab";
            faces_tab.Padding = new Padding(3);
            faces_tab.Size = new Size(1020, 599);
            faces_tab.TabIndex = 3;
            faces_tab.Text = "آلبوم تصاویر";
            faces_tab.UseVisualStyleBackColor = true;
            // 
            // refresh_btn
            // 
            refresh_btn.Location = new Point(0, 0);
            refresh_btn.Name = "refresh_btn";
            refresh_btn.Size = new Size(35, 23);
            refresh_btn.TabIndex = 1;
            refresh_btn.Text = "R";
            refresh_btn.UseVisualStyleBackColor = true;
            refresh_btn.Click += refresh_btn_Click;
            // 
            // Master
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1047, 659);
            Controls.Add(tabs);
            Name = "Master";
            Text = "اتاق فرمان";
            tabs.ResumeLayout(false);
            camera_process_tab.ResumeLayout(false);
            camera_list_tab.ResumeLayout(false);
            camera_list_tab.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            faces_tab.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private TabControl tabs;
        private TabPage camera_process_tab;
        private TabPage camera_list_tab;
        private TabPage faces_tab;
        private TabPage live_tab;
        private DataGridView dataGridView1;
        private Label title_lbl;
        private DataGridViewTextBoxColumn title;
        private DataGridViewTextBoxColumn address;
        private DataGridViewCheckBoxColumn webcam;
        private CheckBox webcam_checkbox;
        private TextBox title_txtbox;
        private TextBox rstp_address;
        private Label source_lbl;
        private Button add_btn;
        private ComboBox cmbSources;
        private GroupBox groupBox_live;
        private Button refresh_btn;
    }
}