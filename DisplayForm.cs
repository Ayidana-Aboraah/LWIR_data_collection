// Copyright (c) 2008-2025 Optris GmbH & Co. KG

using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Microsoft.Win32;
using LWIR_app.classes;
using LWIR_app.models;
using Optris.OtcSdk;
using WpfBrushes = System.Windows.Media.Brushes;

namespace LWIR_app
{
    /// <summary>Main window of the application.</summary>
    public sealed class DisplayForm : Window
    {
        private readonly IRImagerShow imagerShow = new();
        private PlaybackTool playback;
        private readonly DispatcherTimer uiUpdateTimer = new();
        private readonly Dictionary<string, MenuItem> paletteMenuItems = new();

        private bool suppressScaleTextEvents;
        private bool isDraggingRoi;
        private bool hasRoi;
        private System.Windows.Point mouse_position;
        private System.Windows.Point roiDragStart, roiDragCurrent;
        private Rectangle selectedRoi;
        private int currentImageWidth, currentImageHeight;

        public bool replay = false;

        private System.Windows.Controls.Image thermalImage = new System.Windows.Controls.Image
            {
                Stretch = Stretch.Uniform,
                SnapsToDevicePixels = true,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
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

        private string savePath = @"D:\works\data_in\";
        private CheckBox singleBinaryToggle = new CheckBox
        {
            Content = "Single Binary File",
            IsChecked = true,
            Margin = new Thickness(0, 0, 0, 10)
        };

        private CheckBox recordROIOnlyToggle = new CheckBox
        {
            Content = "Record Only ROI",
            IsChecked = false,
            Margin = new Thickness(0, 0, 0, 10)
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
        private System.Windows.Controls.Image roiPreviewImage = new System.Windows.Controls.Image
            {
                Height = 170,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                SnapsToDevicePixels = true
            };

        private TextBlock roiPreviewInfo = null!;

        private RadioButton[] opModes = null!;

        private RadioButton[] saveTypes = new RadioButton[3]{
            new RadioButton { Content = "BaseData", IsChecked = true, Margin = new Thickness(0, 0, 20, 6) },
            new RadioButton { Content = "IntData", Margin = new Thickness(0, 0, 0, 6) },
            new RadioButton { Content = "RLE Data", Margin = new Thickness(0, 0, 20, 0) },
        };

        private MenuItem[] DeviceInteractonsOptions = [
            new MenuItem { Header = "Quick Connect" },
            new MenuItem { Header = "Connect With Configuration..."},
            new MenuItem { Header = "Disconnect", IsEnabled = false },
            new MenuItem { Header = "Refresh Flag", IsEnabled = false },
        ];
        private RoutedEventHandler[] DeviceInteractions = new RoutedEventHandler[4];

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
            playback = new PlaybackTool(imagerShow.imageBuilder);
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

            saveDirectory.Content = savePath;

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

            RenderOptions.SetBitmapScalingMode(thermalImage, BitmapScalingMode.HighQuality);
            thermalImage.MouseLeftButtonDown += ThermalImage_MouseLeftButtonDown;
            thermalImage.MouseMove += ThermalImage_MouseMove;
            thermalImage.MouseLeftButtonUp += ThermalImage_MouseLeftButtonUp;
            thermalImage.MouseRightButtonDown += ThermalImage_MouseRightButtonDown;

            // TODO: Add Camera B-Side Settings and disable during vieo playback

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
            var LoadFrame = new MenuItem { Header = "Load Frame" };
            var LoadBin = new MenuItem { Header = "Load Bin" };
            var quitMenu = new MenuItem { Header = "Quit" };
            LoadFrame.Click += (_,_) =>
            {
                OpenFileDialog dialog = new OpenFileDialog();
                if (dialog.ShowDialog() == true)
                {
                    var frame = BinaryLoader.LoadRecordedFrameBinary(dialog.FileName);
                    playback.LoadFrames(frame);
                    replay = true;
                    UpdateUiOnConnectionStatus();
                }
            };
            fileMenu.Items.Add(LoadFrame);

            LoadBin.Click += (_,_) =>
            {
                OpenFileDialog dialog = new OpenFileDialog();
                if (dialog.ShowDialog() == true)
                {
                    var frame = BinaryLoader.LoadFromBinary(dialog.FileName);
                    playback.LoadFrames(frame);
                    replay = true;
                    UpdateUiOnConnectionStatus();
                }
            };
            fileMenu.Items.Add(LoadBin);

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
            stack.Children.Add(BuildRoiPreviewGroup());
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

        private GroupBox BuildRecordingGroup()
        {
            var group = new GroupBox { Header = "Recording" };

            var stack = new StackPanel { Margin = new Thickness(8) };

            saveDirectory.Click += (_, _) => saveDirectory_Click();

            Grid radioGrid = new Grid { Margin = new Thickness(8) };
            radioGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            radioGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            radioGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            radioGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // TODO: Create a space between the elements

            for (int i = 0; i < saveTypes.Length; i++)
            {
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
            // stack.Children.Add(saveDirectoryPath);
            stack.Children.Add(saveDirectory);
            stack.Children.Add(saveDataTypeBox);
            stack.Children.Add(singleBinaryToggle);
            stack.Children.Add(recordROIOnlyToggle);
            stack.Children.Add(recordEnable);
            
            float focus = imagerShow.Imager.getFocusMotorPosition();

            GroupBox fs = new GroupBox{ Header = "Focus Settings"};
            StackPanel st = new StackPanel {};
            Slider FocusSlider = new Slider
            {
                Maximum = 100,
                TickFrequency = 1,
                Width = 200,
                IsSnapToTickEnabled = true,
                Value = focus
            };

            TextBlock FocusText = new TextBlock
            {
                Text = focus.ToString(),
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(5, 0, 5, 0)
            };

            st.Children.Add(FocusText);
            st.Children.Add(FocusSlider);

            FocusSlider.ValueChanged += (_, _) =>
            {
                focus = (float) FocusSlider.Value;
                FocusText.Text = focus.ToString();
                imagerShow.Imager.setFocusMotorPosition(focus);
            };

            fs.Content = st;
            stack.Children.Add(fs);

            group.Content = stack;
            return group;
        }

        private GroupBox BuildRoiPreviewGroup()
        {
            var group = new GroupBox
            {
                Header = "Region Of Interest",
                Margin = new Thickness(0, 0, 0, 12)
            };

            var stack = new StackPanel { Margin = new Thickness(8) };

            RenderOptions.SetBitmapScalingMode(roiPreviewImage, BitmapScalingMode.HighQuality);

            var imageHost = new Border
            {
                Background = WpfBrushes.Black,
                BorderBrush = WpfBrushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4),
                Child = roiPreviewImage
            };

            roiPreviewInfo = new TextBlock
            {
                Text = "No ROI selected",
                Margin = new Thickness(0, 8, 0, 0),
                Foreground = WpfBrushes.DimGray
            };

            stack.Children.Add(imageHost);
            stack.Children.Add(roiPreviewInfo);

            group.Content = stack;
            return group;
        }

        private void Connect(string filename)
        {
            if (imagerShow.IsConnected) return;

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
            if (imagerShow.IsConnected) return;

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
            if ((!imagerShow.IsConnected) && replay == false)
            {
                Disconnect();
                return;
            }

            sbOperationMode.Text = imagerShow.OperationModeString;
            sbFlag.Text = imagerShow.GetFlagState();
            sbFPS.Text = imagerShow.GetFPS().ToString("N1", CultureInfo.CurrentCulture) + " Hz";

            Bitmap? image = replay ? playback.RenderFrame(playback.GetCurrentFrame()).Bitmap : imagerShow.GetImage();
            if (image == null) return;

            currentImageWidth = image.Width;
            currentImageHeight = image.Height;

            if (TryGetMouseImagePixel(out System.Drawing.Point cursorPixel))
                DrawMeasurement(
                    image,
                    cursorPixel.X,
                    cursorPixel.Y,
                    replay ? playback.findTemp(cursorPixel.X, cursorPixel.Y): imagerShow.findTemp(cursorPixel.X, cursorPixel.Y),
                    System.Drawing.Color.Red,
                    System.Drawing.Color.White);

            if (imagerShow.CalculateMinMaxTemperatureRegions())
            {
                float min = imagerShow.MinRegion.temperature;
                float max = imagerShow.MaxRegion.temperature;
                minTemp.Text = min.ToString("N2", CultureInfo.CurrentCulture);
                maxTemp.Text = max.ToString("N2", CultureInfo.CurrentCulture);

                if (max - min < 1) min -= 1;
                imagerShow.SetScaleRange(min, max);
                
            }

            UpdateRoiPreview(image);

            DrawRoiOverlay(image);

            thermalImage.Source = ConvertBitmapToSource(image);
            image.Dispose();
        }

        private void ThermalImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if ((!imagerShow.IsConnected && !replay)|| currentImageWidth <= 0 || currentImageHeight <= 0 || imagerShow.IsRecording) return;

            System.Windows.Point cursor = e.GetPosition(thermalImage);
            if (!IsPointInsideImageViewport(cursor)) return;

            isDraggingRoi = true;
            roiDragStart = cursor;
            roiDragCurrent = cursor;
            thermalImage.CaptureMouse();
            e.Handled = true;
        }

        private void ThermalImage_MouseMove(object sender, MouseEventArgs e)
        {
            mouse_position = e.GetPosition(thermalImage);
            if (!isDraggingRoi) return;
            roiDragCurrent = e.GetPosition(thermalImage);
        }

        private void ThermalImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isDraggingRoi) return;

