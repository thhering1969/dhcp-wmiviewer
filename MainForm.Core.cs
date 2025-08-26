// MainForm.Core.cs
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
            Width = 980;
            Height = 680;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10F);

            // Layout initialisieren (legt Controls an, verwendet Felder aus MainForm.Controls.cs)
            InitializeLayout();

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
                leaseTable.Columns.Add("HostName", typeof(string));
                leaseTable.Columns.Add("Description", typeof(string)); // Description rechts neben HostName
                leaseTable.Columns.Add("AddressState", typeof(string));
                leaseTable.Columns.Add("LeaseExpiryTime", typeof(string));
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
        public PSCredential? GetCredentialsForServer(string server)
        {
            // Return null to use integrated auth by default.
            return null;
        }

        private PSCredential? AskForCredentials(string server)
        {
            PSCredential? result = null;
            try
            {
                if (this.InvokeRequired) this.Invoke(new Action(() => result = AskForCredentialsInternal(server)));
                else result = AskForCredentialsInternal(server);
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

        // ------------------------
        // Shared helpers (utilized by other partials)
        // ------------------------
        private string GetServerNameOrDefault()
        {
            var server = ".";
            try
            {
                if (this.Controls.Find("txtServer", true).FirstOrDefault() is TextBox tb && !string.IsNullOrWhiteSpace(tb.Text))
                    server = tb.Text.Trim();
                else if (this.Controls.Find("cmbDiscoveredServers", true).FirstOrDefault() is ComboBox cb && cb.SelectedItem != null)
                    server = cb.SelectedItem.ToString() ?? ".";
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
                var rt = await DhcpManager.QueryReservationsAsync(server, scopeId, GetCredentialsForServer);
                reservationTable = rt ?? new DataTable();

                // bindingReservations ist in Controls-Partial deklariert
                if (bindingReservations != null) bindingReservations.DataSource = reservationTable;
                if (dgvReservations != null) dgvReservations.DataSource = bindingReservations;
            }
            catch { /* swallow */ }
        }

        private async Task TryInvokeRefreshLeases(string scopeId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(scopeId)) return;
                var server = GetServerNameOrDefault();
                var lt = await DhcpManager.QueryLeasesAsync(server, scopeId, GetCredentialsForServer);
                if (lt == null) return;

                leaseTable = lt;

                // Heuristische Korrektur: implementiert in MainForm.DataHelpers.cs
                try { FixServerIpValues(leaseTable); } catch { /* ignore */ }

                // Bind
                if (bindingLeases != null) bindingLeases.DataSource = leaseTable;
                if (dgvLeases != null)
                {
                    dgvLeases.DataSource = bindingLeases;
                    // Format ServerIP cells after bind (UI helper in Layout partial)
                    try { FormatServerIpCellsAfterBind(); } catch { /* ignore */ }
                }
            }
            catch { /* swallow */ }
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
                var dt = await DhcpManager.QueryReservationsAsync(server, scopeId, GetCredentialsForServer);
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
                            // signature: (string server, string scopeId, string ip, Func<string,PSCredential?> getCred)
                            var credFunc = new Func<string, PSCredential?>(GetCredentialsForServer);
                            invokeResult = mi.Invoke(null, new object?[] { server, scopeId, ip, credFunc });
                        }
                        else if (pars.Length == 4 &&
                                 pars[0].ParameterType == typeof(string) &&
                                 pars[1].ParameterType == typeof(string) &&
                                 pars[2].ParameterType == typeof(string) &&
                                 pars[3].ParameterType == typeof(PSCredential))
                        {
                            var cred = GetCredentialsForServer(server);
                            invokeResult = mi.Invoke(null, new object?[] { server, scopeId, ip, cred });
                        }
                        else if (pars.Length == 3 &&
                                 pars[0].ParameterType == typeof(string) &&
                                 pars[1].ParameterType == typeof(string) &&
                                 pars[2].ParameterType == typeof(string))
                        {
                            invokeResult = mi.Invoke(null, new object?[] { server, scopeId, ip });
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
        /// </summary>
        public async Task OpenConvertLeaseToReservationDialogAsync(string scopeId, string ipAddress, string clientId, string hostName, string startRange, string endRange, string subnetMask)
        {
            try
            {
                using var dlg = new ConvertLeaseToReservationDialog(
                    scopeId,
                    ipAddress,
                    clientId,
                    hostName,
                    startRange,
                    endRange,
                    subnetMask,
                    ReservationLookupForScopeAsync,
                    ReservationDeleteForScopeAndIpAsync);

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    // Der Dialog hat OK gedrückt — hier kannst du die tatsächliche Create/Change-Logik anstoßen.
                    UpdateStatus("Reservation verändert / erstellt (Dialog bestätigt).");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Fehler beim Öffnen des Dialogs: {ex.Message}", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
                            partnersText = val?.Text ?? string.Empty;
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
