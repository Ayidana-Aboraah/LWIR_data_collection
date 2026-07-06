// Copyright (c) 2008-2025 Optris GmbH & Co. KG

using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Microsoft.Win32;
using LWIR_app.classes;
using LWIR_app.models;
using Optris.OtcSDK;
using WpfBrushes = System.Windows.Media.Brushes;

namespace LWIR_app
{
    /// <summary>Main window of the application.</summary>
    public sealed class DisplayForm : Window
    {
        private readonly IRImagerShow imagerShow = new();
        private readonly DispatcherTimer uiUpdateTimer = new();
        private readonly Dictionary<ColoringPalette, MenuItem> paletteMenuItems = new();

        private bool suppressScaleTextEvents;

        private System.Windows.Controls.Image thermalImage;
        private TextBlock sbOperationMode = new TextBlock
        {
            Text = string.Empty,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(5, 0, 5, 0)
        };

        private TextBlock sbFlag = new TextBlock
        {
            Text = "                  ",
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(5, 0, 5, 0)
        };

        private TextBlock sbFPS = new TextBlock
        {
            Text = "                  ",
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(5, 0, 5, 0),
            TextAlignment = TextAlignment.Right
        };

        private Button recordEnable = new Button
        {
            Content = "Record",
            IsEnabled = false,
            Height = 30
        };

        private Button saveDirectory = new Button
        {
            Content = "Set Save Directory",
            Height = 30,
            Margin = new Thickness(0, 0, 0, 10)
        };

        private TextBox saveDirectoryPath = new TextBox
        {
            Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Margin = new Thickness(0, 0, 0, 8)
        };

        private CheckBox singleBinaryToggle = new CheckBox
        {
            Content = "Single Binary File",
            IsChecked = false,
            Margin = new Thickness(0, 0, 0, 10)
        };

        private CheckBox autoTempScale = new CheckBox
        {
            Content = "Automatic Temperature Scale",
            IsChecked = true,
            Margin = new Thickness(0, 16, 0, 0)
        };

        private TextBox imageScaleLow;
        private TextBox imageScaleHigh;
        private TextBlock minTemp;
        private TextBlock maxTemp;

        private RadioButton[] opModes;
        // private RadioButton opMode1;
        // private RadioButton opMode2;
        // private RadioButton opMode3;

        private RadioButton[] saveTypes = new RadioButton[4]{
            new RadioButton { Content = "BaseData", IsChecked = true, Margin = new Thickness(0, 0, 20, 6) },
            new RadioButton { Content = "IntData", Margin = new Thickness(0, 0, 0, 6) },
            new RadioButton { Content = "RLE Data", Margin = new Thickness(0, 0, 20, 0) },
            new RadioButton { Content = "All" } 
        };
        // private RadioButton saveTypeBase = ;
        // private RadioButton saveTypeInt = ;
        // private RadioButton saveTypeRle = ;
        // private RadioButton saveTypeAll = ;
        private MenuItem miDeviceQuickConnect = new MenuItem { Header = "Quick Connect" };
        private MenuItem miDeviceConnect = new MenuItem { Header = "Connect With Configuration..." };
        private MenuItem miDeviceDisconnect = new MenuItem { Header = "Disconnect", IsEnabled = false };
        private MenuItem miDeviceRefreshFlag = new MenuItem { Header = "Refresh Flag", IsEnabled = false };
        private MenuItem imageConfigurationMenu = new MenuItem { Header = "Image Configuration", IsEnabled = false };
        private MenuItem colorPaletteMenu = new MenuItem { Header = "Color Palette" };
        private GroupBox saveDataTypeBox = new GroupBox
        {
            Header = "Save Data Type",
            Margin = new Thickness(0, 0, 0, 10)
        };

        /// <summary>Constructor.</summary>
        public DisplayForm()
        {
            InitializeComponent();

            uiUpdateTimer.Interval = TimeSpan.FromMilliseconds(33);
            uiUpdateTimer.Tick += (_, _) => UpdateUI();

            BuildPaletteMenu();
            UpdateUiOnConnectionStatus();
        }

