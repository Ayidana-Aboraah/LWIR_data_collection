
using ThermalCamerApp.Camera;
using Optris.OtcSdk;
using System.IO;
using RLE;

namespace ThermalCamerApp.classes
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

        public static FrameRecord[] LoadFramesAndMarkers(string path)
        {
            if (!File.Exists(path)) return [];

            SaveDataType saveType = InferSaveType(path);
            // TODO: Pass the Type into the Load Marked Frame Functions 

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

            return LoadMarkedFrames(reader, width, height);
        }

        // TODO: Implementing partial Loading
        public static FrameRecord[] LoadMarkedFrames(BinaryReader reader, int width, int height)
        {
            var frameIdxes = LoadMarkers(reader, width, height);
            List<FrameRecord> records = new List<FrameRecord>();

            for (int i = 0; i < frameIdxes.Length - 1; i++)
            {
                List<RunLengthPair> frame = new List<RunLengthPair>();
                reader.BaseStream.Position = frameIdxes[i];

                int sum = 0;
                while (sum < width * height)
                {
                    ushort val = reader.ReadUInt16();
                    ushort run = reader.ReadUInt16();
                    frame.Add(new RunLengthPair(val) { run = run });
                    sum += run;
                }
                records.Add(new FrameRecord(width, height, DataConverter.IntToFloat(DataConverter.RLEToInt(frame.ToArray())), new BaseMetadata()));
            }
            return records.ToArray();
        }

        public static long[] LoadMarkers(BinaryReader reader, int width, int height)
        {
            // TODO: Revise the approach to looping through the file stream
            // Possible Issues:
            //  
            List<long> indexes = [];
            int sum = 0;
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                _ = reader.ReadUInt16();
                var len = reader.ReadUInt16();
                // var len = BitConverter.ToUInt16(buffer[2..]); // TODO: Check that this loads properly
                sum += len;
                if (sum == width * height)
                {
                    indexes.Add(reader.BaseStream.Position);
                    sum = 0;
                }
            }
            return indexes.ToArray();

            // TODO: Loop through the pairs and Load the Length
            // TODO: Loop until the sum of lengths == w * h
            // TODO: Mark indexes for frames
            // TODO: Return the marked indexes 
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
                for (int i = 0; i < pixelCount; i++) temperatures[i] = reader.ReadSingle();
            else if (typeof(T) == typeof(ushort))
                for (int i = 0; i < pixelCount; i++) temperatures[i] = reader.ReadUInt16() * 0.01f;
            else if (typeof(T) == typeof(RunLengthPair))
            {
                int count = 0;

                while (count < pixelCount)
                {
                    if (reader.BaseStream.Position + 4 > reader.BaseStream.Length) break;
                    // if (reader.BaseStream.Position + 4 > reader.BaseStream.Length) throw new EndOfStreamException($"Unexpected end of RLE data while decoding {pixelCount} pixels.");

                    ushort value = reader.ReadUInt16();
                    ushort length = reader.ReadUInt16();

                    if (length == 0) break;
                    // if (length == 0) throw new InvalidDataException("Encountered an RLE pair with zero length.");
                    // if (count + length > pixelCount)
                    //     throw new InvalidDataException($"RLE run length {length} exceeds remaining pixel count ({pixelCount - count}).");

                    float temperature = value * 0.01f;
                    for (int i = 0; i < length && count < pixelCount; i++) temperatures[count++] = temperature;
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