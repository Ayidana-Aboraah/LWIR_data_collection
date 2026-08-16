
using System.Windows;

namespace LWIR_app.classes
{
    public class RegionOfInterest
    {
        public int[] indexes;
        public int width, height;
        public bool active;

        public RegionOfInterest() => indexes = [];

        public RegionOfInterest(Point start, Point end, int frameWidth)
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

        public (int, int) Dimensions() => (width, height);
    }
}