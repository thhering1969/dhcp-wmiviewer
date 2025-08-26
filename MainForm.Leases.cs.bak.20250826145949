// MainForm.Leases.cs
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
        /// Wird aufgerufen, wenn der Benutzer "Create reservation from lease..." im Leases-Contextmenu auswählt.
        /// Diese Version ist debugreich: zeigt MessageBoxes an mehreren Punkten, fängt Ausnahmen und
        /// stellt sicher, dass ShowDialog(this) ausgeführt wird.
        /// Ziel: herausfinden, warum nach dem ersten Popup kein Dialog erscheint.
        /// </summary>
        private async Task OnCreateReservationFromLeaseAsync()
        {
            try
            {
                // 1) Einstieg - bestätige, dass Handler aufgerufen wurde
                try { MessageBox.Show(this, "OnCreateReservationFromLeaseAsync entered", "DEBUG", MessageBoxButtons.OK, MessageBoxIcon.Information); } catch { }

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

                // Scope ermitteln
                var scopeId = TryGetScopeIdFromSelection();
                if (string.IsNullOrWhiteSpace(scopeId))
                {
                    MessageBox.Show(this, "Bitte zuerst einen Scope oben auswählen.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Versuche, Range/Subnet aus "dgv" (Scopes grid) falls vorhanden zu lesen — best-effort
                string startRange = string.Empty, endRange = string.Empty, subnetMask = string.Empty;
                try
                {
                    if (dgv != null && dgv.SelectedRows.Count > 0)
                    {
                        var srow = dgv.SelectedRows[0];
                        startRange = TryGetCellValue(srow, "StartRange", "Start") ?? string.Empty;
                        endRange = TryGetCellValue(srow, "EndRange", "End") ?? string.Empty;
                        subnetMask = TryGetCellValue(srow, "SubnetMask", "Mask") ?? string.Empty;
                    }
                }
                catch { /* ignore */ }

                // Debug: Werte anzeigen
                try
                {
                    MessageBox.Show(this,
                        $"Debug Werte:\nIP: {oldIp}\nClientId: {clientId}\nHost: {hostName}\nScope: {scopeId}\nStartRange: {startRange}\nEndRange: {endRange}\nSubnet: {subnetMask}",
                        "DEBUG", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch { }

                // Prefetch description from reservations (optional best-effort)
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
                                prefetchedDescription = dr.Table.Columns.Contains("Description") ? dr["Description"]?.ToString() ?? string.Empty : string.Empty;
                                break;
                            }
                        }
                    }
                }
                catch
                {
                    // ignore - best-effort
                }

                // --------------- Dialog erzeugen / anzeigen ----------------
                try
                {
                    // Debug: vor Konstruktor
                    try { MessageBox.Show(this, "About to construct ConvertLeaseToReservationDialog", "DEBUG", MessageBoxButtons.OK, MessageBoxIcon.Information); } catch { }

                    // Create dialog (ohne reservation-lookup callbacks, um Komplexität zu vermeiden)
                    using var dlg = new ConvertLeaseToReservationDialog(
                        scopeId,
                        oldIp,
                        clientId,
                        hostName,
                        startRange,
                        endRange,
                        subnetMask
                    );

                    // Debug: nach Konstruktor, vor ShowDialog
                    try { MessageBox.Show(this, "Constructed dialog; about to call ShowDialog(this)", "DEBUG", MessageBoxButtons.OK, MessageBoxIcon.Information); } catch { }

                    // Wichtig: auf UI-Thread ShowDialog aufrufen
                    DialogResult dr = DialogResult.None;
                    // Wenn schon auf UI-Thread: einfach ShowDialog(this)
                    if (this.InvokeRequired)
                    {
                        // Wenn InvokeRequired (sehr unwahrscheinlich hier), marshalle auf UI-Thread synchron
                        this.Invoke(new Action(() => { dr = dlg.ShowDialog(this); }));
                    }
                    else
                    {
                        dr = dlg.ShowDialog(this);
                    }

                    // Debug: ShowDialog ist zurückgekommen
                    try { MessageBox.Show(this, $"ShowDialog returned: {dr}", "DEBUG", MessageBoxButtons.OK, MessageBoxIcon.Information); } catch { }

                    if (dr != DialogResult.OK)
                    {
                        // Abgebrochen / Cancel -> nichts weiter
                        return;
                    }

                    // Falls OK -> führe CreateReservationFromLeaseAsync aus (best-effort)
                    try
                    {
                        var server = GetServerNameOrDefault();
                        this.Enabled = false;
                        await DhcpManager.CreateReservationFromLeaseAsync(
                            server,
                            scopeId,
                            oldIp,
                            dlg.IpAddress,   // New IP aus Dialog (Property heißt IpAddress)
                            dlg.ClientId,
                            dlg.Name,
                            dlg.Description,
                            s => GetCredentialsForServer(s)!
                        );
                        MessageBox.Show(this, "Reservation erfolgreich erstellt.", "Erfolg", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        await TryInvokeRefreshReservations(scopeId);
                        await TryInvokeRefreshLeases(scopeId);
                    }
                    catch (Exception exCreate)
                    {
                        MessageBox.Show(this, "Fehler beim Erstellen der Reservation: " + exCreate.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    finally
                    {
                        this.Enabled = true;
                    }
                }
                catch (Exception exDlg)
                {
                    // Wenn Konstruktor oder ShowDialog eine Exception wirft, zeige sie an
                    try { MessageBox.Show(this, "Dialog-Fehler: " + exDlg.ToString(), "DEBUG: Dialog Exception", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
                    return;
                }
            }
            catch (Exception ex)
            {
                try { MessageBox.Show(this, "Unerwarteter Fehler in OnCreateReservationFromLeaseAsync: " + ex.ToString(), "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
            }
        }
    }
}
