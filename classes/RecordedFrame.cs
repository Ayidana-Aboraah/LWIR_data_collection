using Optris.OtcSDK;

namespace LWIR_app.classes
{
    public class RecordedFrame
    {
        public int width { get; set; }
        public int height { get; set; }

        public float[] temperatures { get; set; }

        public FrameMetadata metadata { get; set; }

        public SaveDataType saveType;

        public UInt16[] temperature_Ints;
        public RLE_Pair[] RLE;

        public RecordedFrame(int width, int height, float[] temperatures, FrameMetadata metadata, SaveDataType saveType)
        {
            this.width = width;
            this.height = height;
            this.temperatures = temperatures;
            this.metadata = metadata;
            this.saveType = saveType;

            if (saveType == SaveDataType.BaseData) return;

            temperature_Ints = new UInt16[temperatures.Length];
            RLE = new RLE_Pair[temperatures.Length];

            int current_pair = 0;
            for (int i = 0; i < temperatures.Length; i++)
            {

                // Int Compression
                temperature_Ints[i] = (UInt16)(MathF.Truncate(temperatures[i] * 10.0f));

                if (saveType == SaveDataType.IntData) continue;

                // Run-Length Encoding
                if (RLE[current_pair].value == temperature_Ints[i])
                    RLE[current_pair].length++;
                else
                    RLE[++current_pair] = new RLE_Pair { value = temperature_Ints[i], length = 1 };
            }
            Array.Resize(ref RLE, current_pair+1);
        }
    }

    public struct RLE_Pair
    {
        public UInt16 value;
        public UInt32 length;
    }
}