        private void InitializeComponent()
        {
            Title = "Optris Imager";
            Width = 1538;
            Height = 879;
            MinWidth = 683;
            MinHeight = 515;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = WpfBrushes.Black;

            var root = new DockPanel();
            Content = root;

            var menuStrip = BuildMenu();
            DockPanel.SetDock(menuStrip, Dock.Top);
            root.Children.Add(menuStrip);

            var footer = BuildFooter();
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);

            var controlPanelBorder = BuildControlPanel();
            DockPanel.SetDock(controlPanelBorder, Dock.Right);
            root.Children.Add(controlPanelBorder);

            thermalImage = new System.Windows.Controls.Image
            {
                Stretch = Stretch.Uniform,
                SnapsToDevicePixels = true,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            RenderOptions.SetBitmapScalingMode(thermalImage, BitmapScalingMode.HighQuality);

            var thermalBorder = new Border
            {
                Background = WpfBrushes.DimGray,
                Child = thermalImage
            };
            root.Children.Add(thermalBorder);

            Closing += (_, _) => Disconnect();
        }

        private FrameworkElement BuildFooter()
        {
            var footer = new Border
            {
                Background = WpfBrushes.Gainsboro,
                BorderBrush = WpfBrushes.Gray,
                BorderThickness = new Thickness(1, 1, 0, 0),
                Padding = new Thickness(6, 4, 6, 4)
            };

            var footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(sbOperationMode, 0);
            Grid.SetColumn(sbFlag, 1);
            Grid.SetColumn(sbFPS, 2);
            footerGrid.Children.Add(sbOperationMode);
            footerGrid.Children.Add(sbFlag);
            footerGrid.Children.Add(sbFPS);

            footer.Child = footerGrid;
            return footer;
        }

        private Menu BuildMenu()
        {
            var menuStrip = new Menu();

            var fileMenu = new MenuItem { Header = "File" };
            var quitMenu = new MenuItem { Header = "Quit" };
            quitMenu.Click += (_, _) =>
            {
                Disconnect();
                Application.Current.Shutdown();
            };
            fileMenu.Items.Add(quitMenu);

            var deviceMenu = new MenuItem { Header = "Device" };

            miDeviceQuickConnect.Click += (_, _) => QuickConnect();

            miDeviceConnect.Click += (_, _) => ConnectWithConfigSelection();

            miDeviceDisconnect.Click += (_, _) => Disconnect();

            miDeviceRefreshFlag.Click += (_, _) => imagerShow.RefreshFlag();

            deviceMenu.Items.Add(miDeviceQuickConnect);
            deviceMenu.Items.Add(miDeviceConnect);
            deviceMenu.Items.Add(miDeviceDisconnect);
            deviceMenu.Items.Add(new Separator());
            deviceMenu.Items.Add(miDeviceRefreshFlag);

            imageConfigurationMenu.Items.Add(colorPaletteMenu);

            menuStrip.Items.Add(fileMenu);
            menuStrip.Items.Add(deviceMenu);
            menuStrip.Items.Add(imageConfigurationMenu);

            return menuStrip;
        }

