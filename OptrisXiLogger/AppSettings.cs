using System.Text.Json;
using System.Text.Json.Serialization;

namespace OptrisXiLogger;

/// <summary>
/// All user-configurable settings. Persisted to %AppData%\OptrisXiLogger\settings.json.
/// </summary>
public class AppSettings
{
    // ── Paths ─────────────────────────────────────────────────────────────
    public string SaveDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    public string SessionPrefix  { get; set; } = "session";

    // ── Camera ────────────────────────────────────────────────────────────
    /// <summary>Path to the camera XML config file (required by OTC SDK).</summary>
    public string CameraXmlPath  { get; set; } = "";
    public double Emissivity      { get; set; } = 1.0;
    public double TransmittedTemp { get; set; } = 20.0;  // °C, background radiance
    public double AmbientTemp     { get; set; } = 20.0;  // °C

    // ── Temperature display range ─────────────────────────────────────────
    public bool   AutoRange    { get; set; } = true;
    public double RangeMin     { get; set; } = 20.0;  // °C, used when AutoRange = false
    public double RangeMax     { get; set; } = 80.0;  // °C

    // ── Recording ─────────────────────────────────────────────────────────
    /// <summary>
    /// Hard cap on display update rate (Hz). The camera always records at 32 Hz
    /// regardless of this value. Set lower to reduce CPU load on slow machines.
    /// Hardware limit: Xi 640 outputs frames at 32 Hz; displaying faster is meaningless.
    /// </summary>
    public const  int    MaxAllowedDisplayFps = 32;
    public        int    DisplayFps            { get; set; } = 10;

    // ── Trigger ───────────────────────────────────────────────────────────
    public bool   TriggerEnabled      { get; set; } = false;
    public string NiDaqDeviceName     { get; set; } = "Dev1";
    public string NiDaqDigitalLine    { get; set; } = "port0/line0";
    public TriggerEdge TriggerEdge    { get; set; } = TriggerEdge.Rising;

    // ── Palette ───────────────────────────────────────────────────────────
    public ThermalPalette Palette { get; set; } = ThermalPalette.Ironbow;

    // ── Persistence ───────────────────────────────────────────────────────
    private static readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OptrisXiLogger", "settings.json");

    private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json, _jsonOpts) ?? new AppSettings();
            }
        }
        catch { /* ignore corrupt settings */ }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(this, _jsonOpts));
        }
        catch { /* best-effort */ }
    }

    /// <summary>Clamp and enforce hardware limits before saving.</summary>
    public void Validate()
    {
        DisplayFps  = Math.Clamp(DisplayFps,  1, MaxAllowedDisplayFps);
        Emissivity  = Math.Clamp(Emissivity,  0.01, 1.0);
        if (RangeMin >= RangeMax) RangeMax = RangeMin + 1.0;
    }
}

public enum TriggerEdge   { Rising, Falling }
public enum ThermalPalette { Ironbow, Rainbow, Grayscale, Hot }
