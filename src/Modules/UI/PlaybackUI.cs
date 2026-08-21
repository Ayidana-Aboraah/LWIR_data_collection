using System.Windows.Controls;
using ThermalCamerApp.classes;
using System.Windows;

namespace ThermalCamerApp.UI;

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

    Button play = new Button { Content = "Play" };

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

        playback_speed.ValueChanged += (_, _) => PlaybackTool.SetPlaybackRate(playback_speed.Value * 30);

        playback.ValueChanged += (_, _) => PlaybackTool.currentIndex = (int)playback.Value;
        
        stack.Children.Add(playback);
        stack.Children.Add(playback_speed);
        stack.Children.Add(play);

        Content = stack;
        Visibility = Visibility.Collapsed;
    }

    public void UpdateUI()
    {
        Visibility = PlaybackTool.Active ? Visibility.Visible : Visibility.Collapsed;
        playback.Maximum = PlaybackTool.FrameCount;
        playback.Value = Math.Clamp(PlaybackTool.currentIndex, 0, (int)playback.Maximum);
    }
}