using System.Windows;
using System.Windows.Controls;
using LWIR_app.models;

namespace LWIR_app.Sensor.LWIR;

public class CameraSettingsGroup
{
    IRImagerShow LWIR;
    
    public CameraSettingsGroup(IRImagerShow LWIR)
    {
        this.LWIR = LWIR;
    }

    GroupBox foucsGroup()
    {
        float focus = LWIR.Imager.getFocusMotorPosition();

        GroupBox fs = new GroupBox { Header = "Focus Settings" };
        StackPanel st = new StackPanel { };
        Slider FocusSlider = new Slider
        {
            Maximum = 100,
            TickFrequency = 1,
            Width = 200,
            IsSnapToTickEnabled = true,
            Value = focus
        };

        TextBlock FocusText = new TextBlock
        {
            Text = focus.ToString(),
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(5, 0, 5, 0)
        };

        st.Children.Add(FocusText);
        st.Children.Add(FocusSlider);

        FocusSlider.ValueChanged += (_, _) =>
        {
            focus = (float)FocusSlider.Value;
            FocusText.Text = focus.ToString();
            LWIR.Imager.setFocusMotorPosition(focus);
        };

        fs.Content = st;
        return fs;
    }
}