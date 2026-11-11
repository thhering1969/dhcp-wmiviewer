// MainForm.Reservations.cs
using System.Net;

namespace DhcpWmiViewer
{
    public partial class MainForm
    {
        private ContextMenuStrip CreateReservationsContextMenu()
        {
            var menu = new ContextMenuStrip();
            var change = new ToolStripMenuItem("Ändern...", null, async (s, e) => await ChangeSelectedReservationAsync());
            var delete = new ToolStripMenuItem("Löschen", null, async (s, e) => await DeleteSelectedReservationAsync());
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
                MessageBox.Show(this, "Die eingegebene IP ist ungültig.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var space = GetSelectedScopeAddressSpace();
            var fields = new List<ConfirmChangeDialog.Field>
            {
                new("IP-Adresse", space.Describe(ip), space.Describe(newIp)),
                new("ClientId (MAC)", clientId, clientId),
                new("Name", name, newName),
                new("Beschreibung", description, newDescription)
            };
            if (!fields.Any(f => f.Changed))
            {
                MessageBox.Show(this, "Es wurde nichts geändert.", "Keine Änderung", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!ConfirmChangeDialog.Confirm(this, "Reservation ändern", $"Reservation {ip} ändern", server, scopeId, fields))
                return;

            var ok = dlg.IpChanged
                ? await RunReservationChangeAsync(
                    () => DhcpManager.ChangeReservationIpAsync(server, scopeId, ip, newIp, clientId, newName, newDescription, GetCredentialsForServer),
                    "Fehler beim Ändern der Reservation")
                : await RunReservationChangeAsync(
                    () => DhcpManager.UpdateReservationAsync(server, scopeId, ip, clientId, newName, newDescription, GetCredentialsForServer),
                    "Fehler beim Aktualisieren der Reservation");
            if (!ok) return;

            var replication = await ReplicateAndLogAsync("ChangeReservation", scopeId, newIp, $"ClientId={clientId};{ConfirmChangeDialog.Summarize(fields, ";")}");
            ShowSuccess("Reservation geändert:\n\n" + ConfirmChangeDialog.Summarize(fields), replication);
        }

        private async Task DeleteSelectedReservationAsync()
        {
            if (!TryGetSelectedReservation(out var ip, out var clientId, out var name, out var description, out var scopeId))
                return;

            var server = _activeServer;
            var fields = new List<ConfirmChangeDialog.Field>
            {
                new("IP-Adresse", GetSelectedScopeAddressSpace().Describe(ip), null),
                new("ClientId (MAC)", clientId, null),
                new("Name", name, null),
                new("Beschreibung", description, null)
            };
            if (!ConfirmChangeDialog.Confirm(this, "Reservation löschen", $"Reservation {ip} löschen", server, scopeId, fields, destructive: true))
                return;

            var ok = await RunReservationChangeAsync(
                () => DhcpManager.DeleteReservationAsync(server, scopeId, ip, GetCredentialsForServer),
                "Fehler beim Löschen der Reservation");
            if (!ok) return;

            var replication = await ReplicateAndLogAsync("DeleteReservation", scopeId, ip, $"Name={name};ClientId={clientId}");
            ShowSuccess($"Reservation gelöscht:\n\nIP: {ip}\nName: {name}\nClientId: {clientId}", replication);
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
                MessageBox.Show(this, errorTitle + ":\n" + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            finally
            {
                Enabled = true;
            }

            await RefreshSelectedScopeAsync();
            return true;
        }

        /// <summary>Erfolgsmeldung inkl. Ergebnis der Failover-Replikation (Warnsymbol, wenn Replikation/Protokoll fehlschlug).</summary>
        private void ShowSuccess(string message, string replication)
        {
            var warn = replication.Contains("FEHLGESCHLAGEN") || replication.Contains("ÜBERSPRUNGEN") || replication.Contains("NICHT geschrieben");
            MessageBox.Show(this, message + "\n\n" + replication, warn ? "Erfolgreich – mit Warnung" : "Erfolgreich",
                MessageBoxButtons.OK, warn ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        private bool TryGetSelectedReservation(out string ip, out string clientId, out string name, out string description, out string scopeId)
        {
            ip = clientId = name = description = scopeId = string.Empty;

            if (dgvReservations.SelectedRows.Count != 1)
            {
                MessageBox.Show(this, "Bitte zuerst genau eine Reservation auswählen.", "Keine Auswahl", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            var row = dgvReservations.SelectedRows[0];
            ip = CellText(row, "IPAddress");
            clientId = CellText(row, "ClientId");
            name = CellText(row, "Name");
            description = CellText(row, "Description");
            if (string.IsNullOrWhiteSpace(ip))
            {
                MessageBox.Show(this, "Die ausgewählte Reservation hat keine IP-Adresse.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            scopeId = GetSelectedScopeId();
            if (string.IsNullOrWhiteSpace(scopeId))
            {
                MessageBox.Show(this, "Bitte zuerst einen Scope oben auswählen.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true;
        }
    }
}
