using Optris.OtcSdk;
using ThermalCamerApp.Camera;
using ThermalCamerApp.Camera.LWIR;
using ThermalCamerApp.Camera.NIR;

namespace ThermalCamerApp;

public static class SensorManager
{
    public static string ProjectName = "";
    public static string SensorName = "";
    public static bool OperatorMode = false;
    public static SensorBase current_sensor;

    public static void ImportProjectConfig(string[] args)
    {
        if (args.Length == 0)
        {
            // TODO: Load Temp/dummy Sensor
            // TODO: Put into Developer Mode
            // current_sensor = new DummySensor();
            current_sensor = new IRImagerShow();
            return;
        }

        OperatorMode = true;

        ProjectName = args[0];
        SensorName = args[1];

        switch (SensorName)
        {
            case "LWIR":
                current_sensor = new IRImagerShow();
                break;
            case "NIR":
                current_sensor = new NIR();
                break;
        }
    }

    public static void SetSensor(int i)
    {
        switch (i) {
            case 0: current_sensor = new IRImagerShow();
                break;
            case 1: current_sensor = new NIR();
                break;
        }
    }

    public static bool[] ActiveSensors()
    {
        return [
            EnumerationManager.getInstance().getDetectedDevices().Count > 0,
            Basler.Pylon.CameraFinder.Enumerate().Count > 0
        ];
    }

    public static void DebugImport(string sensor)
    {
        switch (sensor)
        {
            case "LWIR":
                current_sensor = new IRImagerShow();
                break;
            case "NIR":
                current_sensor = new NIR();
                break;
        }
    }
}