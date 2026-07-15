using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using Microsoft.VisualBasic.FileIO;
using Optris.OtcSdk;

namespace LWIR_app.models
{
    public class ExcelPaletteImageBuilder : ImageBuilder
    {
        private List<PaletteStop> paletteStops = BuildDefaultPalette();
        public ExcelPaletteImageBuilder(ColorFormat colorFormat, WidthAlignment widthAlignment) : base(colorFormat, widthAlignment){}

        public string builtinPaletteDir = @"C:\Program Files\Optris\otcsdk\palettes";

        public string[] LoadDefaultPaletteNames()
        {
            DirectoryInfo dirInfo = new DirectoryInfo(builtinPaletteDir);
            FileInfo[] files =  dirInfo.GetFiles(".csv");
            string[] result = new string[files.Length];
            for (int i = 0; i < files.Length; i++) result[i] = files[i].Name;
            return result;
        }

        public void LoadPaletteFromExcel(string workbookPath)
        {
            if (!File.Exists(workbookPath)) throw new FileNotFoundException("Palette workbook not found.", workbookPath);

            List<PaletteStop> stops = ReadPaletteStopsFromWorkbook(workbookPath);
            if (stops.Count == 0) throw new InvalidDataException($"No palette stops were found in '{workbookPath}'.");

            paletteStops = NormalizePaletteStops(stops);
        }

        public void SetPaletteStops(IEnumerable<Color> palette)
        {
            Color[] colors = palette.ToArray();
            if (colors.Length == 0) throw new ArgumentException("Palette cannot be empty.", nameof(palette));

            if (colors.Length == 1)
            {
                paletteStops = new List<PaletteStop>
                {
                    new PaletteStop(0.0f, colors[0]),
                    new PaletteStop(1.0f, colors[0])
                };
                return;
            }

            paletteStops = colors
                .Select((color, index) => new PaletteStop(index / (float)(colors.Length - 1), color))
                .ToList();
        }

        public Color MapTemperatureToColor(float temperature, float scaleMin, float scaleMax)
        {
            if (paletteStops.Count == 0) return Color.Black;

            if (paletteStops.Count == 1) return paletteStops[0].Color;

            float normalized = (temperature - scaleMin) / (scaleMax - scaleMin);
            normalized = Math.Clamp(normalized, 0.0f, 1.0f);

            PaletteStop left = paletteStops[0];
            PaletteStop right = paletteStops[^1];

            if (normalized <= left.Position) return left.Color;

            if (normalized >= right.Position) return right.Color;

            for (int i = 0; i < paletteStops.Count - 1; i++)
            {
                left = paletteStops[i];
                right = paletteStops[i + 1];

                if (normalized > right.Position) continue;

                float span = right.Position - left.Position;
                float localT = span <= float.Epsilon ? 0.0f : (normalized - left.Position) / span;
                return Interpolate(left.Color, right.Color, localT);
            }

            return right.Color;
        }

        public static Color Interpolate(Color left, Color right, float t)
        {
            t = Math.Clamp(t, 0.0f, 1.0f);

            int r = (int)Math.Round(left.R + ((right.R - left.R) * t));
            int g = (int)Math.Round(left.G + ((right.G - left.G) * t));
            int b = (int)Math.Round(left.B + ((right.B - left.B) * t));

            return Color.FromArgb(255, r, g, b);
        }

        public static List<PaletteStop> BuildDefaultPalette()
        {
            return new List<PaletteStop>
            {
                new PaletteStop(0.00f, Color.FromArgb(0, 0, 0)),
                new PaletteStop(0.20f, Color.FromArgb(0, 0, 128)),
                new PaletteStop(0.40f, Color.FromArgb(0, 128, 255)),
                new PaletteStop(0.60f, Color.FromArgb(255, 192, 0)),
                new PaletteStop(0.80f, Color.FromArgb(255, 64, 0)),
                new PaletteStop(1.00f, Color.FromArgb(255, 255, 255))
            };
        }

        public static List<PaletteStop> NormalizePaletteStops(List<PaletteStop> stops)
        {
            var ordered = stops
                .Where(stop => !float.IsNaN(stop.Position) && !float.IsInfinity(stop.Position))
                .OrderBy(stop => stop.Position)
                .ToList();

            if (ordered.Count == 1)
            {
                ordered.Add(new PaletteStop(1.0f, ordered[0].Color));
                ordered[0] = new PaletteStop(0.0f, ordered[0].Color);
                return ordered;
            }

            if (ordered.Count > 1 && (ordered[0].Position < 0.0f || ordered[^1].Position > 1.0f))
            {
                float min = ordered[0].Position;
                float max = ordered[^1].Position;
                float range = max - min;

                if (range > float.Epsilon)
                {
                    ordered = ordered
                        .Select(stop => new PaletteStop((stop.Position - min) / range, stop.Color))
                        .ToList();
                }
            }

            if (ordered.Count > 1)
            {
                ordered[0] = new PaletteStop(0.0f, ordered[0].Color);
                ordered[^1] = new PaletteStop(1.0f, ordered[^1].Color);
            }

            return ordered;
        }

