// MainForm.Core.cs
// Repository: https://github.com/thhering1969/kurzzeit-dhcp-wmiviewer.git
// Branch:     fix/contextmenu-direct-call
// DIESE DATEI WIRD KOMPLETT ANGEZEIGT — EINFACH KOPIEREN & EINFÜGEN
// Hinweis: Diese Version verzichtet bewusst auf Deklarationen, die in anderen MainForm-Partial-Dateien
// bereits vorhanden sind (Controls, BindingSources, helper methods). Dadurch werden Duplikat-Definitionen vermieden.

using System;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Net;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management.Automation;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Threading;

namespace DhcpWmiViewer
{
    public partial class MainForm : Form
    {
        // ------------------------
        // Konstruktor / Init
        // ------------------------
        public MainForm()
        {
            Text = "DHCP Scope Viewer (remote via WinRM)";
            MinimumSize = new Size(1000, 720);
            Width = Math.Max(Width, 1100);
            Height = Math.Max(Height, 780);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10F);

            // Fenster-Icon automatisch aus der EXE übernehmen (sofern vorhanden)
            try { this.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { /* ignore */ }

            // Layout initialisieren (legt Controls an, verwendet Felder aus MainForm.Controls.cs)
            InitializeLayout();

            // Form-Events für Cleanup
            this.FormClosing += MainForm_FormClosing;

            // DebugLogger mit dieser MainForm-Instanz initialisieren
            try
            {
                DebugLogger.Initialize(this);
                DebugLogger.LogInfo("MainForm initialisiert", "MainForm.Constructor");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DebugLogger.Initialize fehlgeschlagen: {ex.Message}");
            }

            // DHCP-Integration für AD-Tooltips initialisieren
            try
            {
                InitializeDhcpIntegration();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DHCP Integration initialization failed: {ex.Message}");
            }

            // Administratorrechte prüfen (wichtig für DHCP-Verwaltung)
            try
            {
                AdminRightsChecker.CheckAndWarnIfNotAdmin();
            }
            catch (Exception ex)
            {
                // Bei Fehlern in der Admin-Überprüfung: Warnung loggen aber fortfahren
                try
                {
                    System.Diagnostics.Debug.WriteLine($"AdminRightsChecker.CheckAndWarnIfNotAdmin failed: {ex}");
                }
                catch { }
                // Nicht erneut werfen - Anwendung soll trotzdem starten
            }

            // Debug-Modus Nachfrage (einfacher Prompt)
            try
            {
                var dr = MessageBox.Show(
                    "Debug-Modus aktivieren?\n\nWenn Ja: Vor jedem PowerShell-Aufruf wird eine Vorschau (Terminal-Fenster) angezeigt.\nWenn Nein: Keine Vorschau wird gezeigt (nützlich für non-interaktive Nutzung).",
                    "Debug-Modus",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2);

                DhcpManager.ShowCommandPreview = (dr == DialogResult.Yes);
            }
            catch
            {
                DhcpManager.ShowCommandPreview = true;
            }

            // Schema & binding initialisieren
            InitDataTableSchema();

            // bind the scopes grid
            try
            {
                // Binding- und dgv-Fields sind absichtlich NICHT hier deklariert,
                // sie werden in einem anderen Partial (Controls/Layout) deklariert.
                if (binding != null && currentTable != null)
                {
                    binding.DataSource = currentTable;
                }
                if (dgv != null) dgv.DataSource = binding;
            }
            catch { /* defensive */ }

            // Setup partials (Context menu etc.) - partial method kann in anderem Partial implementiert sein
            try
            {
                SetupReservationsContextMenu();
            }
            catch { /* swallow */ }

            UpdateStatus("Ready");
        }

        // ------------------------
        // HINWEIS: Die folgenden Felder/Controls werden NICHT neu deklariert,
        // weil sie bereits in anderen partial-Dateien (z.B. MainForm.Controls.cs) existieren.
        // Beispiele: BindingSources (binding, bindingReservations, bindingLeases, bindingEvents),
        // DataGridViews (dgv, dgvLeases, dgvReservations), lblStatus, contextMenuLeases, etc.
        // ------------------------

        // Lokale DataTables (falls noch nicht woanders deklariert)
        // Wenn sie ebenfalls in einem anderen Partial bereits existieren, entferne die Deklaration hier.
        private DataTable currentTable = new DataTable();
        private DataTable reservationTable = new DataTable();
        private DataTable leaseTable = new DataTable();

        // ------------------------
        // Configuration options
        // ------------------------
        
        /// <summary>
        /// When true, lease refresh will use limited queries (first 100) for better performance.
        /// When false, lease refresh will get all leases (slower but complete).
        /// Always set to true for fastest performance.
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool UseFastLeaseRefresh { get; set; } = true;
        
        /// <summary>
        /// Number of leases to fetch when using fast refresh mode
        /// Very limited for maximum performance
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int FastLeaseRefreshLimit { get; set; } = 10;

