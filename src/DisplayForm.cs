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
using LWIR_app.Sensor.LWIR;

namespace LWIR_app
{
    public sealed class DisplayForm : Window
    {
        private readonly IRImagerShow imagerShow = new();
        private readonly DispatcherTimer uiUpdateTimer = new();
        private readonly Dictionary<string, MenuItem> paletteMenuItems = new();

        private SensorBase current_sensor;
        private UniversalRecorder recorder;
        private Display display;
        private RecordingGroup recordingGroup;
        private PlaybackGroup playback;

        private MenuItem[] DeviceInteractonsOptions = [
            new MenuItem { Header = "Quick Connect" },
            new MenuItem { Header = "Connect With Configuration..."},
            new MenuItem { Header = "Disconnect", IsEnabled = false },
        ];
        private RoutedEventHandler[] DeviceInteractions = new RoutedEventHandler[4];
        private MenuItem imageConfigurationMenu = new MenuItem { Header = "Image Configuration", IsEnabled = false };
        private MenuItem colorPaletteMenu = new MenuItem { Header = "Color Palette" };

        public DisplayForm()
        {
            // DEBUG: TODO: remove after debug setup
            current_sensor = imagerShow;
            recorder = new UniversalRecorder(current_sensor);
            recordingGroup = new RecordingGroup(recorder);
            playback = new PlaybackGroup();
            display = new Display(recorder);
            InitializeComponent();

            uiUpdateTimer.Interval = TimeSpan.FromMilliseconds(33);
            uiUpdateTimer.Tick += (_, _) => UpdateUI();

            BuildPaletteMenu();
            UpdateUI();
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
            ];

            var root = new DockPanel();
            Content = root;

            var menuStrip = BuildMenu();
            DockPanel.SetDock(menuStrip, Dock.Top);
            root.Children.Add(menuStrip);

            var controlPanelBorder = BuildControlPanel();
            DockPanel.SetDock(controlPanelBorder, Dock.Right);
            root.Children.Add(controlPanelBorder);
            root.Children.Add(display.baseDisplay);
            Closing += (_, _) => Disconnect();
        }

        private Menu BuildMenu()
        {
            var menuStrip = new Menu();

            var fileMenu = new MenuItem { Header = "File" };
            var LoadFrame = new MenuItem { Header = "Load Frame" };
            var LoadFrameSet = new MenuItem { Header = "Load Frame Set" };
            var quitMenu = new MenuItem { Header = "Quit" };
            LoadFrame.Click += (_, _) =>
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

            LoadFrameSet.Click += (_, _) =>
            {
                OpenFolderDialog dialog = new OpenFolderDialog();
                if (dialog.ShowDialog() == true)
                {
                    PlaybackTool.LoadFrames(BinaryLoader.LoadFrameSet(dialog.FolderName));
                    PlaybackTool.Active = true;
                    Disconnect();
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
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                BorderBrush = WpfBrushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10)
            };

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var stack = new StackPanel{ Orientation = Orientation.Vertical };

            stack.Children.Add(display.BuildRoiPreviewGroup());
            stack.Children.Add(recordingGroup);
            stack.Children.Add(playback);
            stack.Children.Add(current_sensor.UI());

            scrollViewer.Content = stack;
            panelBorder.Child = scrollViewer;
            return panelBorder;
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

            if (openFileDialog.ShowDialog() == true) Connect(openFileDialog.FileName);
        }

        private void Disconnect()
        {
            if (!imagerShow.IsConnected) return;

            recorder.Stop();
            imagerShow.Disconnect();
            UpdateUiOnConnectionStatus();
        }

        private void UpdateUI()
        {
            current_sensor.UI().Visibility = PlaybackTool.Active ? Visibility.Collapsed : Visibility.Visible;
            recordingGroup.Update(current_sensor.Connected());
            current_sensor.UpdateUI();
            playback.UpdateUI();
            display.UpdateUI();
        }

        private void UpdateUiOnConnectionStatus()
        {
            bool connected = imagerShow.IsConnected;

            if (connected || PlaybackTool.Active)
            {
                string status = PlaybackTool.Active ? "Replay": imagerShow.GetDeviceType() + " (S/N " + imagerShow.GetSerialNumber().ToString(CultureInfo.CurrentCulture) + ")";
                Title = "Optris Imager - " + status;
                display.UpdateUI();
                uiUpdateTimer.Start();
            }
            else
            {
                Title = "Optris Imager";
                display.Disable();
                uiUpdateTimer.Stop();
            }

            int optionsLength = DeviceInteractonsOptions.Length;
            for (int i = 0; i < optionsLength; i++) DeviceInteractonsOptions[i].IsEnabled = (i < optionsLength / 2) ? !connected : connected;

            imageConfigurationMenu.IsEnabled = connected;
            recordingGroup.Update(connected);
            current_sensor.UpdateUI();
        }
        private void BuildPaletteMenu()
        {
            colorPaletteMenu.Items.Clear();
            paletteMenuItems.Clear();

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
    }
}
