// DhcpManager.Failover.cs
using System.Diagnostics;
using System.Management.Automation;
using System.Text;

namespace DhcpWmiViewer
{
    /// <summary>Failover-Beziehung eines Scopes (live vom DHCP-Server gelesen).</summary>
    public sealed record FailoverInfo(string Relationship, string Partner);

    /// <summary>Ergebnis von "Bereich replizieren" (wie im Kontextmenü der DHCP-Konsole).</summary>
    /// <param name="Triggered">true, wenn "Bereich replizieren" erfolgreich ausgeführt wurde.</param>
    /// <param name="Failover">Failover-Beziehung des Scopes (null ohne Failover).</param>
    /// <param name="Error">Fehlertext, falls "Bereich replizieren" fehlgeschlagen ist.</param>
    /// <param name="Skipped">true, wenn übersprungen (RSAT-DHCP-Tools fehlen).</param>
    public sealed record ReplicationResult(bool Triggered, FailoverInfo? Failover, string? Error, bool Skipped = false)
    {
        public bool HasFailover => Failover != null;
        public string? Partner => Failover?.Partner;
        public string? Relationship => Failover?.Relationship;

        /// <summary>Kurzer Text für Meldungen.</summary>
        public string Describe()
        {
            if (Error != null) return "Bereich replizieren FEHLGESCHLAGEN: " + Error;
            if (Skipped) return "Bereich replizieren ÜBERSPRUNGEN: Die RSAT-DHCP-Tools sind auf diesem PC nicht installiert.";
            if (!HasFailover) return "Bereich replizieren: nicht nötig – für diesen Scope gibt es keine Failover-Beziehung.";
            return $"Bereich replizieren: zu {Partner} ausgeführt (Failover-Beziehung \"{Relationship}\").";
        }
    }

    public static partial class DhcpManager
    {
        /// <summary>Name der Windows-Funktion mit den DHCP-Verwaltungstools (enthält das PowerShell-Modul DhcpServer).</summary>
        public const string RsatDhcpCapability = "Rsat.DHCP.Tools~~~~0.0.1.0";

        /// <summary>
        /// true, wenn das DhcpServer-Modul lokal vorhanden ist (RSAT-DHCP-Tools oder DHCP-Server-Rolle).
        /// Wird bei jedem Aufruf neu geprüft, keine Annahme über den PC.
        /// </summary>
        public static bool IsRsatDhcpInstalled()
            => Directory.Exists(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "Modules", "DhcpServer"));

        /// <summary>Liest die Failover-Beziehung des Scopes per WinRM vom Server (null = keine).</summary>
        public static async Task<FailoverInfo?> GetFailoverAsync(string server, string scopeId, Func<string, PSCredential?> getCredentials)
        {
            var script = $@"
$f = Get-DhcpServerv4Failover -ScopeId {Helpers.Quote(scopeId)} -ErrorAction SilentlyContinue | Select-Object -First 1
if ($f) {{ 'OK|' + $f.Name + '|' + $f.PartnerServer }} else {{ 'NOFAILOVER' }}
";
            var result = await PowerShellExecutor.InvokeDhcpScriptAsync(server, script, getCredentials).ConfigureAwait(false);
            var line = result.Select(r => r?.BaseObject?.ToString()).LastOrDefault(s => !string.IsNullOrEmpty(s)) ?? string.Empty;
            var parts = line.Split('|');
            return parts.Length >= 3 && parts[0] == "OK" ? new FailoverInfo(parts[1], parts[2]) : null;
        }

