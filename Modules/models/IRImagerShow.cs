// Copyright (c) 2008-2025 Optris GmbH & Co. KG

using LWIR_app.classes;
using Optris.OtcSdk;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Threading;

namespace LWIR_app.models
{
    /// <summary>
    /// A more feature rich implementation of an IRImagerClient that converts thermal frames to false color images and
    /// displays them.
    /// </summary>
    /// 
    /// An IRImager acts as an observer to an IRImager implementation that retrieves and processes thermal data from
    /// Optris thermal cameras.
    public class IRImagerShow : IRImagerClient
    {
        public IRImager Imager { get; private set; }

        public TemperatureRegion MinRegion { get; private set; }
        public TemperatureRegion MaxRegion { get; private set; }
        public TemperatureRegion MeanRegion { get; private set; }

        private ImageBuilder imageBuilder;
        private FrameEvent frameEvent = new();
        private FramerateCounter counter = new();
        private string flagState = "";
        private int regionRadius = 3;
        public string OperationModeString { get; private set; } = string.Empty;

        private ThermalRecorder recorder = new();

        private OperationModeVector operationModes = new();
        private int activeModeIndex = 0;

        public bool IsConnected { get; private set; }
        public bool IsConnectionLost { get; private set; }

        // private bool useAutoScaling = true;

        // private float scaleLow = 0f;
        // private float scaleHigh = 100f;
        private readonly List<int> roiTemperatureIndices = new();
        private int roiWidth;
        private int roiHeight;
        public int ActiveModeIndex { get { return activeModeIndex; } }
        public IReadOnlyList<int> RoiTemperatureIndices { get { return roiTemperatureIndices; } }
        public bool HasROI { get { return roiTemperatureIndices.Count > 0; } }

        private SaveDataType saveDataType;

        /// <summary>Constructor</summary>
        public IRImagerShow()
        {
            /*
             * The factory is implemented as a Singleton. Therefore, you have to call getInstance()
             * first before you can create an IRImager object.
             *
             * The native implementation allows to access the thermal data of cameras connected via
             * USB or Ethernet.
             */
            Imager = IRImagerFactory.getInstance().create("native");

            // Register this instance as client/observer
            Imager.addClient(this);

            /*
             * Create an image builder object that will convert thermal frame data to false color images
             * 
             * Its color format refers to the sequence of the bytes for the color values in the generated image array.
             * The color format of C# Bitmap class, however, refers to the significance of the color value bytes. Since
             * x64 is little-endian a C# Bitmap color format of RBG equals a BGR ImageBuilder color format.
             * 
             * Images are typically read line by line. To improve the performance of that operation this happens in bigger
             * byte chunks. The C# Bitmap class uses four byte chunks. Thus, the width alignment should be set to four 
             * bytes. This will ensure that each line has a size in bytes that is a multiple of four.
             * 
             * The temperature range decimal indicates the precision of the thermal data. The ImageBuilder requires this
             * information to correctly decode that data.
             */
            imageBuilder = new ImageBuilder(ColorFormat.BGR, WidthAlignment.FourBytes);
            // imageBuilder.setPaletteScalingMethod(PaletteScalingMethod.MinMax);
            // useAutoScaling = true;

            IsConnected = false;
            IsConnectionLost = false;

            // Hottest, coldest and mean temperature regions
            MinRegion = new TemperatureRegion();
            MaxRegion = new TemperatureRegion();
            MeanRegion = new TemperatureRegion();
        }

        public float findTemp(int x, int y)
        {
            if (frameEvent.thermalFrame.isEmpty()) return float.NaN;
            return frameEvent.thermalFrame.getTemperature((y * frameEvent.thermalFrame.getWidth()) + x);
        }


        public void UpdateROI(System.Windows.Point start, System.Windows.Point end)
        {
            lock (frameEvent.thermalFrame)
            {
                if (frameEvent.thermalFrame.isEmpty()) return;
            }

            roiTemperatureIndices.Clear();

            roiWidth = frameEvent.thermalFrame.getWidth();
            roiHeight = frameEvent.thermalFrame.getHeight();

            if (roiWidth <= 0 || roiHeight <= 0) return;

            int left = Math.Clamp((int)Math.Floor(Math.Min(start.X, end.X)), 0, roiWidth - 1);
            int right = Math.Clamp((int)Math.Floor(Math.Max(start.X, end.X)), 0, roiWidth - 1);
            int top = Math.Clamp((int)Math.Floor(Math.Min(start.Y, end.Y)), 0, roiHeight - 1);
            int bottom = Math.Clamp((int)Math.Floor(Math.Max(start.Y, end.Y)), 0, roiHeight - 1);

            if (left > right || top > bottom) return;

            int roiPixelCount = (right - left + 1) * (bottom - top + 1);
            roiTemperatureIndices.Capacity = Math.Max(roiTemperatureIndices.Capacity, roiPixelCount);

            for (int y = top; y <= bottom; y++)
            {
                int rowOffset = y * roiWidth;
                for (int x = left; x <= right; x++)
                {
                    // Flatten 2D image coordinates into a 1D temperature index.
                    int index = rowOffset + x;
                    roiTemperatureIndices.Add(index);
                }
            }

            recorder.roi = roiTemperatureIndices;
        }

