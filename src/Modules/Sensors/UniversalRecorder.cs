using System.Globalization;
using System.IO;
using LWIR_app.classes;
using Optris.OtcSdk;

namespace LWIR_app.Sensor;

public class UniversalRecorder(SensorBase sensor) : RecorderBase(sensor)
{
    Task? writerTask;
    StreamWriter? metadataWriter;
    BinaryWriter? singleFileWriter;

    string sessionDirectory = "";
    string frameDirectory = "";
    int frameIndex = 0;
    RecorderSettings settings;

    public void Start(RecorderSettings settings)
    {
        this.settings = settings;
        if (recording) return;

        frameIndex = 0;

        string sessionName = $"Session_{DateTime.Now:yyyyMMdd_HHmmss}";

        sessionDirectory = Path.Combine(settings.baseDirectory, sessionName);

        Directory.CreateDirectory(sessionDirectory);

        metadataWriter = new StreamWriter(Path.Combine(sessionDirectory, "metadata.csv"));

        metadataWriter.WriteLine("Frame,Timestamp,Counter,HardwareCounter,MinTemp,MaxTemp,MeanTemp,BoxTemp,ChipTemp");

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

        writerTask!.Wait();

        metadataWriter?.Flush();
        metadataWriter?.Close();
        singleFileWriter?.Close();

        recording = false;
    }

    public void Dispose() => Stop();

    private async Task WriterLoop()
    {
        await foreach (FrameRecord frame in sensor.Reader().ReadAllAsync())
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

    private async Task SingleWriterLoop()
    {
        await foreach (FrameRecord frame in sensor.Reader().ReadAllAsync()) WriteFrame(frame, singleFileWriter);
    }

    private void WriteFrame(FrameRecord frame, BinaryWriter writer)
    {
        WriteRecordedFrame(frame, writer);
        WriteMetadataRow(frame);

        frameIndex++;
    }

    private void WriteRecordedFrame(FrameRecord frame, BinaryWriter writer)
    {
        if (settings.recordROIOnly)
        {
            switch (frame.saveType)
            {
                case SaveDataType.Float:
                    for (int i = 0; i < sensor.ROI().Length; i++) writer.Write(frame.data.fValue[sensor.ROI()[i]]);
                    break;
                case SaveDataType.U16:
                    for (int i = 0; i < sensor.ROI().Length; i++) writer.Write(frame.data.iValue[sensor.ROI()[i]]);
                    break;
                case SaveDataType.RLE:
                    for (int i = 0; i < sensor.ROI().Length; i++)
                    {
                        writer.Write(frame.data.rleValue[sensor.ROI()[i]].value);
                        writer.Write(frame.data.rleValue[sensor.ROI()[i]].length);
                    }
                    break;
            }
        }
        else
        {
            switch (frame.saveType)
            {
                case SaveDataType.Float:
                    foreach (float value in frame.data.fValue) writer.Write(value);
                    break;
                case SaveDataType.U16:
                    foreach (ushort value in frame.data.iValue) writer.Write(value);
                    break;
                case SaveDataType.RLE:
                    for (int i = 0; i < frame.data.rleValue.Length; i++)
                    {
                        writer.Write(frame.data.rleValue[i].value);
                        writer.Write(frame.data.rleValue[i].length);
                    }
                    break;
            }
        }
    }

    private void WriteBinHeader(BinaryWriter writer)
    {
        writer.Write(settings.camera_width);
        writer.Write(settings.camera_height);
    }

    private void WriteFrameHeader(FrameRecord frame, BinaryWriter writer)
    {
        writer.Write(settings.camera_width);
        writer.Write(settings.camera_height);
        writer.Write(((FrameMetadata)frame.metadata).getTimestamp());
        writer.Write(((FrameMetadata)frame.metadata).getCounter());
        writer.Write(((FrameMetadata)frame.metadata).getCounterHardware());
    }

    private void WriteMetadataRow(FrameRecord frame)
    {
        (float min, float max) = (float.MaxValue, float.MinValue);


        // TODO: Maybe just pass the analyser's statics tings into it
        // double sum = 0;

        // foreach (float temp in frame.temperatures)
        // {
        //     if (temp < min) min = temp;
        //     if (temp > max) max = temp;
        //     sum += temp;
        // }

        // double mean = sum / frame.temperatures.Length;

        metadataWriter!.WriteLine(
            string.Join(",",
                frameIndex,
                ((FrameMetadata)frame.metadata).getTimestamp(),
                ((FrameMetadata)frame.metadata).getCounter(),
                ((FrameMetadata)frame.metadata).getCounterHardware(),
                min.ToString(CultureInfo.InvariantCulture),
                max.ToString(CultureInfo.InvariantCulture),
                // mean.ToString(CultureInfo.InvariantCulture),
                "",
                ((FrameMetadata)frame.metadata).getTemperatureBox().ToString(CultureInfo.InvariantCulture),
                ((FrameMetadata)frame.metadata).getTemperatureChip().ToString(CultureInfo.InvariantCulture)));

        metadataWriter.Flush();
    }
}