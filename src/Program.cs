using System.Windows;
using Optris.OtcSdk;
using System.IO;
using SensorInterface.Sensor.LWIR;
using FFMediaToolkit;
using SensorInterface.Sensor;

namespace SensorInterface
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "-s")
            {
                // TODO: Launch Sensor Project
                // TODO: Check args[1] for what sensor / the sensor type
                switch (args[1])
                {
                    case nameof(SensorType.Optris_LWIR):
                    break;
                    case nameof(SensorType.Basler_NIR):
                    break;
                    case nameof(SensorType.Photodiode):
                    break;
                }
            }
            FFmpegLoader.FFmpegPath = @"C:\Users\ayidana.aboraah\Downloads\ffmpeg-n7.1.3-43-g5a1f107b4c-win64-gpl-shared-7.1\ffmpeg-n7.1.3-43-g5a1f107b4c-win64-gpl-shared-7.1\bin";

            IRImagerShow.init();
            // SensorManager.DebugImport("LWIR");

            AppDomain.CurrentDomain.FirstChanceException += (_, eventArgs) =>
            {
                if (eventArgs.Exception is InvalidOperationException) LogException(eventArgs.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            {
                LogException(eventArgs.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception"));
            };

            try
            {
                var app = new Application();
                app.DispatcherUnhandledException += (_, eventArgs) =>
                {
                    LogException(eventArgs.Exception);
                    eventArgs.Handled = true;
                };
                app.Run(new DisplayForm());
            }
            catch (SDKException ex)
            {
                LogException(ex);
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                LogException(ex);
                MessageBox.Show(ex.ToString(), "Unhandled Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void LogException(Exception exception) =>
            File.AppendAllText(
                Path.Combine(Path.GetTempPath(), "LWIR_app_error.log"),
                DateTime.Now.ToString("O") + "\n" + exception + "\n\n");
    }
}