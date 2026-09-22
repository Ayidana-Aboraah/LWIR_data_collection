using SensorInterface.Sensor.LWIR;
using SensorInterface.Sensor.NIR;

namespace SensorInterface.Sensor;

public class SesnorSubprocess
{
    SensorBase sensor;
    public SesnorSubprocess(SensorType mType)
    {
        sensor = mType switch
        {
            SensorType.Optris_LWIR => new IRImagerShow(),
            SensorType.Basler_NIR => new BaslerNIR(),
            // SensorType.Photodiode => new DAQChannel(),
        };
    }
}