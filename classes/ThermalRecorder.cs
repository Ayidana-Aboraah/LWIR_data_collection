using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Globalization;
using Optris.OtcSDK;
using System.Diagnostics;

namespace LWIR_app.classes
{
    public class ThermalRecorder : IDisposable
    {
        private BlockingCollection<RecordedFrame>? queue;
        private Task? writerTask;

        private bool isRecording;

        private string sessionDirectory = "";
        private string frameDirectory = "";

        private StreamWriter? metadataWriter;

        private int frameIndex = 0;
        private SaveDataType saveDataType = SaveDataType.All;

        public bool IsRecording => isRecording;

        public void Start(string baseDirectory)
        {
            Start(baseDirectory, SaveDataType.All);
        }

        public void Start(string baseDirectory, SaveDataType dataType)
        {
            if (isRecording)
                return;

            saveDataType = dataType;
            frameIndex = 0;

            string sessionName =
                $"Session_{DateTime.Now:yyyyMMdd_HHmmss}";

            sessionDirectory =
                Path.Combine(baseDirectory, sessionName);

            frameDirectory =
                Path.Combine(sessionDirectory, "frames");

            Directory.CreateDirectory(sessionDirectory);
            Directory.CreateDirectory(frameDirectory);

            metadataWriter =
                new StreamWriter(
                    Path.Combine(sessionDirectory, "metadata.csv"));

            metadataWriter.WriteLine(
                "Frame,Timestamp,Counter,HardwareCounter,MinTemp,MaxTemp,MeanTemp,BoxTemp,ChipTemp");

            queue =
                new BlockingCollection<RecordedFrame>(500);

            isRecording = true;

            writerTask =
                Task.Run(WriterLoop);
        }

        public void Stop()
        {
            if (!isRecording)
                return;

            queue!.CompleteAdding();

            writerTask!.Wait();

            metadataWriter?.Flush();
            metadataWriter?.Close();

            isRecording = false;
        }
        public void Enqueue(RecordedFrame frame)
        {
            try
            {
                if (queue != null && !queue.IsAddingCompleted)
                {
                    queue.Add(frame);
                }
            }
            catch (InvalidOperationException)
            {
                // Queue completed between check and Add().
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private void WriterLoop()
        {
            foreach (var frame in queue!.GetConsumingEnumerable())
            {
                WriteFrame(frame);
            }
        }
        private void WriteFrame(RecordedFrame frame)
        {
            switch (saveDataType) {
                case SaveDataType.BaseData: WriteBaseDataFrame(frame);
                break;
                
                case SaveDataType.IntData:  WriteIntFrame(frame);
                break;

                case SaveDataType.RleData:  WriteRleFrame(frame);
                break;

                default:
                    WriteBaseDataFrame(frame);
                    WriteIntFrame(frame);
                    WriteRleFrame(frame);
                    break;
            }

            WriteMetadataRow(frame);

            frameIndex++;
        }

        private void WriteBaseDataFrame(RecordedFrame frame)
        {
            string filename =
                Path.Combine(
                    frameDirectory,
                    $"frame_{frameIndex:D8}_base.bin");

            using var writer =
                new BinaryWriter(
                    File.Open(
                        filename,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None));

            WriteFrameHeader(frame, writer); // TODO: Discuss Removing or shortening Frame header

            foreach (float value in frame.temperatures)
            {
                writer.Write(value);
            }
        }

        private void WriteIntFrame(RecordedFrame frame)
        {
            string filename =
                Path.Combine(
                    frameDirectory,
                    $"frame_{frameIndex:D8}_int.bin");

            using var writer =
                new BinaryWriter(
                    File.Open(
                        filename,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None));

            WriteFrameHeader(frame, writer);
            foreach (ushort value in frame.temperature_Ints)
            {
                writer.Write(value);
            }
        }

        private void WriteRleFrame(RecordedFrame frame)
        {
            string filename =
                Path.Combine(
                    frameDirectory,
                    $"frame_{frameIndex:D8}_rle.bin");

            using var writer =
                new BinaryWriter(
                    File.Open(
                        filename,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None));

            WriteFrameHeader(frame, writer);

            Debug.WriteLine(frame.RLE.Length);

            for (int i = 0; i < frame.RLE.Length; i++){
                writer.Write(frame.RLE[i].value);
                writer.Write(frame.RLE[i].length);
            }
        }

        private static void WriteFrameHeader(RecordedFrame frame, BinaryWriter writer)
        {
            writer.Write(frame.width);
            writer.Write(frame.height);
            writer.Write(frame.metadata.getTimestamp());
            writer.Write(frame.metadata.getCounter());
            writer.Write(frame.metadata.getCounterHardware());
        }

        private void WriteMetadataRow(RecordedFrame frame)
        {
            float min = float.MaxValue;
            float max = float.MinValue;

            double sum = 0;

            foreach (float temp in frame.temperatures)
            {
                if (temp < min)
                    min = temp;

                if (temp > max)
                    max = temp;

                sum += temp;
            }

            double mean =
                sum / frame.temperatures.Length;

            metadataWriter!.WriteLine(
                string.Join(",",
                    frameIndex,
                    frame.metadata.getTimestamp(),
                    frame.metadata.getCounter(),
                    frame.metadata.getCounterHardware(),
                    min.ToString(CultureInfo.InvariantCulture),
                    max.ToString(CultureInfo.InvariantCulture),
                    mean.ToString(CultureInfo.InvariantCulture),
                    frame.metadata.getTemperatureBox().ToString(CultureInfo.InvariantCulture),
                    frame.metadata.getTemperatureChip().ToString(CultureInfo.InvariantCulture)));

            metadataWriter.Flush();
        }
    }


}
