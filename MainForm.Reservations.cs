// MainForm.Reservations.cs

using System;
using System.Data;
using System.Drawing;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    public partial class MainForm : Form
    {
        // --- Dieser Teil enthält die Hauptlogik zum Ändern einer Reservation,
        //     aufgerufen z.B. aus dem Context-Menu oder einem DoubleClick.
        //     Achte darauf, dass diese Datei keine Event-Handler enthält,
        //     die in Layout.cs referenziert werden (die kommen in ReservationsHandlers.cs).
        // ---

        /// <summary>
        /// Öffnet den ChangeReservationDialog für eine ausgewählte Reservation (von Reservations-Grid).
        /// Diese Methode enthält die eigentliche Änder-Logik und wird von den Handlern aufgerufen.
        /// </summary>
        private async Task OnChangeReservationFromReservationsAsync()
        {
            try
            {
                if (dgvReservations == null || dgvReservations.SelectedRows.Count == 0)
                {
                    MessageBox.Show(this, "Bitte zuerst eine Reservation auswählen.", "Keine Auswahl", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var row = dgvReservations.SelectedRows[0];
                var ip = row.Cells["IPAddress"]?.Value?.ToString() ?? string.Empty;
                var clientId = row.Cells["ClientId"]?.Value?.ToString() ?? string.Empty;
                var name = row.Cells["Name"]?.Value?.ToString() ?? string.Empty;
                var description = row.Cells["Description"]?.Value?.ToString() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(ip))
                {
                    MessageBox.Show(this, "Die ausgewählte Reservation hat keine IP-Adresse.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var scopeId = TryGetScopeIdFromSelection() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(scopeId))
                {
                    MessageBox.Show(this, "Bitte zuerst einen Scope oben auswählen.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Scope-Metadaten (falls vorhanden)
                string startRange = string.Empty, endRange = string.Empty, subnetMask = string.Empty;
                try
                {
                    if (dgv != null && dgv.SelectedRows.Count > 0)
                    {
                        var srow = dgv.SelectedRows[0];
                        startRange = srow.Cells["StartRange"]?.Value?.ToString() ?? string.Empty;
                        endRange = srow.Cells["EndRange"]?.Value?.ToString() ?? string.Empty;
                        subnetMask = srow.Cells["SubnetMask"]?.Value?.ToString() ?? string.Empty;
                    }
                }
                catch { /* best-effort */ }

                var server = GetServerNameOrDefault();

                // open ChangeReservationDialog (ersetzt EditIpDialog)
                using var dlg = new ChangeReservationDialog(ip, name, description, startRange, endRange, subnetMask);
                // Description ist bereits per ctor gesetzt, aber setze hier nochmal für volle Kompatibilität
                dlg.Description = description ?? string.Empty;

                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                var newIp = dlg.NewIp?.Trim() ?? string.Empty;
                var newName = dlg.NewName?.Trim() ?? string.Empty;
                var newDescription = dlg.NewDescription?.Trim() ?? string.Empty;

                if (dlg.IpChanged)
                {
                    if (!IPAddress.TryParse(newIp, out _))
                    {
                        MessageBox.Show(this, "Die eingegebene IP ist ungültig.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var confirm = MessageBox.Show(this, $"Reservation ändern:\n\nScope: {scopeId}\nClientId: {clientId}\nVon: {ip}\nNach: {newIp}\n\nFortfahren?", "Bestätigen", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (confirm != DialogResult.Yes) return;

                    try
                    {
                        this.Enabled = false;
                        await DhcpManager.ChangeReservationIpAsync(
                            server,
                            scopeId,
                            ip,
                            newIp,
                            clientId,
                            newName,
                            newDescription,
                            s => GetCredentialsForServerWithTracking(s)   // use cached credentials or integrated auth
                        );

                        MessageBox.Show(this, "Reservation erfolgreich geändert.", "Erfolg", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        await TryInvokeRefreshReservations(scopeId);
                        await TryInvokeRefreshLeases(scopeId);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "Fehler beim Ändern der Reservation:\n" + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    finally
                    {
                        this.Enabled = true;
                    }
                }
                else
                {
                    // Nur Metadaten aktualisieren
                    try
                    {
                        this.Enabled = false;
                        await DhcpManager.UpdateReservationPropertiesAsync(
                            server,
                            scopeId,
                            ip,
                            clientId,
                            newName,
                            newDescription,
                            s => GetCredentialsForServerWithTracking(s)
                        );

                        MessageBox.Show(this, "Reservation-Eigenschaften erfolgreich aktualisiert.", "Erfolg", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        await TryInvokeRefreshReservations(scopeId);
                        await TryInvokeRefreshLeases(scopeId);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "Fehler beim Aktualisieren der Reservationseigenschaften:\n" + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    finally
                    {
                        this.Enabled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Unerwarteter Fehler: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // --- Ende Reservations-spezifischer Methoden in dieser Datei ---
    }
}
