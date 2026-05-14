using NationalInstruments.DAQmx;

namespace OptrisXiLogger.Acquisition;

/// <summary>
/// Listens for a digital edge on a configurable NI-DAQmx line and raises
/// <see cref="Triggered"/> when detected. Runs on a dedicated background thread.
/// </summary>
public sealed class NiDaqTrigger : IDisposable
{
    private readonly AppSettings _settings;
    private          Task?       _daqTask;
    private          Thread?     _pollThread;
    private volatile bool        _running;

    /// <summary>Raised on the polling thread when a trigger edge is detected.</summary>
    public event Action? Triggered;

    /// <summary>Raised when an error occurs in the DAQ subsystem.</summary>
    public event Action<string>? OnError;

    public bool IsArmed => _running;

    public NiDaqTrigger(AppSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Arm the trigger. Starts monitoring the configured digital line.
    /// Call from UI thread after recording session is set up.
    /// </summary>
    public void Arm()
    {
        if (_running) return;

        try
        {
            // Build the full physical channel string, e.g. "Dev1/port0/line0"
            string channel = $"{_settings.NiDaqDeviceName}/{_settings.NiDaqDigitalLine}";

            _daqTask = new Task();
            _daqTask.DIChannels.CreateChannel(channel, "", ChannelLineGrouping.OneChannelForAllLines);
            _daqTask.Control(TaskAction.Verify);

            _running    = true;
            _pollThread = new Thread(PollLoop)
            {
                Name         = "DaqTriggerPollThread",
                IsBackground = true,
                Priority     = ThreadPriority.AboveNormal
            };
            _pollThread.Start();
        }
        catch (DaqException ex)
        {
            OnError?.Invoke($"NI-DAQmx arm failed: {ex.Message}");
            DisposeTask();
        }
    }

    /// <summary>Disarm and release DAQ resources.</summary>
    public void Disarm()
    {
        _running = false;
        _pollThread?.Join(1000);
        _pollThread = null;
        DisposeTask();
    }

    // ── Poll loop ─────────────────────────────────────────────────────────

    private void PollLoop()
    {
        // We use software polling with edge detection rather than hardware-timed
        // change detection, which would require a counter or timing task.
        // At 1 kHz polling this gives ~1 ms trigger latency — adequate for NI-DAQ
        // triggered recording. For sub-millisecond latency, use a hardware counter task.

        bool lastState = false;
        bool firstRead = true;

        using var reader = new DigitalSingleChannelReader(_daqTask!.Stream);

        while (_running)
        {
            try
            {
                bool currentState = reader.ReadSingleSampleSingleLine();

                if (!firstRead)
                {
                    bool risingEdge  = !lastState && currentState;
                    bool fallingEdge =  lastState && !currentState;

                    bool edgeDetected = _settings.TriggerEdge switch
                    {
                        TriggerEdge.Rising  => risingEdge,
                        TriggerEdge.Falling => fallingEdge,
                        _                   => false
                    };

                    if (edgeDetected)
                        Triggered?.Invoke();
                }

                lastState = currentState;
                firstRead = false;

                Thread.Sleep(1);  // ~1 kHz poll rate
            }
            catch (DaqException ex) when (_running)
            {
                OnError?.Invoke($"NI-DAQmx read error: {ex.Message}");
                Thread.Sleep(500);  // back off before retrying
            }
        }
    }

    private void DisposeTask()
    {
        try { _daqTask?.Dispose(); } catch { }
        _daqTask = null;
    }

    public void Dispose() => Disarm();
}