        public void ClearROI() => roiTemperatureIndices.Clear();

        /// <summary>Connects to the device specified in the configuration file.</summary>
        /// 
        /// <param name="configFile">path to the configuration files of the device to connect to.</param>
        public void Connect(string configFile)
        {
            if (IsConnected) return;

            // Read the configuration file and initialize the imager with it
            IRImagerConfig config = IRImagerConfigReader.read(configFile);
            Imager.connect(config);

            StartProcessing();

            IsConnected = true;
            IsConnectionLost = false;
        }

        /// <summary>Quickly connects to the first detected device on the USB port.</summary>
        public void QuickConnect()
        {
            if (IsConnected)
            {
                return;
            }

            // The serial number 0 servers as a wild card to connect to the first available device.
            Imager.connect(0);

            StartProcessing();

            IsConnected = true;
            IsConnectionLost = false;
        }

        /// <summary>Disconnects from the currently connected device.</summary>
        public void Disconnect()
        {
            if (IsConnected)
            {
                Imager.disconnect();

                IsConnected = false;
                IsConnectionLost = false;
            }
        }

        /// <summary>Refreshes the flag by triggering a flag event that cause the shutter flag to close for a short time.</summary>
        public void RefreshFlag()
        {
            if (IsConnected)
            {
                try
                {
                    Imager.forceFlagEvent();
                }
                catch (SDKException ex)
                {
                    ShowMessageBox(ex.Message, "Failed to refresh flag", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void SetOperationMode(int modeIndex)
        {
            if (!IsConnected) return;

            try
            {
                Imager.setActiveOperationMode(
                    operationModes[modeIndex]);
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


        /// <summary>Returns the type of the device.</summary>
        /// 
        /// <return>type of the device.</summary>
        public string GetDeviceType()
        {
            return Imager.getDeviceType().ToString();
        }

        /// <summary>Returns the serial number of the device.</summary>
        /// 
        /// <return>serial number of the device.</return>
        public uint GetSerialNumber()
        {
            return Imager.getSerialNumber();
        }

        /// <summary>Returns the current flag state of the device.</summary>
        /// 
        /// <return>current flag state of the device.</return>
        public string GetFlagState()
        {
            lock (flagState)
            {
                return flagState;
            }
        }

        /// <summary>Returns the current frame rate in Hz.</summary>
        /// 
        /// <return> current frame rate in Hz.</return>
        public double GetFPS()
        {
            lock (frameEvent.thermalFrame)
            {
                return Math.Round(counter.getFps(), 1);
            }
        }

        /// <summary>Converts the latest thermal frame into a false color image and returns it.</summary>
        /// 
        /// <return>converted false color image.</return>
        public Bitmap? GetImage()
        {
            lock (frameEvent.thermalFrame)
            {
                if (frameEvent.thermalFrame.isEmpty()) return null;

                imageBuilder.setThermalFrame(frameEvent.thermalFrame);
            }

            // Convert the thermal frame to a false color image
            imageBuilder.convertTemperatureToPaletteImage();

            // Extract the image data...
            int width = imageBuilder.getWidth();
            int height = imageBuilder.getHeight();

            // The image size in bytes may not equal width * height due to width padding
            byte[] image = new byte[imageBuilder.getImageSizeInBytes()];
            imageBuilder.copyImageDataTo(image, image.Length);


            // .. and create a bitmap
            System.Drawing.Rectangle rectangle = new System.Drawing.Rectangle(0, 0, width, height);
            Bitmap bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            BitmapData bitmapData = bitmap.LockBits(rectangle, ImageLockMode.ReadWrite, bitmap.PixelFormat);
            System.Runtime.InteropServices.Marshal.Copy(image, 0, bitmapData.Scan0, image.Length);
            bitmap.UnlockBits(bitmapData);

            return bitmap;
        }

        /// <summary>Calculates the position and temperature of the hottest and coldest region with the given radius.</summary>
        /// 
        /// <returns>True, if calculation was successful. False otherwise.</returns>
        public bool CalculateMinMaxTemperatureRegions()
        {
            return imageBuilder.getMinMaxRegions(regionRadius, MinRegion, MaxRegion);
        }

        /// <summary>Calculates the mean temperature of a region with the given radius in the center of the frame.</summary>
        /// 
        /// <returns>True, if calculation was successful. False otherwise.</returns>
        public bool CalculateCenterMeanTemperatureRegion()
        {
            MeanRegion = new TemperatureRegion(Imager.getWidth() / 2 - regionRadius,
                                               Imager.getHeight() / 2 - regionRadius,
                                               Imager.getWidth() / 2 + regionRadius,
                                               Imager.getHeight() / 2 + regionRadius);

            return imageBuilder.getMeanTemperatureInRegion(MeanRegion);
        }


        // Callbacks
        /// <summary>Callback method triggered by imager when a new thermal frame is available.</summary>
        /// 
        /// <param name="thermal">thermal frame data.</param>
        /// <param name="meta">data of the thermal frame.</param>
        public override void onFrame(FrameEvent evt)
        {
            lock (frameEvent)
            {
                frameEvent = evt.clone();
                counter.trigger();
            }


            // Sending thermal frame to the recorder if recording is active
            if (recorder.IsRecording)
            {
                float[] temperatures = new float[frameEvent.thermalFrame.getSize()];

                frameEvent.thermalFrame.copyTemperaturesTo(
                    temperatures,
                    temperatures.Length);

                recorder.Enqueue(
                    new RecordedFrame(
                        frameEvent.thermalFrame.getWidth(),
                        frameEvent.thermalFrame.getHeight(),
                        temperatures,
                        frameEvent.meta,
                        saveDataType
                ));
            }
        }

        /// <summary>Starts the imager processing loop.</summary>
        private void StartProcessing()
        {
            /*
             * Get a list of the available operation modes. Each operation mode is a valid combination of optics,
             * temperature ranges and video formats. The returned container is always sorted in the same way. The
             * availability of certain operation modes can depend on the used connection type (USB, Ethernet) to
             * device (different video formats).
             */
            operationModes = Imager.getOperationModes();
            activeModeIndex = Imager.getActiveOperationMode().getIndex();

            UpdateOperationModeString();

            // Start processing
            Imager.runAsync();
        }

        /// <summary>Updates the string representation of the active operation mode.</summary>
        private void UpdateOperationModeString()
        {
            OperationMode mode = Imager.getActiveOperationMode();

            string opticsText = "";
            if (mode.getOpticsText().Length > 0)
            {
                opticsText = string.Format(" {},", mode.getOpticsText());
            }

            OperationModeString = string.Format("{0}°, {1}x{2} @ {3} Hz",
                                                 mode.getFieldOfView(),
                                                 mode.getFrameWidth(),
                                                 mode.getFrameHeight(),
                                                 mode.getFramerate());
        }

        public (float Lower, float Upper) GetTemperatureRange()
        {
            OperationMode mode = Imager.getActiveOperationMode();
            var range = (mode.getTemperatureLowerLimit(), mode.getTemperatureUpperLimit());

            return range;
        }

        public void ChangePalette(string paletteName)
        {
            if (IsConnected)
            {
                try
                {
                    imageBuilder.setPalette(paletteName);
                }
                catch (SDKException ex)
                {
                    ShowMessageBox(ex.Message, "Failed to change palette", MessageBoxButton.OK, MessageBoxImage.Error);
                    string palette = imageBuilder.getPaletteName();
                }
            }
        }

        // Thermal recording parameter
        public bool IsRecording{ get { return recorder.IsRecording; } }

        public void StartRecording(string directory, SaveDataType saveDataType, bool singleBinary, bool recordROIOnly)
        {
            this.saveDataType = saveDataType;
            recorder.Start(
                new RecorderSettings
                {
                    camera_width = ((HasROI) ? roiWidth : Imager.getWidth()),
                    camera_height = ((HasROI) ? roiHeight : Imager.getHeight()),
                    baseDirectory = directory,
                    dataType = saveDataType,
                    singleBinary = singleBinary,
                    recordROIOnly = recordROIOnly
                }
            );
        }

        public void StopRecording() => recorder.Stop();

        // public void SetScaleRange(float low, float high)
        // {
        //     if (!useAutoScaling && IsConnected) imageBuilder.setTemperatureScaling(low, high);
        // }
        // public void SetAutoScaling(bool enabled)
        // {
        //     useAutoScaling = enabled;

        //     if (IsConnected)
        //     {
        //         imageBuilder.setPaletteScalingMethod(
        //             enabled
        //                 ? PaletteScalingMethod.MinMax
        //                 : PaletteScalingMethod.Manual);
        //     }
        // }

        private static MessageBoxResult ShowMessageBox(string message, string title, MessageBoxButton button, MessageBoxImage icon)
        {
            Dispatcher? dispatcher = Application.Current?.Dispatcher;

            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                return dispatcher.Invoke(() => MessageBox.Show(message, title, button, icon));
            }

            return MessageBox.Show(message, title, button, icon);
        }
    }
}