        // ------------------------
        // DataTable Schema initialisieren
        // ------------------------
        private void InitDataTableSchema()
        {
            // Hinweis: binding, bindingReservations, bindingLeases und bindingEvents
            // werden in Deinem Controls-Partial erwartet. Hier verwenden wir sie nur,
            // ohne sie erneut zu deklarieren.
            try
            {
                // Scopes table
                currentTable = new DataTable();
                currentTable.Columns.Add("Name", typeof(string));
                currentTable.Columns.Add("ScopeId", typeof(string));
                currentTable.Columns.Add("StartRange", typeof(string));
                currentTable.Columns.Add("EndRange", typeof(string));
                currentTable.Columns.Add("SubnetMask", typeof(string));
                currentTable.Columns.Add("State", typeof(string));
                currentTable.Columns.Add("Description", typeof(string));

                // Reservations
                reservationTable = new DataTable();
                reservationTable.Columns.Add("IPAddress", typeof(string));
                reservationTable.Columns.Add("ClientId", typeof(string));
                reservationTable.Columns.Add("Name", typeof(string));
                reservationTable.Columns.Add("Description", typeof(string));
                reservationTable.Columns.Add("AddressState", typeof(string));

                // Leases
                leaseTable = new DataTable();
                leaseTable.Columns.Add("IPAddress", typeof(string));
                leaseTable.Columns.Add("ClientId", typeof(string));
                leaseTable.Columns.Add("ClientType", typeof(string));
                leaseTable.Columns.Add("HostName", typeof(string));
                leaseTable.Columns.Add("Description", typeof(string));
                leaseTable.Columns.Add("AddressState", typeof(string));
                leaseTable.Columns.Add("LeaseExpiryTime", typeof(string));
                leaseTable.Columns.Add("ScopeId", typeof(string));
                leaseTable.Columns.Add("ServerIP", typeof(string));
                leaseTable.Columns.Add("PSComputerName", typeof(string));
                leaseTable.Columns.Add("CimClass", typeof(string));
                leaseTable.Columns.Add("CimInstanceProperties", typeof(string));
                leaseTable.Columns.Add("CimSystemProperties", typeof(string));
            }
            catch
            {
                // defensive: Fehler beim Schema dürfen nicht crashen
            }
        }

        // ------------------------
        // Utilities: Status / CSV
        // ------------------------
        private void UpdateStatus(string text)
        {
            try
            {
                // lblStatus wird in Controls-Partial deklariert; Zugriff hier ist nur lesend/setzend
                if (lblStatus == null) return;
                if (lblStatus.InvokeRequired) lblStatus.Invoke(new Action(() => lblStatus.Text = text));
                else lblStatus.Text = text;
            }
            catch { /* swallow UI errors */ }
        }

