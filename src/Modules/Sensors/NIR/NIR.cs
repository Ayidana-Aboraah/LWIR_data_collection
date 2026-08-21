using System.Drawing;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using ThermalCamerApp.classes;

namespace ThermalCamerApp.Camera.NIR;

public class NIR : SensorBase
{

    // Connection
    public void Connect(){}
    public bool Connected() => true; // TODO: REPLACE
    public void Disconnect(){}

    // ROI
    public RegionOfInterest ROI()
    {
        return null; // TODO: REMOVE
    }
    public void  UpdateROI(System.Windows.Point start, System.Windows.Point end){}
    public (int, int) Dimensions() => (0,0); // TODO: REPLACE
    public float findValue(int x, int y) => 0; // TODO: REPLACE
    public bool HasROI() => true; // TODO: REPLACE
    public void ClearROI(){}

    // External
    public void IsRecording(bool recording) {}
    public ChannelReader<FrameRecord> Reader() => null; // TODO: REPLACE

    // Rendering
    public Bitmap? Render()
    {
        return null; // TODO: REMOVE
    }

    public StackPanel UI()
    {
        return null; // TODO: REMOVE
    }

    public void UpdateUI(){}

    public void DisableUI(){}

    public FrameworkElement Footer()
    {
        return null; // TODO: REMOVE
    }
}