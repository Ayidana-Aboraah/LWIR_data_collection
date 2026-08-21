using System.IO;
using ThermalCamerApp.Camera;
using ThermalCamerApp.Camera.LWIR;
using Tommy;

namespace ThermalCamerApp;

public static class SensorManager
{
    public static string ProjectName = "";
    public static SensorBase current_sensor;
    
    public static void ImportProjectConfig(string[] args)
    {
        if (args.Length < 1) return;
        
        ProjectName = args[0];

        var config = TOML.Parse(new StringReader(args[1]));

        switch (config["sensorType"].AsString.ToString())
        {
            case "LWIR": IRImagerShow.init();
            current_sensor = new IRImagerShow();
            break;
            case "NIR":
            break;
        }
    }
}