            roiDragCurrent = e.GetPosition(thermalImage);
            isDraggingRoi = false;
            thermalImage.ReleaseMouseCapture();

            if (TryBuildImageRectangle(roiDragStart, roiDragCurrent, out System.Drawing.Rectangle rectangle))
            {
                selectedRoi = rectangle;
                System.Windows.Point start = new System.Windows.Point(rectangle.Left, rectangle.Top);
                System.Windows.Point end =  new System.Windows.Point(rectangle.Right - 1, rectangle.Bottom - 1);
                hasRoi = true;
                if (replay) playback.UpdateROI(start, end);
                else imagerShow.UpdateROI(start, end);
            }
            else
            {
                hasRoi = false;
                imagerShow.ClearROI();
            }

            e.Handled = true;
        }

        private void ThermalImage_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {

            hasRoi = false;
            isDraggingRoi = false;
            imagerShow.ClearROI();
            thermalImage.ReleaseMouseCapture();
            e.Handled = true;
        }

        private bool IsPointInsideImageViewport(System.Windows.Point point)
        {
            Rect viewport = GetImageViewport(currentImageWidth, currentImageHeight);
            return viewport.Contains(point);
        }

        private bool TryGetMouseImagePixel(out System.Drawing.Point imagePixel)
        {
            imagePixel = default;

            if (currentImageWidth <= 0 || currentImageHeight <= 0) return false;

            Rect viewport = GetImageViewport(currentImageWidth, currentImageHeight);
            if (viewport.IsEmpty || !viewport.Contains(mouse_position)) return false;

            imagePixel = MapDisplayToImagePixel(mouse_position, viewport);
            return true;
        }

