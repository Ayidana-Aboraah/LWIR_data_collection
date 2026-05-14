// NOTE: The OTC SDK C# wrapper classes expose the same API as the C++ SDK.
// The key types used here are:
//   IRImager          – main camera control class
//   IRImagerClient    – abstract base; override onThermalFrame(), onVisibleFrame(), etc.
//   IRFrameMetadata   – metadata struct delivered with each frame
//   IRDeviceDS        – Windows DirectShow acquisition class (USB on Windows)
//
// After installing the SDK, copy the C# wrapper .cs files from
//   C:\Program Files\Optris\otcsdk\bindings\csharp\classes\
// into a 'classes/' subfolder of this project.

using evo.IRImager;  // OTC SDK namespace (from wrapper classes)

namespace OptrisXiLogger.Camera;

/// <summary>
/// Subclass of IRImagerClient that receives thermal frames from the OTC SDK
/// and pushes them into the shared FrameBuffer.
/// Runs entirely on the SDK's internal acquisition thread — keep it lightweight.
/// </summary>
public sealed class ThermalCameraClient : IRImagerClient, IDisposable
{
    private readonly FrameBuffer      _buffer;
    private readonly AppSettings      _settings;
    private          IRImager?        _imager;
    private          IRDeviceDS?      _device;    // Windows DirectShow USB acquisition
    private          Thread?          _grabThread;
    private volatile bool             _running;
    private          long             _frameIndex;
    private volatile bool             _recordingEnabled; // gate: only enqueue when recording

    public bool IsConnected   => _imager != null;
    public bool IsRecording   => _recordingEnabled;
    public int  Width         { get; private set; }
    public int  Height        { get; private set; }
    public string CameraSerial { get; private set; } = "";

    // Raised on the grab thread — subscribe on UI thread with BeginInvoke
    public event Action<string>?       OnError;
    public event Action<ThermalFrame>? OnFrameReady;  // raised even when not recording (for display)

    public ThermalCameraClient(FrameBuffer buffer, AppSettings settings)
    {
        _buffer   = buffer;
        _settings = settings;
    }

    // ── Connection ────────────────────────────────────────────────────────

    /// <summary>
    /// Connect to the camera using the XML config file specified in AppSettings.
    /// Throws on failure so the caller can display an error dialog.
    /// </summary>
    public void Connect()
    {
        if (_imager != null)
            throw new InvalidOperationException("Already connected.");

        if (!File.Exists(_settings.CameraXmlPath))
            throw new FileNotFoundException($"Camera XML config not found: {_settings.CameraXmlPath}");

        _imager = new IRImager();

        // IRImager.init(xmlPath, serialNumber="", formatIndex=0)
        // formatIndex 0 = full resolution, full frame rate
        int result = _imager.init(_settings.CameraXmlPath, "", 0);
        if (result != 1)
            throw new Exception($"IRImager.init() failed with code {result}. Check XML config and USB connection.");

        Width  = (int)_imager.getWidth();
        Height = (int)_imager.getHeight();
        CameraSerial = _imager.getSerialNumber().ToString();

        // Register this instance as the callback client
        _imager.setClient(this);

        // Apply radiation parameters
        ApplyRadiationParams();

        // Start the DirectShow acquisition device
        _device = new IRDeviceDS();
        _device.init(_imager);

        _running = true;
        _grabThread = new Thread(GrabLoop)
        {
            Name         = "CameraGrabThread",
            IsBackground = true,
            Priority     = ThreadPriority.Highest  // keep up with 32 Hz
        };
        _grabThread.Start();
    }

    public void Disconnect()
    {
        _running = false;
        _grabThread?.Join(2000);
        _device?.stopVideo();
        _device   = null;
        _imager   = null;
        _running  = false;
    }

    // ── Recording gate ────────────────────────────────────────────────────

    public void StartRecording() => _recordingEnabled = true;
    public void StopRecording()  => _recordingEnabled = false;

