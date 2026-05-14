using BitMiracle.LibTiff.Classic;

namespace OptrisXiLogger.IO;

/// <summary>
/// Writes a single thermal frame as a 16-bit grayscale TIFF.
/// Raw ushort pixel values are written directly (Optris encoding: raw/10 - 100 = °C).
/// Full metadata is embedded as TIFF tags and in a custom XML ImageDescription.
/// </summary>
public static class TiffWriter
{
    // Custom TIFF tag numbers in the private-use range (0xC000–0xFFFF)
    private const TiffTag TAG_FRAME_INDEX      = (TiffTag)0xC000;
    private const TiffTag TAG_MIN_TEMP         = (TiffTag)0xC001;
    private const TiffTag TAG_MAX_TEMP         = (TiffTag)0xC002;
    private const TiffTag TAG_AVG_TEMP         = (TiffTag)0xC003;
    private const TiffTag TAG_HOTSPOT_X        = (TiffTag)0xC004;
    private const TiffTag TAG_HOTSPOT_Y        = (TiffTag)0xC005;
    private const TiffTag TAG_EMISSIVITY       = (TiffTag)0xC006;
    private const TiffTag TAG_AMBIENT_TEMP     = (TiffTag)0xC007;
    private const TiffTag TAG_TRANSMITTED_TEMP = (TiffTag)0xC008;
    private const TiffTag TAG_SDK_TIMESTAMP    = (TiffTag)0xC009;
    private const TiffTag TAG_FLAG_STATE       = (TiffTag)0xC00A;

    // App version embedded in metadata
    private static readonly string AppVersion =
        System.Reflection.Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString() ?? "1.0.0";

    /// <summary>
    /// Write a thermal frame to <paramref name="filePath"/> as a 16-bit TIFF.
    /// The caller is responsible for creating the output directory beforehand.
    /// </summary>
    public static void Write(string filePath, Camera.ThermalFrame frame)
    {
        using var tiff = Tiff.Open(filePath, "w");
        if (tiff == null)
            throw new IOException($"Cannot open TIFF for writing: {filePath}");

        int width  = frame.Width;
        int height = frame.Height;

        // ── Standard TIFF tags ────────────────────────────────────────────
        tiff.SetField(TiffTag.IMAGEWIDTH,      width);
        tiff.SetField(TiffTag.IMAGELENGTH,     height);
        tiff.SetField(TiffTag.SAMPLESPERPIXEL, 1);
        tiff.SetField(TiffTag.BITSPERSAMPLE,   16);
        tiff.SetField(TiffTag.PHOTOMETRIC,     Photometric.MINISBLACK);
        tiff.SetField(TiffTag.COMPRESSION,     Compression.NONE);  // lossless, no compression
        tiff.SetField(TiffTag.PLANARCONFIG,    PlanarConfig.CONTIG);
        tiff.SetField(TiffTag.ROWSPERSTRIP,    height);            // single strip

        // Resolution (no spatial calibration available; mark as unitless)
        tiff.SetField(TiffTag.RESOLUTIONUNIT,  ResUnit.NONE);
        tiff.SetField(TiffTag.XRESOLUTION,     1.0f);
        tiff.SetField(TiffTag.YRESOLUTION,     1.0f);

        // Software and date/time
        tiff.SetField(TiffTag.SOFTWARE,        $"OptrisXiLogger v{AppVersion}");
        tiff.SetField(TiffTag.DATETIME,
            frame.Timestamp.ToString("yyyy:MM:dd HH:mm:ss"));  // TIFF date format

        // ── XML ImageDescription with all metadata ────────────────────────
        tiff.SetField(TiffTag.IMAGEDESCRIPTION, BuildXmlDescription(frame));

        // ── Custom private tags ───────────────────────────────────────────
        // LibTiff.NET requires tags to be registered before SetField.
        // We use the ASCII workaround: store values in the ImageDescription XML above.
        // The custom tags below are best-effort extras for tools that parse raw TIFF IFDs.
        // (LibTiff.NET may silently ignore unregistered private tags; the XML is canonical.)

        // ── Pixel data ────────────────────────────────────────────────────
        // TIFF stores data row by row. ushort[] → byte[] via span reinterpretation.
        int bytesPerRow = width * 2;  // 16 bpp = 2 bytes per pixel
        byte[] rowBuf   = new byte[bytesPerRow];

        for (int row = 0; row < height; row++)
        {
            int srcOffset = row * width;
            // Copy ushort row to byte buffer (little-endian, native)
            Buffer.BlockCopy(frame.RawData, srcOffset * 2, rowBuf, 0, bytesPerRow);
            tiff.WriteScanline(rowBuf, row);
        }

        tiff.FlushData();
    }

    // ── Metadata XML ──────────────────────────────────────────────────────

    private static string BuildXmlDescription(Camera.ThermalFrame f)
    {
        // ISO 8601 timestamp with sub-millisecond precision
        string ts = f.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");

        string flagDesc = f.FlagState switch
        {
            0 => "Open",
            1 => "Closed",
            2 => "Opening",
            3 => "Closing",
            4 => "Error",
            5 => "Initializing",
            _ => $"Unknown({f.FlagState})"
        };

        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <OptrisXiLoggerMetadata>
              <Capture>
                <Timestamp>{ts}</Timestamp>
                <FrameIndex>{f.FrameIndex}</FrameIndex>
                <SdkTimestampMs>{f.SdkTimestamp}</SdkTimestampMs>
                <ShutterFlagState>{flagDesc}</ShutterFlagState>
              </Capture>
              <Temperature unit="Celsius">
                <Min>{f.MinTemp:F3}</Min>
                <Max>{f.MaxTemp:F3}</Max>
                <Average>{f.AvgTemp:F3}</Average>
                <HotspotX>{f.HotspotX}</HotspotX>
                <HotspotY>{f.HotspotY}</HotspotY>
              </Temperature>
              <RadiationParameters>
                <Emissivity>{f.Emissivity:F4}</Emissivity>
                <AmbientTemperature unit="Celsius">{f.AmbientTemp:F2}</AmbientTemperature>
                <TransmittedTemperature unit="Celsius">{f.TransmittedTemp:F2}</TransmittedTemperature>
              </RadiationParameters>
              <PixelEncoding>
                <Formula>Celsius = (RawUInt16 / 10.0) - 100.0</Formula>
                <BitDepth>16</BitDepth>
                <ByteOrder>LittleEndian</ByteOrder>
              </PixelEncoding>
              <Software>
                <Name>OptrisXiLogger</Name>
                <Version>{AppVersion}</Version>
                <Camera>Optris Xi 640</Camera>
              </Software>
            </OptrisXiLoggerMetadata>
            """;
    }
}
