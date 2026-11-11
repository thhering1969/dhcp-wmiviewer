// MainForm.Events.cs
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.Text;

namespace DhcpWmiViewer
{
    public partial class MainForm
    {
        private static readonly string[] EventColumns = { "TimeCreated", "EntryType", "Source", "InstanceId", "Server", "Message" };
        private DataTable eventsTable = DhcpManager.CreateTable(EventColumns);

        private Panel CreateEventsPanel()
        {
            var eventsPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, MinimumSize = new Size(300, 160) };

            btnFetchEvents = Theme.PrimaryButton("Fetch Events");
            btnFetchEvents.Click += async (s, e) => await WaitDialog.RunAsync(this, "Events werden geladen…", FetchAndBindEventsAsync);

            var btnClearEvents = Theme.SecondaryButton("Clear");
            btnClearEvents.Click += (s, e) => eventsTable.Rows.Clear();

            nudEventsLookbackDays = new NumericUpDown { Minimum = 1, Maximum = 30, Value = 14, Width = 60, Increment = 1, Margin = new Padding(0, 3, 0, 0) };
            nudEventsMax = new NumericUpDown { Minimum = 50, Maximum = 5000, Value = 200, Width = 80, Increment = 50, Margin = new Padding(0, 3, 0, 0) };

            Label Caption(string text, int left) => new()
            {
                Text = text, AutoSize = true, ForeColor = Theme.TextSecondary, Margin = new Padding(left, 7, 6, 0)
            };

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 8, 8, 8),
                FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Theme.Surface
            };
            toolbar.Controls.AddRange(new Control[]
            {
                btnFetchEvents,
                btnClearEvents,
                Caption("Tage:", 12),
                nudEventsLookbackDays,
                Caption("Max:", 12),
                nudEventsMax
            });

            dgvEvents = CreateGrid(headerHeight: 25);
            dgvEvents.AutoGenerateColumns = false;
            dgvEvents.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvEvents.RowHeadersVisible = false;
            dgvEvents.MinimumSize = new Size(200, 120);
            void AddCol(string name, string header, int width) => dgvEvents.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name, HeaderText = header, DataPropertyName = name, Width = width, MinimumWidth = 60,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            });
            AddCol("TimeCreated", "Time (UTC)", 160);
            AddCol("EntryType", "Type", 90);
            AddCol("Source", "Source", 140);
            AddCol("InstanceId", "Id", 80);
            AddCol("Server", "Server", 140);
            dgvEvents.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Message", HeaderText = "Message", DataPropertyName = "Message",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 220
            });

            bindingEvents.DataSource = eventsTable;
            dgvEvents.DataSource = bindingEvents;

            // Reihenfolge wichtig für Docking: Grid (Fill) zuerst, Toolbar (Top) danach
            eventsPanel.Controls.Add(dgvEvents);
            eventsPanel.Controls.Add(toolbar);
            return eventsPanel;
        }

        /// <summary>
        /// Holt die Events der App ("DhcpWmiViewer") vom aktiven DHCP-Server – dorthin schreibt
        /// ReplicateAndLogAsync (aktiver Server + Failover-Partner). Erst per PowerShell-Remoting; nur wenn das scheitert, per EventLogReader.
        /// </summary>
        private async Task FetchAndBindEventsAsync()
        {
            var lookbackDays = (int)nudEventsLookbackDays.Value;
            var maxEvents = (int)nudEventsMax.Value;
            var errPath = Path.Combine(Path.GetTempPath(), "dhcp_fetch_events_error.txt");

            btnFetchEvents.Enabled = false;
            try
            {
                var servers = await GetActiveDhcpServersAsync();
                if (servers.Count == 0)
                {
                    MessageBox.Show(this, "Es wurde kein aktiver DHCP-Server gefunden. Bitte Server eingeben oder \"Discover\" ausführen.",
                        "Events", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Alle aktiven Server parallel abfragen (Änderungen können auf jedem Server protokolliert sein)
                var results = await Task.WhenAll(servers.Select(async s => (Server: s, Rows: await FetchEventsFromServerAsync(s, lookbackDays, maxEvents))));
                var rows = results.Where(r => r.Rows != null)
                                  .SelectMany(r => r.Rows!)
                                  .OrderByDescending(r => r.Time, StringComparer.Ordinal) // ISO-8601 (UTC) sortiert korrekt als Text
                                  .ToList();

                eventsTable.BeginLoadData();
                eventsTable.Rows.Clear();
                foreach (var r in rows)
                    eventsTable.Rows.Add(r.Time, r.Type, r.Source, r.Id, r.Server, r.Message);
                eventsTable.EndLoadData();

                if (dgvEvents.Rows.Count > 0)
                {
                    dgvEvents.CurrentCell = dgvEvents.Rows[0].Cells[0];
                    dgvEvents.FirstDisplayedScrollingRowIndex = 0;
                }
                var perServer = string.Join(", ", results.Select(r => r.Rows == null ? $"{r.Server}: nicht erreichbar" : $"{r.Server}: {r.Rows.Count}"));
                UpdateStatus($"{rows.Count} Event(s), letzte {lookbackDays} Tage – {perServer}");
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(errPath, ex.ToString(), Encoding.UTF8); } catch { }
                MessageBox.Show(this, "Fehler beim Abrufen der Events: " + ex.Message + Environment.NewLine + $"(Details: {Path.GetFileName(errPath)})",
                    "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnFetchEvents.Enabled = true;
            }
        }

        private const int EventsTimeoutMs = 30000;

        /// <summary>
        /// Aktive DHCP-Server: die geprüfte Discover-Liste; ist sie leer, wird AD jetzt abgefragt und geprüft.
        /// Der Server der angezeigten Scopes ist immer dabei. Keine festen Listen.
        /// </summary>
        private async Task<List<string>> GetActiveDhcpServersAsync()
        {
            var servers = cmbDiscoveredServers.Items.Cast<object>()
                .Select(i => i?.ToString()?.Trim() ?? "")
                .Where(s => s.Length > 0)
                .ToList();

            if (servers.Count == 0)
            {
                UpdateStatus("Suche aktive DHCP-Server im AD...");
                var authorized = await Task.Run(DhcpDiscovery.DiscoverDhcpServersInAD);
                var checks = await DhcpDiscovery.CheckServersAsync(authorized);
                servers = checks.Where(c => c.Usable).Select(c => c.Server).ToList();
            }

            var active = _activeServer is { Length: > 0 } and not "." ? _activeServer : ReadServerFromUi();
            if (active != "." && !PowerShellExecutor.IsLocalHost(active))
                servers.Add(active);

            // Doppelte (Kurzname vs. FQDN) entfernen, FQDN bevorzugen
            return servers
                .GroupBy(s => s.Split('.')[0], StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(s => s.Length).First())
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Events eines Servers: PowerShell-Remoting, sonst Remote-EventLog; null, wenn beides scheitert.</summary>
        private async Task<List<EventRow>?> FetchEventsFromServerAsync(string server, int lookbackDays, int maxEvents)
        {
            var rows = await FetchEventsViaPowerShellAsync(server, lookbackDays, maxEvents);
            if (rows != null) return rows;

            try
            {
                var fallback = Task.Run(() => FetchEventsViaEventLogReader(server, lookbackDays, maxEvents));
                if (await Task.WhenAny(fallback, Task.Delay(EventsTimeoutMs)) == fallback)
                    return await fallback;
                Helpers.WriteDebugLog($"FetchEvents via EventLogReader timed out for {server}");
            }
            catch (Exception ex)
            {
                Helpers.WriteDebugLog($"FetchEvents via EventLogReader failed for {server}: {ex.Message}");
            }
            return null;
        }

        private readonly record struct EventRow(string Time, string Type, string Source, string Id, string Server, string Message);

        /// <summary>Liefert die Events oder null, wenn die Abfrage per Remoting nicht möglich war.</summary>
        private async Task<List<EventRow>?> FetchEventsViaPowerShellAsync(string server, int lookbackDays, int maxEvents)
        {
            // Serverseitig nach Provider und Zeitraum filtern; "keine Events gefunden" ist kein Fehler
            var script = $@"
$start = (Get-Date).AddDays(-{lookbackDays})
Get-WinEvent -FilterHashtable @{{ LogName = '{AppConstants.EventLogName}'; ProviderName = '{AppConstants.EventSourceName}'; StartTime = $start }} -MaxEvents {maxEvents} -ErrorAction SilentlyContinue |
  Select-Object @{{Name='Server'; Expression = {{ $env:COMPUTERNAME }} }}, @{{Name='TimeCreated'; Expression = {{ $_.TimeCreated.ToUniversalTime().ToString('o') }} }}, ProviderName, Id, LevelDisplayName, @{{Name='Message'; Expression = {{ ($_.Message -replace '\r?\n',' ') }} }}
";
            string[] columns = { "Server", "TimeCreated", "ProviderName", "Id", "LevelDisplayName", "Message" };

            try
            {
                var query = PowerShellExecutor.ExecutePowerShellQueryAsync(server, GetKnownCredentials, ps => ps.AddScript(script), columns);
                if (await Task.WhenAny(query, Task.Delay(EventsTimeoutMs)) != query)
                {
                    Helpers.WriteDebugLog($"FetchEvents via PowerShell timed out for {server}");
                    return null;
                }

                var table = await query;
                return table.Rows.Cast<DataRow>()
                    .Select(r => new EventRow(
                        r["TimeCreated"].ToString() ?? "", r["LevelDisplayName"].ToString() ?? "", r["ProviderName"].ToString() ?? "",
                        r["Id"].ToString() ?? "", r["Server"].ToString() ?? "", r["Message"].ToString() ?? ""))
                    .ToList();
            }
            catch (Exception ex)
            {
                Helpers.WriteDebugLog($"FetchEvents via PowerShell failed for {server}: {ex.Message}");
                return null;
            }
        }

        private static List<EventRow> FetchEventsViaEventLogReader(string server, int lookbackDays, int maxEvents)
        {
            var list = new List<EventRow>();
            var ms = (long)lookbackDays * 24 * 60 * 60 * 1000;
            // Provider schon in der Abfrage filtern, damit nicht das ganze Log übers Netz gelesen wird
            var xPath = $"*[System[Provider[@Name='{AppConstants.EventSourceName}'] and TimeCreated[timediff(@SystemTime) <= {ms}]]]";

            using var session = new EventLogSession(server);
            var query = new EventLogQuery(AppConstants.EventLogName, PathType.LogName, xPath)
            {
                ReverseDirection = true,
                TolerateQueryErrors = true,
                Session = session
            };

            using var reader = new EventLogReader(query);
            for (var ev = reader.ReadEvent(); ev != null && list.Count < maxEvents; ev = reader.ReadEvent())
            {
                using (ev)
                try
                {
                    string msg;
                    try { msg = ev.FormatDescription() ?? ""; }
                    catch { msg = ""; }
                    if (msg.Length > 4000) msg = msg.Substring(0, 4000) + "...";

                    var time = ev.TimeCreated?.ToUniversalTime() ?? DateTime.MinValue;
                    list.Add(new EventRow(time.ToString("o"), ev.LevelDisplayName ?? ev.Level?.ToString() ?? "", ev.ProviderName ?? "",
                        ev.Id.ToString(), server, msg.Replace(Environment.NewLine, " ")));
                }
                catch
                {
                    // einzelne fehlerhafte Einträge ignorieren
                }
            }
            return list;
        }
    }
}
