// Copyright (c) 2008-2025 Optris GmbH & Co. KG

namespace SimpleViewCS
{
    partial class DisplayForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            thermalImage = new PictureBox();
            menuStrip1 = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            msFileQuit = new ToolStripMenuItem();
            msDevice = new ToolStripMenuItem();
            miDeviceQuickConnect = new ToolStripMenuItem();
            miDeviceConnect = new ToolStripMenuItem();
            miDeviceDisconnect = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            miDeviceRefreshFlag = new ToolStripMenuItem();
            imageConfigurationToolStripMenuItem = new ToolStripMenuItem();
            colorPaletteToolStripMenuItem = new ToolStripMenuItem();
            statusStrip = new StatusStrip();
            sbOperationMode = new ToolStripStatusLabel();
            sbFlag = new ToolStripStatusLabel();
            sbFPS = new ToolStripStatusLabel();
            recordEnable = new Button();
            controlPanel = new Panel();
            recordingBox = new GroupBox();
            saveDataTypeBox = new GroupBox();
            saveTypeAll = new RadioButton();
            saveTypeRle = new RadioButton();
            saveTypeInt = new RadioButton();
            saveTypeBase = new RadioButton();
            saveDirectory = new Button();
            tempOutputBox = new GroupBox();
            operationMode = new GroupBox();
            opMode2 = new RadioButton();
            opMode3 = new RadioButton();
            opMode1 = new RadioButton();
            minTempLabel = new Label();
            maxLabel = new Label();
            minTemp = new Label();
            maxTemp = new Label();
            AutoTempScale = new CheckBox();
            imageScale = new GroupBox();
            imageScaleLow = new NumericUpDown();
            degreeLabel2 = new Label();
            imageScaleHigh = new NumericUpDown();
            degreeLabel = new Label();
            toolTip1 = new ToolTip(components);
            blinkTimer = new System.Windows.Forms.Timer(components);
            openSaveDirectory = new FolderBrowserDialog();
            errorProvider1 = new ErrorProvider(components);
            ((System.ComponentModel.ISupportInitialize)thermalImage).BeginInit();
            menuStrip1.SuspendLayout();
            statusStrip.SuspendLayout();
            controlPanel.SuspendLayout();
            recordingBox.SuspendLayout();
            saveDataTypeBox.SuspendLayout();
            tempOutputBox.SuspendLayout();
            operationMode.SuspendLayout();
            imageScale.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)imageScaleLow).BeginInit();
            ((System.ComponentModel.ISupportInitialize)imageScaleHigh).BeginInit();
            ((System.ComponentModel.ISupportInitialize)errorProvider1).BeginInit();
            SuspendLayout();
            // 
            // thermalImage
            // 
            thermalImage.BackColor = SystemColors.ControlDark;
            thermalImage.Dock = DockStyle.Fill;
            thermalImage.Location = new Point(0, 0);
            thermalImage.Margin = new Padding(3, 4, 3, 4);
            thermalImage.Name = "thermalImage";
            thermalImage.Size = new Size(1160, 879);
            thermalImage.SizeMode = PictureBoxSizeMode.Zoom;
            thermalImage.TabIndex = 0;
            thermalImage.TabStop = false;
            // 
            // menuStrip1
            // 
            menuStrip1.ImageScalingSize = new Size(20, 20);
            menuStrip1.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, msDevice, imageConfigurationToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Padding = new Padding(7, 3, 0, 3);
            menuStrip1.Size = new Size(1160, 30);
            menuStrip1.TabIndex = 1;
            menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { msFileQuit });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(46, 24);
            fileToolStripMenuItem.Text = "&File";
            // 
            // msFileQuit
            // 
            msFileQuit.Name = "msFileQuit";
            msFileQuit.ShortcutKeys = Keys.Control | Keys.Q;
            msFileQuit.Size = new Size(173, 26);
            msFileQuit.Text = "&Quit";
            msFileQuit.TextAlign = ContentAlignment.TopLeft;
            msFileQuit.ToolTipText = "Quit Application";
            msFileQuit.Click += msFileQuit_Click;
            // 
            // msDevice
            // 
            msDevice.DropDownItems.AddRange(new ToolStripItem[] { miDeviceQuickConnect, miDeviceConnect, miDeviceDisconnect, toolStripSeparator1, miDeviceRefreshFlag });
            msDevice.Name = "msDevice";
            msDevice.Size = new Size(68, 24);
            msDevice.Text = "&Device";
            msDevice.ToolTipText = "Connect to the First Device Available";
            // 
            // miDeviceQuickConnect
            // 
            miDeviceQuickConnect.Name = "miDeviceQuickConnect";
            miDeviceQuickConnect.ShortcutKeys = Keys.F5;
            miDeviceQuickConnect.Size = new Size(342, 26);
            miDeviceQuickConnect.Text = "&Quick Connect";
            miDeviceQuickConnect.Click += miDeviceQuickConnect_Click;
            // 
            // miDeviceConnect
            // 
            miDeviceConnect.Name = "miDeviceConnect";
            miDeviceConnect.ShortcutKeys = Keys.Control | Keys.F5;
            miDeviceConnect.Size = new Size(342, 26);
            miDeviceConnect.Text = "&Connect With Configuration...";
            miDeviceConnect.ToolTipText = "Connect to Device WIth Configuration";
            miDeviceConnect.Click += miDeviceConnect_Click;
            // 
            // miDeviceDisconnect
            // 
            miDeviceDisconnect.Enabled = false;
            miDeviceDisconnect.Name = "miDeviceDisconnect";
            miDeviceDisconnect.ShortcutKeys = Keys.Control | Keys.D;
            miDeviceDisconnect.Size = new Size(342, 26);
            miDeviceDisconnect.Text = "&Disconnect";
            miDeviceDisconnect.ToolTipText = "Disconnect From Device";
            miDeviceDisconnect.Click += miDeviceDisconnect_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(339, 6);
            // 
            // miDeviceRefreshFlag
            // 
            miDeviceRefreshFlag.Enabled = false;
            miDeviceRefreshFlag.Name = "miDeviceRefreshFlag";
            miDeviceRefreshFlag.ShortcutKeyDisplayString = "";
            miDeviceRefreshFlag.ShortcutKeys = Keys.Control | Keys.R;
            miDeviceRefreshFlag.Size = new Size(342, 26);
            miDeviceRefreshFlag.Text = "&Refresh Flag";
            miDeviceRefreshFlag.ToolTipText = "Refresh Shutter Flag";
            miDeviceRefreshFlag.Click += miDeviceRefreshFlag_Click;
            // 
            // imageConfigurationToolStripMenuItem
            // 
            imageConfigurationToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { colorPaletteToolStripMenuItem });
            imageConfigurationToolStripMenuItem.Enabled = false;
            imageConfigurationToolStripMenuItem.Name = "imageConfigurationToolStripMenuItem";
            imageConfigurationToolStripMenuItem.Size = new Size(160, 24);
            imageConfigurationToolStripMenuItem.Text = "Image Configuration";
            // 
            // colorPaletteToolStripMenuItem
            // 
            colorPaletteToolStripMenuItem.Name = "colorPaletteToolStripMenuItem";
            colorPaletteToolStripMenuItem.Size = new Size(177, 26);
            colorPaletteToolStripMenuItem.Text = "Color Palette";
            colorPaletteToolStripMenuItem.ToolTipText = "Change the color filter of the image";
            // 
            // statusStrip
            // 
            statusStrip.ImageScalingSize = new Size(20, 20);
            statusStrip.Items.AddRange(new ToolStripItem[] { sbOperationMode, sbFlag, sbFPS });
            statusStrip.Location = new Point(0, 849);
            statusStrip.Name = "statusStrip";
            statusStrip.Padding = new Padding(1, 0, 16, 0);
            statusStrip.Size = new Size(1160, 30);
            statusStrip.TabIndex = 2;
            statusStrip.Text = "statusStrip1";
            // 
            // sbOperationMode
            // 
            sbOperationMode.BorderSides = ToolStripStatusLabelBorderSides.Left | ToolStripStatusLabelBorderSides.Top | ToolStripStatusLabelBorderSides.Right | ToolStripStatusLabelBorderSides.Bottom;
            sbOperationMode.BorderStyle = Border3DStyle.SunkenOuter;
            sbOperationMode.Name = "sbOperationMode";
            sbOperationMode.Padding = new Padding(5, 0, 5, 0);
            sbOperationMode.Size = new Size(953, 24);
            sbOperationMode.Spring = true;
            sbOperationMode.TextAlign = ContentAlignment.MiddleLeft;
            sbOperationMode.ToolTipText = "Active Operation Mode";
            // 
            // sbFlag
            // 
            sbFlag.BorderSides = ToolStripStatusLabelBorderSides.Left | ToolStripStatusLabelBorderSides.Top | ToolStripStatusLabelBorderSides.Right | ToolStripStatusLabelBorderSides.Bottom;
            sbFlag.BorderStyle = Border3DStyle.SunkenOuter;
            sbFlag.DisplayStyle = ToolStripItemDisplayStyle.Text;
            sbFlag.Name = "sbFlag";
            sbFlag.Padding = new Padding(5, 0, 5, 0);
            sbFlag.Size = new Size(95, 24);
            sbFlag.Text = "                  ";
            sbFlag.ToolTipText = "Shutter Flag State";
            // 
            // sbFPS
            // 
            sbFPS.BorderSides = ToolStripStatusLabelBorderSides.Left | ToolStripStatusLabelBorderSides.Top | ToolStripStatusLabelBorderSides.Right | ToolStripStatusLabelBorderSides.Bottom;
            sbFPS.BorderStyle = Border3DStyle.SunkenOuter;
            sbFPS.DisplayStyle = ToolStripItemDisplayStyle.Text;
            sbFPS.Name = "sbFPS";
            sbFPS.Padding = new Padding(5, 0, 5, 0);
            sbFPS.Size = new Size(95, 24);
            sbFPS.Text = "                  ";
            sbFPS.TextAlign = ContentAlignment.MiddleRight;
            sbFPS.ToolTipText = "FPS";
            // 
            // recordEnable
            // 
            recordEnable.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            recordEnable.Enabled = false;
            recordEnable.Location = new Point(88, 186);
            recordEnable.Name = "recordEnable";
            recordEnable.Size = new Size(86, 29);
            recordEnable.TabIndex = 3;
            recordEnable.Text = "Record";
            recordEnable.UseVisualStyleBackColor = true;
            recordEnable.Click += recordEnable_Click;
            // 
            // controlPanel
            // 
            controlPanel.Controls.Add(recordingBox);
            controlPanel.Controls.Add(tempOutputBox);
            controlPanel.Controls.Add(imageScale);
            controlPanel.Dock = DockStyle.Right;
            controlPanel.Enabled = false;
            controlPanel.Location = new Point(1160, 0);
            controlPanel.Margin = new Padding(3, 4, 3, 4);
            controlPanel.MinimumSize = new Size(280, 0);
            controlPanel.Name = "controlPanel";
            controlPanel.Size = new Size(378, 879);
            controlPanel.TabIndex = 6;
            // 
            // recordingBox
            // 
            recordingBox.BackColor = Color.Transparent;
            recordingBox.Controls.Add(saveDataTypeBox);
            recordingBox.Controls.Add(recordEnable);
            recordingBox.Controls.Add(saveDirectory);
            recordingBox.Location = new Point(93, 642);
            recordingBox.Name = "recordingBox";
            recordingBox.Size = new Size(268, 219);
            recordingBox.TabIndex = 17;
            recordingBox.TabStop = false;
            recordingBox.Text = "Recording";
            // 
            // saveDataTypeBox
            // 
            saveDataTypeBox.Controls.Add(saveTypeBase);
            saveDataTypeBox.Controls.Add(saveTypeRle);
            saveDataTypeBox.Controls.Add(saveTypeInt);
            saveDataTypeBox.Controls.Add(saveTypeAll);
            saveDataTypeBox.Location = new Point(6, 60);
            saveDataTypeBox.Name = "saveDataTypeBox";
            saveDataTypeBox.Size = new Size(256, 120);
            saveDataTypeBox.TabIndex = 12;
            saveDataTypeBox.TabStop = false;
            saveDataTypeBox.Text = "Save Data Type";
            // 
            // saveTypeAll
            // 
            saveTypeAll.AutoSize = true;
            saveTypeAll.Checked = false;
            saveTypeAll.Location = new Point(130, 71);
            saveTypeAll.Name = "saveTypeAll";
            saveTypeAll.Size = new Size(45, 24);
            saveTypeAll.TabIndex = 3;
            saveTypeAll.TabStop = true;
            saveTypeAll.Text = "All";
            saveTypeAll.UseVisualStyleBackColor = true;
            // 
            // saveTypeRle
            // 
            saveTypeRle.AutoSize = true;
            saveTypeRle.Location = new Point(16, 71);
            saveTypeRle.Name = "saveTypeRle";
            saveTypeRle.Size = new Size(87, 24);
            saveTypeRle.TabIndex = 2;
            saveTypeRle.TabStop = true;
            saveTypeRle.Text = "RLE Data";
            saveTypeRle.UseVisualStyleBackColor = true;
            // 
            // saveTypeInt
            // 
            saveTypeInt.AutoSize = true;
            saveTypeInt.Location = new Point(130, 32);
            saveTypeInt.Name = "saveTypeInt";
            saveTypeInt.Size = new Size(79, 24);
            saveTypeInt.TabIndex = 1;
            saveTypeInt.TabStop = true;
            saveTypeInt.Text = "IntData";
            saveTypeInt.UseVisualStyleBackColor = true;
            // 
            // saveTypeBase
            // 
            saveTypeBase.AutoSize = true;
            saveTypeBase.Checked = true;
            saveTypeBase.Location = new Point(16, 32);
            saveTypeBase.Name = "saveTypeBase";
            saveTypeBase.Size = new Size(92, 24);
            saveTypeBase.TabIndex = 0;
            saveTypeBase.TabStop = true;
            saveTypeBase.Text = "BaseData";
            saveTypeBase.UseVisualStyleBackColor = true;
            // 
            // saveDirectory
            // 
            saveDirectory.Location = new Point(6, 26);
            saveDirectory.Name = "saveDirectory";
            saveDirectory.Size = new Size(256, 29);
            saveDirectory.TabIndex = 11;
            saveDirectory.Text = "Set Save Directory";
            saveDirectory.UseVisualStyleBackColor = true;
            saveDirectory.Click += saveDirectory_Click;
            // 
            // tempOutputBox
            // 
            tempOutputBox.BackColor = Color.Transparent;
            tempOutputBox.Controls.Add(operationMode);
            tempOutputBox.Controls.Add(minTempLabel);
            tempOutputBox.Controls.Add(maxLabel);
            tempOutputBox.Controls.Add(minTemp);
            tempOutputBox.Controls.Add(maxTemp);
            tempOutputBox.Controls.Add(AutoTempScale);
            tempOutputBox.Location = new Point(93, 425);
            tempOutputBox.Margin = new Padding(3, 4, 3, 4);
            tempOutputBox.Name = "tempOutputBox";
            tempOutputBox.Padding = new Padding(3, 4, 3, 4);
            tempOutputBox.Size = new Size(268, 192);
            tempOutputBox.TabIndex = 14;
            tempOutputBox.TabStop = false;
            tempOutputBox.Text = "Temperature";
            // 
            // operationMode
            // 
            operationMode.Controls.Add(opMode2);
            operationMode.Controls.Add(opMode3);
            operationMode.Controls.Add(opMode1);
            errorProvider1.SetIconAlignment(operationMode, ErrorIconAlignment.BottomLeft);
            operationMode.Location = new Point(111, 17);
            operationMode.Margin = new Padding(3, 4, 3, 4);
            operationMode.Name = "operationMode";
            operationMode.Padding = new Padding(3, 4, 3, 4);
            operationMode.Size = new Size(142, 133);
            operationMode.TabIndex = 19;
            operationMode.TabStop = false;
            operationMode.Text = "Operation Mode";
            toolTip1.SetToolTip(operationMode, "Set the operation mode of the camera to your desired temperature range");
            // 
            // opMode2
            // 
            opMode2.CheckAlign = ContentAlignment.MiddleRight;
            opMode2.Location = new Point(2, 55);
            opMode2.Margin = new Padding(3, 4, 3, 4);
            opMode2.Name = "opMode2";
            opMode2.Size = new Size(122, 32);
            opMode2.TabIndex = 17;
            opMode2.TabStop = true;
            opMode2.Text = "     0°C–250°C";
            opMode2.TextAlign = ContentAlignment.MiddleRight;
            opMode2.UseVisualStyleBackColor = true;
            opMode2.CheckedChanged += opMode2_CheckedChanged;
            // 
            // opMode3
            // 
            opMode3.CheckAlign = ContentAlignment.MiddleRight;
            opMode3.Location = new Point(2, 92);
            opMode3.Margin = new Padding(3, 4, 3, 4);
            opMode3.Name = "opMode3";
            opMode3.Size = new Size(122, 32);
            opMode3.TabIndex = 18;
            opMode3.TabStop = true;
            opMode3.Text = "250°C–900°C";
            opMode3.TextAlign = ContentAlignment.MiddleRight;
            opMode3.UseVisualStyleBackColor = true;
            opMode3.CheckedChanged += opMode3_CheckedChanged;
            // 
            // opMode1
            // 
            opMode1.CheckAlign = ContentAlignment.MiddleRight;
            opMode1.Location = new Point(2, 21);
            opMode1.Margin = new Padding(3, 4, 3, 4);
            opMode1.Name = "opMode1";
            opMode1.Size = new Size(122, 32);
            opMode1.TabIndex = 0;
            opMode1.TabStop = true;
            opMode1.Text = " -20°C–100°C";
            opMode1.TextAlign = ContentAlignment.MiddleRight;
            opMode1.UseVisualStyleBackColor = true;
            opMode1.CheckedChanged += opMode1_CheckedChanged;
            // 
            // minTempLabel
            // 
            minTempLabel.AutoSize = true;
            minTempLabel.Location = new Point(11, 56);
            minTempLabel.Name = "minTempLabel";
            minTempLabel.Size = new Size(37, 20);
            minTempLabel.TabIndex = 15;
            minTempLabel.Text = "Min:";
            minTempLabel.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // maxLabel
            // 
            maxLabel.AutoSize = true;
            maxLabel.Location = new Point(10, 29);
            maxLabel.Name = "maxLabel";
            maxLabel.Size = new Size(40, 20);
            maxLabel.TabIndex = 8;
            maxLabel.Text = "Max:";
            maxLabel.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // minTemp
            // 
            minTemp.Location = new Point(45, 56);
            minTemp.Name = "minTemp";
            minTemp.Size = new Size(61, 20);
            minTemp.TabIndex = 16;
            minTemp.Text = "0.00°C";
            minTemp.TextAlign = ContentAlignment.MiddleRight;
            // 
            // maxTemp
            // 
            maxTemp.Location = new Point(45, 25);
            maxTemp.Name = "maxTemp";
            maxTemp.Size = new Size(61, 31);
            maxTemp.TabIndex = 9;
            maxTemp.Text = "0.00°C";
            maxTemp.TextAlign = ContentAlignment.MiddleRight;
            // 
            // AutoTempScale
            // 
            AutoTempScale.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            AutoTempScale.AutoSize = true;
            AutoTempScale.Checked = true;
            AutoTempScale.CheckState = CheckState.Checked;
            AutoTempScale.Location = new Point(22, 160);
            AutoTempScale.Margin = new Padding(3, 4, 3, 4);
            AutoTempScale.Name = "AutoTempScale";
            AutoTempScale.Size = new Size(227, 24);
            AutoTempScale.TabIndex = 7;
            AutoTempScale.Text = "Automatic Temperature Scale";
            AutoTempScale.UseVisualStyleBackColor = true;
            AutoTempScale.CheckedChanged += AutoTempScale_CheckedChanged;
            // 
            // imageScale
            // 
            imageScale.BackColor = Color.Transparent;
            imageScale.Controls.Add(imageScaleLow);
            imageScale.Controls.Add(degreeLabel2);
            imageScale.Controls.Add(imageScaleHigh);
            imageScale.Controls.Add(degreeLabel);
            imageScale.Location = new Point(3, 16);
            imageScale.Margin = new Padding(3, 4, 3, 4);
            imageScale.Name = "imageScale";
            imageScale.Padding = new Padding(3, 4, 3, 4);
            imageScale.Size = new Size(77, 847);
            imageScale.TabIndex = 16;
            imageScale.TabStop = false;
            imageScale.Text = "Scale";
            // 
            // imageScaleLow
            // 
            imageScaleLow.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            imageScaleLow.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            imageScaleLow.Location = new Point(5, 797);
            imageScaleLow.Margin = new Padding(3, 4, 3, 4);
            imageScaleLow.Maximum = new decimal(new int[] { 950, 0, 0, 0 });
            imageScaleLow.Name = "imageScaleLow";
            imageScaleLow.ReadOnly = true;
            imageScaleLow.Size = new Size(48, 27);
            imageScaleLow.TabIndex = 16;
            imageScaleLow.TextAlign = HorizontalAlignment.Right;
            imageScaleLow.UpDownAlign = LeftRightAlignment.Left;
            imageScaleLow.ValueChanged += imageScaleLow_ValueChanged;
            // 
            // degreeLabel2
            // 
            degreeLabel2.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            degreeLabel2.AutoSize = true;
            degreeLabel2.Location = new Point(53, 801);
            degreeLabel2.Name = "degreeLabel2";
            degreeLabel2.Size = new Size(24, 20);
            degreeLabel2.TabIndex = 9;
            degreeLabel2.Text = "°C";
            // 
            // imageScaleHigh
            // 
            imageScaleHigh.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            imageScaleHigh.Location = new Point(5, 29);
            imageScaleHigh.Margin = new Padding(3, 4, 3, 4);
            imageScaleHigh.Maximum = new decimal(new int[] { 950, 0, 0, 0 });
            imageScaleHigh.Name = "imageScaleHigh";
            imageScaleHigh.ReadOnly = true;
            imageScaleHigh.Size = new Size(48, 27);
            imageScaleHigh.TabIndex = 15;
            imageScaleHigh.TextAlign = HorizontalAlignment.Right;
            imageScaleHigh.UpDownAlign = LeftRightAlignment.Left;
            imageScaleHigh.ValueChanged += imageScaleHigh_ValueChanged;
            // 
            // degreeLabel
            // 
            degreeLabel.AutoSize = true;
            degreeLabel.Location = new Point(53, 33);
            degreeLabel.Name = "degreeLabel";
            degreeLabel.Size = new Size(24, 20);
            degreeLabel.TabIndex = 8;
            degreeLabel.Text = "°C";
            // 
            // blinkTimer
            // 
            blinkTimer.Interval = 500;
            blinkTimer.Tick += blinkTimer_Tick;
            // 
            // errorProvider1
            // 
            errorProvider1.ContainerControl = this;
            // 
            // DisplayForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1538, 879);
            Controls.Add(menuStrip1);
            Controls.Add(statusStrip);
            Controls.Add(thermalImage);
            Controls.Add(controlPanel);
            MainMenuStrip = menuStrip1;
            Margin = new Padding(3, 4, 3, 4);
            MinimumSize = new Size(683, 515);
            Name = "DisplayForm";
            Text = "Optris Imager";
            FormClosing += DisplayForm_FormClosing;
            Load += DisplayForm_Load;
            ((System.ComponentModel.ISupportInitialize)thermalImage).EndInit();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            controlPanel.ResumeLayout(false);
            recordingBox.ResumeLayout(false);
            saveDataTypeBox.ResumeLayout(false);
            saveDataTypeBox.PerformLayout();
            tempOutputBox.ResumeLayout(false);
            tempOutputBox.PerformLayout();
            operationMode.ResumeLayout(false);
            imageScale.ResumeLayout(false);
            imageScale.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)imageScaleLow).EndInit();
            ((System.ComponentModel.ISupportInitialize)imageScaleHigh).EndInit();
            ((System.ComponentModel.ISupportInitialize)errorProvider1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private PictureBox thermalImage;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem msDevice;
        private ToolStripMenuItem miDeviceConnect;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel sbFlag;
        private ToolStripStatusLabel sbFPS;
        private ToolStripMenuItem miDeviceDisconnect;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem msFileQuit;
        private ToolStripMenuItem miDeviceQuickConnect;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripMenuItem miDeviceRefreshFlag;
        private ToolStripStatusLabel sbOperationMode;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripMenuItem miDeviceCycleOperationMode;
        private Button recordEnable;
        private Panel controlPanel;
        private ToolTip toolTip1;
        private CheckBox AutoTempScale;
        private Label degreeLabel;
        private Label degreeLabel2;
        private System.Windows.Forms.Timer blinkTimer;
        private FolderBrowserDialog openSaveDirectory;
        private Button saveDirectory;
        private ToolStripMenuItem imageConfigurationToolStripMenuItem;
        private ToolStripMenuItem colorPaletteToolStripMenuItem;
        private GroupBox tempOutputBox;
        private Label maxTemp;
        private Label maxLabel;
        private Label minTemp;
        private Label minTempLabel;
        private NumericUpDown imageScaleHigh;
        private GroupBox imageScale;
        private NumericUpDown imageScaleLow;
        private ErrorProvider errorProvider1;
        private RadioButton opMode2;
        private RadioButton opMode3;
        private RadioButton opMode1;
        private GroupBox operationMode;
        private GroupBox recordingBox;
        private GroupBox saveDataTypeBox;
        private RadioButton saveTypeAll;
        private RadioButton saveTypeRle;
        private RadioButton saveTypeInt;
        private RadioButton saveTypeBase;
    }
}