        public static List<PaletteStop> ReadPaletteStopsFromWorkbook(string workbookPath)
        {
            using ZipArchive archive = ZipFile.OpenRead(workbookPath);
            string worksheetPath = GetFirstWorksheetPath(archive);
            Dictionary<int, string> sharedStrings = ReadSharedStrings(archive);
            List<string?[]> rows = ReadWorksheetRows(archive, worksheetPath, sharedStrings);

            if (rows.Count == 0)
            {
                return new List<PaletteStop>();
            }

            int? positionColumn = null;
            int? redColumn = null;
            int? greenColumn = null;
            int? blueColumn = null;
            int? colorColumn = null;

            var header = rows[0];
            var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < header.Length; i++)
            {
                string? headerValue = header[i];
                if (string.IsNullOrWhiteSpace(headerValue)) continue;

                headerMap[headerValue.Trim()] = i;
            }

            bool hasHeader = ContainsKnownPaletteHeaders(headerMap);
            if (hasHeader)
            {
                positionColumn = FindHeaderIndex(headerMap, "Temperature", "Temp", "Value", "Position", "Index");
                redColumn = FindHeaderIndex(headerMap, "R", "Red");
                greenColumn = FindHeaderIndex(headerMap, "G", "Green");
                blueColumn = FindHeaderIndex(headerMap, "B", "Blue");
                colorColumn = FindHeaderIndex(headerMap, "Color", "Hex", "Palette");
            }

            var stops = new List<PaletteStop>();
            int startRow = hasHeader ? 1 : 0;

            for (int rowIndex = startRow; rowIndex < rows.Count; rowIndex++)
            {
                string?[] row = rows[rowIndex];
                if (row.Length == 0) continue;

                if (!TryReadPaletteRow(row, positionColumn, redColumn, greenColumn, blueColumn, colorColumn, rowIndex - startRow, out PaletteStop stop)) continue;

                stops.Add(stop);
            }

            if (!hasHeader && stops.Count > 1)
            {
                // When no explicit temperature column exists, distribute the rows evenly across the palette.
                for (int i = 0; i < stops.Count; i++)
                {
                    float position = stops.Count == 1 ? 0.0f : i / (float)(stops.Count - 1);
                    stops[i] = new PaletteStop(position, stops[i].Color);
                }
            }

            return stops;
        }

        public static bool TryReadPaletteRow(
            string?[] row,
            int? positionColumn,
            int? redColumn,
            int? greenColumn,
            int? blueColumn,
            int? colorColumn,
            int fallbackIndex,
            out PaletteStop stop)
        {
            stop = default;

            float position = fallbackIndex;
            if (positionColumn.HasValue && positionColumn.Value < row.Length &&
                TryParseFloat(row[positionColumn.Value], out float parsedPosition))
                position = parsedPosition;

            if (redColumn.HasValue && greenColumn.HasValue && blueColumn.HasValue)
            {
                if (TryParseByte(row, redColumn.Value, out byte red) &&
                    TryParseByte(row, greenColumn.Value, out byte green) &&
                    TryParseByte(row, blueColumn.Value, out byte blue))
                {
                    stop = new PaletteStop(position, Color.FromArgb(255, red, green, blue));
                    return true;
                }
            }

            if (colorColumn.HasValue && colorColumn.Value < row.Length &&
                TryParseColor(row[colorColumn.Value], out Color color))
            {
                stop = new PaletteStop(position, color);
                return true;
            }

            if (row.Length >= 4 &&
                TryParseByte(row, 1, out byte redValue) &&
                TryParseByte(row, 2, out byte greenValue) &&
                TryParseByte(row, 3, out byte blueValue))
            {
                if (row.Length > 0 && TryParseFloat(row[0], out float parsedFirst))
                    position = parsedFirst;

                stop = new PaletteStop(position, Color.FromArgb(255, redValue, greenValue, blueValue));
                return true;
            }

            if (row.Length >= 2 && TryParseColor(row[1], out Color parsedColor))
            {
                if (TryParseFloat(row[0], out float parsedFirst))
                    position = parsedFirst;

                stop = new PaletteStop(position, parsedColor);
                return true;
            }

            if (row.Length >= 1 && TryParseColor(row[0], out Color singleColumnColor))
            {
                stop = new PaletteStop(position, singleColumnColor);
                return true;
            }

            return false;
        }

