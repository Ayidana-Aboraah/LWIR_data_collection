using System.Windows;
using Optris.OtcSdk;
using System.IO;

namespace LWIR_app
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            ProjectDetails.Import(args);

            AppDomain.CurrentDomain.FirstChanceException += (_, eventArgs) =>
            {
                if (eventArgs.Exception is InvalidOperationException) LogException(eventArgs.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            {
                LogException(eventArgs.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception"));
            };

            Sdk.init(Verbosity.Off, Verbosity.Off, "LWIR_app");

            EnumerationManager.getInstance().addEthernetDetector("192.168.0.0/24");

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