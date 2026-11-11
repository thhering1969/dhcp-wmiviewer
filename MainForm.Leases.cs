// MainForm.Leases.cs
using static DhcpWmiViewer.Loc;
namespace DhcpWmiViewer
{
    public partial class MainForm
    {
        private ContextMenuStrip CreateLeasesContextMenu()
        {
            var menu = new ContextMenuStrip();
            var create = new ToolStripMenuItem(T("Reservation aus Lease erstellen...", "Create reservation from lease..."), null, async (s, e) => await CreateReservationFromSelectedLeaseAsync());
            var refresh = new ToolStripMenuItem(T("Leases aktualisieren", "Refresh leases"), null, async (s, e) => await RefreshSelectedScopeAsync());
            menu.Items.AddRange(new ToolStripItem[] { create, new ToolStripSeparator(), refresh });

            menu.Opening += (s, e) => create.Enabled = dgvLeases.SelectedRows.Count == 1;
            return menu;
        }

        /// <summary>Öffnet den ConvertLeaseToReservationDialog für die gewählte Lease und legt die Reservation an.</summary>
        private async Task CreateReservationFromSelectedLeaseAsync()
        {
            if (dgvLeases.SelectedRows.Count != 1)
            {
                MessageBox.Show(this, T("Bitte zuerst eine Lease auswählen.", "Please select a lease first."), T("Keine Auswahl", "No selection"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var scopeId = GetSelectedScopeId();
            if (string.IsNullOrWhiteSpace(scopeId))
            {
                MessageBox.Show(this, T("Bitte zuerst einen Scope auswählen.", "Please select a scope first."), T("Fehler", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var row = dgvLeases.SelectedRows[0];
            var server = _activeServer;

            using var dlg = new ConvertLeaseToReservationDialog
            {
                IpAddress = CellText(row, "IPAddress"),
                ClientId = CellText(row, "ClientId"),
                HostName = CellText(row, "HostName"),
                ScopeId = scopeId,
                AddressSpace = GetSelectedScopeAddressSpace(),
                PrefetchedReservations = reservationTable.Rows.Count > 0 ? reservationTable : null,
                PrefetchedLeases = leaseTable.Rows.Count > 0 ? leaseTable : null,
                // Fallback, falls keine Reservations vorab geladen sind (ohne Credential-Abfrage)
                ReservationLookup = id => DhcpManager.QueryReservationsAsync(server, id, GetKnownCredentials)
            };

            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            var ip = dlg.IpAddress;
            var clientId = dlg.ClientId;
            var name = dlg.HostName;
            var description = dlg.Description;
            if (string.IsNullOrWhiteSpace(ip)) return;

            var fields = new List<ConfirmChangeDialog.Field>
            {
                new(T("IP-Adresse", "IP address"), null, GetSelectedScopeAddressSpace().Describe(ip), "IP"),
                new("ClientId (MAC)", null, clientId, "ClientId"),
                new("Name", null, name, "Name"),
                new(T("Beschreibung", "Description"), null, description, "Description")
            };
            if (!ConfirmChangeDialog.Confirm(this, T("Reservation anlegen", "Create reservation"), T($"Neue Reservation {ip} anlegen", $"Create new reservation {ip}"), server, scopeId, fields))
                return;

            try
            {
                Helpers.WriteDebugLog($"CREATE reservation: srv={server}, scopeId={scopeId}, ip={ip}, clientId={clientId}");
                await WaitDialog.RunAsync(this, T("Reservation wird angelegt…", "Creating reservation…"), () =>
                    DhcpManager.CreateReservationAsync(server, scopeId, ip, clientId, name, description, GetCredentialsForServer));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, T("Reservation konnte nicht erstellt werden:", "Could not create the reservation:") + "\r\n" + ex.Message, T("Fehler", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            await WaitDialog.RunAsync(this, T("Aktualisiere Ansicht…", "Refreshing view…"), RefreshSelectedScopeAsync);
            var replication = await ReplicateAndLogAsync("ConvertLeaseToReservation", scopeId, ip, $"ClientId={clientId};Name={name}");
            ShowSuccess(T("Reservation angelegt:", "Reservation created:") + "\n\n" + ConfirmChangeDialog.Summarize(fields), replication);
        }
    }
}
