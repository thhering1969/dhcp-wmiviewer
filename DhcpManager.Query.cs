// DhcpManager.Query.cs
using System;
using System.Data;
using System.Management.Automation;
using System.Threading.Tasks;

namespace DhcpWmiViewer
{
    public static partial class DhcpManager
    {
        public static Task<DataTable> QueryScopesAsync(string server, Func<string, PSCredential> getCredentials)
        {
            return PowerShellExecutor.ExecutePowerShellQueryAsync(server, getCredentials, ps =>
            {
                ps.AddCommand("Get-DhcpServerv4Scope");
                ps.AddCommand("Select-Object").AddParameter("Property", new[] { "Name", "ScopeId", "StartRange", "EndRange", "SubnetMask", "State", "Description" });
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

        public static Task<DataTable> QueryReservationsAsync(string server, string scopeId, Func<string, PSCredential> getCredentials)
        {
            return PowerShellExecutor.ExecutePowerShellQueryAsync(server, getCredentials, ps =>
            {
                ps.AddCommand("Get-DhcpServerv4Reservation").AddParameter("ScopeId", scopeId);
                ps.AddCommand("Select-Object").AddParameter("Property", new[] { "IPAddress", "ClientId", "Name", "Description", "AddressState" });
            }, dt =>
            {
                dt.Columns.Add("IPAddress", typeof(string));
                dt.Columns.Add("ClientId", typeof(string));
                dt.Columns.Add("Name", typeof(string));
                dt.Columns.Add("Description", typeof(string));
                dt.Columns.Add("AddressState", typeof(string));
            });
        }

        public static Task<DataTable> QueryLeasesAsync(string server, string scopeId, Func<string, PSCredential> getCredentials)
        {
            return PowerShellExecutor.ExecutePowerShellQueryAsync(server, getCredentials, ps =>
            {
                ps.AddCommand("Get-DhcpServerv4Lease").AddParameter("ScopeId", scopeId);
            }, dt => { }, isDynamic: true);
        }
    }
}
