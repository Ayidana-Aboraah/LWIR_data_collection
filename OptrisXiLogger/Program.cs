using OptrisXiLogger.UI;

namespace OptrisXiLogger;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Global exception handlers so unhandled exceptions show a dialog
        // rather than silently crashing on the recording computer.
        Application.ThreadException += (_, e) =>
        {
            MessageBox.Show(
                $"Unhandled UI error:\n\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
                "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            MessageBox.Show(
                $"Unhandled error:\n\n{e.ExceptionObject}",
                "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        Application.Run(new MainForm());
    }
}
