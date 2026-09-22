using System.Windows;
using System.Windows.Controls;
using SensorInterface.Sensor;
using SensorInterface.classes;
using SensorInterface.models;

namespace SensorInterface.UI;

public class RecordingMenu : GroupBox
{
    SaveDataType saveType;
    private string savePath = @"D:\works\data_in\";

    public bool recordROIOnly = false;

    private Button saveDirectory = new Button
    {
        Content = "Set Save Directory",
        Height = 30,
        Margin = new Thickness(0, 0, 0, 10),
        IsEnabled = false,
    };

    private CheckBox singleBinaryToggle = new CheckBox
    {
        Content = "Single Binary File",
        IsChecked = true,
        Margin = new Thickness(0, 0, 0, 10),
        IsEnabled = false,
    };

    public TextBox projectName = new TextBox
    {
        TextWrapping = TextWrapping.Wrap,
        Text = "[Type Project Name Here]"
    };

    public RecordingMenu()
    {
        Header = "Recording";
        StackPanel panel = new StackPanel() { };

        Grid split = new Grid();
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(saveDirectory, 0);
        Grid.SetColumn(singleBinaryToggle, 1);
        split.Children.Add(saveDirectory);
        split.Children.Add(singleBinaryToggle);

        panel.Children.Add(projectName);
        panel.Children.Add(split);

        foreach (SensorSet sensor in SensorManager.sensors.ToArray())
            panel.Children.Add(BuildSensorOption(sensor.recorder));

        Content = panel;
    }

    // TODO: create a list of a grids for each recording thing
    private Grid BuildSensorOption(UniversalRecorder recorder)
    {
        var grid = new Grid();

        TextBlock name = new TextBlock
        {
            Text = recorder.sensor.config().name,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(5, 0, 5, 0)
        };

        Button ROIOnlyToggle = new Button
        {
            Content = "⌞ ⌝",
            Background = Visuals.CCAM_Blue,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Height = 30,
            Width = 30,
        };
        ROIOnlyToggle.Click += (_, _) =>
        {
            if (recordROIOnly)
            {
                ROIOnlyToggle.Background = Visuals.CCAM_Blue;
                recordROIOnly = false;
            }
            else
            {
                ROIOnlyToggle.Background = Visuals.CCAM_Yellow;
                recordROIOnly = true;
            }
        };

        Button record = new Button
        {
            Content = "▶︎",
            Background = Visuals.CCAM_Blue,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Height = 30,
            Width = 30,
        };
        record.Click += (_, _) =>
        {
            if (recorder.recording)
            {
                record.Background = Visuals.CCAM_Blue;
                recorder.Stop();
                record.Content = "▶︎";
            }
            else
            {
                (int width, int height) = recorder.hasROI() ? recorder.sensor.ROI().Dimensions() : recorder.sensor.Dimensions();
                recorder.Start(new RecorderSettings
                {
                    camera_width = width,
                    camera_height = height,
                    baseDirectory = savePath,
                    dataType = saveType,
                    singleBinary = singleBinaryToggle.IsChecked == true,
                    recordROIOnly = recordROIOnly,
                });
                record.Background = Visuals.Stop_Red;
                record.Content = "⏸";
            }
        };

        Button connection = new Button
        {
            Content = "⏻",
            Background = Visuals.CCAM_Blue,
            Height = 30,
            Margin = new Thickness(0, 0, 0, 10),
        };
        connection.Click += (_, _) =>
        {
            if (recorder.sensor.Connected())
            {
                connection.Background = Visuals.Grey;
                recorder.sensor.Disconnect();
            }
            else
            {
                connection.Background = Visuals.Start_Green;
                recorder.sensor.Connect();
            }
        };

        ComboBox compression = new ComboBox
        {
            Height = 32,
            IsEditable = false,
            Background = Visuals.CCAM_Blue,
            ItemsSource = Enum.GetNames<SaveDataType>(),
            SelectedIndex = (int)recorder.sensor.config().saveType,
            Padding = new Thickness(8, 4, 8, 4)
        };
        compression.SelectionChanged += (_, _) => saveType = (SaveDataType)compression.SelectedIndex;

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(name, 0);
        Grid.SetColumn(record, 1);
        Grid.SetColumn(connection, 2);
        Grid.SetColumn(compression, 3);

        grid.Children.Add(name);
        grid.Children.Add(record);
        grid.Children.Add(connection);
        grid.Children.Add(compression);

        return grid;
    }
}
