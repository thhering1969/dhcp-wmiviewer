// EventLogger.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace DhcpWmiViewer
{
    /// <summary>
    /// Einfacher, robuster Event-Logger:
    /// - Initialize(source, logName, tryCreateSource) zur optionalen Anlage der Quelle.
    /// - LogException / LogError schreibt in EventLog (oder fallback in Temp-File).
    /// </summary>
    public static class EventLogger
    {
        private static string _source = "DhcpWmiViewer";
        private static string _logName = "Application";
        private static int _initialized = 0; // 0 = not, 1 = yes
        private static readonly object _initLock = new object();

        public static string EventSourceName => _source;
        public static string EventLogName => _logName;

        /// <summary>
        /// Initialisierung (optional: versucht, die EventSource anzulegen). Fehler werden geschluckt.
        /// </summary>
        public static void Initialize(string source = "DhcpWmiViewer", string logName = "Application", bool tryCreateSource = false)
        {
            if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0) return;

            _source = string.IsNullOrWhiteSpace(source) ? _source : source;
            _logName = string.IsNullOrWhiteSpace(logName) ? _logName : logName;

            if (!tryCreateSource) return;

            // try-create in background to avoid blocking UI start and to swallow permission errors
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    try
                    {
                        if (!EventLog.SourceExists(_source))
                        {
                            var data = new EventSourceCreationData(_source, _logName);
                            EventLog.CreateEventSource(data);
                        }
                    }
                    catch (Exception exCreate)
                    {
                        // ignore create failures but write local fallback
                        SafeWriteFallback("EventLogger.Initialize CreateEventSource failed: " + exCreate);
                    }
                }
                catch (Exception ex)
                {
                    SafeWriteFallback("EventLogger.Initialize error: " + ex);
                }
            });
        }

        public static void LogException(Exception ex, string title = "Exception")
        {
            try
            {
                var msg = new StringBuilder();
                msg.AppendLine(title);
                if (ex != null)
                {
                    msg.AppendLine(ex.GetType().FullName + ": " + ex.Message);
                    msg.AppendLine(ex.StackTrace ?? "");
                }

                WriteEvent(msg.ToString(), EventLogEntryType.Error);
            }
            catch (Exception e)
            {
                SafeWriteFallback("LogException failed: " + e);
            }
        }

        public static void LogError(string message)
        {
            try
            {
                WriteEvent(message, EventLogEntryType.Error);
            }
            catch (Exception e)
            {
                SafeWriteFallback("LogError failed: " + e + " | original: " + message);
            }
        }

        public static void LogInfo(string message)
        {
            try
            {
                WriteEvent(message, EventLogEntryType.Information);
            }
            catch (Exception e)
            {
                SafeWriteFallback("LogInfo failed: " + e + " | original: " + message);
            }
        }

        private static void WriteEvent(string message, EventLogEntryType type)
        {
            try
            {
                // Prefer EventLog if possible
                if (!string.IsNullOrEmpty(_source))
                {
                    // If source not registered, EventLog.WriteEntry may throw; catch and fallback.
                    EventLog.WriteEntry(_source, message, type);
                    return;
                }
            }
            catch (Exception ex)
            {
                SafeWriteFallback("EventLog.WriteEntry failed: " + ex + " | payload: " + message);
            }

            // fallback if EventLog didn't work
            SafeWriteFallback(message);
        }

        private static void SafeWriteFallback(string text)
        {
            try
            {
                var p = Path.Combine(Path.GetTempPath(), "DhcpWmiViewer-eventlog-fallback.log");
                var line = DateTime.UtcNow.ToString("o") + " - " + text + Environment.NewLine + "-----" + Environment.NewLine;
                File.AppendAllText(p, line, Encoding.UTF8);
            }
            catch
            {
                // last resort: swallow
            }
        }
    }
}