        /// <summary>
        /// Mit lokalen RSAT-DHCP-Tools: Failover-Beziehung lesen UND "Bereich replizieren" in einem lokalen Aufruf
        /// (kein WinRM nötig). Ohne Failover-Beziehung: Ergebnis ohne Failover.
        /// </summary>
        public static async Task<ReplicationResult> GetFailoverAndReplicateLocallyAsync(string server, string scopeId)
        {
            var script = $@"
Import-Module DhcpServer -ErrorAction Stop
$f = Get-DhcpServerv4Failover -ComputerName {Helpers.Quote(server)} -ScopeId {Helpers.Quote(scopeId)} -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $f) {{ 'NOFAILOVER'; exit 0 }}
'FAILOVER|' + $f.Name + '|' + $f.PartnerServer
Invoke-DhcpServerv4FailoverReplication -ComputerName {Helpers.Quote(server)} -ScopeId {Helpers.Quote(scopeId)} -Force -ErrorAction Stop | Out-Null
'OK'
";
            Helpers.WriteDebugLog($"GetFailoverAndReplicateLocally: Server={server}, ScopeId={scopeId}");
            var (exitCode, output) = await RunWindowsPowerShellAsync(script, TimeSpan.FromMinutes(2)).ConfigureAwait(false);
            var lines = output.Split('\r', '\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

            if (lines.Contains("NOFAILOVER")) return new ReplicationResult(false, null, null);

            var fo = lines.FirstOrDefault(l => l.StartsWith("FAILOVER|", StringComparison.Ordinal))?.Split('|');
            var failover = fo is { Length: >= 3 } ? new FailoverInfo(fo[1], fo[2]) : null;

            if (exitCode == 0 && lines.LastOrDefault() == "OK" && failover != null)
                return new ReplicationResult(true, failover, null);

            var error = string.Join(" ", lines.Where(l => !l.StartsWith("FAILOVER|", StringComparison.Ordinal) && l != "OK"));
            Helpers.WriteDebugLog($"GetFailoverAndReplicateLocally failed ({exitCode}): {output}");
            return new ReplicationResult(false, failover, string.IsNullOrWhiteSpace(error) ? $"Exit-Code {exitCode}" : error);
        }

        /// <summary>
        /// "Bereich replizieren" für den Scope. Läuft LOKAL (Invoke-DhcpServerv4FailoverReplication -ComputerName),
        /// damit dieser PC direkt mit beiden Servern spricht – per WinRM scheitert der Befehl am Double-Hop.
        /// Voraussetzung: <see cref="IsRsatDhcpInstalled"/>. Wirft keine Exception; Fehler stehen im Ergebnis.
        /// </summary>
        public static async Task<ReplicationResult> ReplicateScopeLocallyAsync(string server, string scopeId, FailoverInfo failover)
        {
            var script = $@"
Import-Module DhcpServer -ErrorAction Stop
Invoke-DhcpServerv4FailoverReplication -ComputerName {Helpers.Quote(server)} -ScopeId {Helpers.Quote(scopeId)} -Force -ErrorAction Stop | Out-Null
'OK'
";
            Helpers.WriteDebugLog($"ReplicateScopeLocally: Server={server}, ScopeId={scopeId}, Partner={failover.Partner}");
            var (exitCode, output) = await RunWindowsPowerShellAsync(script, TimeSpan.FromMinutes(2)).ConfigureAwait(false);
            if (exitCode == 0 && output.TrimEnd().EndsWith("OK", StringComparison.Ordinal))
                return new ReplicationResult(true, failover, null);

            Helpers.WriteDebugLog($"ReplicateScopeLocally failed ({exitCode}): {output}");
            return new ReplicationResult(false, failover, string.IsNullOrWhiteSpace(output) ? $"Exit-Code {exitCode}" : output.Trim());
        }

