// Copyright (c) 2008-2025 Optris GmbH & Co. KG

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using Optris.OtcSDK;
using SimpleViewCS.models;

namespace SimpleViewCS
{
    /// <summary>Main windows of the application.</summary>
    public partial class DisplayForm : Form
    {
        private IRImagerShow imagerShow = new();
        private System.Windows.Forms.Timer uiUpdateTimer = new();
        private bool recordingIndicatorVisible = false;

        /// <summary>Constructor.</summary>
        public DisplayForm()
        {
            InitializeComponent();
        }

        /// <summary>Connects to the device specified in the configuration file.</summary>
        /// 
        /// <param name="filename">path to the configuration file of the device to connect to.</param>
        private void Connect(string filename)
        {
            if (imagerShow.IsConnected)
            {
                return;
            }

            try
            {
                imagerShow.Connect(filename);
            }
            catch (SDKException ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            UpdateUiOnConnectionStatus();
        }

        /// <summary>Quickly connects to the first detected device on the USB port.</summary>
        private void QuickConnect()
        {
            if (imagerShow.IsConnected)
            {
                return;
            }

            try
            {
                imagerShow.QuickConnect();
            }
            catch (SDKException ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            UpdateUiOnConnectionStatus();
        }

        /// <summary>Opens a file dialog to choose the configuration file of the device to connect to and starts the connection.</summary>
        private void ConnectWithConfigSelection()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "Please select the configuration file of the device to connect to...";
            openFileDialog.Filter = "XML configuration files (*.xml)|*.xml|All files (*.*)|*.*";
            openFileDialog.Multiselect = false;

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                Connect(openFileDialog.FileName);
            }
        }

        /// <summary>Disconnects form the currently connected device.</summary>
        private void Disconnect()
        {
            if (!imagerShow.IsConnected)
            {
                return;
            }

            imagerShow.Disconnect();

            UpdateUiOnConnectionStatus();
        }

        /// <summary>Callback of the UI update timer.</summary>
        private void UpdateUI(object? sender, EventArgs e)
        {
            if (!imagerShow.IsConnected || imagerShow.IsConnectionLost)
            {
                Disconnect();
                return;
            }

            sbOperationMode.Text = imagerShow.OperationModeString;
            sbFlag.Text = imagerShow.GetFlagState();
            sbFPS.Text = imagerShow.GetFPS().ToString() + " Hz";

            Bitmap? image = imagerShow.GetImage();

            if (imagerShow.IsRecording && recordingIndicatorVisible)
            {
                DrawRecordingIndicator();
            }

            if (image != null)
            {
                // False color image
                thermalImage.Image = image;

                // Determine the coldest and hottest region with the given radius in the thermal frame
                if (imagerShow.CalculateMinMaxTemperatureRegions())
                {
                    /*
                    // Draws a blue crosshair at the center of the lowest temperature region in the thermal frame
                    drawMeasurement((imagerShow.MinRegion.x1 + imagerShow.MinRegion.x2) / 2,
                                    (imagerShow.MinRegion.y1 + imagerShow.MinRegion.y2) / 2,
                                    imagerShow.MinRegion.temperature,
                                    Color.Blue,
                                    Color.White);
                    */
                    // Draws a red crosshair at the center of the hottest temperature region in the thermal frame
                    drawMeasurement((imagerShow.MaxRegion.x1 + imagerShow.MaxRegion.x2) / 2,
                                    (imagerShow.MaxRegion.y1 + imagerShow.MaxRegion.y2) / 2,
                                    imagerShow.MaxRegion.temperature,
                                    Color.Red,
                                    Color.White);

                    minTemp.Text = imagerShow.MinRegion.temperature.ToString("N2");
                    maxTemp.Text = imagerShow.MaxRegion.temperature.ToString("N2");

                    if (AutoTempScale.Checked)
                    {
                        imageScaleLow.Value = (int)imagerShow.MinRegion.temperature;
                        imageScaleHigh.Value = (int)imagerShow.MaxRegion.temperature;
                    }

                }

                // Calculate the mean temperature in a small region in the center of thermal frame
                /*
                if (imagerShow.CalculateCenterMeanTemperatureRegion())
                {
                    // Draws a white cross hair in the center of the display with the mean temperature
                    drawMeasurement(imagerShow.Imager.getWidth() / 2 - 1,
                                    imagerShow.Imager.getHeight() / 2 - 1,
                                    imagerShow.MeanRegion.temperature,
                                    Color.Black,
                                    Color.White);
                }
                */

                thermalImage.Invalidate();
            }
        }
        private void DrawRecordingIndicator()
        {
            if (thermalImage.Image == null)
                return;

            using (Graphics g = Graphics.FromImage(thermalImage.Image))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                g.FillEllipse(
                    Brushes.Red,
                    10,
                    10,
                    20,
                    20);

                g.DrawString(
                    "REC",
                    new Font("Segoe UI", 12, FontStyle.Bold),
                    Brushes.Red,
                    40,
                    5);
            }
        }

