using System.Windows;

namespace ThermalCamerApp.classes;

public class ThermalAnalyser
{
    public static (float Min, float Max, float Mean) CalculateStatistics(float[] temperatures)
    {
        (float min, float max) = (float.MaxValue, float.MinValue);
        double sum = 0;

        foreach (float temperature in temperatures)
        {
            min = (temperature < min) ? temperature : min;
            max = (temperature > max) ? temperature : max;
            sum += temperature;
        }

        return (min, max, (float)(sum / temperatures.Length));
    }

    public static float FindRegionTemp(float[] temperatures, int x, int y, int width ,int height)
    {
        (_, _, var mean) = new RegionOfInterest(new Point(x, y), new Point(x+width, y+height), width).Statistics(temperatures);
        return mean;    
    }
}

public struct ThermalStatistics
{
    double thermal_derivative;
    double mean_temperature;
}