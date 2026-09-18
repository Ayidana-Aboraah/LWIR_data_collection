using Tommy;
using System.IO;
using Optris.OtcSdk;
using SensorInterface.classes;
using SensorInterface.Sensor;
using SensorInterface.Sensor.LWIR;
using SensorInterface.Sensor.NIR;

namespace SensorInterface;

public static class SensorManager
{
    public static string ProjectName = "";
    public static string SensorName = "";
    public static bool OperatorMode = false;
    public static List<SensorBase> sensors;

    public static void ImportSensors(string configFile)
    {
        foreach (TomlNode sensor in TOML.Parse(File.OpenText(configFile))["sensors"].AsArray)
        {
            TomlTable sensorTable = sensor.AsTable;
            SensorConfig config = new SensorConfig(sensorTable["Name"], Enum.Parse<SaveDataType>(sensorTable["SaveType"].AsString.ToString()), Enum.Parse<SensorType>(sensorTable["SensorType"].AsString.ToString()), sensorTable["Settings"].AsTable);
            sensors.Add( (sensorTable["SensorType"].AsString.ToString()) switch
            {
                "Optris_LWIR" => (new IRImagerShow()),
                "Basler_NIR" => (new NIR()),
                // "Photodiode" => ,
            });
        }
    }

    public static bool[] ActiveSensors()
    {
        return [
            EnumerationManager.getInstance().getDetectedDevices().Count > 0,
            Basler.Pylon.CameraFinder.Enumerate().Count > 0
        ];
    }
}