using System.Windows.Controls;
using System.Windows;
using System.Globalization;
using LWIR_app.models;

public class TemperatureScalingGroup : GroupBox
{
    private IRImagerShow imagerShow;
    // private CheckBox autoTempScale = new CheckBox
    // {
    //     Content = "Automatic Temperature Scale",
    //     IsChecked = true,
    //     Margin = new Thickness(0, 16, 0, 0)
    // };

    private bool suppressScaleTextEvents;

    private TextBox imageScaleLow = null!;
    private TextBox imageScaleHigh = null!;
    private TextBlock minTemp = null!;
    private TextBlock maxTemp = null!;

    private RadioButton[] opModes = null!;

    public TemperatureScalingGroup(IRImagerShow imagerShow)
    {
        var layout = new Grid { Margin = new Thickness(8) };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var valueStack = new StackPanel { Orientation = Orientation.Vertical };
        valueStack.Children.Add(BuildTemperatureValueRow("Max:", out maxTemp));
        valueStack.Children.Add(BuildTemperatureValueRow("Min:", out minTemp, 10));

        this.imagerShow = imagerShow;

        // autoTempScale.Checked += (_, _) => AutoTempScale_CheckedChanged();
        // autoTempScale.Unchecked += (_, _) => AutoTempScale_CheckedChanged();
        // valueStack.Children.Add(autoTempScale);

        Grid.SetColumn(valueStack, 0);
        layout.Children.Add(valueStack);

        var opGroup = new GroupBox
        {
            Header = "Operation Mode",
            Margin = new Thickness(10, 0, 0, 0),
            Padding = new Thickness(6)
        };

        var opStack = new StackPanel { Orientation = Orientation.Vertical };
        opModes = [
            BuildOperationModeRadio(" -20°C–100°C", 0),
                BuildOperationModeRadio("0°C–250°C", 1),
                BuildOperationModeRadio("250°C–900°C", 2)
        ];

        foreach (RadioButton opMode in opModes) opStack.Children.Add(opMode);
        opGroup.Content = opStack;

        Grid.SetColumn(opGroup, 1);
        layout.Children.Add(opGroup);

        this.Content = layout;
    }

    public void UpdateUI()
    {
        minTemp.Text = imagerShow.MinRegion.temperature.ToString("N2", CultureInfo.CurrentCulture);
        maxTemp.Text = imagerShow.MaxRegion.temperature.ToString("N2", CultureInfo.CurrentCulture);

        // if (autoTempScale.IsChecked == true) SetAutoScalingRange();
    }


    private GroupBox BuildScaleGroup()
    {
        var group = new GroupBox
        {
            Header = "Scale",
            Margin = new Thickness(0, 0, 0, 12)
        };

        Grid panel = new Grid { Margin = new Thickness(8) };
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        FrameworkElement[] s = {
                BuildScaleRow("High:", out imageScaleHigh),
                BuildScaleRow("Low:", out imageScaleLow)
            };

        // TODO: Create a space between the elements

        for (int i = 0; i < s.Length; i++)
        {
            Grid.SetRow(s[i], 0);
            Grid.SetColumn(s[i], i);
            panel.Children.Add(s[i]);
        }

        group.Content = panel;
        return group;
    }

    private FrameworkElement BuildTemperatureValueRow(string labelText, out TextBlock valueText, double topMargin = 0)
    {
        var row = new DockPanel { Margin = new Thickness(0, topMargin, 0, 0) };

        var label = new TextBlock
        {
            Text = labelText,
            Width = 48,
            VerticalAlignment = VerticalAlignment.Center
        };

        valueText = new TextBlock
        {
            Text = "0.00",
            Width = 80,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        };

        var unit = new TextBlock
        {
            Text = "°C",
            VerticalAlignment = VerticalAlignment.Center
        };

        DockPanel.SetDock(label, Dock.Left);
        DockPanel.SetDock(unit, Dock.Right);

        row.Children.Add(label);
        row.Children.Add(valueText);
        row.Children.Add(unit);
        return row;
    }

