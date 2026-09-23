using System.Windows;
using System.Windows.Controls;
using SensorInterface.Sensor;
using SensorInterface.classes;
using SensorInterface.models;

namespace SensorInterface.UI;

public class RecordingMenu : GroupBox
{
    private string savePath = @"D:\works\data_in\";
    public bool recordROIOnly = false;
    SaveDataType saveType;

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

    StackPanel sensorPanel = new StackPanel{};

    public RecordingMenu()
    {
        Header = "Recording";

        Grid split = new Grid();
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(saveDirectory, 0);
        Grid.SetColumn(singleBinaryToggle, 1);

        split.Children.Add(saveDirectory);
        split.Children.Add(singleBinaryToggle);

        sensorPanel.Children.Add(projectName);
        sensorPanel.Children.Add(split);

        UpdateSensorList();

        Content = sensorPanel;
    }

    public void UpdateSensorList()
    {
        sensorPanel.Children.Clear();
        foreach (UniversalRecorder sensor in SensorManager.sensors) sensorPanel.Children.Add(BuildSensorOption(sensor));
    }

    // TODO: create a list of grids for each recording thing
    private Grid BuildSensorOption(UniversalRecorder recorder)
    {
        var grid = new Grid();

        TextBlock name = new TextBlock
        {
            Text = recorder.config.name,
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
                (int width, int height) = recorder.hasROI() ? recorder.roi.Dimensions() : recorder.Dimensions();
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
            Padding = new Thickness(8, 4, 8, 4),
            ItemsSource = Enum.GetNames<SaveDataType>(),
            SelectedIndex = (int)recorder.config.saveType,
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
