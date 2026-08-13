using System.Numerics;

namespace RLE;

public class RunLengthPair
{
    public ushort value, run = 1;
    public RunLengthPair(ushort value) => this.value = value;
}

public class SuperRunLengthPair
{
    public ushort value;
    public byte run = 1;
    public SuperRunLengthPair(ushort value) => this.value = value;
}

public static class DataConverter
{
    public static object? ValueToValue(Type inputType, Type outputType, Array input)
    {
        switch (inputType)
        {
            case var IT when IT == typeof(float):
                switch (outputType)
                {
                    case var OT when OT == typeof(float): return input;
                    case var OT when OT == typeof(ushort): return FloatToInt((float[])input);
                    case var OT when OT == typeof(RunLengthPair): return IntToRLE(FloatToInt((float[])input));
                }
                break;

            case var IT when IT == typeof(ushort):
                switch (outputType)
                {
                    case var OT when OT == typeof(float): return IntToFloat((ushort[])input);
                    case var OT when OT == typeof(ushort): return input;
                    case var OT when OT == typeof(RunLengthPair): return IntToRLE((ushort[])input);
                }
                break;

            case var IT when IT == typeof(RunLengthPair):
                switch (outputType)
                {
                    case var OT when OT == typeof(float): return IntToFloat(RLEToInt((RunLengthPair[])input));
                    case var OT when OT == typeof(ushort): return RLEToInt((RunLengthPair[])input);
                    case var OT when OT == typeof(RunLengthPair): return input;
                }
                break;
        }
        return null;
    }

    public static ushort[] FloatToInt(float[] values)
    {
        ushort[] result = new ushort[values.Length];
        for (int i = 0; i < result.Length; i++)
        {
            float scaled = values[i] * 100f;
            // Clamp to ushort range to prevent overflow
            if (scaled > ushort.MaxValue) result[i] = ushort.MaxValue;
            else if (scaled < ushort.MinValue) result[i] = ushort.MinValue;
            else result[i] = (ushort)Math.Floor(scaled);
        }
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
        if (values.Length == 0) return [];

        List<RunLengthPair> rle = [];
        ushort currentValue = values[0];
        int currentRun = 1;

        for (int i = 1; i < values.Length; i++)
        {
            if (values[i] == currentValue && currentRun < ushort.MaxValue) currentRun++;
            else
            {
                rle.Add(new RunLengthPair(currentValue) { run = (ushort)currentRun });
                currentValue = values[i];
                currentRun = 1;
            }
        }

        rle.Add(new RunLengthPair(currentValue) { run = (ushort)currentRun });
        return rle.ToArray();
    }

    // Return the full size, and which RLE was complete, and the last part of the run it was on
    public static (ushort[], int, int) RLEToInt(RunLengthPair[] values, int size)
    {
        ushort[] result = new ushort[size];
        int rle_idx = 0, run_idx = 0;
        
        for (int frame_data_count = 0; frame_data_count < result.Length;)
        {
            // Bounds check to prevent IndexOutOfRangeException
            if (rle_idx >= values.Length) break;
            
            for (run_idx = 0; run_idx < values[rle_idx].run; run_idx++)
            {
                if (frame_data_count >= result.Length) break;
                result[frame_data_count++] = values[rle_idx].value;
            }
            rle_idx++;
        }
        return (result, rle_idx, run_idx);
    }

    public static ushort[] RLEToInt(RunLengthPair[] values)
    {
        List<ushort> result = new List<ushort>();
        for (int rle_idx = 0; rle_idx < values.Length; rle_idx++) for (int a = 0; a < values[rle_idx].run; a++) result.Add(values[rle_idx].value);
        return result.ToArray();
    }
}