    private RadioButton BuildOperationModeRadio(string text, int modeIndex)
    {
        var radio = new RadioButton
        {
            Content = text,
            Tag = modeIndex,
            Margin = new Thickness(0, 4, 0, 4),
        };
        radio.Checked += OperationMode_CheckedChanged;
        return radio;
    }

    // TODO: Finish this ting

    // private void AutoTempScale_CheckedChanged()
    // {
    //     bool autoTempScaleEnabled = autoTempScale.IsChecked == true;
    //     imageScaleHigh.IsReadOnly = autoTempScaleEnabled;
    //     imageScaleLow.IsReadOnly = autoTempScaleEnabled;
    //     imagerShow.SetAutoScaling(autoTempScaleEnabled);

    //     if (autoTempScaleEnabled) SetAutoScalingRange();
    //     else ApplyManualScaleRangeFromInputs();
    // }

    private FrameworkElement BuildScaleRow(string labelText, out TextBox textBox, double topMargin = 0)
    {
        var row = new DockPanel { Margin = new Thickness(0, topMargin, 0, 0) };

        textBox = new TextBox
        {
            Width = 72,
            Text = "0",
            HorizontalContentAlignment = HorizontalAlignment.Right,
            IsReadOnly = true,
            Margin = new Thickness(0, 0, 6, 0)
        };
        textBox.TextChanged += imageScale_TextChanged;

        var label = new TextBlock
        {
            Text = labelText,
            Width = 48,
            VerticalAlignment = VerticalAlignment.Center
        };

        var unit = new TextBlock
        {
            Text = "°C",
            VerticalAlignment = VerticalAlignment.Center
        };

        DockPanel.SetDock(label, Dock.Left);
        DockPanel.SetDock(unit, Dock.Right);

        row.Children.Add(label);
        row.Children.Add(textBox);
        row.Children.Add(unit);
        return row;
    }


    private void imageScale_TextChanged(object sender, TextChangedEventArgs e)
    {
        // if (suppressScaleTextEvents || autoTempScale.IsChecked == true) return;
        if (suppressScaleTextEvents) return;

        ApplyManualScaleRangeFromInputs();
    }

    private void ApplyManualScaleRangeFromInputs()
    {
        if (!TryReadScaleValue(imageScaleLow.Text, out float low)) return;

        if (!TryReadScaleValue(imageScaleHigh.Text, out float high)) return;

        if (low > high) (low, high) = (high, low);

        imagerShow.SetScaleRange(low, high);
    }

    private static bool TryReadScaleValue(string? text, out float value)
    {
        return float.TryParse(
            text,
            NumberStyles.Float,
            CultureInfo.CurrentCulture,
            out value);
    }

    private void OperationMode_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton radioButton || radioButton.IsChecked != true) return;

        if (radioButton.Tag is not int modeIndex) return;

        imagerShow.SetOperationMode(modeIndex);
        // SetAutoScalingRange();
    }

    // private void SetAutoScalingRange()
    // {
    //     if (!imagerShow.IsConnected || autoTempScale.IsChecked != true) return;

    //     var range = imagerShow.GetTemperatureRange();

    //     suppressScaleTextEvents = true;
    //     try
    //     {
    //         imageScaleLow.Text = ((int)range.Lower - 50).ToString(CultureInfo.CurrentCulture);
    //         imageScaleHigh.Text = ((int)range.Upper + 50).ToString(CultureInfo.CurrentCulture);
    //     }
    //     finally
    //     {
    //         suppressScaleTextEvents = false;
    //     }
    // }

    private void SetOperationModeSelection(int modeIndex)
    {
        // opModes[modeIndex].IsChecked = true;
        for (int i = 0; i < opModes.Length; i++) opModes[i].IsChecked = i == modeIndex;
    }
}