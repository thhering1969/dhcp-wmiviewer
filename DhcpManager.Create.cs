// DhcpManager.Create.cs
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
        public static async Task CreateReservationFromLeaseAsync(
            string server,
            string scopeId,
            string ipAddress,
            string clientId,
            string name,
            string description,
            Func<string, PSCredential?> getCredentials)
        {
            DhcpWmiViewer.Helpers.WriteDebugLog("CreateReservationFromLeaseAsync starting");
            DhcpWmiViewer.Helpers.WriteDebugLog($"Server={server}, ScopeId={scopeId}, IP={ipAddress}, ClientIdRaw={clientId}, Name={name}, Desc={description}");

            // 1) Abfragen ob IP bereits reserviert ist (remote)
            var existing = await QueryReservationsAsync(server, scopeId, getCredentials).ConfigureAwait(false);
            if (existing != null && existing.Rows.Count > 0)
            {
                foreach (DataRow r in existing.Rows)
                {
                    var val = (r["IPAddress"] ?? string.Empty).ToString();
                    if (string.Equals(val, ipAddress, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("IP address " + ipAddress + " is already reserved.");
                    }
                }
            }

            // 2) Versuche Add mit Kandidaten
            var candidates = DhcpWmiViewer.Helpers.BuildClientIdCandidates(clientId).ToList();
            if (!candidates.Any()) candidates.Add(string.Empty);

            var addErrors = new List<string>();
            foreach (var candidate in candidates)
            {
                DhcpWmiViewer.Helpers.WriteDebugLog("Attempting Add-DhcpServerv4Reservation with ClientId='" + (candidate ?? "<null>") + "'");

                try
                {
                    await PowerShellExecutor.ExecutePowerShellActionAsync(server, getCredentials, ps =>
                    {
                        ps.Commands.Clear();
                        var addCmd = ps.AddCommand("Add-DhcpServerv4Reservation")
                                      .AddParameter("ComputerName", server)
                                      .AddParameter("ScopeId", scopeId)
                                      .AddParameter("IPAddress", ipAddress)
                                      .AddParameter("ErrorAction", "Stop");
                        if (!string.IsNullOrWhiteSpace(candidate))
                            addCmd.AddParameter("ClientId", candidate);
                        if (!string.IsNullOrWhiteSpace(name))
                            addCmd.AddParameter("Name", name);
                        if (!string.IsNullOrWhiteSpace(description))
                            addCmd.AddParameter("Description", description);
                    }).ConfigureAwait(false);

                    var after = await QueryReservationsAsync(server, scopeId, getCredentials).ConfigureAwait(false);
                    var found = after != null && after.Rows.Cast<DataRow>().Any(r => string.Equals((r["IPAddress"] ?? "").ToString(), ipAddress, StringComparison.OrdinalIgnoreCase));
                    if (found)
                    {
                        DhcpWmiViewer.Helpers.WriteDebugLog("Add-DhcpServerv4Reservation success with ClientId='" + (candidate ?? "<null>") + "'");
                        DhcpWmiViewer.Helpers.WriteDebugLog("CreateReservationFromLeaseAsync finished successfully");
                        return;
                    }
                    else
                    {
                        addErrors.Add($"ClientId='{candidate ?? "<null>"}': Add did not produce reservation (no error text).");
                    }
                }
                catch (Exception ex)
                {
                    addErrors.Add($"ClientId='{candidate ?? "<null>"}': {ex.Message}");
                    DhcpWmiViewer.Helpers.WriteDebugLog("Add-DhcpServerv4Reservation failed with ClientId='" + (candidate ?? "<null>") + "': " + ex);
                }
            }

            throw new InvalidOperationException("All attempts to create reservation failed:\n" + string.Join("\n", addErrors));
        }
    }
}
