using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using LWIR_app.Sensor;

namespace LWIR_app.classes
{
    public class ThermalRecorder(SensorBase sensor) : RecorderBase(sensor), IDisposable
    {
        private BlockingCollection<RecordedFrame> queue = [];
        private Task? writerTask;
        private string sessionDirectory = "";
        private string frameDirectory = "";

        private StreamWriter? metadataWriter;
        private BinaryWriter? singleFileWriter;
        private int frameIndex = 0;
        private RecorderSettings settings;

        public void Start(RecorderSettings settings)
        {
            this.settings = settings;
            if (recording) return;

            frameIndex = 0;

            string sessionName = $"Session_{DateTime.Now:yyyyMMdd_HHmmss}";

            sessionDirectory = Path.Combine(settings.baseDirectory, sessionName);

            Directory.CreateDirectory(sessionDirectory);

            metadataWriter = new StreamWriter(Path.Combine(sessionDirectory, "metadata.csv"));

            metadataWriter.WriteLine(
                "Frame,Timestamp,Counter,HardwareCounter,MinTemp,MaxTemp,MeanTemp,BoxTemp,ChipTemp");

            queue = new BlockingCollection<RecordedFrame>(600);
            recording = true;

            if (settings.singleBinary)
            {
                string suffix = settings.dataType.ToString() + (settings.recordROIOnly ? "_ROI" : "");
                string filename = Path.Combine(
                    sessionDirectory,
                    $"frame_{suffix}.bin");
                singleFileWriter = new BinaryWriter(
                    File.Open(
                        filename,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None));

                // Don't worry about the queue warning since start can only be called after getting camera feed
                WriteBinHeader(singleFileWriter);

                writerTask = Task.Run(SingleWriterLoop);
            }
            else
            {
                frameDirectory = Path.Combine(sessionDirectory, "frames");
                Directory.CreateDirectory(frameDirectory);
                writerTask = Task.Run(WriterLoop);
            }
        }

        public void Stop()
        {
            if (!recording) return;

            queue!.CompleteAdding();

            writerTask!.Wait();

            metadataWriter?.Flush();
            metadataWriter?.Close();
            singleFileWriter?.Close();

            recording = false;
        }
        
        public void Enqueue(RecordedFrame frame)
        {
            try
            {
                if (queue != null && !queue.IsAddingCompleted) queue.Add(frame);
            }
            catch (InvalidOperationException)
            {
                // Queue completed between check and Add().
            }
        }

        public RecordedFrame[]? ReadFrames()
        {
            if (!recording) return null;

            var len = queue.Count;
            return queue.ToArray()[(len - 20)..len];
        }

        public void Dispose() => Stop();

        private void WriterLoop()
        {
            foreach (var frame in queue!.GetConsumingEnumerable())
            {
                string suffix = settings.dataType.ToString() + (settings.recordROIOnly ? "_ROI" : "");
                string filename = Path.Combine(
                    frameDirectory,
                    $"frame_{frameIndex}_{suffix}.bin");

                using var writer =
                    new BinaryWriter(
                        File.Open(
                            filename,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.None));

                WriteFrameHeader(frame, writer);
                WriteFrame(frame, writer);
            }
        }

        private void SingleWriterLoop()
        {
            foreach (var frame in queue!.GetConsumingEnumerable()) WriteFrame(frame, singleFileWriter);
        }

        private void WriteFrame(RecordedFrame frame, BinaryWriter writer)
        {

            switch (frame.saveType)
            {
                case SaveDataType.U16: WriteIntFrame(frame, writer);
                    break;

                case SaveDataType.RLE: WriteRleFrame(frame, writer);
                    break;

                default: WriteBaseDataFrame(frame, writer);
                    break;
            }

            WriteMetadataRow(frame);

            frameIndex++;
        }

        private void WriteBaseDataFrame(RecordedFrame frame, BinaryWriter writer)
        {
            if (settings.recordROIOnly)
            {
                for (int i = 0; i < sensor.ROI().Length; i++) writer.Write(frame.temperatures[sensor.ROI()[i]]);
            }
            else foreach (float value in frame.temperatures) writer.Write(value);
        }

        private void WriteIntFrame(RecordedFrame frame, BinaryWriter writer)
        {
            if (settings.recordROIOnly)
            {
                for (int i = 0; i < sensor.ROI().Length; i++) writer.Write(frame.temperature_Ints[sensor.ROI() [i]]);
            }
            else foreach (ushort value in frame.temperature_Ints) writer.Write(value);
        }

        private void WriteRleFrame(RecordedFrame frame, BinaryWriter writer)
        {
            if (settings.recordROIOnly)
            {
                for (int i = 0; i < sensor.ROI().Length; i++)
                {
                    writer.Write(frame.RLE[sensor.ROI()[i]].value);
                    writer.Write(frame.RLE[sensor.ROI()[i]].length);
                }
            }
            else
            {
                for (int i = 0; i < frame.RLE.Length; i++)
                {
                    writer.Write(frame.RLE[i].value);
                    writer.Write(frame.RLE[i].length);
                }
            }
        }

        private void WriteBinHeader(BinaryWriter writer)
        {
            writer.Write(settings.camera_width);
            writer.Write(settings.camera_height);
        }

        private void WriteFrameHeader(RecordedFrame frame, BinaryWriter writer)
        {
            writer.Write(settings.camera_width);
            writer.Write(settings.camera_height);
            writer.Write(frame.metadata.getTimestamp());
            writer.Write(frame.metadata.getCounter());
            writer.Write(frame.metadata.getCounterHardware());
        }

        private void WriteMetadataRow(RecordedFrame frame)
        {
            (float min, float max) = (float.MaxValue, float.MinValue);

            double sum = 0;

            foreach (float temp in frame.temperatures)
            {
                if (temp < min) min = temp;
                if (temp > max) max = temp;
                sum += temp;
            }

            double mean = sum / frame.temperatures.Length;

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
