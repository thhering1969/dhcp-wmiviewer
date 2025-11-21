// DhcpManager.Update.cs
using System;
using System.Data;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace DhcpWmiViewer
{
    public static partial class DhcpManager
    {
        public static async Task UpdateReservationPropertiesAsync(
            string server,
            string scopeId,
            string ipAddress,
            string clientId,
            string name,
            string description,
            Func<string, PSCredential?> getCredentials)
        {
            DhcpWmiViewer.Helpers.WriteDebugLog("UpdateReservationPropertiesAsync starting");
            DhcpWmiViewer.Helpers.WriteDebugLog($"Server={server}, ScopeId={scopeId}, IP={ipAddress}, ClientIdRaw={clientId}, Name={name}, Desc={description}");

            try
            {
                await PowerShellExecutor.ExecutePowerShellActionAsync(server, getCredentials, ps =>
                {
                    ps.Commands.Clear();
                    var setCmd = ps.AddCommand("Set-DhcpServerv4Reservation")
                                   .SafeAddParameter("IPAddress", ipAddress)
                                   .SafeAddParameter("ErrorAction", "Stop");

                    if (!string.IsNullOrWhiteSpace(name))
                        setCmd.SafeAddParameter("Name", name);
                    if (!string.IsNullOrWhiteSpace(description))
                        setCmd.SafeAddParameter("Description", description);
                    if (!string.IsNullOrWhiteSpace(clientId))
                        setCmd.SafeAddParameter("ClientId", clientId);

                    if (!string.IsNullOrWhiteSpace(server) && server != ".")
                        setCmd.SafeAddParameter("ComputerName", server);
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                DhcpWmiViewer.Helpers.WriteDebugLog("Set-DhcpServerv4Reservation failed: " + ex);
                throw new InvalidOperationException("Error executing Set-DhcpServerv4Reservation: " + ex.Message, ex);
            }

            try
            {
                var after = await QueryReservationsAsync(server, scopeId, getCredentials).ConfigureAwait(false);
                var row = after?.Rows.Cast<DataRow>().FirstOrDefault(r => string.Equals((r["IPAddress"] ?? "").ToString(), ipAddress, StringComparison.OrdinalIgnoreCase));
                if (row == null)
                {
                    throw new InvalidOperationException("Reservation not found after Set-DhcpServerv4Reservation. The cmdlet may not be available on the remote host.");
                }
            }
            catch (Exception ve)
            {
                DhcpWmiViewer.Helpers.WriteDebugLog("Verification after Set-DhcpServerv4Reservation failed: " + ve);
                throw;
            }

            DhcpWmiViewer.Helpers.WriteDebugLog("UpdateReservationPropertiesAsync finished successfully");
        }

        /// <summary>
        /// Aktualisiert eine bestehende DHCP-Reservation (mit IP-Änderung)
        /// </summary>
        public static async Task<bool> UpdateReservationAsync(string server, string scopeId, string clientId, string newIP, string newName, string newDescription, Func<string, PSCredential?> getCredentials)
        {
            try
            {
                await PowerShellExecutor.ExecutePowerShellActionAsync(server, getCredentials, ps =>
                {
                    ps.Commands.Clear();
                    
                    // Erst alte Reservation entfernen
                    ps.AddCommand("Remove-DhcpServerv4Reservation")
                      .SafeAddParameter("ScopeId", scopeId)
                      .SafeAddParameter("ClientId", clientId)
                      .SafeAddParameter("ErrorAction", "Stop");
                      
                    if (!string.IsNullOrWhiteSpace(server) && server != ".")
                        ps.Commands.AddParameter("ComputerName", server);

                    ps.AddStatement();
                    
                    // Neue Reservation mit neuer IP hinzufügen
                    var addCmd = ps.AddCommand("Add-DhcpServerv4Reservation")
                                   .SafeAddParameter("ScopeId", scopeId)
                                   .SafeAddParameter("IPAddress", newIP)
                                   .SafeAddParameter("ClientId", clientId)
                                   .SafeAddParameter("ErrorAction", "Stop");

                    if (!string.IsNullOrWhiteSpace(newName))
                        addCmd.SafeAddParameter("Name", newName);
                    if (!string.IsNullOrWhiteSpace(newDescription))
                        addCmd.SafeAddParameter("Description", newDescription);
                    if (!string.IsNullOrWhiteSpace(server) && server != ".")
                        addCmd.SafeAddParameter("ComputerName", server);
                });
                
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("UpdateReservationAsync failed: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Aktualisiert nur Name und Description einer Reservation (ohne IP-Änderung)
        /// </summary>
        public static async Task<bool> UpdateReservationDetailsAsync(string server, string scopeId, string clientId, string newName, string newDescription, Func<string, PSCredential?> getCredentials)
        {
            try
            {
                await PowerShellExecutor.ExecutePowerShellActionAsync(server, getCredentials, ps =>
                {
                    ps.Commands.Clear();
                    var setCmd = ps.AddCommand("Set-DhcpServerv4Reservation")
                                   .SafeAddParameter("ScopeId", scopeId)  
                                   .SafeAddParameter("ClientId", clientId)
                                   .SafeAddParameter("ErrorAction", "Stop");

                    if (!string.IsNullOrWhiteSpace(newName))
                        setCmd.SafeAddParameter("Name", newName);
                    if (!string.IsNullOrWhiteSpace(newDescription))
                        setCmd.SafeAddParameter("Description", newDescription);
                    if (!string.IsNullOrWhiteSpace(server) && server != ".")
                        setCmd.SafeAddParameter("ComputerName", server);
                });
                
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("UpdateReservationDetailsAsync failed: {0}", ex.Message);
                return false;
            }
        }
    }
}
