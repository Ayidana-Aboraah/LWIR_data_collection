using System.Windows;
using System.Windows.Controls;
using LWIR_app.classes;

namespace LWIR_app.UI;

public class PlaybackGroup : GroupBox
{
    Slider playback = new Slider
    {
        Value = 0,
        Minimum = 0,
        Maximum = 1,
        TickFrequency = 1,
        IsSnapToTickEnabled = true,
        Width = 200
    };

    Slider playback_speed = new Slider
    {
        Value = 1,
        Minimum = .25,
        Maximum = 2,
        TickFrequency = .25,
        IsSnapToTickEnabled = true,
        Width = 200
    };

    Button play = new Button
    {
        Content = "Play"
    };

    public PlaybackGroup()
    {
        Header = "Playback";
        StackPanel stack = new StackPanel { Margin = new Thickness(8) };

        play.Click += (_,_) =>
        {
            if (PlaybackTool.IsPlaying)
            {
                play.Content = "Play";
                PlaybackTool.Stop();
            }
            else
            {
                play.Content = "Pause";
                Task.Run(() => PlaybackTool.PlayAsync());
            }
        };

        playback_speed.ValueChanged += (_, _) =>
        {
            PlaybackTool.SetPlaybackRate(playback_speed.Value * 30);
        };

        playback.ValueChanged += (_, _) =>
        {
            PlaybackTool.currentIndex = (int) playback.Value;
        };
        
        stack.Children.Add(play);
        stack.Children.Add(playback);
        stack.Children.Add(playback_speed);
        Content = stack;
        Visibility = Visibility.Collapsed;
    }

    public void UpdateUI()
    {
        Visibility = PlaybackTool.Active ? Visibility.Visible : Visibility.Collapsed;
        playback.Maximum = PlaybackTool.FrameCount;
    }
}