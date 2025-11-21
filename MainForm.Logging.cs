// MainForm.Logging.cs

using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Concurrent;
using System.Management.Automation; // NuGet: Microsoft.PowerShell.SDK
using System.Management.Automation.Runspaces;

namespace DhcpWmiViewer
{
    public partial class MainForm
    {
        private const string LocalFallbackFileName = "DhcpWmiViewer-eventlog-fallback.log";

        // Cache für Remote-Source-Existenz (Server -> exists)
        private static readonly ConcurrentDictionary<string, bool> _remoteSourceExistsCache = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        // Einmal-Flag / Lock für lokale Registrierung (nur relevant, wenn App lokal auf DHCP-Server läuft)
        private static int _eventSourceEnsureAttempted = 0; // 0 = not attempted, 1 = attempted

        private void EnsureEventSourceRegisteredBestEffort()
        {
            if (Interlocked.CompareExchange(ref _eventSourceEnsureAttempted, 1, 0) != 0)
                return;

            Task.Run(() =>
            {
                try
                {
                    try
                    {
                        if (EventLog.SourceExists(AppConstants.EventSourceName))
                        {
                            SafeWriteFallback($"Event source '{AppConstants.EventSourceName}' already exists locally.");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        SafeWriteFallback("EventLog.SourceExists failed (local): " + ex.Message);
                        return;
                    }

                    try
                    {
                        var csd = new EventSourceCreationData(AppConstants.EventSourceName, AppConstants.EventLogName);
                        EventLog.CreateEventSource(csd);
                        SafeWriteFallback($"Event source '{AppConstants.EventSourceName}' created locally.");
                    }
                    catch (Exception exCreate)
                    {
                        SafeWriteFallback("CreateEventSource failed (local): " + exCreate.ToString());
                    }
                }
                catch (Exception ex)
                {
                    SafeWriteFallback("EnsureEventSourceRegisteredBestEffort unexpected: " + ex.ToString());
                }
            });
        }

        /// <summary>
        /// Zentrale, asynchrone Log-Methode, die appweit entscheiden soll: lokal (wenn App auf DHCP-Server läuft)
        /// oder remote (Invoke-Command auf Remote-Server). Diese Methode prüft vorher, ob die Source remote existiert,
        /// und legt sie bei Bedarf an.
        /// </summary>
        public async Task LogGuiEventAsync(string action, string scopeId = "", string ip = "", string details = "")
        {
            using (DebugLogger.MeasurePerformance("LogGuiEventAsync"))
            {
                try
                {
                    DebugLogger.LogDebug($"Remote-Logging gestartet: Action={action}, Scope={scopeId}, IP={ip}");
                var sb = new StringBuilder();
                sb.Append($"Action={action};");
                if (!string.IsNullOrEmpty(scopeId)) sb.Append($"Scope={scopeId};");
                if (!string.IsNullOrEmpty(ip)) sb.Append($"IP={ip};");
                sb.Append($"User={Environment.UserName};");
                sb.Append($"Time={DateTime.UtcNow:O};");
                if (!string.IsNullOrEmpty(details)) sb.Append($"Details={details};");
                var message = sb.ToString();

                // 1) Immer auf dem gewählten Server schreiben (aus discovered Server-Liste)
                var server = GetServerNameOrDefault();
                if (string.IsNullOrWhiteSpace(server) || server == "." || server.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase))
                {
                    DebugLogger.LogDebug($"Kein Remote-Server-Ziel verfügbar: {server}");
                    SafeWriteFallback("No remote server target for LogGuiEvent. payload: " + message);
                    return;
                }

                // 2) Remote schreiben auf den gewählten Server

                // Prüfe (mit Cache) ob Source bereits auf remote existiert; falls nicht -> anlegen
                try
                {
                    bool exists = await RemoteEventSourceExistsAsync(server).ConfigureAwait(false);
                    if (!exists)
                    {
                        bool created = await EnsureRemoteEventSourceRegisteredAsync(server).ConfigureAwait(false);
                        if (!created)
                        {
                            SafeWriteFallback($"EnsureRemoteEventSourceRegisteredAsync failed for {server}. Will still attempt write.");
                        }
                        else
                        {
                            // Update cache
                            _remoteSourceExistsCache[server] = true;
                        }
                    }
                }
                catch (Exception exCheck)
                {
                    SafeWriteFallback("Remote source check/create failed: " + exCheck.ToString());
                    // Continue to attempt write even if check failed
                }

                // Nun das eigentliche Schreiben versuchen
                try
                {
                    bool ok = await WriteEventRemoteViaInvokeCommandAsync(server, message).ConfigureAwait(false);
                    if (!ok)
                    {
                        DebugLogger.LogDebug($"Remote-Schreibvorgang fehlgeschlagen für {server}");
                        SafeWriteFallback($"Remote write attempt failed for {server}. payload: {message}");
                    }
                    else
                    {
                        DebugLogger.LogDebug($"Remote-Schreibvorgang erfolgreich für {server}");
                        SafeWriteFallback($"RemoteWriteSuccess to {server}: {message}");
                    }
                }
                catch (Exception exRemote)
                {
                    DebugLogger.LogError("Remote-Schreibvorgang Exception", exRemote);
                    SafeWriteFallback("Remote write outer exception: " + exRemote.ToString() + " | payload: " + message);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogError("LogGuiEventAsync fehlgeschlagen", ex);
                SafeWriteFallback("LogGuiEventAsync failed: " + ex.ToString());
            }
            }
        }

        /// <summary>
        /// Prüft, ob die App-EventSource auf dem Remote-Server existiert.
        /// Nutzt PowerShell Invoke-Command: [System.Diagnostics.EventLog]::SourceExists(...).
        /// Ergebnis wird gecached (pro Server).
        /// </summary>
        private async Task<bool> RemoteEventSourceExistsAsync(string server)
        {
            if (string.IsNullOrWhiteSpace(server)) return false;

            if (_remoteSourceExistsCache.TryGetValue(server, out bool cached) && cached) return true;

            try
            {
                PSCredential? cred = null; // no credential prompts

                string ps = $@"
Invoke-Command -ComputerName '{EscapeForPowerShell(server)}' -ScriptBlock {{
    try {{
        [System.Diagnostics.EventLog]::SourceExists('{EscapeForPowerShell(AppConstants.EventSourceName)}')
    }} catch {{
        $false
    }}
}}
";

                return await Task.Run(() =>
                {
                    try
                    {
                        using (var psInst = PowerShell.Create())
                        {
                            // add credential if provided
                            if (cred != null)
                            {
                                // set variable and call with -Credential
                                psInst.Runspace = RunspaceFactory.CreateRunspace();
                                psInst.Runspace.Open();
                                psInst.Runspace.SessionStateProxy.SetVariable("__cred", cred);
                                var scriptWithCred = $@"
$cred = $__cred
Invoke-Command -ComputerName '{EscapeForPowerShell(server)}' -Credential $cred -ScriptBlock {{
    try {{
        [System.Diagnostics.EventLog]::SourceExists('{EscapeForPowerShell(AppConstants.EventSourceName)}')
    }} catch {{
        $false
    }}
}}
";
                                psInst.AddScript(scriptWithCred);
                            }
                            else
                            {
                                psInst.AddScript(ps);
                            }

                            var results = psInst.Invoke();
                            if (psInst.HadErrors)
                            {
                                foreach (var e in psInst.Streams.Error)
                                    SafeWriteFallback("RemoteEventSourceExistsAsync PS error: " + e.ToString());
                                return false;
                            }

                            foreach (var r in results)
                            {
                                if (r == null) continue;
                                var bo = r.BaseObject;
                                if (bo is bool b) return b;
                                var s = bo?.ToString() ?? "";
                                if (bool.TryParse(s, out bool parsed)) return parsed;
                            }

                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        SafeWriteFallback("RemoteEventSourceExistsAsync exception: " + ex.ToString());
                        return false;
                    }
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                SafeWriteFallback("RemoteEventSourceExistsAsync outer: " + ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// Versucht per PowerShell-Remoting die EventSource auf dem Remote-Server anzulegen.
        /// Rückgabe true = erstellt / exists, false = fehlgeschlagen.
        /// </summary>
        private async Task<bool> EnsureRemoteEventSourceRegisteredAsync(string server)
        {
            if (string.IsNullOrWhiteSpace(server)) return false;

            try
            {
                PSCredential? cred = null; // no credential prompts

                string ps = $@"
Invoke-Command -ComputerName '{EscapeForPowerShell(server)}' -ScriptBlock {{
    try {{
        if (-not [System.Diagnostics.EventLog]::SourceExists('{EscapeForPowerShell(AppConstants.EventSourceName)}')) {{
            New-EventLog -LogName '{EscapeForPowerShell(AppConstants.EventLogName)}' -Source '{EscapeForPowerShell(AppConstants.EventSourceName)}'
            'created'
        }} else {{
            'exists'
        }}
    }} catch {{
        'error:' + $_.ToString()
    }}
}}
";

                return await Task.Run(() =>
                {
                    try
                    {
                        using (var psInst = PowerShell.Create())
                        {
                            if (cred != null)
                            {
                                psInst.Runspace = RunspaceFactory.CreateRunspace();
                                psInst.Runspace.Open();
                                psInst.Runspace.SessionStateProxy.SetVariable("__cred", cred);
                                var scriptWithCred = $@"
$cred = $__cred
Invoke-Command -ComputerName '{EscapeForPowerShell(server)}' -Credential $cred -ScriptBlock {{
    try {{
        if (-not [System.Diagnostics.EventLog]::SourceExists('{EscapeForPowerShell(AppConstants.EventSourceName)}')) {{
            New-EventLog -LogName '{EscapeForPowerShell(AppConstants.EventLogName)}' -Source '{EscapeForPowerShell(AppConstants.EventSourceName)}'
            'created'
        }} else {{
            'exists'
        }}
    }} catch {{
        'error:' + $_.ToString()
    }}
}}
";
                                psInst.AddScript(scriptWithCred);
                            }
                            else
                            {
                                psInst.AddScript(ps);
                            }

                            var results = psInst.Invoke();
                            if (psInst.HadErrors)
                            {
                                foreach (var e in psInst.Streams.Error)
                                    SafeWriteFallback("EnsureRemoteEventSourceRegisteredAsync PS error: " + e.ToString());
                                return false;
                            }

                            foreach (var r in results)
                            {
                                var s = r?.BaseObject?.ToString() ?? "";
                                if (s.StartsWith("created", StringComparison.OrdinalIgnoreCase) || s.StartsWith("exists", StringComparison.OrdinalIgnoreCase))
                                {
                                    _remoteSourceExistsCache[server] = true;
                                    return true;
                                }
                                if (s.StartsWith("error:", StringComparison.OrdinalIgnoreCase))
                                {
                                    SafeWriteFallback("EnsureRemoteEventSourceRegisteredAsync remote error: " + s);
                                    return false;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        SafeWriteFallback("EnsureRemoteEventSourceRegisteredAsync exception: " + ex.ToString());
                        return false;
                    }

                    return false;
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                SafeWriteFallback("EnsureRemoteEventSourceRegisteredAsync outer: " + ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// Schreibt die Message per Invoke-Command in das EventLog des Remote-Servers.
        /// Erwartet, dass vorher die Source existiert (oder erstellt wurde).
        /// </summary>
        private async Task<bool> WriteEventRemoteViaInvokeCommandAsync(string server, string message)
        {
            if (string.IsNullOrWhiteSpace(server)) return false;

            try
            {
                PSCredential? cred = null; // no credential prompts

                // Script: Write-EventLog with here-string for message
                string remoteScript = $@"
Invoke-Command -ComputerName '{EscapeForPowerShell(server)}' -ScriptBlock {{
    try {{
        Write-EventLog -LogName '{EscapeForPowerShell(AppConstants.EventLogName)}' -Source '{EscapeForPowerShell(AppConstants.EventSourceName)}' -EntryType Information -EventId 0 -Message @'
{message}
'@
        'wrote'
    }} catch {{
        'error:' + $_.ToString()
    }}
}}
";

                return await Task.Run(() =>
                {
                    try
                    {
                        using (var ps = PowerShell.Create())
                        {
                            if (cred != null)
                            {
                                ps.Runspace = RunspaceFactory.CreateRunspace();
                                ps.Runspace.Open();
                                ps.Runspace.SessionStateProxy.SetVariable("__cred", cred);
                                var scriptWithCred = $@"
$cred = $__cred
Invoke-Command -ComputerName '{EscapeForPowerShell(server)}' -Credential $cred -ScriptBlock {{
    try {{
        Write-EventLog -LogName '{EscapeForPowerShell(AppConstants.EventLogName)}' -Source '{EscapeForPowerShell(AppConstants.EventSourceName)}' -EntryType Information -EventId 0 -Message @'
{message}
'@
        'wrote'
    }} catch {{
        'error:' + $_.ToString()
    }}
}}
";
                                ps.AddScript(scriptWithCred);
                            }
                            else
                            {
                                ps.AddScript(remoteScript);
                            }

                            var results = ps.Invoke();

                            if (ps.HadErrors)
                            {
                                foreach (var e in ps.Streams.Error)
                                    SafeWriteFallback("WriteEventRemoteViaInvokeCommand PS error: " + e.ToString());
                                return false;
                            }

                            foreach (var r in results)
                            {
                                var s = r?.BaseObject?.ToString() ?? "";
                                if (s.StartsWith("wrote", StringComparison.OrdinalIgnoreCase))
                                    return true;
                                if (s.StartsWith("error:", StringComparison.OrdinalIgnoreCase))
                                {
                                    SafeWriteFallback("WriteEventRemoteViaInvokeCommand remote error: " + s);
                                    return false;
                                }
                            }

                            SafeWriteFallback("WriteEventRemoteViaInvokeCommand: no explicit success output.");
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        SafeWriteFallback("WriteEventRemoteViaInvokeCommand exception: " + ex.ToString());
                        return false;
                    }
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                SafeWriteFallback("WriteEventRemoteViaInvokeCommand outer: " + ex.ToString());
                return false;
            }
        }

        private static string EscapeForPowerShell(string input)
        {
            if (input == null) return "";
            return input.Replace("'", "''");
        }

        private void SafeWriteFallback(string text)
        {
            try
            {
                var p = Path.Combine(Path.GetTempPath(), LocalFallbackFileName);
                var line = DateTime.UtcNow.ToString("o") + " - " + text + Environment.NewLine + "-----" + Environment.NewLine;
                File.AppendAllText(p, line, Encoding.UTF8);
            }
            catch
            {
                // swallow
            }
        }
    }
}
