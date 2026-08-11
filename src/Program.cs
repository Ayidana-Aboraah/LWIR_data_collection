// Copyright (c) 2008-2025 Optris GmbH & Co. KG

using Optris.OtcSdk;
using System.IO;
using System.Windows;


namespace LWIR_app
{
    internal static class Program
    {
        /// <summary>Program main entry point.</summary>
        [STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.FirstChanceException += (_, eventArgs) =>
            {
                if (eventArgs.Exception is InvalidOperationException)
                {
                    LogException(eventArgs.Exception);
                }
            };

            AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            {
                LogException(eventArgs.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception"));
            };

            // Initialize the SDK by setting log verbosity
            Sdk.init(Verbosity.Off, Verbosity.Off, "LWIR_app");

            /*
             * Add an additional detector for Ethernet devices on the network 192.168.0.0/24. A detector for 
             * USB devices was added when calling Sdk.init().
             */
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

        private static void LogException(Exception exception)
        {
            string logPath = Path.Combine(Path.GetTempPath(), "LWIR_app_error.log");
            File.AppendAllText(logPath, DateTime.Now.ToString("O") + Environment.NewLine + exception + Environment.NewLine + Environment.NewLine);
        }
    }
}