using System.Drawing;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace LWIR_app.Sensor;

public class DummySensor : SensorBase
{
    Channel<FrameRecord> channel = Channel.CreateBounded<FrameRecord>(new BoundedChannelOptions(60 * 60 * 15)
    {
        SingleReader = true,
        SingleWriter = true,
        FullMode = BoundedChannelFullMode.DropOldest
    });

    private readonly DispatcherTimer uiUpdateTimer = new();

    public DummySensor()
    {
        uiUpdateTimer.Interval = TimeSpan.FromMilliseconds(33);
        uiUpdateTimer.Tick += (_, _) =>
        {
            channel.Writer.WriteAsync(
                new FrameRecord(1, 1, [float.NaN], new BaseMetadata(), classes.SaveDataType.Float)
            );
        };
    }

    public float findValue(int x, int y) => (float)x * y;

    public Bitmap? Render() => null;

    public void QuickConnect() { }
    public void Disconnect() { }
    public bool Connected() => true;
    public void UpdateROI(System.Windows.Point a, System.Windows.Point b) { }
    public void ClearROI() { }
    public int[] ROI() => [];

    public ChannelReader<FrameRecord> Reader() => channel.Reader;

    public StackPanel UI() => new StackPanel();

    public void UpdateUI() { }
    public void DisableUI() { }
    public FrameworkElement Footer() => new StackPanel();
}