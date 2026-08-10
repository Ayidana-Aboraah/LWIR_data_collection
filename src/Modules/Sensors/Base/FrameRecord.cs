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

    public SaveDataType saveType;

    public FrameRecord(int width, int height, float[] data, BaseMetadata metadata, SaveDataType saveType)
    {
        this.width = width;
        this.height = height;
        this.metadata = metadata;
        this.saveType = saveType;

        if (saveType == SaveDataType.Float) {
            this.data.fValue = data;
            return;
        }

        var temperature_Ints = new ushort[data.Length];
        var RLE = new RunLengthPair[data.Length];

        int current_pair = 0;
        for (int i = 0; i < data.Length; i++)
        {
            temperature_Ints[i] = (ushort)(data[i] * 100.0f);

            if (saveType == SaveDataType.U16) continue;

            if (RLE[current_pair].value != temperature_Ints[i] || RLE[current_pair].run >= ushort.MaxValue)
                RLE[++current_pair] = new RunLengthPair(temperature_Ints[i]);
            else
                RLE[current_pair].run++;
        }
        Array.Resize(ref RLE, current_pair + 1);

        if (saveType == SaveDataType.U16) this.data.iValue = temperature_Ints;
        else this.data.rleValue = RLE;
    }
}