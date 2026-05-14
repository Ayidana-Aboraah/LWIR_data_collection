namespace OptrisXiLogger.UI;

/// <summary>
/// Main application window.
/// Layout: Settings panel (right) | Thermal display (center) | Status bar (bottom)
/// </summary>
public sealed class MainForm : Form
{
    // ── Core objects ──────────────────────────────────────────────────────
    private readonly AppSettings                   _settings;
    private readonly Camera.FrameBuffer            _frameBuffer;
    private readonly Camera.ThermalCameraClient    _camera;
    private readonly Acquisition.AcquisitionController _acquisition;
    private readonly SettingsPanel                 _settingsPanel;
    private readonly ThermalDisplayPanel           _displayPanel;

    // ── UI elements ───────────────────────────────────────────────────────
    private System.Windows.Forms.Timer _displayTimer = null!;
    private StatusStrip _statusStrip              = null!;
    private ToolStripStatusLabel _lblStatus       = null!;
    private ToolStripStatusLabel _lblFps          = null!;
    private ToolStripStatusLabel _lblSaved        = null!;
    private ToolStripStatusLabel _lblDropped      = null!;
    private ToolStripStatusLabel _lblRecording    = null!;
    private Button _btnRecord                     = null!;
    private Button _btnStop                       = null!;
    private Button _btnArmTrigger                 = null!;

    // FPS tracking
    private int  _displayFrameCount;
    private long _fpsCheckTime = Environment.TickCount64;

    public MainForm()
    {
        _settings    = AppSettings.Load();
        _frameBuffer = new Camera.FrameBuffer(256);
        _camera      = new Camera.ThermalCameraClient(_frameBuffer, _settings);
        _acquisition = new Acquisition.AcquisitionController(_camera, _frameBuffer, _settings);

        _settingsPanel = new SettingsPanel(_settings);
        _displayPanel  = new ThermalDisplayPanel();

        InitializeComponent();
        WireEvents();

        _displayPanel.Initialize(_settings);
    }

    // ── Form construction ─────────────────────────────────────────────────

    private void InitializeComponent()
    {
        SuspendLayout();

        Text            = "Optris Xi 640 — Thermal Logger";
        Size            = new Size(1280, 780);
        MinimumSize     = new Size(900, 600);
        BackColor       = Color.FromArgb(20, 20, 20);
        ForeColor       = Color.WhiteSmoke;
        StartPosition   = FormStartPosition.CenterScreen;

        // ── Toolbar (top) ─────────────────────────────────────────────────
        var toolbar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 44,
            BackColor = Color.FromArgb(35, 35, 50),
            Padding   = new Padding(8, 6, 8, 6),
        };
        Controls.Add(toolbar);

        Button MakeButton(string text, Color bg)
        {
            var b = new Button
            {
                Text      = text,
                Height    = 32,
                Width     = 130,
                FlatStyle = FlatStyle.Flat,
                BackColor = bg,
                ForeColor = Color.WhiteSmoke,
                Margin    = new Padding(0, 0, 6, 0),
                Font      = new Font("Segoe UI", 9, FontStyle.Bold),
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 120);
            return b;
        }

        _btnRecord     = MakeButton("⏺ Record",      Color.FromArgb(40, 120, 40));
        _btnStop       = MakeButton("⏹ Stop",        Color.FromArgb(140, 40, 40));
        _btnArmTrigger = MakeButton("⚡ Arm Trigger", Color.FromArgb(80, 60, 20));
        _btnStop.Enabled = false;

