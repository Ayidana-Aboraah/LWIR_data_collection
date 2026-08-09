// Copyright (c) 2008-2025 Optris GmbH & Co. KG

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using LWIR_app.classes;
using LWIR_app.models;
using Optris.OtcSdk;
using WpfBrushes = System.Windows.Media.Brushes;
using LWIR_app.UI;
using LWIR_app.Sensor;

namespace LWIR_app
{
    /// <summary>Main window of the application.</summary>
    public sealed class DisplayForm : Window
    {
        private readonly IRImagerShow imagerShow = new();
        private readonly DispatcherTimer uiUpdateTimer = new();
        private readonly Dictionary<string, MenuItem> paletteMenuItems = new();

        private bool suppressScaleTextEvents;

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

        private CheckBox autoTempScale = new CheckBox
        {
            Content = "Automatic Temperature Scale",
            IsChecked = true,
            Margin = new Thickness(0, 16, 0, 0)
        };

        private TextBox imageScaleLow = null!;
        private TextBox imageScaleHigh = null!;
        private TextBlock minTemp = null!;
        private TextBlock maxTemp = null!;
        

        private RadioButton[] opModes = null!;

        private MenuItem[] DeviceInteractonsOptions = [
            new MenuItem { Header = "Quick Connect" },
            new MenuItem { Header = "Connect With Configuration..."},
            new MenuItem { Header = "Disconnect", IsEnabled = false },
            new MenuItem { Header = "Refresh Flag", IsEnabled = false },
        ];
        private RoutedEventHandler[] DeviceInteractions = new RoutedEventHandler[4];

        private MenuItem imageConfigurationMenu = new MenuItem { Header = "Image Configuration", IsEnabled = false };
        private MenuItem colorPaletteMenu = new MenuItem { Header = "Color Palette" };

        private Display display;
        private RecordingGroup recordingGroup;
        private RecorderBase recorder;

