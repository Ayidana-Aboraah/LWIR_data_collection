using System.Globalization;
using System.IO;
using LWIR_app.classes;
using Optris.OtcSdk;
using RLE;

namespace LWIR_app.Sensor;

public class UniversalRecorder(SensorBase sensor) : RecorderBase(sensor)
{
    Task? writerTask;
    StreamWriter? metadataWriter;
    BinaryWriter? singleFileWriter;
    // CancellationTokenSource cancellation = new CancellationTokenSource();

    string sessionDirectory = "";
    string frameDirectory = "";
    int frameIndex = 0;

    public override void Start(RecorderSettings settings)
    {
        base.Start(settings);

        // cancellation.TryReset();

        frameIndex = 0;

        // sessionDirectory = Path.Combine(settings.baseDirectory, $"Session_{DateTime.Now:yyyyMMdd_HHmmss}");
        sessionDirectory = Path.Combine(settings.baseDirectory, $"{DateTime.Now:yyyy-MM-dd}");

        sessionDirectory = Path.Combine(sessionDirectory, $"LWIR");

        sessionDirectory = Path.Combine(sessionDirectory, $"Session_{DateTime.Now:HH_mm_ss}");

        Directory.CreateDirectory(sessionDirectory);

        metadataWriter = new StreamWriter(Path.Combine(sessionDirectory, "metadata.csv"));

        metadataWriter.WriteLine("Frame,Timestamp,Counter,HardwareCounter,MinTemp,MaxTemp,MeanTemp,BoxTemp,ChipTemp");

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

    public override void Stop()
    {
        if (!recording) return;

        // cancellation.Cancel();
        // writerTask!.Dispose();

        metadataWriter?.Flush();
        metadataWriter?.Close();
        if (settings.singleBinary) singleFileWriter?.Close();

        base.Stop();
    }

    private async Task WriterLoop()
    {
        await foreach (var frame in sensor.Reader().ReadAllAsync())
        {
            string suffix = settings.dataType.ToString() + (settings.recordROIOnly ? "_ROI" : "");
            string filename = Path.Combine(
                frameDirectory,
                $"frame_{frameIndex}_{suffix}.bin");

            using var writer =
                new BinaryWriter(
                    File.Open(filename, FileMode.Create, FileAccess.Write, FileShare.None));

            WriteFrameHeader(frame, writer);
            WriteFrame(frame, writer);
        }
    }

    private async Task SingleWriterLoop()
    {
        StreamWriter sWriter = new StreamWriter(singleFileWriter.BaseStream);
        
        await foreach (var frame in sensor.Reader().ReadAllAsync()) {
            WriteFrame(frame, singleFileWriter!);
            WriteYuriFrame(frame, sWriter);
        }
    }

    private void WriteFrame(FrameRecord frame, BinaryWriter writer)
    {
        WriteRecordedFrame(frame, writer);
        WriteMetadataRow(frame);
        frameIndex++;
    }

    private void WriteYuriFrame(FrameRecord frame, StreamWriter writer)
    {
        YuriFrame yFrame = new YuriFrame(frame);
        yFrame.Output(writer);
        frameIndex++;
    }

    private void WriteRecordedFrame(FrameRecord frame, BinaryWriter writer)
    {
        if (frame.data.Length != settings.camera_width * settings.camera_height) return;
        
        if (settings.recordROIOnly)
        {
            switch (settings.dataType)
            {
                case SaveDataType.Float: foreach(int ROI_Idx in sensor.ROI().indexes) writer.Write(frame.data[ROI_Idx]);
                    break;

                case SaveDataType.U16:
                    ushort[] iValue = DataConverter.FloatToInt(frame.data);
                    foreach(int ROI_Idx in sensor.ROI().indexes) writer.Write(iValue[ROI_Idx]);
                    break;

                case SaveDataType.RLE:
                    RunLengthPair[] data = DataConverter.IntToRLE(DataConverter.FloatToInt(frame.data));
                    foreach(int ROI_Idx in sensor.ROI().indexes)
                    {
                        writer.Write(data[ROI_Idx].value);
                        writer.Write(data[ROI_Idx].run);
                    }
                    break;
            }
        }
        else
        {
            switch (settings.dataType)
            {
                case SaveDataType.Float: foreach (float value in frame.data) writer.Write(value);
                    break;
                case SaveDataType.U16:   foreach (ushort value in DataConverter.FloatToInt(frame.data)) writer.Write(value);
                    break;
                case SaveDataType.RLE:   
                foreach (RunLengthPair value in DataConverter.IntToRLE(DataConverter.FloatToInt(frame.data)))
                    {
                        writer.Write(value.value);
                        writer.Write(value.run);
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

        double sum = 0;

        foreach (float temp in frame.data)
        {
            if (temp < min) min = temp;
            if (temp > max) max = temp;
            sum += temp;
        }

        double mean = sum / frame.data.Length;

        metadataWriter!.WriteLine(
            string.Join(",",
                frameIndex,
                ((FrameMetadata)frame.metadata).getTimestamp(),
                ((FrameMetadata)frame.metadata).getCounter(),
                ((FrameMetadata)frame.metadata).getCounterHardware(),
                min.ToString(CultureInfo.InvariantCulture),
                max.ToString(CultureInfo.InvariantCulture),
                mean.ToString(CultureInfo.InvariantCulture),
                ((FrameMetadata)frame.metadata).getTemperatureBox().ToString(CultureInfo.InvariantCulture),
                ((FrameMetadata)frame.metadata).getTemperatureChip().ToString(CultureInfo.InvariantCulture)));

        metadataWriter.Flush();
    }
}