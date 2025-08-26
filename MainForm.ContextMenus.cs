// MainForm.ContextMenus.cs
using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// Ersetzt die vorher duplicate SetupReservationsContextMenu-Definition:
        /// Dieser Helper stellt einen Reflection-invoker zur Verfügung, mit dem
        /// partial-Implementierungen in anderen Dateien aufgerufen werden können.
        /// </summary>
        private async Task InvokeHandlerIfExistsAsync(string methodName)
        {
            try
            {
                var mi = this.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (mi != null)
                {
                    var t = mi.Invoke(this, null) as Task;
                    if (t != null) await t;
                    return;
                }

                // fallback: show message
                MessageBox.Show(this, $"Handler '{methodName}' ist nicht implementiert.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Fehler beim Aufruf von '{methodName}': {ex.Message}", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Optionaler Fallback: Erzeuge ein sehr kleines ContextMenu und weise es dem dgvReservations
        /// zu **falls** noch kein Kontextmenü gesetzt ist. Dieses Tool ist benannt, damit es nicht
        /// mit der Haupt-SetupReservationsContextMenu() kollidiert.
        /// </summary>
        private void EnsureFallbackReservationsContextMenu()
        {
            try
            {
                if (dgvReservations == null) return;
                if (dgvReservations.ContextMenuStrip != null) return;

                var cms = new ContextMenuStrip();
                var miChange = new ToolStripMenuItem("Change reservation IP...");
                miChange.Click += async (s, e) => await InvokeHandlerIfExistsAsync("OnChangeReservationIpFromReservationsAsync");

                var miDelete = new ToolStripMenuItem("Löschen");
                miDelete.Click += async (s, e) => await InvokeHandlerIfExistsAsync("OnDeleteReservationFromReservationsAsync");

                cms.Items.Add(miChange);
                cms.Items.Add(new ToolStripSeparator());
                cms.Items.Add(miDelete);

                cms.Opening += (s, e) =>
                {
                    var hasSelection = dgvReservations != null && dgvReservations.SelectedRows.Count == 1;
                    foreach (ToolStripItem item in cms.Items)
                    {
                        if (item is ToolStripSeparator) continue;
                        item.Enabled = hasSelection;
                    }
                    if (!hasSelection) e.Cancel = true;
                };

                dgvReservations.ContextMenuStrip = cms;
            }
            catch
            {
                // swallow - best effort
            }
        }

        /// <summary>
        /// Optionaler Wrapper, der per Reflection die tatsächliche Delete-Implementierung (falls vorhanden)
        /// aufruft. Unbedingt anders benannt, damit es keine Namenskollision mit der konkreten Implementierung gibt.
        /// </summary>
        private async Task DeleteSelectedReservationViaReflectionAsync()
        {
            await InvokeHandlerIfExistsAsync("OnDeleteReservationFromReservationsAsync");
        }
    }
}
