// MainForm.Leases.ContextMenu.cs
using System;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    public partial class MainForm : Form
    {
        private void DgvLeases_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
            {
                try
                {
                    if (dgvLeases == null) return;
                    dgvLeases.ClearSelection();
                    dgvLeases.Rows[e.RowIndex].Selected = true;
                    if (dgvLeases.Rows[e.RowIndex].Cells.Count > 0)
                        dgvLeases.CurrentCell = dgvLeases.Rows[e.RowIndex].Cells[0];
                }
                catch { /* ignore UI errors */ }
            }
        }

        private void ContextMenuLeases_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (dgvLeases == null)
                {
                    e.Cancel = true;
                    return;
                }

                var clientPos = dgvLeases.PointToClient(Cursor.Position);
                var hit = dgvLeases.HitTest(clientPos.X, clientPos.Y);

                if (hit.RowIndex < 0 || dgvLeases.SelectedRows.Count == 0)
                {
                    e.Cancel = true;
                    return;
                }

                var row = dgvLeases.SelectedRows[0];
                string stateRaw = TryGetCellValue(row, "Col_AddressState", "AddressState") ?? string.Empty;
                var state = stateRaw.Trim().ToLowerInvariant();

                bool containsReservation = state.Contains("reservation");
                bool containsActive = state.Contains("active");
                bool containsLease = state.Contains("lease");

                if (contextMenuLeases == null) contextMenuLeases = new ContextMenuStrip();
                contextMenuLeases.Items.Clear();

                if (containsReservation)
                {
                    var miChange = new ToolStripMenuItem("Change reservation IP...");
                    // Direktaufruf der impliziten Methode (keine Reflection)
                    miChange.Click += async (s, args) =>
                    {
                        try
                        {
                            await OnChangeReservationFromLeaseRowAsync().ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            try { MessageBox.Show(this, "Fehler beim Aufruf von OnChangeReservationFromLeaseRowAsync: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
                        }
                    };
                    contextMenuLeases.Items.Add(miChange);
                }
                else if (containsActive || containsLease)
                {
                    var miConvert = new ToolStripMenuItem("Create reservation from lease...");
                    // Direktaufruf der impliziten Methode (keine Reflection)
                    miConvert.Click += async (s, args) =>
                    {
                        try
                        {
                            await OnCreateReservationFromLeaseAsync().ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            try { MessageBox.Show(this, "Fehler beim Aufruf von OnCreateReservationFromLeaseAsync: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
                        }
                    };
                    contextMenuLeases.Items.Add(miConvert);
                }
                else
                {
                    var miChange = new ToolStripMenuItem("Change reservation IP...");
                    miChange.Click += async (s, args) =>
                    {
                        try
                        {
                            await OnChangeReservationFromLeaseRowAsync().ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            try { MessageBox.Show(this, "Fehler beim Aufruf von OnChangeReservationFromLeaseRowAsync: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
                        }
                    };
                    contextMenuLeases.Items.Add(miChange);
                }

                var hasSelection = dgvLeases.SelectedRows.Count > 0;
                foreach (ToolStripItem item in contextMenuLeases.Items) item.Enabled = hasSelection;

                if (!e.Cancel && contextMenuLeases.Items.Count > 0)
                {
                    contextMenuLeases.Show(Cursor.Position);
                }
            }
            catch
            {
                try { e.Cancel = true; } catch { }
            }
        }
    }
}
