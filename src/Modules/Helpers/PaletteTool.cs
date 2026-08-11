using System.IO;
using System.Windows.Media;
using Microsoft.VisualBasic.FileIO;

namespace LWIR_app.models
{
    public static class PaletteTool
    {
        public static string builtinPaletteDir = @"C:\Program Files\Optris\otcsdk\palettes";
        private static Dictionary<string, Color[]> palettes = [];
        public static Color[] current_palette = [];
        public static void Init()
        {
            palettes = LoadDefaultPalettes();
            palettes.TryGetValue("Iron", out current_palette);
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
    }
}