        /// <summary>Draws a marker at the position of a temperature measurement and its value next to it.</summary>
        /// <param name="x">x position.</param>
        /// <param name="y">y position.</param>
        /// <param name="value">of the measurement.</param>
        /// <param name="fgColor">foreground color to use.</param>
        /// <param name="bgColor">background color to use.</param>
        private void drawMeasurement(int x, int y, float value, Color fgColor, Color bgColor)
        {
            int markerSize = 20;
            int markerSizeHalf = markerSize / 2;

            using (Graphics g = Graphics.FromImage(thermalImage.Image))
            using (GraphicsPath path = new GraphicsPath(FillMode.Winding))
            using (Brush fgBrush = new SolidBrush(fgColor))
            using (Pen fgPen = new Pen(fgBrush, 1))
            using (Pen bgPen = new Pen(bgColor, 3))
            {
                // Rendering options
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                // Draw marker denoting measurement position
                g.DrawLine(bgPen, x - markerSizeHalf, y, x + markerSizeHalf, y);
                g.DrawLine(bgPen, x, y - markerSizeHalf, x, y + markerSizeHalf);

                g.DrawLine(fgPen, x - markerSizeHalf, y, x + markerSizeHalf, y);
                g.DrawLine(fgPen, x, y - markerSizeHalf, x, y + markerSizeHalf);

                // Measurement value
                path.AddString(string.Format("{0:N1}", value),
                               SystemFonts.DefaultFont.FontFamily,
                               (int)FontStyle.Regular,
                               (float)(12),
                               new Point(x + markerSizeHalf / 2, y - markerSizeHalf * 2),
                               StringFormat.GenericDefault);

                g.DrawPath(bgPen, path);
                g.FillPath(fgBrush, path);
            }
        }

        /// <summary>Updates the UI based on the current connection status.</summary>
        private void UpdateUiOnConnectionStatus()
        {
            bool connected = imagerShow.IsConnected;

            if (connected)
            {
                // Title bar
                Text = "Optris Imager - " + imagerShow.GetDeviceType() + " (S/N " + imagerShow.GetSerialNumber().ToString() + ")";

                // Status bar
                sbOperationMode.Text = imagerShow.OperationModeString;
                sbFlag.Text = imagerShow.GetFlagState();

                // Ui updates
                uiUpdateTimer.Tick += new EventHandler(UpdateUI);
                uiUpdateTimer.Interval = 1;
                uiUpdateTimer.Start();
            }
            else
            {
                // Title bar
                Text = "Optris Imager";

                // Status bar
                sbOperationMode.Text = "";
                sbFlag.Text = string.Format("{0, 18}", " ");
                sbFPS.Text = string.Format("{0, 11}", " ");

                // Remove displayed false color image
                thermalImage.Image = null;
                thermalImage.Invalidate();

                // Ui updates
                uiUpdateTimer.Stop();
            }

            // Menu
            miDeviceQuickConnect.Enabled = !connected;
            miDeviceConnect.Enabled = !connected;
            miDeviceDisconnect.Enabled = connected;
            miDeviceRefreshFlag.Enabled = connected;
            controlPanel.Enabled = connected;
            imageConfigurationToolStripMenuItem.Enabled = connected;

            SetAutoScalingRange();

            switch (imagerShow.ActiveModeIndex)
            {
                case 0:
                    opMode1.Checked = true;
                    break;

                case 1:
                    opMode2.Checked = true;
                    break;

                case 2:
                    opMode3.Checked = true;
                    break;
            }
        }
        private void SetAutoScalingRange()
        {
            var range = imagerShow.GetTemperatureRange();
            imageScaleLow.Minimum = (int)range.Lower - 50;
            imageScaleHigh.Maximum = (int)range.Upper + 50;
        }

        /// <summary>Action called when the quick connect menu entry is clicked.</summary>
        private void miDeviceQuickConnect_Click(object sender, EventArgs e)
        {
            QuickConnect();
        }

        /// <summary>Action called when the connect menu entry is clicked.</summary>
        private void miDeviceConnect_Click(object? sender, EventArgs e)
        {
            ConnectWithConfigSelection();
        }

