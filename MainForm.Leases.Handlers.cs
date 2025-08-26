// MainForm.Leases.Handlers.cs
using System;
using System.Data;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// Wird aufgerufen, wenn der Benutzer "Create reservation from lease..." auswählt.
        /// </summary>
        private async Task OnCreateReservationFromLeaseAsync()
        {
            try
            {
                try { this.BeginInvoke(new Action(() => MessageBox.Show(this, "OnCreateReservationFromLeaseAsync entered", "DEBUG"))); } catch { }

                if (dgvLeases == null || dgvLeases.SelectedRows.Count == 0)
                {
                    MessageBox.Show(this, "Bitte zuerst eine Lease-Zeile auswählen.", "Keine Auswahl", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var leaseRow = dgvLeases.SelectedRows[0];
                var (oldIp, clientId, hostName) = ReadLeaseRowValuesSafe(leaseRow);

                if (string.IsNullOrWhiteSpace(oldIp))
                {
                    MessageBox.Show(this, "Die ausgewählte Zeile enthält keine IP-Adresse.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var scopeId = TryGetScopeIdFromSelection();
                if (string.IsNullOrWhiteSpace(scopeId))
                {
                    MessageBox.Show(this, "Bitte zuerst einen Scope oben auswählen.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string prefetchedDescription = string.Empty;
                try
                {
                    var server = GetServerNameOrDefault();
                    var resTable = await DhcpManager.QueryReservationsAsync(server, scopeId, s => GetCredentialsForServer(s)!);
                    if (resTable != null)
                    {
                        foreach (DataRow dr in resTable.Rows)
                        {
                            var ipVal = dr["IPAddress"]?.ToString() ?? string.Empty;
                            if (string.Equals(ipVal, oldIp, StringComparison.OrdinalIgnoreCase))
                            {
                                prefetchedDescription = dr["Description"]?.ToString() ?? string.Empty;
                                break;
                            }
                        }
                    }
                }
                catch
                {
                    // ignore - best-effort
                }

                using var dlg = new ConvertLeaseToReservationDialog(
                    scopeId,
                    oldIp,
                    clientId,
                    hostName,
                    /* startRange */ string.Empty,
                    /* endRange */ string.Empty,
                    /* subnetMask */ string.Empty,
                    reservationLookupByScope: async (sc) =>
                    {
                        try
                        {
                            var server = GetServerNameOrDefault();
                            return await DhcpManager.QueryReservationsAsync(server, sc, s => GetCredentialsForServer(s)!).ConfigureAwait(false);
                        }
                        catch { return new DataTable(); }
                    },
                    reservationDeleteByScopeAndIp: async (sc, ip) =>
                    {
                        try
                        {
                            var server = GetServerNameOrDefault();
                            // <-- HIER: some DeleteReservationAsync overloads expect an extra string (z.B. clientId/name).
                            // pass empty string as placeholder; replace if your signature requires a specific value.
                            await DhcpManager.DeleteReservationAsync(server, sc, ip, string.Empty, s => GetCredentialsForServer(s)!);
                            return true;
                        }
                        catch
                        {
                            return false;
                        }
                    }
                );

                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    this.Enabled = false;
                    var server2 = GetServerNameOrDefault();

                    await DhcpManager.CreateReservationFromLeaseAsync(
                        server2,
                        scopeId,
                        oldIp,
                        dlg.ClientId,
                        dlg.Name,
                        dlg.Description,
                        s => GetCredentialsForServer(s)!
                    );

                    MessageBox.Show(this, "Reservation erfolgreich erstellt.", "Erfolg", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    await TryInvokeRefreshReservations(scopeId);
                    await TryInvokeRefreshLeases(scopeId);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Fehler beim Erstellen der Reservation:\n" + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    this.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Unerwarteter Fehler: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ... falls OnChangeReservationFromLeaseRowAsync bereits existiert, lass sie unverändert ...
    }
}
