using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ThermalCamerApp.models;
using ThermalCamerApp.Camera;

namespace ThermalCamerApp.classes
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
            (float min, float max, float mean) = CalculateStatistics(frame.data);
            Bitmap bitmap = PaletteTool.Render(frame.data, min, max, frame.width, frame.height);
            return new PlaybackFrame(frame, bitmap, min, max, min, max, mean);
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

        public static (float Min, float Max, float Mean) CalculateStatistics(float[] temperatures)
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

    }
}