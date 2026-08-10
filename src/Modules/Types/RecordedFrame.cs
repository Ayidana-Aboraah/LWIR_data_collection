using Optris.OtcSdk;
using RLE;

namespace LWIR_app.classes
{
    public class RecordedFrame
    {
        public int width { get; set; }
        public int height { get; set; }

        public float[] temperatures { get; set; }

        public FrameMetadata metadata { get; set; }

        public SaveDataType saveType;

        public ushort[] temperature_Ints;
        public RunLengthPair[] RLE;

        public RecordedFrame(int width, int height, float[] temperatures, FrameMetadata metadata, SaveDataType saveType)
        {
            this.width = width;
            this.height = height;
            this.temperatures = temperatures;
            this.metadata = metadata;
            this.saveType = saveType;

            if (saveType == SaveDataType.Float) return;

            temperature_Ints = new ushort[temperatures.Length];
            RLE = new RunLengthPair[temperatures.Length];

            int current_pair = 0;
            for (int i = 0; i < temperatures.Length; i++)
            {
                temperature_Ints[i] = (ushort)(temperatures[i] * 100.0f);

                if (saveType == SaveDataType.U16) continue;

                if (RLE[current_pair].value != temperature_Ints[i] || RLE[current_pair].run >= ushort.MaxValue)
                    RLE[++current_pair] = new RunLengthPair(temperature_Ints[i]);
                else 
                    RLE[current_pair].run++;
            }
            Array.Resize(ref RLE, current_pair+1);
        }
    }
}
