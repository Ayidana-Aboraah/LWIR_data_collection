using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media;
using Microsoft.VisualBasic.FileIO;
using Color = System.Windows.Media.Color;

namespace ThermalCamerApp.models
{
    public static class PaletteTool
    {
        public static string builtinPaletteDir = @"C:\Program Files\Optris\otcsdk\palettes";
        private static Dictionary<string, Color[]> palettes = [];
        public static Color[] current_palette = [];
        public static void Init()
        {
            SetPalette("Iron");
        }

        public static void SetPalette(string palette)
        {
            if (palettes.Count < 1) palettes = LoadDefaultPalettes();
            palettes.TryGetValue(palette, out current_palette!);
        }

        public static string[] LoadDefaultPaletteNames()
        {
            DirectoryInfo dirInfo = new DirectoryInfo(builtinPaletteDir);
            FileInfo[] files = dirInfo.GetFiles();
            string[] result = new string[files.Length];
            for (int i = 0; i < files.Length; i++) result[i] = Path.GetFileNameWithoutExtension(files[i].Name);
            return result;
        }

        public static Dictionary<string, Color[]> LoadDefaultPalettes()
        {
            string[] names = LoadDefaultPaletteNames();
            Dictionary<string, Color[]> colors = new Dictionary<string, Color[]>();
            foreach (string name in names) colors[name] = ParsePaletteCSV(builtinPaletteDir + "\\" + name + ".csv");
            return colors;
        }

        public static Color[] ParsePaletteCSV(string path)
        {
            TextFieldParser parser = new TextFieldParser(path);
            parser.SetDelimiters(",");

            List<Color> colors = new List<Color>();

            while (!parser.EndOfData)
            {
                string[] fields = parser.ReadFields()!;
                byte[] pixel = new byte[3];
                for (int i = 0; i < fields!.Length; i++) pixel[i] = byte.Parse(fields[i]);
                colors.Add(Color.FromArgb(255, pixel[0], pixel[1], pixel[2]));
            }
            return colors.ToArray();
        }

        public static (byte R, byte G, byte B) MapTemperatureToColor(float temperature, float scaleMin, float scaleMax)
        {
            if (palettes == null || palettes.Count == 0)
            {
                palettes = LoadDefaultPalettes();
                palettes.TryGetValue("Iron", out current_palette!);
            }

            float normalised = (temperature - scaleMin) / (scaleMax - scaleMin);
            normalised = Math.Clamp(normalised, 0.0f, 1.0f);
            int len = current_palette!.Length;

            int i = Math.Clamp((int)Math.Floor(normalised * current_palette.Count()), 0, len - 1);

            var p = current_palette[i];
            return (p.R, p.G, p.B);
        }

        public static Bitmap Render(float[] temperatures, float min, float max, int width, int height)
        {
            if (temperatures == null || temperatures.Length == 0) throw new InvalidDataException("The playback frame does not contain any temperature data.");

            if (Math.Abs(max - min) < float.Epsilon) max = min + 0.0001f;

            return RenderBitmap(width, height, temperatures, min, max);
        }

        private static Bitmap RenderBitmap(int width, int height, float[] temperatures, float scaleMin, float scaleMax)
        {
            if (width <= 0 || height <= 0) throw new InvalidDataException("Cannot render a frame with empty dimensions.");

            if (temperatures.Length != checked(width * height)) throw new InvalidDataException("Temperature data length does not match the frame dimensions.");

            Bitmap bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            // WriteableBitmap wb = new WriteableBitmap(width, height, 70, 40, System.Windows.Media.PixelFormats.Rgb24, new BitmapPalette(imageBuilder.current_palette));
            Rectangle rectangle = new Rectangle(0, 0, width, height);
            // Int32Rect rect = new Int32Rect(0,0, width, height);
            BitmapData bitmapData = bitmap.LockBits(rectangle, ImageLockMode.WriteOnly, bitmap.PixelFormat);
            try
            {
                int stride = bitmapData.Stride;
                // int stride = wb.BackBufferStride;
                byte[] pixels = new byte[stride * height];

                for (int y = 0; y < height; y++)
                {
                    int sourceRowOffset = y * width;
                    int destinationRowOffset = y * stride;

                    for (int x = 0; x < width; x++)
                    {
                        (byte R, byte G, byte B) = PaletteTool.MapTemperatureToColor(temperatures[sourceRowOffset + x], scaleMin, scaleMax);
                        int pixelOffset = destinationRowOffset + (x * 3);

                        pixels[pixelOffset] = B;
                        pixels[pixelOffset + 1] = G;
                        pixels[pixelOffset + 2] = R;
                    }
                }

                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bitmapData.Scan0, pixels.Length);
                // wb.WritePixels(rect,pixels, stride, 0);
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            return bitmap;
        }

    }
}