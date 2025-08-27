// MainForm.ReservationsHandlers.cs
// Option 1 — Signaturen exakt passend zu .NET-Delegates (object sender)
// Einfach kopieren & einfügen

using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;

namespace DhcpWmiViewer
{
    public partial class MainForm : Form
    {
        // Kontextmenü für Reservations-Grid
        private ContextMenuStrip? _reservationsContextMenu;
        private ToolStripMenuItem? _tsmiDeleteReservation;
        private ToolStripMenuItem? _tsmiChangeReservationIp;

        /// <summary>
        /// Muss nach InitializeComponent() aufgerufen werden (z.B. im Konstruktor oder Load),
        /// damit das Context-Menu sichtbar wird und richtig funktioniert.
        /// </summary>
        partial void SetupReservationsContextMenu()
        {
            if (dgvReservations == null) return;

            try { dgvReservations.ContextMenuStrip = null; } catch { }

            if (_reservationsContextMenu != null) return;

            _reservationsContextMenu = new ContextMenuStrip();

            _tsmiChangeReservationIp = new ToolStripMenuItem("Change reservation IP...");
            _tsmiChangeReservationIp.Click += async (s, e) =>
            {
                await OnChangeReservationIpFromReservationsAsync();
            };
            _reservationsContextMenu.Items.Add(_tsmiChangeReservationIp);

            _reservationsContextMenu.Items.Add(new ToolStripSeparator());

            _tsmiDeleteReservation = new ToolStripMenuItem("Löschen");
            _tsmiDeleteReservation.Click += async (s, e) => await DeleteSelectedReservationAsync();
            _reservationsContextMenu.Items.Add(_tsmiDeleteReservation);

            _reservationsContextMenu.Opening += (s, e) =>
            {
                try
                {
                    var enabledSingle = dgvReservations != null && dgvReservations.SelectedRows.Count == 1;

                    if (_tsmiChangeReservationIp != null) _tsmiChangeReservationIp.Enabled = enabledSingle;
                    if (_tsmiDeleteReservation != null) _tsmiDeleteReservation.Enabled = enabledSingle;
                }
                catch
                {
                    e.Cancel = true;
                }
            };

            dgvReservations.CellContextMenuStripNeeded -= DgvReservations_CellContextMenuStripNeeded;
            dgvReservations.CellContextMenuStripNeeded += DgvReservations_CellContextMenuStripNeeded;

            dgvReservations.CellMouseDown -= DgvReservations_CellMouseDown;
            dgvReservations.CellMouseDown += DgvReservations_CellMouseDown;

            dgvReservations.ContextMenuStrip = _reservationsContextMenu;
        }

        private void DgvReservations_CellContextMenuStripNeeded(object sender, DataGridViewCellContextMenuStripNeededEventArgs e)
        {
            try
            {
                if (e.RowIndex >= 0)
                    e.ContextMenuStrip = _reservationsContextMenu;
                else
                    e.ContextMenuStrip = null;
            }
            catch
            {
                e.ContextMenuStrip = null;
            }
        }

        // <- HIER: Signatur angepasst (object sender)
        private void DgvReservations_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            try
            {
                if (sender is not DataGridView dgv) return;
                if (e.Button != MouseButtons.Right) return;
                if (e.RowIndex < 0) return;

                dgv.ClearSelection();
                dgv.Rows[e.RowIndex].Selected = true;
                if (dgv.Rows[e.RowIndex].Cells.Count > 0)
                    dgv.CurrentCell = dgv.Rows[e.RowIndex].Cells[0];

                if (_reservationsContextMenu != null)
                {
                    _reservationsContextMenu.Show(Cursor.Position);
                }
            }
            catch { /* ignore */ }
        }

        private async Task DeleteSelectedReservationAsync()
        {
            try
            {
                if (dgvReservations == null || dgvReservations.SelectedRows.Count != 1)
                {
                    MessageBox.Show(this, "Bitte zuerst genau eine Reservation auswählen.", "Keine Auswahl", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var row = dgvReservations.SelectedRows[0];
                var ip = row.Cells["IPAddress"]?.Value?.ToString() ?? string.Empty;
                var clientId = row.Cells["ClientId"]?.Value?.ToString() ?? string.Empty;
                var name = row.Cells["Name"]?.Value?.ToString() ?? string.Empty;

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

                var confirm = MessageBox.Show(this,
                    $"Reservation löschen:\n\nScope: {scopeId}\nName: {name}\nIP: {ip}\n\nFortfahren?",
                    "Reservation löschen",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirm != DialogResult.Yes) return;

                var server = GetServerNameOrDefault();

                try
                {
                    this.Enabled = false;

                    await DhcpManager.DeleteReservationAsync(
                        server,
                        scopeId,
                        ip,
                        clientId,
                        s => GetCredentialsForServer(s)!);

                    MessageBox.Show(this, "Reservation erfolgreich gelöscht.", "Erfolg", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // zentralisierte Methode entscheidet lokal vs. remote anhand AppConstants und UI-Server
                    try
                    {
                        await LogGuiEventAsync("DeleteReservation", scopeId, ip, $"Name={name};ClientId={clientId}");
                    }
                    catch { /* swallow logging errors to not disturb UI flow */ }

                    await TryInvokeRefreshReservations(scopeId);
                    await TryInvokeRefreshLeases(scopeId);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Fehler beim Löschen der Reservation:\n" + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    this.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Unerwarteter Fehler beim Löschen: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // <- HIER: Signatur angepasst (object sender)
        private void DgvReservations_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e == null || e.RowIndex < 0) return;
                if (dgvReservations == null) return;

                dgvReservations.ClearSelection();
                dgvReservations.Rows[e.RowIndex].Selected = true;
                if (dgvReservations.Rows[e.RowIndex].Cells.Count > 0)
                    dgvReservations.CurrentCell = dgvReservations.Rows[e.RowIndex].Cells[0];

                _ = OnChangeReservationIpFromReservationsAsync();
            }
            catch { /* ignore UI handler errors */ }
        }

        private async Task OnChangeReservationIpFromReservationsAsync()
        {
            try
            {
                var mi = this.GetType().GetMethod("OnChangeReservationFromReservationsAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (mi != null)
                {
                    var t = mi.Invoke(this, null) as Task;
                    if (t != null) await t;
                    return;
                }

                MessageBox.Show(this, "Änderungs-Handler ist nicht implementiert.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Fehler beim Aufruf des Change-Handlers: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
