// MainForm.Scopes.cs
using System.Data;
using static DhcpWmiViewer.Loc;

namespace DhcpWmiViewer
{
    public partial class MainForm
    {
        // Jede Detail-Ladeanforderung bekommt eine Nummer; angezeigt wird nur das Ergebnis der neuesten.
        // Sonst können bei schnellem Scope-Wechsel A -> B die später eintreffenden Daten von A unter B landen.
        private int _detailsRequestVersion;
        private bool _suppressSelectionChanged;

        private async void BtnDiscover_Click(object? sender, EventArgs e)
        {
            btnDiscover.Enabled = false;
            UpdateStatus(T("Suche DHCP-Server im AD...", "Discovering DHCP servers in AD..."));
            cmbDiscoveredServers.Items.Clear();
            try
            {
                var authorized = await Task.Run(DhcpDiscovery.DiscoverDhcpServersInAD);

                // Abgeschaltete Server (Autorisierung im AD noch vorhanden) vor der Anzeige aussortieren
                UpdateStatus(T($"Prüfe {authorized.Count} im AD autorisierte DHCP-Server...", $"Checking {authorized.Count} DHCP servers authorized in AD..."));
                var checks = await DhcpDiscovery.CheckServersAsync(authorized);
                var usable = checks.Where(c => c.Usable).Select(c => c.Server).OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
                var hidden = checks.Where(c => !c.Usable).ToList();

                // Ist gar keiner erreichbar (z. B. VPN getrennt), lieber alle zeigen als eine leere Liste
                var servers = usable.Count > 0 ? usable : authorized;
                foreach (var s in servers) cmbDiscoveredServers.Items.Add(s);

                // Läuft die App selbst auf einem der DHCP-Server? Dann wird für diesen lokal ausgeführt.
                AppEnvironment.SetRunningOnDhcpServer(
                    DhcpDiscovery.LocalHostAppearsInDiscovery(servers) || AppEnvironment.IsDhcpServerServiceRunning());

                if (cmbDiscoveredServers.Items.Count > 0)
                    cmbDiscoveredServers.SelectedIndex = 0; // übernimmt den Server ins Textfeld

                var status = T($"{servers.Count} DHCP-Server gefunden.", $"{servers.Count} DHCP server(s) found.");
                if (usable.Count == 0 && authorized.Count > 0)
                    status += T(" Keiner erreichbar – VPN/Netzwerk prüfen.", " None reachable – check VPN/network.");
                else if (hidden.Count > 0)
                    status += T(" Ausgeblendet: ", " Hidden: ") + string.Join(", ", hidden.Select(h => $"{h.Server} ({h.Reason})"));
                UpdateStatus(status);
                Helpers.WriteDebugLog("Discover: " + string.Join("; ", checks.Select(c => $"{c.Server}={c.Reason}")));
            }
            catch (Exception ex)
            {
                ShowError(T("DHCP-Server konnten nicht gesucht werden", "Could not search for DHCP servers"), null, ex);
            }
            finally
            {
                btnDiscover.Enabled = true;
            }
        }

