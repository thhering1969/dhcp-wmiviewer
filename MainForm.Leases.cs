// MainForm.Leases.cs
namespace DhcpWmiViewer
{
    public partial class MainForm
    {
        private ContextMenuStrip CreateLeasesContextMenu()
        {
            var menu = new ContextMenuStrip();
            var create = new ToolStripMenuItem("Create reservation from lease...", null, async (s, e) => await CreateReservationFromSelectedLeaseAsync());
            var refresh = new ToolStripMenuItem("Refresh leases", null, async (s, e) => await RefreshSelectedScopeAsync());
            menu.Items.AddRange(new ToolStripItem[] { create, new ToolStripSeparator(), refresh });

            menu.Opening += (s, e) => create.Enabled = dgvLeases.SelectedRows.Count == 1;
            return menu;
        }

        /// <summary>Öffnet den ConvertLeaseToReservationDialog für die gewählte Lease und legt die Reservation an.</summary>
        private async Task CreateReservationFromSelectedLeaseAsync()
        {
            if (dgvLeases.SelectedRows.Count != 1)
            {
                MessageBox.Show(this, "Bitte zuerst eine Lease auswählen.", "Keine Auswahl", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var scopeId = GetSelectedScopeId();
            if (string.IsNullOrWhiteSpace(scopeId))
            {
                MessageBox.Show(this, "Bitte zuerst einen Scope auswählen.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                new("IP-Adresse", null, GetSelectedScopeAddressSpace().Describe(ip)),
                new("ClientId (MAC)", null, clientId),
                new("Name", null, name),
                new("Beschreibung", null, description)
            };
            if (!ConfirmChangeDialog.Confirm(this, "Reservation anlegen", $"Neue Reservation {ip} anlegen", server, scopeId, fields))
                return;

            try
            {
                Helpers.WriteDebugLog($"CREATE reservation: srv={server}, scopeId={scopeId}, ip={ip}, clientId={clientId}");
                await WaitDialog.RunAsync(this, "Reservation wird angelegt…", () =>
                    DhcpManager.CreateReservationAsync(server, scopeId, ip, clientId, name, description, GetCredentialsForServer));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Reservation konnte nicht erstellt werden:\r\n" + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            await WaitDialog.RunAsync(this, "Aktualisiere Ansicht…", RefreshSelectedScopeAsync);
            var replication = await ReplicateAndLogAsync("ConvertLeaseToReservation", scopeId, ip, $"ClientId={clientId};Name={name}");
            ShowSuccess("Reservation angelegt:\n\n" + ConfirmChangeDialog.Summarize(fields), replication);
        }
    }
}
