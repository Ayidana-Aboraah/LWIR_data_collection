using System.Drawing;

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
}