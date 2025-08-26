// MainForm.Leases.Helpers.cs
using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// Robuster Zugriff auf Zellenwerte: prüft mehrere mögliche Spaltennamen
        /// (z.B. "Col_IPAddress" und "IPAddress") und gibt den ersten Treffer zurück.
        /// </summary>
        protected string? TryGetCellValue(DataGridViewRow? row, params string[] names)
        {
            try
            {
                if (row == null || names == null || names.Length == 0) return null;
                var dgv = row.DataGridView;
                if (dgv == null) return null;

                foreach (var name in names)
                {
                    try
                    {
                        if (dgv.Columns.Contains(name))
                        {
                            var cell = row.Cells[name];
                            if (cell != null) return cell.Value?.ToString();
                        }
                    }
                    catch { /* ignore missing/index issues */ }
                }

                // fallback: try by headerText match (case-insensitive)
                foreach (DataGridViewCell c in row.Cells)
                {
                    try
                    {
                        var col = c.OwningColumn;
                        if (col != null)
                        {
                            var header = col.HeaderText ?? string.Empty;
                            foreach (var name in names)
                            {
                                if (string.Equals(header.Trim(), name, StringComparison.OrdinalIgnoreCase))
                                {
                                    return c.Value?.ToString();
                                }
                            }
                        }
                    }
                    catch { }
                }

                return null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Liest die wichtigsten Lease-Felder (Ip, ClientId, HostName) sicher aus einer DataGridViewRow.
        /// </summary>
        protected (string Ip, string ClientId, string HostName) ReadLeaseRowValuesSafe(DataGridViewRow row)
        {
            try
            {
                var ip = TryGetCellValue(row, "Col_IPAddress", "IPAddress") ?? string.Empty;
                var clientId = TryGetCellValue(row, "Col_ClientId", "ClientId") ?? string.Empty;
                var hostName = TryGetCellValue(row, "Col_HostName", "HostName") ?? string.Empty;
                return (ip, clientId, hostName);
            }
            catch { return (string.Empty, string.Empty, string.Empty); }
        }

        /// <summary>
        /// Versucht per Reflection eine private Instanzmethode aufzurufen, falls vorhanden.
        /// (keine Exceptions nach außen)
        /// </summary>
        protected async Task InvokeOptionalHandlerAsync(string methodName)
        {
            try
            {
                var mi = this.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (mi != null)
                {
                    var ret = mi.Invoke(this, null);
                    if (ret is Task t) await t.ConfigureAwait(false);
                    else if (ret is ValueTask vt) await vt.AsTask().ConfigureAwait(false);
                    return;
                }

                // falls nicht vorhanden: informiere (optional)
                this.BeginInvoke(new Action(() =>
                {
                    MessageBox.Show(this, $"Handler '{methodName}' ist nicht vorhanden.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }));
            }
            catch (Exception ex)
            {
                try { this.BeginInvoke(new Action(() => MessageBox.Show(this, $"Fehler beim Aufrufen von '{methodName}': {ex.Message}", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error))); } catch { }
            }
        }
    }
}
