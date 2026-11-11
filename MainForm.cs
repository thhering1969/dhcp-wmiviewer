// MainForm.cs
using static DhcpWmiViewer.Loc;
// Hauptfenster. Aufteilung:
//   MainForm.cs              Felder, Konstruktor, aktiver Server, Credentials, Status
//   MainForm.Layout.cs       Aufbau der Controls, Spaltenbreiten
//   MainForm.Scopes.cs       Discovery, Scopes abfragen, Reservations/Leases eines Scopes laden
//   MainForm.Reservations.cs Kontextmenü + Ändern/Löschen von Reservations
//   MainForm.Leases.cs       Kontextmenü + Reservation aus Lease anlegen
//   MainForm.Events.cs       Events-Tab
//   MainForm.Logging.cs      Protokollierung von Änderungen ins EventLog des DHCP-Servers

using System.Collections.Concurrent;
using System.Data;
using System.Management.Automation;
using System.Reflection;
using System.Security;

namespace DhcpWmiViewer
{
    public partial class MainForm : Form
    {
        // ---- Controls (werden in InitializeLayout erzeugt) ----
        private TextBox txtServer = null!;
        private ComboBox cmbDiscoveredServers = null!;
        private Button btnDiscover = null!;
        private Button btnQuery = null!;
        private Button btnExportCsv = null!;
        private Label lblStatus = null!;
        private ProgressBar pb = null!;
        private DataGridView dgv = null!;
        private DataGridView dgvReservations = null!;
        private DataGridView dgvLeases = null!;
        private DataGridView dgvEvents = null!;
        private TabPage tabReservations = null!;
        private TabPage tabLeases = null!;
        private Button btnFetchEvents = null!;
        private NumericUpDown nudEventsLookbackDays = null!;
        private NumericUpDown nudEventsMax = null!;

        // ---- Daten ----
        private readonly BindingSource binding = new();
        private readonly BindingSource bindingReservations = new();
        private readonly BindingSource bindingLeases = new();
        private readonly BindingSource bindingEvents = new();
        private DataTable currentTable = DhcpManager.CreateTable(DhcpManager.ScopeColumns);
        private DataTable reservationTable = DhcpManager.CreateTable(DhcpManager.ReservationColumns);
        private DataTable leaseTable = DhcpManager.CreateTable(DhcpManager.LeaseColumns);

        /// <summary>
        /// Server, von dem die angezeigten Scopes stammen. Alle Folgeaktionen (Reservations, Leases,
        /// Anlegen/Ändern/Löschen) verwenden diesen Server – nicht den aktuellen Inhalt des Textfelds.
        /// </summary>
        private string _activeServer = ".";

        // Credentials pro Server (null = Benutzer hat abgebrochen -> nicht erneut fragen)
        private readonly ConcurrentDictionary<string, PSCredential?> _credentialCache = new(StringComparer.OrdinalIgnoreCase);

        public MainForm()
        {
            Text = "DHCP Scope Viewer";
            MinimumSize = new Size(1000, 720);
            Width = Math.Max(Width, 1100);
            Height = Math.Max(Height, 780);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10F);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            InitializeLayout();

            binding.DataSource = currentTable;
            bindingReservations.DataSource = reservationTable;
            bindingLeases.DataSource = leaseTable;

            try
            {
                AdminRightsChecker.CheckAndWarnIfNotAdmin();
            }
            catch (Exception ex)
            {
                Helpers.WriteDebugLog("AdminRightsChecker.CheckAndWarnIfNotAdmin failed: " + ex);
            }

            // Debug-Vorschau der PowerShell-Befehle: Menü Extras (standardmäßig aus)
            UpdateStatus(T("Bereit – Server eingeben und \"Query Scopes\" klicken (oder Enter).", "Ready – enter a server and click \"Query Scopes\" (or press Enter)."));
        }

        // ------------------------------------------------------------------
        // Server / Credentials
        // ------------------------------------------------------------------

        /// <summary>Server aus der UI (Textfeld, sonst Auswahl der Combobox, sonst lokal).</summary>
        private string ReadServerFromUi()
        {
            var server = txtServer.Text.Trim();
            if (server.Length == 0)
                server = cmbDiscoveredServers.SelectedItem?.ToString()?.Trim() ?? string.Empty;
            return server.Length == 0 ? "." : server;
        }

        /// <summary>
        /// Credential-Callback für den PowerShellExecutor: wird nur aufgerufen, wenn die integrierte
        /// Anmeldung scheitert. Fragt einmal pro Server nach und merkt sich das Ergebnis.
        /// </summary>
        private PSCredential? GetCredentialsForServer(string server)
        {
            if (string.IsNullOrWhiteSpace(server) || server.Trim() == ".")
                return null;

            return _credentialCache.GetOrAdd(server.Trim(), AskForCredentials);
        }

        /// <summary>Nur bereits bekannte Credentials, ohne nachzufragen (für Hintergrund-Abfragen).</summary>
        private PSCredential? GetKnownCredentials(string server)
            => _credentialCache.TryGetValue(server.Trim(), out var cred) ? cred : null;

        private PSCredential? AskForCredentials(string server)
        {
            // Wird aus Hintergrund-Threads aufgerufen -> Dialog im UI-Thread anzeigen
            if (InvokeRequired)
                return (PSCredential?)Invoke(new Func<PSCredential?>(() => AskForCredentials(server)));

            using var dlg = new CredentialDialog { Server = server };
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return null;

            var secure = new SecureString();
            foreach (var c in dlg.Password ?? string.Empty) secure.AppendChar(c);
            secure.MakeReadOnly();
            return new PSCredential(dlg.UserName ?? string.Empty, secure);
        }

