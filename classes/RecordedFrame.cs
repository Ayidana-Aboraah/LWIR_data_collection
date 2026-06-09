using Optris.OtcSDK;

namespace LWIR_app.classes
{
    public class RecordedFrame
    {
        public int Width { get; set; }
        public int Height { get; set; }

        public required float[] Temperatures { get; set; }

        public required FrameMetadata Metadata { get; set; }
        public ushort[] ConvertTemperatures(float[] temps)
        {
            ushort[] ints = new ushort[temps.Length];
            for (int i = 0; i < temps.Length; i++)
                ints[i] = (ushort) (temps[i] * 100);
            return ints;
        }
    }
}
