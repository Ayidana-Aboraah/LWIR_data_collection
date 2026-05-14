using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace OptrisXiLogger.UI;

/// <summary>
/// Custom WinForms control that renders the live thermal image as a false-color bitmap,
/// draws a crosshair at the hotspot, and overlays temperature stats.
/// Rendering happens on the UI thread; the bitmap is built from the latest frame.
/// </summary>
public sealed class ThermalDisplayPanel : Control
{
    private AppSettings?         _settings;
    private Camera.ThermalFrame? _lastFrame;
    private Bitmap?              _displayBitmap;
    private readonly object      _bitmapLock = new();

    // Pre-built palette LUTs (256 entries, each a Color)
    private Color[] _palette = Array.Empty<Color>();

    // Scale range used when rendering (updated each frame if AutoRange)
    private float _rangeMin = 20f;
    private float _rangeMax = 80f;

    public ThermalDisplayPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint           |
            ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.Black;
    }

    public void Initialize(AppSettings settings)
    {
        _settings = settings;
        RebuildPalette();
    }

    /// <summary>
    /// Called by the display timer on the UI thread.
    /// Converts the latest frame to a false-color bitmap and invalidates.
    /// </summary>
    public void UpdateFrame(Camera.ThermalFrame frame)
    {
        _lastFrame = frame;

        // Update scale range
        if (_settings?.AutoRange == true)
        {
            _rangeMin = frame.MinTemp;
            _rangeMax = frame.MaxTemp;
        }
        else if (_settings != null)
        {
            _rangeMin = (float)_settings.RangeMin;
            _rangeMax = (float)_settings.RangeMax;
        }

        BuildBitmap(frame);
        Invalidate();
    }

    public void RebuildPalette()
    {
        _palette = _settings?.Palette switch
        {
            ThermalPalette.Rainbow   => BuildRainbowPalette(),
            ThermalPalette.Grayscale => BuildGrayscalePalette(),
            ThermalPalette.Hot       => BuildHotPalette(),
            _                        => BuildIronbowPalette()
        };
    }

    // ── Bitmap construction ───────────────────────────────────────────────

    private void BuildBitmap(Camera.ThermalFrame frame)
    {
        int w = frame.Width, h = frame.Height;

        var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        var bmpData = bmp.LockBits(new Rectangle(0, 0, w, h),
            ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

        int stride  = bmpData.Stride;
        var rawData = new byte[stride * h];
        float range = _rangeMax - _rangeMin;
        if (range < 0.1f) range = 0.1f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float tempC = Camera.ThermalFrame.RawToCelsius(frame.RawData[y * w + x]);
                float t     = Math.Clamp((tempC - _rangeMin) / range, 0f, 1f);
                int   idx   = (int)(t * 255);
                Color c     = _palette[idx];

                int pos = y * stride + x * 3;
                rawData[pos]     = c.B;
                rawData[pos + 1] = c.G;
                rawData[pos + 2] = c.R;
            }
        }

        Marshal.Copy(rawData, 0, bmpData.Scan0, rawData.Length);
        bmp.UnlockBits(bmpData);

        lock (_bitmapLock)
        {
            _displayBitmap?.Dispose();
            _displayBitmap = bmp;
        }
    }

    // ── Painting ──────────────────────────────────────────────────────────

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.Black);

        Bitmap? bmp;
        lock (_bitmapLock)
            bmp = _displayBitmap;

        if (bmp == null || _lastFrame == null)
        {
            using var font = new Font("Segoe UI", 12);
            g.DrawString("No camera signal", font, Brushes.Gray, 10, 10);
            return;
        }

        // Scale bitmap to fill control while preserving aspect ratio
        var destRect = ComputeLetterboxRect(bmp.Width, bmp.Height, Width, Height);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        g.DrawImage(bmp, destRect);

        // Draw hotspot crosshair
        DrawHotspot(g, _lastFrame, bmp.Width, bmp.Height, destRect);

        // Draw colorbar on the right
        DrawColorbar(g, destRect);

        // Draw stats overlay
        DrawStatsOverlay(g, _lastFrame);
    }

    private void DrawHotspot(Graphics g, Camera.ThermalFrame frame,
        int imgW, int imgH, Rectangle dest)
    {
        if (imgW == 0 || imgH == 0) return;

        float scaleX = (float)dest.Width  / imgW;
        float scaleY = (float)dest.Height / imgH;
        int   cx     = dest.Left + (int)(frame.HotspotX * scaleX);
        int   cy     = dest.Top  + (int)(frame.HotspotY * scaleY);
        int   arm    = 10;

        using var pen = new Pen(Color.White, 1.5f);
        // Crosshair
        g.DrawLine(pen, cx - arm, cy, cx + arm, cy);
        g.DrawLine(pen, cx, cy - arm, cx, cy + arm);
        // Circle
        g.DrawEllipse(pen, cx - 5, cy - 5, 10, 10);

        // Temperature label
        string label = $"{frame.MaxTemp:F1}°C";
        using var font = new Font("Consolas", 8, FontStyle.Bold);
        var sz   = g.MeasureString(label, font);
        var bg   = new RectangleF(cx + 7, cy - sz.Height / 2, sz.Width + 2, sz.Height);
        g.FillRectangle(Brushes.Black, bg);
        g.DrawString(label, font, Brushes.White, bg.Left + 1, bg.Top);
    }

    private void DrawColorbar(Graphics g, Rectangle imageRect)
    {
        int cbW   = 18;
        int cbH   = imageRect.Height;
        int cbX   = imageRect.Right + 4;
        int cbY   = imageRect.Top;

        if (cbX + cbW > Width) return;

        for (int py = 0; py < cbH; py++)
        {
            float t   = 1.0f - (float)py / cbH;
            int   idx = (int)(t * 255);
            using var pen = new Pen(_palette[idx]);
            g.DrawLine(pen, cbX, cbY + py, cbX + cbW, cbY + py);
        }

        using var font  = new Font("Consolas", 7);
        g.DrawString($"{_rangeMax:F0}°", font, Brushes.White, cbX, cbY);
        g.DrawString($"{_rangeMin:F0}°", font, Brushes.White, cbX, cbY + cbH - 12);
    }

    private static void DrawStatsOverlay(Graphics g, Camera.ThermalFrame frame)
    {
        string[] lines =
        {
            $"Min:  {frame.MinTemp,7:F2} °C",
            $"Max:  {frame.MaxTemp,7:F2} °C",
            $"Avg:  {frame.AvgTemp,7:F2} °C",
            $"Hot: ({frame.HotspotX},{frame.HotspotY})",
            $"#{frame.FrameIndex:D6}",
        };

        using var font = new Font("Consolas", 9, FontStyle.Bold);
        float lineH = font.Height + 2;
        float totalH = lineH * lines.Length + 8;
        float totalW = 150;

        var bg = new RectangleF(4, 4, totalW, totalH);
        g.FillRectangle(new SolidBrush(Color.FromArgb(160, 0, 0, 0)), bg);

        for (int i = 0; i < lines.Length; i++)
            g.DrawString(lines[i], font, Brushes.White, 8, 8 + i * lineH);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static Rectangle ComputeLetterboxRect(int imgW, int imgH, int ctrlW, int ctrlH)
    {
        float scale = Math.Min((float)ctrlW / imgW, (float)ctrlH / imgH);
        int   w     = (int)(imgW * scale);
        int   h     = (int)(imgH * scale);
        return new Rectangle((ctrlW - w) / 2, (ctrlH - h) / 2, w, h);
    }

    // ── Palettes ──────────────────────────────────────────────────────────

    private static Color[] BuildIronbowPalette()
    {
        var p = new Color[256];
        for (int i = 0; i < 256; i++)
        {
            float t = i / 255f;
            int r = (int)Math.Clamp(255 * (1.5f * t),          0, 255);
            int g = (int)Math.Clamp(255 * (2.0f * t - 0.8f),   0, 255);
            int b = (int)Math.Clamp(255 * (3.0f * t - 2.0f),   0, 255);
            p[i] = Color.FromArgb(r, g, b);
        }
        return p;
    }

    private static Color[] BuildRainbowPalette()
    {
        var p = new Color[256];
        for (int i = 0; i < 256; i++)
        {
            float h = (1f - i / 255f) * 240f;  // 240 (blue) → 0 (red)
            p[i] = HsvToRgb(h, 1f, 1f);
        }
        return p;
    }

    private static Color[] BuildGrayscalePalette()
    {
        var p = new Color[256];
        for (int i = 0; i < 256; i++) p[i] = Color.FromArgb(i, i, i);
        return p;
    }

    private static Color[] BuildHotPalette()
    {
        var p = new Color[256];
        for (int i = 0; i < 256; i++)
        {
            float t = i / 255f;
            int r   = (int)Math.Clamp(255 * t * 3f,        0, 255);
            int g   = (int)Math.Clamp(255 * (t * 3f - 1f), 0, 255);
            int b   = (int)Math.Clamp(255 * (t * 3f - 2f), 0, 255);
            p[i] = Color.FromArgb(r, g, b);
        }
        return p;
    }

    private static Color HsvToRgb(float h, float s, float v)
    {
        if (s == 0f) { int g = (int)(v * 255); return Color.FromArgb(g, g, g); }
        float hh = h / 60f;
        int   ii = (int)hh;
        float ff = hh - ii;
        float p  = v * (1f - s);
        float q  = v * (1f - s * ff);
        float t  = v * (1f - s * (1f - ff));
        return ii switch
        {
            0 => Rgb(v, t, p),
            1 => Rgb(q, v, p),
            2 => Rgb(p, v, t),
            3 => Rgb(p, q, v),
            4 => Rgb(t, p, v),
            _ => Rgb(v, p, q)
        };

        static Color Rgb(float r, float g, float b) =>
            Color.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_bitmapLock)
            {
                _displayBitmap?.Dispose();
                _displayBitmap = null;
            }
        }
        base.Dispose(disposing);
    }
}
