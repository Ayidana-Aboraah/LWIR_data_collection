// Copyright (c) 2008-2025 Optris GmbH & Co. KG

using LWIR_app.classes;
using LWIR_app.Sensor.LWIR.UI;
using Optris.OtcSdk;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace LWIR_app.Sensor.LWIR
{
    /// <summary>
    /// A more feature rich implementation of an IRImagerClient that converts thermal frames to false color images and
    /// displays them.
    /// </summary>
    /// 
    /// An IRImager acts as an observer to an IRImager implementation that retrieves and processes thermal data from
    /// Optris thermal cameras.
    public class IRImagerShow : IRImagerClient, SensorBase
    {
        public IRImager Imager { get; private set; }

        public TemperatureRegion MinRegion { get; private set; }
        public TemperatureRegion MaxRegion { get; private set; }
        public TemperatureRegion MeanRegion { get; private set; }

        public ImageBuilder imageBuilder;
        private FrameEvent frameEvent = new();
        private FramerateCounter counter = new();
        private string flagState = "";
        private int regionRadius = 3;
        public string OperationModeString { get; private set; } = string.Empty;

        private OperationModeVector operationModes = new();
        private int activeModeIndex = 0;
        public bool useAutoScaling;

        public bool IsConnected { get; private set; }
        public bool Connected() => IsConnected;

        public int ActiveModeIndex { get { return activeModeIndex; } }
        public bool recording = false;


        Channel<FrameRecord> recorderChannel = Channel.CreateBounded<FrameRecord>(new BoundedChannelOptions(60 * 60 * 15)
        {
            SingleReader = false,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        StackPanel UI_Panel = new StackPanel { };
        private TemperatureScalingGroup temperatureScaling;
        private LWIR_Footer footer;
        RegionOfInterest roi;
        bool hasROI;
        private readonly DispatcherTimer flagRefreshTimer = new();


        /// <summary>Constructor</summary>
        public IRImagerShow()
        {
            Imager = IRImagerFactory.getInstance().create("native");

            Imager.addClient(this);

            imageBuilder = new ImageBuilder(ColorFormat.BGR, WidthAlignment.FourBytes);
            imageBuilder.setTemperatureScalingMode(TemperatureScalingMode.Manual);

            IsConnected = false;

            MinRegion = new TemperatureRegion();
            MaxRegion = new TemperatureRegion();
            MeanRegion = new TemperatureRegion();

            temperatureScaling = new TemperatureScalingGroup(this);
            footer = new LWIR_Footer(this);

            UI_Panel.Children.Add(temperatureScaling);

            flagRefreshTimer.Interval = TimeSpan.FromMinutes(5);
            flagRefreshTimer.Tick += (_, _) => RefreshFlag();
        }

        public StackPanel UI() => UI_Panel;

        public FrameworkElement Footer() => footer;

        public void DisableUI()
        {
            temperatureScaling.Disable();
            footer.Disable();
        }

        // TODO: Implement
        public void UpdateUI()
        {
            UI_Panel.Visibility = IsConnected ? Visibility.Visible : Visibility.Collapsed;
            temperatureScaling.UpdateUI();
            footer.UpdateUI();
        }

        public (int, int) Dimensions() => (Imager.getWidth(), Imager.getHeight());

        public void IsRecording(bool recording) => this.recording = recording;

        public RegionOfInterest ROI() => roi;

        public bool HasROI() => hasROI;

        public void UpdateROI(System.Windows.Point start, System.Windows.Point end)
        {
            lock (frameEvent.thermalFrame)
            {
                if (frameEvent.thermalFrame.isEmpty()) return;
            }
            roi = new RegionOfInterest(start, end, frameEvent.thermalFrame.getWidth());
            hasROI = true;
        }

        public void ClearROI() => hasROI = false;


        public float findValue(int x, int y)
        {
            if (frameEvent.thermalFrame.isEmpty()) return float.NaN;
            return frameEvent.thermalFrame.getTemperature((y * frameEvent.thermalFrame.getWidth()) + x);
        }

        public void Connect(string configFile)
        {
            if (IsConnected) return;

            Imager.connect(IRImagerConfigReader.read(configFile)); // Read the configuration file and initialize the imager with it

            StartProcessing();

            IsConnected = true;
        }

        /// Quickly connects to the first detected device on the USB port
        public void QuickConnect()
        {
            if (IsConnected) return;

            Imager.connect(0); // The serial number 0 servers as a wild card to connect to the first available device.

            StartProcessing();

            SetOperationMode(operationModes.Last().getIndex());

            IsConnected = true;
        }

        /// Disconnects from the currently connected device
        public void Disconnect()
        {
            if (!IsConnected) return;

            Imager.disconnect();

            IsConnected = false;
        }

        /// Refreshes the flag by triggering a flag event that cause the shutter flag to close for a short time
        public void RefreshFlag()
        {
            if (!IsConnected) return;
            
            try
            {
                Imager.forceFlagEvent();
            }
            catch (SDKException ex)
            {
                ShowMessageBox(ex.Message, "Failed to refresh flag", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void SetOperationMode(int modeIndex)
        {
            if (!IsConnected) return;

            try
            {
                // TODO: CHECK, did this because we found operations to have a duplicates which offset it
                Imager.setActiveOperationMode(operationModes[modeIndex * 2]);
                activeModeIndex = Imager.getActiveOperationMode().getIndex();
            }
            catch (SDKException ex)
            {
                ShowMessageBox(
                    ex.Message,
                    "Failed to change operation mode",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            UpdateOperationModeString();
        }

        public string GetDeviceType() => Imager.getDeviceType().ToString();

        public uint GetSerialNumber() => Imager.getSerialNumber();

        public string GetFlagState() => flagState;

        public double GetFPS() => Math.Round(counter.getFps(), 1); /// Returns the current frame rate in Hz

        public Bitmap? Render()
        {
            lock (frameEvent.thermalFrame)
            {
                if (frameEvent.thermalFrame.isEmpty()) return null;

                imageBuilder.setThermalFrame(frameEvent.thermalFrame);
            }

            imageBuilder.convertTemperatureToPaletteImage();

            (int width, int height) = (imageBuilder.getWidth(), imageBuilder.getHeight());

            byte[] image = new byte[imageBuilder.getImageSizeInBytes()];
            imageBuilder.copyImageDataTo(image, image.Length);


            Rectangle rectangle = new Rectangle(0, 0, width, height);
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);

            BitmapData bitmapData = bitmap.LockBits(rectangle, ImageLockMode.ReadWrite, bitmap.PixelFormat);
            System.Runtime.InteropServices.Marshal.Copy(image, 0, bitmapData.Scan0, image.Length);
            bitmap.UnlockBits(bitmapData);

            return bitmap;
        }

        // Calculates the position and temperature of the hottest and coldest region with the given radius.
        public bool CalculateMinMaxTemperatureRegions() => imageBuilder.getMinMaxRegions(regionRadius, MinRegion, MaxRegion);

        // Callback method triggered by imager when a new thermal frame is available
        public override void onFrame(FrameEvent evt)
        {
            lock (frameEvent)
            {
                frameEvent = evt.clone();
                counter.trigger();
            }

            if (!recording || frameEvent.thermalFrame.getSize() != Imager.getWidth() * Imager.getHeight()) return;

            float[] temperatures = new float[frameEvent.thermalFrame.getSize()];

            frameEvent.thermalFrame.copyTemperaturesTo(
                temperatures,
                temperatures.Length);

            recorderChannel.Writer.WriteAsync(new FrameRecord(
                        frameEvent.thermalFrame.getWidth(),
                        frameEvent.thermalFrame.getHeight(),
                        temperatures,
                        frameEvent.meta));
        }

        public ChannelReader<FrameRecord> Reader() => recorderChannel.Reader;

        private void StartProcessing()
        {
            operationModes = Imager.getOperationModes();
            activeModeIndex = Imager.getActiveOperationMode().getIndex();

            UpdateOperationModeString();

            Imager.runAsync();
        }

        private void UpdateOperationModeString()
        {
            OperationMode mode = Imager.getActiveOperationMode();

            OperationModeString = string.Format("{0}°, {1}x{2} @ {3} Hz",
                                                 mode.getFieldOfView(),
                                                 mode.getFrameWidth(),
                                                 mode.getFrameHeight(),
                                                 mode.getFramerate());
        }

        public (float Lower, float Upper) GetTemperatureRange()
        {
            OperationMode mode = Imager.getActiveOperationMode();
            return (mode.getTemperatureLowerLimit(), mode.getTemperatureUpperLimit());
        }

        public void ChangePalette(string paletteName)
        {
            if (!IsConnected) return;

            try
            {
                imageBuilder.setPalette(paletteName);
            }
            catch (SDKException ex)
            {
                ShowMessageBox(ex.Message, "Failed to change palette", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void SetScaleRange(float low, float high)
        {
            if (IsConnected) imageBuilder.setTemperatureScaling(low, high);
        }
        public void SetAutoScaling(bool enabled) => useAutoScaling = enabled;

        private static MessageBoxResult ShowMessageBox(string message, string title, MessageBoxButton button, MessageBoxImage icon)
        {
            Dispatcher? dispatcher = Application.Current?.Dispatcher;

            if (dispatcher != null && !dispatcher.CheckAccess()) return dispatcher.Invoke(() => MessageBox.Show(message, title, button, icon));

            return MessageBox.Show(message, title, button, icon);
        }
    }
}
