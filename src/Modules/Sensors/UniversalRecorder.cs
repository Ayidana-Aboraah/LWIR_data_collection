using ThermalCamerApp.classes;
using System.IO;
using RLE;

namespace ThermalCamerApp.Camera;

public class UniversalRecorder(SensorBase sensor) : RecorderBase(sensor)
{
    StreamWriter? metadataWriter;
    BinaryWriter? singleFileWriter;

    string sessionDirectory = "";
    string frameDirectory = "";
    int frameIndex = 0;
    CancellationTokenSource? writerCancellation;
    Task? writerTask;

    public override void Start(RecorderSettings settings)
    {
        base.Start(settings);

        frameIndex = 0;

        sessionDirectory = Path.Combine(settings.baseDirectory, $"{DateTime.Now:yyyy-MM-dd}");

        sessionDirectory = Path.Combine(sessionDirectory, SensorManager.ProjectName);

        sessionDirectory = Path.Combine(sessionDirectory, SensorManager.SensorName);

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

            writerCancellation = new CancellationTokenSource();
            writerTask = Task.Run(() => SingleWriterLoop(writerCancellation.Token));
        }
        else
        {
            frameDirectory = Path.Combine(sessionDirectory, "frames");
            Directory.CreateDirectory(frameDirectory);
            writerCancellation = new CancellationTokenSource();
            writerTask = Task.Run(() => WriterLoop(writerCancellation.Token));
        }
    }

    public override void Stop()
    {
        if (!recording) return;

        base.Stop();
        writerCancellation?.Cancel();
        try { writerTask?.GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { }
        writerCancellation?.Dispose();
        writerCancellation = null;
        writerTask = null;
        metadataWriter?.Flush();
        metadataWriter?.Dispose();
        metadataWriter = null;
        singleFileWriter?.Dispose();
        singleFileWriter = null;
    }

    private async Task WriterLoop(CancellationToken cancellationToken)
    {
        await foreach (var frame in sensor.Reader().ReadAllAsync(cancellationToken))
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

    private async Task SingleWriterLoop(CancellationToken cancellationToken)
    {
        // TODO: Setup the filepath for Everything and note Yuri on it
        // StreamWriter sWriter = new StreamWriter(new FileStream("Yuri.bin", FileMode.Append));

        await foreach (var frame in sensor.Reader().ReadAllAsync(cancellationToken))
        {
            WriteFrame(frame, singleFileWriter!);
            // WriteYuriFrame(frame, sWriter);
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
                case SaveDataType.Float:
                    foreach (int ROI_Idx in sensor.ROI().indexes) writer.Write(frame.data[ROI_Idx]);
                    break;

                case SaveDataType.U16:
                    foreach (int ROI_Idx in sensor.ROI().indexes) writer.Write(ToUShort(frame.data[ROI_Idx]));
                    break;

                case SaveDataType.RLE:
                    List<float> roi_temps = new List<float>();
                    foreach (int ROI_Idx in sensor.ROI().indexes) roi_temps.Add(frame.data[ROI_Idx]);

                    RunLengthPair[] data = DataConverter.IntToRLE(DataConverter.FloatToInt(roi_temps.ToArray()));
                    foreach (RunLengthPair pair in data)
                    {
                        writer.Write(pair.value);
                        writer.Write(pair.run);
                    }
                    break;
            }
        }
        else
        {
            switch (settings.dataType)
            {
                case SaveDataType.Float:
                    foreach (float value in frame.data) writer.Write(value);
                    break;
                case SaveDataType.U16:
                    foreach (float value in frame.data) writer.Write(ToUShort(value));
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

    private static ushort ToUShort(float value)
    {
        float scaled = value * 100f;
        if (scaled > ushort.MaxValue) return ushort.MaxValue;
        if (scaled < ushort.MinValue) return ushort.MinValue;
        return (ushort)Math.Floor(scaled);
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
        frame.metadata.WriteHeader(writer);
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

        frame.metadata.WriteMetadata(metadataWriter!, frameIndex, min, max, mean);

        metadataWriter!.Flush();
    }
}