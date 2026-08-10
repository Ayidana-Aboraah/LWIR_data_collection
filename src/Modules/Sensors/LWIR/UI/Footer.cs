using LWIR_app.models;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using WpfBrushes = System.Windows.Media.Brushes;

namespace LWIR_app.Sensor.LWIR.UI;

public class LWIR_Footer : Border
{
    IRImagerShow LWIR;

    private TextBlock sbOperationMode = new TextBlock
    {
        Text = string.Empty,
        VerticalAlignment = VerticalAlignment.Center,
        Padding = new Thickness(5, 0, 5, 0)
    };

    private TextBlock sbFlag = new TextBlock
    {
        Text = "                  ",
        VerticalAlignment = VerticalAlignment.Center,
        Padding = new Thickness(5, 0, 5, 0)
    };

    private TextBlock sbFPS = new TextBlock
    {
        Text = "                  ",
        VerticalAlignment = VerticalAlignment.Center,
        Padding = new Thickness(5, 0, 5, 0),
        TextAlignment = TextAlignment.Right
    };

    public LWIR_Footer(IRImagerShow LWIR)
    {
        this.LWIR = LWIR;

        Background = WpfBrushes.Gainsboro;
        BorderBrush = WpfBrushes.Gray;
        BorderThickness = new Thickness(1, 1, 0, 0);
        Padding = new Thickness(6, 4, 6, 4);

        var footerGrid = new Grid();
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(sbOperationMode, 0);
        Grid.SetColumn(sbFlag, 1);
        Grid.SetColumn(sbFPS, 2);
        footerGrid.Children.Add(sbOperationMode);
        footerGrid.Children.Add(sbFlag);
        footerGrid.Children.Add(sbFPS);

        Child = footerGrid;
    }

    public void UpdateUI()
    {
        sbOperationMode.Text = LWIR.OperationModeString;
        sbFlag.Text = LWIR.GetFlagState();
        sbFPS.Text = LWIR.GetFPS().ToString("N1", CultureInfo.CurrentCulture) + " Hz";
    }

    public void Disable()
    {
        sbOperationMode.Text = string.Empty;
        sbFlag.Text = string.Format(CultureInfo.CurrentCulture, "{0, 18}", " ");
        sbFPS.Text = string.Format(CultureInfo.CurrentCulture, "{0, 11}", " ");
    }
}