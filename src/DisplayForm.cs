// Copyright (c) 2008-2025 Optris GmbH & Co. KG

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using SensorInterface.classes;
using SensorInterface.models;
using Optris.OtcSdk;
using WpfBrushes = System.Windows.Media.Brushes;
using SensorInterface.UI;
using SensorInterface.Sensor;

namespace SensorInterface
{
    public sealed class DisplayForm : Window
    {
        private readonly DispatcherTimer uiUpdateTimer = new();
        private readonly Dictionary<string, MenuItem> paletteMenuItems = new();

        private UniversalRecorder recorder;
        private Display display;
        private RecordingGroup recordingGroup;
        private PlaybackGroup playback;

        private MenuItem imageConfigurationMenu = new MenuItem { Header = "Image Configuration", IsEnabled = false };
        private MenuItem colorPaletteMenu = new MenuItem { Header = "Color Palette" };

        public DisplayForm()
        {
            // DEBUG: TODO: remove after debug setup
            recorder = new UniversalRecorder(SensorManager.current_sensor);
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
            Title = "CCAM Sensor Interface";
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

            var controlPanelBorder = BuildControlPanel();
            DockPanel.SetDock(controlPanelBorder, Dock.Right);
            root.Children.Add(controlPanelBorder);
            root.Children.Add(display.baseDisplay);
            // Closing += (_, _) => Disconnect();
        }

        private Menu BuildMenu()
        {
            var menuStrip = new Menu();

            var fileMenu = new MenuItem { Header = "File" };
            var LoadFrame = new MenuItem { Header = "Load Frame" };
            var LoadFrameSet = new MenuItem { Header = "Load Frame Set" };
            var ExportVideo = new MenuItem { Header = "Export Video" };
            var quitMenu = new MenuItem { Header = "Quit" };
            LoadFrame.Click += (_, _) =>
            {
                OpenFileDialog dialog = new OpenFileDialog();
                if (dialog.ShowDialog() == true)
                {
                    // PlaybackTool.LoadFrames(BinaryLoader.LoadFrames(dialog.FileName));
                    PlaybackTool.LoadFrames(BinaryLoader.LoadFramesAndMarkers(dialog.FileName));
                    PlaybackTool.Active = true;
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
                    UpdateUiOnConnectionStatus();
                }
            };
            fileMenu.Items.Add(LoadFrameSet);

            ExportVideo.Click += (_, _) =>
            {
                OpenFolderDialog dialog = new OpenFolderDialog();
                if (dialog.ShowDialog() == true) VideoHelper.LoadFramesAsVideo(640, 480, dialog.FolderName);
            };
            fileMenu.Items.Add(ExportVideo);

            quitMenu.Click += (_, _) => Application.Current.Shutdown();
            fileMenu.Items.Add(quitMenu);

            var deviceMenu = new MenuItem { Header = "Device" };

            // for (int i = 0; i < DeviceInteractonsOptions.Length; i++)
            // {
            //     deviceMenu.Items.Add(DeviceInteractonsOptions[i]);
            //     DeviceInteractonsOptions[i].Click += DeviceInteractions[i];
            // }

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

            var stack = new StackPanel { Orientation = Orientation.Vertical };

            stack.Children.Add(display.BuildRoiPreviewGroup());
            stack.Children.Add(recordingGroup);
            stack.Children.Add(playback);
            foreach (SensorBase sensor in SensorManager.sensors) stack.Children.Add(sensor.UI());

            scrollViewer.Content = stack;
            panelBorder.Child = scrollViewer;
            return panelBorder;
        }

        private void UpdateUI()
        {
            foreach (SensorBase sensor in SensorManager.sensors)
            {
                sensor.UI().Visibility =
                    PlaybackTool.Active || SensorManager.OperatorMode
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                recordingGroup.Update(sensor.Connected());
                sensor.UpdateUI();
            }

            playback.UpdateUI();
            display.UpdateUI();
        }

        private void UpdateUiOnConnectionStatus()
        {
            foreach (SensorBase sensor in SensorManager.sensors) sensor.UpdateUI();
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
                    PaletteTool.SetPalette(palette);
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
