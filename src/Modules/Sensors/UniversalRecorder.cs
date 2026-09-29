using ThermalCamerApp.classes;
using System.IO;
using System.Threading.Channels;
using RLE;

namespace ThermalCamerApp.Camera;

public class UniversalRecorder(SensorBase sensor) : RecorderBase(sensor)
{
    private const int WorkerCount = 4;
    private const int FixedArraySize = 16;

    private sealed record IndexedFrame(int Index, FrameRecord Frame);
    private sealed record CompressedFrame(int Index, FrameRecord Frame, byte[] Data);

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

      metadataWriter.Write("Frame,Timestamp,Counter,HardwareCounter,BoxTemp,ChipTemp,MinTemp,MaxTemp,MeanTemp\n");


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
        await RunPipeline(cancellationToken, compressedFrame =>
        {
            string suffix = settings.dataType.ToString() + (settings.recordROIOnly ? "_ROI" : "");
            string filename = Path.Combine(
                frameDirectory,
                $"frame_{compressedFrame.Index}_{suffix}.bin");

            using var writer = new BinaryWriter(File.Open(filename, FileMode.Create, FileAccess.Write, FileShare.None));

            WriteFrameHeader(compressedFrame.Frame, writer);
            writer.Write(compressedFrame.Data);
            WriteMetadataRow(compressedFrame.Frame, compressedFrame.Index);
        });
    }

    private async Task SingleWriterLoop(CancellationToken cancellationToken)
    {
        await RunPipeline(cancellationToken, compressedFrame =>
        {
            singleFileWriter!.Write(compressedFrame.Data);
            WriteMetadataRow(compressedFrame.Frame, compressedFrame.Index);
        });
    }

    private async Task RunPipeline(CancellationToken cancellationToken, Action<CompressedFrame> saveFrame)
    {
        Channel<IndexedFrame> input = Channel.CreateBounded<IndexedFrame>(FixedArraySize);
        Channel<CompressedFrame> output = Channel.CreateBounded<CompressedFrame>(FixedArraySize);
        using var availableSlots = new SemaphoreSlim(FixedArraySize, FixedArraySize);

        Task producer = ProduceFrames(input.Writer, availableSlots, cancellationToken);
        Task[] workers = Enumerable.Range(0, WorkerCount)
            .Select(_ => CompressFrames(input.Reader, output.Writer))
            .ToArray();

        _ = CompleteOutputWhenWorkersFinish(workers, output.Writer);

        var fixedFrames = new CompressedFrame?[FixedArraySize];
        int nextIndex = 0;

        await foreach (CompressedFrame compressedFrame in output.Reader.ReadAllAsync())
        {
            fixedFrames[compressedFrame.Index % FixedArraySize] = compressedFrame;

            while (fixedFrames[nextIndex % FixedArraySize] is { } readyFrame && readyFrame.Index == nextIndex)
            {
                saveFrame(readyFrame);
                fixedFrames[nextIndex % FixedArraySize] = null;
                availableSlots.Release();
                nextIndex++;
            }
        }

        await producer;
        await Task.WhenAll(workers);
    }

    private async Task ProduceFrames(
        ChannelWriter<IndexedFrame> writer,
        SemaphoreSlim availableSlots,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (FrameRecord frame in sensor.Reader().ReadAllAsync(cancellationToken))
            {
                await availableSlots.WaitAsync(cancellationToken);
                int index = frameIndex++;

                try
                {
                    await writer.WriteAsync(new IndexedFrame(index, frame), cancellationToken);
                }
                catch
                {
                    availableSlots.Release();
                    throw;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private async Task CompressFrames(ChannelReader<IndexedFrame> reader, ChannelWriter<CompressedFrame> writer)
    {
        await foreach (IndexedFrame indexedFrame in reader.ReadAllAsync())
        {
            byte[] compressedData = CompressToRle(indexedFrame.Frame);
            await writer.WriteAsync(new CompressedFrame(indexedFrame.Index, indexedFrame.Frame, compressedData));
        }
    }

    private static async Task CompleteOutputWhenWorkersFinish(
        Task[] workers,
        ChannelWriter<CompressedFrame> writer)
    {
        try
        {
            await Task.WhenAll(workers);
            writer.TryComplete();
        }
        catch (Exception exception)
        {
            writer.TryComplete(exception);
        }
    }

    private byte[] CompressToRle(FrameRecord frame)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            if (settings.dataType == SaveDataType.RLE)
            {
                WriteRleFrame(frame, writer);
            }
            else
            {
                WriteRecordedFrame(frame, writer);
            }
        }

        return stream.ToArray();
    }

    private void WriteRleFrame(FrameRecord frame, BinaryWriter writer)
    {
        (int sensorWidth, int sensorHeight) = sensor.Dimensions();
        if (frame.data.Length != sensorWidth * sensorHeight) return;

        float[] values = settings.recordROIOnly
            ? sensor.ROI().indexes.Select(index => frame.data[index]).ToArray()
            : frame.data;

        foreach (RunLengthPair pair in DataConverter.IntToRLE(DataConverter.FloatToInt(values)))
        {
            writer.Write(pair.value);
            writer.Write(pair.run);
        }
    }

    private void WriteRecordedFrame(FrameRecord frame, BinaryWriter writer)
    {
        (int sensorWidth, int sensorHeight) = sensor.Dimensions();
        if (frame.data.Length != sensorWidth * sensorHeight) return;

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
        // frame.metadata.WriteHeader(new BinaryWriter(metadataWriter!.BaseStream));
    }

    private void WriteMetadataRow(FrameRecord frame, int index)
    {
        (float min, float max, float mean) = PlaybackTool.CalculateStatistics(frame.data);

        frame.metadata.WriteMetadata(metadataWriter!, index, min, max, mean);

        metadataWriter!.Flush();
    }
}