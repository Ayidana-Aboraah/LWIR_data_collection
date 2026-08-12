using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using LWIR_app.models;
using LWIR_app.Sensor;

namespace LWIR_app.classes
{
    public sealed class PlaybackFrame : IDisposable
    {
        public PlaybackFrame(FrameRecord sourceFrame, Bitmap bitmap, float scaleMin, float scaleMax, float frameMin, float frameMax, float frameMean)
        {
            SourceFrame = sourceFrame;
            Bitmap = bitmap;
            ScaleMin = scaleMin;
            ScaleMax = scaleMax;
            FrameMin = frameMin;
            FrameMax = frameMax;
            FrameMean = frameMean;
        }

        public FrameRecord SourceFrame { get; }
        public Bitmap Bitmap { get; }
        public float ScaleMin { get; }
        public float ScaleMax { get; }
        public float FrameMin { get; }
        public float FrameMax { get; }
        public float FrameMean { get; }

        public void Dispose() => Bitmap.Dispose();
    }

    public static class PlaybackTool
    {
        private static readonly List<FrameRecord> frames = new();
        private static readonly object gate = new();
        private static CancellationTokenSource? playbackCancellation;
        public static int currentIndex;

        public static double FramesPerSecond { get; set; } = 10.0;
        public static bool IsPlaying { get; private set; }
        public static bool Active = false;
        public static int FrameCount => frames.Count;

        private static RegionOfInterest? roi;

        public static Action<PlaybackFrame>? FrameRendered;

        public static void UpdateROI(System.Windows.Point s, System.Windows.Point e) => roi = new RegionOfInterest(s, e, frames[currentIndex].width);

        public static void LoadFrames(IEnumerable<FrameRecord> newFrames)
        {
            lock (gate)
            {
                frames.Clear();
                frames.AddRange(newFrames);
                currentIndex = 0;
            }
        }
        
        public static void Resume() => IsPlaying = true;

        public static void Pause() => IsPlaying = false;

        public static FrameRecord? GetCurrentFrame()
        {
            if (frames.Count == 0) return null;

            currentIndex = Math.Clamp(currentIndex, 0, frames.Count - 1);
            return frames[currentIndex];
        }

        public static float findTemp(int x, int y)
        {
            if (frames.Count() == 0) return float.NaN;
            currentIndex = Math.Clamp(currentIndex, 0, frames.Count - 1);
            return frames[currentIndex].data[(y * frames[currentIndex].width) + x];
        }

        public static void SetPlaybackRate(double framesPerSecond) => FramesPerSecond = framesPerSecond;

        public static PlaybackFrame? RenderCurrentFrame()
        {
            FrameRecord? frame = GetCurrentFrame();
            return frame == null ? null : RenderFrame(frame);
        }

        public static PlaybackFrame RenderFrame(FrameRecord frame)
        {
            float[] temperatures = frame.data;
            if (temperatures == null || temperatures.Length == 0) throw new InvalidDataException("The playback frame does not contain any temperature data.");

            (float frameMin, float frameMax, float frameMean) = CalculateStatistics(temperatures);
            (float scaleMin, float scaleMax) = (frameMin, frameMax);

            if (Math.Abs(scaleMax - scaleMin) < float.Epsilon) scaleMax = scaleMin + 0.0001f;

            Bitmap bitmap = RenderBitmap(frame.width, frame.height, temperatures, scaleMin, scaleMax);
            return new PlaybackFrame(frame, bitmap, scaleMin, scaleMax, frameMin, frameMax, frameMean);
        }

        public static async Task PlayAsync(CancellationToken cancellationToken = default) => await PlayAsync(null, cancellationToken).ConfigureAwait(false);

        public static async Task PlayAsync(Action<PlaybackFrame>? frameHandler, CancellationToken cancellationToken = default)
        {
            if (frameHandler != null) FrameRendered += frameHandler;

            try
            {
                await PlayInternalAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (frameHandler != null) FrameRendered -= frameHandler;
            }
        }

        public static void Stop() => playbackCancellation?.Cancel();

        public static void Reset()
        {
            lock (gate) currentIndex = 0;
        }

        private static async Task PlayInternalAsync(CancellationToken cancellationToken)
        {
            CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            playbackCancellation = linkedCancellation;

            try
            {
                IsPlaying = true;

                while (true)
                {
                    PlaybackFrame? playbackFrame = null;

                    lock (gate)
                    {
                        if (frames.Count == 0 || currentIndex >= frames.Count)break;

                        playbackFrame = RenderFrame(frames[currentIndex]);
                        ++currentIndex;
                        Math.Clamp(currentIndex, 0, FrameCount-1);
                    }

                    if (playbackFrame == null) break;

                    try
                    {
                        FrameRendered?.Invoke(playbackFrame);
                    }
                    finally
                    {
                        playbackFrame.Dispose();
                    }

                    if (FramesPerSecond > 0)
                    {
                        TimeSpan delay = TimeSpan.FromSeconds(1.0 / FramesPerSecond);
                        await Task.Delay(delay, linkedCancellation.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                IsPlaying = false;
                playbackCancellation = null;
                linkedCancellation.Dispose();
            }
        }

        private static (float Min, float Max, float Mean) CalculateStatistics(float[] temperatures)
        {
            (float min, float max) = (float.MaxValue, float.MinValue);
            double sum = 0;

            foreach (float temperature in temperatures)
            {
                min = (temperature < min) ? temperature : min;
                max = (temperature > max) ? temperature : max;
                sum += temperature;
            }

            return (min, max, (float)(sum / temperatures.Length));
        }

        private static Bitmap RenderBitmap(int width, int height, float[] temperatures, float scaleMin, float scaleMax)
        {
            if (width <= 0 || height <= 0) throw new InvalidDataException("Cannot render a frame with empty dimensions.");

            if (temperatures.Length != checked(width * height)) throw new InvalidDataException("Temperature data length does not match the frame dimensions.");

            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            // WriteableBitmap wb = new WriteableBitmap(width, height, 70, 40, System.Windows.Media.PixelFormats.Rgb24, new BitmapPalette(imageBuilder.current_palette));
            Rectangle rectangle = new Rectangle(0, 0, width, height);
            // Int32Rect rect = new Int32Rect(0,0, width, height);
            BitmapData bitmapData = bitmap.LockBits(rectangle, ImageLockMode.WriteOnly, bitmap.PixelFormat);
            try
            {
                int stride = bitmapData.Stride;
                // int stride = wb.BackBufferStride;
                byte[] pixels = new byte[stride * height];

                for (int y = 0; y < height; y++)
                {
                    int sourceRowOffset = y * width;
                    int destinationRowOffset = y * stride;

                    for (int x = 0; x < width; x++)
                    {
                        (byte R, byte G, byte B) = PaletteTool.MapTemperatureToColor(temperatures[sourceRowOffset + x], scaleMin, scaleMax);
                        int pixelOffset = destinationRowOffset + (x * 3);

                        pixels[pixelOffset] = B;
                        pixels[pixelOffset + 1] = G;
                        pixels[pixelOffset + 2] = R;
                    }
                }

                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bitmapData.Scan0, pixels.Length);
                // wb.WritePixels(rect,pixels, stride, 0);
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            return bitmap;
            // return wb;
        }

    }
}