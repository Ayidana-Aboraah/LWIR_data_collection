using System.Drawing;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using ThermalCamerApp.classes;

namespace ThermalCamerApp.Camera;

public interface SensorBase
{
    public abstract float findValue(int x, int y);
    public abstract Bitmap? Render();
    public abstract void Connect();
    public abstract void Connect(string filename);
    public abstract void Disconnect();
    public abstract bool Connected();
    public abstract void UpdateROI(System.Windows.Point start, System.Windows.Point end);
    public abstract RegionOfInterest ROI();

    public abstract (int, int) Dimensions();

    public abstract void IsRecording(bool recording);

    public abstract ChannelReader<FrameRecord> Reader();
    
    public abstract StackPanel UI();
    public abstract void UpdateUI();
    public abstract void DisableUI();
    public string status();

    public abstract FrameworkElement Footer();
}