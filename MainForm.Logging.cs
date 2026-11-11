// MainForm.Logging.cs
using System.Text;

namespace DhcpWmiViewer
{
    public partial class MainForm
    {
        private const string ManualReplication = "Manuell";

        private static readonly string LogFallbackFile = Path.Combine(Path.GetTempPath(), "DhcpWmiViewer-eventlog-fallback.log");

        /// <summary>
        /// Nach einer erfolgreichen Änderung: Scope sofort auf den Failover-Partner replizieren und
        /// Änderung + Replikation im EventLog des aktiven Servers UND des Partners protokollieren.
        /// Liefert einen Text für die Erfolgsmeldung. <paramref name="action"/> = "Manuell": nur
        /// "Bereich replizieren" (aus dem Scope-Kontextmenü), ohne vorherige Änderung.
        /// </summary>
        private async Task<string> ReplicateAndLogAsync(string action, string scopeId, string ip, string details)
        {
            var server = _activeServer;
            var rep = await RunReplicationAsync(server, scopeId);

            var events = new List<(string Message, string EntryType)>();
            if (action != ManualReplication)
                events.Add((BuildEventMessage(action, scopeId, ip, details), "Information"));
            if (rep.Error != null)
                events.Add((BuildEventMessage("Bereich replizieren", scopeId, "", $"Result=Fehler;Trigger={action};Source={server};Partner={rep.Partner};Error={rep.Error}"), "Warning"));
            else if (rep.Skipped)
                events.Add((BuildEventMessage("Bereich replizieren", scopeId, "", $"Result=Übersprungen (RSAT-DHCP-Tools fehlen auf {Environment.MachineName});Trigger={action};Source={server};Partner={rep.Partner}"), "Warning"));
            else if (rep.HasFailover)
                events.Add((BuildEventMessage("Bereich replizieren", scopeId, "",
                    $"Result=Angestoßen;Trigger={action};Source={server};Partner={rep.Partner};Relationship={rep.Relationship}"), "Information"));

            // Manuell ohne Failover-Beziehung: nichts passiert, nichts zu protokollieren
            if (events.Count == 0) return rep.Describe();

            // Zielserver: aktiver Server + Failover-Partner (live vom Server gelesen, keine feste Liste)
            // Partner steht in der Failover-Beziehung evtl. als IP -> für WinRM/Kerberos den DNS-Namen nehmen
            var targets = new List<string> { server };
            if (rep.HasFailover)
            {
                var partner = await ResolveHostNameAsync(rep.Partner!);
                if (!SameHost(partner, server)) targets.Add(partner);
            }

            // Für den Partner dieselben (bereits bekannten) Anmeldedaten wie für den aktiven Server verwenden
            var results = await Task.WhenAll(targets.Select(async t =>
                (Server: t, Ok: await WriteEventsAsync(t, events, s => GetKnownCredentials(s) ?? GetKnownCredentials(server)))));

            var ok = results.Where(r => r.Ok).Select(r => r.Server).ToList();
            var failed = results.Where(r => !r.Ok).Select(r => r.Server).ToList();

            var text = new StringBuilder(rep.Describe());
            if (ok.Count > 0) text.Append("\nProtokolliert im EventLog von: " + string.Join(", ", ok));
            if (failed.Count > 0) text.Append("\nEventLog NICHT geschrieben auf: " + string.Join(", ", failed));
            return text.ToString();
        }

