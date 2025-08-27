// MainForm.ContextMenus.cs
// Repo: DhcpWmiViewer
// Branch: fix/contextmenu-direct-call
// **KOMPLETTE DATEI** — zentrale, saubere ContextMenu-Handler für Leases.

using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;

namespace DhcpWmiViewer
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// Öffnet den Kontext-Menu-Dialog bzw. startet die Reservation-Erstellung.
        /// Dieser Handler wird an alle relevanten MenuItems gebunden.
        /// </summary>
        private async void OnContextMenuCreateReservation_Click(object? sender, EventArgs e)
        {
            try
            {
                await OnCreateReservationFromLeaseAsync();
            }
            catch (Exception ex)
            {
                try { MessageBox.Show(this, "Fehler beim Starten: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
            }
        }

        /// <summary>
        /// ContextMenu Opening handler for leases grid.
        /// Ensures necessary menu items exist and enables/disables them depending on selection.
        /// </summary>
        private async void ContextMenuLeases_Opening(object? sender, CancelEventArgs e)
        {
            try
            {
                var cms = sender as ContextMenuStrip ?? this.contextMenuLeases;
                if (cms == null)
                {
                    e.Cancel = true;
                    return;
                }

                // Ensure canonical items exist (idempotent)
                EnsureLeasesMenuItems(cms);

                // Determine selection state
                bool singleRowSelected = dgvLeases != null && dgvLeases.SelectedRows.Count == 1;

                // Enable/disable items based on selection
                foreach (ToolStripItem it in cms.Items)
                {
                    try
                    {
                        if (it is ToolStripMenuItem tmi)
                        {
                            var text = (tmi.Text ?? string.Empty).Trim();
                            if (string.Equals(text, "Create reservation from lease...", StringComparison.OrdinalIgnoreCase))
                                tmi.Enabled = singleRowSelected;
                            else if (string.Equals(text, "Refresh leases", StringComparison.OrdinalIgnoreCase))
                                tmi.Enabled = true;
                            else
                                tmi.Enabled = singleRowSelected;
                        }
                    }
                    catch { /* ignore per-item errors */ }
                }

                await Task.CompletedTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ContextMenuLeases_Opening error: " + ex);
                try { e.Cancel = true; } catch { }
            }
        }

        /// <summary>
        /// Ensures canonical menu items for leases context menu are present and wired.
        /// Safe to call multiple times.
        /// </summary>
        private void EnsureLeasesMenuItems(ContextMenuStrip cms)
        {
            if (cms == null) return;

            // Create reservation item
            var createItem = cms.Items.OfType<ToolStripMenuItem>()
                                      .FirstOrDefault(i => string.Equals(i.Text, "Create reservation from lease...", StringComparison.OrdinalIgnoreCase));
            if (createItem == null)
            {
                createItem = new ToolStripMenuItem("Create reservation from lease...");
                // bind central handler (safe)
                createItem.Click -= OnContextMenuCreateReservation_Click;
                createItem.Click += OnContextMenuCreateReservation_Click;
                cms.Items.Insert(0, createItem);
            }
            else
            {
                // ensure single binding via rebind
                try
                {
                    createItem.Click -= OnContextMenuCreateReservation_Click;
                    createItem.Click += OnContextMenuCreateReservation_Click;
                }
                catch { /* ignore */ }
            }

            // Refresh leases item
            var refreshItem = cms.Items.OfType<ToolStripMenuItem>()
                                       .FirstOrDefault(i => string.Equals(i.Text, "Refresh leases", StringComparison.OrdinalIgnoreCase));
            if (refreshItem == null)
            {
                refreshItem = new ToolStripMenuItem("Refresh leases");
                refreshItem.Click += async (s, e) =>
                {
                    try
                    {
                        var scope = TryGetScopeIdFromSelection() ?? string.Empty;
                        await TryInvokeRefreshLeases(scope);
                    }
                    catch (Exception ex)
                    {
                        try { MessageBox.Show(this, "Fehler beim Aktualisieren: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
                    }
                };
                cms.Items.Add(new ToolStripSeparator());
                cms.Items.Add(refreshItem);
            }
            else
            {
                try
                {
                    // rebind safely
                    refreshItem.Click -= async (s, e) => { await TryInvokeRefreshLeases(TryGetScopeIdFromSelection() ?? string.Empty); };
                    refreshItem.Click += async (s, e) =>
                    {
                        try { var scope = TryGetScopeIdFromSelection() ?? string.Empty; await TryInvokeRefreshLeases(scope); } catch (Exception ex) { try { MessageBox.Show(this, "Fehler beim Aktualisieren: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { } }
                    };
                }
                catch { /* ignore */ }
            }
        }

        /// <summary>
        /// DataGridView CellMouseDown handler for leases grid.
        /// Selects the clicked row and shows the context menu.
        /// </summary>
        private void DgvLeases_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
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

                // Show context menu (if present)
                try
                {
                    var cms = this.contextMenuLeases;
                    if (cms != null)
                    {
                        // Ensure menu items exist and are wired (idempotent)
                        EnsureLeasesMenuItems(cms);
                        cms.Show(Cursor.Position);
                    }
                }
                catch { /* ignore show errors */ }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("DgvLeases_CellMouseDown error: " + ex);
            }
        }
    }
}
