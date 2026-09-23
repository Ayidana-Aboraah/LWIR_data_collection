using Tommy;
using System.IO;
using Optris.OtcSdk;
using SensorInterface.classes;
using SensorInterface.Sensor;

namespace SensorInterface;

public static class SensorManager
{
    public static List<UniversalRecorder> sensors = [];
    public static bool OperatorMode = false;
    public static string ProjectName = "";
    public static string SensorName = "";

    public static void ImportSensors(string configFile)
    {
        foreach (TomlNode sensor in TOML.Parse(File.OpenText(configFile))["sensors"].AsArray)
        {
            TomlTable sensorTable = sensor.AsTable;
            SensorConfig config = new SensorConfig(sensorTable["Name"], Enum.Parse<SaveDataType>(sensorTable["SaveType"].AsString.ToString()), Enum.Parse<SensorType>(sensorTable["SensorType"].AsString.ToString()), sensorTable["Settings"].AsTable);
            SensorType s = sensorTable["SensorType"].AsString.ToString() switch
            {
                "Optris_LWIR" => SensorType.Optris_LWIR,
                "Basler_NIR" => SensorType.Basler_NIR,
                "Photodiode" => SensorType.Photodiode, // When using anything from the NI DAQ, setup the whole thing if it's not already, so that the sensor can just generate instances as it needs
                // _ => null,
            };

            sensors.Add(new UniversalRecorder(s, config));
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