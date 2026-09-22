using Tommy;
using System.IO;
using Optris.OtcSdk;
using SensorInterface.classes;
using SensorInterface.Sensor;
using SensorInterface.Sensor.LWIR;
using SensorInterface.Sensor.NIR;

namespace SensorInterface;

public class SensorSet
{
    SensorBase sensor;
    public UniversalRecorder recorder;

    public SensorSet(SensorBase sensor, UniversalRecorder recorder)
    {
        this.sensor = sensor;
        this.recorder = recorder;
    }
}

public static class SensorManager
{
    public static string ProjectName = "";
    public static string SensorName = "";
    public static bool OperatorMode = false;
    public static List<SensorSet> sensors;

    public static void ImportSensors(string configFile)
    {
        foreach (TomlNode sensor in TOML.Parse(File.OpenText(configFile))["sensors"].AsArray)
        {
            TomlTable sensorTable = sensor.AsTable;
            SensorConfig config = new SensorConfig(sensorTable["Name"], Enum.Parse<SaveDataType>(sensorTable["SaveType"].AsString.ToString()), Enum.Parse<SensorType>(sensorTable["SensorType"].AsString.ToString()), sensorTable["Settings"].AsTable);
            SensorBase s =  (sensorTable["SensorType"].AsString.ToString()) switch
            {
                "Optris_LWIR" => (new IRImagerShow()),
                "Basler_NIR" => (new BaslerNIR()),
                // "Photodiode" => , // When using anything from the NI DAQ, setup the whole thing if it's not already, so that the sensor can just generate instances as it needs
                // _ => null,
            };
            var recorder = new UniversalRecorder(s);
            sensors.Add(new SensorSet(s, recorder));
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