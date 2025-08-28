// MainForm.Leases.Handlers.cs

using System;
using System.Data;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    public partial class MainForm : Form
    {
        // existing fields assumed: dgvLeases, txtServer, GetCredentialsForServer, TryGetScopeIdFromSelection, etc.

        // ----- Handler: Create Reservation from Lease (context menu / double click) -----
        private async Task OnCreateReservationFromLeaseAsync()
        {
            try
            {
                Helpers.WriteDebugLog("TRACE: OnCreateReservationFromLeaseAsync entered");

                if (dgvLeases == null || dgvLeases.SelectedRows.Count == 0)
                {
                    MessageBox.Show(this, "Bitte zuerst eine Lease auswählen.", "Keine Auswahl", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var row = dgvLeases.SelectedRows[0];
                var ip = row.Cells["IPAddress"]?.Value?.ToString() ?? string.Empty;
                var clientId = row.Cells["ClientId"]?.Value?.ToString() ?? string.Empty;
                var hostName = row.Cells["HostName"]?.Value?.ToString() ?? string.Empty;

                Helpers.WriteDebugLog($"TRACE: Lease values: IP={ip}, Clientid={clientId}, HostName={hostName}");

                // Try to resolve the scope id from selected scope grid (or other means)
                var scopeId = TryGetScopeIdFromSelection();
                if (string.IsNullOrWhiteSpace(scopeId))
                {
                    MessageBox.Show(this, "Bitte zuerst einen Scope auswählen.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Gather scope meta (start/end/mask) if available from scopes grid (dgv)
                string startRange = string.Empty, endRange = string.Empty, subnetMask = string.Empty;
                try
                {
                    if (dgv != null && dgv.SelectedRows.Count > 0)
                    {
                        var srow = dgv.SelectedRows[0];
                        startRange = srow.Cells["StartRange"]?.Value?.ToString() ?? string.Empty;
                        endRange = srow.Cells["EndRange"]?.Value?.ToString() ?? string.Empty;
                        subnetMask = srow.Cells["SubnetMask"]?.Value?.ToString() ?? string.Empty;
                        Helpers.WriteDebugLog($"TRACE: scope meta start={startRange} end={endRange} mask={subnetMask}");
                    }
                }
                catch { /* best-effort */ }

                // server name
                var server = GetServerNameOrDefault();

                // Decide firewall pool range: (your requirement: 192.168.116.180 - 192.168.116.254)
                // You can compute this dynamically if needed; for now set constants (or fetch from config).
                var firewallStart = "192.168.116.180";
                var firewallEnd = "192.168.116.254";

                Helpers.WriteDebugLog("TRACE: Preparing to create ConvertLeaseToReservationDialog instance");

                // Create dialog instance (parameterless constructor used for compatibility)
                using var dlg = new ConvertLeaseToReservationDialog();

                // Try to set common properties (existing method in MainForm.Core)
                TrySetPropertyIfExists(dlg, "ScopeId", scopeId);
                TrySetPropertyIfExists(dlg, "Scope", scopeId);
                TrySetPropertyIfExists(dlg, "IpAddress", ip);
                TrySetPropertyIfExists(dlg, "IPAddress", ip);
                TrySetPropertyIfExists(dlg, "ClientId", clientId);
                TrySetPropertyIfExists(dlg, "Client", clientId);
                TrySetPropertyIfExists(dlg, "HostName", hostName);
                TrySetPropertyIfExists(dlg, "Name", hostName);
                TrySetPropertyIfExists(dlg, "StartRange", startRange);
                TrySetPropertyIfExists(dlg, "EndRange", endRange);
                TrySetPropertyIfExists(dlg, "SubnetMask", subnetMask);

                // Set firewall range (properties added by the partial class above)
                TrySetPropertyIfExists(dlg, "FirewallStart", firewallStart);
                TrySetPropertyIfExists(dlg, "FirewallEnd", firewallEnd);

                // Assign ReservationLookup delegate so dialog can fetch reservations itself if needed.
                // ReservationLookupAdapter.CreateLookup captures server and credential-provider.
                try
                {
                    var reservationLookup = ReservationLookupAdapter.CreateLookup(server, s => GetCredentialsForServer(s)!);
                    TryAssignDelegatePropertyIfExists(dlg, "ReservationLookup", reservationLookup);
                }
                catch
                {
                    // ignore if assignment fails (dialog still works with prefetched data)
                }

                // If you have a prefetched reservations table available in the form, pass it to dialog
                try
                {
                    if (reservationTable != null && reservationTable.Rows.Count > 0)
                        TrySetPropertyIfExists(dlg, "PrefetchedReservations", reservationTable);
                }
                catch { /* swallow */ }

                // If dialog has an initializer method, call it
                TryInvokeInitializeMethodIfExists(dlg, scopeId, ip, clientId, hostName, startRange, endRange, subnetMask);

                Helpers.WriteDebugLog("TRACE: About to show ConvertLeaseToReservationDialog (ensured)");
                var dr = dlg.ShowDialog(this);
                if (dr == DialogResult.OK)
                {
                    Helpers.WriteDebugLog("TRACE: ConvertLeaseToReservationDialog returned OK");
                    // Optionally refresh reservations / leases after create
                    await TryInvokeRefreshReservations(scopeId);
                    await TryInvokeRefreshLeases(scopeId);
                }
                else
                {
                    Helpers.WriteDebugLog("TRACE: Dialog closed with result: " + dr);
                }
            }
            catch (Exception ex)
            {
                Helpers.WriteDebugLog("TRACE: OnCreateReservationFromLeaseAsync error: " + ex);
                MessageBox.Show(this, "Fehler beim Erstellen der Reservation: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // --- other handlers and helper methods remain unchanged in this partial ---
    }
}