        private async void BtnQuery_Click(object? sender, EventArgs e)
        {
            var server = ReadServerFromUi();

            // Eigener PC ohne DHCP-Serverdienst: nicht per WinRM auf sich selbst verbinden
            if (IsLocalHost(server) && !AppEnvironment.RunningOnDhcpServer)
            {
                MessageBox.Show(this,
                    T($"Auf diesem Rechner ({Environment.MachineName}) läuft kein DHCP-Server.\n\n" +
                      "Bitte den Namen eines DHCP-Servers eingeben oder mit \"Discover\" die Server im Active Directory suchen.",
                      $"No DHCP server is running on this computer ({Environment.MachineName}).\n\n" +
                      "Please enter the name of a DHCP server or use \"Discover\" to find the servers in Active Directory."),
                    T("Kein DHCP-Server", "No DHCP server"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtServer.Focus();
                txtServer.SelectAll();
                return;
            }

            btnExportCsv.Enabled = false;
            UpdateStatus(T("Frage Scopes ab...", "Querying scopes..."));
            SetBusy(true);
            try
            {
                var dt = await DhcpManager.QueryScopesAsync(server, GetCredentialsForServer);

                _activeServer = server;
                _detailsRequestVersion++; // evtl. noch laufende Detail-Abfragen des alten Servers verwerfen

                // Beim Neubinden wählt das Grid automatisch die erste Zeile -> keine Detail-Abfrage dafür auslösen
                _suppressSelectionChanged = true;
                try
                {
                    currentTable = dt;
                    binding.DataSource = currentTable;
                    dgv.ClearSelection();
                }
                finally
                {
                    _suppressSelectionChanged = false;
                }
                if (dgv.Columns["Description"] is { } descCol)
                {
                    descCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    descCol.MinimumWidth = Math.Max(descCol.Width, 160);
                }

                BindScopeDetails(DhcpManager.CreateTable(DhcpManager.ReservationColumns), DhcpManager.CreateTable(DhcpManager.LeaseColumns));
                btnExportCsv.Enabled = currentTable.Rows.Count > 0;
                UpdateStatus(T($"{currentTable.Rows.Count} Scope(s) auf {server} gefunden.{RouteSuffix(server)}", $"Found {currentTable.Rows.Count} scope(s) on {server}.{RouteSuffix(server)}"));
                UpdateTitle(server);
                AppConfig.SaveLastServer(server);
            }
            catch (Exception ex)
            {
                ShowError(T("Scopes konnten nicht abgefragt werden", "Could not query scopes"), server, ex);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void Dgv_SelectionChanged(object? sender, EventArgs e)
        {
            if (_suppressSelectionChanged) return;

            var scopeId = GetSelectedScopeId();
            if (string.IsNullOrWhiteSpace(scopeId))
            {
                _detailsRequestVersion++; // laufende Abfragen verwerfen
                SetBusy(false);
                BindScopeDetails(DhcpManager.CreateTable(DhcpManager.ReservationColumns), DhcpManager.CreateTable(DhcpManager.LeaseColumns));
                return;
            }

            try
            {
                await LoadScopeDetailsAsync(scopeId);
            }
            catch (Exception ex)
            {
                ShowError(T("Reservations/Leases konnten nicht geladen werden", "Could not load reservations/leases"), _activeServer, ex);
            }
        }

        /// <summary>
        /// Lädt Reservations und Leases des Scopes parallel und zeigt sie an.
        /// Wird bei Scope-Auswahl und nach Änderungen verwendet. Ist inzwischen eine neuere
        /// Anforderung gestartet worden, wird das Ergebnis verworfen.
        /// </summary>
        private async Task LoadScopeDetailsAsync(string scopeId)
        {
            var requestVersion = ++_detailsRequestVersion;
            bool IsStale() => requestVersion != _detailsRequestVersion;

            var server = _activeServer;
            SetBusy(true);
            UpdateStatus(T($"Lade Reservations und Leases für {scopeId} von {server}...", $"Querying reservations & leases for {scopeId} on {server}..."));
            try
            {
                var tRes = DhcpManager.QueryReservationsAsync(server, scopeId, GetCredentialsForServer);
                var tLea = DhcpManager.QueryLeasesAsync(server, scopeId, GetCredentialsForServer);
                await Task.WhenAll(tRes, tLea);

                if (IsStale()) return;

                BindScopeDetails(tRes.Result, tLea.Result);
                UpdateStatus(T($"{reservationTable.Rows.Count} Reservation(s) und {leaseTable.Rows.Count} Lease(s) in Scope {scopeId}.{RouteSuffix(_activeServer)}", $"Found {reservationTable.Rows.Count} reservation(s) and {leaseTable.Rows.Count} lease(s) in scope {scopeId}.{RouteSuffix(_activeServer)}"));
                UpdateTitle(_activeServer);
            }
            catch (Exception ex)
            {
                if (IsStale()) return; // Fehler einer überholten Anforderung nicht anzeigen
                UpdateStatus(T("Fehler: ", "Error: ") + ex.Message);
                throw;
            }
            finally
            {
                // Fortschrittsanzeige gehört der jeweils neuesten Anforderung
                if (!IsStale()) SetBusy(false);
            }
        }

        private void BindScopeDetails(DataTable reservations, DataTable leases)
        {
            reservationTable = reservations;
            bindingReservations.DataSource = reservationTable;
            dgvReservations.ClearSelection();
            AdjustReservationsColumnWidths();

            leaseTable = leases;
            bindingLeases.DataSource = leaseTable;
            dgvLeases.ClearSelection();

            tabReservations.Text = $"Reservations ({reservationTable.Rows.Count})";
            tabLeases.Text = $"Leases ({leaseTable.Rows.Count})";
        }

        /// <summary>Aktualisiert Reservations/Leases des aktuell gewählten Scopes (z. B. nach einer Änderung).</summary>
        private async Task RefreshSelectedScopeAsync()
        {
            var scopeId = GetSelectedScopeId();
            if (string.IsNullOrWhiteSpace(scopeId)) return;

            try
            {
                await LoadScopeDetailsAsync(scopeId);
            }
            catch (Exception ex)
            {
                UpdateStatus(T("Aktualisierung fehlgeschlagen: ", "Refresh failed: ") + ex.Message);
            }
        }

        private ContextMenuStrip CreateScopesContextMenu()
        {
            var menu = new ContextMenuStrip();
            var replicate = new ToolStripMenuItem(T("Bereich replizieren...", "Replicate scope..."), null, async (s, e) => await ReplicateSelectedScopeAsync());
            menu.Items.Add(replicate);
            menu.Opening += (s, e) =>
            {
                if (dgv.SelectedRows.Count != 1) { e.Cancel = true; return; }
                replicate.Enabled = !string.IsNullOrWhiteSpace(_activeServer);
            };
            return menu;
        }

        /// <summary>"Bereich replizieren" von Hand (wie im Kontextmenü der DHCP-Konsole), mit Rückfrage und Protokoll.</summary>
        private async Task ReplicateSelectedScopeAsync()
        {
            var scopeId = GetSelectedScopeId();
            if (string.IsNullOrWhiteSpace(scopeId)) return;
            var name = dgv.SelectedRows.Count == 1 ? CellText(dgv.SelectedRows[0], "Name") : string.Empty;
            var label = string.IsNullOrWhiteSpace(name) ? scopeId : $"{name} ({scopeId})";

            if (MessageBox.Show(this,
                    T($"Bereich {label} jetzt vom Server {_activeServer} auf den Failover-Partner replizieren?\n\n" +
                      "Die Konfiguration des Partners wird dabei mit diesem Server überschrieben.",
                      $"Replicate scope {label} from server {_activeServer} to the failover partner now?\n\n" +
                      "The partner's configuration of this scope will be overwritten with this server's."),
                    T("Bereich replizieren", "Replicate scope"), MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            var result = await ReplicateAndLogAsync(ManualReplication, scopeId, string.Empty, string.Empty);
            MessageBox.Show(this, result.Text, T("Bereich replizieren", "Replicate scope"), MessageBoxButtons.OK, result.IsWarning ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        /// <summary>Hinweis für die Statusleiste, über welchen Weg abgefragt wurde (" (via RSAT)" / " (via WinRM)").</summary>
        private static string RouteSuffix(string? server)
            => PowerShellExecutor.GetLastRoute(server) is string r ? $"  (via {r})" : string.Empty;

        /// <summary>Fenstertitel mit Server und tatsächlich genutztem Weg, z. B. "DHCP Scope Viewer – dhcp01 (via RSAT)".</summary>
        private void UpdateTitle(string? server)
        {
            if (string.IsNullOrWhiteSpace(server)) { Text = "DHCP Scope Viewer"; return; }
            var route = PowerShellExecutor.GetLastRoute(server);
            Text = route == null ? $"DHCP Scope Viewer – {server}" : $"DHCP Scope Viewer – {server} (via {route})";
        }

        private string GetSelectedScopeId()
            => dgv.SelectedRows.Count == 0 ? string.Empty : CellText(dgv.SelectedRows[0], "ScopeId");

        /// <summary>Start/Ende/Maske des gewählten Scopes (leer, falls nicht verfügbar).</summary>
        private (string Start, string End, string Mask) GetSelectedScopeRange()
        {
            if (dgv.SelectedRows.Count == 0) return (string.Empty, string.Empty, string.Empty);
            var row = dgv.SelectedRows[0];
            return (CellText(row, "StartRange"), CellText(row, "EndRange"), CellText(row, "SubnetMask"));
        }

        /// <summary>Adressraum des gewählten Scopes (Subnetz + Teilmenge mit Internetfreigabe).</summary>
        private ScopeAddressSpace GetSelectedScopeAddressSpace()
        {
            if (dgv.SelectedRows.Count == 0) return new ScopeAddressSpace(IpRangeSet.Empty, IpRangeSet.Empty, AppConfig.InternetRanges.Count > 0);
            var row = dgv.SelectedRows[0];
            return IpRanges.ForScope(CellText(row, "ScopeId"), CellText(row, "SubnetMask"), CellText(row, "StartRange"), CellText(row, "EndRange"));
        }
    }
}
