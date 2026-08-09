using System.Windows;
using System.Windows.Controls;
using LWIR_app.classes;
using LWIR_app.Sensor;
using Microsoft.Win32;
using WpfBrushes = System.Windows.Media.Brushes;


namespace LWIR_app.UI;

public class RecordingGroup : GroupBox
{
    RecorderBase recorder;

    private GroupBox saveDataTypeBox = new GroupBox
    {
        Header = "Save Data Type",
        Margin = new Thickness(0, 0, 0, 10)
    };
    private RadioButton[] saveTypes = [
        new RadioButton { Content = "BaseData", IsChecked = true, Margin = new Thickness(0, 0, 20, 6) },
            new RadioButton { Content = "IntData", Margin = new Thickness(0, 0, 0, 6) },
            new RadioButton { Content = "RLE Data", Margin = new Thickness(0, 0, 20, 0) },
        ];

    private Button recordEnable = new Button
    {
        Content = "Record",
        IsEnabled = false,
        Height = 30
    };

    private Button saveDirectory = new Button
    {
        Content = "Set Save Directory",
        Height = 30,
        Margin = new Thickness(0, 0, 0, 10)
    };

    private string savePath = @"D:\works\data_in\";
    private CheckBox singleBinaryToggle = new CheckBox
    {
        Content = "Single Binary File",
        IsChecked = true,
        Margin = new Thickness(0, 0, 0, 10)
    };

    private CheckBox recordROIOnlyToggle = new CheckBox
    {
        Content = "Record Only ROI",
        IsChecked = false,
        Margin = new Thickness(0, 0, 0, 10)
    };

    public RecordingGroup(RecorderBase recorder)
    {
        this.recorder = recorder;

        Header = "Recording";

        StackPanel stack = new StackPanel { Margin = new Thickness(8) };

        saveDirectory.Content = savePath;
        saveDirectory.Click += (_, _) => saveDirectory_Click();

        Grid radioGrid = new Grid { Margin = new Thickness(8) };
        radioGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        radioGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        radioGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        radioGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // TODO: Create a space between the elements

        for (int i = 0; i < saveTypes.Length; i++)
        {
            Grid.SetRow(saveTypes[i], 0);
            Grid.SetColumn(saveTypes[i], i);
            radioGrid.Children.Add(saveTypes[i]);
        }

        saveDataTypeBox.Content = radioGrid;

        recordEnable.Click += recordEnable_Click;

        stack.Children.Add(new TextBlock
        {
            Text = "Save Directory",
            Margin = new Thickness(0, 0, 0, 4)
        });
        // stack.Children.Add(saveDirectoryPath);
        stack.Children.Add(saveDirectory);
        stack.Children.Add(saveDataTypeBox);
        stack.Children.Add(singleBinaryToggle);
        stack.Children.Add(recordROIOnlyToggle);
        stack.Children.Add(recordEnable);

        Content = stack;
    }

    public void Update(bool connected)
    {
        saveDirectory.IsEnabled = connected;
        saveDataTypeBox.IsEnabled = connected;
        singleBinaryToggle.IsEnabled = connected;
        recordROIOnlyToggle.IsEnabled = connected;
        recordEnable.IsEnabled = connected && !string.IsNullOrWhiteSpace(savePath);
    }

    public void Update(bool recording, bool sensorConnected)
    {
        saveDirectory.IsEnabled = !recording && sensorConnected;
        saveDataTypeBox.IsEnabled = !recording && sensorConnected;
        singleBinaryToggle.IsEnabled = !recording && sensorConnected;
        recordROIOnlyToggle.IsEnabled = !recording && sensorConnected;
    }

    private void saveDirectory_Click()
    {
        OpenFolderDialog folderDialog = new OpenFolderDialog();

        if (folderDialog.ShowDialog() == true)
        {
            string path = folderDialog.FolderName;
            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show("Please enter a directory path first.", "Save Directory", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                System.IO.Directory.CreateDirectory(path);
                savePath = System.IO.Path.GetFullPath(path);
                saveDirectory.Content = savePath;
                recordEnable.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Invalid Directory", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }

    private void recordEnable_Click(object sender, RoutedEventArgs e)
    {
        if (!recorder.recording)
        {
            string directory = savePath;
            if (string.IsNullOrWhiteSpace(directory))
            {
                MessageBox.Show("Please set a save directory first.", "Recording", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (recordROIOnlyToggle.IsChecked == true && !recorder.hasROI)
            {
                MessageBox.Show("Create ROI");
                return;
            }

            recorder.Start(
                directory, new RecorderSettings
                {
                    dataType = GetSelectedSaveDataType(),
                    singleBinary = singleBinaryToggle.IsChecked == true,
                    recordROIOnly = recordROIOnlyToggle.IsChecked == true
                }
            );

            recordEnable.Background = WpfBrushes.LimeGreen;
            recordEnable.Content = "Stop Recording";
            Update(true, recorder.Connected());
        }
        else
        {
            recorder.Stop();
            recordEnable.ClearValue(BackgroundProperty);
            recordEnable.Content = "Record";
            Update(false, recorder.Connected());
        }
    }

    private SaveDataType GetSelectedSaveDataType()
    {
        for (int i = 0; i < saveTypes.Length; i++)
        {
            if (saveTypes[i].IsChecked == true) return (SaveDataType)i;
        }

        return SaveDataType.Float;
    }
}