        public static bool TryParseByte(string?[] row, int index, out byte value)
        {
            value = 0;
            return index < row.Length && byte.TryParse(row[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryParseFloat(string? value, out float parsed)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ||
                   float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed);
        }

        public static bool TryParseColor(string? value, out Color color)
        {
            color = Color.Empty;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string trimmed = value.Trim();

            if (trimmed.Contains(','))
            {
                string[] parts = trimmed.Split(',', ';');
                if (parts.Length >= 3 &&
                    byte.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte red) &&
                    byte.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte green) &&
                    byte.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte blue))
                {
                    color = Color.FromArgb(255, red, green, blue);
                    return true;
                }
            }

            try
            {
                color = ColorTranslator.FromHtml(trimmed.StartsWith('#') ? trimmed : $"#{trimmed}");
                return true;
            }
            catch
            {
            }

            try
            {
                color = Color.FromName(trimmed);
                return color.A != 0 || color.IsKnownColor || color.IsNamedColor;
            }
            catch
            {
                return false;
            }
        }

        public static List<string?[]> ReadWorksheetRows(ZipArchive archive, string worksheetPath, Dictionary<int, string> sharedStrings)
        {
            ZipArchiveEntry? worksheetEntry = archive.GetEntry(worksheetPath);
            if (worksheetEntry == null) return new List<string?[]>();

            using Stream stream = worksheetEntry.Open();
            XDocument document = XDocument.Load(stream);

            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var rows = new List<string?[]>();

            foreach (XElement row in document.Descendants(ns + "row"))
            {
                var cells = new SortedDictionary<int, string?>();

                foreach (XElement cell in row.Elements(ns + "c"))
                {
                    int columnIndex = GetColumnIndex(cell.Attribute("r")?.Value);
                    cells[columnIndex] = ReadCellValue(cell, ns, sharedStrings);
                }

                if (cells.Count == 0) continue;

                int maxIndex = cells.Keys.Max();
                var values = new string?[maxIndex + 1];
                foreach (KeyValuePair<int, string?> cell in cells) values[cell.Key] = cell.Value;

                rows.Add(values);
            }

            return rows;
        }

        public static string ReadCellValue(XElement cell, XNamespace ns, Dictionary<int, string> sharedStrings)
        {
            string cellType = cell.Attribute("t")?.Value ?? string.Empty;

            if (cellType.Equals("inlineStr", StringComparison.OrdinalIgnoreCase)) return cell.Element(ns + "is")?.Element(ns + "t")?.Value ?? string.Empty;

            string rawValue = cell.Element(ns + "v")?.Value ?? string.Empty;
            if (cellType.Equals("s", StringComparison.OrdinalIgnoreCase) && int.TryParse(rawValue, out int sharedIndex) &&
                sharedStrings.TryGetValue(sharedIndex, out string? sharedValue))
                return sharedValue;

            return rawValue;
        }

        public static Dictionary<int, string> ReadSharedStrings(ZipArchive archive)
        {
            var sharedStrings = new Dictionary<int, string>();
            ZipArchiveEntry? entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null) return sharedStrings;

            using Stream stream = entry.Open();
            XDocument document = XDocument.Load(stream);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

            int index = 0;
            foreach (XElement stringItem in document.Descendants(ns + "si")) sharedStrings[index++] = stringItem.Value;

            return sharedStrings;
        }

        public static string GetFirstWorksheetPath(ZipArchive archive)
        {
            ZipArchiveEntry? workbookEntry = archive.GetEntry("xl/workbook.xml");
            ZipArchiveEntry? relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");

            if (workbookEntry == null || relsEntry == null) return "xl/worksheets/sheet1.xml";

            XNamespace workbookNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace relNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XNamespace packageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

            using Stream workbookStream = workbookEntry.Open();
            using Stream relsStream = relsEntry.Open();

            XDocument workbookDoc = XDocument.Load(workbookStream);
            XDocument relsDoc = XDocument.Load(relsStream);

            string? relId = workbookDoc.Descendants(workbookNs + "sheet").FirstOrDefault()?.Attribute(relNs + "id")?.Value;
            if (string.IsNullOrWhiteSpace(relId)) return "xl/worksheets/sheet1.xml";

            string? target = relsDoc
                .Descendants(packageRelNs + "Relationship")
                .FirstOrDefault(item => string.Equals(item.Attribute("Id")?.Value, relId, StringComparison.Ordinal))
                ?.Attribute("Target")?.Value;

            if (string.IsNullOrWhiteSpace(target)) return "xl/worksheets/sheet1.xml";

            target = target.Replace('\\', '/');
            if (!target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)) target = $"xl/{target.TrimStart('/')}";

            return target;
        }

        public static int GetColumnIndex(string? cellReference)
        {
            if (string.IsNullOrWhiteSpace(cellReference)) return 0;

            int index = 0;
            foreach (char character in cellReference)
            {
                if (!char.IsLetter(character)) break;

                index = (index * 26) + (char.ToUpperInvariant(character) - 'A' + 1);
            }

            return Math.Max(0, index - 1);
        }

        public static int? FindHeaderIndex(Dictionary<string, int> headers, params string[] names)
        {
            foreach (string name in names)
            {
                if (headers.TryGetValue(name, out int index)) return index;
            }

            return null;
        }

        public static bool ContainsKnownPaletteHeaders(Dictionary<string, int> headers)
        {
            return FindHeaderIndex(headers, "Temperature", "Temp", "Value", "Position", "Index") != null ||
                   FindHeaderIndex(headers, "R", "Red") != null ||
                   FindHeaderIndex(headers, "G", "Green") != null ||
                   FindHeaderIndex(headers, "B", "Blue") != null ||
                   FindHeaderIndex(headers, "Color", "Hex", "Palette") != null;
        }

        public readonly record struct PaletteStop(float Position, Color Color); 
    }
}