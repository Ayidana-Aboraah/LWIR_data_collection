using System.ComponentModel.DataAnnotations;
using LWIR_app.classes;

namespace LWIR_app.Sensor;

public class RecorderBase
{
    [Required]
    public SensorBase sensor;
    protected RecorderSettings settings;
    public bool recording = false;
    public bool hasROI = false;

    public RecorderBase(SensorBase sensor) => this.sensor = sensor;

    public void Bind(SensorBase sensor) => this.sensor = sensor;

    public virtual bool Connected() => sensor.Connected();

    public virtual void Start(RecorderSettings settings)
    {
        if (recording) return;
        this.settings = settings;
        sensor.saveType(settings.dataType);
        recording = true;
        sensor.IsRecording(recording);
    }

    public virtual void Stop() {
        recording = false;
        sensor.IsRecording(recording);
    }
}