
using System.Windows;

namespace ThermalCamerApp.classes
{
    public class RegionOfInterest
    {
        public int[] indexes = [];
        public int width = 0, height = 0;
        public bool active = false;

        public RegionOfInterest() => Clear();

        public RegionOfInterest(Point start, Point end, int frameWidth) => Update(start,end,frameWidth);

        public (float,float,float) Statistics(float[] temperatures){
            List<float> temps = new List<float>();
            for(int i = 0; i < indexes.Length; i++)temps.Add(temperatures[indexes[i]]);
            return ThermalAnalyser.CalculateStatistics(temps.ToArray());
        }


        public void Update(Point start, Point end, int frameWidth)
        {
            height  = (int) Math.Floor(end.Y - start.Y) + 1;
            width   = (int) Math.Floor(end.X - start.X) + 1;
            indexes = new int[width * height];
            int count = 0;

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    indexes[count++] = ((((int)Math.Floor(start.Y)) + y) * frameWidth) + ((int)Math.Floor(start.X)) + x;

            active = true;
        }

        public bool HasROI() => indexes.Length > 0;

        public void Clear()
        {
            indexes = [];
            active = false;
            (width, height) = (0, 0);
        }

        public (int, int) Dimensions() => (width, height);
    }
}