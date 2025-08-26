// MainForm.Leases.Handlers.cs
using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// Wird aufgerufen, wenn aus dem Leases-Context-Menu "Create reservation from lease..." ausgewählt wird.
        /// Diese Implementierung ist defensiv: prüft Auswahl, ermittelt Scope/Range, konstruiert Dialog
        /// und zeigt ihn robust auf dem UI-Thread. Zusätzliche Debug-MessageBoxes helfen beim Troubleshooting.
        /// </summary>
        private async Task OnCreateReservationFromLeaseAsync()
        {
            try
            {
                // DEBUG: Einstieg
                try { MessageBox.Show(this, "OnCreateReservationFromLeaseAsync entered", "DEBUG"); } catch { }

                // Auswahl prüfen
                if (dgvLeases == null || dgvLeases.SelectedRows.Count == 0)
                {
                    try { MessageBox.Show(this, "Bitte zuerst eine Lease-Zeile auswählen.", "Keine Auswahl", MessageBoxButtons.OK, MessageBoxIcon.Information); } catch { }
                    return;
                }

                var leaseRow = dgvLeases.SelectedRows[0];
                var (oldIp, clientId, hostName) = ReadLeaseRowValuesSafe(leaseRow);

                if (string.IsNullOrWhiteSpace(oldIp))
                {
                    try { MessageBox.Show(this, "Die ausgewählte Zeile enthält keine IP-Adresse.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
                    return;
                }

                // Scope ermitteln (best-effort)
                var scopeId = TryGetScopeIdFromSelection();
                if (string.IsNullOrWhiteSpace(scopeId))
                {
                    try { MessageBox.Show(this, "Bitte zuerst einen Scope oben auswählen.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
                    return;
                }

                // Versuche, Range/Subnet aus Scopes grid zu lesen — best-effort
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

                // Prefetch Description (optional, best-effort) - keine Fehler throwen
                string prefetchedDescription = string.Empty;
                try
                {
                    var server = GetServerNameOrDefault();
                    var resTable = await DhcpManager.QueryReservationsAsync(server, scopeId, s => GetCredentialsForServer(s)!);
                    if (resTable != null)
                    {
                        foreach (DataRow dr in resTable.Rows)
                        {
                            var ipVal = dr.Table.Columns.Contains("IPAddress") ? dr["IPAddress"]?.ToString() ?? string.Empty : string.Empty;
                            if (string.Equals(ipVal, oldIp, StringComparison.OrdinalIgnoreCase))
                            {
                                prefetchedDescription = dr.Table.Columns.Contains("Description") ? dr["Description"]?.ToString() ?? string.Empty : string.Empty;
                                break;
                            }
                        }
                    }
                }
                catch { /* ignore */ }

                // ---------------- Dialog erzeugen & anzeigen ----------------
                ConvertLeaseToReservationDialog dlg = null!;
                try
                {
                    // Konstruktion: wenn Konstruktor fehlschlägt, wird Exception gezeigt
                    try
                    {
                        dlg = new ConvertLeaseToReservationDialog(
                            scopeId,
                            oldIp,
                            clientId,
                            hostName,
                            startRange,
                            endRange,
                            subnetMask
                        );
                    }
                    catch (Exception ctorEx)
                    {
                        try { MessageBox.Show(this, "Fehler beim Erstellen des Dialogs: " + ctorEx.ToString(), "Dialog-Konstruktion fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
                        return;
                    }

                    // DEBUG vor ShowDialog
                    try { MessageBox.Show(this, "Constructed ConvertLeaseToReservationDialog; about to ShowDialog", "DEBUG"); } catch { }

                    // Sicherstellen, dass wir auf UI-Thread sind und ShowDialog so aufrufen, dass es sichtbar ist.
                    // Manchmal landet Modal-Dialog hinter dem Hauptfenster — wir setzen temporär TopMost für Debug.
                    bool showDialogSucceeded = false;
                    DialogResult dr = DialogResult.None;

                    Action showAction = () =>
                    {
                        try
                        {
                            // DEBUG: mark dialog temporarily topmost to detect z-order issues
                            bool wasTopMost = dlg.TopMost;
                            try
                            {
                                dlg.TopMost = true;
                            }
                            catch { /* ignore if property not available */ }

                            // Versuche mit Owner
                            dr = dlg.ShowDialog(this);

                            // restore TopMost
                            try { dlg.TopMost = wasTopMost; } catch { }
                            showDialogSucceeded = true;
                        }
                        catch (Exception exShow)
                        {
                            // Wenn ShowDialog mit Owner Probleme macht, versuchen wir ohne Owner
                            try
                            {
                                try { dlg.TopMost = true; } catch { }
                                dr = dlg.ShowDialog();
                                showDialogSucceeded = true;
                            }
                            catch (Exception exShow2)
                            {
                                // beides gescheitert -> zeigen
                                try { MessageBox.Show(this, "ShowDialog schlägt fehl: " + exShow2.ToString(), "Dialog-Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
                            }
                        }
                    };

                    if (this.InvokeRequired)
                    {
                        // synchron auf UI-Thread ausführen, damit ShowDialog blockierend funktioniert
                        this.Invoke(showAction);
                    }
                    else
                    {
                        showAction();
                    }

                    // DEBUG nach ShowDialog (wenn wir überhaupt hierher kommen)
                    try { MessageBox.Show(this, $"ShowDialog finished (succeeded={showDialogSucceeded}) returned: {dr}", "DEBUG"); } catch { }

                    if (!showDialogSucceeded)
                    {
                        // Dialog wurde nicht erfolgreich gezeigt -> abbrechen
                        return;
                    }

                    if (dr != DialogResult.OK)
                    {
                        // Benutzer hat abgebrochen -> nichts weiter tun
                        return;
                    }

                    // Wenn OK: führe CreateReservationFromLeaseAsync aus
                    try
                    {
                        var server = GetServerNameOrDefault();
                        this.Enabled = false;
                        await DhcpManager.CreateReservationFromLeaseAsync(
                            server,
                            scopeId,
                            oldIp,
                            dlg.IpAddress,
                            dlg.ClientId,
                            dlg.Name,
                            dlg.Description,
                            s => GetCredentialsForServer(s)!
                        ).ConfigureAwait(false);

                        // UI-Benachrichtigung (auf UI-Thread)
                        try
                        {
                            this.BeginInvoke(new Action(() =>
                            {
                                MessageBox.Show(this, "Reservation erfolgreich erstellt.", "Erfolg", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }));
                        }
                        catch { }
                        await TryInvokeRefreshReservations(scopeId).ConfigureAwait(false);
                        await TryInvokeRefreshLeases(scopeId).ConfigureAwait(false);
                    }
                    catch (Exception exCreate)
                    {
                        try { MessageBox.Show(this, "Fehler beim Erstellen der Reservation: " + exCreate.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
                    }
                    finally
                    {
                        try { this.BeginInvoke(new Action(() => { this.Enabled = true; })); } catch { }
                    }
                }
                finally
                {
                    // Dispose dialog falls nicht bereits disposet (defensive)
                    try { dlg?.Dispose(); } catch { }
                }
            }
            catch (Exception ex)
            {
                try { MessageBox.Show(this, "Unerwarteter Fehler in OnCreateReservationFromLeaseAsync: " + ex.ToString(), "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
            }
        }

        /// <summary>
        /// Handler für "Change reservation IP..." aus dem Leases-Kontextmenü.
        /// Implementiert analoge Defensive wie Create-Handler.
        /// </summary>
        private async Task OnChangeReservationFromLeaseRowAsync()
        {
            try
            {
                // DEBUG
                try { MessageBox.Show(this, "OnChangeReservationFromLeaseRowAsync entered", "DEBUG"); } catch { }

                if (dgvLeases == null || dgvLeases.SelectedRows.Count == 0)
                {
                    try { MessageBox.Show(this, "Bitte zuerst eine Lease-Zeile auswählen.", "Keine Auswahl", MessageBoxButtons.OK, MessageBoxIcon.Information); } catch { }
                    return;
                }

                var row = dgvLeases.SelectedRows[0];
                var (ip, clientId, host) = ReadLeaseRowValuesSafe(row);

                if (string.IsNullOrWhiteSpace(ip))
                {
                    try { MessageBox.Show(this, "IP in der ausgewählten Zeile nicht gefunden.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
                    return;
                }

                // Hier könnte ein Dialog zur IP-Änderung aufgerufen werden; für jetzt zeige nur Debug
                try { MessageBox.Show(this, $"ChangeReservation requested for {host} / {ip}", "DEBUG", MessageBoxButtons.OK, MessageBoxIcon.Information); } catch { }

                // TODO: echte Implementierung: Open ChangeReservationDialog, call DhcpManager.ChangeReservationIpAsync etc.
            }
            catch (Exception ex)
            {
                try { MessageBox.Show(this, "Unerwarteter Fehler in OnChangeReservationFromLeaseRowAsync: " + ex.ToString(), "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
            }
        }
    }
}
