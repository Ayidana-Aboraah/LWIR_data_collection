namespace OptrisXiLogger.Camera;

/// <summary>
/// A single captured thermal frame with all metadata needed for TIFF writing and display.
/// Raw thermal data is stored as ushort values encoding temperature.
/// Actual °C = (rawValue / 10.0f) - 100.0f   (standard Optris SDK encoding)
/// </summary>
public sealed class ThermalFrame
{
    public int      Width        { get; init; }
    public int      Height       { get; init; }
    public ushort[] RawData      { get; init; } = Array.Empty<ushort>();
    public DateTime Timestamp    { get; init; }
    public long     FrameIndex   { get; init; }

    // Per-frame statistics (°C)
    public float    MinTemp      { get; set; }
    public float    MaxTemp      { get; set; }
    public float    AvgTemp      { get; set; }
    public int      HotspotX     { get; set; }
    public int      HotspotY     { get; set; }

    // Camera parameters at capture time (snapshot for metadata embedding)
    public double   Emissivity   { get; init; }
    public double   AmbientTemp  { get; init; }
    public double   TransmittedTemp { get; init; }

    // SDK metadata passthrough
    public uint     SdkTimestamp { get; init; }  // IRFrameMetadata.timestamp (ms since epoch)
    public int      FlagState    { get; init; }  // shutter flag state

    /// <summary>Convert raw index to °C.</summary>
    public static float RawToCelsius(ushort raw) => raw / 10.0f - 100.0f;

    /// <summary>Create a deep copy (so the ring buffer slot can be reused).</summary>
    public ThermalFrame Clone()
    {
        var cloned = new ThermalFrame
        {
            Width           = Width,
            Height          = Height,
            RawData         = new ushort[RawData.Length],
            Timestamp       = Timestamp,
            FrameIndex      = FrameIndex,
            MinTemp         = MinTemp,
            MaxTemp         = MaxTemp,
            AvgTemp         = AvgTemp,
            HotspotX        = HotspotX,
            HotspotY        = HotspotY,
            Emissivity      = Emissivity,
            AmbientTemp     = AmbientTemp,
            TransmittedTemp = TransmittedTemp,
            SdkTimestamp    = SdkTimestamp,
            FlagState       = FlagState,
        };
        Array.Copy(RawData, cloned.RawData, RawData.Length);
        return cloned;
    }
}

/// <summary>
/// Single-producer / single-consumer lock-free ring buffer.
/// The camera callback thread writes; the disk-writer thread reads.
/// The display thread additionally peeks at the latest available frame (separate slot).
/// </summary>
public sealed class FrameBuffer : IDisposable
{
    private readonly ThermalFrame?[] _slots;
    private readonly int             _capacity;

    // For the writer/reader pipeline (SPSC)
    private volatile int _writePos = 0;
    private volatile int _readPos  = 0;
    private readonly SemaphoreSlim _dataAvailable = new(0);

    // Latest frame for display (overwritten freely; display thread takes a snapshot)
    private ThermalFrame? _latestFrame;
    private readonly object _latestLock = new();
    private long _droppedFrames = 0;

    public int  Capacity      => _capacity;
    public long DroppedFrames => Interlocked.Read(ref _droppedFrames);

    public FrameBuffer(int capacity = 256)
    {
        _capacity = capacity;
        _slots    = new ThermalFrame?[capacity];
    }

    /// <summary>
    /// Called by the camera callback thread. Non-blocking.
    /// If the buffer is full, the oldest unread frame is overwritten (frame drop logged).
    /// </summary>
    public void Enqueue(ThermalFrame frame)
    {
        // Always update the latest-frame display slot
        lock (_latestLock)
            _latestFrame = frame;

        int next = (_writePos + 1) % _capacity;
        if (next == _readPos)
        {
            // Buffer full — drop oldest frame, advance read pointer
            Interlocked.Increment(ref _droppedFrames);
            _readPos = (_readPos + 1) % _capacity;
        }

        _slots[_writePos] = frame;
        _writePos = next;
        _dataAvailable.Release();
    }

    /// <summary>
    /// Called by the disk-writer thread. Blocks until a frame is available or cancellation requested.
    /// Returns null on cancellation.
    /// </summary>
    public ThermalFrame? Dequeue(CancellationToken ct)
    {
        try
        {
            _dataAvailable.Wait(ct);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        var frame = _slots[_readPos];
        _slots[_readPos] = null;
        _readPos = (_readPos + 1) % _capacity;
        return frame;
    }

    /// <summary>
    /// Called by the display thread. Returns a snapshot of the most recently enqueued frame.
    /// Never blocks. Returns null if no frame has arrived yet.
    /// </summary>
    public ThermalFrame? PeekLatest()
    {
        lock (_latestLock)
            return _latestFrame;
    }

    public void Dispose() => _dataAvailable.Dispose();
}
