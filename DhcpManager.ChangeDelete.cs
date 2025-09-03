// DhcpManager.ChangeDelete.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace DhcpWmiViewer
{
    public static partial class DhcpManager
    {
        public static async Task ChangeReservationIpAsync(
            string server,
            string scopeId,
            string oldIp,
            string newIp,
            string clientId,
            string name,
            string description,
            Func<string, PSCredential?> getCredentials)
        {
            DhcpWmiViewer.Helpers.WriteDebugLog("ChangeReservationIpAsync starting");
            DhcpWmiViewer.Helpers.WriteDebugLog($"Server={server}, ScopeId={scopeId}, OldIp={oldIp}, NewIp={newIp}, ClientIdRaw={clientId}");

            var reservations = await QueryReservationsAsync(server, scopeId, getCredentials).ConfigureAwait(false);
            if (reservations != null && reservations.Rows.Cast<DataRow>().Any(r => string.Equals((r["IPAddress"] ?? "").ToString(), newIp, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("IP address " + newIp + " is already reserved.");
            }

            var candidates = DhcpWmiViewer.Helpers.BuildClientIdCandidates(clientId).ToList();
            if (!candidates.Any()) candidates.Add(string.Empty);

            var addErrors = new List<string>();
            var addSucceeded = false;
            foreach (var candidate in candidates)
            {
                DhcpWmiViewer.Helpers.WriteDebugLog("Attempting Add (new IP) with ClientId='" + (candidate ?? "<null>") + "'");

                try
                {
                    await PowerShellExecutor.ExecutePowerShellActionAsync(server, getCredentials, ps =>
                    {
                        ps.Commands.Clear();
                        var add = ps.AddCommand("Add-DhcpServerv4Reservation")
                                    .SafeAddParameter("ScopeId", scopeId)
                                    .SafeAddParameter("IPAddress", newIp)
                                    .SafeAddParameter("ErrorAction", "Stop");
                        if (!string.IsNullOrWhiteSpace(candidate))
                            add.SafeAddParameter("ClientId", candidate);
                        if (!string.IsNullOrWhiteSpace(name))
                            add.SafeAddParameter("Name", name);
                        if (!string.IsNullOrWhiteSpace(description))
                            add.SafeAddParameter("Description", description);
                    }).ConfigureAwait(false);

                    var afterAdd = await QueryReservationsAsync(server, scopeId, getCredentials).ConfigureAwait(false);
                    var found = afterAdd != null && afterAdd.Rows.Cast<DataRow>().Any(r => string.Equals((r["IPAddress"] ?? "").ToString(), newIp, StringComparison.OrdinalIgnoreCase));
                    if (found)
                    {
                        addSucceeded = true;
                        DhcpWmiViewer.Helpers.WriteDebugLog("Add new reservation succeeded with ClientId='" + (candidate ?? "<null>") + "'");
                        break;
                    }
                    else
                    {
                        addErrors.Add($"ClientId='{candidate ?? "<null>"}': Add did not create a reservation (no error text).");
                    }
                }
                catch (Exception ex)
                {
                    addErrors.Add($"ClientId='{candidate ?? "<null>"}': {ex.Message}");
                    DhcpWmiViewer.Helpers.WriteDebugLog("Add new reservation failed with ClientId='" + (candidate ?? "<null>") + "': " + ex);
                }
            }

            if (!addSucceeded)
                throw new InvalidOperationException("Failed to add new reservation for " + newIp + ". Attempts:\n" + string.Join("\n", addErrors));

            try
            {
                // Try parameter set using ClientId (preferred if available)
                bool removed = false;
                if (!string.IsNullOrWhiteSpace(clientId))
                {
                    try
                    {
                        await PowerShellExecutor.ExecutePowerShellActionAsync(server, getCredentials, ps =>
                        {
                            ps.Commands.Clear();
                            ps.AddCommand("Remove-DhcpServerv4Reservation")
                              .SafeAddParameter("ComputerName", server)
                              .SafeAddParameter("ScopeId", scopeId)
                              .SafeAddParameter("ClientId", clientId)
                              .SafeAddParameter("ErrorAction", "Stop");
                        }).ConfigureAwait(false);
                        removed = true;
                    }
                    catch { /* fall back to IP */ }
                }

                if (!removed)
                {
                    await PowerShellExecutor.ExecutePowerShellActionAsync(server, getCredentials, ps =>
                    {
                        ps.Commands.Clear();
                        ps.AddCommand("Remove-DhcpServerv4Reservation")
                          .SafeAddParameter("ComputerName", server)
                          .SafeAddParameter("ScopeId", scopeId)
                          .SafeAddParameter("IPAddress", oldIp)
                          .SafeAddParameter("ErrorAction", "Stop");
                    }).ConfigureAwait(false);
                }

                var afterRemove = await QueryReservationsAsync(server, scopeId, getCredentials).ConfigureAwait(false);
                var stillExists = afterRemove != null && afterRemove.Rows.Cast<DataRow>().Any(r => string.Equals((r["IPAddress"] ?? "").ToString(), oldIp, StringComparison.OrdinalIgnoreCase));
                if (stillExists)
                {
                    throw new InvalidOperationException("Removing old reservation appears to have failed (old IP still present).");
                }

                DhcpWmiViewer.Helpers.WriteDebugLog("ChangeReservationIpAsync completed successfully");
                return;
            }
            catch (Exception removeEx)
            {
                DhcpWmiViewer.Helpers.WriteDebugLog("Removing old reservation failed: " + removeEx);

                var rollbackErrors = new List<string>();
                try
                {
                    await PowerShellExecutor.ExecutePowerShellActionAsync(server, getCredentials, ps =>
                    {
                        ps.Commands.Clear();
                        ps.AddCommand("Remove-DhcpServerv4Reservation")
                          .SafeAddParameter("ComputerName", server)
                          .SafeAddParameter("IPAddress", newIp)
                          .SafeAddParameter("ErrorAction", "Stop");
                    }).ConfigureAwait(false);
                }
                catch (Exception rbRemEx)
                {
                    rollbackErrors.Add("Fail removing new reservation: " + rbRemEx.Message);
                }

                try
                {
                    await PowerShellExecutor.ExecutePowerShellActionAsync(server, getCredentials, ps =>
                    {
                        ps.Commands.Clear();
                        var add = ps.AddCommand("Add-DhcpServerv4Reservation")
                                    .SafeAddParameter("ScopeId", scopeId)
                                    .SafeAddParameter("IPAddress", oldIp)
                                    .SafeAddParameter("ErrorAction", "Stop");
                        if (!string.IsNullOrWhiteSpace(clientId))
                            add.SafeAddParameter("ClientId", clientId);
                        if (!string.IsNullOrWhiteSpace(name))
                            add.SafeAddParameter("Name", name);
                        if (!string.IsNullOrWhiteSpace(description))
                            add.SafeAddParameter("Description", description);
                    }).ConfigureAwait(false);
                }
                catch (Exception rbAddEx)
                {
                    rollbackErrors.Add("Fail re-adding old reservation: " + rbAddEx.Message);
                }

                if (rollbackErrors.Any())
                {
                    throw new InvalidOperationException("Remove old reservation failed and rollback had errors: " + string.Join(" | ", rollbackErrors), removeEx);
                }

                throw new InvalidOperationException("Remove old reservation failed but rollback succeeded (old reservation restored). Original error: " + removeEx.Message, removeEx);
            }
        }

        public static async Task DeleteReservationAsync(
            string server,
            string scopeId,
            string ipAddress,
            string clientId,
            Func<string, PSCredential?> getCredentials)
        {
            DhcpWmiViewer.Helpers.WriteDebugLog("DeleteReservationAsync starting");
            DhcpWmiViewer.Helpers.WriteDebugLog($"Server={server}, ScopeId={scopeId}, IP={ipAddress}, ClientIdRaw={clientId}");

            try
            {
                await PowerShellExecutor.ExecutePowerShellActionAsync(server, getCredentials, ps =>
                {
                    ps.Commands.Clear();
                    var cmd = ps.AddCommand("Remove-DhcpServerv4Reservation")
                                .SafeAddParameter("ComputerName", server)
                                .SafeAddParameter("ErrorAction", "Stop");

                    if (!string.IsNullOrWhiteSpace(clientId))
                    {
                        cmd.SafeAddParameter("ScopeId", scopeId)
                           .SafeAddParameter("ClientId", clientId);
                    }
                    else
                    {
                        cmd.SafeAddParameter("IPAddress", ipAddress);
                    }
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                DhcpWmiViewer.Helpers.WriteDebugLog("Remove-DhcpServerv4Reservation failed: " + ex);
                throw new InvalidOperationException("Error executing Remove-DhcpServerv4Reservation: " + ex.Message, ex);
            }

            var after = await QueryReservationsAsync(server, scopeId, getCredentials).ConfigureAwait(false);
            var stillExists = after != null && after.Rows.Cast<DataRow>().Any(r => string.Equals((r["IPAddress"] ?? "").ToString(), ipAddress, StringComparison.OrdinalIgnoreCase));
            if (stillExists)
            {
                throw new InvalidOperationException("Removing reservation appears to have failed (old IP still present).");
            }

            DhcpWmiViewer.Helpers.WriteDebugLog("DeleteReservationAsync finished successfully");
        }
    }
}
