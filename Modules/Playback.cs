using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using Optris.OtcSdk;

namespace LWIR_app.classes
{
    public static class BinaryLoader
    {
        public static RecordedFrame[] LoadFromBinary(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Binary file not found.", path);

            SaveDataType saveType = InferSaveType(path);

            using FileStream stream = File.OpenRead(path);
            using BinaryReader reader = new BinaryReader(stream);

            int width = reader.ReadInt32();
            int height = reader.ReadInt32();

            if (width <= 0 || height <= 0) throw new InvalidDataException($"Invalid frame dimensions in '{path}'.");

            var frames = new List<RecordedFrame>();
            while (stream.Position < stream.Length)
            {
                frames.Add(ReadFramePayload(reader, width, height, saveType, new FrameMetadata()));
            }

            return frames.ToArray();
        }

        public static RecordedFrame[] LoadFrameSet(string path)
        {
            if (File.Exists(path))
            {
                return IsPerFrameBinary(path) ? LoadRecordedFrameBinary(path) : LoadFromBinary(path);
            }

            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException($"Playback path not found: {path}");
            }

            string[] binaryFiles = Directory
                .EnumerateFiles(path, "*.bin", SearchOption.AllDirectories)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (binaryFiles.Length == 0)
            {
                return Array.Empty<RecordedFrame>();
            }

            var frames = new List<RecordedFrame>();
            foreach (string binaryFile in binaryFiles)
            {
                frames.AddRange(IsPerFrameBinary(binaryFile)
                    ? LoadRecordedFrameBinary(binaryFile)
                    : LoadFromBinary(binaryFile));
            }

            return frames.ToArray();
        }

        private static RecordedFrame[] LoadRecordedFrameBinary(string path)
        {
            SaveDataType saveType = InferSaveType(path);

            using FileStream stream = File.OpenRead(path);
            using BinaryReader reader = new BinaryReader(stream);

            int width = reader.ReadInt32();
            int height = reader.ReadInt32();
            long timestamp = reader.ReadInt64();
            uint counter = reader.ReadUInt32();
            uint hardwareCounter = reader.ReadUInt32();

            if (width <= 0 || height <= 0)
            {
                throw new InvalidDataException($"Invalid frame dimensions in '{path}'.");
            }

            float[] temperatures = ReadFrameTemperatures(reader, width, height, saveType);
            FrameMetadata metadata = new FrameMetadata();

            // The current SWIG wrapper exposes read access to metadata but not direct setters for all fields.
            // Keep the deserialized frame content while preserving a valid metadata object for playback.
            _ = timestamp;
            _ = counter;
            _ = hardwareCounter;

            return new[] { new RecordedFrame(width, height, temperatures, metadata, saveType) };
        }

        private static RecordedFrame ReadFramePayload(BinaryReader reader, int width, int height, SaveDataType saveType, FrameMetadata metadata)
        {
            float[] temperatures = ReadFrameTemperatures(reader, width, height, saveType);
            return new RecordedFrame(width, height, temperatures, metadata, saveType);
        }

        private static float[] ReadFrameTemperatures(BinaryReader reader, int width, int height, SaveDataType saveType)
        {
            int pixelCount = checked(width * height);

            return saveType switch
            {
                SaveDataType.Float => ReadFloatTemperatures(reader, pixelCount),
                SaveDataType.U16 => ReadU16Temperatures(reader, pixelCount),
                SaveDataType.RLE => ReadRleTemperatures(reader, pixelCount),
                SaveDataType.All => ReadFloatTemperatures(reader, pixelCount),
                _ => ReadFloatTemperatures(reader, pixelCount)
            };
        }

        private static float[] ReadFloatTemperatures(BinaryReader reader, int pixelCount)
        {
            var temperatures = new float[pixelCount];
            for (int i = 0; i < pixelCount; i++)
            {
                temperatures[i] = reader.ReadSingle();
            }

            return temperatures;
        }

        private static float[] ReadU16Temperatures(BinaryReader reader, int pixelCount)
        {
            var temperatures = new float[pixelCount];
            for (int i = 0; i < pixelCount; i++)
            {
                temperatures[i] = reader.ReadUInt16() / 100.0f;
            }

            return temperatures;
        }

