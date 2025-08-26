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
            string server = GetServerNameOrDefault();
            if (string.IsNullOrWhiteSpace(server))
            {
                MessageBox.Show(this, "Kein Server ausgewählt.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
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
                    string script = @"
Get-WinEvent -LogName Application -MaxEvents 1000 `"
  + @"| Where-Object { ($_.ProviderName -eq 'DhcpWmiViewer') -or ($_.Message -match 'Reservation') } `"
  + @"| Select-Object @{Name='TimeCreated'; Expression = { $_.TimeCreated.ToUniversalTime().ToString('o') } }, ProviderName, Id, LevelDisplayName, @{Name='Message'; Expression = { ($_.Message -replace '\r?\n',' ') }}
";
                    remoteDt = await PowerShellExecutor.ExecutePowerShellQueryAsync(
                        server,
                        s => GetCredentialsForServer(s)!, // PSCredential provider
                        ps =>
                        {
                            ps.AddScript(script);
                        },
                        dt =>
                        {
                            dt.Columns.Add("TimeCreated", typeof(string));
                            dt.Columns.Add("LevelDisplayName", typeof(string));
                            dt.Columns.Add("ProviderName", typeof(string));
                            dt.Columns.Add("Id", typeof(string));
                            dt.Columns.Add("Message", typeof(string));
                        },
                        isDynamic: true
                    ).ConfigureAwait(false);
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
                        using (var session = new EventLogSession(server))
                        {
                            var query = new EventLogQuery("Application", PathType.LogName)
                            {
                                ReverseDirection = true,
                                TolerateQueryErrors = true
                            };

                            // Binde Session an die Query
                            query.Session = session;

                            using (var reader = new EventLogReader(query))
                            {
                                int read = 0;
                                const int maxToRead = 1000;

                                for (EventRecord ev = reader.ReadEvent(); ev != null && read < maxToRead; ev = reader.ReadEvent())
                                {
                                    try
                                    {
                                        // Filter: nur Events mit diesem Provider-Namen
                                        var provider = (ev.ProviderName ?? "").Trim();
                                        if (!string.Equals(provider, "DhcpWmiViewer", StringComparison.OrdinalIgnoreCase))
                                        {
                                            // nicht unsere Quelle -> überspringen
                                            continue;
                                        }

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
                                        list.Add((time, typ, src, id, msg.Replace(Environment.NewLine, " ")));
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
                    catch (Exception ex)
                    {
                        try { File.WriteAllText(errPath, "EventLogReader fallback error:\r\n" + ex.ToString(), Encoding.UTF8); } catch { }
                        throw new Exception("Fehler beim Lesen des Eventlogs auf " + server + ": " + ex.Message, ex);
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