        /// <summary>
        /// Liest die Failover-Beziehung (per WinRM) und führt "Bereich replizieren" lokal aus.
        /// Fehlen die RSAT-DHCP-Tools, wird vorher gefragt, ob sie installiert werden sollen.
        /// </summary>
        private async Task<ReplicationResult> RunReplicationAsync(string server, string scopeId)
        {
            // RSAT vorhanden: alles lokal in einem Aufruf (Failover lesen + replizieren), kein WinRM nötig
            if (DhcpManager.IsRsatDhcpInstalled())
            {
                ReplicationResult local = new(false, null, null);
                await WaitDialog.RunAsync(this, "Bereich replizieren…", async () =>
                    local = await DhcpManager.GetFailoverAndReplicateLocallyAsync(server, scopeId));
                return local;
            }

            // Ohne RSAT: Failover per WinRM lesen (für die Rückfrage), dann ggf. installieren
            FailoverInfo? failover = null;
            try
            {
                await WaitDialog.RunAsync(this, "Failover-Beziehung wird gelesen…", async () =>
                    failover = await DhcpManager.GetFailoverAsync(server, scopeId, GetCredentialsForServer));
            }
            catch (Exception ex)
            {
                return new ReplicationResult(false, null, "Failover-Beziehung konnte nicht gelesen werden: " + ex.Message);
            }
            if (failover == null) return new ReplicationResult(false, null, null);

            if (!DhcpManager.IsRsatDhcpInstalled())
            {
                var answer = MessageBox.Show(this,
                    $"Für \"Bereich replizieren\" (Scope {scopeId} → {failover.Partner}) werden auf diesem PC die " +
                    "RSAT-DHCP-Tools benötigt. Sie sind nicht installiert.\n\n" +
                    "Sollen die RSAT-DHCP-Tools jetzt installiert werden?\n" +
                    "(Windows-Funktion, braucht Adminrechte und Windows Update, einmalig pro PC)\n\n" +
                    "Dauer: zuletzt ca. 16 Minuten über VPN, davon ca. 10 Minuten Download.\n" +
                    "Der Download geht im Büro meist schneller.\n\n" +
                    "Bei \"Nein\" wird \"Bereich replizieren\" übersprungen.",
                    "RSAT-DHCP-Tools installieren?", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
                if (answer != DialogResult.Yes)
                    return new ReplicationResult(false, failover, null, Skipped: true);

                string? installError = null;
                await WaitDialog.RunAsync(this, "RSAT-DHCP-Tools werden installiert… (kann einige Minuten dauern)", async progress =>
                    installError = await DhcpManager.InstallRsatDhcpAsync(progress));
                if (installError != null)
                    return new ReplicationResult(false, failover,
                        "Installation der RSAT-DHCP-Tools fehlgeschlagen: " + installError +
                        "\n(Bei WSUS ggf. per GPO \"Optionale Komponenten direkt von Windows Update herunterladen\" erlauben.)");
            }

            ReplicationResult rep = new(false, failover, null);
            await WaitDialog.RunAsync(this, "Bereich replizieren…", async () =>
                rep = await DhcpManager.ReplicateScopeLocallyAsync(server, scopeId, failover));
            return rep;
        }

        /// <summary>IP -> DNS-Name (Reverse-Lookup); bei Namen oder ohne PTR-Eintrag unverändert.</summary>
        private static async Task<string> ResolveHostNameAsync(string host)
        {
            if (!System.Net.IPAddress.TryParse(host, out var ip)) return host;
            try { return (await System.Net.Dns.GetHostEntryAsync(ip)).HostName; }
            catch { return host; }
        }

        private static string BuildEventMessage(string action, string scopeId, string ip, string details)
        {
            var sb = new StringBuilder($"Action={action};");
            if (!string.IsNullOrEmpty(scopeId)) sb.Append($"Scope={scopeId};");
            if (!string.IsNullOrEmpty(ip)) sb.Append($"IP={ip};");
            sb.Append($"User={Environment.UserName};Time={DateTime.UtcNow:O};");
            if (!string.IsNullOrEmpty(details)) sb.Append($"Details={details};");
            return sb.ToString();
        }

        /// <summary>
        /// Schreibt die Events in das Application-EventLog des Servers (Quelle wird bei Bedarf angelegt).
        /// Fehler werden nur in eine lokale Datei geschrieben.
        /// </summary>
        private static async Task<bool> WriteEventsAsync(string server, IEnumerable<(string Message, string EntryType)> events, Func<string, System.Management.Automation.PSCredential?> getCredentials)
        {
            var list = events.ToList();
            if (string.IsNullOrWhiteSpace(server) || list.Count == 0) return false;

            var source = Helpers.Quote(AppConstants.EventSourceName);
            var log = Helpers.Quote(AppConstants.EventLogName);
            var script = new StringBuilder($"if (-not [System.Diagnostics.EventLog]::SourceExists({source})) {{ New-EventLog -LogName {log} -Source {source} }}\n");
            foreach (var (message, type) in list)
                script.Append($"Write-EventLog -LogName {log} -Source {source} -EntryType {type} -EventId 0 -Message {Helpers.Quote(message)}\n");
            script.Append("'wrote'\n");

            var payload = string.Join(" | ", list.Select(e => e.Message));
            try
            {
                var result = await PowerShellExecutor.InvokeScriptAsync(server, script.ToString(), getCredentials);
                var ok = result.Any(r => r?.BaseObject?.ToString() == "wrote");
                WriteLogFallback(ok ? $"RemoteWriteSuccess to {server}: {payload}" : $"Remote write attempt failed for {server}. payload: {payload}");
                return ok;
            }
            catch (Exception ex)
            {
                WriteLogFallback($"Remote write to {server} failed: {ex.Message} | payload: {payload}");
                return false;
            }
        }

        /// <summary>Vergleicht Servernamen über den Kurznamen (FQDN vs. Kurzname).</summary>
        private static bool SameHost(string a, string b)
        {
            static string Short(string s) => System.Net.IPAddress.TryParse(s.Trim(), out _) ? s.Trim() : s.Trim().Split('.')[0];
            return Short(a).Equals(Short(b), StringComparison.OrdinalIgnoreCase);
        }

        private static void WriteLogFallback(string text)
        {
            try
            {
                File.AppendAllText(LogFallbackFile, DateTime.UtcNow.ToString("o") + " - " + text + Environment.NewLine + "-----" + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }
    }
}
