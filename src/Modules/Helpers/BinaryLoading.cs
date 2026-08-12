
using System.IO;
using LWIR_app.Sensor;
using Optris.OtcSdk;
using RLE;

namespace LWIR_app.classes
{
    public static class BinaryLoader
    {
        public static FrameRecord[] LoadFrames(string path)
        {
            if (!File.Exists(path)) return [];

            SaveDataType saveType = InferSaveType(path);

            using FileStream stream = File.OpenRead(path);
            using BinaryReader reader = new BinaryReader(stream);

            int width = reader.ReadInt32();
            int height = reader.ReadInt32();
            if (IsPerFrameBinary(path))
            {
                long timestamp = reader.ReadInt64();
                uint counter = reader.ReadUInt32();
                uint hardwareCounter = reader.ReadUInt32();
            }

            if (width <= 0 || height <= 0) throw new InvalidDataException($"Invalid frame dimensions in '{path}'.");

            var frames = new List<FrameRecord>();
            while (stream.Position < stream.Length)
                frames.Add(new FrameRecord(width, height, ReadFrameTemperatures(reader, width, height, saveType), new FrameMetadata()));

            return frames.ToArray();
        }

        public static FrameRecord[] LoadFrameSet(string path)
        {
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException($"Playback path not found: {path}");

            string[] binaryFiles = Directory
                .EnumerateFiles(path, "*.bin", SearchOption.AllDirectories)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (binaryFiles.Length == 0) return Array.Empty<FrameRecord>();

            var frames = new List<FrameRecord>();
            foreach (string binaryFile in binaryFiles) frames.AddRange(LoadFrames(binaryFile));

            return frames.ToArray();
        }
        private static float[] ReadFrameTemperatures(BinaryReader reader, int width, int height, SaveDataType saveType)
        {
            int pixelCount = checked(width * height);

            return saveType switch
            {
                SaveDataType.Float => ReadTemperatures<float>(reader, pixelCount),
                SaveDataType.U16 => ReadTemperatures<ushort>(reader, pixelCount),
                SaveDataType.RLE => ReadTemperatures<RunLengthPair>(reader, pixelCount),
                _ => ReadTemperatures<float>(reader, pixelCount)
            };
        }

        private static float[] ReadTemperatures<T>(BinaryReader reader, int pixelCount)
        {
            var temperatures = new float[pixelCount];

            if (typeof(T) == typeof(float))
            {
                for (int i = 0; i < pixelCount; i++) temperatures[i] = reader.ReadSingle();
            }
            else if (typeof(T) == typeof(ushort))
            {
                for (int i = 0; i < pixelCount; i++) temperatures[i] = reader.ReadUInt16();
            }
            else if (typeof(T) == typeof(RunLengthPair))
            {
                for (int count = 0; temperatures.Length < pixelCount;)
                {
                    ushort value = reader.ReadUInt16();
                    ushort length = reader.ReadUInt16();

                    if (length == 0) throw new InvalidDataException("Encountered an RLE pair with zero length.");

                    float temperature = value / 100.0f;
                    for (uint i = 0; i < length; i++) temperatures[count++] = temperature;
                }
            }

            return temperatures;
        }

        private static SaveDataType InferSaveType(string path)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);

            if (fileName.Contains("U16", StringComparison.OrdinalIgnoreCase) || fileName.Contains("Int", StringComparison.OrdinalIgnoreCase)) return SaveDataType.U16;

            if (fileName.Contains("RLE", StringComparison.OrdinalIgnoreCase)) return SaveDataType.RLE;

            return SaveDataType.Float;
        }

        private static bool IsPerFrameBinary(string path) => path.Contains("frames", StringComparison.OrdinalIgnoreCase);
    }
}