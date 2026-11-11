// DhcpManager.cs
using System.Data;
using System.Management.Automation;

namespace DhcpWmiViewer
{
    /// <summary>
    /// DHCP-Abfragen und -Änderungen über die DhcpServer-Cmdlets (ausgeführt auf dem Zielserver).
    /// Schreibende Operationen: siehe DhcpManager.Reservations.cs.
    /// </summary>
    public static partial class DhcpManager
    {
        /// <summary>Zeigt vor jedem PowerShell-Aufruf eine Vorschau des Befehls an.</summary>
        public static bool ShowCommandPreview { get; set; }

        public static readonly string[] ScopeColumns =
            { "Name", "ScopeId", "StartRange", "EndRange", "SubnetMask", "State", "Description" };

        public static readonly string[] ReservationColumns =
            { "IPAddress", "ClientId", "Name", "Description", "AddressState" };

        public static readonly string[] LeaseColumns =
            { "IPAddress", "ClientId", "ClientType", "HostName", "Description", "AddressState", "LeaseExpiryTime", "ScopeId", "ServerIP", "PSComputerName" };

        /// <summary>Leere Tabelle mit den angegebenen (string-)Spalten.</summary>
        public static DataTable CreateTable(string[] columns)
        {
            var dt = new DataTable();
            foreach (var c in columns) dt.Columns.Add(c, typeof(string));
            return dt;
        }

        public static Task<DataTable> QueryScopesAsync(string server, Func<string, PSCredential?>? getCredentials)
            => QueryAsync(server, getCredentials, "Get-DhcpServerv4Scope", scopeId: null, ScopeColumns);

        public static Task<DataTable> QueryReservationsAsync(string server, string scopeId, Func<string, PSCredential?>? getCredentials)
            => QueryAsync(server, getCredentials, "Get-DhcpServerv4Reservation", RequireScopeId(scopeId), ReservationColumns);

        public static Task<DataTable> QueryLeasesAsync(string server, string scopeId, Func<string, PSCredential?>? getCredentials)
            => QueryAsync(server, getCredentials, "Get-DhcpServerv4Lease", RequireScopeId(scopeId), LeaseColumns);

        private static Task<DataTable> QueryAsync(string server, Func<string, PSCredential?>? getCredentials, string cmdlet, string? scopeId, string[] columns)
        {
            return PowerShellExecutor.ExecutePowerShellQueryAsync(server, getCredentials, ps =>
            {
                ps.AddCommand(cmdlet).SafeAddParameter("ScopeId", scopeId);
                // Nur benötigte Properties übertragen (deutlich weniger Daten als komplette CIM-Objekte)
                ps.AddCommand("Select-Object").AddParameter("Property", columns);
            }, columns, preferRsat: true);
        }

        private static string RequireScopeId(string scopeId)
        {
            if (string.IsNullOrWhiteSpace(scopeId))
                throw new ArgumentException("ScopeId cannot be null or empty", nameof(scopeId));
            return scopeId;
        }
    }
}