        private void ClearCredentialCache()
        {
            _credentialCache.Clear();
            // Offene WinRM-Verbindungen (ggf. mit alten Credentials) ebenfalls verwerfen
            PowerShellExecutor.ResetConnections();
        }

        // ------------------------------------------------------------------
        // Status / Busy / Sonstiges
        // ------------------------------------------------------------------

        private void UpdateStatus(string text)
        {
            if (lblStatus.InvokeRequired) lblStatus.BeginInvoke(() => lblStatus.Text = text);
            else lblStatus.Text = text;
        }

        private static bool IsLocalHost(string server) => PowerShellExecutor.IsLocalHost(server);

        /// <summary>
        /// Zeigt einen Fehler verständlich an (Ursache + Hinweis statt Stacktrace).
        /// Die vollständigen Details landen in %TEMP%\DhcpWmiViewer-error.log.
        /// </summary>
        private void ShowError(string title, string? server, Exception ex)
        {
            var details = ex.ToString();
            var logPath = Path.Combine(Path.GetTempPath(), "DhcpWmiViewer-error.log");
            try { File.AppendAllText(logPath, $"{DateTime.Now:s} - {title} (Server: {server}){Environment.NewLine}{details}{Environment.NewLine}--------------------{Environment.NewLine}"); } catch { }

            var msg = ex.Message;
            var target = string.IsNullOrWhiteSpace(server) ? T("der Server", "the server") : server;
            string hint;
            if (msg.Contains("no credentials were provided", StringComparison.OrdinalIgnoreCase))
                hint = T($"Die Anmeldung an {target} wurde abgebrochen.", $"Signing in to {target} was cancelled.");
            else if (msg.Contains("Access is denied", StringComparison.OrdinalIgnoreCase) || msg.Contains("Zugriff verweigert", StringComparison.OrdinalIgnoreCase))
                hint = T($"Zugriff auf {target} verweigert. Der Benutzer braucht Rechte als DHCP-Administrator.", $"Access to {target} denied. The user needs DHCP administrator rights.");
            else if (msg.Contains("WinRM", StringComparison.OrdinalIgnoreCase) || msg.Contains("WS-Management", StringComparison.OrdinalIgnoreCase)
                     || msg.Contains("keine Verbindung", StringComparison.OrdinalIgnoreCase) || msg.Contains("cannot connect", StringComparison.OrdinalIgnoreCase)
                     || msg.Contains("auth/transport", StringComparison.OrdinalIgnoreCase))
                hint = T($"{target} ist nicht erreichbar.\n\nMögliche Ursachen:\n• Server ausgeschaltet oder Name falsch\n• VPN-Verbindung getrennt\n• WinRM auf dem Server nicht aktiviert",
                         $"{target} is not reachable.\n\nPossible causes:\n• Server is off or the name is wrong\n• VPN disconnected\n• WinRM not enabled on the server");
            else
            {
                hint = msg.Length > 400 ? msg[..400] + "…" : msg;
            }

            UpdateStatus($"{title}: {(server ?? "").Trim()}");
            MessageBox.Show(this, $"{hint}\n\n(Details: {logPath})", title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void SetBusy(bool busy)
        {
            pb.Style = ProgressBarStyle.Marquee;
            pb.Visible = busy;
            btnQuery.Enabled = !busy;
        }

        private void BtnExportCsv_Click(object? sender, EventArgs e)
        {
            if (currentTable.Rows.Count == 0) return;

            using var sfd = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "dhcp_scopes.csv" };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                CsvExport.Write(currentTable, sfd.FileName);
                MessageBox.Show(this, T("Export abgeschlossen.", "Export completed."), "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, T("Export fehlgeschlagen: ", "Export failed: ") + ex.Message, "Export", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowAboutDialog()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
            var isAdmin = AdminRightsChecker.IsRunningAsAdministrator();
            var userInfo = AdminRightsChecker.GetCurrentUserInfo();

            var message = "DHCP WMI Viewer\n" +
                          $"Version: {version}\n\n" +
                          T("Verwaltung von Windows-DHCP-Servern: Scopes, Reservierungen, Leases,\n" +
                            "Bereich replizieren (Failover), Protokoll im EventLog.\n\n",
                            "Management of Windows DHCP servers: scopes, reservations, leases,\n" +
                            "scope replication (failover), event log audit trail.\n\n") +
                          T("Aktuelle Sitzung:\n", "Current session:\n") +
                          $"Administrator: {(isAdmin ? T("Ja", "Yes") : T("Nein", "No"))}\n\n" +
                          $"{userInfo}\n\n" +
                          "github.com/thhering1969/dhcp-wmiviewer\n" +
                          T("MIT-Lizenz", "MIT License");

            MessageBox.Show(this, message, T("Über DHCP WMI Viewer", "About DHCP WMI Viewer"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>Wählt bei Rechtsklick die Zeile unter dem Mauszeiger aus (das Kontextmenü zeigt das Grid selbst).</summary>
        private static void SelectRowOnRightClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (sender is not DataGridView grid || e.Button != MouseButtons.Right || e.RowIndex < 0) return;

            grid.ClearSelection();
            grid.Rows[e.RowIndex].Selected = true;
            if (e.ColumnIndex >= 0)
                grid.CurrentCell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
        }

        private static string CellText(DataGridViewRow row, string column)
            => row.DataGridView?.Columns.Contains(column) == true ? row.Cells[column].Value?.ToString() ?? string.Empty : string.Empty;
    }
}
