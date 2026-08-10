using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using LWIR_app.classes;
using WpfBrushes = System.Windows.Media.Brushes;
using LWIR_app.Sensor;

namespace LWIR_app.UI;

public class Display
{
    bool isDraggingRoi;
    bool hasRoi;
    System.Windows.Point mouse_position;
    System.Windows.Point roiDragStart, roiDragCurrent;
    Rectangle selectedRoi;
    int currentImageWidth, currentImageHeight;
    RecorderBase recorder;
    public Border thermalBorder;

    public DockPanel baseDisplay = new DockPanel();


    private System.Windows.Controls.Image roiPreviewImage = new System.Windows.Controls.Image
    {
        Height = 170,
        Stretch = Stretch.Uniform,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Center,
        SnapsToDevicePixels = true
    };

    private TextBlock roiPreviewInfo = null!;

    System.Windows.Controls.Image displayImage = new System.Windows.Controls.Image
    {
        Stretch = Stretch.Uniform,
        SnapsToDevicePixels = true,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch
    };

    public Display(RecorderBase recorder)
    {
        this.recorder = recorder;
        RenderOptions.SetBitmapScalingMode(displayImage, BitmapScalingMode.HighQuality);
        displayImage.MouseLeftButtonDown += ThermalImage_MouseLeftButtonDown;
        displayImage.MouseMove += ThermalImage_MouseMove;
        displayImage.MouseLeftButtonUp += ThermalImage_MouseLeftButtonUp;
        displayImage.MouseRightButtonDown += ThermalImage_MouseRightButtonDown;

        thermalBorder = new Border
        {
            Background = WpfBrushes.DimGray,
            Child = displayImage,
        };

        DockPanel.SetDock(recorder.sensor.Footer(), Dock.Bottom);
        baseDisplay.Children.Add(thermalBorder);
    }

    public void Disable()
    {
        displayImage.Source = null;
        roiPreviewImage.Source = null;
        roiPreviewInfo.Text = "No ROI selected";
        hasRoi = false;
        isDraggingRoi = false;
        currentImageWidth = 0;
        currentImageHeight = 0;
    }

    public void UpdateUI()
    {
        Bitmap? image = PlaybackTool.Active ? PlaybackTool.RenderFrame(PlaybackTool.GetCurrentFrame()).Bitmap : recorder.sensor.Render();
        if (image == null) return;

        currentImageWidth = image.Width;
        currentImageHeight = image.Height;

        if (TryGetMouseImagePixel(out System.Drawing.Point cursorPixel))
            DrawMeasurement(
                image,
                cursorPixel.X,
                cursorPixel.Y,
                PlaybackTool.Active ? PlaybackTool.findTemp(cursorPixel.X, cursorPixel.Y) : recorder.sensor.findValue(cursorPixel.X, cursorPixel.Y),
                System.Drawing.Color.Red,
                System.Drawing.Color.White);


        UpdateRoiPreview(image);

        DrawRoiOverlay(image);

        displayImage.Source = ConvertBitmapToSource(image);
        image.Dispose();

    }

    private void ThermalImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if ((!recorder.Connected() && !PlaybackTool.Active) || currentImageWidth <= 0 || currentImageHeight <= 0 || recorder.recording) return;

        System.Windows.Point cursor = e.GetPosition(displayImage);
        if (!IsPointInsideImageViewport(cursor)) return;

