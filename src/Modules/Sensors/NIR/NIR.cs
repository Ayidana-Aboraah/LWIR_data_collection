using System.Threading.Channels;
using System.Windows.Controls;
using ThermalCamerApp.classes;
using System.Drawing.Imaging;
using System.Windows;
using System.Drawing;
using Basler.Pylon;
using RLE;
using ThermalCamerApp.models;
using System.ComponentModel.DataAnnotations;

namespace ThermalCamerApp.Camera.NIR;

public struct NIR_Config
{
    public int Width, Height;
}

public class NIR : SensorBase
{
    private const double AcquisitionFrameRate = 30.0;
    private const int RecorderBufferCapacity = 120;

    Basler.Pylon.Camera camera;
    RegionOfInterest roi = new();
    PixelDataConverter converter;
    Channel<FrameRecord> recorderChannel = Channel.CreateBounded<FrameRecord>(new BoundedChannelOptions(RecorderBufferCapacity)
    {
        SingleReader = true,
        SingleWriter = true,
        FullMode = BoundedChannelFullMode.Wait,
    });

    NIR_Config config;
    private volatile bool recording;
    private readonly EventHandler<ImageGrabbedEventArgs> imageGrabbedHandler;

    public float[] currentTemperatures = [];
    Border footer = new Border { };

    public NIR()
    {
        camera = new Basler.Pylon.Camera(CameraSelectionStrategy.FirstFound);
        converter = new PixelDataConverter();
        imageGrabbedHandler = OnImageGrabbed;
        camera.StreamGrabber.ImageGrabbed += imageGrabbedHandler;
    }

    public void parseTemperatures(IGrabResult currentFrame)
    {
        converter.OutputPixelFormat = PixelType.Mono16;
        ushort[] values = new ushort[currentFrame!.PayloadSize / 2];
        converter.Convert(values, currentFrame);
        currentTemperatures = DataConverter.IntToFloat(values);
        for (int i = 0; i < currentTemperatures.Length; i++) currentTemperatures[i] = (currentTemperatures[i] < 10) ? 0 : currentTemperatures[i];
    }

    // Connection
    public void Connect(string filename) => Connect(); 

    public void Connect()
    {
        camera.Open(); // TODO: Set up a parameters display ting

        camera.Parameters[PLCameraLinkCamera.PixelFormat].SetValue(PLCamera.PixelFormat.Mono12);

        if (camera.Parameters[PLCamera.AcquisitionFrameRateEnable].IsWritable &&
            camera.Parameters[PLCamera.AcquisitionFrameRate].IsWritable)
        {
            camera.Parameters[PLCamera.AcquisitionFrameRateEnable].SetValue(true);
            camera.Parameters[PLCamera.AcquisitionFrameRate].SetValue(AcquisitionFrameRate);
        }

        camera.StreamGrabber.Start(GrabStrategy.LatestImages, GrabLoop.ProvidedByStreamGrabber);
    }

    private void OnImageGrabbed(object? sender, ImageGrabbedEventArgs args)
    {
        IGrabResult currentFrame = args.GrabResult;
        parseTemperatures(currentFrame);
        config.Width = currentFrame.Width;
        config.Height = currentFrame.Height;
        if (recording)
            recorderChannel.Writer.WriteAsync(new FrameRecord(currentFrame.Width, currentFrame.Height, currentTemperatures, new BaseMetadata())).GetAwaiter().GetResult();
    }
    public bool Connected() => camera!.IsOpen;
    public void Disconnect()
    {
        if (!camera.IsOpen) return;

        recording = false;
        if (camera.StreamGrabber.IsGrabbing) camera.StreamGrabber.Stop();
        camera.Close();
    }

    public void Dispose()
    {
        Disconnect();
        converter.Dispose();
        camera.Dispose();
        GC.SuppressFinalize(this);
    }

    // ROI
    public RegionOfInterest ROI() => roi;
    public void UpdateROI(System.Windows.Point start, System.Windows.Point end) => roi.Update(start, end, config.Width);
    public (int, int) Dimensions() => (config.Width, config.Height);
    public float findValue(int x, int y) => currentTemperatures[(y * config.Width) + x];

    // External
    public void IsRecording(bool recording) => this.recording = recording;
    public ChannelReader<FrameRecord> Reader() => recorderChannel.Reader;

    // Rendering
    public Bitmap? Render()
    {
        if (!camera!.StreamGrabber.IsGrabbing || currentTemperatures.Count() < 1) return null;

        (float min, float max, _) = PlaybackTool.CalculateStatistics(currentTemperatures);
        return PaletteTool.Render(currentTemperatures, min, max, config.Width, config.Height);
    }

    public string status() => "";
    public StackPanel UI()
    {
        StackPanel panel = new StackPanel { Orientation = Orientation.Vertical};
        
        // TODO: Add Threshold
        // TODO: Gain Slider
        // TODO: Exposure Slider

        return panel;
    }

    public void UpdateUI() { }

    public void DisableUI() { }

    public FrameworkElement Footer() => footer;
}