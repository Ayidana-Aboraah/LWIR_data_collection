namespace OptrisXiLogger.UI;

/// <summary>
/// Right-hand settings panel. Edits AppSettings in-place; raises SettingsChanged
/// so MainForm can apply changes to the camera and display.
/// </summary>
public sealed class SettingsPanel : Panel
{
    private readonly AppSettings _settings;
    public event Action? SettingsChanged;
    public event Action? ConnectRequested;
    public event Action? BrowseSaveDirectoryRequested;
    public event Action? BrowseCameraXmlRequested;

    // Controls
    private TextBox   _txSaveDir       = null!;
    private TextBox   _txSessionPrefix = null!;
    private TextBox   _txCameraXml     = null!;
    private NumericUpDown _nudEmissivity   = null!;
    private NumericUpDown _nudAmbient      = null!;
    private NumericUpDown _nudTransmitted  = null!;
    private NumericUpDown _nudDisplayFps   = null!;
    private NumericUpDown _nudRangeMin     = null!;
    private NumericUpDown _nudRangeMax     = null!;
    private CheckBox  _chkAutoRange     = null!;
    private CheckBox  _chkTrigger       = null!;
    private TextBox   _txDaqDevice      = null!;
    private TextBox   _txDaqLine        = null!;
    private ComboBox  _cbxTriggerEdge   = null!;
    private ComboBox  _cbxPalette       = null!;
    private Label     _lblMaxFps        = null!;

    public SettingsPanel(AppSettings settings)
    {
        _settings    = settings;
        AutoScroll   = true;
        Width        = 280;
        BackColor    = Color.FromArgb(30, 30, 30);
        ForeColor    = Color.WhiteSmoke;
        BuildUI();
        LoadFromSettings();
    }

    // ── UI construction ───────────────────────────────────────────────────

    private void BuildUI()
    {
        var layout = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 1,
            AutoSize    = true,
            AutoSizeMode= AutoSizeMode.GrowAndShrink,
            Padding     = new Padding(8),
        };
        Controls.Add(layout);

