// DhcpManager.Query.cs
using System;
using System.Data;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace DhcpWmiViewer
{
    public static partial class DhcpManager
    {
        public static Task<DataTable> QueryScopesAsync(string server, Func<string, PSCredential?>? getCredentials)
        {
            return PowerShellExecutor.ExecutePowerShellQueryAsync(server, getCredentials, ps =>
            {
                ps.AddCommand("Get-DhcpServerv4Scope");
                var properties = new[] { "Name", "ScopeId", "StartRange", "EndRange", "SubnetMask", "State", "Description" };
                if (properties == null || properties.Any(p => string.IsNullOrWhiteSpace(p)))
                    throw new ArgumentException("Properties array contains null or empty values");
                ps.AddCommand("Select-Object").SafeAddParameter("Property", properties);
            }, dt =>
            {
                dt.Columns.Add("Name", typeof(string));
                dt.Columns.Add("ScopeId", typeof(string));
                dt.Columns.Add("StartRange", typeof(string));
                dt.Columns.Add("EndRange", typeof(string));
                dt.Columns.Add("SubnetMask", typeof(string));
                dt.Columns.Add("State", typeof(string));
                dt.Columns.Add("Description", typeof(string));
            });
        }

        public static Task<DataTable> QueryReservationsAsync(string server, string scopeId, Func<string, PSCredential?>? getCredentials)
        {
            if (string.IsNullOrWhiteSpace(scopeId))
                throw new ArgumentException("ScopeId cannot be null or empty", nameof(scopeId));
                
            return PowerShellExecutor.ExecutePowerShellQueryAsync(server, getCredentials, ps =>
            {
                ps.AddCommand("Get-DhcpServerv4Reservation").SafeAddParameter("ScopeId", scopeId);
                ps.AddCommand("Select-Object").SafeAddParameter("Property", new[] { "IPAddress", "ClientId", "Name", "Description", "AddressState" });
            }, dt =>
            {
                dt.Columns.Add("IPAddress", typeof(string));
                dt.Columns.Add("ClientId", typeof(string));
                dt.Columns.Add("Name", typeof(string));
                dt.Columns.Add("Description", typeof(string));
                dt.Columns.Add("AddressState", typeof(string));
            });
        }

        public static Task<DataTable> QueryLeasesAsync(string server, string scopeId, Func<string, PSCredential?>? getCredentials, int? limit = null)
        {
            if (string.IsNullOrWhiteSpace(scopeId))
                throw new ArgumentException("ScopeId cannot be null or empty", nameof(scopeId));
                
            return PowerShellExecutor.ExecutePowerShellQueryAsync(server, getCredentials, ps =>
            {
                ps.AddCommand("Get-DhcpServerv4Lease").SafeAddParameter("ScopeId", scopeId);
                ps.AddCommand("Select-Object").SafeAddParameter("Property", new[] { 
                    "IPAddress", "ClientId", "ClientType", "HostName", "Description", 
                    "AddressState", "LeaseExpiryTime", "ScopeId", "ServerIP", "PSComputerName",
                    "CimClass", "CimInstanceProperties", "CimSystemProperties"
                });
                if (limit.HasValue)
                {
                    ps.AddCommand("Select-Object").SafeAddParameter("First", limit.Value);
                }
            }, dt =>
            {
                dt.Columns.Add("IPAddress", typeof(string));
                dt.Columns.Add("ClientId", typeof(string));
                dt.Columns.Add("ClientType", typeof(string));
                dt.Columns.Add("HostName", typeof(string));
                dt.Columns.Add("Description", typeof(string));
                dt.Columns.Add("AddressState", typeof(string));
                dt.Columns.Add("LeaseExpiryTime", typeof(string));
                dt.Columns.Add("ScopeId", typeof(string));
                dt.Columns.Add("ServerIP", typeof(string));
                dt.Columns.Add("PSComputerName", typeof(string));
                dt.Columns.Add("CimClass", typeof(string));
                dt.Columns.Add("CimInstanceProperties", typeof(string));
                dt.Columns.Add("CimSystemProperties", typeof(string));
            }, isDynamic: false);
        }

        /// <summary>
        /// Query all leases for a scope without the First 5 limitation
        /// </summary>
        public static Task<DataTable> QueryLeasesAsyncUnlimited(string server, string scopeId, Func<string, PSCredential?>? getCredentials)
        {
            return PowerShellExecutor.ExecutePowerShellQueryAsync(server, getCredentials, ps =>
            {
                ps.AddCommand("Get-DhcpServerv4Lease").SafeAddParameter("ScopeId", scopeId);
                ps.AddCommand("Select-Object").SafeAddParameter("Property", new[] { 
                    "IPAddress", "ClientId", "ClientType", "HostName", "Description", 
                    "AddressState", "LeaseExpiryTime", "ScopeId", "ServerIP", "PSComputerName",
                    "CimClass", "CimInstanceProperties", "CimSystemProperties"
                });
            }, dt =>
            {
                dt.Columns.Add("IPAddress", typeof(string));
                dt.Columns.Add("ClientId", typeof(string));
                dt.Columns.Add("ClientType", typeof(string));
                dt.Columns.Add("HostName", typeof(string));
                dt.Columns.Add("Description", typeof(string));
                dt.Columns.Add("AddressState", typeof(string));
                dt.Columns.Add("LeaseExpiryTime", typeof(string));
                dt.Columns.Add("ScopeId", typeof(string));
                dt.Columns.Add("ServerIP", typeof(string));
                dt.Columns.Add("PSComputerName", typeof(string));
                dt.Columns.Add("CimClass", typeof(string));
                dt.Columns.Add("CimInstanceProperties", typeof(string));
                dt.Columns.Add("CimSystemProperties", typeof(string));
            }, isDynamic: false);
        }

        /// <summary>
        /// Very fast query - only essential data for maximum speed
        /// </summary>
        public static Task<DataTable> QueryLeasesFastAsync(string server, string scopeId, Func<string, PSCredential?>? getCredentials, int? limit = null)
        {
            return PowerShellExecutor.ExecutePowerShellQueryAsync(server, getCredentials, ps =>
            {
                ps.AddCommand("Get-DhcpServerv4Lease").SafeAddParameter("ScopeId", scopeId);
                ps.AddCommand("Select-Object").SafeAddParameter("Property", new[] { 
                    "IPAddress", "ClientId", "HostName", "AddressState"
                });
                if (limit.HasValue)
                {
                    ps.AddCommand("Select-Object").SafeAddParameter("First", limit.Value);
                }
            }, dt =>
            {
                // Only essential columns
                dt.Columns.Add("IPAddress", typeof(string));
                dt.Columns.Add("ClientId", typeof(string));
                dt.Columns.Add("HostName", typeof(string));
                dt.Columns.Add("AddressState", typeof(string));
                // Add empty columns for compatibility
                dt.Columns.Add("ClientType", typeof(string));
                dt.Columns.Add("Description", typeof(string));
                dt.Columns.Add("LeaseExpiryTime", typeof(string));
                dt.Columns.Add("ScopeId", typeof(string));
                dt.Columns.Add("ServerIP", typeof(string));
                dt.Columns.Add("PSComputerName", typeof(string));
                dt.Columns.Add("CimClass", typeof(string));
                dt.Columns.Add("CimInstanceProperties", typeof(string));
                dt.Columns.Add("CimSystemProperties", typeof(string));
            }, isDynamic: false);
        }
    }
}