        var flowTools = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents  = false,
        };
        flowTools.Controls.AddRange(new Control[] { _btnRecord, _btnStop, _btnArmTrigger });
        toolbar.Controls.Add(flowTools);

        // ── Status bar (bottom) ───────────────────────────────────────────
        _statusStrip = new StatusStrip { BackColor = Color.FromArgb(30, 30, 45) };
        _lblStatus   = new ToolStripStatusLabel("Ready") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _lblRecording= new ToolStripStatusLabel("⬛ IDLE") { ForeColor = Color.Gray };
        _lblFps      = new ToolStripStatusLabel("Display: -- fps");
        _lblSaved    = new ToolStripStatusLabel("Saved: 0");
        _lblDropped  = new ToolStripStatusLabel("Dropped: 0");
        _statusStrip.Items.AddRange(new ToolStripItem[] { _lblStatus, _lblRecording, _lblFps, _lblSaved, _lblDropped });
        Controls.Add(_statusStrip);

        // ── Main split ────────────────────────────────────────────────────
        var splitter = new SplitContainer
        {
            Dock           = DockStyle.Fill,
            Orientation    = Orientation.Vertical,
            SplitterWidth  = 4,
            Panel2MinSize  = 270,
            FixedPanel     = FixedPanel.Panel2,
            BackColor      = Color.FromArgb(50, 50, 60),
        };
        splitter.SplitterDistance = Math.Max(splitter.Width - 290, 400);
        Controls.Add(splitter);

        _displayPanel.Dock = DockStyle.Fill;
        splitter.Panel1.Controls.Add(_displayPanel);

        _settingsPanel.Dock = DockStyle.Fill;
        splitter.Panel2.Controls.Add(_settingsPanel);

        // ── Display refresh timer ─────────────────────────────────────────
        _displayTimer          = new System.Windows.Forms.Timer();
        _displayTimer.Interval = 1000 / Math.Max(_settings.DisplayFps, 1);
        _displayTimer.Tick    += DisplayTimer_Tick;
        _displayTimer.Start();

        ResumeLayout();
    }

    // ── Event wiring ──────────────────────────────────────────────────────

    private void WireEvents()
    {
        // Toolbar buttons
        _btnRecord.Click     += (_, _) => StartRecordingManual();
        _btnStop.Click       += (_, _) => StopRecording();
        _btnArmTrigger.Click += (_, _) => ToggleTriggerArm();

        // Settings panel callbacks
        _settingsPanel.ConnectRequested             += ConnectCamera;
        _settingsPanel.BrowseSaveDirectoryRequested += BrowseSaveDirectory;
        _settingsPanel.BrowseCameraXmlRequested     += BrowseCameraXml;
        _settingsPanel.SettingsChanged              += OnSettingsChanged;

        // Camera events (raised on grab thread → marshal to UI)
        _camera.OnError      += msg => BeginInvoke(() => SetStatus($"Camera error: {msg}", true));

        // Acquisition events
        _acquisition.OnStatusMessage  += msg => BeginInvoke(() => SetStatus(msg));
        _acquisition.OnError          += msg => BeginInvoke(() => SetStatus($"Error: {msg}", true));
        _acquisition.OnRecordingStarted += () => BeginInvoke(UpdateRecordingUI);
        _acquisition.OnRecordingStopped += () => BeginInvoke(UpdateRecordingUI);

        // Form closing
        FormClosing += (_, _) =>
        {
            _displayTimer.Stop();
            _acquisition.Dispose();
            _camera.Dispose();
            _frameBuffer.Dispose();
        };
    }

    // ── Display timer ─────────────────────────────────────────────────────

    private void DisplayTimer_Tick(object? sender, EventArgs e)
    {
        var frame = _frameBuffer.PeekLatest();
        if (frame == null) return;

        _displayPanel.UpdateFrame(frame);
        _displayFrameCount++;

        // Update FPS counter once per second
        long now = Environment.TickCount64;
        if (now - _fpsCheckTime >= 1000)
        {
            _lblFps.Text      = $"Display: {_displayFrameCount} fps";
            _displayFrameCount = 0;
            _fpsCheckTime     = now;
        }

        // Update counters
        _lblSaved.Text   = $"Saved: {_acquisition.SavedFrames:N0}";
        _lblDropped.Text = $"Dropped: {_acquisition.DroppedFrames:N0}";
        if (_acquisition.DroppedFrames > 0)
            _lblDropped.ForeColor = Color.Orange;
    }

    // ── Button handlers ───────────────────────────────────────────────────

    private void StartRecordingManual()
    {
        if (!_camera.IsConnected)
        {
            MessageBox.Show("Please connect the camera first.", "Not Connected",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _acquisition.StartRecording();
    }

    private void StopRecording()
    {
        _acquisition.StopRecording();
        _acquisition.DisarmTrigger();
    }

    private void ToggleTriggerArm()
    {
        if (!_camera.IsConnected)
        {
            MessageBox.Show("Please connect the camera first.", "Not Connected",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_acquisition.IsTriggerArmed)
        {
            _acquisition.DisarmTrigger();
            _btnArmTrigger.Text      = "⚡ Arm Trigger";
            _btnArmTrigger.BackColor = Color.FromArgb(80, 60, 20);
        }
        else
        {
            if (!_settings.TriggerEnabled)
            {
                MessageBox.Show("Enable the trigger in Settings and apply first.",
                    "Trigger Disabled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _acquisition.ArmTrigger();
            _btnArmTrigger.Text      = "⚡ Disarm Trigger";
            _btnArmTrigger.BackColor = Color.FromArgb(160, 120, 20);
        }
    }

    private void UpdateRecordingUI()
    {
        bool rec = _acquisition.IsRecording;
        _btnRecord.Enabled  = !rec;
        _btnStop.Enabled    = rec;
        _lblRecording.Text      = rec ? "🔴 RECORDING" : "⬛ IDLE";
        _lblRecording.ForeColor = rec ? Color.Red : Color.Gray;
    }

    // ── Camera connection ─────────────────────────────────────────────────

    private void ConnectCamera()
    {
        if (_camera.IsConnected)
        {
            SetStatus("Camera already connected.");
            return;
        }

        try
        {
            SetStatus("Connecting to camera…");
            _camera.Connect();
            SetStatus($"Connected: Optris Xi 640 (S/N {_camera.CameraSerial}) — {_camera.Width}×{_camera.Height} @ 32 Hz");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to connect:\n\n{ex.Message}",
                "Camera Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("Connection failed.", error: true);
        }
    }

    // ── Settings ──────────────────────────────────────────────────────────

    private void OnSettingsChanged()
    {
        // Update display timer interval
        _displayTimer.Interval = 1000 / Math.Max(_settings.DisplayFps, 1);

        // Rebuild palette
        _displayPanel.RebuildPalette();

        // Push new radiation params to camera (if connected)
        if (_camera.IsConnected)
            _camera.ApplyRadiationParams();

        SetStatus("Settings applied.");
    }

    private void BrowseSaveDirectory()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description         = "Select save directory for TIFF recordings",
            SelectedPath        = _settings.SaveDirectory,
            UseDescriptionForTitle = true,
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            _settingsPanel.SetSaveDirectory(dlg.SelectedPath);
    }

    private void BrowseCameraXml()
    {
        using var dlg = new OpenFileDialog
        {
            Title  = "Select Optris camera XML configuration file",
            Filter = "XML Config|*.xml|All Files|*.*",
            InitialDirectory = Path.GetDirectoryName(_settings.CameraXmlPath)
                               ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            _settingsPanel.SetCameraXmlPath(dlg.FileName);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void SetStatus(string msg, bool error = false)
    {
        _lblStatus.Text      = msg;
        _lblStatus.ForeColor = error ? Color.Tomato : Color.WhiteSmoke;
    }
}
