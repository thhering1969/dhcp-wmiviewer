// ReservationLookupAdapter.cs
using System;
using System.Data;
using System.Management.Automation;
using System.Threading.Tasks;

namespace DhcpWmiViewer
{
    /// <summary>
    /// Hilfsadapter für MainForm: erstellt eine Func<string, Task<DataTable>>
    /// die der Dialog erwartet, basierend auf DhcpManager.QueryReservationsAsync.
    /// </summary>
    public static class ReservationLookupAdapter
    {
        /// <summary>
        /// Liefert einen Callback, der ein ScopeId entgegennimmt und DataTable mit Reservations zurückgibt.
        /// server = DHCP-Servername, getCreds = delegierte Funktion.
        /// </summary>
        public static Func<string, Task<DataTable>> CreateLookup(string server, Func<string, PSCredential> getCreds)
        {
            return async (scopeId) =>
            {
                // DhcpManager.QueryReservationsAsync erwartet (server, scopeId, getCredentials)
                var dt = await DhcpManager.QueryReservationsAsync(server, scopeId, getCreds).ConfigureAwait(false);
                return dt ?? new DataTable();
            };
        }
    }
}