        private void BtnExportCsv_Click(object? sender, EventArgs e)
        {
            try
            {
                if (currentTable == null || currentTable.Rows.Count == 0) return;
                using var sfd = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "dhcp_scopes.csv" };
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    DhcpHelper.ExportDataTableToCsv(currentTable, sfd.FileName);
                    MessageBox.Show(this, "Export completed.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch { /* swallow */ }
        }

        // ------------------------
        // Credentials helpers
        // ------------------------
        
        // Cache für Credentials pro Server
        private readonly Dictionary<string, PSCredential?> _credentialCache = new Dictionary<string, PSCredential?>();
        private readonly HashSet<string> _integratedAuthServers = new HashSet<string>();
        
        public PSCredential? GetCredentialsForServer(string server)
        {
            try
            {
                // Integrierte Auth für lokalen Server (".")
                if (string.IsNullOrWhiteSpace(server) || server.Trim() == ".")
                    return null;

                var normalizedServer = server.Trim().ToLowerInvariant();
                
                // Wenn integrierte Auth für diesen Server bereits erfolgreich war, keine Credentials nötig
                if (_integratedAuthServers.Contains(normalizedServer))
                    return null;
                
                // Wenn bereits Credentials für diesen Server gecacht sind, diese verwenden
                if (_credentialCache.TryGetValue(normalizedServer, out var cachedCred))
                    return cachedCred;

                // Für Remote-Server: Anmeldedaten abfragen (einmal pro Server)
                return AskForCredentials(server);
            }
            catch { return null; }
        }
        
        /// <summary>
        /// Spezielle Credential-Provider-Funktion, die verfolgt, ob integrierte Authentifizierung erfolgreich war.
        /// Diese Funktion wird nur aufgerufen, wenn der erste Versuch ohne Credentials fehlgeschlagen ist.
        /// Das bedeutet, wenn diese Funktion NICHT aufgerufen wird, war integrierte Auth erfolgreich.
        /// </summary>
        public PSCredential? GetCredentialsForServerWithTracking(string server)
        {
            try
            {
                // Integrierte Auth für lokalen Server (".")
                if (string.IsNullOrWhiteSpace(server) || server.Trim() == ".")
                    return null;

                var normalizedServer = server.Trim().ToLowerInvariant();
                
                // Wenn integrierte Auth für diesen Server bereits erfolgreich war, keine Credentials nötig
                if (_integratedAuthServers.Contains(normalizedServer))
                    return null;
                
                // Wenn bereits Credentials für diesen Server gecacht sind, diese verwenden
                if (_credentialCache.TryGetValue(normalizedServer, out var cachedCred))
                    return cachedCred;

                // Wenn wir hier ankommen, bedeutet das, dass integrierte Auth fehlgeschlagen ist
                // und wir Credentials brauchen. Für Remote-Server: Anmeldedaten abfragen
                return AskForCredentials(server);
            }
            catch { return null; }
        }
        
        /// <summary>
        /// Wrapper für DhcpManager-Aufrufe, der automatisch integrierte Authentifizierung erkennt
        /// </summary>
        public async Task<T> ExecuteWithIntegratedAuthDetection<T>(string server, Func<string, Func<string, PSCredential?>?, Task<T>> operation)
        {
            // Null-Check für server-Parameter
            if (string.IsNullOrWhiteSpace(server))
                server = ".";
                
            var normalizedServer = server.Trim().ToLowerInvariant();
            bool credentialCallbackWasCalled = false;
            
            // Credential-Provider, der verfolgt, ob er aufgerufen wurde
            PSCredential? CredentialProviderWithTracking(string s)
            {
                credentialCallbackWasCalled = true;
                return GetCredentialsForServerWithTracking(s);
            }
            
            try
            {
                var result = await operation(server, CredentialProviderWithTracking);
                
                // Wenn der Credential-Callback NICHT aufgerufen wurde, war integrierte Auth erfolgreich
                if (!credentialCallbackWasCalled && !string.IsNullOrWhiteSpace(server) && server.Trim() != ".")
                {
                    MarkServerAsIntegratedAuth(server);
                }
                
                return result;
            }
            catch
            {
                // Bei Fehlern keine integrierte Auth markieren
                throw;
            }
        }
        
        /// <summary>
        /// Wrapper für parallele DhcpManager-Aufrufe (Reservations + Leases)
        /// </summary>
        public async Task<(DataTable reservations, DataTable leases)> ExecuteReservationsAndLeasesWithIntegratedAuthDetection(
            string server, string scopeId, int? leaseLimit = null)
        {
            var normalizedServer = server.Trim().ToLowerInvariant();
            bool credentialCallbackWasCalled = false;
            
            // Credential-Provider, der verfolgt, ob er aufgerufen wurde
            PSCredential? CredentialProviderWithTracking(string s)
            {
                credentialCallbackWasCalled = true;
                return GetCredentialsForServerWithTracking(s);
            }
            
            try
            {
                // Starte beide Abfragen parallel
                var tRes = DhcpManager.QueryReservationsAsync(server, scopeId, CredentialProviderWithTracking);
                var tLea = DhcpManager.QueryLeasesAsync(server, scopeId, CredentialProviderWithTracking, leaseLimit);
                
                // Warte auf beide Ergebnisse
                var resTable = await tRes;
                var leaTable = await tLea;
                
                // Wenn der Credential-Callback NICHT aufgerufen wurde, war integrierte Auth erfolgreich
                if (!credentialCallbackWasCalled && !string.IsNullOrWhiteSpace(server) && server.Trim() != ".")
                {
                    MarkServerAsIntegratedAuth(server);
                }
                
                return (resTable ?? new DataTable(), leaTable ?? new DataTable());
            }
            catch
            {
                // Bei Fehlern keine integrierte Auth markieren
                throw;
            }
        }
        
        /// <summary>
        /// Markiert einen Server als erfolgreich mit integrierter Authentifizierung verbunden
        /// </summary>
        public void MarkServerAsIntegratedAuth(string server)
        {
            if (!string.IsNullOrWhiteSpace(server) && server.Trim() != ".")
            {
                var normalizedServer = server.Trim().ToLowerInvariant();
                _integratedAuthServers.Add(normalizedServer);
                // Entferne gecachte Credentials, da integrierte Auth funktioniert
                _credentialCache.Remove(normalizedServer);
            }
        }
        
        /// <summary>
        /// Löscht alle gecachten Credentials und integrierte Auth Markierungen
        /// </summary>
        public void ClearCredentialCache()
        {
            _credentialCache.Clear();
            _integratedAuthServers.Clear();
        }

        private PSCredential? AskForCredentials(string server)
        {
            PSCredential? result = null;
            try
            {
                if (this.InvokeRequired) this.Invoke(new Action(() => result = AskForCredentialsInternal(server)));
                else result = AskForCredentialsInternal(server);
                
                // Cache das Ergebnis (auch null, um zu vermeiden, dass erneut gefragt wird)
                if (!string.IsNullOrWhiteSpace(server))
                {
                    var normalizedServer = server.Trim().ToLowerInvariant();
                    _credentialCache[normalizedServer] = result;
                }
            }
            catch { result = null; }
            return result;
        }

        private PSCredential? AskForCredentialsInternal(string server)
        {
            using var dlg = new CredentialDialog();
            dlg.Server = server;
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                var username = dlg.UserName ?? string.Empty;
                var pwd = dlg.Password ?? string.Empty;
                var secure = new SecureString();
                foreach (var c in pwd) secure.AppendChar(c);
                secure.MakeReadOnly();
                return new PSCredential(username, secure);
            }
            return null;
        }