        isDraggingRoi = true;
        roiDragStart = cursor;
        roiDragCurrent = cursor;
        displayImage.CaptureMouse();
        e.Handled = true;
    }

    private void ThermalImage_MouseMove(object sender, MouseEventArgs e)
    {
        mouse_position = e.GetPosition(displayImage);
        if (!isDraggingRoi) return;
        roiDragCurrent = e.GetPosition(displayImage);
    }

    private void ThermalImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!isDraggingRoi) return;

        roiDragCurrent = e.GetPosition(displayImage);
        isDraggingRoi = false;
        displayImage.ReleaseMouseCapture();

        if (TryBuildImageRectangle(roiDragStart, roiDragCurrent, out Rectangle rectangle))
        {
            selectedRoi = rectangle;
            System.Windows.Point start = new System.Windows.Point(rectangle.Left, rectangle.Top);
            System.Windows.Point end = new System.Windows.Point(rectangle.Right - 1, rectangle.Bottom - 1);
            hasRoi = true;
            if (PlaybackTool.Active) PlaybackTool.UpdateROI(start, end);
            else recorder.sensor.UpdateROI(start, end);
        }
        else
        {
            hasRoi = false;
            recorder.sensor.ClearROI();
        }

        e.Handled = true;
    }

    private void ThermalImage_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        hasRoi = false;
        isDraggingRoi = false;
        recorder.sensor.ClearROI();
        displayImage.ReleaseMouseCapture();
        e.Handled = true;
    }

    private bool IsPointInsideImageViewport(System.Windows.Point point)
    {
        Rect viewport = GetImageViewport(currentImageWidth, currentImageHeight);
        return viewport.Contains(point);
    }

    private bool TryGetMouseImagePixel(out System.Drawing.Point imagePixel)
    {
        imagePixel = default;

        if (currentImageWidth <= 0 || currentImageHeight <= 0) return false;

        Rect viewport = GetImageViewport(currentImageWidth, currentImageHeight);
        if (viewport.IsEmpty || !viewport.Contains(mouse_position)) return false;

        imagePixel = MapDisplayToImagePixel(mouse_position, viewport);
        return true;
    }

    private Rect GetImageViewport(int imageWidth, int imageHeight)
    {
        (double controlWidth, double controlHeight) = (displayImage.ActualWidth, displayImage.ActualHeight);

        if (controlWidth <= 0 || controlHeight <= 0 || imageWidth <= 0 || imageHeight <= 0) return Rect.Empty;

        double imageAspect = (double)imageWidth / imageHeight;
        double controlAspect = controlWidth / controlHeight;

        if (controlAspect > imageAspect)
        {
            double scaledWidth = controlHeight * imageAspect;
            double offsetX = (controlWidth - scaledWidth) / 2.0;
            return new Rect(offsetX, 0, scaledWidth, controlHeight);
        }

        double scaledHeight = controlWidth / imageAspect;
        double offsetY = (controlHeight - scaledHeight) / 2.0;
        return new Rect(0, offsetY, controlWidth, scaledHeight);
    }

    private bool TryBuildImageRectangle(System.Windows.Point start, System.Windows.Point end, out Rectangle rectangle)
    {
        rectangle = Rectangle.Empty;

        Rect viewport = GetImageViewport(currentImageWidth, currentImageHeight);
        if (viewport.IsEmpty) return false;

        System.Windows.Point startClamped = ClampToRect(start, viewport);
        System.Windows.Point endClamped = ClampToRect(end, viewport);

        System.Drawing.Point startPixel = MapDisplayToImagePixel(startClamped, viewport);
        System.Drawing.Point endPixel = MapDisplayToImagePixel(endClamped, viewport);

        int left = Math.Min(startPixel.X, endPixel.X);
        int right = Math.Max(startPixel.X, endPixel.X);
        int top = Math.Min(startPixel.Y, endPixel.Y);
        int bottom = Math.Max(startPixel.Y, endPixel.Y);

        if (right - left < 2 || bottom - top < 2) return false;

        rectangle = new Rectangle(left, top, right - left + 1, bottom - top + 1);
        return true;
    }

    private static System.Windows.Point ClampToRect(System.Windows.Point point, Rect rect)
    {
        if (rect.IsEmpty) return point;

        double clampedX = Math.Max(rect.Left, Math.Min(point.X, rect.Right));
        double clampedY = Math.Max(rect.Top, Math.Min(point.Y, rect.Bottom));

        return new System.Windows.Point(clampedX, clampedY);
    }

    private System.Drawing.Point MapDisplayToImagePixel(System.Windows.Point point, Rect viewport)
    {
        double normalizedX = (point.X - viewport.X) / viewport.Width;
        double normalizedY = (point.Y - viewport.Y) / viewport.Height;

        normalizedX = Math.Max(0.0, Math.Min(1.0, normalizedX));
        normalizedY = Math.Max(0.0, Math.Min(1.0, normalizedY));

        int pixelX = (int)Math.Round(normalizedX * (currentImageWidth - 1));
        int pixelY = (int)Math.Round(normalizedY * (currentImageHeight - 1));

        return new System.Drawing.Point(pixelX, pixelY);
    }

    private void DrawRoiOverlay(Bitmap bitmap)
    {
        if (hasRoi) DrawRectangleOverlay(bitmap, selectedRoi, System.Drawing.Color.Lime, 2f);

        if (isDraggingRoi && TryBuildImageRectangle(roiDragStart, roiDragCurrent, out Rectangle preview))
            DrawRectangleOverlay(bitmap, preview, System.Drawing.Color.Yellow, 1.5f);
    }

    private void UpdateRoiPreview(Bitmap sourceImage)
    {
        if (!hasRoi)
        {
            roiPreviewImage.Source = null;
            roiPreviewInfo.Text = "No ROI selected";
            return;
        }

        Rectangle imageBounds = new Rectangle(0, 0, sourceImage.Width, sourceImage.Height);
        Rectangle roi = Rectangle.Intersect(selectedRoi, imageBounds);

        if (roi.Width < 2 || roi.Height < 2)
        {
            roiPreviewImage.Source = null;
            roiPreviewInfo.Text = "No ROI selected";
            return;
        }

        using Bitmap roiBitmap = sourceImage.Clone(roi, sourceImage.PixelFormat);
        roiPreviewImage.Source = ConvertBitmapToSource(roiBitmap);
        roiPreviewInfo.Text = string.Format(CultureInfo.CurrentCulture, "{0} x {1} px", roi.Width, roi.Height);
    }

    private static void DrawRectangleOverlay(Bitmap bitmap, Rectangle rectangle, System.Drawing.Color color, float thickness)
    {
        if (rectangle.Width < 2 || rectangle.Height < 2) return;

        using Graphics graphics = Graphics.FromImage(bitmap);
        using System.Drawing.Pen borderPen = new System.Drawing.Pen(color, thickness)
        {
            DashStyle = System.Drawing.Drawing2D.DashStyle.Dash
        };
        using System.Drawing.Brush fillBrush = new SolidBrush(System.Drawing.Color.FromArgb(45, color));

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.FillRectangle(fillBrush, rectangle);
        graphics.DrawRectangle(borderPen, rectangle);
    }

    private void DrawMeasurement(Bitmap bitmap, int x, int y, float value, System.Drawing.Color fgColor, System.Drawing.Color bgColor)
    {
        int markerSize = 20;
        int markerSizeHalf = markerSize / 2;

        using Graphics graphics = Graphics.FromImage(bitmap);
        using GraphicsPath path = new GraphicsPath(FillMode.Winding);
        using System.Drawing.Brush fgBrush = new SolidBrush(fgColor);
        using System.Drawing.Pen fgPen = new System.Drawing.Pen(fgBrush, 1);
        using System.Drawing.Pen bgPen = new System.Drawing.Pen(bgColor, 3);

        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        graphics.DrawLine(bgPen, x - markerSizeHalf, y, x + markerSizeHalf, y);
        graphics.DrawLine(bgPen, x, y - markerSizeHalf, x, y + markerSizeHalf);

        graphics.DrawLine(fgPen, x - markerSizeHalf, y, x + markerSizeHalf, y);
        graphics.DrawLine(fgPen, x, y - markerSizeHalf, x, y + markerSizeHalf);

        path.AddString(
            string.Format(CultureInfo.CurrentCulture, "{0:N1}", value),
            System.Drawing.SystemFonts.DefaultFont.FontFamily,
            (int)System.Drawing.FontStyle.Regular,
            12,
            new System.Drawing.Point(x + markerSizeHalf / 2, y - markerSizeHalf * 2),
            StringFormat.GenericDefault);

        graphics.DrawPath(bgPen, path);
        graphics.FillPath(fgBrush, path);
    }


    public GroupBox BuildRoiPreviewGroup()
    {
        var stack = new StackPanel { Margin = new Thickness(8) };

        RenderOptions.SetBitmapScalingMode(roiPreviewImage, BitmapScalingMode.HighQuality);

        var imageHost = new Border
        {
            Background = WpfBrushes.Black,
            BorderBrush = WpfBrushes.Gray,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4),
            Child = roiPreviewImage
        };

        roiPreviewInfo = new TextBlock
        {
            Text = "No ROI selected",
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = WpfBrushes.DimGray
        };

        stack.Children.Add(imageHost);
        stack.Children.Add(roiPreviewInfo);

        return new GroupBox
        {
            Header = "Region Of Interest",
            Margin = new Thickness(0, 0, 0, 12),
            Content = stack
        };
    }


    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private static BitmapSource ConvertBitmapToSource(Bitmap bitmap)
    {
        IntPtr hBitmap = bitmap.GetHbitmap();
        try
        {
            BitmapSource source = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            source.Freeze();
            return source;
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

}