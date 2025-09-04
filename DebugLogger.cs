// DebugLogger.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DhcpWmiViewer
{
    /// <summary>
    /// Erweiterte Debug-Logger-Klasse, die beide Log-Mechanismen konsistent integriert:
    /// - EventLogger für lokale EventLog-Ausgaben
    /// - MainForm.LogGuiEventAsync für Remote-Logging
    /// - Debug-Ausgaben für Development
    /// - Conditional Compilation für Debug vs. Release
    /// </summary>
    public static class DebugLogger
    {
        private static MainForm? _mainFormInstance;
        private static bool _isInitialized = false;

        /// <summary>
        /// Initialisiert den DebugLogger mit einer MainForm-Instanz für Remote-Logging.
        /// </summary>
        public static void Initialize(MainForm? mainForm = null)
        {
            _mainFormInstance = mainForm;
            _isInitialized = true;
            
            LogDebug("DebugLogger initialisiert");
        }

        /// <summary>
        /// Debug-Level Logging (nur in Debug-Builds aktiv).
        /// </summary>
        [Conditional("DEBUG")]
        public static void LogDebug(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var debugMessage = $"[DEBUG] {fileName}.{memberName}:{lineNumber} - {message}";
                
                // Debug-Ausgabe (nur in Debug-Builds)
                Debug.WriteLine(debugMessage);
                
                // Zusätzlich in Fallback-Datei schreiben
                SafeWriteDebugFallback(debugMessage);
            }
            catch
            {
                // Swallow debug logging errors
            }
        }

        /// <summary>
        /// Einfache Debug-Ausgabe (Kompatibilität mit bestehender Implementierung).
        /// </summary>
        [Conditional("DEBUG")]
        public static void Log(string message)
        {
            LogDebug(message);
        }

        /// <summary>
        /// Formatierte Debug-Ausgabe (Kompatibilität mit bestehender Implementierung).
        /// </summary>
        [Conditional("DEBUG")]
        public static void LogFormat(string format, params object[] args)
        {
            try
            {
                var message = string.Format(format, args);
                LogDebug(message);
            }
            catch (Exception ex)
            {
                LogDebug($"LogFormat failed: {ex.Message} | Format: {format}");
            }
        }

        /// <summary>
        /// Separator-Linie für bessere Lesbarkeit (Kompatibilität mit bestehender Implementierung).
        /// </summary>
        [Conditional("DEBUG")]
        public static void LogSeparator(string title = "")
        {
            var separator = string.IsNullOrEmpty(title) 
                ? "=" + new string('=', 50) 
                : $"=== {title} " + new string('=', Math.Max(0, 50 - title.Length - 5));
            LogDebug(separator);
        }

        /// <summary>
        /// Info-Level Logging (sowohl lokal als auch remote).
        /// </summary>
        public static void LogInfo(string message, string action = "Info", string scopeId = "", string ip = "", string details = "")
        {
            try
            {
                // Lokaler EventLogger
                EventLogger.LogInfo($"[INFO] {message}");
                
                // Remote-Logging (asynchron, falls MainForm verfügbar)
                if (_mainFormInstance != null)
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await _mainFormInstance.LogGuiEventAsync(action, scopeId, ip, $"{message}|{details}");
                        }
                        catch (Exception ex)
                        {
                            LogDebug($"Remote-Logging fehlgeschlagen: {ex.Message}");
                        }
                    });
                }
                
                LogDebug($"Info logged: {message}");
            }
            catch (Exception ex)
            {
                LogDebug($"LogInfo fehlgeschlagen: {ex.Message}");
            }
        }

        /// <summary>
        /// Error-Level Logging (sowohl lokal als auch remote).
        /// </summary>
        public static void LogError(string message, Exception? exception = null, string action = "Error", string scopeId = "", string ip = "")
        {
            try
            {
                var fullMessage = exception != null ? $"{message} - Exception: {exception}" : message;
                
                // Lokaler EventLogger
                if (exception != null)
                {
                    EventLogger.LogException(exception, message);
                }
                else
                {
                    EventLogger.LogError($"[ERROR] {message}");
                }
                
                // Remote-Logging (asynchron, falls MainForm verfügbar)
                if (_mainFormInstance != null)
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await _mainFormInstance.LogGuiEventAsync(action, scopeId, ip, fullMessage);
                        }
                        catch (Exception ex)
                        {
                            LogDebug($"Remote-Error-Logging fehlgeschlagen: {ex.Message}");
                        }
                    });
                }
                
                LogDebug($"Error logged: {fullMessage}");
            }
            catch (Exception ex)
            {
                LogDebug($"LogError fehlgeschlagen: {ex.Message}");
            }
        }

        /// <summary>
        /// GUI-Action Logging (primär für Remote-Logging, mit lokalem Fallback).
        /// </summary>
        public static void LogGuiAction(string action, string scopeId = "", string ip = "", string details = "")
        {
            try
            {
                var message = $"GUI-Action: {action}";
                if (!string.IsNullOrEmpty(scopeId)) message += $" | Scope: {scopeId}";
                if (!string.IsNullOrEmpty(ip)) message += $" | IP: {ip}";
                if (!string.IsNullOrEmpty(details)) message += $" | Details: {details}";
                
                // Lokaler EventLogger als Fallback
                EventLogger.LogInfo($"[GUI] {message}");
                
                // Remote-Logging (asynchron, falls MainForm verfügbar)
                if (_mainFormInstance != null)
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await _mainFormInstance.LogGuiEventAsync(action, scopeId, ip, details);
                        }
                        catch (Exception ex)
                        {
                            LogDebug($"Remote-GUI-Logging fehlgeschlagen: {ex.Message}");
                        }
                    });
                }
                
                LogDebug($"GUI-Action logged: {message}");
            }
            catch (Exception ex)
            {
                LogDebug($"LogGuiAction fehlgeschlagen: {ex.Message}");
            }
        }

        /// <summary>
        /// Performance-Logging für zeitkritische Operationen.
        /// </summary>
        [Conditional("DEBUG")]
        public static void LogPerformance(string operation, TimeSpan duration, [CallerMemberName] string memberName = "")
        {
            try
            {
                var message = $"[PERF] {operation} in {memberName}: {duration.TotalMilliseconds:F2}ms";
                LogDebug(message);
            }
            catch
            {
                // Swallow performance logging errors
            }
        }

        /// <summary>
        /// Hilfsmethode für Performance-Messung.
        /// </summary>
        public static IDisposable MeasurePerformance(string operation, [CallerMemberName] string memberName = "")
        {
            return new PerformanceMeasurer(operation, memberName);
        }

        private static void SafeWriteDebugFallback(string text)
        {
            try
            {
                var path = Path.Combine(Path.GetTempPath(), "DhcpWmiViewer-debug.log");
                var line = DateTime.UtcNow.ToString("o") + " - " + text + Environment.NewLine;
                File.AppendAllText(path, line, Encoding.UTF8);
            }
            catch
            {
                // Last resort: swallow
            }
        }

        /// <summary>
        /// Performance-Messer-Klasse für using-Statement.
        /// </summary>
        private class PerformanceMeasurer : IDisposable
        {
            private readonly string _operation;
            private readonly string _memberName;
            private readonly Stopwatch _stopwatch;

            public PerformanceMeasurer(string operation, string memberName)
            {
                _operation = operation;
                _memberName = memberName;
                _stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                _stopwatch.Stop();
                LogPerformance(_operation, _stopwatch.Elapsed, _memberName);
            }
        }
    }
}