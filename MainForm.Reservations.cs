// MainForm.Reservations.cs
using System.Net;
using static DhcpWmiViewer.Loc;

namespace DhcpWmiViewer
{
    public partial class MainForm
    {
        private ContextMenuStrip CreateReservationsContextMenu()
        {
            var menu = new ContextMenuStrip();
            var change = new ToolStripMenuItem(T("Ändern...", "Edit..."), null, async (s, e) => await ChangeSelectedReservationAsync());
            var delete = new ToolStripMenuItem(T("Löschen", "Delete"), null, async (s, e) => await DeleteSelectedReservationAsync());
            menu.Items.AddRange(new ToolStripItem[] { change, new ToolStripSeparator(), delete });

            menu.Opening += (s, e) =>
            {
                if (dgvReservations.CurrentCell == null || dgvReservations.CurrentCell.RowIndex < 0)
                {
                    e.Cancel = true;
                    return;
                }
                var single = dgvReservations.SelectedRows.Count == 1;
                change.Enabled = single;
                delete.Enabled = single;
            };
            return menu;
        }

        private async void DgvReservations_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            dgvReservations.ClearSelection();
            dgvReservations.Rows[e.RowIndex].Selected = true;
            await ChangeSelectedReservationAsync();
        }

        /// <summary>Öffnet den ChangeReservationDialog für die gewählte Reservation und führt die Änderung aus.</summary>
        private async Task ChangeSelectedReservationAsync()
        {
            if (!TryGetSelectedReservation(out var ip, out var clientId, out var name, out var description, out var scopeId))
                return;

            var (start, end, mask) = GetSelectedScopeRange();
            using var dlg = new ChangeReservationDialog(ip, name, description, start, end, mask);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            var server = _activeServer;
            var newIp = dlg.IpChanged ? dlg.NewIp : ip;
            var newName = dlg.NewName;
            var newDescription = dlg.NewDescription;

            if (dlg.IpChanged && !IPAddress.TryParse(newIp, out _))
            {
                MessageBox.Show(this, T("Die eingegebene IP ist ungültig.", "The IP address entered is invalid."), T("Fehler", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var space = GetSelectedScopeAddressSpace();
            var fields = new List<ConfirmChangeDialog.Field>
            {
                new(T("IP-Adresse", "IP address"), space.Describe(ip), space.Describe(newIp), "IP"),
                new("ClientId (MAC)", clientId, clientId, "ClientId"),
                new("Name", name, newName, "Name"),
                new(T("Beschreibung", "Description"), description, newDescription, "Description")
            };
            if (!fields.Any(f => f.Changed))
            {
                MessageBox.Show(this, T("Es wurde nichts geändert.", "Nothing was changed."), T("Keine Änderung", "No change"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!ConfirmChangeDialog.Confirm(this, T("Reservation ändern", "Edit reservation"), T($"Reservation {ip} ändern", $"Edit reservation {ip}"), server, scopeId, fields))
                return;

            var ok = dlg.IpChanged
                ? await RunReservationChangeAsync(
                    () => DhcpManager.ChangeReservationIpAsync(server, scopeId, ip, newIp, clientId, newName, newDescription, GetCredentialsForServer),
                    T("Fehler beim Ändern der Reservation", "Error changing the reservation"))
                : await RunReservationChangeAsync(
                    () => DhcpManager.UpdateReservationAsync(server, scopeId, ip, clientId, newName, newDescription, GetCredentialsForServer),
                    T("Fehler beim Aktualisieren der Reservation", "Error updating the reservation"));
            if (!ok) return;

            var replication = await ReplicateAndLogAsync("ChangeReservation", scopeId, newIp, $"ClientId={clientId};{ConfirmChangeDialog.SummarizeForLog(fields)}");
            ShowSuccess(T("Reservation geändert:", "Reservation changed:") + "\n\n" + ConfirmChangeDialog.Summarize(fields), replication);
        }

        private async Task DeleteSelectedReservationAsync()
        {
            if (!TryGetSelectedReservation(out var ip, out var clientId, out var name, out var description, out var scopeId))
                return;

            var server = _activeServer;
            var fields = new List<ConfirmChangeDialog.Field>
            {
                new(T("IP-Adresse", "IP address"), GetSelectedScopeAddressSpace().Describe(ip), null, "IP"),
                new("ClientId (MAC)", clientId, null, "ClientId"),
                new("Name", name, null, "Name"),
                new(T("Beschreibung", "Description"), description, null, "Description")
            };
            if (!ConfirmChangeDialog.Confirm(this, T("Reservation löschen", "Delete reservation"), T($"Reservation {ip} löschen", $"Delete reservation {ip}"), server, scopeId, fields, destructive: true))
                return;

            var ok = await RunReservationChangeAsync(
                () => DhcpManager.DeleteReservationAsync(server, scopeId, ip, GetCredentialsForServer),
                T("Fehler beim Löschen der Reservation", "Error deleting the reservation"));
            if (!ok) return;

            var replication = await ReplicateAndLogAsync("DeleteReservation", scopeId, ip, $"Name={name};ClientId={clientId}");
            ShowSuccess(T("Reservation gelöscht:", "Reservation deleted:") + $"\n\nIP: {ip}\nName: {name}\nClientId: {clientId}", replication);
        }

        /// <summary>
        /// Führt eine Änderung aus (Formular währenddessen gesperrt), meldet Fehler und lädt die Ansicht neu.
        /// </summary>
        private async Task<bool> RunReservationChangeAsync(Func<Task> action, string errorTitle)
        {
            try
            {
                Enabled = false;
                await action();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, errorTitle + ":\n" + ex.Message, T("Fehler", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            finally
            {
                Enabled = true;
            }

            await RefreshSelectedScopeAsync();
            return true;
        }

        /// <summary>Erfolgsmeldung inkl. Ergebnis von "Bereich replizieren" (Warnsymbol, wenn Replikation/Protokoll fehlschlug).</summary>
        private void ShowSuccess(string message, ReplicationReport replication)
        {
            MessageBox.Show(this, message + "\n\n" + replication.Text,
                replication.IsWarning ? T("Erfolgreich – mit Warnung", "Done – with warning") : T("Erfolgreich", "Done"),
                MessageBoxButtons.OK, replication.IsWarning ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        private bool TryGetSelectedReservation(out string ip, out string clientId, out string name, out string description, out string scopeId)
        {
            ip = clientId = name = description = scopeId = string.Empty;

            if (dgvReservations.SelectedRows.Count != 1)
            {
                MessageBox.Show(this, T("Bitte zuerst genau eine Reservation auswählen.", "Please select exactly one reservation first."), T("Keine Auswahl", "No selection"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            var row = dgvReservations.SelectedRows[0];
            ip = CellText(row, "IPAddress");
            clientId = CellText(row, "ClientId");
            name = CellText(row, "Name");
            description = CellText(row, "Description");
            if (string.IsNullOrWhiteSpace(ip))
            {
                MessageBox.Show(this, T("Die ausgewählte Reservation hat keine IP-Adresse.", "The selected reservation has no IP address."), T("Fehler", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            scopeId = GetSelectedScopeId();
            if (string.IsNullOrWhiteSpace(scopeId))
            {
                MessageBox.Show(this, T("Bitte zuerst einen Scope oben auswählen.", "Please select a scope above first."), T("Fehler", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true;
        }
    }
}
