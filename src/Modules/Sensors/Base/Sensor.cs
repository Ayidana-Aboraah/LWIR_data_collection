using System.Drawing;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;

namespace LWIR_app.Sensor;

public interface SensorBase
{
    public abstract float findValue(int x, int y);
    public abstract Bitmap? Render();
    public abstract void QuickConnect();
    public abstract void Disconnect();
    public abstract void UpdateUI();
    public abstract bool Connected();
    public abstract void UpdateROI(System.Windows.Point start, System.Windows.Point end);
    public abstract void ClearROI();
    public abstract int[] ROI();

    public abstract ChannelReader<FrameRecord> Reader();

    public abstract StackPanel UI();
    public abstract FrameworkElement Footer();
}