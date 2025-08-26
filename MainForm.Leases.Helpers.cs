// MainForm.Leases.Helpers.cs
// DIESE DATEI WIRD KOMPLETT ANGEZEIGT — EINFACH KOPIEREN & EINFÜGEN
using System;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// Versucht, einen Wert aus einer DataGridViewRow zu lesen, indem mehrere mögliche Spaltennamen
        /// durchprobiert werden (z.B. "Col_AddressState" oder "AddressState").
        /// Liefert null, wenn keine Spalte gefunden oder Wert null/leer ist.
        /// </summary>
        protected string? TryGetCellValue(DataGridViewRow? row, params string[] names)
        {
            try
            {
                if (row == null || names == null || names.Length == 0) return null;
                foreach (var n in names)
                {
                    try
                    {
                        if (row.Cells.Contains(n))
                        {
                            var v = row.Cells[n]?.Value;
                            if (v != null)
                            {
                                var s = v.ToString();
                                if (!string.IsNullOrWhiteSpace(s)) return s;
                            }
                        }
                    }
                    catch { /* ignore per-cell errors */ }
                }
            }
            catch { /* swallow */ }
            return null;
        }

        /// <summary>
        /// Liest die IP, ClientId und HostName aus einer Lease-DataGridViewRow und liefert
        /// ein Tupel. Defensive: bei Fehlern werden leere Strings zurückgegeben.
        /// </summary>
        protected (string Ip, string ClientId, string HostName) ReadLeaseRowValuesSafe(DataGridViewRow row)
        {
            try
            {
                var ip = TryGetCellValue(row, "IPAddress", "Col_IPAddress") ?? string.Empty;
                var clientId = TryGetCellValue(row, "ClientId", "Col_ClientId") ?? string.Empty;
                var hostName = TryGetCellValue(row, "HostName", "Col_HostName") ?? string.Empty;
                return (ip, clientId, hostName);
            }
            catch
            {
                return (string.Empty, string.Empty, string.Empty);
            }
        }

        /// <summary>
        /// Reflection-invoker (keine Exceptions nach außen) — ruft eine Instanzmethode mit dem Namen methodName
        /// auf *diesem* MainForm-Objekt, falls vorhanden. Nutzt BindingFlags.Instance | NonPublic.
        /// Diese Methode ist bereits vorhanden in manchen Partials — hier als proteceted impl.
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

                MessageBox.Show(this, $"Handler '{methodName}' ist nicht vorhanden.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Fehler beim Aufrufen von '{methodName}': {ex.Message}", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Neu: Versucht per Reflection, eine statische DhcpManager-Methode mit Name methodName aufzurufen.
        /// Erwartetes Verhalten:
        ///  - Wenn ein Task zurückgegeben wird: await; wenn Task<TResult> und TResult is bool -> Ergebnis zurückgeben.
        ///  - Wenn bool direkt zurückgegeben wird: zurückgeben.
        ///  - Bei void / Task (ohne Result): bei erfolgreichem Aufruf true zurückgeben.
        ///  - Bei Fehlern: false. Die Methode probiert alle Overloads mit passender Parameteranzahl und
        ///    versucht, sie aufzurufen (Fehler werden gefangen).
        /// 
        /// Usage sample (so wie im Code): 
        ///   var created = await TryInvokeDhcpManagerBoolMethodAsync("CreateReservationFromLeaseAsync", server, scopeId, ip, clientId, name, desc, new Func<string, PSCredential?>(GetCredentialsForServer));
        /// </summary>
        protected async Task<bool> TryInvokeDhcpManagerBoolMethodAsync(string methodName, params object?[] args)
        {
            try
            {
                var t = typeof(DhcpManager);
                var methods = t.GetMethods(BindingFlags.Public | BindingFlags.Static)
                               .Where(m => string.Equals(m.Name, methodName, StringComparison.Ordinal));

                foreach (var mi in methods)
                {
                    // quick filter: same parameter count (best-effort)
                    var pars = mi.GetParameters();
                    if (pars.Length != args.Length) continue;

                    try
                    {
                        // Try invoke; if parameter types don't match Invoke will throw -> catch and continue
                        var invokeResult = mi.Invoke(null, args);

                        // null result -> for many void or Task methods, null can be OK -> interpret as success
                        if (invokeResult == null) return true;

                        // If Task
                        if (invokeResult is Task task)
                        {
                            await task.ConfigureAwait(false);

                            // If generic Task<TResult>, try read Result
                            var taskType = task.GetType();
                            if (taskType.IsGenericType)
                            {
                                var resProp = taskType.GetProperty("Result", BindingFlags.Instance | BindingFlags.Public);
                                if (resProp != null)
                                {
                                    var val = resProp.GetValue(task);
                                    if (val is bool b) return b;
                                    // if other result type, treat non-null as success
                                    return val != null;
                                }
                            }

                            // non-generic Task completed -> treat as success
                            return true;
                        }

                        // If direct bool
                        if (invokeResult is bool bb) return bb;

                        // other non-null return -> treat as success
                        return invokeResult != null;
                    }
                    catch
                    {
                        // try next overload
                        continue;
                    }
                }
            }
            catch
            {
                // swallow
            }

            // nothing matched or all failed
            return false;
        }
    }
}
