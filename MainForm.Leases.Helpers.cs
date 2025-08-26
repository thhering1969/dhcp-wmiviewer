// MainForm.Leases.Helpers.cs
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// Sucht in der gegebenen Row nach einer Zelle, deren Spalte einen der angegebenen Namen hat.
        /// Als Namen werden versucht: Column.Name, Column.DataPropertyName, Column.HeaderText (in dieser Reihenfolge).
        /// Liefert den Wert als string zurück oder null, falls nicht gefunden / leer.
        /// Diese Implementierung vermeidet problematische string-Indexer auf DataGridViewRow.Cells.
        /// </summary>
        protected string? TryGetCellValue(DataGridViewRow? row, params string[] names)
        {
            try
            {
                if (row == null || names == null || names.Length == 0) return null;

                // Versuche zuerst die DataGridView-Referenz (falls vorhanden) für schnellen Lookup
                var grid = row.DataGridView;

                foreach (var name in names)
                {
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    int colIdx = -1;

                    // 1) Wenn das globale dgvLeases bekannt ist, versuche direkt über dessen Columns (Name)
                    if (dgvLeases != null)
                    {
                        var colByName = dgvLeases.Columns.Cast<DataGridViewColumn>()
                                            .FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
                        if (colByName != null) colIdx = colByName.Index;
                    }

                    // 2) Falls noch nicht gefunden, nutze die Columns-Sammlung der Row.DataGridView (falls vorhanden)
                    if (colIdx < 0 && grid != null)
                    {
                        var col = grid.Columns.Cast<DataGridViewColumn>()
                                    .FirstOrDefault(c =>
                                        string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(c.DataPropertyName ?? string.Empty, name, StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(c.HeaderText ?? string.Empty, name, StringComparison.OrdinalIgnoreCase));
                        if (col != null) colIdx = col.Index;
                    }

                    // 3) Falls wir eine sinnvolle Index gefunden haben, hole die Zelle und ihren Wert
                    if (colIdx >= 0 && colIdx < row.Cells.Count)
                    {
                        var cell = row.Cells[colIdx];
                        if (cell?.Value != null)
                        {
                            var s = cell.Value.ToString();
                            if (!string.IsNullOrWhiteSpace(s)) return s;
                        }
                    }

                    // 4) Fallback: suche alle Zellen in der Row und vergleiche OwningColumn-Infos (defensiv)
                    for (int i = 0; i < row.Cells.Count; i++)
                    {
                        try
                        {
                            var c = row.Cells[i];
                            var owning = c?.OwningColumn;
                            if (owning == null) continue;
                            if (string.Equals(owning.Name, name, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(owning.DataPropertyName ?? string.Empty, name, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(owning.HeaderText ?? string.Empty, name, StringComparison.OrdinalIgnoreCase))
                            {
                                if (c.Value != null)
                                {
                                    var s = c.Value.ToString();
                                    if (!string.IsNullOrWhiteSpace(s)) return s;
                                }
                            }
                        }
                        catch { /* ignore problematic cells */ }
                    }
                }
            }
            catch { /* swallow - this helper must be resilient */ }

            return null;
        }

        /// <summary>
        /// Liest die drei typischen Werte (IP, ClientId, HostName) robust aus einer Lease-Row.
        /// Liefert leere Strings bei Fehlern.
        /// </summary>
        protected (string Ip, string ClientId, string HostName) ReadLeaseRowValuesSafe(DataGridViewRow row)
        {
            try
            {
                var ip = TryGetCellValue(row, "IPAddress", "Col_IPAddress", "IP", "IPAdress") ?? string.Empty;
                var clientId = TryGetCellValue(row, "ClientId", "Col_ClientId", "Client") ?? string.Empty;
                var hostName = TryGetCellValue(row, "HostName", "Col_HostName", "Name", "Host") ?? string.Empty;

                return (ip.Trim(), clientId.Trim(), hostName.Trim());
            }
            catch
            {
                return (string.Empty, string.Empty, string.Empty);
            }
        }

        /// <summary>
        /// Versucht, eine benannte private/protected async/sync Methode auf diesem Form-Objekt per Reflection aufzurufen,
        /// falls vorhanden. Die Methode darf entweder Task zurückgeben oder void. Parameterlos.
        /// Fehler werden gefangen und geschluckt (GUI-robustheit).
        /// WICHTIG: await des Task ohne ConfigureAwait(false) damit UI-Fortsetzungen wieder auf UI-Thread laufen.
        /// </summary>
        protected async Task InvokeOptionalHandlerAsync(string methodName)
        {
            if (string.IsNullOrWhiteSpace(methodName)) return;

            try
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                var mi = this.GetType().GetMethod(methodName, flags, null, Type.EmptyTypes, null);
                if (mi == null)
                {
                    try { MessageBox.Show(this, $"Handler '{methodName}' nicht gefunden (Reflection).", "DEBUG: Handler fehlt", MessageBoxButtons.OK, MessageBoxIcon.Warning); } catch { }
                    return;
                }

                object? invokeResult = null;
                try
                {
                    invokeResult = mi.Invoke(this, null);
                }
                catch (TargetInvocationException tie)
                {
                    var inner = tie.InnerException ?? tie;
                    try { MessageBox.Show(this, $"Handler '{methodName}' löste eine Ausnahme aus:\n{inner}", "DEBUG: Handler-Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
                    return;
                }
                catch (Exception exInvoke)
                {
                    try { MessageBox.Show(this, $"Invoke fehlgeschlagen: {exInvoke}", "DEBUG: Invoke-Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
                    return;
                }

                if (invokeResult is Task t)
                {
                    try
                    {
                        // WICHTIG: hier kein ConfigureAwait(false) — wir wollen UI-Fortsetzungen wieder auf UI-Thread
                        await t;
                    }
                    catch (Exception exAwait)
                    {
                        try
                        {
                            this.BeginInvoke(new Action(() =>
                                MessageBox.Show(this, $"Handler '{methodName}' warf während await eine Ausnahme:\n{exAwait}", "DEBUG: Handler-Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error)
                            ));
                        }
                        catch { /* ignore */ }
                    }
                }
            }
            catch (Exception ex)
            {
                try { MessageBox.Show(this, $"InvokeOptionalHandlerAsync: {ex}", "DEBUG: Reflection Error", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
            }
        }
    }
}
