using SensorInterface.classes;
using System.IO;
using RLE;

namespace SensorInterface.Sensor;

public class UniversalRecorder
{
    StreamWriter? metadataWriter;
    BinaryWriter? singleFileWriter;
    protected RecorderSettings settings;
    string sessionDirectory = "";
    string frameDirectory = "";
    int frameIndex = 0;
    public SensorService sensor;
    public SensorType sensorType;
    public bool recording = false;
    // public bool hasROI() => sensor.ROI().HasROI();

    public UniversalRecorder(SensorType sensorType) => this.sensorType = sensorType;

    public void Start(RecorderSettings settings)
    {
        if (recording) return;
        recording = true;
        this.settings = settings;
        // sensor.IsRecording(recording);

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
            string filename = Path.Combine(sessionDirectory, $"frame_{settings.dataType.ToString() + (settings.recordROIOnly ? "_ROI" : "")}.bin");

            singleFileWriter = new BinaryWriter( File.Open(filename, FileMode.Create, FileAccess.Write, FileShare.None));

            WriteBinHeader(singleFileWriter);

            Task.Run(SingleWriterLoop);
        }
        else
        {
            frameDirectory = Path.Combine(sessionDirectory, "frames");
            Directory.CreateDirectory(frameDirectory);
            Task.Run(WriterLoop);
        }
    }

    public virtual bool Connected() => sensor.Connected();

    public void Stop()
    {
        if (!recording) return;

        metadataWriter?.Flush();
        metadataWriter?.Close();
        if (settings.singleBinary) singleFileWriter?.Close();

        recording = false;
        sensor.Activeate(recording);
    }

    private async Task WriterLoop()
    {
        await foreach (var frame in sensor.Reader().ReadAllAsync())
        {
            string filename = Path.Combine(frameDirectory, $"frame_{frameIndex}_{settings.dataType.ToString() + (settings.recordROIOnly ? "_ROI" : "")}.bin");

            using var writer = new BinaryWriter(File.Open(filename, FileMode.Create, FileAccess.Write, FileShare.None));

            WriteFrameHeader(frame, writer);
            WriteFrame(frame, writer);
        }
    }

    private async Task SingleWriterLoop()
    {
        await foreach (var frame in sensor.Reader().ReadAllAsync()) WriteFrame(frame, singleFileWriter!);
    }

    private void WriteFrame(FrameRecord frame, BinaryWriter writer)
    {
        WriteRecordedFrame(frame, writer);
        WriteMetadataRow(frame);
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
                    ushort[] iValue = DataConverter.FloatToInt(frame.data);
                    foreach (int ROI_Idx in sensor.ROI().indexes) writer.Write(iValue[ROI_Idx]);
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
                    foreach (ushort value in DataConverter.FloatToInt(frame.data)) writer.Write(value);
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
        frame.metadata.WriteHeader(writer);
    }

    private void WriteMetadataRow(FrameRecord frame)
    {
        (float min, float max, float mean) = ThermalAnalyser.CalculateStatistics(frame.data);
        frame.metadata.WriteMetadata(metadataWriter!, frameIndex, min, max, mean);
        metadataWriter!.Flush();
    }
}