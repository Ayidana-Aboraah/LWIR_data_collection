namespace ThermalCamerApp.Camera;

public class FrameRecord
{
    public int width, height;
    public float[] data;
    public BaseMetadata metadata;

    public FrameRecord(int width, int height, float[] data, BaseMetadata metadata)
    {
        this.width = width;
        this.height = height;
        this.metadata = metadata;
        this.data = data;
    }
}