        private static float[] ReadRleTemperatures(BinaryReader reader, int pixelCount)
        {
            var temperatures = new List<float>(pixelCount);

            while (temperatures.Count < pixelCount)
            {
                ushort value = reader.ReadUInt16();
                uint length = reader.ReadUInt32();

                if (length == 0)
                {
                    throw new InvalidDataException("Encountered an RLE pair with zero length.");
                }

                float temperature = value / 100.0f;
                uint remaining = (uint)(pixelCount - temperatures.Count);
                uint copyCount = Math.Min(length, remaining);

                for (uint i = 0; i < copyCount; i++)
                {
                    temperatures.Add(temperature);
                }
            }

            return temperatures.ToArray();
        }

        private static SaveDataType InferSaveType(string path)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);

            if (fileName.Contains("Float", StringComparison.OrdinalIgnoreCase))
                return SaveDataType.Float;

            if (fileName.Contains("U16", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("Int", StringComparison.OrdinalIgnoreCase))
                return SaveDataType.U16;

            if (fileName.Contains("RLE", StringComparison.OrdinalIgnoreCase))
                return SaveDataType.RLE;

            if (fileName.Contains("All", StringComparison.OrdinalIgnoreCase))
                return SaveDataType.All;

            return SaveDataType.Float;
        }

        private static bool IsPerFrameBinary(string path)
        {
            string fileName = Path.GetFileName(path);
            return fileName.EndsWith("_base.bin", StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class PlaybackFrame : IDisposable
    {
        public PlaybackFrame(RecordedFrame sourceFrame, Bitmap bitmap, float scaleMin, float scaleMax, float frameMin, float frameMax, float frameMean)
        {
            SourceFrame = sourceFrame;
            Bitmap = bitmap;
            ScaleMin = scaleMin;
            ScaleMax = scaleMax;
            FrameMin = frameMin;
            FrameMax = frameMax;
            FrameMean = frameMean;
        }

        public RecordedFrame SourceFrame { get; }
        public Bitmap Bitmap { get; }
        public float ScaleMin { get; }
        public float ScaleMax { get; }
        public float FrameMin { get; }
        public float FrameMax { get; }
        public float FrameMean { get; }

        public void Dispose()
        {
            Bitmap.Dispose();
        }
    }

    public sealed class PlaybackTool
    {
        private readonly List<RecordedFrame> frames = new();
        private readonly object gate = new();
        private List<PaletteStop> paletteStops = BuildDefaultPalette();
        private CancellationTokenSource? playbackCancellation;
        private bool useAutoScaling = true;
        private float scaleLow;
        private float scaleHigh = 100.0f;
        private int currentIndex;

        public double FramesPerSecond { get; set; } = 10.0;
        public bool IsPlaying { get; private set; }
        public int CurrentIndex => currentIndex;
        public int FrameCount => frames.Count;

        public Action<PlaybackFrame>? FrameRendered;

        public void Load(string path)
        {
            LoadFrames(BinaryLoader.LoadFrameSet(path));
        }

        public void LoadFrames(IEnumerable<RecordedFrame> newFrames)
        {
            lock (gate)
            {
                frames.Clear();
                frames.AddRange(newFrames);
                currentIndex = 0;
            }
        }

        public RecordedFrame? GetCurrentFrame()
        {
            lock (gate)
            {
                if (frames.Count == 0)
                {
                    return null;
                }

                currentIndex = Math.Clamp(currentIndex, 0, frames.Count - 1);
                return frames[currentIndex];
            }
        }

        public void SetPlaybackRate(double framesPerSecond)
        {
            FramesPerSecond = framesPerSecond;
        }

        public void SetAutoScaling(bool enabled)
        {
            useAutoScaling = enabled;
        }

        public void SetScaleRange(float low, float high)
        {
            if (low > high)
            {
                (low, high) = (high, low);
            }

            scaleLow = low;
            scaleHigh = high;
            useAutoScaling = false;
        }

        public void LoadPaletteFromExcel(string workbookPath)
        {
            if (!File.Exists(workbookPath))
            {
                throw new FileNotFoundException("Palette workbook not found.", workbookPath);
            }

            List<PaletteStop> stops = ReadPaletteStopsFromWorkbook(workbookPath);
            if (stops.Count == 0)
            {
                throw new InvalidDataException($"No palette stops were found in '{workbookPath}'.");
            }

            paletteStops = NormalizePaletteStops(stops);
        }

        public void SetPaletteStops(IEnumerable<Color> palette)
        {
            Color[] colors = palette.ToArray();
            if (colors.Length == 0)
            {
                throw new ArgumentException("Palette cannot be empty.", nameof(palette));
            }

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

        public PlaybackFrame? RenderCurrentFrame()
        {
            RecordedFrame? frame = GetCurrentFrame();
            return frame == null ? null : RenderFrame(frame);
        }

        public PlaybackFrame RenderFrame(RecordedFrame frame)
        {
            float[] temperatures = frame.temperatures;
            if (temperatures == null || temperatures.Length == 0)
            {
                throw new InvalidDataException("The playback frame does not contain any temperature data.");
            }

            (float frameMin, float frameMax, float frameMean) = CalculateStatistics(temperatures);
            (float scaleMin, float scaleMax) = useAutoScaling
                ? (frameMin, frameMax)
                : (scaleLow, scaleHigh);

            if (Math.Abs(scaleMax - scaleMin) < float.Epsilon)
            {
                scaleMax = scaleMin + 0.0001f;
            }

            Bitmap bitmap = RenderBitmap(frame.width, frame.height, temperatures, scaleMin, scaleMax);
            return new PlaybackFrame(frame, bitmap, scaleMin, scaleMax, frameMin, frameMax, frameMean);
        }

        public async Task PlayAsync(CancellationToken cancellationToken = default)
        {
            await PlayAsync(null, cancellationToken).ConfigureAwait(false);
        }

        public async Task PlayAsync(Action<PlaybackFrame>? frameHandler, CancellationToken cancellationToken = default)
        {
            if (frameHandler != null)
            {
                FrameRendered += frameHandler;
            }

            try
            {
                await PlayInternalAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (frameHandler != null)
                {
                    FrameRendered -= frameHandler;
                }
            }
        }

        public void Stop()
        {
            playbackCancellation?.Cancel();
        }

        public void Reset()
        {
            lock (gate)
            {
                currentIndex = 0;
            }
        }

        private async Task PlayInternalAsync(CancellationToken cancellationToken)
        {
            CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            playbackCancellation = linkedCancellation;

            try
            {
                IsPlaying = true;

                while (true)
                {
                    PlaybackFrame? playbackFrame = null;

                    lock (gate)
                    {
                        if (frames.Count == 0 || currentIndex >= frames.Count)
                        {
                            break;
                        }

                        playbackFrame = RenderFrame(frames[currentIndex]);
                        currentIndex++;
                    }

                    if (playbackFrame == null)
                    {
                        break;
                    }

                    try
                    {
                        FrameRendered?.Invoke(playbackFrame);
                    }
                    finally
                    {
                        playbackFrame.Dispose();
                    }

                    if (FramesPerSecond > 0)
                    {
                        TimeSpan delay = TimeSpan.FromSeconds(1.0 / FramesPerSecond);
                        await Task.Delay(delay, linkedCancellation.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                IsPlaying = false;
                playbackCancellation = null;
                linkedCancellation.Dispose();
            }
        }

        private static (float Min, float Max, float Mean) CalculateStatistics(float[] temperatures)
        {
            float min = float.MaxValue;
            float max = float.MinValue;
            double sum = 0;

            foreach (float temperature in temperatures)
            {
                if (temperature < min)
                {
                    min = temperature;
                }

                if (temperature > max)
                {
                    max = temperature;
                }

                sum += temperature;
            }

            return (min, max, (float)(sum / temperatures.Length));
        }

        private Bitmap RenderBitmap(int width, int height, float[] temperatures, float scaleMin, float scaleMax)
        {
            if (width <= 0 || height <= 0)
            {
                throw new InvalidDataException("Cannot render a frame with empty dimensions.");
            }

            if (temperatures.Length != checked(width * height))
            {
                throw new InvalidDataException("Temperature data length does not match the frame dimensions.");
            }

            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            Rectangle rectangle = new Rectangle(0, 0, width, height);
            BitmapData bitmapData = bitmap.LockBits(rectangle, ImageLockMode.WriteOnly, bitmap.PixelFormat);

            try
            {
                int stride = bitmapData.Stride;
                byte[] pixels = new byte[stride * height];

                for (int y = 0; y < height; y++)
                {
                    int sourceRowOffset = y * width;
                    int destinationRowOffset = y * stride;

                    for (int x = 0; x < width; x++)
                    {
                        Color color = MapTemperatureToColor(temperatures[sourceRowOffset + x], scaleMin, scaleMax);
                        int pixelOffset = destinationRowOffset + (x * 3);

                        pixels[pixelOffset] = color.B;
                        pixels[pixelOffset + 1] = color.G;
                        pixels[pixelOffset + 2] = color.R;
                    }
                }

                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bitmapData.Scan0, pixels.Length);
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            return bitmap;
        }

        private Color MapTemperatureToColor(float temperature, float scaleMin, float scaleMax)
        {
            if (paletteStops.Count == 0)
            {
                return Color.Black;
            }

            if (paletteStops.Count == 1)
            {
                return paletteStops[0].Color;
            }

            float normalized = (temperature - scaleMin) / (scaleMax - scaleMin);
            normalized = Math.Clamp(normalized, 0.0f, 1.0f);

            PaletteStop left = paletteStops[0];
            PaletteStop right = paletteStops[^1];

            if (normalized <= left.Position)
            {
                return left.Color;
            }

            if (normalized >= right.Position)
            {
                return right.Color;
            }

            for (int i = 0; i < paletteStops.Count - 1; i++)
            {
                left = paletteStops[i];
                right = paletteStops[i + 1];

                if (normalized > right.Position)
                {
                    continue;
                }

                float span = right.Position - left.Position;
                float localT = span <= float.Epsilon ? 0.0f : (normalized - left.Position) / span;
                return Interpolate(left.Color, right.Color, localT);
            }

            return right.Color;
        }

        private static Color Interpolate(Color left, Color right, float t)
        {
            t = Math.Clamp(t, 0.0f, 1.0f);

            int r = (int)Math.Round(left.R + ((right.R - left.R) * t));
            int g = (int)Math.Round(left.G + ((right.G - left.G) * t));
            int b = (int)Math.Round(left.B + ((right.B - left.B) * t));

            return Color.FromArgb(255, r, g, b);
        }

        private static List<PaletteStop> BuildDefaultPalette()
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

        private static List<PaletteStop> NormalizePaletteStops(List<PaletteStop> stops)
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

        private static List<PaletteStop> ReadPaletteStopsFromWorkbook(string workbookPath)
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
                if (string.IsNullOrWhiteSpace(headerValue))
                {
                    continue;
                }

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
                if (row.Length == 0)
                {
                    continue;
                }

                if (!TryReadPaletteRow(row, positionColumn, redColumn, greenColumn, blueColumn, colorColumn, rowIndex - startRow, out PaletteStop stop))
                {
                    continue;
                }

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

        private static bool TryReadPaletteRow(
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
            {
                position = parsedPosition;
            }

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
                {
                    position = parsedFirst;
                }

                stop = new PaletteStop(position, Color.FromArgb(255, redValue, greenValue, blueValue));
                return true;
            }

            if (row.Length >= 2 && TryParseColor(row[1], out Color parsedColor))
            {
                if (TryParseFloat(row[0], out float parsedFirst))
                {
                    position = parsedFirst;
                }

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

        private static bool TryParseByte(string?[] row, int index, out byte value)
        {
            value = 0;
            return index < row.Length && byte.TryParse(row[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryParseFloat(string? value, out float parsed)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ||
                   float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed);
        }

        private static bool TryParseColor(string? value, out Color color)
        {
            color = Color.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

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

        private static List<string?[]> ReadWorksheetRows(ZipArchive archive, string worksheetPath, Dictionary<int, string> sharedStrings)
        {
            ZipArchiveEntry? worksheetEntry = archive.GetEntry(worksheetPath);
            if (worksheetEntry == null)
            {
                return new List<string?[]>();
            }

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

                if (cells.Count == 0)
                {
                    continue;
                }

                int maxIndex = cells.Keys.Max();
                var values = new string?[maxIndex + 1];
                foreach (KeyValuePair<int, string?> cell in cells)
                {
                    values[cell.Key] = cell.Value;
                }

                rows.Add(values);
            }

            return rows;
        }

        private static string ReadCellValue(XElement cell, XNamespace ns, Dictionary<int, string> sharedStrings)
        {
            string cellType = cell.Attribute("t")?.Value ?? string.Empty;

            if (cellType.Equals("inlineStr", StringComparison.OrdinalIgnoreCase))
            {
                return cell.Element(ns + "is")?.Element(ns + "t")?.Value ?? string.Empty;
            }

            string rawValue = cell.Element(ns + "v")?.Value ?? string.Empty;
            if (cellType.Equals("s", StringComparison.OrdinalIgnoreCase) && int.TryParse(rawValue, out int sharedIndex) &&
                sharedStrings.TryGetValue(sharedIndex, out string? sharedValue))
            {
                return sharedValue;
            }

            return rawValue;
        }

        private static Dictionary<int, string> ReadSharedStrings(ZipArchive archive)
        {
            var sharedStrings = new Dictionary<int, string>();
            ZipArchiveEntry? entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
            {
                return sharedStrings;
            }

            using Stream stream = entry.Open();
            XDocument document = XDocument.Load(stream);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

            int index = 0;
            foreach (XElement stringItem in document.Descendants(ns + "si"))
            {
                sharedStrings[index++] = stringItem.Value;
            }

            return sharedStrings;
        }

        private static string GetFirstWorksheetPath(ZipArchive archive)
        {
            ZipArchiveEntry? workbookEntry = archive.GetEntry("xl/workbook.xml");
            ZipArchiveEntry? relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");

            if (workbookEntry == null || relsEntry == null)
            {
                return "xl/worksheets/sheet1.xml";
            }

            XNamespace workbookNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace relNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XNamespace packageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

            using Stream workbookStream = workbookEntry.Open();
            using Stream relsStream = relsEntry.Open();

            XDocument workbookDoc = XDocument.Load(workbookStream);
            XDocument relsDoc = XDocument.Load(relsStream);

            string? relId = workbookDoc.Descendants(workbookNs + "sheet").FirstOrDefault()?.Attribute(relNs + "id")?.Value;
            if (string.IsNullOrWhiteSpace(relId))
            {
                return "xl/worksheets/sheet1.xml";
            }

            string? target = relsDoc
                .Descendants(packageRelNs + "Relationship")
                .FirstOrDefault(item => string.Equals(item.Attribute("Id")?.Value, relId, StringComparison.Ordinal))
                ?.Attribute("Target")?.Value;

            if (string.IsNullOrWhiteSpace(target))
            {
                return "xl/worksheets/sheet1.xml";
            }

            target = target.Replace('\\', '/');
            if (!target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
            {
                target = $"xl/{target.TrimStart('/')}";
            }

            return target;
        }

        private static int GetColumnIndex(string? cellReference)
        {
            if (string.IsNullOrWhiteSpace(cellReference))
            {
                return 0;
            }

            int index = 0;
            foreach (char character in cellReference)
            {
                if (!char.IsLetter(character))
                {
                    break;
                }

                index = (index * 26) + (char.ToUpperInvariant(character) - 'A' + 1);
            }

            return Math.Max(0, index - 1);
        }

        private static int? FindHeaderIndex(Dictionary<string, int> headers, params string[] names)
        {
            foreach (string name in names)
            {
                if (headers.TryGetValue(name, out int index))
                {
                    return index;
                }
            }

            return null;
        }

        private static bool ContainsKnownPaletteHeaders(Dictionary<string, int> headers)
        {
            return FindHeaderIndex(headers, "Temperature", "Temp", "Value", "Position", "Index") != null ||
                   FindHeaderIndex(headers, "R", "Red") != null ||
                   FindHeaderIndex(headers, "G", "Green") != null ||
                   FindHeaderIndex(headers, "B", "Blue") != null ||
                   FindHeaderIndex(headers, "Color", "Hex", "Palette") != null;
        }

        private readonly record struct PaletteStop(float Position, Color Color);
    }
}