// MainForm.Scopes.cs
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    public partial class MainForm : Form
    {
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
                var dt = await DhcpManager.QueryScopesAsync(server, GetCredentialsForServer);
                currentTable = dt;
                binding.DataSource = currentTable;
                dgv.DataSource = binding;
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

                // start both queries in parallel
                var tRes = DhcpManager.QueryReservationsAsync(server, scopeId, GetCredentialsForServer);
                var tLea = DhcpManager.QueryLeasesAsync(server, scopeId, GetCredentialsForServer);

                // await results (tasks were started already so this keeps parallelism)
                var resTable = await tRes;
                var leaTable = await tLea;

                // assign & bind on UI-thread (we are in UI context because this is an event handler)
                reservationTable = resTable ?? new System.Data.DataTable();
                bindingReservations.DataSource = reservationTable;
                dgvReservations.DataSource = bindingReservations;
                dgvReservations.ClearSelection();

                leaseTable = leaTable ?? new System.Data.DataTable();
                bindingLeases.DataSource = leaseTable;
                dgvLeases.DataSource = bindingLeases;
                dgvLeases.ClearSelection();

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
    }
}
