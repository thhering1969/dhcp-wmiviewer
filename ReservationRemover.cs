// ReservationRemover.cs

using System;
using System.Data;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace DhcpWmiViewer
{
    /// <summary>
    /// Hilfsklasse zum Entfernen einer Reservation via PowerShell.
    /// Liefert true zurück wenn nach der Operation keine Reservation für die IP mehr gefunden wurde.
    /// </summary>
    public static class ReservationRemover
    {
        public static async Task<bool> DeleteReservationAsync(string server, string scopeId, string ipAddress, Func<string, PSCredential?> getCredentials)
        {
            try
            {
                // Versuche Remove-DhcpServerv4Reservation remote/lokal via PowerShellExecutor
                await PowerShellExecutor.ExecutePowerShellActionAsync(server, getCredentials, ps =>
                {
                    ps.Commands.Clear();
                    ps.AddCommand("Remove-DhcpServerv4Reservation")
                      .SafeAddParameter("ScopeId", scopeId)
                      .SafeAddParameter("IPAddress", ipAddress)
                      .SafeAddParameter("Confirm", new SwitchParameter(false))
                      .SafeAddParameter("ErrorAction", "Stop");
                }).ConfigureAwait(false);

                // Verifiziere: ist die Reservation noch vorhanden?
                var after = await DhcpManager.QueryReservationsAsync(server, scopeId, getCredentials).ConfigureAwait(false);
                var stillExists = after != null && after.Rows.Cast<DataRow>().Any(r => string.Equals((r["IPAddress"] ?? "").ToString(), ipAddress, StringComparison.OrdinalIgnoreCase));
                return !stillExists;
            }
            catch
            {
                return false;
            }
        }
    }
}
