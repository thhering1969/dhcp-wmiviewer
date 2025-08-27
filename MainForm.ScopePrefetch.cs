// MainForm.ScopePrefetch.cs
// Proaktives Prefetch von Reservations beim Scope-Wechsel (background, non-blocking)

using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management.Automation;

namespace DhcpWmiViewer
{
    public partial class MainForm : Form
    {
        // verhindert gleichzeitige Prefetches für denselben Scope (leichtgewichtige Guard)
        private string? _prefetchScopeInProgress = null;
        private readonly object _prefetchLock = new object();

        /// <summary>
        /// Public/instance helper — startet Prefetch für gegebenen scopeId (fire-and-forget).
        /// Diese Methode wird von der vorhandenen Dgv_SelectionChanged in MainForm.Scopes.cs aufgerufen.
        /// </summary>
        public void StartPrefetchReservationsForScope(string scopeId)
        {
            if (string.IsNullOrWhiteSpace(scopeId)) return;
            _ = PrefetchReservationsForScopeAsync(scopeId);
        }

        /// <summary>
        /// Hintergrund-Prefetch: versucht Reservations für scopeId zu laden und bei Erfolg die bindingReservations zu setzen.
        /// Kürzt mit Timeout ab, läuft resilient bei fehlenden Credentials.
        /// </summary>
        private async Task PrefetchReservationsForScopeAsync(string scopeId)
        {
            if (string.IsNullOrWhiteSpace(scopeId)) return;

            // Doppelte gleichzeitige Prefetches für denselben Scope vermeiden
            lock (_prefetchLock)
            {
                if (_prefetchScopeInProgress != null && string.Equals(_prefetchScopeInProgress, scopeId, StringComparison.OrdinalIgnoreCase))
                {
                    // bereits in Arbeit
                    return;
                }
                _prefetchScopeInProgress = scopeId;
            }

            try
            {
                Helpers.WriteDebugLog($"TRACE: PrefetchReservationsForScopeAsync starting for {scopeId}");

                string server = GetServerNameOrDefault();

                // Cred-Fabrik: versuche vorhandene Credentials zurückzugeben; falls null -> we handle gracefully
                Func<string, PSCredential>? credFactory = (s) =>
                {
                    try { return GetCredentialsForServer(s); } catch { return null; }
                };

                // Starte Query in background
                Task<DataTable?> queryTask;
                try
                {
                    queryTask = DhcpManager.QueryReservationsAsync(server, scopeId, s => credFactory(s));
                }
                catch (Exception exStart)
                {
                    Helpers.WriteDebugLog("TRACE: Prefetch start failed: " + exStart);
                    return;
                }

                // Kurzes Timeout damit Prefetch nie lange blockiert (z. B. 2000ms)
                var timeoutMs = 2000;
                var completed = await Task.WhenAny(queryTask, Task.Delay(timeoutMs)).ConfigureAwait(false);
                if (!ReferenceEquals(completed, queryTask))
                {
                    Helpers.WriteDebugLog($"TRACE: PrefetchReservationsForScopeAsync timed out after {timeoutMs}ms for {scopeId}");
                    return;
                }

                DataTable? resTable = null;
                try
                {
                    resTable = await queryTask.ConfigureAwait(false);
                }
                catch (Exception exQuery)
                {
                    Helpers.WriteDebugLog("TRACE: Prefetch Reservations failed (background): " + exQuery);
                    return;
                }

                if (resTable == null)
                {
                    Helpers.WriteDebugLog($"TRACE: Prefetch returned null table for {scopeId}");
                    return;
                }

                // Update UI binding on UI thread
                SafeBeginInvoke(() =>
                {
                    try
                    {
                        if (bindingReservations != null)
                        {
                            bindingReservations.DataSource = resTable;
                        }
                        else if (dgvReservations != null)
                        {
                            dgvReservations.DataSource = resTable;
                        }

                        try
                        {
                            if (dgvReservations != null)
                            {
                                dgvReservations.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
                                dgvReservations.Refresh();
                            }
                        }
                        catch { /* ignore UI resize problems */ }

                        Helpers.WriteDebugLog($"TRACE: PrefetchReservationsForScopeAsync applied results for {scopeId} (rows={resTable.Rows.Count})");
                    }
                    catch (Exception exUi)
                    {
                        Helpers.WriteDebugLog("TRACE: Prefetch UI-apply failed: " + exUi);
                    }
                });
            }
            finally
            {
                lock (_prefetchLock)
                {
                    _prefetchScopeInProgress = null;
                }
            }
        }
    }
}
