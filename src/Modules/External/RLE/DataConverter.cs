using System.Numerics;

namespace RLE;

public class RunLengthPair
{
    public ushort value, run = 1;
    public RunLengthPair(ushort value) => this.value = value;
}

public static class DataConverter
{
    public static O[]? ValueToValue<I, O>(I[] input)
        where I : RunLengthPair, INumber<ushort>, INumber<float>
        where O : RunLengthPair, INumber<ushort>, INumber<float>
    {
        switch (typeof(I))
        {
            case var IT when IT == typeof(float):
                switch (typeof(O))
                {
                    case var OT when OT == typeof(float): return (O[])(object)input;
                    case var OT when OT == typeof(ushort): return (O[])(object)FloatToInt((float[])(object)input);
                    case var OT when OT == typeof(RunLengthPair): return (O[])(object)IntToRLE((ushort[])(object)FloatToInt((float[])(object)input));
                }
                break;

            case var IT when IT == typeof(ushort):
                switch (typeof(O))
                {
                    case var OT when OT == typeof(float): return (O[])(object)IntToFloat((ushort[])(object)input);
                    case var OT when OT == typeof(ushort): return (O[])(object)input;
                    case var OT when OT == typeof(RunLengthPair): return (O[])(object)IntToRLE((ushort[])(object)input);
                }
                break;

            case var IT when IT == typeof(RunLengthPair):
                switch (typeof(O))
                {
                    case var OT when OT == typeof(float): return (O[])(object)IntToFloat((ushort[])(object)RLEToInt((RunLengthPair[])(object)input));
                    case var OT when OT == typeof(ushort): return (O[])(object)RLEToInt((RunLengthPair[])(object)input);
                    case var OT when OT == typeof(RunLengthPair): return (O[])(object)input;
                }
                break;
        }
        return null;
    }

    public static ushort[] FloatToInt(float[] values)
    {
        ushort[] result = new ushort[values.Length];
        for (int i = 0; i < result.Length; i++) result[i] = (ushort)Math.Floor(values[i] * 100f);
        return result;
    }

    public static float[] IntToFloat(ushort[] values)
    {
        float[] result = new float[values.Length];
        for (int i = 0; i < result.Length; i++) result[i] = values[i] * 0.01f;
        return result;
    }

    public static RunLengthPair[] IntToRLE(ushort[] values)
    {
        List<RunLengthPair> rle = [new RunLengthPair(values.Last())];
        for (int i = 0; i < values.Length; i++)
        {
            RunLengthPair last = rle.Last();
            if (last.value == values[i] && last.run < ushort.MaxValue) last.run++;
            else rle.Add(new RunLengthPair(values[i]));
        }
        return rle.ToArray();
    }

    // Return the full size, and which RLE was complete, and the last part of the run it was on
    public static (ushort[], int, int) RLEToInt(RunLengthPair[] values, int size)
    {
        ushort[] result = new ushort[size];
        int rle_idx = 0, run_idx = 0;
        
        for (int frame_data_count = 0; frame_data_count < result.Length;)
        {
            for (run_idx = 0; run_idx < values[rle_idx].run; run_idx++) result[frame_data_count++] = values[rle_idx].value;
            rle_idx++;
        }
        return (result, rle_idx, run_idx);
    }

    public static ushort[] RLEToInt(RunLengthPair[] values)
    {
        List<ushort> result = new List<ushort>();
        for (int i = 0; i < values.Length; i++) for (int a = 0; a < values[i].run; a++) result.Add(values[a].value);
        return result.ToArray();
    }
}