        void AddSection(string title)
        {
            var lbl = new Label
            {
                Text      = title,
                Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 180, 255),
                AutoSize  = true,
                Margin    = new Padding(0, 10, 0, 2),
            };
            layout.Controls.Add(lbl);
        }

        Label AddLabel(string text)
        {
            var l = new Label { Text = text, AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
            layout.Controls.Add(l);
            return l;
        }

        TextBox AddTextBox(string? value = null)
        {
            var t = new TextBox
            {
                Text      = value ?? "",
                Dock      = DockStyle.Fill,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.FixedSingle,
            };
            layout.Controls.Add(t);
            return t;
        }

        NumericUpDown AddNumeric(decimal min, decimal max, decimal val, decimal increment = 0.01m, int decimals = 2)
        {
            var n = new NumericUpDown
            {
                Minimum        = min,
                Maximum        = max,
                Value          = val,
                Increment      = increment,
                DecimalPlaces  = decimals,
                Dock           = DockStyle.Fill,
                BackColor      = Color.FromArgb(50, 50, 50),
                ForeColor      = Color.WhiteSmoke,
            };
            layout.Controls.Add(n);
            return n;
        }

        Button AddButton(string text, EventHandler click)
        {
            var b = new Button
            {
                Text      = text,
                Dock      = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 80),
                ForeColor = Color.WhiteSmoke,
                Height    = 28,
                Margin    = new Padding(0, 2, 0, 2),
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 120);
            b.Click += click;
            layout.Controls.Add(b);
            return b;
        }

        // ── Paths ──────────────────────────────────────────────────────────
        AddSection("📁 Save Location");
        AddLabel("Save Directory:");
        _txSaveDir = AddTextBox();
        AddButton("Browse...", (_, _) => BrowseSaveDirectoryRequested?.Invoke());

        AddLabel("Session Prefix:");
        _txSessionPrefix = AddTextBox();

        // ── Camera ─────────────────────────────────────────────────────────
        AddSection("📷 Camera");
        AddLabel("Camera XML Config:");
        _txCameraXml = AddTextBox();
        AddButton("Browse...", (_, _) => BrowseCameraXmlRequested?.Invoke());
        AddButton("Connect Camera", (_, _) => ConnectRequested?.Invoke());

        AddLabel("Emissivity (0.01–1.00):");
        _nudEmissivity = AddNumeric(0.01m, 1.00m, 1.00m, 0.01m, 2);

        AddLabel("Ambient Temp (°C):");
        _nudAmbient = AddNumeric(-40, 120, 20, 0.5m, 1);

        AddLabel("Transmitted Temp (°C):");
        _nudTransmitted = AddNumeric(-40, 120, 20, 0.5m, 1);

        // ── Display ────────────────────────────────────────────────────────
        AddSection("🖥 Display");
        AddLabel("Color Palette:");
        _cbxPalette = new ComboBox
        {
            Dock          = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor     = Color.FromArgb(50, 50, 50),
            ForeColor     = Color.WhiteSmoke,
        };
        _cbxPalette.Items.AddRange(Enum.GetNames<ThermalPalette>());
        layout.Controls.Add(_cbxPalette);

        AddLabel("Display FPS (1–32):");
        _nudDisplayFps = AddNumeric(1, AppSettings.MaxAllowedDisplayFps, 10, 1, 0);
        _lblMaxFps = AddLabel($"(Hardware limit: {AppSettings.MaxAllowedDisplayFps} Hz)");
        _lblMaxFps.ForeColor = Color.Gray;

        _chkAutoRange = new CheckBox { Text = "Auto Temperature Range", AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
        layout.Controls.Add(_chkAutoRange);
        _chkAutoRange.CheckedChanged += (_, _) => UpdateRangeControlState();

        AddLabel("Range Min (°C):");
        _nudRangeMin = AddNumeric(-100, 1000, 20, 1, 1);

        AddLabel("Range Max (°C):");
        _nudRangeMax = AddNumeric(-100, 1000, 80, 1, 1);

        // ── Trigger ────────────────────────────────────────────────────────
        AddSection("⚡ NI-DAQ Trigger");
        _chkTrigger = new CheckBox { Text = "Enable External Trigger", AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
        layout.Controls.Add(_chkTrigger);
        _chkTrigger.CheckedChanged += (_, _) => UpdateTriggerControlState();

        AddLabel("DAQ Device (e.g. Dev1):");
        _txDaqDevice = AddTextBox("Dev1");

        AddLabel("Digital Line (e.g. port0/line0):");
        _txDaqLine = AddTextBox("port0/line0");

        AddLabel("Trigger Edge:");
        _cbxTriggerEdge = new ComboBox
        {
            Dock          = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor     = Color.FromArgb(50, 50, 50),
            ForeColor     = Color.WhiteSmoke,
        };
        _cbxTriggerEdge.Items.AddRange(Enum.GetNames<TriggerEdge>());
        layout.Controls.Add(_cbxTriggerEdge);

        // ── Apply ──────────────────────────────────────────────────────────
        AddSection("");
        AddButton("Apply Settings", (_, _) => ApplySettings());

        UpdateRangeControlState();
        UpdateTriggerControlState();
    }

    // ── Settings sync ─────────────────────────────────────────────────────

    private void LoadFromSettings()
    {
        _txSaveDir.Text          = _settings.SaveDirectory;
        _txSessionPrefix.Text    = _settings.SessionPrefix;
        _txCameraXml.Text        = _settings.CameraXmlPath;
        _nudEmissivity.Value     = (decimal)_settings.Emissivity;
        _nudAmbient.Value        = (decimal)_settings.AmbientTemp;
        _nudTransmitted.Value    = (decimal)_settings.TransmittedTemp;
        _nudDisplayFps.Value     = _settings.DisplayFps;
        _chkAutoRange.Checked    = _settings.AutoRange;
        _nudRangeMin.Value       = (decimal)_settings.RangeMin;
        _nudRangeMax.Value       = (decimal)_settings.RangeMax;
        _chkTrigger.Checked      = _settings.TriggerEnabled;
        _txDaqDevice.Text        = _settings.NiDaqDeviceName;
        _txDaqLine.Text          = _settings.NiDaqDigitalLine;
        _cbxPalette.SelectedIndex    = (int)_settings.Palette;
        _cbxTriggerEdge.SelectedIndex= (int)_settings.TriggerEdge;
    }

    private void ApplySettings()
    {
        _settings.SaveDirectory    = _txSaveDir.Text.Trim();
        _settings.SessionPrefix    = _txSessionPrefix.Text.Trim();
        _settings.CameraXmlPath    = _txCameraXml.Text.Trim();
        _settings.Emissivity       = (double)_nudEmissivity.Value;
        _settings.AmbientTemp      = (double)_nudAmbient.Value;
        _settings.TransmittedTemp  = (double)_nudTransmitted.Value;
        _settings.DisplayFps       = (int)_nudDisplayFps.Value;
        _settings.AutoRange        = _chkAutoRange.Checked;
        _settings.RangeMin         = (double)_nudRangeMin.Value;
        _settings.RangeMax         = (double)_nudRangeMax.Value;
        _settings.TriggerEnabled   = _chkTrigger.Checked;
        _settings.NiDaqDeviceName  = _txDaqDevice.Text.Trim();
        _settings.NiDaqDigitalLine = _txDaqLine.Text.Trim();
        _settings.Palette          = (ThermalPalette)_cbxPalette.SelectedIndex;
        _settings.TriggerEdge      = (TriggerEdge)_cbxTriggerEdge.SelectedIndex;

        _settings.Validate();
        _settings.Save();
        SettingsChanged?.Invoke();
    }

    // Expose textboxes for MainForm directory-picker callbacks
    public void SetSaveDirectory(string path)   { _txSaveDir.Text   = path; }
    public void SetCameraXmlPath(string path)   { _txCameraXml.Text = path; }

    private void UpdateRangeControlState()
    {
        bool manual = !_chkAutoRange.Checked;
        _nudRangeMin.Enabled = manual;
        _nudRangeMax.Enabled = manual;
    }

    private void UpdateTriggerControlState()
    {
        bool en = _chkTrigger.Checked;
        _txDaqDevice.Enabled      = en;
        _txDaqLine.Enabled        = en;
        _cbxTriggerEdge.Enabled   = en;
    }
}