        private Rect GetImageViewport(int imageWidth, int imageHeight)
        {
            (double controlWidth, double controlHeight) = (thermalImage.ActualWidth, thermalImage.ActualHeight);

            if (controlWidth <= 0 || controlHeight <= 0 || imageWidth <= 0 || imageHeight <= 0) return Rect.Empty;

            double imageAspect = (double)imageWidth / imageHeight;
            double controlAspect = controlWidth / controlHeight;

            if (controlAspect > imageAspect)
            {
                double scaledWidth = controlHeight * imageAspect;
                double offsetX = (controlWidth - scaledWidth) / 2.0;
                return new Rect(offsetX, 0, scaledWidth, controlHeight);
            }

            double scaledHeight = controlWidth / imageAspect;
            double offsetY = (controlHeight - scaledHeight) / 2.0;
            return new Rect(0, offsetY, controlWidth, scaledHeight);
        }

        private bool TryBuildImageRectangle(System.Windows.Point start, System.Windows.Point end, out Rectangle rectangle)
        {
            rectangle = Rectangle.Empty;

            Rect viewport = GetImageViewport(currentImageWidth, currentImageHeight);
            if (viewport.IsEmpty) return false;

            System.Windows.Point startClamped = ClampToRect(start, viewport);
            System.Windows.Point endClamped = ClampToRect(end, viewport);

            System.Drawing.Point startPixel = MapDisplayToImagePixel(startClamped, viewport);
            System.Drawing.Point endPixel = MapDisplayToImagePixel(endClamped, viewport);

            int left = Math.Min(startPixel.X, endPixel.X);
            int right = Math.Max(startPixel.X, endPixel.X);
            int top = Math.Min(startPixel.Y, endPixel.Y);
            int bottom = Math.Max(startPixel.Y, endPixel.Y);

            if (right - left < 2 || bottom - top < 2) return false;

            rectangle = new Rectangle(left, top, right - left + 1, bottom - top + 1);
            return true;
        }

        private static System.Windows.Point ClampToRect(System.Windows.Point point, Rect rect)
        {
            if (rect.IsEmpty) return point;

            double clampedX = Math.Max(rect.Left, Math.Min(point.X, rect.Right));
            double clampedY = Math.Max(rect.Top, Math.Min(point.Y, rect.Bottom));

            return new System.Windows.Point(clampedX, clampedY);
        }

