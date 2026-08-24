using System.Threading.Channels;
using System.Windows.Controls;
using ThermalCamerApp.classes;
using System.Drawing.Imaging;
using System.Windows;
using System.Drawing;
using Basler.Pylon;
using RLE;
using System.Net.Http.Headers;

namespace ThermalCamerApp.Camera.NIR;

public struct NIR_Config
{
    public int width;
    public int height;
}

public class NIR : SensorBase
{
    Basler.Pylon.Camera camera;
    RegionOfInterest roi = new();
    PixelDataConverter converter = new PixelDataConverter();
    Channel<FrameRecord> recorderChannel = Channel.CreateUnbounded<FrameRecord>(new UnboundedChannelOptions()
    {
        SingleReader = true,
        SingleWriter = true,
    });

    public float[] currentTemperatures;
    IGrabResult? currentFrame;
    NIR_Config config;

    StackPanel UI_Panel = new StackPanel { };
    Border footer = new Border { };

    public NIR()
    {
        camera = new Basler.Pylon.Camera("USB", CameraSelectionStrategy.FirstFound);
        currentTemperatures = [];
    }

    public void ConvertToFrameData()
    {
        converter.OutputPixelFormat = PixelType.Mono16;
        ushort[] values = new ushort[currentFrame!.PayloadSize];
        converter.Convert(values, currentFrame);
        currentTemperatures = DataConverter.IntToFloat(values);
    }

    // Connection
    public void Connect()
    {
        camera.Open();
        // TODO: Set up a parameters display ting

        camera.Parameters[PLCameraLinkCamera.PixelFormat].SetValue(PLCamera.PixelFormat.Mono12);

        camera.StreamGrabber.Start();

        config = new NIR_Config
        {
            width = (int)camera.Parameters[PLCameraLinkCamera.Width].GetValue(),
            height = (int)camera.Parameters[PLCameraLinkCamera.Height].GetValue(),
        };

        camera.StreamGrabber.ImageGrabbed += (object? sender, ImageGrabbedEventArgs args) =>
        {
            currentFrame = args.GrabResult;
            ConvertToFrameData();
        };
    }
    public bool Connected() => camera!.IsConnected; // TODO: REPLACE
    public void Disconnect()
    {
        camera!.StreamGrabber.Stop();
        camera.Close();
    }

    // ROI
    public RegionOfInterest ROI() => roi;
    public void UpdateROI(System.Windows.Point start, System.Windows.Point end) => roi.Update(start, end, config.width);
    public (int, int) Dimensions() => (config.width, config.height);
    public float findValue(int x, int y) => currentTemperatures[(y * currentFrame!.Width) + x];

    // External
    public void IsRecording(bool recording) { }
    public ChannelReader<FrameRecord> Reader() => recorderChannel.Reader;

    // Rendering
    public Bitmap? Render()
    {
        if (camera!.StreamGrabber.IsGrabbing) return null;

        Bitmap bitmap = new Bitmap(config.width, config.height, PixelFormat.Format32bppRgb);
        BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, bitmap.PixelFormat);
        converter.OutputPixelFormat = PixelType.BGRA8packed; // Place the pointer to the buffer of the bitmap.

        converter.Convert(bmpData.Scan0, bmpData.Stride * bitmap.Height, currentFrame);
        bitmap.UnlockBits(bmpData);
        return bitmap;
    }

    public StackPanel UI() => UI_Panel;

    public void UpdateUI() { }

    public void DisableUI() { }

    public FrameworkElement Footer() => footer;
} 