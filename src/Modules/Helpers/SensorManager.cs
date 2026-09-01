using ThermalCamerApp.Camera;
using ThermalCamerApp.Camera.LWIR;
using ThermalCamerApp.Camera.NIR;

namespace ThermalCamerApp;

public static class SensorManager
{
    public static string ProjectName = "";

    public static string SensorName = "";
    public static SensorBase current_sensor;

    public static void ImportProjectConfig(string[] args)
    {
        if (args.Length == 0)
        {
            // TODO: Load Temp/dummy Sensor
            // TODO: Put into Developer Mode
            current_sensor = new IRImagerShow();
            return;
        }

        ProjectName = args[0];
        SensorName = args[1];

        switch (SensorName)
        {
            case "LWIR": current_sensor = new IRImagerShow();
                break;
            case "NIR": current_sensor = new NIR();
                break;
        }
    }

    public static void DebugImport(string sensor)
    {
        switch (sensor)
        {
            case "LWIR": current_sensor = new IRImagerShow();
                break;
            case "NIR": current_sensor = new NIR();
                break;
        }
    }
}