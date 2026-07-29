
using System.Windows;

namespace LWIR_app.classes
{
//     public class ThermalAnalyser
//     {
//         ThermalRecorder recorder;

//         RecordedFrame[] frameset;

//         RegionOfInterest roi;

//         public ThermalAnalyser(ThermalRecorder recorder)
//         {
//             this.recorder = recorder;
//         }

//         public void Update()
//         {
//             var frameset = recorder.ReadFrames();

//             if (frameset == null) return; // TODO: Don't draw

//             uint[] indexes;

//             float[][] temps = new float[frameset.Length][];

//             // foreach (RecordedFrame frame in frameset)
//             for (int i = 0; i < frameset.Length; i++)
//             {
//                 indexes = roi.indexes((uint)frameset[i].width);

//                 for (int x = 0; i < indexes.Length; i++) temps[i][x] = frameset[x].temperatures[x];

//                 // TODO: Generate statistics
//             }

//             // TODO: Average Statistics
//             // TODO: Update Plot (temp against frameidx)
//             // TODO: Update Statistics Text
//         }

//         public void GenerateFrameStatistics(float[][] temperatures_set, uint set_size)
//         {
//             float fps = 60; // S0, 60 frames to get 1 sec
//             float[][] averages = new float[temperatures_set.Length][];
//             for (int i = 0; i < temperatures_set[0].Length; i++)
//             {
//                 // for (InitializingNewItemEventArgs)
//             }
//             // TODO: 
//             // TODO: RMS based on set size
//             // TODO: Compare by setsize
//         }
//     }

//     public struct ThermalStatistics
//     {
//         double thermal_derivative;
//         double mean_temperature;
//     }


    public class RegionOfInterest
    {
        public int[] indexes;
        public int width, height;
        public bool active;

        public RegionOfInterest(Point start, Point end, int frameWidth)
        {
            height  = (int) Math.Floor(start.Y - end.Y);
            width   = (int) Math.Floor(start.X - end.X);
            indexes = new int[width * height];
            int count = 0;

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    indexes[count++] = ((((int)Math.Floor(start.Y)) + y) * frameWidth) + ((int)Math.Floor(start.X)) + x;

            active = true;
        }
    }
}