        /// <summary>
        /// Zeigt den Über-Dialog an
        /// </summary>
        private void ShowAboutDialog()
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
            var isAdmin = AdminRightsChecker.IsRunningAsAdministrator();
            var userInfo = AdminRightsChecker.GetCurrentUserInfo();
            
            var message = $"DHCP WMI Viewer\n" +
                         $"Version: {version}\n\n" +
                         $"Eine Windows Forms-Anwendung zur Verwaltung von DHCP-Servern\n" +
                         $"mit PowerShell- und WMI-Integration.\n\n" +
                         $"Aktuelle Sitzung:\n" +
                         $"Administrator: {(isAdmin ? "Ja" : "Nein")}\n\n" +
                         $"{userInfo}\n\n" +
                         $"Funktionen:\n" +
                         $"• DHCP-Server-Erkennung\n" +
                         $"• Scope- und Lease-Verwaltung\n" +
                         $"• Reservierungen erstellen/ändern\n" +
                         $"• PowerShell-Integration\n" +
                         $"• CSV-Export\n" +
                         $"• Netzwerk-Ping-Tests\n\n" +
                         $"© 2025 - Entwickelt für Windows Server-Umgebungen";

            MessageBox.Show(
                message,
                "Über DHCP WMI Viewer",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        // ------------------------
        // Shared helpers (utilized by other partials)
        // ------------------------
        private string GetServerNameOrDefault()
        {
            var server = ".";
            try
            {
                // Verwende direkt die Felder (nicht Controls.Find, da Name-Eigenschaften nicht gesetzt sind)
                if (cmbDiscoveredServers != null)
                {
                    if (cmbDiscoveredServers.SelectedItem != null)
                        server = cmbDiscoveredServers.SelectedItem.ToString() ?? ".";
                    else if (!string.IsNullOrWhiteSpace(cmbDiscoveredServers.Text))
                        server = cmbDiscoveredServers.Text.Trim();
                }
                if ((server == "." || string.IsNullOrWhiteSpace(server)) && txtServer != null && !string.IsNullOrWhiteSpace(txtServer.Text))
                {
                    server = txtServer.Text.Trim();
                }
            }
            catch { /* defensive */ }
            return server;
        }

        private string TryGetScopeIdFromSelection()
        {
            try
            {
                // dgv ist in Controls-Partial deklariert
                if (dgv == null) return string.Empty;
                if (dgv.SelectedRows.Count == 0) return string.Empty;
                var scopeRow = dgv.SelectedRows[0];
                var scopeId = scopeRow.Cells["ScopeId"]?.Value?.ToString() ?? string.Empty;
                return scopeId;
            }
            catch { return string.Empty; }
        }

        private async Task TryInvokeRefreshReservations(string scopeId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(scopeId)) return;
                var server = GetServerNameOrDefault();
                // Reads: verwende gecachte Credentials oder integrierte Auth (kein neuer Prompt)
                var rt = await DhcpManager.QueryReservationsAsync(server, scopeId, s => 
                {
                    // Für Refresh-Operationen: nur bereits bekannte Credentials verwenden, keine neuen Prompts
                    var normalizedServer = s.Trim().ToLowerInvariant();
                    if (_integratedAuthServers.Contains(normalizedServer))
                        return null; // Integrierte Auth verwenden
                    if (_credentialCache.TryGetValue(normalizedServer, out var cachedCred))
                        return cachedCred; // Gecachte Credentials verwenden
                    return null; // Fallback: integrierte Auth versuchen
                });
                reservationTable = rt ?? new DataTable();

                // bindingReservations ist in Controls-Partial deklariert
                if (bindingReservations != null) bindingReservations.DataSource = reservationTable;
                if (dgvReservations != null) dgvReservations.DataSource = bindingReservations;
                // Autosize columns to header+content for reservations grid + let Description fill
                try {
                    dgvReservations?.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
                    if (dgvReservations != null && dgvReservations.Columns != null && dgvReservations.Columns.Contains("Description"))
                    {
                        var c = dgvReservations.Columns["Description"]; 
                        if (c != null) { c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; c.MinimumWidth = Math.Max(c.Width, 160); }
                    }
                } catch { }
                
                // Update tab text with reservation count
                UpdateReservationsTabText();
            }
            catch { /* swallow */ }
        }
        
        private void UpdateReservationsTabText()
        {
            try
            {
                if (tabReservations != null)
                {
                    var count = reservationTable?.Rows.Count ?? 0;
                    tabReservations.Text = $"Reservations ({count})";
                }
            }
            catch { /* ignore */ }
        }