        /// <summary>Constructor.</summary>
        public DisplayForm()
        {
            recorder = new RecorderBase(imagerShow);
            recordingGroup = new RecordingGroup(recorder);
            display = new Display(recorder);
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

            DeviceInteractions = [
                (_,_) => QuickConnect(),
                (_,_) => ConnectWithConfigSelection(),
                (_,_) => Disconnect(),
                (_,_) => imagerShow.RefreshFlag(),
            ];


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

            // TODO: Add Camera B-Side Settings and disable during vieo playback

            root.Children.Add(display.thermalBorder);

            // TODO: Call Sensor.Disoconnect & maybe free memory from thermal recorder
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
            var LoadFrame = new MenuItem { Header = "Load Frame" };
            var LoadFrameSet = new MenuItem { Header = "Load Frame Set" };
            var quitMenu = new MenuItem { Header = "Quit" };
            LoadFrame.Click += (_,_) =>
            {
                OpenFileDialog dialog = new OpenFileDialog();
                if (dialog.ShowDialog() == true)
                {
                    PlaybackTool.LoadFrames(BinaryLoader.LoadFrames(dialog.FileName));
                    PlaybackTool.Active = true;
                    imagerShow.Disconnect();
                    UpdateUiOnConnectionStatus();
                }
            };
            fileMenu.Items.Add(LoadFrame);

            LoadFrameSet.Click += (_,_) =>
            {
                OpenFolderDialog dialog = new OpenFolderDialog();
                if (dialog.ShowDialog() == true)
                {
                    PlaybackTool.LoadFrames(BinaryLoader.LoadFrameSet(dialog.FolderName));
                    PlaybackTool.Active = true;
                    imagerShow.Disconnect();
                    UpdateUiOnConnectionStatus();
                }
            };
            fileMenu.Items.Add(LoadFrameSet);

            quitMenu.Click += (_, _) =>
            {
                Disconnect();
                Application.Current.Shutdown();
            };
            fileMenu.Items.Add(quitMenu);

            var deviceMenu = new MenuItem { Header = "Device" };

            for (int i = 0; i < DeviceInteractonsOptions.Length; i++)
            {
                deviceMenu.Items.Add(DeviceInteractonsOptions[i]);
                DeviceInteractonsOptions[i].Click += DeviceInteractions[i];
            }

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

            // stack.Children.Add(BuildScaleGroup());
            stack.Children.Add(BuildTemperatureGroup());
            stack.Children.Add(display.BuildRoiPreviewGroup());
            stack.Children.Add(recordingGroup);

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

            Grid panel = new Grid { Margin = new Thickness(8) };
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            FrameworkElement[] s = {
                BuildScaleRow("High:", out imageScaleHigh),
                BuildScaleRow("Low:", out imageScaleLow)
            };

            // TODO: Create a space between the elements

            for (int i = 0; i < s.Length; i++)
            {
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


            // autoTempScale.Checked += (_, _) => AutoTempScale_CheckedChanged();
            // autoTempScale.Unchecked += (_, _) => AutoTempScale_CheckedChanged();
            // valueStack.Children.Add(autoTempScale);

            Grid.SetColumn(valueStack, 0);
            layout.Children.Add(valueStack);

            var opGroup = new GroupBox
            {
                Header = "Operation Mode",
                Margin = new Thickness(10, 0, 0, 0),
                Padding = new Thickness(6)
            };

            var opStack = new StackPanel { Orientation = Orientation.Vertical };
            opModes = [
                BuildOperationModeRadio(" -20°C–100°C", 0),
                BuildOperationModeRadio("0°C–250°C", 1),
                BuildOperationModeRadio("250°C–900°C", 2)
            ];

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

        // TODO: Finish this ting
        private GroupBox BuildCameraSettingsGroup()
        {
            GroupBox group = new GroupBox { Header = "Playback" };
            StackPanel stack = new StackPanel { Margin = new Thickness(8) };

            Slider playback_speed = new Slider { };
            Slider playback = new Slider { };

            playback_speed.ValueChanged += (_, _) => { };

            playback.ValueChanged += (_, _) => { };

            return group;
        }
        private void Connect(string filename)
        {
            if (imagerShow.IsConnected) return;
            

            try
            {
                imagerShow.Connect(filename);
                PlaybackTool.Active = false;
            }
            catch (SDKException ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            UpdateUiOnConnectionStatus();
        }

        private void QuickConnect()
        {
            if (imagerShow.IsConnected) return;

            try
            {
                imagerShow.QuickConnect();
                PlaybackTool.Active = false;
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
            if ((!imagerShow.IsConnected) && PlaybackTool.Active == false)
            {
                Disconnect();
                return;
            }

            sbOperationMode.Text = imagerShow.OperationModeString;
            sbFlag.Text = imagerShow.GetFlagState();
            sbFPS.Text = imagerShow.GetFPS().ToString("N1", CultureInfo.CurrentCulture) + " Hz";


        if (imagerShow.CalculateMinMaxTemperatureRegions())
        {
            float min = imagerShow.MinRegion.temperature;
            float max = imagerShow.MaxRegion.temperature;
            minTemp.Text = min.ToString("N2", CultureInfo.CurrentCulture);
            maxTemp.Text = max.ToString("N2", CultureInfo.CurrentCulture);

            if (max - min < 1) min -= 1;
            imagerShow.SetScaleRange(min, max);

        }
        display.UpdateUI();
        }


        private void UpdateUiOnConnectionStatus()
        {
            bool connected = imagerShow.IsConnected;

            if (connected || PlaybackTool.Active)
            {
                Title = "Optris Imager - " + imagerShow.GetDeviceType() + " (S/N " + imagerShow.GetSerialNumber().ToString(CultureInfo.CurrentCulture) + ")";
                sbOperationMode.Text = imagerShow.OperationModeString;
                sbFlag.Text = imagerShow.GetFlagState();
                uiUpdateTimer.Start();
            }
            else
            {
                Title = "Optris Imager";
                sbOperationMode.Text = string.Empty;
                sbFlag.Text = string.Format(CultureInfo.CurrentCulture, "{0, 18}", " ");
                sbFPS.Text = string.Format(CultureInfo.CurrentCulture, "{0, 11}", " ");
                display.InActive();
                uiUpdateTimer.Stop();
                recordingGroup.Update(false, false);
            }

            int optionsLength = DeviceInteractonsOptions.Length;
            for (int i = 0; i < optionsLength; i++) DeviceInteractonsOptions[i].IsEnabled = (i < optionsLength/2) ? !connected : connected;

            imageConfigurationMenu.IsEnabled = connected;
            recordingGroup.Update(false, true);

            SetOperationModeSelection(imagerShow.ActiveModeIndex);
        }

        private void SetAutoScalingRange()
        {
            if (!imagerShow.IsConnected || autoTempScale.IsChecked != true) return;

            var range = imagerShow.GetTemperatureRange();

            suppressScaleTextEvents = true;
            try
            {
                // imageScaleLow.Text = ((int)range.Lower - 50).ToString(CultureInfo.CurrentCulture);
                // imageScaleHigh.Text = ((int)range.Upper + 50).ToString(CultureInfo.CurrentCulture);
            }
            finally
            {
                suppressScaleTextEvents = false;
            }
        }

        private void SetOperationModeSelection(int modeIndex)
        {
            // opModes[modeIndex].IsChecked = true;
            for (int i = 0; i < opModes.Length; i++) opModes[i].IsChecked = i == modeIndex;
        }

        private void BuildPaletteMenu()
        {
            colorPaletteMenu.Items.Clear();
            paletteMenuItems.Clear();

            // TODO: have the palette options be generated by reading all palette file names from the default directory

            foreach (string palette in PaletteTool.LoadDefaultPaletteNames())
            {
                var item = new MenuItem
                {
                    Header = palette.ToString(),
                    IsCheckable = true,
                    Tag = palette
                };
                item.Click += (object sender, RoutedEventArgs e) =>
                {
                    if (sender is not MenuItem item || item.Tag is not string) return;

                    SetSelectedPalette(palette);
                    imagerShow.ChangePalette(palette);
                };
                colorPaletteMenu.Items.Add(item);
                paletteMenuItems[palette] = item;
            }

            SetSelectedPalette("Iron");
        }

        private void SetSelectedPalette(string palette)
        {
            foreach (var pair in paletteMenuItems) pair.Value.IsChecked = pair.Key == palette;
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
            var row = new DockPanel { Margin = new Thickness(0, topMargin, 0, 0) };

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
            if (suppressScaleTextEvents || autoTempScale.IsChecked == true) return;

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
    }
}
