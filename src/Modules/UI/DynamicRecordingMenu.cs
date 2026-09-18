using System.Windows;
using System.Windows.Controls;
using SensorInterface.Sensor;
using SensorInterface.classes;

namespace SensorInterface.UI;

public class RecordingMenu
{
    SaveDataType saveType;
    private string savePath = @"D:\works\data_in\";

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

    private CheckBox recordROIOnlyToggle = new CheckBox
    {
        Content = "Record Only ROI",
        IsChecked = false,
        Margin = new Thickness(0, 0, 0, 10),
        IsEnabled = false,
    };

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

        Button record = new Button
        {
            Content = "▶︎",
            Height = 30,
            Margin = new Thickness(0, 0, 0, 10),
        };
        record.Click += (_,_) =>
        {
            if (recorder.recording) recorder.Stop();
            else {
                (int width, int height) = recorder.hasROI() ? recorder.sensor.ROI().Dimensions() : recorder.sensor.Dimensions() ;
                
                recorder.Start(new RecorderSettings
                {
                    camera_width = width,
                    camera_height = height,
                    baseDirectory = savePath,
                    dataType = saveType,
                    singleBinary = singleBinaryToggle.IsChecked == true,
                    recordROIOnly = recordROIOnlyToggle.IsChecked == true
                });
            }
        };

        Button connection = new Button
        {
            Content = "⏻",
            Height = 30,
            Margin = new Thickness(0, 0, 0, 10),
        };
        connection.Click += (_,_) =>
        {
            if (recorder.sensor.Connected())
            {
                // TODO: Set the colour for the Content to Yellow
                recorder.sensor.Disconnect();
            }
            else
            {
                // TODO: Set the Colour for the content to Blue
                recorder.sensor.Connect();
            }
        };

        ComboBox compression = new ComboBox
        {
            Height = 32,
            IsEditable = false,
            ItemsSource = Enum.GetNames<SaveDataType>(),
            SelectedIndex = (int) recorder.sensor.config().saveType,
            Padding = new Thickness(8, 4, 8, 4)
        };
        compression.SelectionChanged += (_,_) => saveType = (SaveDataType)compression.SelectedIndex;

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
