// Program.cs
namespace DhcpWmiViewer
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            AppConfig.Load();
            AppEnvironment.Initialize();

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Globale Fehlerbehandlung: anzeigen und in %TEMP%\DhcpWmiViewer-crash.log schreiben
            Application.ThreadException += (s, e) => ShowAndLog("UI Thread Exception", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ShowAndLog("Unhandled Exception", e.ExceptionObject as Exception);

            Application.Run(new MainForm());

            // Fenster zu = App zu: Threads des geladenen DhcpServer-Moduls (CIM) oder hängende WinRM-Verbindungen
            // dürfen den Prozess nicht im Hintergrund weiterleben lassen. ProcessExit räumt noch kurz auf (max. 2 s).
            Environment.Exit(0);
        }

        private static void ShowAndLog(string title, Exception? ex)
        {
            var msg = $"{title}: {ex?.GetType().FullName ?? "<no-exception-type>"}: {ex?.Message}\n\n{ex?.StackTrace}";

            try { MessageBox.Show(msg, Loc.T("Fehler", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }

            try
            {
                var path = Path.Combine(Path.GetTempPath(), "DhcpWmiViewer-crash.log");
                File.AppendAllText(path, $"{DateTime.Now:s} - {title}{Environment.NewLine}{msg}{Environment.NewLine}--------------------{Environment.NewLine}");
            }
            catch { }
        }
    }
}
