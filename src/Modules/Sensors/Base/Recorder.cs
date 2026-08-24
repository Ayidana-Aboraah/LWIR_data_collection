using System.ComponentModel.DataAnnotations;
using ThermalCamerApp.classes;

namespace ThermalCamerApp.Camera;

public class RecorderBase
{
    [Required]
    public SensorBase sensor;
    protected RecorderSettings settings;
    public bool recording = false;
    public bool hasROI() => sensor.ROI().HasROI();

    public RecorderBase(SensorBase sensor) => this.sensor = sensor;

    public void Bind(SensorBase sensor) => this.sensor = sensor;

    public virtual bool Connected() => sensor.Connected();

    public virtual void Start(RecorderSettings settings)
    {
        if (recording) return;
        this.settings = settings;
        recording = true;
        sensor.IsRecording(recording);
    }

    public virtual void Stop() {
        recording = false;
        sensor.IsRecording(recording);
    }
}