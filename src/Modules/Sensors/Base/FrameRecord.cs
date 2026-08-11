using System.Runtime.InteropServices;
using LWIR_app.classes;
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
    public FrameData data;
    public BaseMetadata metadata;

    public FrameRecord(int width, int height, float[] data, BaseMetadata metadata, SaveDataType saveType)
    {
        this.width = width;
        this.height = height;
        this.metadata = metadata;

        switch (saveType)
        {
            case SaveDataType.U16: this.data.iValue = DataConverter.FloatToInt(data);
            break;
            case SaveDataType.RLE: this.data.rleValue = DataConverter.IntToRLE(DataConverter.FloatToInt(data));
            break;
            default: this.data.fValue = data;
            break;
        }
    }
}