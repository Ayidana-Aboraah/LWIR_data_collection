using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Globalization;
using Optris.OtcSDK;

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

        public bool IsRecording => isRecording;

        public void Start(string baseDirectory)
        {
            if (isRecording)
                return;

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
            string filename =
                Path.Combine(
                    frameDirectory,
                    $"frame_{frameIndex:D8}.bin");

            using (var writer =
                   new BinaryWriter(
                       File.Open(
                           filename,
                           FileMode.Create,
                           FileAccess.Write,
                           FileShare.None)))
            {
                writer.Write(frame.Width);
                writer.Write(frame.Height);

                writer.Write(frame.Metadata.getTimestamp());
                writer.Write(frame.Metadata.getCounter());
                writer.Write(frame.Metadata.getCounterHardware());

                foreach (float value in frame.Temperatures)
                {
                    writer.Write(value);
                }
            }

            WriteMetadataRow(frame);

            frameIndex++;
        }

        private void WriteMetadataRow(RecordedFrame frame)
        {
            float min = float.MaxValue;
            float max = float.MinValue;

            double sum = 0;

            foreach (float temp in frame.Temperatures)
            {
                if (temp < min)
                    min = temp;

                if (temp > max)
                    max = temp;

                sum += temp;
            }

            double mean =
                sum / frame.Temperatures.Length;

            metadataWriter!.WriteLine(
                string.Join(",",
                    frameIndex,
                    frame.Metadata.getTimestamp(),
                    frame.Metadata.getCounter(),
                    frame.Metadata.getCounterHardware(),
                    min.ToString(CultureInfo.InvariantCulture),
                    max.ToString(CultureInfo.InvariantCulture),
                    mean.ToString(CultureInfo.InvariantCulture),
                    frame.Metadata.getTemperatureBox().ToString(CultureInfo.InvariantCulture),
                    frame.Metadata.getTemperatureChip().ToString(CultureInfo.InvariantCulture)));

            metadataWriter.Flush();
        }
    }
}
