// DhcpDiscovery.cs
using System.DirectoryServices;

namespace DhcpWmiViewer
{
    /// <summary>
    /// Discovery-Utilities: discover DHCP servers in AD und einfache local-host checks.
    /// </summary>
    public static class DhcpDiscovery
    {
        /// <summary>
        /// Ermittelt DHCP-Server aus Active Directory (best-effort, Fehler werden ignoriert).
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

        /// <summary>Ergebnis der Prüfung eines gefundenen Servers.</summary>
        public sealed record ServerCheck(string Server, bool Usable, string Reason);

        /// <summary>
        /// Prüft die im AD autorisierten Server, bevor sie angezeigt werden (parallel, mit Zeitlimit):
        /// 1. Computerkonto im AD vorhanden und nicht deaktiviert (wenn das AD abfragbar ist)
        /// 2. Name per DNS auflösbar und WinRM-Port 5985 erreichbar
        /// Autorisierungen abgeschalteter Server bleiben im AD sonst oft jahrelang stehen.
        /// </summary>
        public static async Task<List<ServerCheck>> CheckServersAsync(IEnumerable<string> servers, int timeoutMs = 2500)
        {
            var checks = servers.Select(async server =>
            {
                var adState = await Task.Run(() => GetComputerAccountState(server));
                if (adState == ComputerAccountState.Missing)
                    return new ServerCheck(server, false, Loc.T("kein Computerkonto im AD", "no computer account in AD"));
                if (adState == ComputerAccountState.Disabled)
                    return new ServerCheck(server, false, Loc.T("Computerkonto deaktiviert", "computer account disabled"));

                return await IsWinRmReachableAsync(server, timeoutMs)
                    ? new ServerCheck(server, true, "ok")
                    : new ServerCheck(server, false, Loc.T("nicht erreichbar", "unreachable"));
            });
            return (await Task.WhenAll(checks)).ToList();
        }

        private enum ComputerAccountState { Unknown, Active, Disabled, Missing }

        private static ComputerAccountState GetComputerAccountState(string server)
        {
            try
            {
                using var rootDse = new DirectoryEntry("LDAP://RootDSE");
                var defaultNc = rootDse.Properties["defaultNamingContext"].Value?.ToString();
                if (string.IsNullOrEmpty(defaultNc)) return ComputerAccountState.Unknown;

                var name = server.Trim();
                var shortName = name.Split('.')[0];
                using var root = new DirectoryEntry($"LDAP://{defaultNc}");
                using var searcher = new DirectorySearcher(root)
                {
                    Filter = $"(&(objectCategory=computer)(|(dNSHostName={EscapeLdap(name)})(cn={EscapeLdap(shortName)})))",
                    ClientTimeout = TimeSpan.FromSeconds(5)
                };
                searcher.PropertiesToLoad.Add("userAccountControl");

                var result = searcher.FindOne();
                if (result == null) return ComputerAccountState.Missing;

                const int accountDisable = 0x2;
                if (result.Properties["userAccountControl"].Count > 0 && result.Properties["userAccountControl"][0] is int uac)
                    return (uac & accountDisable) != 0 ? ComputerAccountState.Disabled : ComputerAccountState.Active;
                return ComputerAccountState.Active;
            }
            catch
            {
                // AD nicht abfragbar -> nur Erreichbarkeit entscheidet
                return ComputerAccountState.Unknown;
            }
        }

        private static async Task<bool> IsWinRmReachableAsync(string server, int timeoutMs)
        {
            try
            {
                using var cts = new CancellationTokenSource(timeoutMs);
                using var client = new System.Net.Sockets.TcpClient();
                await client.ConnectAsync(server.Trim(), 5985, cts.Token);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string EscapeLdap(string value)
            => value.Replace("\\", "\\5c").Replace("*", "\\2a").Replace("(", "\\28").Replace(")", "\\29").Replace("\0", "\\00");

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
