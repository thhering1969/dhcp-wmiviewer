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
                if (string.IsNullOrWhiteSpace(server) || server == "." || server.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase) || server.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(this, "Bitte zuerst einen entfernten DHCP-Server auswählen (z.B. vmdc3/vmdc4).", "Server fehlt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

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
                    var reservationLookup = ReservationLookupAdapter.CreateLookup(server, s => null);
                    TryAssignDelegatePropertyIfExists(dlg, "ReservationLookup", reservationLookup);
                }
                catch
                {
                    // ignore if assignment fails (dialog still works with prefetched data)
                }

                // If you have a prefetched reservations/leases table available in the form, pass it to dialog
                try
                {
                    if (reservationTable != null && reservationTable.Rows.Count > 0)
                        TrySetPropertyIfExists(dlg, "PrefetchedReservations", reservationTable);
                    if (leaseTable != null && leaseTable.Rows.Count > 0)
                        TrySetPropertyIfExists(dlg, "PrefetchedLeases", leaseTable);
                }
                catch { /* swallow */ }

                // If dialog has an initializer method, call it
                TryInvokeInitializeMethodIfExists(dlg, scopeId, ip, clientId, hostName, startRange, endRange, subnetMask);

                Helpers.WriteDebugLog("TRACE: About to show ConvertLeaseToReservationDialog (ensured)");
                var dr = dlg.ShowDialog(this);
                if (dr == DialogResult.OK)
                {
                    Helpers.WriteDebugLog("TRACE: ConvertLeaseToReservationDialog returned OK");

                    try
                    {
                        // Werte ggf. aus dem Dialog lesen (falls der Nutzer sie geändert hat)
                        string pickedIp = ReadDialogStringProperty(dlg, new[] { "IpAddress", "IPAddress" }) ?? ip;
                        string pickedClientId = ReadDialogStringProperty(dlg, new[] { "ClientId", "Client" }) ?? clientId;
                        string pickedName = ReadDialogStringProperty(dlg, new[] { "HostName", "Name" }) ?? hostName;
                        string pickedDesc = ReadDialogStringProperty(dlg, new[] { "Description" }) ?? string.Empty;

                        if (!string.IsNullOrWhiteSpace(pickedIp))
                        {
                            var srv = server; // reuse resolved server (e.g., vmdc3/vmdc4), avoid fallback to local host
                            // Hard-guard: wenn trotzdem '.' o.ä., dann aus cmb/text neu lesen
                            if (string.IsNullOrWhiteSpace(srv) || srv == "." || srv.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase) || srv.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    if (cmbDiscoveredServers != null)
                                    {
                                        if (cmbDiscoveredServers.SelectedItem != null)
                                            srv = cmbDiscoveredServers.SelectedItem.ToString() ?? srv;
                                        else if (!string.IsNullOrWhiteSpace(cmbDiscoveredServers.Text))
                                            srv = cmbDiscoveredServers.Text.Trim();
                                    }
                                    if ((string.IsNullOrWhiteSpace(srv) || srv == ".") && txtServer != null && !string.IsNullOrWhiteSpace(txtServer.Text))
                                        srv = txtServer.Text.Trim();
                                }
                                catch { }
                            }
                            Helpers.WriteDebugLog($"CREATE reservation: srv={srv}, scopeId={scopeId}, ip={pickedIp}, clientId={pickedClientId}");
                            // Blockierender Warte-Dialog bis Aktion fertig/Fehler
                            await WaitDialog.RunAsync(this, "Reservation wird angelegt…", async () =>
                            {
                                await DhcpManager.CreateReservationFromLeaseAsync(srv, scopeId, pickedIp, pickedClientId, pickedName, pickedDesc, s => GetCredentialsForServerWithTracking(s));
                            });

                            await WaitDialog.RunAsync(this, "Aktualisiere Ansicht…", async () =>
                            {
                                await TryInvokeRefreshReservations(scopeId);
                                await TryInvokeRefreshLeases(scopeId);
                            });

                            // Log GUI event to EventLog (remote) after successful conversion
                            try
                            {
                                await LogGuiEventAsync("ConvertLeaseToReservation", scopeId, pickedIp, $"ClientId={pickedClientId};Name={pickedName}");
                            }
                            catch { /* non-fatal */ }

                            MessageBox.Show(this, $"Reservation für {pickedIp} erstellt.", "Erfolg", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                    catch (Exception exCreate)
                    {
                        MessageBox.Show(this, "Reservation konnte nicht erstellt werden:\r\n" + exCreate.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
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

        private static string? ReadDialogStringProperty(object dlg, string[] names)
        {
            if (dlg == null || names == null || names.Length == 0) return null;
            try
            {
                var t = dlg.GetType();
                foreach (var n in names)
                {
                    if (string.IsNullOrWhiteSpace(n)) continue;
                    var pi = t.GetProperty(n, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.IgnoreCase);
                    if (pi != null)
                    {
                        var v = pi.GetValue(dlg);
                        if (v != null) return v.ToString();
                    }
                    var fi = t.GetField(n, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.IgnoreCase);
                    if (fi != null)
                    {
                        var v = fi.GetValue(dlg);
                        if (v != null) return v.ToString();
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