        /// <summary>Action called when the disconnect menu entry is clicked.</summary>
        private void miDeviceDisconnect_Click(object? sender, EventArgs e)
        {
            Disconnect();
        }

        /// <summary>Action called when the refresh flag menu entry is clicked.</summary>
        private void miDeviceRefreshFlag_Click(object sender, EventArgs e)
        {
            imagerShow.RefreshFlag();
        }

        /// <summary>Action called when the quit menu entry is clicked.</summary>
        private void msFileQuit_Click(object? sender, EventArgs e)
        {
            Disconnect();
            Application.Exit();
        }

        /// <summary>Action called prior to closing the window.</summary>
        private void DisplayForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            Disconnect();
        }

        private void DisplayForm_Load(object sender, EventArgs e)
        {
            BuildPaletteMenu();
        }

        private void BuildPaletteMenu()
        {
            colorPaletteToolStripMenuItem.DropDownItems.Clear();

            foreach (ColoringPalette palette in Enum.GetValues(typeof(ColoringPalette)))
            {
                var item = new ToolStripMenuItem(palette.ToString())
                {
                    Tag = palette,
                    CheckOnClick = false // we manually enforce single selection
                };

                colorPaletteToolStripMenuItem.DropDownItems.Add(item);
            }
            SetSelectedPalette(ColoringPalette.Iron);
        }

        //data directory
        private void openFileDialog1_FileOk(object sender, CancelEventArgs e)
        {

        }

        private void saveDirectory_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                // Optional configuration
                folderDialog.Description = "Select the directory to save data to";
                folderDialog.UseDescriptionForTitle = true; // Use description text as the window title
                folderDialog.InitialDirectory = @"C:\Users\Public"; // Set starting point

                // Display the dialog and verify the user pressed 'OK'
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    // Capture the absolute path to the directory
                    string selectedPath = folderDialog.SelectedPath;

                    // Output confirmation or use the path
                    saveDirectory.Text = selectedPath;
                }
            }
            recordEnable.Enabled = true;
        }
        private void recordEnable_Click(object sender, EventArgs e)
        {
            if (!imagerShow.IsRecording)
            {
                imagerShow.StartRecording(
                    saveDirectory.Text);
                recordEnable.BackColor = Color.LimeGreen;
                recordEnable.Text = "Stop Recording";
                recordingIndicatorVisible = true;
                blinkTimer.Start();
            }
            else
            {
                imagerShow.StopRecording();
                recordEnable.BackColor = SystemColors.Control;
                recordEnable.Text = "Record";
                recordingIndicatorVisible = false;
                blinkTimer.Stop();
            }
        }

        private void SetSelectedPalette(ColoringPalette palette)
        {
            foreach (ToolStripMenuItem item in colorPaletteToolStripMenuItem.DropDownItems)
            {
                if (item.Tag is ColoringPalette p)
                {
                    item.Checked = (p == palette);
                }
            }
        }
        private void AutoTempScale_CheckedChanged(object sender, EventArgs e)
        {
            bool autoTempScaleEnabled = AutoTempScale.Checked;
            imageScaleHigh.ReadOnly = autoTempScaleEnabled;
            imageScaleLow.ReadOnly = autoTempScaleEnabled;
            imagerShow.SetAutoScaling(autoTempScaleEnabled);
        }

        private void imageScaleHigh_ValueChanged(object sender, EventArgs e)
        {
            if (AutoTempScale.Checked) return;
            imagerShow.SetScaleRange(
                (float)imageScaleLow.Value,
                (float)imageScaleHigh.Value);
        }

        private void imageScaleLow_ValueChanged(object sender, EventArgs e)
        {
            if (AutoTempScale.Checked) return;
            imagerShow.SetScaleRange(
                (float)imageScaleLow.Value,
                (float)imageScaleHigh.Value);
        }
        private void opMode1_CheckedChanged(object sender, EventArgs e)
        {
            if (!opMode1.Checked)
                return;

            imagerShow.SetOperationMode(0);
            SetAutoScalingRange();
        }
        private void opMode2_CheckedChanged(object sender, EventArgs e)
        {
            if (!opMode2.Checked)
                return;

            imagerShow.SetOperationMode(1);
            SetAutoScalingRange();
        }
        private void opMode3_CheckedChanged(object sender, EventArgs e)
        {
            if (!opMode3.Checked)
                return;

            imagerShow.SetOperationMode(2);
            SetAutoScalingRange();
        }

        private void blinkTimer_Tick(object sender, EventArgs e)
        {
            recordingIndicatorVisible = !recordingIndicatorVisible;
        }
    }
}
