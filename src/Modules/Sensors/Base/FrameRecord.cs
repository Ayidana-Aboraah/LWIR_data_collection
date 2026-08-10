using LWIR_app.classes;

namespace LWIR_app.Sensor;

public abstract record FrameData;
public record FloatFrame(float[] data) : FrameData;
public record IntFrame(ushort[] data) : FrameData;
public record RLEFrame(RLE_Pair[] data) : FrameData;
public struct FrameRecord
{
    public int width, height;
    public FrameData data;
    public BaseMetadata metadata;

    public FrameRecord(int width, int height, FrameData data, BaseMetadata metadata)
    {
        this.width = width;
        this.height = height;
        this.metadata = metadata;
        this.data = data;
    }

    public FrameRecord(int width, int height, float[] data, BaseMetadata metadata, SaveDataType saveType)
    {
        this.width = width;
        this.height = height;
        this.metadata = metadata;

        if (saveType == SaveDataType.Float) {
            this.data = new FloatFrame(data);
            return;
        }

        var temperature_Ints = new ushort[data.Length];
        var RLE = new RLE_Pair[data.Length];

        int current_pair = 0;
        for (int i = 0; i < data.Length; i++)
        {
            temperature_Ints[i] = (ushort)(data[i] * 100.0f);

            if (saveType == SaveDataType.U16) continue;

            if (RLE[current_pair].value != temperature_Ints[i] || RLE[current_pair].length >= ushort.MaxValue)
                RLE[++current_pair] = new RLE_Pair { value = temperature_Ints[i], length = 1 };
            else
                RLE[current_pair].length++;
        }
        Array.Resize(ref RLE, current_pair + 1);

        if (saveType == SaveDataType.U16) this.data = new IntFrame(temperature_Ints);
        else this.data = new RLEFrame(RLE);
    }
}