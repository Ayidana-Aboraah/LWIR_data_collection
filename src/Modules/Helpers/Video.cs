using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using FFMediaToolkit.Encoding;
using ThermalCamerApp.models;

namespace ThermalCamerApp.classes;

public static class VideoHelper
{
    public static void LoadFramesAsVideo(int width, int height, string output_path)
    {
        if (string.IsNullOrWhiteSpace(output_path))
            throw new ArgumentException("Output path cannot be empty.", nameof(output_path));

        string outputFile = Path.Combine(output_path, "out.mp4");

        VideoEncoderSettings settings = new VideoEncoderSettings(width: width, height: height, codec: VideoCodec.H264)
        {
            EncoderPreset = EncoderPreset.UltraFast,
            // CRF = 17,
        };

        using var file = MediaBuilder.CreateContainer(outputFile).WithVideo(settings).Create();

        for (int i = 0; i < PlaybackTool.FrameCount; i += (int)PlaybackTool.FramesPerSecond)
        {
            using var bitmap = PaletteTool.Render(PlaybackTool.frames[Math.Clamp(i, 0, PlaybackTool.FrameCount-1)]);

            Rectangle rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData bitmapData = bitmap.LockBits(rectangle, ImageLockMode.ReadOnly, bitmap.PixelFormat);

            try
            {
                int stride = Math.Abs(bitmapData.Stride);
                int byteCount = stride * bitmap.Height;
                byte[] image = new byte[byteCount];

                Marshal.Copy(bitmapData.Scan0, image, 0, byteCount);

                file.Video.AddFrame(new FFMediaToolkit.Graphics.ImageData(
                    image,
                    FFMediaToolkit.Graphics.ImagePixelFormat.Bgr24,
                    new Size(bitmap.Width, bitmap.Height)));
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
        }
    }
}