    // ── Radiation parameters ──────────────────────────────────────────────

    public void ApplyRadiationParams()
    {
        if (_imager == null) return;
        _imager.setEmissivity((float)_settings.Emissivity);
        _imager.setEnvironmentTemp((float)_settings.AmbientTemp);
        _imager.setTransmittedTemp((float)_settings.TransmittedTemp);
    }

    // ── Grab loop (runs on _grabThread) ───────────────────────────────────

    private void GrabLoop()
    {
        // IRDeviceDS.run() is blocking — it calls _imager.process() on each
        // incoming raw USB frame, which in turn triggers onThermalFrame().
        try
        {
            _device!.run();  // blocks until stopped
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Camera grab error: {ex.Message}");
        }
    }

    // ── IRImagerClient callbacks (called on grab thread) ─────────────────

    /// <summary>
    /// Called at 32 Hz by the SDK with a fresh thermal frame.
    /// MUST be fast — do not do disk I/O here.
    /// </summary>
    public override void onThermalFrame(
        ushort[]        thermalData,
        uint            width,
        uint            height,
        IRFrameMetadata metadata,
        object?         arg)
    {
        long idx = Interlocked.Increment(ref _frameIndex);

        // Compute statistics inline (fast — single pass over array)
        ComputeStats(thermalData, (int)(width * height),
            out float minC, out float maxC, out float avgC,
            out int hotX, out int hotY, (int)width);

        var frame = new ThermalFrame
        {
            Width           = (int)width,
            Height          = (int)height,
            RawData         = thermalData,   // NOTE: SDK owns this buffer; we copy in Enqueue if needed
            Timestamp       = DateTime.UtcNow,
            FrameIndex      = idx,
            MinTemp         = minC,
            MaxTemp         = maxC,
            AvgTemp         = avgC,
            HotspotX        = hotX,
            HotspotY        = hotY,
            Emissivity      = _settings.Emissivity,
            AmbientTemp     = _settings.AmbientTemp,
            TransmittedTemp = _settings.TransmittedTemp,
            SdkTimestamp    = metadata.timestamp,
            FlagState       = (int)metadata.flagState,
        };

        // Always raise for display (display panel reads _buffer.PeekLatest())
        // Only enqueue into the write pipeline when recording is active
        if (_recordingEnabled)
        {
            // Clone so the ring buffer has its own copy of the pixel data
            _buffer.Enqueue(frame.Clone());
        }
        else
        {
            // Still update the "latest" display slot without going into write queue
            _buffer.Enqueue(frame);  // FrameBuffer.PeekLatest() path only
        }

        OnFrameReady?.Invoke(frame);
    }

    public override void onVisibleFrame(byte[] visibleData, uint width, uint height, IRFrameMetadata metadata, object? arg)
    {
        // Xi 640 is thermal-only; no visible camera. Safe to ignore.
    }

    public override void onFlagStateChange(uint flagState, object? arg)
    {
        // Shutter flag cycling — no action needed; FlagState is embedded in frame metadata.
    }

    // ── Statistics ────────────────────────────────────────────────────────

    private static void ComputeStats(
        ushort[] raw, int count,
        out float minC, out float maxC, out float avgC,
        out int hotX, out int hotY, int width)
    {
        ushort minR = ushort.MaxValue, maxR = ushort.MinValue;
        long   sum  = 0;
        int    hotIdx = 0;

        for (int i = 0; i < count; i++)
        {
            ushort v = raw[i];
            if (v < minR) minR = v;
            if (v > maxR) { maxR = v; hotIdx = i; }
            sum += v;
        }

        minC = ThermalFrame.RawToCelsius(minR);
        maxC = ThermalFrame.RawToCelsius(maxR);
        avgC = ThermalFrame.RawToCelsius((ushort)(sum / count));
        hotX = hotIdx % width;
        hotY = hotIdx / width;
    }

    public void Dispose() => Disconnect();
}
