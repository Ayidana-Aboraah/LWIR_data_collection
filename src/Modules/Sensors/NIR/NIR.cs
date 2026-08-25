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
    Basler.Pylon.Camera camera;
    RegionOfInterest roi = new();
    PixelDataConverter converter;
    Channel<FrameRecord> recorderChannel = Channel.CreateUnbounded<FrameRecord>(new UnboundedChannelOptions()
    {
        SingleReader = true,
        SingleWriter = true,
    });

    NIR_Config config;

    public float[] currentTemperatures = [];
    StackPanel UI_Panel = new StackPanel { };
    Border footer = new Border { };

    public NIR()
    {
        camera = new Basler.Pylon.Camera(CameraSelectionStrategy.FirstFound);
        converter = new PixelDataConverter();
    }

    public void parseTemperatures(IGrabResult currentFrame)
    {
        converter.OutputPixelFormat = PixelType.Mono16;
        ushort[] values = new ushort[currentFrame!.PayloadSize / 2];
        converter.Convert(values, currentFrame);
        currentTemperatures = DataConverter.IntToFloat(values);
    }

    // Connection
    public void Connect(string filename) => Connect(); 

    public void Connect()
    {
        camera.Open();
        // TODO: Set up a parameters display ting

        camera.Parameters[PLCameraLinkCamera.PixelFormat].SetValue(PLCamera.PixelFormat.Mono12);

        camera.StreamGrabber.Start(GrabStrategy.LatestImages, GrabLoop.ProvidedByStreamGrabber);

        camera.StreamGrabber.ImageGrabbed += (object? sender, ImageGrabbedEventArgs args) =>
        {
            var currentFrame = args.GrabResult;
            parseTemperatures(currentFrame);
            config.Width = currentFrame.Width;
            config.Height = currentFrame.Height;
            recorderChannel.Writer.WriteAsync(new FrameRecord(currentFrame.Width, currentFrame.Height, currentTemperatures, new BaseMetadata()));
        };
    }
    public bool Connected() => camera!.IsConnected;
    public void Disconnect()
    {
        camera!.StreamGrabber.Stop();
        camera.Close();
    }

    // ROI
    public RegionOfInterest ROI() => roi;
    public void UpdateROI(System.Windows.Point start, System.Windows.Point end) => roi.Update(start, end, config.Width);
    public (int, int) Dimensions() => (config.Width, config.Height);
    public float findValue(int x, int y) => currentTemperatures[(y * config.Width) + x];

    // External
    public void IsRecording(bool recording) { }
    public ChannelReader<FrameRecord> Reader() => recorderChannel.Reader;

    // Rendering
    public Bitmap? Render()
    {
        if (!camera!.StreamGrabber.IsGrabbing || currentTemperatures.Count() < 1) return null;

        (float min, float max, _) = PlaybackTool.CalculateStatistics(currentTemperatures);
        return PaletteTool.Render(currentTemperatures, min, max, config.Width, config.Height);
    }

    public string status() => "";
    public StackPanel UI() => UI_Panel;

    public void UpdateUI() { }

    public void DisableUI() { }

    public FrameworkElement Footer() => footer;
}