// DhcpDiscovery.cs
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.ServiceProcess;

namespace DhcpWmiViewer
{
    /// <summary>
    /// Discovery-Utilities: discover DHCP servers in AD und einfache local-host checks.
    /// </summary>
    public static class DhcpDiscovery
    {
        /// <summary>
        /// Discover DHCP servers in Active Directory (best-effort).
        /// Kopie der bisherigen Implementation aus MainForm.Scopes.cs.
        /// </summary>
        public static List<string> DiscoverDhcpServersInAD()
        {
            var resultsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var rootDse = new DirectoryEntry("LDAP://RootDSE");
                var configNamingContext = rootDse.Properties["configurationNamingContext"].Value?.ToString();
                if (!string.IsNullOrEmpty(configNamingContext))
                {
                    var netServicesPath = $"LDAP://CN=NetServices,CN=Services,{configNamingContext}";
                    using var netServices = new DirectoryEntry(netServicesPath);
                    using var searcher = new DirectorySearcher(netServices)
                    {
                        Filter = "(|(objectClass=dHCPClass)(serviceBindingInformation=*)(dNSHostName=*))",
                        PageSize = 1000
                    };
                    searcher.PropertiesToLoad.AddRange(new[] { "cn", "dNSHostName", "serviceBindingInformation", "objectClass" });
                    var found = searcher.FindAll();
                    foreach (SearchResult sr in found)
                    {
                        if (sr.Properties.Contains("dNSHostName") && sr.Properties["dNSHostName"].Count > 0)
                        {
                            var dns = sr.Properties["dNSHostName"][0]?.ToString();
                            if (!string.IsNullOrEmpty(dns)) resultsSet.Add(dns.Trim());
                            continue;
                        }
                        if (sr.Properties.Contains("cn") && sr.Properties["cn"].Count > 0)
                        {
                            var cn = sr.Properties["cn"][0]?.ToString();
                            if (!string.IsNullOrEmpty(cn) && !cn.Equals("DhcpRoot", StringComparison.OrdinalIgnoreCase)) resultsSet.Add(cn.Trim());
                        }
                    }
                }
            }
            catch { /* swallow */ }

            if (resultsSet.Count == 0)
            {
                try
                {
                    using var domainRoot = new DirectoryEntry("LDAP://RootDSE");
                    var defaultNC = domainRoot.Properties["defaultNamingContext"].Value?.ToString();
                    if (!string.IsNullOrEmpty(defaultNC))
                    {
                        using var root = new DirectoryEntry($"LDAP://{defaultNC}");
                        using var ds2 = new DirectorySearcher(root)
                        {
                            Filter = "(&(objectCategory=computer)(|(cn=*dhcp*)(dNSHostName=*dhcp*)))",
                            PageSize = 1000
                        };
                        ds2.PropertiesToLoad.AddRange(new[] { "cn", "dNSHostName" });
                        var found2 = ds2.FindAll();
                        foreach (SearchResult sr in found2)
                        {
                            if (sr.Properties.Contains("dNSHostName") && sr.Properties["dNSHostName"].Count > 0)
                            {
                                var s = sr.Properties["dNSHostName"][0]?.ToString();
                                if (!string.IsNullOrEmpty(s)) resultsSet.Add(s.Trim());
                            }
                        }
                    }
                }
                catch { /* swallow */ }
            }

            return resultsSet.ToList();
        }

        /// <summary>
        /// Simple check: is local machine running the DHCP server service?
        /// Achtung: DHCP-Client-Service (Dhcp) ist etwas anderes; wir suchen explizit nach DHCP Server Service.
        /// </summary>
        public static bool CheckLocalDhcpServiceRunning()
        {
            try
            {
                var services = ServiceController.GetServices();
                foreach (var s in services)
                {
                    // ServiceName "DHCPServer" ist typisch für Windows DHCP Server; DisplayName kann "DHCP Server" enthalten.
                    if (string.Equals(s.ServiceName, "DHCPServer", StringComparison.OrdinalIgnoreCase)
                        || (!string.IsNullOrEmpty(s.DisplayName) && s.DisplayName.IndexOf("DHCP Server", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        try
                        {
                            return s.Status == ServiceControllerStatus.Running;
                        }
                        catch { continue; }
                    }
                }
            }
            catch
            {
                // swallow - cannot rely on ServiceController in constrained environments
            }
            return false;
        }

        /// <summary>
        /// Prüft, ob der lokale Host (kurzname oder FQDN) in der entdeckten Serverliste enthalten ist.
        /// Normalisiert beide Seiten (hostname ohne domain).
        /// </summary>
        public static bool LocalHostAppearsInDiscovery(IEnumerable<string> discoveredServers)
        {
            if (discoveredServers == null) return false;
            var local = Environment.MachineName?.Trim() ?? "";
            if (string.IsNullOrEmpty(local)) return false;

            string Normalize(string s)
            {
                if (string.IsNullOrEmpty(s)) return "";
                s = s.Trim();
                var idx = s.IndexOf('.');
                if (idx > 0) s = s.Substring(0, idx);
                return s.ToLowerInvariant();
            }

            var localNorm = Normalize(local);
            foreach (var ds in discoveredServers)
            {
                if (string.IsNullOrEmpty(ds)) continue;
                if (Normalize(ds) == localNorm) return true;
            }
            return false;
        }
    }
}
