using SensorInterface.classes;
using Tommy;

namespace SensorInterface.Sensor;

public enum SensorType
{
    Optris_LWIR,
    Basler_NIR,
    Photodiode,
}

public class SensorConfig
{
    public string name;
    public SaveDataType saveType;
    public SensorType sensorType;
    public TomlTable sensorSettings;

    public SensorConfig(string name, SaveDataType saveType, SensorType sensorType, TomlTable sensorSettings)
    {
        this.name = name;
        this.saveType = saveType;
        this.sensorType = sensorType;
        this.sensorSettings = sensorSettings;
    }
}