        private Border BuildControlPanel()
        {
            var panelBorder = new Border
            {
                Width = 378,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 245)),
                BorderBrush = WpfBrushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10)
            };

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var stack = new StackPanel
            {
                Orientation = Orientation.Vertical
            };

            stack.Children.Add(BuildScaleGroup());
            stack.Children.Add(BuildTemperatureGroup());
            stack.Children.Add(BuildRecordingGroup());

            scrollViewer.Content = stack;
            panelBorder.Child = scrollViewer;
            return panelBorder;
        }

        private GroupBox BuildScaleGroup()
        {
            var group = new GroupBox
            {
                Header = "Scale",
                Margin = new Thickness(0, 0, 0, 12)
            };

            Grid panel = new Grid {Margin = new Thickness(8)};
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            FrameworkElement[] s = {
                BuildScaleRow("High:", out imageScaleHigh),
                BuildScaleRow("Low:", out imageScaleLow)
            };

            // TODO: Create a space between the elements

            for (int i = 0; i < s.Length; i++){
                Grid.SetRow(s[i], 0);
                Grid.SetColumn(s[i], i);
                panel.Children.Add(s[i]);
            }

            group.Content = panel;
            return group;
        }

        private GroupBox BuildTemperatureGroup()
        {
            var group = new GroupBox
            {
                Header = "Temperature",
                Margin = new Thickness(0, 0, 0, 12)
            };

            var layout = new Grid { Margin = new Thickness(8) };
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var valueStack = new StackPanel { Orientation = Orientation.Vertical };
            valueStack.Children.Add(BuildTemperatureValueRow("Max:", out maxTemp));
            valueStack.Children.Add(BuildTemperatureValueRow("Min:", out minTemp, 10));


            autoTempScale.Checked += (_, _) => AutoTempScale_CheckedChanged();
            autoTempScale.Unchecked += (_, _) => AutoTempScale_CheckedChanged();
            valueStack.Children.Add(autoTempScale);

            Grid.SetColumn(valueStack, 0);
            layout.Children.Add(valueStack);

            var opGroup = new GroupBox
            {
                Header = "Operation Mode",
                Margin = new Thickness(10, 0, 0, 0),
                Padding = new Thickness(6)
            };

            var opStack = new StackPanel { Orientation = Orientation.Vertical };
            opModes = new RadioButton[3]{
                BuildOperationModeRadio(" -20°C–100°C", 0),
                BuildOperationModeRadio("0°C–250°C", 1),
                BuildOperationModeRadio("250°C–900°C", 2)
            };

            foreach (RadioButton opMode in opModes) opStack.Children.Add(opMode);
            opGroup.Content = opStack;

            Grid.SetColumn(opGroup, 1);
            layout.Children.Add(opGroup);

            group.Content = layout;
            return group;
        }

        private FrameworkElement BuildTemperatureValueRow(string labelText, out TextBlock valueText, double topMargin = 0)
        {
            var row = new DockPanel
            {
                Margin = new Thickness(0, topMargin, 0, 0)
            };

            var label = new TextBlock
            {
                Text = labelText,
                Width = 48,
                VerticalAlignment = VerticalAlignment.Center
            };

            valueText = new TextBlock
            {
                Text = "0.00",
                Width = 80,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };

            var unit = new TextBlock
            {
                Text = "°C",
                VerticalAlignment = VerticalAlignment.Center
            };

            DockPanel.SetDock(label, Dock.Left);
            DockPanel.SetDock(unit, Dock.Right);

            row.Children.Add(label);
            row.Children.Add(valueText);
            row.Children.Add(unit);
            return row;
        }

        private RadioButton BuildOperationModeRadio(string text, int modeIndex)
        {
            var radio = new RadioButton
            {
                Content = text,
                Tag = modeIndex,
                Margin = new Thickness(0, 4, 0, 4),
            };
            radio.Checked += OperationMode_CheckedChanged;
            return radio;
        }

        private GroupBox BuildRecordingGroup()
        {
            var group = new GroupBox
            {
                Header = "Recording"
            };

            var stack = new StackPanel
            {
                Margin = new Thickness(8)
            };

            saveDirectory.Click += saveDirectory_Click;

            var radioGrid = new Grid { Margin = new Thickness(8) };
            radioGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            radioGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            radioGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            radioGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            radioGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // TODO: Create a space between the elements

            for (int i = 0; i < saveTypes.Length; i++){
                Grid.SetRow(saveTypes[i], 0);
                Grid.SetColumn(saveTypes[i], i);
                radioGrid.Children.Add(saveTypes[i]);
            }

            saveDataTypeBox.Content = radioGrid;

            recordEnable.Click += recordEnable_Click;

            stack.Children.Add(new TextBlock
            {
                Text = "Save Directory",
                Margin = new Thickness(0, 0, 0, 4)
            });
            stack.Children.Add(saveDirectoryPath);
            stack.Children.Add(saveDirectory);
            stack.Children.Add(saveDataTypeBox);
            stack.Children.Add(singleBinaryToggle);
            stack.Children.Add(recordEnable);

            group.Content = stack;
            return group;
        }

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
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            UpdateUiOnConnectionStatus();
        }

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
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            UpdateUiOnConnectionStatus();
        }

        private void ConnectWithConfigSelection()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Please select the configuration file of the device to connect to...",
                Filter = "XML configuration files (*.xml)|*.xml|All files (*.*)|*.*",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                Connect(openFileDialog.FileName);
            }
        }

        private void Disconnect()
        {
            if (!imagerShow.IsConnected) return;

            if (imagerShow.IsRecording) imagerShow.StopRecording();

            imagerShow.Disconnect();
            UpdateUiOnConnectionStatus();
        }

        private void UpdateUI()
        {
            if (!imagerShow.IsConnected || imagerShow.IsConnectionLost)
            {
                Disconnect();
                return;
            }

            sbOperationMode.Text = imagerShow.OperationModeString;
            sbFlag.Text = imagerShow.GetFlagState();
            sbFPS.Text = imagerShow.GetFPS().ToString("N1", CultureInfo.CurrentCulture) + " Hz";

            Bitmap? image = imagerShow.GetImage();
            if (image == null) return;

            if (imagerShow.CalculateMinMaxTemperatureRegions())
            {
                DrawMeasurement(
                    image,
                    (imagerShow.MaxRegion.x1 + imagerShow.MaxRegion.x2) / 2,
                    (imagerShow.MaxRegion.y1 + imagerShow.MaxRegion.y2) / 2,
                    imagerShow.MaxRegion.temperature,
                    System.Drawing.Color.Red,
                    System.Drawing.Color.White);

                minTemp.Text = imagerShow.MinRegion.temperature.ToString("N2", CultureInfo.CurrentCulture);
                maxTemp.Text = imagerShow.MaxRegion.temperature.ToString("N2", CultureInfo.CurrentCulture);

                if (autoTempScale.IsChecked == true) SetAutoScalingRange();
            }

            thermalImage.Source = ConvertBitmapToSource(image);
            image.Dispose();
        }

        private void DrawMeasurement(Bitmap bitmap, int x, int y, float value, System.Drawing.Color fgColor, System.Drawing.Color bgColor)
        {
            int markerSize = 20;
            int markerSizeHalf = markerSize / 2;

            using Graphics graphics = Graphics.FromImage(bitmap);
            using GraphicsPath path = new GraphicsPath(FillMode.Winding);
            using System.Drawing.Brush fgBrush = new System.Drawing.SolidBrush(fgColor);
            using System.Drawing.Pen fgPen = new System.Drawing.Pen(fgBrush, 1);
            using System.Drawing.Pen bgPen = new System.Drawing.Pen(bgColor, 3);

            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            graphics.DrawLine(bgPen, x - markerSizeHalf, y, x + markerSizeHalf, y);
            graphics.DrawLine(bgPen, x, y - markerSizeHalf, x, y + markerSizeHalf);

            graphics.DrawLine(fgPen, x - markerSizeHalf, y, x + markerSizeHalf, y);
            graphics.DrawLine(fgPen, x, y - markerSizeHalf, x, y + markerSizeHalf);

            path.AddString(
                string.Format(CultureInfo.CurrentCulture, "{0:N1}", value),
                System.Drawing.SystemFonts.DefaultFont.FontFamily,
                (int)System.Drawing.FontStyle.Regular,
                12,
                new System.Drawing.Point(x + markerSizeHalf / 2, y - markerSizeHalf * 2),
                StringFormat.GenericDefault);

            graphics.DrawPath(bgPen, path);
            graphics.FillPath(fgBrush, path);
        }

        private void UpdateUiOnConnectionStatus()
        {
            bool connected = imagerShow.IsConnected;

            if (connected)
            {
                Title = "Optris Imager - " + imagerShow.GetDeviceType() + " (S/N " + imagerShow.GetSerialNumber().ToString(CultureInfo.CurrentCulture) + ")";
                sbOperationMode.Text = imagerShow.OperationModeString;
                sbFlag.Text = imagerShow.GetFlagState();
                uiUpdateTimer.Start();
                SetAutoScalingRange();
            }
            else
            {
                Title = "Optris Imager";
                sbOperationMode.Text = string.Empty;
                sbFlag.Text = string.Format(CultureInfo.CurrentCulture, "{0, 18}", " ");
                sbFPS.Text = string.Format(CultureInfo.CurrentCulture, "{0, 11}", " ");
                thermalImage.Source = null;
                uiUpdateTimer.Stop();
                SetRecordingUiState(false);
            }

            miDeviceQuickConnect.IsEnabled = !connected;
            miDeviceConnect.IsEnabled = !connected;
            miDeviceDisconnect.IsEnabled = connected;
            miDeviceRefreshFlag.IsEnabled = connected;
            imageConfigurationMenu.IsEnabled = connected;
            saveDirectory.IsEnabled = connected;
            saveDirectoryPath.IsEnabled = connected;
            saveDataTypeBox.IsEnabled = connected;
            singleBinaryToggle.IsEnabled = connected;
            recordEnable.IsEnabled = connected && !string.IsNullOrWhiteSpace(saveDirectoryPath.Text);

            SetOperationModeSelection(imagerShow.ActiveModeIndex);
        }

        private void SetAutoScalingRange()
        {
            if (!imagerShow.IsConnected || autoTempScale.IsChecked != true) return;

            var range = imagerShow.GetTemperatureRange();

            suppressScaleTextEvents = true;
            try
            {
                imageScaleLow.Text = ((int)range.Lower - 50).ToString(CultureInfo.CurrentCulture);
                imageScaleHigh.Text = ((int)range.Upper + 50).ToString(CultureInfo.CurrentCulture);
            }
            finally
            {
                suppressScaleTextEvents = false;
            }
        }

        private void SetOperationModeSelection(int modeIndex)
        {
            opModes[modeIndex].IsChecked = true;
            // for (int i = 0; i < opModes.Length; i++) opModes[i].IsChecked = i == modeIndex;
        }

        private void BuildPaletteMenu()
        {
            colorPaletteMenu.Items.Clear();
            paletteMenuItems.Clear();

            foreach (ColoringPalette palette in System.Enum.GetValues(typeof(ColoringPalette)))
            {
                var item = new MenuItem
                {
                    Header = palette.ToString(),
                    IsCheckable = true,
                    Tag = palette
                };
                item.Click += (object sender, RoutedEventArgs e) =>
                {
                    if (sender is not MenuItem item || item.Tag is not ColoringPalette palette) return;

                    SetSelectedPalette(palette);
                    imagerShow.ChangePalette(palette);
                };
                colorPaletteMenu.Items.Add(item);
                paletteMenuItems[palette] = item;
            }

            SetSelectedPalette(ColoringPalette.Iron);
        }

        private void SetSelectedPalette(ColoringPalette palette)
        {
            foreach (var pair in paletteMenuItems)
            {
                pair.Value.IsChecked = pair.Key == palette;
            }
        }

        private void saveDirectory_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog folderDialog = new OpenFolderDialog();

            if (folderDialog.ShowDialog() == true)
            {
                string path = folderDialog.FolderName;
                if (string.IsNullOrWhiteSpace(path))
                {
                    MessageBox.Show("Please enter a directory path first.", "Save Directory", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                try
                {
                    System.IO.Directory.CreateDirectory(path);
                    saveDirectoryPath.Text = System.IO.Path.GetFullPath(path);
                    recordEnable.IsEnabled = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Invalid Directory", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

        }

        private void recordEnable_Click(object sender, RoutedEventArgs e)
        {
            if (!imagerShow.IsRecording)
            {
                string directory = saveDirectoryPath.Text.Trim();
                if (string.IsNullOrWhiteSpace(directory))
                {
                    MessageBox.Show("Please set a save directory first.", "Recording", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                imagerShow.StartRecording(
                    directory,
                    GetSelectedSaveDataType(),
                    singleBinaryToggle.IsChecked == true);

                recordEnable.Background = WpfBrushes.LimeGreen;
                recordEnable.Content = "Stop Recording";
                SetRecordingUiState(true);
            }
            else
            {
                imagerShow.StopRecording();
                recordEnable.ClearValue(BackgroundProperty);
                recordEnable.Content = "Record";
                SetRecordingUiState(false);
            }
        }

        private void SetRecordingUiState(bool recording)
        {
            saveDirectory.IsEnabled = !recording && imagerShow.IsConnected;
            saveDirectoryPath.IsEnabled = !recording && imagerShow.IsConnected;
            saveDataTypeBox.IsEnabled = !recording && imagerShow.IsConnected;
            singleBinaryToggle.IsEnabled = !recording && imagerShow.IsConnected;
        }

        private SaveDataType GetSelectedSaveDataType()
        {
            for (int i = 0; i < saveTypes.Length; i++){
                if (saveTypes[i].IsChecked == true) return (SaveDataType) i;
            }

            return SaveDataType.Float;
        }

        private void AutoTempScale_CheckedChanged()
        {
            bool autoTempScaleEnabled = autoTempScale.IsChecked == true;
            imageScaleHigh.IsReadOnly = autoTempScaleEnabled;
            imageScaleLow.IsReadOnly = autoTempScaleEnabled;
            imagerShow.SetAutoScaling(autoTempScaleEnabled);

            if (autoTempScaleEnabled) SetAutoScalingRange();
            else ApplyManualScaleRangeFromInputs();
        }

        private FrameworkElement BuildScaleRow(string labelText, out TextBox textBox, double topMargin = 0)
        {
            var row = new DockPanel{ Margin = new Thickness(0, topMargin, 0, 0) };

            textBox = new TextBox
            {
                Width = 72,
                Text = "0",
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Right,
                IsReadOnly = true,
                Margin = new Thickness(0, 0, 6, 0)
            };
            textBox.TextChanged += imageScale_TextChanged;

            var label = new TextBlock
            {
                Text = labelText,
                Width = 48,
                VerticalAlignment = VerticalAlignment.Center
            };

            var unit = new TextBlock
            {
                Text = "°C",
                VerticalAlignment = VerticalAlignment.Center
            };

            DockPanel.SetDock(label, Dock.Left);
            DockPanel.SetDock(unit, Dock.Right);

            row.Children.Add(label);
            row.Children.Add(textBox);
            row.Children.Add(unit);
            return row;
        }

        private void imageScale_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (suppressScaleTextEvents || autoTempScale.IsChecked == true)
            {
                return;
            }

            ApplyManualScaleRangeFromInputs();
        }

        private void ApplyManualScaleRangeFromInputs()
        {
            if (!TryReadScaleValue(imageScaleLow.Text, out float low)) return;

            if (!TryReadScaleValue(imageScaleHigh.Text, out float high)) return;

            if (low > high) (low, high) = (high, low);

            imagerShow.SetScaleRange(low, high);
        }

        private static bool TryReadScaleValue(string? text, out float value)
        {
            return float.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out value);
        }

        private void OperationMode_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton radioButton || radioButton.IsChecked != true) return;

            if (radioButton.Tag is not int modeIndex) return;

            imagerShow.SetOperationMode(modeIndex);
            SetAutoScalingRange();
        }


        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private static BitmapSource ConvertBitmapToSource(Bitmap bitmap)
        {
            IntPtr hBitmap = bitmap.GetHbitmap();
            try
            {
                BitmapSource source = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                source.Freeze();
                return source;
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }
    }
}
