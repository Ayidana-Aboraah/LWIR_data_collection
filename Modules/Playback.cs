using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using LWIR_app.models;
using Optris.OtcSdk;

namespace LWIR_app.classes
{
 
    public sealed class PlaybackFrame : IDisposable
    {
        public PlaybackFrame(RecordedFrame sourceFrame, Bitmap bitmap, float scaleMin, float scaleMax, float frameMin, float frameMax, float frameMean)
        {
            SourceFrame = sourceFrame;
            Bitmap = bitmap;
            ScaleMin = scaleMin;
            ScaleMax = scaleMax;
            FrameMin = frameMin;
            FrameMax = frameMax;
            FrameMean = frameMean;
        }

        public RecordedFrame SourceFrame { get; }
        public Bitmap Bitmap { get; }
        public float ScaleMin { get; }
        public float ScaleMax { get; }
        public float FrameMin { get; }
        public float FrameMax { get; }
        public float FrameMean { get; }

        public void Dispose() => Bitmap.Dispose();
    }

    public sealed class PlaybackTool
    {
        private readonly List<RecordedFrame> frames = new();
        private readonly object gate = new();
        private CancellationTokenSource? playbackCancellation;
        private int currentIndex;

        public double FramesPerSecond { get; set; } = 10.0;
        public bool IsPlaying { get; private set; }
        public int CurrentIndex => currentIndex;
        public int FrameCount => frames.Count;

        public Action<PlaybackFrame>? FrameRendered;

        CustomImageBuilder imageBuilder;

        public PlaybackTool(CustomImageBuilder imager)
        {
            imageBuilder = imager;
        }

        public void Load(string path) => LoadFrames(BinaryLoader.LoadFrameSet(path));

        public void LoadFrames(IEnumerable<RecordedFrame> newFrames)
        {
            lock (gate)
            {
                frames.Clear();
                frames.AddRange(newFrames);
                currentIndex = 0;
            }
        }

        public RecordedFrame? GetCurrentFrame()
        {
            lock (gate)
            {
                if (frames.Count == 0) return null;

                currentIndex = Math.Clamp(currentIndex, 0, frames.Count - 1);
                return frames[currentIndex];
            }
        }

        public void SetPlaybackRate(double framesPerSecond) => FramesPerSecond = framesPerSecond;

        public PlaybackFrame? RenderCurrentFrame()
        {
            RecordedFrame? frame = GetCurrentFrame();
            return frame == null ? null : RenderFrame(frame);
        }

        public PlaybackFrame RenderFrame(RecordedFrame frame)
        {
            float[] temperatures = frame.temperatures;
            if (temperatures == null || temperatures.Length == 0) throw new InvalidDataException("The playback frame does not contain any temperature data.");

            (float frameMin, float frameMax, float frameMean) = CalculateStatistics(temperatures);
            (float scaleMin, float scaleMax) =  (frameMin, frameMax);

            if (Math.Abs(scaleMax - scaleMin) < float.Epsilon) scaleMax = scaleMin + 0.0001f;

            Bitmap bitmap = RenderBitmap(frame.width, frame.height, temperatures, scaleMin, scaleMax);
            return new PlaybackFrame(frame, bitmap, scaleMin, scaleMax, frameMin, frameMax, frameMean);
        }

        public async Task PlayAsync(CancellationToken cancellationToken = default) => await PlayAsync(null, cancellationToken).ConfigureAwait(false);

        public async Task PlayAsync(Action<PlaybackFrame>? frameHandler, CancellationToken cancellationToken = default)
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

        public void Stop() => playbackCancellation?.Cancel();

        public void Reset()
        {
            lock (gate) currentIndex = 0;
        }

        private async Task PlayInternalAsync(CancellationToken cancellationToken)
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
                        if (frames.Count == 0 || currentIndex >= frames.Count) break;

                        playbackFrame = RenderFrame(frames[currentIndex]);
                        currentIndex++;
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
            catch (OperationCanceledException){}
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
                max = (temperature > max) ? temperature: max;
                sum += temperature;
            }

            return (min, max, (float)(sum / temperatures.Length));
        }

        private Bitmap RenderBitmap(int width, int height, float[] temperatures, float scaleMin, float scaleMax)
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
                        (byte R, byte G, byte B) = imageBuilder.MapTemperatureToColor(temperatures[sourceRowOffset + x], scaleMin, scaleMax);
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