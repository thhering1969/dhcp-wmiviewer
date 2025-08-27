// MainForm.Leases.Helpers.cs
// **KOMPLETTE DATEI** — ersetzt die vorhandene Helper-Partial komplett.
// Diese Version verwendet Debug.WriteLine anstelle von Debug-MessageBoxes,
// damit Debug-Informationen im Output landen, aber keine störenden Popups erscheinen.

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;

namespace DhcpWmiViewer
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// Sucht in der gegebenen Row nach einer Zelle, deren Spalte einen der angegebenen Namen hat.
        /// Liefert den Wert als string zurück oder null, falls nicht gefunden / leer.
        /// </summary>
        protected string? TryGetCellValue(DataGridViewRow? row, params string[] names)
        {
            try
            {
                if (row == null || names == null || names.Length == 0) return null;

                var grid = row.DataGridView;

                foreach (var name in names)
                {
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    int colIdx = -1;

                    if (dgvLeases != null)
                    {
                        var colByName = dgvLeases.Columns.Cast<DataGridViewColumn>()
                                            .FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
                        if (colByName != null) colIdx = colByName.Index;
                    }

                    if (colIdx < 0 && grid != null)
                    {
                        var col = grid.Columns.Cast<DataGridViewColumn>()
                                    .FirstOrDefault(c =>
                                        string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(c.DataPropertyName ?? string.Empty, name, StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(c.HeaderText ?? string.Empty, name, StringComparison.OrdinalIgnoreCase));
                        if (col != null) colIdx = col.Index;
                    }

                    if (colIdx >= 0 && colIdx < row.Cells.Count)
                    {
                        var cell = row.Cells[colIdx];
                        if (cell?.Value != null)
                        {
                            var s = cell.Value.ToString();
                            if (!string.IsNullOrWhiteSpace(s)) return s;
                        }
                    }

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
        /// Fehler werden nicht als Debug-MessageBox angezeigt (Debug.WriteLine stattdessen).
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
                    Debug.WriteLine($"Handler '{methodName}' nicht gefunden (Reflection).");
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
                    Debug.WriteLine($"Handler '{methodName}' löste eine Ausnahme aus: {inner}");
                    return;
                }
                catch (Exception exInvoke)
                {
                    Debug.WriteLine($"Invoke fehlgeschlagen: {exInvoke}");
                    return;
                }

                if (invokeResult is Task t)
                {
                    try
                    {
                        // await so that UI-continuation runs on UI thread
                        await t;
                    }
                    catch (Exception exAwait)
                    {
                        try
                        {
                            // log instead of showing a debug popup
                            Debug.WriteLine($"Handler '{methodName}' warf während await eine Ausnahme: {exAwait}");
                        }
                        catch { /* ignore */ }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InvokeOptionalHandlerAsync: {ex}");
            }
        }
    }
}
