using System.Threading.Channels;
using System.Windows.Threading;
using System.Windows.Controls;
using ThermalCamerApp.classes;
using System.Drawing;
using System.Windows;

namespace ThermalCamerApp.Camera;

public class DummySensor : SensorBase
{
    Channel<FrameRecord> channel = Channel.CreateBounded<FrameRecord>(new BoundedChannelOptions(60 * 60 * 15)
    {
        SingleReader = true,
        SingleWriter = true,
        FullMode = BoundedChannelFullMode.DropOldest
    });

    private readonly DispatcherTimer dataTimer = new();

    RegionOfInterest roi;

    public DummySensor()
    {
        roi = new();

        dataTimer.Interval = TimeSpan.FromMilliseconds(33);
        dataTimer.Tick += (_, _) => channel.Writer.WriteAsync(
                new FrameRecord(1, 1, [float.NaN], new BaseMetadata()));
    }

    public void Connect(string x){}

    public string status() => "";

    public bool HasROI() => false;
    public void IsRecording(bool stuff) => stuff = false;

    public float findValue(int x, int y) => (float)x * y;

    public Bitmap? Render() => null;

    public void Connect() { }
    public void Disconnect() { }
    public bool Connected() => true;
    public void UpdateROI(System.Windows.Point a, System.Windows.Point b) => roi = new RegionOfInterest(a,b, 1);
    public void ClearROI() { }
    public RegionOfInterest ROI() => roi;

    public (int, int) Dimensions() => (1, 1);

    public ChannelReader<FrameRecord> Reader() => channel.Reader;

    public StackPanel UI() => new StackPanel();

    public void UpdateUI() { }
    public void DisableUI() { }
    public FrameworkElement Footer() => new StackPanel();
}