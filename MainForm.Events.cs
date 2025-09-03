// MainForm.Events.cs

using System;
using System.Data;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics.Eventing.Reader;
using System.Collections.Generic;

namespace DhcpWmiViewer
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// Initialisiert die in-memory DataTable für Events und bindet sie an dgvEvents.
        /// </summary>
        private void InitEventsTable()
        {
            try
            {
                eventsTable = new DataTable("Events");
                eventsTable.Columns.Add("TimeCreated", typeof(string));
                eventsTable.Columns.Add("EntryType", typeof(string));
                eventsTable.Columns.Add("Source", typeof(string));
                eventsTable.Columns.Add("InstanceId", typeof(string));
                eventsTable.Columns.Add("Server", typeof(string));
                eventsTable.Columns.Add("Message", typeof(string));

                bindingEvents = new BindingSource();
                bindingEvents.DataSource = eventsTable;
                if (dgvEvents != null) dgvEvents.DataSource = bindingEvents;
            }
            catch
            {
                // keep UI functional even if something goes wrong
            }
        }

        /// <summary>
        /// Holt Events remote per PowerShell (WinRM) mittels PowerShellExecutor.
        /// Falls das fehlschlägt, fällt diese Methode auf EventLogReader (remote) zurück.
        /// Liefert nur Events vom Provider "DhcpWmiViewer".
        /// Benötigt: GetCredentialsForServer(server) -> PSCredential (existiert im MainForm-Kontext).
        /// </summary>
        private async Task FetchAndBindEventsAsync()
        {
            // Build target server list: discovered list if available, otherwise selected/default server
            var servers = new List<string>();
            try
            {
                if (cmbDiscoveredServers != null && cmbDiscoveredServers.Items != null && cmbDiscoveredServers.Items.Count > 0)
                {
                    foreach (var it in cmbDiscoveredServers.Items)
                    {
                        var s = it?.ToString();
                        if (!string.IsNullOrWhiteSpace(s)) servers.Add(s.Trim());
                    }
                }
            }
            catch { }
            if (servers.Count == 0)
            {
                var single = GetServerNameOrDefault();
                if (string.IsNullOrWhiteSpace(single))
                {
                    MessageBox.Show(this, "Kein Server ausgewählt.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                servers.Add(single);
            }
            
            // Always include local computer for events (since events might be written locally)
            var localComputer = Environment.MachineName;
            if (!servers.Contains(localComputer, StringComparer.OrdinalIgnoreCase))
            {
                servers.Add(localComputer);
            }

            if (dgvEvents == null) return;
            try { btnFetchEvents!.Enabled = false; } catch { }

            // Temp-Path für Fehler-Logs
            string errPath = Path.Combine(Path.GetTempPath(), "dhcp_fetch_events_error.txt");

            try
            {
                DataTable? remoteDt = null;

                // 1) Versuch: PowerShell-Remoting via PowerShellExecutor (AddScript - komplett als Script)
                try
                {
                    // Filter server-side by Provider and recent time window to speed up query
                    // Use -FilterHashtable which is much faster than piping and Where-Object
                    int lookbackDays = 2;
                    int maxEvents = 200;
                    try { lookbackDays = (int)nudEventsLookbackDays.Value; } catch { }
                    try { maxEvents = (int)nudEventsMax.Value; } catch { }

                    // Fast server-side filter by ProviderName and StartTime; also return Server name inside the selection
                    string script = @"
$start = (Get-Date).AddDays(-" + lookbackDays + @")
Get-WinEvent -FilterHashtable @{ LogName = 'Application'; ProviderName = 'DhcpWmiViewer'; StartTime = $start } -MaxEvents " + maxEvents + @" |
  Select-Object @{Name='Server'; Expression = { $env:COMPUTERNAME } }, @{Name='TimeCreated'; Expression = { $_.TimeCreated.ToUniversalTime().ToString('o') } }, ProviderName, Id, LevelDisplayName, @{Name='Message'; Expression = { ($_.Message -replace '\r?\n',' ') }}
";
                    // Aggregate results from all servers in parallel
                    var all = new DataTable();
                    all.Columns.Add("TimeCreated", typeof(string));
                    all.Columns.Add("LevelDisplayName", typeof(string));
                    all.Columns.Add("ProviderName", typeof(string));
                    all.Columns.Add("Id", typeof(string));
                    all.Columns.Add("Server", typeof(string));
                    all.Columns.Add("Message", typeof(string));

                    var tasks = new List<Task<(string server, DataTable? dt)>>();
                    async Task<(string server, DataTable? dt)> InvokeWithTimeoutAsync(string target)
                    {
                        try
                        {
                            var queryTask = PowerShellExecutor.ExecutePowerShellQueryAsync(
                                target,
                                null,
                                ps => { ps.AddScript(script); },
                                dt =>
                                {
                                    dt.Columns.Add("Server", typeof(string));
                                    dt.Columns.Add("TimeCreated", typeof(string));
                                    dt.Columns.Add("LevelDisplayName", typeof(string));
                                    dt.Columns.Add("ProviderName", typeof(string));
                                    dt.Columns.Add("Id", typeof(string));
                                    dt.Columns.Add("Message", typeof(string));
                                },
                                isDynamic: true
                            );

                            var timeoutMs = 8000;
                            var done = await Task.WhenAny(queryTask, Task.Delay(timeoutMs)).ConfigureAwait(false);
                            if (done == queryTask)
                            {
                                var dt = await queryTask.ConfigureAwait(false);
                                return (target, dt);
                            }
                            return (target, null);
                        }
                        catch
                        {
                            return (target, null);
                        }
                    }

                    foreach (var server in servers)
                    {
                        tasks.Add(InvokeWithTimeoutAsync(server));
                    }

                    var results = await Task.WhenAll(tasks).ConfigureAwait(false);
                    foreach (var tup in results)
                    {
                        var server = tup.server;
                        var dtOne = tup.dt;
                        if (dtOne == null || dtOne.Rows.Count == 0) continue;
                        foreach (DataRow r in dtOne.Rows)
                        {
                            var nr = all.NewRow();
                            nr["TimeCreated"] = r["TimeCreated"]?.ToString() ?? "";
                            nr["LevelDisplayName"] = r["LevelDisplayName"]?.ToString() ?? "";
                            nr["ProviderName"] = r["ProviderName"]?.ToString() ?? "";
                            nr["Id"] = r["Id"]?.ToString() ?? "";
                            nr["Server"] = r.Table.Columns.Contains("Server") ? (r["Server"]?.ToString() ?? server) : server;
                            nr["Message"] = r["Message"]?.ToString() ?? "";
                            all.Rows.Add(nr);
                        }
                    }

                    remoteDt = all;
                }
                catch (Exception psEx)
                {
                    try { File.WriteAllText(errPath, "PowerShellExecutor error:\r\n" + psEx.ToString(), Encoding.UTF8); } catch { }
                    Helpers.WriteDebugLog("FetchEvents via PowerShellExecutor failed: " + psEx.Message);
                    remoteDt = null;
                }

                // 2) Wenn PowerShell-Remote erfolgreich war -> mappe DataTable in eventsTable
                if (remoteDt != null && remoteDt.Rows.Count > 0)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (eventsTable == null) InitEventsTable();
                            eventsTable!.Rows.Clear();

                            foreach (DataRow r in remoteDt.Rows)
                            {
                                var dr = eventsTable.NewRow();
                                dr["TimeCreated"] = r.Table.Columns.Contains("TimeCreated") ? (r["TimeCreated"]?.ToString() ?? "") : "";
                                dr["EntryType"] = r.Table.Columns.Contains("LevelDisplayName") ? (r["LevelDisplayName"]?.ToString() ?? "") : "";
                                dr["Source"] = r.Table.Columns.Contains("ProviderName") ? (r["ProviderName"]?.ToString() ?? "") : "";
                                dr["InstanceId"] = r.Table.Columns.Contains("Id") ? (r["Id"]?.ToString() ?? "") : "";
                                dr["Server"] = r.Table.Columns.Contains("Server") ? (r["Server"]?.ToString() ?? "") : GetServerNameOrDefault();
                                dr["Message"] = r.Table.Columns.Contains("Message") ? (r["Message"]?.ToString() ?? "") : "";
                                eventsTable.Rows.Add(dr);
                            }

                            if (dgvEvents.Rows.Count > 0)
                            {
                                dgvEvents.CurrentCell = dgvEvents.Rows[0].Cells[0];
                                dgvEvents.Rows[0].Selected = true;
                            }

                            // Force layout/visibility after bind
                            try
                            {
                                dgvEvents.Visible = true;
                                dgvEvents.BringToFront();
                                dgvEvents.Dock = DockStyle.Fill;
                                SafeBeginInvoke(() =>
                                {
                                    try
                                    {
                                        AdjustEventsColumnWidths();
                                        dgvEvents.AutoResizeRows(DataGridViewAutoSizeRowsMode.AllCells);
                                        dgvEvents.Refresh();
                                        if (dgvEvents.Rows.Count > 0) dgvEvents.FirstDisplayedScrollingRowIndex = 0;
                                    }
                                    catch { }
                                });
                            }
                            catch { }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, "Fehler beim Aktualisieren der Event-Tabelle: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }));
                    try { btnFetchEvents.Enabled = true; } catch { }
                    return;
                }

                // 3) Fallback: EventLogReader (robuster für Remote-Zugriffe) — nur Provider "DhcpWmiViewer"
                var rows = await Task.Run(() =>
                {
                    var list = new List<(DateTime time, string type, string source, string id, string message)>();
                    try
                    {
                        // EventLogSession für Remote-Computer
                        foreach (var serverOne in servers)
                        {
                            using (var session = new EventLogSession(serverOne))
                            {
                                int lookbackDays = 2;
                                int maxEvents = 200;
                                try { lookbackDays = (int)nudEventsLookbackDays.Value; } catch { }
                                try { maxEvents = (int)nudEventsMax.Value; } catch { }

                                // XPath filter: only recent timeframe; provider/message filter is applied in code for robustness
                                var ms = lookbackDays * 24 * 60 * 60 * 1000;
                                string xPath = $"*[System[TimeCreated[timediff(@SystemTime) <= {ms}]]]";
                                var query = new EventLogQuery("Application", PathType.LogName, xPath)
                                {
                                    ReverseDirection = true,
                                    TolerateQueryErrors = true
                                };

                                // Binde Session an die Query
                                query.Session = session;

                                using (var reader = new EventLogReader(query))
                                {
                                    int read = 0;
                                    int maxToRead = Math.Max(1, maxEvents);

                                    for (EventRecord ev = reader.ReadEvent(); ev != null && read < maxToRead; ev = reader.ReadEvent())
                                    {
                                        try
                                        {
                                            var provider = (ev.ProviderName ?? "").Trim();

                                            // Extrahiere Felder (FormatDescription kann fehlschlagen)
                                            var time = ev.TimeCreated?.ToUniversalTime() ?? DateTime.MinValue;
                                            var typ = ev.LevelDisplayName ?? (ev.Level?.ToString() ?? "");
                                            var src = provider;
                                            var id = ev.Id.ToString();
                                            string msg = "";
                                            try
                                            {
                                                msg = ev.FormatDescription() ?? "";
                                            }
                                            catch
                                            {
                                                try { msg = ev.ToXml() ?? ""; } catch { msg = ""; }
                                            }

                                            if (msg.Length > 4000) msg = msg.Substring(0, 4000) + "...";

                                            // Apply provider/message filter here to be robust across systems/localization
                                            var combined = msg ?? string.Empty;
                                            bool isMatch = string.Equals(provider, "DhcpWmiViewer", StringComparison.OrdinalIgnoreCase)
                                                           || combined.IndexOf("Reservation", StringComparison.OrdinalIgnoreCase) >= 0;
                                            if (!isMatch)
                                            {
                                                continue;
                                            }

                                            list.Add((time, typ, serverOne, id, combined.Replace(Environment.NewLine, " ")));
                                            read++;
                                        }
                                        catch
                                        {
                                            // ignore single entry errors
                                        }
                                        finally
                                        {
                                            try { ev.Dispose(); } catch { }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        try { File.WriteAllText(errPath, "EventLogReader fallback error:\r\n" + ex.ToString(), Encoding.UTF8); } catch { }
                        throw new Exception("Fehler beim Lesen des Eventlogs: " + ex.Message, ex);
                    }

                    return list;
                }).ConfigureAwait(false);

                // 4) Binde Fallback-Resultate in die UI
                this.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (eventsTable == null) InitEventsTable();
                        eventsTable!.Rows.Clear();

                        foreach (var r in rows)
                        {
                            var dr = eventsTable.NewRow();
                            dr["TimeCreated"] = r.time.ToString("o");
                            dr["EntryType"] = r.type;
                            dr["Source"] = r.source;
                            dr["InstanceId"] = r.id;
                            dr["Server"] = r.source; // in fallback we used 'source' to carry server name
                            dr["Message"] = r.message;
                            eventsTable.Rows.Add(dr);
                        }

                        if (dgvEvents.Rows.Count > 0)
                        {
                            dgvEvents.CurrentCell = dgvEvents.Rows[0].Cells[0];
                            dgvEvents.Rows[0].Selected = true;
                        }

                        // Force layout/visibility after bind (fallback path)
                        try
                        {
                            dgvEvents.Visible = true;
                            dgvEvents.BringToFront();
                            dgvEvents.Dock = DockStyle.Fill;
                            SafeBeginInvoke(() =>
                            {
                                try
                                {
                                    AdjustEventsColumnWidths();
                                    dgvEvents.AutoResizeRows(DataGridViewAutoSizeRowsMode.AllCells);
                                    dgvEvents.Refresh();
                                    if (dgvEvents.Rows.Count > 0) dgvEvents.FirstDisplayedScrollingRowIndex = 0;
                                }
                                catch { }
                            });
                        }
                        catch { }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "Fehler beim Aktualisieren der Event-Tabelle: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }));
            }
            catch (Exception outer)
            {
                try
                {
                    File.WriteAllText(errPath, outer.ToString(), Encoding.UTF8);
                }
                catch { /* ignore logging errors */ }

                this.BeginInvoke(new Action(() =>
                {
                    MessageBox.Show(this, "Fehler beim Abrufen der Events: " + outer.Message + Environment.NewLine + $"(Details: {Path.GetFileName(errPath)})", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }));
            }
            finally
            {
                try { btnFetchEvents.Enabled = true; } catch { }
            }
        }
    }
}
