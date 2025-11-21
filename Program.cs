// Program.cs

using System;
using System.Windows.Forms;
using System.IO;

namespace DhcpWmiViewer
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // 0) Konfiguration laden
            try
            {
                AppConfig.Load();
            }
            catch
            {
                // Fehler beim Laden der Konfiguration ignorieren
            }

            // 1) PowerShell-Umgebung-Initialisierung - der neue PowerShellExecutor verwendet automatisch
            // die beste verfügbare Methode (lokal oder remote)
            try
            {
                System.Diagnostics.Debug.WriteLine("PowerShell-Executor initialisiert - verwendet automatisch lokale oder remote Ausführung");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PowerShell-Konfiguration-Warnung: {ex.Message}");
            }

            // 2) App-weite Environment-Prüfung (z.B. ob wir lokal auf einem DHCP-Server laufen)
            try
            {
                AppEnvironment.Initialize();
            }
            catch
            {
                // ignore initialization errors
            }

            // 3) EventLogger initialisieren (lokaler EventLog-Logger)
            // Achtung: CreateEventSource benötigt Admin-Rechte; EnsureEventSourceRegisteredBestEffort
            // in MainForm.Logging.cs ist so implementiert, dass Fehler geschluckt werden.
            try
            {
                EventLogger.Initialize(AppConstants.EventSourceName, AppConstants.EventLogName, tryCreateSource: true);
                EventLogger.LogInfo("DhcpWmiViewer gestartet - EventLogger initialisiert");
            }
            catch (Exception ex)
            {
                // Fallback zu Debug-Ausgabe wenn EventLogger fehlschlägt
                System.Diagnostics.Debug.WriteLine($"EventLogger.Initialize fehlgeschlagen: {ex.Message}");
            }

            // Ensure proper DPI scaling for WinForms controls
            try { Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); } catch { /* not supported on some frameworks */ }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Global exception handlers (zeigt und loggt Fehler)
            Application.ThreadException += (s, e) => ShowAndLog("UI Thread Exception", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                ShowAndLog("Unhandled Exception", ex);
            };

            // 4) Administratorrechte prüfen wird in MainForm.Load gemacht für bessere Stabilität

            Application.Run(new MainForm());
        }

        static void ShowAndLog(string title, Exception? ex)
        {
            try
            {
                string typeName = ex?.GetType().FullName ?? "<no-exception-type>";
                string msg = $"{title}: {typeName}: {ex?.Message}\n\n{ex?.StackTrace}";

                // Benutzer informieren (falls UI möglich)
                try { MessageBox.Show(msg, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                catch { /* headless */ }

                // In Temp-File schreiben (Crash-Log)
                try
                {
                    var p = Path.Combine(Path.GetTempPath(), "DhcpWmiViewer-crash.log");
                    File.AppendAllText(p, DateTime.Now.ToString("s") + " - " + title + Environment.NewLine + msg + Environment.NewLine + "--------------------" + Environment.NewLine);
                }
                catch { /* ignore */ }

                // In EventLog schreiben (wenn möglich)
                try
                {
                    EventLogger.LogException(ex, title);
                }
                catch (Exception logEx)
                {
                    // Fallback zu Debug-Ausgabe wenn EventLogger fehlschlägt
                    System.Diagnostics.Debug.WriteLine($"EventLogger.LogException fehlgeschlagen: {logEx.Message}");
                }
            }
            catch
            {
                // nothing more we can do
            }
        }
    }
}