        private async Task TryInvokeRefreshLeases(string scopeId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(scopeId)) return;
                var server = GetServerNameOrDefault();
                
                // Quick feedback without blocking
                UpdateStatus("Leases werden aktualisiert...");
                pb.Style = ProgressBarStyle.Marquee;
                pb.Visible = true;
                
                try
                {
                    // Use the same method as reservations refresh (no credential prompts)
                    var queryTask = DhcpManager.QueryLeasesAsync(server, scopeId, s => 
                    {
                        // Für Refresh-Operationen: nur bereits bekannte Credentials verwenden, keine neuen Prompts
                        var normalizedServer = s.Trim().ToLowerInvariant();
                        if (_integratedAuthServers.Contains(normalizedServer))
                            return null; // Integrierte Auth verwenden
                        if (_credentialCache.TryGetValue(normalizedServer, out var cachedCred))
                            return cachedCred; // Gecachte Credentials verwenden
                        return null; // Fallback: integrierte Auth versuchen
                    }, 5);
                    UpdateStatus("Leases werden abgerufen...");
                    
                    // Same timeout as scope selection
                    var timeoutMs = 5000; // 5s timeout
                    var completed = await Task.WhenAny(queryTask, Task.Delay(timeoutMs)).ConfigureAwait(false);
                    
                    if (!ReferenceEquals(completed, queryTask))
                    {
                        UpdateStatus("Lease-Aktualisierung abgebrochen (Timeout).");
                        return;
                    }
                    
                    var lt = await queryTask.ConfigureAwait(false);
                    
                    if (lt != null)
                    {
                        leaseTable = lt;
                        
                        // Safely update DataGridView to prevent rowIndex errors
                        if (dgvLeases != null)
                        {
                            try
                            {
                                dgvLeases.ClearSelection();
                                dgvLeases.DataSource = null;
                                if (bindingLeases != null) bindingLeases.DataSource = leaseTable;
                                dgvLeases.DataSource = bindingLeases;
                                // adjust leases column widths to fit header and content
                                try { AdjustLeasesColumnWidths(); } catch { }
                            }
                            catch (Exception ex)
                            {
                                // Log error but don't crash
                                System.Diagnostics.Debug.WriteLine($"DataGridView update error: {ex.Message}");
                            }
                        }
                        
                        UpdateLeasesTabText();
                        
                        var leaseCount = leaseTable.Rows.Count;
                        UpdateStatus($"Leases aktualisiert: {leaseCount} Einträge.");
                    }
                    else
                    {
                        UpdateStatus("Keine Leases gefunden.");
                    }
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Fehler: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Fehler: {ex.Message}");
            }
            finally
            {
                pb.Visible = false;
            }
        }
        
        private void UpdateLeasesUI(dynamic result)
        {
            try
            {
                // Update UI based on result
                if (result.Success && result.Data != null)
                {
                    leaseTable = result.Data;
                    
                    // Safely update DataGridView to prevent rowIndex errors
                    if (dgvLeases != null)
                    {
                        try
                        {
                            dgvLeases.ClearSelection();
                            dgvLeases.DataSource = null;
                            if (bindingLeases != null) bindingLeases.DataSource = leaseTable;
                            dgvLeases.DataSource = bindingLeases;
                            try { AdjustLeasesColumnWidths(); } catch { }
                        }
                        catch (Exception ex)
                        {
                            // Log error but don't crash
                            System.Diagnostics.Debug.WriteLine($"DataGridView update error: {ex.Message}");
                        }
                    }
                    
                    UpdateLeasesTabText();
                    
                    var leaseCount = leaseTable.Rows.Count;
                    UpdateStatus($"Leases aktualisiert: {leaseCount} Einträge.");
                }
                else if (result.Message == "Timeout")
                {
                    UpdateStatus("Lease-Aktualisierung abgebrochen (zu langsam).");
                }
                else
                {
                    UpdateStatus($"Fehler: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Fehler: {ex.Message}");
            }
            finally
            {
                pb.Visible = false;
            }
        }
        
        private void UpdateLeasesTabText()
        {
            try
            {
                if (tabLeases != null)
                {
                    var count = leaseTable?.Rows.Count ?? 0;
                    tabLeases.Text = $"Leases ({count})";
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash
                System.Diagnostics.Debug.WriteLine($"UpdateLeasesTabText error: {ex.Message}");
            }
        }

        /// <summary>
        /// Liefert (asynchron) die Reservations-Tabelle für ein Scope mittels DhcpManager.
        /// Rückgabe: DataTable (leer bei Fehlern).
        /// </summary>
        private async Task<DataTable> ReservationLookupForScopeAsync(string scopeId)
        {
            try
            {
                var server = GetServerNameOrDefault();
                // Reads: verwende gecachte Credentials oder integrierte Auth (kein neuer Prompt)
                var dt = await DhcpManager.QueryReservationsAsync(server, scopeId, s => 
                {
                    // Für Lookup-Operationen: nur bereits bekannte Credentials verwenden, keine neuen Prompts
                    var normalizedServer = s.Trim().ToLowerInvariant();
                    if (_integratedAuthServers.Contains(normalizedServer))
                        return null; // Integrierte Auth verwenden
                    if (_credentialCache.TryGetValue(normalizedServer, out var cachedCred))
                        return cachedCred; // Gecachte Credentials verwenden
                    return null; // Fallback: integrierte Auth versuchen
                });
                return dt ?? new DataTable();
            }
            catch { return new DataTable(); }
        }

        /// <summary>
        /// Versucht, eine DeleteReservationAsync-Methode in DhcpManager per Reflection aufzurufen.
        /// Unterstützte Signaturen (versucht in dieser Reihenfolge):
        ///  - Task&lt;bool&gt; DeleteReservationAsync(string server, string scopeId, string ip, Func&lt;string, PSCredential?&gt; getCred)
        ///  - Task&lt;bool&gt; DeleteReservationAsync(string server, string scopeId, string ip, PSCredential? cred)
        ///  - Task&lt;bool&gt; DeleteReservationAsync(string server, string scopeId, string ip)
        /// Falls keine passende Methode gefunden wird oder Aufruf fehlschlägt, wird false zurückgegeben.
        /// </summary>
        private async Task<bool> ReservationDeleteForScopeAndIpAsync(string scopeId, string ip)
        {
            try
            {
                var server = GetServerNameOrDefault();
                var dmType = typeof(DhcpManager);
                var methods = dmType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => string.Equals(m.Name, "DeleteReservationAsync", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (methods.Length == 0) return false;

                foreach (var mi in methods)
                {
                    var pars = mi.GetParameters();
                    try
                    {
                        object? invokeResult = null;

                        if (pars.Length == 4 &&
                            pars[0].ParameterType == typeof(string) &&
                            pars[1].ParameterType == typeof(string) &&
                            pars[2].ParameterType == typeof(string) &&
                            typeof(Delegate).IsAssignableFrom(pars[3].ParameterType))
                        {
                            // signature: (string server, string scopeId, string ip, some-delegate)
                            // Build a delegate that maps to GetCredentialsForServer method, converted to the expected delegate type if possible.
                            object credDelegate;
                            try
                            {
                                var methodInfo = this.GetType().GetMethod(nameof(GetCredentialsForServer), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (methodInfo != null)
                                {
                                    try
                                    {
                                        var created = Delegate.CreateDelegate(pars[3].ParameterType, this, methodInfo);
                                        credDelegate = created ?? (object)new Func<string, PSCredential?>(s => GetCredentialsForServerWithTracking(s));
                                    }
                                    catch
                                    {
                                        credDelegate = new Func<string, PSCredential?>(s => GetCredentialsForServerWithTracking(s));
                                    }
                                }
                                else
                                {
                                    credDelegate = new Func<string, PSCredential?>(s => GetCredentialsForServerWithTracking(s));
                                }
                            }
                            catch
                            {
                                credDelegate = new Func<string, PSCredential?>(s => GetCredentialsForServerWithTracking(s));
                            }

                            // use object[] (not object?[]) to avoid nullable-array mismatch warnings
                            invokeResult = mi.Invoke(null, new object[] { server, scopeId, ip, credDelegate });
                        }
                        else if (pars.Length == 4 &&
                                 pars[0].ParameterType == typeof(string) &&
                                 pars[1].ParameterType == typeof(string) &&
                                 pars[2].ParameterType == typeof(string) &&
                                 (pars[3].ParameterType == typeof(PSCredential) || pars[3].ParameterType == typeof(object)))
                        {
                            var cred = GetCredentialsForServerWithTracking(server);
                            invokeResult = mi.Invoke(null, new object[] { server, scopeId, ip, cred });
                        }
                        else if (pars.Length == 3 &&
                                 pars[0].ParameterType == typeof(string) &&
                                 pars[1].ParameterType == typeof(string) &&
                                 pars[2].ParameterType == typeof(string))
                        {
                            invokeResult = mi.Invoke(null, new object[] { server, scopeId, ip });
                        }
                        else
                        {
                            // unsupported signature for this overload — skip
                            continue;
                        }

                        if (invokeResult == null) continue;

                        // handle Task / Task<bool>
                        if (invokeResult is Task<bool> tBool)
                        {
                            return await tBool.ConfigureAwait(false);
                        }
                        else if (invokeResult is Task t)
                        {
                            await t.ConfigureAwait(false);
                            // try to get Result property via reflection (if Task<TResult>)
                            var resProp = invokeResult.GetType().GetProperty("Result");
                            if (resProp != null)
                            {
                                var val = resProp.GetValue(invokeResult);
                                if (val is bool b) return b;
                            }
                            // assume success if no Result -> return true
                            return true;
                        }
                    }
                    catch
                    {
                        // try next overload
                        continue;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Komfort-Methode: Öffnet ConvertLeaseToReservationDialog für gegebene Parameter und übergibt
        /// die Lookup/Delete-Callbacks automatisch.
        /// Hinweis: Diese Implementierung verwendet den parameterlosen Konstruktor und
        /// versucht per Reflection, Properties/Methoden zu befüllen. Dadurch vermeiden wir
        /// Kompilationsfehler, wenn es alternative Konstruktor-Signaturen gibt.
        /// </summary>
        public Task OpenConvertLeaseToReservationDialogAsync(string scopeId, string ipAddress, string clientId, string hostName, string startRange, string endRange, string subnetMask)
        {
            try
            {
                // Verwende parameterlosen Konstruktor, um Overload-Probleme (Methodengruppen -> string) zu vermeiden.
                using var dlg = new ConvertLeaseToReservationDialog();

                // Versuche, bekannte Properties / Fields zu setzen (best-effort)
                TrySetPropertyIfExists(dlg, "ScopeId", scopeId);
                TrySetPropertyIfExists(dlg, "Scope", scopeId);
                TrySetPropertyIfExists(dlg, "IpAddress", ipAddress);
                TrySetPropertyIfExists(dlg, "IPAddress", ipAddress);
                TrySetPropertyIfExists(dlg, "ClientId", clientId);
                TrySetPropertyIfExists(dlg, "Client", clientId);
                TrySetPropertyIfExists(dlg, "HostName", hostName);
                TrySetPropertyIfExists(dlg, "Name", hostName);
                TrySetPropertyIfExists(dlg, "StartRange", startRange);
                TrySetPropertyIfExists(dlg, "EndRange", endRange);
                TrySetPropertyIfExists(dlg, "SubnetMask", subnetMask);

                // Versuche, Delegates (Lookup / Delete) zuzuweisen, falls passende Properties existieren.
                TryAssignDelegatePropertyIfExists(dlg, "ReservationLookup", (Func<string, Task<DataTable>>)ReservationLookupForScopeAsync);
                TryAssignDelegatePropertyIfExists(dlg, "ReservationLookupForScopeAsync", (Func<string, Task<DataTable>>)ReservationLookupForScopeAsync);
                TryAssignDelegatePropertyIfExists(dlg, "Lookup", (Func<string, Task<DataTable>>)ReservationLookupForScopeAsync);

                TryAssignDelegatePropertyIfExists(dlg, "ReservationDelete", (Func<string, string, Task<bool>>)((s, ip) => ReservationDeleteForScopeAndIpAsync(s, ip)));
                TryAssignDelegatePropertyIfExists(dlg, "ReservationDeleteForScopeAndIpAsync", (Func<string, string, Task<bool>>)((s, ip) => ReservationDeleteForScopeAndIpAsync(s, ip)));
                TryAssignDelegatePropertyIfExists(dlg, "DeleteReservation", (Func<string, string, Task<bool>>)((s, ip) => ReservationDeleteForScopeAndIpAsync(s, ip)));

                // Wenn Dialog eine Initializer-Methode hat, versuchen wir diese (z.B. InitializeWithValues)
                TryInvokeInitializeMethodIfExists(dlg, scopeId, ipAddress, clientId, hostName, startRange, endRange, subnetMask);

                // Anzeige (synchron). Methode war zuvor async ohne await — um Hinweis/Cs1998 zu beseitigen, behalten wir Sync-ShowDialog und liefern Task.CompletedTask zurück.
                dlg.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Fehler beim Öffnen des Dialogs: {ex.Message}", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return Task.CompletedTask;
        }

        private void TryInvokeInitializeMethodIfExists(object dlg, params object[] values)
        {
            if (dlg == null) return;
            try
            {
                var t = dlg.GetType();
                var candidates = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                  .Where(m => string.Equals(m.Name, "InitializeWithValues", StringComparison.OrdinalIgnoreCase)
                                           || string.Equals(m.Name, "Initialize", StringComparison.OrdinalIgnoreCase)
                                           || string.Equals(m.Name, "Init", StringComparison.OrdinalIgnoreCase))
                                  .ToArray();
                foreach (var mi in candidates)
                {
                    if (mi == null) continue;
                    var ps = mi.GetParameters();
                    if (ps.Length > 0 && ps.Length <= values.Length)
                    {
                        // try to build args of correct length
                        var args = new object[ps.Length];
                        for (int i = 0; i < ps.Length; i++)
                        {
                            var expected = ps[i].ParameterType;
                            var provided = values.Length > i ? values[i] : null;
                            // try simple assign or convert
                            if (provided == null)
                            {
                                args[i] = null!;
                            }
                            else if (expected.IsAssignableFrom(provided.GetType()))
                            {
                                args[i] = provided;
                            }
                            else
                            {
                                // last resort: try to convert ToString
                                args[i] = provided.ToString() ?? string.Empty;
                            }
                        }

                        try
                        {
                            mi.Invoke(dlg, args);
                            return;
                        }
                        catch
                        {
                            // ignore and try next overload
                        }
                    }
                }
            }
            catch { /* swallow */ }
        }

        private void TrySetPropertyIfExists(object target, string propName, object? value)
        {
            if (target == null || string.IsNullOrWhiteSpace(propName)) return;
            try
            {
                var pi = target.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                if (pi != null && pi.CanWrite)
                {
                    if (value == null)
                    {
                        // set null for reference types
                        if (!pi.PropertyType.IsValueType || Nullable.GetUnderlyingType(pi.PropertyType) != null)
                        {
                            pi.SetValue(target, null);
                        }
                        return;
                    }

                    var targetType = pi.PropertyType;
                    if (value != null && targetType.IsAssignableFrom(value.GetType()))
                    {
                        pi.SetValue(target, value);
                        return;
                    }

                    // If the property is string, set string representation
                    if (targetType == typeof(string))
                    {
                        pi.SetValue(target, value.ToString());
                        return;
                    }

                    // If property is a control, try to set its Text
                    var val = pi.GetValue(target);
                    if (val != null)
                    {
                        var textProp = val.GetType().GetProperty("Text", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (textProp != null && textProp.CanWrite)
                        {
                            textProp.SetValue(val, value?.ToString());
                            return;
                        }
                    }
                }
                else
                {
                    // try fields
                    var fi = target.GetType().GetField(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                    if (fi != null)
                    {
                        if (value != null && fi.FieldType.IsAssignableFrom(value.GetType()))
                        {
                            fi.SetValue(target, value);
                        }
                        else if (fi.FieldType == typeof(string))
                        {
                            fi.SetValue(target, value?.ToString());
                        }
                        else
                        {
                            var fieldVal = fi.GetValue(target);
                            if (fieldVal != null)
                            {
                                var textProp = fieldVal.GetType().GetProperty("Text", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (textProp != null && textProp.CanWrite)
                                {
                                    textProp.SetValue(fieldVal, value?.ToString());
                                }
                            }
                        }
                    }
                }
            }
            catch { /* swallow */ }
        }

        private void TryAssignDelegatePropertyIfExists(object target, string propName, Delegate del)
        {
            if (target == null || string.IsNullOrWhiteSpace(propName) || del == null) return;
            try
            {
                var pi = target.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                if (pi != null && pi.CanWrite)
                {
                    var pType = pi.PropertyType;
                    if (pType != null && pType.IsAssignableFrom(del.GetType()))
                    {
                        pi.SetValue(target, del);
                        return;
                    }

                    // try to create a delegate of required type
                    if (pType != null && typeof(Delegate).IsAssignableFrom(pType))
                    {
                        try
                        {
                            var created = Delegate.CreateDelegate(pType, del.Target, del.Method);
                            if (created != null) pi.SetValue(target, created);
                            return;
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                }

                // try fields too
                var fi = target.GetType().GetField(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                if (fi != null)
                {
                    var fType = fi.FieldType;
                    if (fType != null && fType.IsAssignableFrom(del.GetType()))
                    {
                        fi.SetValue(target, del);
                        return;
                    }

                    if (fType != null && typeof(Delegate).IsAssignableFrom(fType))
                    {
                        try
                        {
                            var created = Delegate.CreateDelegate(fType, del.Target, del.Method);
                            if (created != null) fi.SetValue(target, created);
                            return;
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                }
            }
            catch { /* swallow */ }
        }

        private async Task ReplicateToPartnersAsync(string originServer, Func<string, Task> actionPerPartner)
        {
            try
            {
                if (actionPerPartner == null) return;

                var partnersText = string.Empty;
                try
                {
                    var tb = this.Controls.Find("txtPartnerServers", true).FirstOrDefault() as TextBox;
                    if (tb != null) partnersText = tb.Text ?? string.Empty;
                }
                catch { /* ignore */ }

                if (string.IsNullOrWhiteSpace(partnersText))
                {
                    try
                    {
                        var fi = this.GetType().GetField("txtPartnerServers", BindingFlags.Instance | BindingFlags.NonPublic);
                        if (fi != null)
                        {
                            var val = fi.GetValue(this) as TextBox;
                            if (val != null) partnersText = val.Text ?? string.Empty;
                        }
                    }
                    catch { }
                }

                if (string.IsNullOrWhiteSpace(partnersText)) return;

                var partners = partnersText
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p) && !p.Equals(originServer, StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                foreach (var partner in partners)
                {
                    try
                    {
                        UpdateStatus($"Replicating to {partner} ...");
                        await actionPerPartner(partner);
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus($"Replication to {partner} failed: {ex.Message}");
                    }
                }

                UpdateStatus("Replication finished.");
            }
            catch
            {
                // swallow
            }
        }

        // ------------------------
        // Partial method stub - Implementierung optional in Reservations partial
        // ------------------------
        partial void SetupReservationsContextMenu();
    }
}
