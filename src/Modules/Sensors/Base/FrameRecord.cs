using System.Runtime.InteropServices;
using RLE;

namespace LWIR_app.Sensor;

[StructLayout(LayoutKind.Explicit)]
public struct FrameData
{
    [FieldOffset(0)] public ushort[] iValue;
    [FieldOffset(0)] public float[] fValue;
    [FieldOffset(0)] public RunLengthPair[] rleValue;
}

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