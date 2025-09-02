// MainForm.Scopes.cs
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data;
using System.Collections.Concurrent;

namespace DhcpWmiViewer
{
    public partial class MainForm : Form
    {
        // Prefetch cache: key = "{canonicalServer}::{scopeId}" -> Task<DataTable?> (background fetch)
        // ConcurrentDictionary damit mehrere Threads/Tasks sicher arbeiten können.
        private readonly ConcurrentDictionary<string, Task<DataTable?>> _reservationPrefetchTasks = new();

        private async void BtnDiscover_Click(object? sender, EventArgs e)
        {
            btnDiscover.Enabled = false;
            UpdateStatus("Discovering DHCP servers in AD...");
            cmbDiscoveredServers.Items.Clear();
            try
            {
                var servers = await Task.Run(() => DhcpDiscovery.DiscoverDhcpServersInAD());
                foreach (var s in servers) cmbDiscoveredServers.Items.Add(s);

                // Entscheide appweit, ob wir lokal auf einem DHCP-Server laufen:
                // Setze AppConstants.RunningOnDhcpServer = true, wenn entweder
                //  - die lokale Maschine in der entdeckten Serverliste enthalten ist (Hostname/Vergleich), oder
                //  - ein lokaler DHCP-Server-Service läuft (best-effort)
                bool localAppears = DhcpDiscovery.LocalHostAppearsInDiscovery(servers);
                bool localService = DhcpDiscovery.CheckLocalDhcpServiceRunning();
                AppConstants.RunningOnDhcpServer = localAppears || localService;

                if (cmbDiscoveredServers.Items.Count > 0)
                {
                    cmbDiscoveredServers.SelectedIndex = 0;
                    if (cmbDiscoveredServers.SelectedItem != null)
                        txtServer.Text = cmbDiscoveredServers.SelectedItem.ToString() ?? Environment.MachineName;
                }

                UpdateStatus($"Found {servers.Count} DHCP server(s). RunningOnDhcpServer={AppConstants.RunningOnDhcpServer}");
            }
            catch (Exception ex)
            {
                UpdateStatus("Error: " + ex.Message);
                MessageBox.Show(this, ex.ToString(), "Discovery error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnDiscover.Enabled = true;
            }
        }

        private List<string> DiscoverDhcpServersInAD()
        {
            // Diese Methode ist nicht mehr verwendet (auslagert); wenn Du dennoch eine lokale Kopie brauchst,
            // kannst Du DhcpDiscovery.DiscoverDhcpServersInAD() aufrufen. Ich belasse diese Methode hier leer.
            return new List<string>();
        }

        private async void BtnQuery_Click(object? sender, EventArgs e)
        {
            btnQuery.Enabled = false;
            btnExportCsv.Enabled = false;
            UpdateStatus("Querying scopes...");
            pb.Style = ProgressBarStyle.Marquee;
            pb.Visible = true;

            var server = txtServer.Text.Trim();
            if (string.IsNullOrEmpty(server) && cmbDiscoveredServers.SelectedItem != null)
                server = cmbDiscoveredServers.SelectedItem.ToString() ?? ".";
            if (string.IsNullOrEmpty(server)) server = ".";

            try
            {
                var dt = await ExecuteWithIntegratedAuthDetection(server, DhcpManager.QueryScopesAsync);
                currentTable = dt;
                binding.DataSource = currentTable;
                dgv.DataSource = binding;
                // Autosize columns to header+content for scopes grid
                try { dgv.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells); } catch { }
                // Let "Description" consume remaining space if present
                try 
                { 
                    var descCol = dgv.Columns.Contains("Description") ? dgv.Columns["Description"] : null; 
                    if (descCol != null) 
                    { 
                        descCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; 
                        descCol.MinimumWidth = Math.Max(descCol.Width, 160); 
                    }
                } 
                catch { }
                dgv.ClearSelection();
                reservationTable.Rows.Clear();
                leaseTable.Rows.Clear();
                if (currentTable.Rows.Count > 0) btnExportCsv.Enabled = true;
                UpdateStatus($"Found {currentTable.Rows.Count} scopes on {server}.");
            }
            catch (Exception ex)
            {
                UpdateStatus("Error: " + ex.Message);
                MessageBox.Show(this, ex.ToString(), "Query error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                pb.Visible = false;
                btnQuery.Enabled = true;
            }
        }

        // Wenn Scope-Auswahl wechselt -> Reservations & Leases neu laden
        private async void Dgv_SelectionChanged(object? sender, EventArgs e)
        {
            if (dgv.SelectedRows.Count == 0)
            {
                reservationTable.Rows.Clear();
                leaseTable.Rows.Clear();
                // Reset tab texts
                UpdateReservationsTabText();
                UpdateLeasesTabText();
                return;
            }

            var row = dgv.SelectedRows[0];
            var scopeIdObj = row.Cells["ScopeId"].Value;
            if (scopeIdObj == null) return;
            var scopeId = scopeIdObj.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(scopeId)) return;

            var server = txtServer.Text.Trim();
            if (string.IsNullOrEmpty(server) && cmbDiscoveredServers.SelectedItem != null)
                server = cmbDiscoveredServers.SelectedItem.ToString() ?? ".";
            if (string.IsNullOrEmpty(server)) server = ".";

            try
            {
                btnQuery.Enabled = false;
                pb.Style = ProgressBarStyle.Marquee;
                pb.Visible = true;
                UpdateStatus($"Querying reservations & leases for {scopeId} on {server}...");

                // start both queries in parallel with integrated auth detection
                var (resTable, leaTable) = await ExecuteReservationsAndLeasesWithIntegratedAuthDetection(server, scopeId, 5);

                // assign & bind on UI-thread (we are in UI context because this is an event handler)
                reservationTable = resTable;
                bindingReservations.DataSource = reservationTable;
                dgvReservations.DataSource = bindingReservations;
                dgvReservations.ClearSelection();

                leaseTable = leaTable;
                bindingLeases.DataSource = leaseTable;
                dgvLeases.DataSource = bindingLeases;
                dgvLeases.ClearSelection();

                // Update tab texts with counts
                UpdateReservationsTabText();
                UpdateLeasesTabText();

                UpdateStatus($"Found {reservationTable.Rows.Count} reservation(s) and {leaseTable.Rows.Count} lease(s) in scope {scopeId}.");
            }
            catch (Exception ex)
            {
                UpdateStatus("Error: " + ex.Message);
                MessageBox.Show(this, ex.ToString(), "Query reservations/leases error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                pb.Visible = false;
                btnQuery.Enabled = true;
            }
        }

        // ===========================
        // Prefetch helpers (complete implementations)
        // ===========================

        /// <summary>
        /// Startet (non-blocking) einen Background-Task, der Reservations für den gegebenen scope abruft
        /// und das Ergebnis in _reservationPrefetchTasks cached. Funktion ist idempotent (GetOrAdd).
        /// </summary>
        public void StartReservationPrefetchForScope(string server, string scopeId)
        {
            if (string.IsNullOrWhiteSpace(scopeId)) return;

            var canonicalServer = CanonicalizeServerKey(server);
            var key = $"{canonicalServer}::{scopeId}";

            // GetOrAdd: wenn bereits ein Task existiert, wird er wiederverwendet (keine Doppelstarts).
            _reservationPrefetchTasks.GetOrAdd(key, k =>
            {
                return Task.Run(async () =>
                {
                    try
                    {
                        Helpers.WriteDebugLog($"TRACE: Prefetch reservations started for {canonicalServer}::{scopeId}");
                        var dt = await DhcpManager.QueryReservationsAsync(server, scopeId, s => GetCredentialsForServerWithTracking(s)!).ConfigureAwait(false);
                        Helpers.WriteDebugLog($"TRACE: Prefetch reservations finished for {canonicalServer}::{scopeId} (rows={(dt?.Rows.Count.ToString() ?? "null")})");
                        return dt;
                    }
                    catch (Exception ex)
                    {
                        Helpers.WriteDebugLog($"Prefetch Reservations failed (background): {ex}");
                        return null;
                    }
                });
            });
        }

        /// <summary>
        /// Liefert das bereits fertiggestellte Ergebnis einer Prefetch-Task (nicht-blockierend) zurück.
        /// Suche erfolgt robust: exakte key -> alternative key -> wildcard search nach scopeId.
        /// </summary>
        /// <returns>DataTable wenn fertig vorhanden, sonst null</returns>
        public DataTable? TryGetPrefetchedReservations(string server, string scopeId)
        {
            if (string.IsNullOrWhiteSpace(scopeId)) return null;

            try
            {
                var canonical = CanonicalizeServerKey(server);
                var key = $"{canonical}::{scopeId}";

                if (_reservationPrefetchTasks.TryGetValue(key, out var t) && t.IsCompletedSuccessfully)
                {
                    Helpers.WriteDebugLog($"TRACE: TryGetPrefetchedReservations found exact-key {key}");
                    return t.Result;
                }

                // alternative canonical (z.B. the explicit configured server vs "." default)
                var altCanonical = CanonicalizeServerKey(GetServerNameOrDefault() ?? ".");
                var altKey = $"{altCanonical}::{scopeId}";

                if (!string.Equals(altKey, key, StringComparison.OrdinalIgnoreCase)
                    && _reservationPrefetchTasks.TryGetValue(altKey, out var t2)
                    && t2.IsCompletedSuccessfully)
                {
                    Helpers.WriteDebugLog($"TRACE: TryGetPrefetchedReservations found alt-key {altKey} for requested {key}");
                    return t2.Result;
                }

                // last-resort: return any completed prefetch that matches the same scopeId (ignore server part)
                foreach (var kv in _reservationPrefetchTasks)
                {
                    try
                    {
                        var parts = kv.Key.Split(new[] { "::" }, StringSplitOptions.None);
                        if (parts.Length == 2 && string.Equals(parts[1], scopeId, StringComparison.OrdinalIgnoreCase))
                        {
                            var task = kv.Value;
                            if (task.IsCompletedSuccessfully)
                            {
                                Helpers.WriteDebugLog($"TRACE: TryGetPrefetchedReservations found wildcard-key {kv.Key} for requested {key}");
                                return task.Result;
                            }
                        }
                    }
                    catch { /* ignore problematic entries */ }
                }
            }
            catch (Exception ex)
            {
                Helpers.WriteDebugLog("TryGetPrefetchedReservations error: " + ex);
            }

            return null;
        }

        /// <summary>
        /// Hilfs-Methode: Normiert einen Server-String zu einem einfachen Schlüsselteil.
        /// "." bleibt ".", leere Strings werden zu ".". Ansonsten lowercased trimmed.
        /// </summary>
        private static string CanonicalizeServerKey(string? server)
        {
            if (string.IsNullOrWhiteSpace(server)) return ".";
            var s = server.Trim();
            if (s == ".") return ".";
            return s.ToLowerInvariant();
        }
    }
}
