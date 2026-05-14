namespace OptrisXiLogger.Acquisition;

/// <summary>
/// Orchestrates the recording session: manages the disk-writer thread,
/// coordinates the NI-DAQ trigger, and exposes Start/Stop recording to the UI.
/// </summary>
public sealed class AcquisitionController : IDisposable
{
    private readonly Camera.ThermalCameraClient _camera;
    private readonly Camera.FrameBuffer         _buffer;
    private readonly AppSettings                _settings;

    private NiDaqTrigger?      _trigger;
    private Thread?            _writerThread;
    private CancellationTokenSource? _writerCts;
    private string             _sessionDir   = "";
    private long               _savedFrames;
    private long               _sessionStart; // Stopwatch ticks

    public bool  IsRecording  { get; private set; }
    public bool  IsTriggerArmed => _trigger?.IsArmed ?? false;
    public long  SavedFrames  => Interlocked.Read(ref _savedFrames);
    public long  DroppedFrames => _buffer.DroppedFrames;

    // Events raised from worker threads — marshal to UI with BeginInvoke
    public event Action<string>? OnStatusMessage;
    public event Action<string>? OnError;
    public event Action?         OnRecordingStarted;
    public event Action?         OnRecordingStopped;

    public AcquisitionController(
        Camera.ThermalCameraClient camera,
        Camera.FrameBuffer         buffer,
        AppSettings                settings)
    {
        _camera   = camera;
        _buffer   = buffer;
        _settings = settings;
    }

    // ── Trigger management ────────────────────────────────────────────────

    public void ArmTrigger()
    {
        if (!_settings.TriggerEnabled) return;

        _trigger = new NiDaqTrigger(_settings);
        _trigger.Triggered += OnTriggerFired;
        _trigger.OnError   += msg => OnError?.Invoke(msg);
        _trigger.Arm();
        OnStatusMessage?.Invoke($"Trigger armed on {_settings.NiDaqDeviceName}/{_settings.NiDaqDigitalLine}");
    }

    public void DisarmTrigger()
    {
        _trigger?.Disarm();
        _trigger?.Dispose();
        _trigger = null;
        OnStatusMessage?.Invoke("Trigger disarmed.");
    }

    private void OnTriggerFired()
    {
        OnStatusMessage?.Invoke("Trigger received — starting recording.");
        StartRecording();
    }

    // ── Recording ─────────────────────────────────────────────────────────

    /// <summary>
    /// Begin recording. Creates session directory and starts the writer thread.
    /// Safe to call from any thread.
    /// </summary>
    public void StartRecording()
    {
        if (IsRecording) return;

        // Build session directory: SaveDirectory/Prefix_YYYYMMDD_HHmmss/
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string dirName   = $"{_settings.SessionPrefix}_{timestamp}";
        _sessionDir      = Path.Combine(_settings.SaveDirectory, dirName);

        try
        {
            Directory.CreateDirectory(_sessionDir);
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Cannot create session directory: {ex.Message}");
            return;
        }

        Interlocked.Exchange(ref _savedFrames, 0);
        _sessionStart = System.Diagnostics.Stopwatch.GetTimestamp();

        // Start writer thread
        _writerCts    = new CancellationTokenSource();
        _writerThread = new Thread(() => WriterLoop(_writerCts.Token))
        {
            Name         = "TiffWriterThread",
            IsBackground = true,
            Priority     = ThreadPriority.AboveNormal
        };
        _writerThread.Start();

        // Open the recording gate on the camera
        _camera.StartRecording();
        IsRecording = true;

        OnRecordingStarted?.Invoke();
        OnStatusMessage?.Invoke($"Recording to: {_sessionDir}");
    }

    /// <summary>
    /// Stop recording. Flushes remaining buffered frames before returning.
    /// </summary>
    public void StopRecording()
    {
        if (!IsRecording) return;

        // Close the gate first — no new frames enqueued after this
        _camera.StopRecording();
        IsRecording = false;

        // Signal writer and wait for it to drain the buffer and exit
        _writerCts?.Cancel();
        _writerThread?.Join(10_000);  // up to 10 s to drain remaining frames

        double elapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - _sessionStart)
                         / (double)System.Diagnostics.Stopwatch.Frequency;
        double fps = SavedFrames / Math.Max(elapsed, 0.001);

        OnRecordingStopped?.Invoke();
        OnStatusMessage?.Invoke(
            $"Recording stopped. {SavedFrames} frames saved ({fps:F1} fps avg). " +
            $"Dropped: {DroppedFrames}. Dir: {_sessionDir}");
    }

    // ── Writer loop ───────────────────────────────────────────────────────

    private void WriterLoop(CancellationToken ct)
    {
        while (true)
        {
            var frame = _buffer.Dequeue(ct);
            if (frame == null) break;  // cancelled

            string fileName = $"frame_{frame.FrameIndex:D8}_{frame.Timestamp:yyyyMMdd_HHmmss_fff}.tiff";
            string filePath = Path.Combine(_sessionDir, fileName);

            try
            {
                IO.TiffWriter.Write(filePath, frame);
                Interlocked.Increment(ref _savedFrames);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"TIFF write error (frame {frame.FrameIndex}): {ex.Message}");
                // Continue — try to save subsequent frames
            }
        }

        // Drain any remaining frames that were enqueued before cancellation
        // (Dequeue returns null immediately on cancelled token, so we poll)
        while (true)
        {
            using var cts2 = new CancellationTokenSource(0);  // non-blocking
            var frame = _buffer.Dequeue(cts2.Token);
            if (frame == null) break;

            string fileName = $"frame_{frame.FrameIndex:D8}_{frame.Timestamp:yyyyMMdd_HHmmss_fff}.tiff";
            string filePath = Path.Combine(_sessionDir, fileName);
            try
            {
                IO.TiffWriter.Write(filePath, frame);
                Interlocked.Increment(ref _savedFrames);
            }
            catch { /* best-effort drain */ }
        }
    }

    public void Dispose()
    {
        StopRecording();
        DisarmTrigger();
    }
}