        /// <summary>
        /// Installiert die RSAT-DHCP-Tools (Windows-Funktion, braucht Adminrechte und Zugriff auf Windows Update/WSUS).
        /// Liefert null bei Erfolg, sonst den Fehlertext.
        /// </summary>
        public static async Task<string?> InstallRsatDhcpAsync(IProgress<(int Percent, string? Step)>? progress = null)
        {
            // Das CBS-Log alle 1,5 s mitlesen: Download-Fortschritt ("DownloadProgress: [75 / 100]"),
            // Stillstand und Fehler. Bei einem eindeutigen Fehler wird die Installation abgebrochen.
            using var stopWatch = new CancellationTokenSource();
            using var abort = new CancellationTokenSource();
            string? fatalError = null;
            var started = DateTime.Now.AddSeconds(-5);
            var downloadDone = false;
            int? lastPct = null;
            var lastChange = DateTime.Now;
            var cbsWatcher = Task.Run(async () =>
            {
                while (!stopWatch.IsCancellationRequested)
                {
                    // Nur eindeutige Fehler zählen; CBS schreibt auch bei erfolgreicher Installation
                    // "Failed ..."-Zeilen (z. B. 0x800f0805 beim Öffnen von Paketen)
                    var (pct, error, fatal) = ReadCbsState(started);
                    if (fatal && fatalError == null)
                    {
                        fatalError = error;
                        progress?.Report((-1, "⚠ Fehler: " + Shorten(error ?? "", 70)));
                        abort.Cancel();
                    }
                    else if (pct is int d && !downloadDone)
                    {
                        if (d != lastPct) { lastPct = d; lastChange = DateTime.Now; }
                        var stalled = DateTime.Now - lastChange;
                        var step = "Download";
                        if (stalled >= TimeSpan.FromMinutes(2)) step += $" (seit {(int)stalled.TotalMinutes} min unverändert)";
                        progress?.Report((d, step));
                        if (d >= 100) downloadDone = true;
                    }
                    try { await Task.Delay(1500, stopWatch.Token).ConfigureAwait(false); } catch (OperationCanceledException) { }
                }
            });

            try
            {
                if (AdminRightsChecker.IsRunningAsAdministrator())
                {
                    // dism.exe meldet den Gesamtfortschritt als "[====   42.5%   ]"
                    var (exitCode, output) = await RunDismAsync(
                        $"/Online /Add-Capability /CapabilityName:{RsatDhcpCapability} /NoRestart",
                        pct => { if (downloadDone || pct >= 100) progress?.Report((pct, "Installation")); },
                        TimeSpan.FromMinutes(30), abort.Token).ConfigureAwait(false);
                    if (fatalError != null) return "Abgebrochen wegen Fehler im Windows-Protokoll (CBS.log): " + fatalError;
                    // 3010 = erfolgreich, Neustart empfohlen
                    if (IsRsatDhcpInstalled()) return null; // maßgeblich ist, ob das Modul da ist (auch bei Zeitüberschreitung)
                    return DismError(output, exitCode);
                }

                // Ohne Adminrechte: Installation in eigenem Fenster mit UAC-Abfrage (Fortschritt nur aus dem CBS-Log)
                var script = $"Add-WindowsCapability -Online -Name {Helpers.Quote(RsatDhcpCapability)} -ErrorAction Stop | Out-Null";
                var psi = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + Encode(script))
                { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Minimized };
                using var p = Process.Start(psi);
                if (p == null) return "Installation konnte nicht gestartet werden.";
                try { await p.WaitForExitAsync(abort.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { return "Abgebrochen wegen Fehler im Windows-Protokoll (CBS.log): " + fatalError; }
                return IsRsatDhcpInstalled() ? null : $"Installation nicht abgeschlossen (Exit-Code {p.ExitCode}).";
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return "Die Installation wurde abgebrochen (keine Adminrechte bestätigt).";
            }
            finally
            {
                stopWatch.Cancel();
                try { await cbsWatcher.ConfigureAwait(false); } catch { }
            }
        }

        /// <summary>
        /// HRESULTs, bei denen eine optionale Windows-Funktion sicher nicht installiert wird
        /// (WSUS liefert sie nicht, Quelle nicht gefunden, Download/Windows Update nicht erreichbar).
        /// </summary>
        private static readonly string[] FatalCbsCodes =
            { "0x800f0954", "0x800f0950", "0x800f081f", "0x800f0906", "0x800f0907", "0x8024402c", "0x80240438", "0x80072ee2", "0x80072efd" };

        /// <summary>
        /// Liest das Ende von %windir%\Logs\CBS\CBS.log (nur Zeilen seit <paramref name="since"/>):
        /// letzter Download-Fortschritt und letzter Fehler. Fatal = Fehlercode aus <see cref="FatalCbsCodes"/>.
        /// Ignoriert harmlose Meldungen wie ERROR_DELETE_PENDING (0x8007012f).
        /// </summary>
        private static (int? Percent, string? Error, bool Fatal) ReadCbsState(DateTime since)
        {
            int? percent = null;
            string? error = null;
            var fatal = false;
            try
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Logs", "CBS", "CBS.log");
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var len = Math.Min(fs.Length, 64 * 1024);
                fs.Seek(-len, SeekOrigin.End);
                using var reader = new StreamReader(fs);

                foreach (var raw in reader.ReadToEnd().Split('\n'))
                {
                    var line = raw.TrimEnd('\r');
                    var m = System.Text.RegularExpressions.Regex.Match(line, @"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}), (\w+)\s+\w*\s+(.*)$");
                    if (!m.Success) continue;
                    if (!DateTime.TryParse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var t) || t < since)
                        continue;

                    var level = m.Groups[2].Value;
                    var text = m.Groups[3].Value.Trim();

                    var p = System.Text.RegularExpressions.Regex.Match(text, @"DownloadProgress: \[(\d+) / 100\]");
                    if (p.Success) { percent = int.Parse(p.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture); continue; }

                    var isError = level.Equals("Error", StringComparison.OrdinalIgnoreCase)
                        || (System.Text.RegularExpressions.Regex.IsMatch(text, @"\b(Failed|failure|error)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                            && System.Text.RegularExpressions.Regex.IsMatch(text, @"0x8[0-9a-f]{7}", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                            && !text.Contains("0x8007012f", StringComparison.OrdinalIgnoreCase));
                    if (!isError) continue;

                    if (FatalCbsCodes.Any(c => text.Contains(c, StringComparison.OrdinalIgnoreCase)))
                    {
                        error = text;
                        fatal = true;
                    }
                }
            }
            catch { /* Log nicht lesbar -> nichts bekannt */ }
            return (percent, error, fatal);
        }

        private static string Shorten(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

        /// <summary>Startet dism.exe, meldet Prozentwerte aus der Fortschrittsanzeige und liefert Exit-Code + Ausgabe.</summary>
        private static async Task<(int ExitCode, string Output)> RunDismAsync(string arguments, Action<int> onPercent, TimeSpan timeout, CancellationToken abort)
        {
            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "dism.exe"), arguments + " /English")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var p = new Process { StartInfo = psi };
            p.Start();
            var stderr = p.StandardError.ReadToEndAsync();
            var all = new StringBuilder();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(abort);
            cts.CancelAfter(timeout);
            try
            {
                // Fortschritt wird mit '\r' überschrieben -> zeichenweise lesen
                var buffer = new char[512];
                var current = new StringBuilder();
                int n;
                while ((n = await p.StandardOutput.ReadAsync(buffer.AsMemory(), cts.Token).ConfigureAwait(false)) > 0)
                {
                    for (var i = 0; i < n; i++)
                    {
                        var c = buffer[i];
                        all.Append(c);
                        if (c == '\r' || c == '\n')
                        {
                            var m = System.Text.RegularExpressions.Regex.Match(current.ToString(), @"(\d{1,3})(?:[.,]\d)?%");
                            if (m.Success) onPercent(int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
                            current.Clear();
                        }
                        else current.Append(c);
                    }
                }
                await p.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { p.Kill(entireProcessTree: true); } catch { }
                return (-1, $"Zeitüberschreitung nach {timeout.TotalMinutes:0} Minuten.");
            }

            return (p.ExitCode, all + await stderr.ConfigureAwait(false));
        }

        /// <summary>Fehlerzeile aus der DISM-Ausgabe (z. B. "Error: 0x800f0954") statt der ganzen Ausgabe.</summary>
        private static string DismError(string output, int exitCode)
        {
            var lines = output.Split('\r', '\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("[", StringComparison.Ordinal)).ToList();
            var idx = lines.FindIndex(l => l.StartsWith("Error", StringComparison.OrdinalIgnoreCase));
            var text = idx >= 0 ? string.Join(" ", lines.Skip(idx).Take(3)) : $"DISM Exit-Code {exitCode}";
            if (output.Contains("0x800f0954", StringComparison.OrdinalIgnoreCase))
                text += " – Windows Update/WSUS liefert die optionale Funktion nicht.";
            return text;
        }

        /// <summary>Führt ein Script in Windows PowerShell 5.1 (powershell.exe) aus; Fehler landen in der Ausgabe.</summary>
        private static async Task<(int ExitCode, string Output)> RunWindowsPowerShellAsync(string script, TimeSpan timeout)
        {
            // Ausgabe als UTF-8 (Umlaute in Fehlermeldungen); Fehler als Text, Exit-Code 1
            var wrapped = "[Console]::OutputEncoding = [Text.Encoding]::UTF8\n$ProgressPreference = 'SilentlyContinue'\n" +
                          "try {\n" + script + "\n} catch { $_.Exception.Message; exit 1 }";

            var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + Encode(wrapped))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var p = new Process { StartInfo = psi };
            p.Start();
            var stdout = p.StandardOutput.ReadToEndAsync();
            var stderr = p.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(timeout);
            try
            {
                await p.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { p.Kill(entireProcessTree: true); } catch { }
                return (-1, $"Zeitüberschreitung nach {timeout.TotalMinutes:0} Minuten.");
            }

            var output = (await stdout.ConfigureAwait(false)) + (await stderr.ConfigureAwait(false));
            return (p.ExitCode, output);
        }

        private static string Encode(string script) => Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
    }
}