        private System.Drawing.Point MapDisplayToImagePixel(System.Windows.Point point, Rect viewport)
        {
            double normalizedX = (point.X - viewport.X) / viewport.Width;
            double normalizedY = (point.Y - viewport.Y) / viewport.Height;

            normalizedX = Math.Max(0.0, Math.Min(1.0, normalizedX));
            normalizedY = Math.Max(0.0, Math.Min(1.0, normalizedY));

            int pixelX = (int)Math.Round(normalizedX * (currentImageWidth - 1));
            int pixelY = (int)Math.Round(normalizedY * (currentImageHeight - 1));

            return new System.Drawing.Point(pixelX, pixelY);
        }

        private void DrawRoiOverlay(Bitmap bitmap)
        {
            if (hasRoi) DrawRectangleOverlay(bitmap, selectedRoi, System.Drawing.Color.Lime, 2f);

            if (isDraggingRoi && TryBuildImageRectangle(roiDragStart, roiDragCurrent, out Rectangle preview))
                DrawRectangleOverlay(bitmap, preview, System.Drawing.Color.Yellow, 1.5f);
        }

        private void UpdateRoiPreview(Bitmap sourceImage)
        {
            if (!hasRoi)
            {
                roiPreviewImage.Source = null;
                roiPreviewInfo.Text = "No ROI selected";
                return;
            }

            Rectangle imageBounds = new Rectangle(0, 0, sourceImage.Width, sourceImage.Height);
            Rectangle roi = Rectangle.Intersect(selectedRoi, imageBounds);

            if (roi.Width < 2 || roi.Height < 2)
            {
                roiPreviewImage.Source = null;
                roiPreviewInfo.Text = "No ROI selected";
                return;
            }

            using Bitmap roiBitmap = sourceImage.Clone(roi, sourceImage.PixelFormat);
            roiPreviewImage.Source = ConvertBitmapToSource(roiBitmap);
            roiPreviewInfo.Text = string.Format(CultureInfo.CurrentCulture, "{0} x {1} px", roi.Width, roi.Height);
        }

        private static void DrawRectangleOverlay(Bitmap bitmap, Rectangle rectangle, System.Drawing.Color color, float thickness)
        {
            if (rectangle.Width < 2 || rectangle.Height < 2) return;

            using Graphics graphics = Graphics.FromImage(bitmap);
            using System.Drawing.Pen borderPen = new System.Drawing.Pen(color, thickness)
            {
                DashStyle = System.Drawing.Drawing2D.DashStyle.Dash
            };
            using System.Drawing.Brush fillBrush = new SolidBrush(System.Drawing.Color.FromArgb(45, color));

            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.FillRectangle(fillBrush, rectangle);
            graphics.DrawRectangle(borderPen, rectangle);
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

            if (connected || replay)
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
                thermalImage.Source = null;
                roiPreviewImage.Source = null;
                roiPreviewInfo.Text = "No ROI selected";
                hasRoi = false;
                isDraggingRoi = false;
                currentImageWidth = 0;
                currentImageHeight = 0;
                uiUpdateTimer.Stop();
                SetRecordingUiState(false);
            }

            int optionsLength = DeviceInteractonsOptions.Length;
            for (int i = 0; i < optionsLength; i++) DeviceInteractonsOptions[i].IsEnabled = (i < optionsLength/2) ? !connected : connected;

            imageConfigurationMenu.IsEnabled = connected;
            saveDirectory.IsEnabled = connected;
            saveDataTypeBox.IsEnabled = connected;
            singleBinaryToggle.IsEnabled = connected;
            recordROIOnlyToggle.IsEnabled = connected;
            recordEnable.IsEnabled = connected && !string.IsNullOrWhiteSpace(savePath);

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

            foreach (string palette in imagerShow.LoadDefaultPaletteNames)
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

        private void saveDirectory_Click()
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
                    savePath = System.IO.Path.GetFullPath(path);
                    saveDirectory.Content = savePath;
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
                string directory = savePath;
                if (string.IsNullOrWhiteSpace(directory))
                {
                    MessageBox.Show("Please set a save directory first.", "Recording", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (recordROIOnlyToggle.IsChecked == true && !hasRoi)
                {
                    MessageBox.Show("Create ROI");
                    return;
                }

                imagerShow.StartRecording(
                    directory,
                    GetSelectedSaveDataType(),
                    singleBinaryToggle.IsChecked == true,
                    recordROIOnlyToggle.IsChecked == true);

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
            saveDataTypeBox.IsEnabled = !recording && imagerShow.IsConnected;
            singleBinaryToggle.IsEnabled = !recording && imagerShow.IsConnected;
            recordROIOnlyToggle.IsEnabled = !recording && imagerShow.IsConnected;
        }

        private SaveDataType GetSelectedSaveDataType()
        {
            for (int i = 0; i < saveTypes.Length; i++)
            {
                if (saveTypes[i].IsChecked == true) return (SaveDataType)i;
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
