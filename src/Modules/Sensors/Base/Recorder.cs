using System.ComponentModel.DataAnnotations;
using LWIR_app.classes;

namespace LWIR_app.Sensor;

public class RecorderBase
{
    [Required]
    public SensorBase sensor;
    public bool recording = false;
    public bool hasROI = false;

    public RecorderBase(SensorBase sensor)
    {
        this.sensor = sensor;
    }

    public void Bind(SensorBase sensor)
    {
        this.sensor = sensor;
    }

    public bool Connected() => sensor.Connected();

    public void Start(string directory, RecorderSettings settings){}

    public void Stop(){
    }
}