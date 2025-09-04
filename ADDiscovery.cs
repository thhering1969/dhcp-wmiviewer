// ADDiscovery.cs
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.ServiceProcess;

namespace DhcpWmiViewer
{
    /// <summary>
    /// Discovery-Utilities für Active Directory Domain Controller.
    /// Analog zu DhcpDiscovery, aber für DCs.
    /// </summary>
    public static class ADDiscovery
    {
        /// <summary>
        /// Ermittelt Domain Controller in der aktuellen Domäne.
        /// </summary>
        public static List<string> DiscoverDomainControllersInAD()
        {
            var resultsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            try
            {
                // Methode 1: Über RootDSE und Configuration Naming Context
                using var rootDse = new DirectoryEntry("LDAP://RootDSE");
                var configNamingContext = rootDse.Properties["configurationNamingContext"].Value?.ToString();
                if (!string.IsNullOrEmpty(configNamingContext))
                {
                    var sitesPath = $"LDAP://CN=Sites,{configNamingContext}";
                    using var sites = new DirectoryEntry(sitesPath);
                    using var searcher = new DirectorySearcher(sites)
                    {
                        Filter = "(&(objectClass=server)(serverReference=*))",
                        PageSize = 1000
                    };
                    searcher.PropertiesToLoad.AddRange(new[] { "cn", "dNSHostName", "serverReference" });
                    
                    var found = searcher.FindAll();
                    foreach (SearchResult sr in found)
                    {
                        // Prüfe zuerst dNSHostName (bevorzugt)
                        if (sr.Properties.Contains("dNSHostName") && sr.Properties["dNSHostName"].Count > 0)
                        {
                            var dns = sr.Properties["dNSHostName"][0]?.ToString();
                            if (!string.IsNullOrEmpty(dns)) 
                                resultsSet.Add(dns.Trim());
                        }
                        // Fallback auf cn (Server-Name)
                        else if (sr.Properties.Contains("cn") && sr.Properties["cn"].Count > 0)
                        {
                            var cn = sr.Properties["cn"][0]?.ToString();
                            if (!string.IsNullOrEmpty(cn)) 
                                resultsSet.Add(cn.Trim());
                        }
                    }
                }
            }
            catch { /* swallow */ }

            // Methode 2: Fallback - Suche nach Computer-Objekten mit DC-Rolle
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
                            Filter = "(&(objectCategory=computer)(userAccountControl:1.2.840.113556.1.4.803:=8192))", // SERVER_TRUST_ACCOUNT
                            PageSize = 1000
                        };
                        ds2.PropertiesToLoad.AddRange(new[] { "cn", "dNSHostName" });
                        
                        var found2 = ds2.FindAll();
                        foreach (SearchResult sr in found2)
                        {
                            if (sr.Properties.Contains("dNSHostName") && sr.Properties["dNSHostName"].Count > 0)
                            {
                                var s = sr.Properties["dNSHostName"][0]?.ToString();
                                if (!string.IsNullOrEmpty(s)) 
                                    resultsSet.Add(s.Trim());
                            }
                            else if (sr.Properties.Contains("cn") && sr.Properties["cn"].Count > 0)
                            {
                                var cn = sr.Properties["cn"][0]?.ToString();
                                if (!string.IsNullOrEmpty(cn) && cn.EndsWith("$"))
                                {
                                    // Entferne das $ am Ende für Computer-Accounts
                                    resultsSet.Add(cn.Substring(0, cn.Length - 1).Trim());
                                }
                            }
                        }
                    }
                }
                catch { /* swallow */ }
            }

            return resultsSet.ToList();
        }

        /// <summary>
        /// Prüft, ob die lokale Maschine ein Domain Controller ist.
        /// </summary>
        public static bool CheckLocalDomainControllerServiceRunning()
        {
            try
            {
                var services = ServiceController.GetServices();
                foreach (var s in services)
                {
                    // NTDS (Active Directory Domain Services) ist der Hauptdienst für DCs
                    if (string.Equals(s.ServiceName, "NTDS", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(s.ServiceName, "ActiveDirectoryDomainServices", StringComparison.OrdinalIgnoreCase)
                        || (!string.IsNullOrEmpty(s.DisplayName) && 
                            (s.DisplayName.IndexOf("Active Directory Domain Services", StringComparison.OrdinalIgnoreCase) >= 0
                             || s.DisplayName.IndexOf("NTDS", StringComparison.OrdinalIgnoreCase) >= 0)))
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
        /// Prüft, ob der lokale Host (kurzname oder FQDN) in der entdeckten DC-Liste enthalten ist.
        /// </summary>
        public static bool LocalHostAppearsInDiscovery(IEnumerable<string> discoveredDCs)
        {
            if (discoveredDCs == null) return false;
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
            foreach (var dc in discoveredDCs)
            {
                if (string.IsNullOrEmpty(dc)) continue;
                if (Normalize(dc) == localNorm) return true;
            }
            return false;
        }

        /// <summary>
        /// Ermittelt alle OUs, die Computerobjekte enthalten.
        /// </summary>
        public static List<ADOrganizationalUnit> GetOUsWithComputers(string domainController = "")
        {
            var results = new List<ADOrganizationalUnit>();
            
            try
            {
                string ldapPath = string.IsNullOrEmpty(domainController) ? "LDAP://RootDSE" : $"LDAP://{domainController}/RootDSE";
                using var rootDse = new DirectoryEntry(ldapPath);
                var defaultNC = rootDse.Properties["defaultNamingContext"].Value?.ToString();
                
                if (!string.IsNullOrEmpty(defaultNC))
                {
                    string domainPath = string.IsNullOrEmpty(domainController) ? $"LDAP://{defaultNC}" : $"LDAP://{domainController}/{defaultNC}";
                    using var domain = new DirectoryEntry(domainPath);
                    
                    // Suche alle OUs
                    using var ouSearcher = new DirectorySearcher(domain)
                    {
                        Filter = "(objectClass=organizationalUnit)",
                        PageSize = 1000,
                        SearchScope = SearchScope.Subtree
                    };
                    ouSearcher.PropertiesToLoad.AddRange(new[] { "distinguishedName", "name", "description" });
                    
                    var ous = ouSearcher.FindAll();
                    
                    foreach (SearchResult ouResult in ous)
                    {
                        var dn = ouResult.Properties["distinguishedName"][0]?.ToString();
                        var name = ouResult.Properties["name"][0]?.ToString();
                        var description = ouResult.Properties["description"].Count > 0 ? 
                            ouResult.Properties["description"][0]?.ToString() : "";
                        
                        if (!string.IsNullOrEmpty(dn) && !string.IsNullOrEmpty(name))
                        {
                            // Prüfe, ob diese OU Computerobjekte enthält
                            if (HasComputerObjects(dn, domainController))
                            {
                                results.Add(new ADOrganizationalUnit
                                {
                                    Name = name,
                                    DistinguishedName = dn,
                                    Description = description ?? "",
                                    ComputerCount = CountComputerObjects(dn, domainController)
                                });
                            }
                        }
                    }
                }
            }
            catch { /* swallow */ }
            
            return results.OrderBy(ou => ou.DistinguishedName).ToList();
        }

        /// <summary>
        /// Prüft, ob eine OU Computerobjekte enthält.
        /// </summary>
        private static bool HasComputerObjects(string ouDN, string domainController = "")
        {
            try
            {
                string ldapPath = string.IsNullOrEmpty(domainController) ? $"LDAP://{ouDN}" : $"LDAP://{domainController}/{ouDN}";
                using var ou = new DirectoryEntry(ldapPath);
                using var searcher = new DirectorySearcher(ou)
                {
                    Filter = "(objectClass=computer)",
                    PageSize = 1,
                    SearchScope = SearchScope.OneLevel
                };
                searcher.PropertiesToLoad.Add("cn");
                
                var result = searcher.FindOne();
                return result != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Zählt die Computerobjekte in einer OU.
        /// </summary>
        private static int CountComputerObjects(string ouDN, string domainController = "")
        {
            try
            {
                string ldapPath = string.IsNullOrEmpty(domainController) ? $"LDAP://{ouDN}" : $"LDAP://{domainController}/{ouDN}";
                using var ou = new DirectoryEntry(ldapPath);
                using var searcher = new DirectorySearcher(ou)
                {
                    Filter = "(objectClass=computer)",
                    PageSize = 1000,
                    SearchScope = SearchScope.OneLevel
                };
                searcher.PropertiesToLoad.Add("cn");
                
                var results = searcher.FindAll();
                return results.Count;
            }
            catch
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// Repräsentiert eine Organizational Unit mit Computerobjekten.
    /// </summary>
    public class ADOrganizationalUnit
    {
        public string Name { get; set; } = "";
        public string DistinguishedName { get; set; } = "";
        public string Description { get; set; } = "";
        public int ComputerCount { get; set; }
    }

    /// <summary>
    /// Repräsentiert ein AD-Objekt (OU oder Computer) für die Baumansicht.
    /// </summary>
    public class ADTreeItem
    {
        public string Type { get; set; } = ""; // "OU" oder "Computer"
        public string Name { get; set; } = "";
        public string DistinguishedName { get; set; } = "";
        public string Description { get; set; } = "";
        public string ParentDN { get; set; } = "";
        public int ComputerCount { get; set; }
        public bool Enabled { get; set; } = true;
        public string OperatingSystem { get; set; } = "";
        public string LastLogonDate { get; set; } = "";

        /// <summary>
        /// Online-Status des Computers (wird lazy geladen)
        /// </summary>
        public ComputerOnlineChecker.OnlineStatus? OnlineStatus { get; set; }

        /// <summary>
        /// Prüft und cached den Online-Status
        /// </summary>
        public bool IsOnline
        {
            get
            {
                if (!IsComputer) return false;
                
                OnlineStatus = ComputerOnlineChecker.GetOnlineStatus(Name);
                return OnlineStatus.IsOnline;
            }
        }

        public bool IsOU => Type == "OU";
        public bool IsComputer => Type == "Computer";
        public bool IsContainer => Type == "Container";

        public string DisplayText
        {
            get
            {
                if (IsOU)
                {
                    var displayName = GetCleanName();
                    return ComputerCount > 0 ? $"{displayName} ({ComputerCount} computers)" : $"{displayName}";
                }
                else if (IsContainer)
                {
                    var displayName = GetCleanName();
                    return ComputerCount > 0 ? $"{displayName} ({ComputerCount} computers)" : $"{displayName}";
                }
                else if (IsComputer)
                {
                    var osInfo = !string.IsNullOrEmpty(OperatingSystem) ? $" [{OperatingSystem}]" : "";
                    return $"{Name}{osInfo}";
                }
                return Name;
            }
        }

        /// <summary>
        /// Gibt einen sauberen Anzeigenamen zurück (ohne CN= Präfix).
        /// </summary>
        public string GetCleanName()
        {
            // Entferne CN= Präfix für Container
            if (Name.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
            {
                return Name.Substring(3);
            }
            
            return Name;
        }

        public string ToolTipText
        {
            get
            {
                if (IsOU)
                {
                    return $"OU: {Name}\nDN: {DistinguishedName}\nDescription: {Description}\nComputers: {ComputerCount}";
                }
                else if (IsContainer)
                {
                    return $"Container: {Name}\nDN: {DistinguishedName}\nDescription: {Description}\nComputers: {ComputerCount}";
                }
                else if (IsComputer)
                {
                    var enabledText = Enabled ? "Enabled" : "Disabled";
                    var lastLogon = !string.IsNullOrEmpty(LastLogonDate) ? LastLogonDate : "Never";
                    var dhcpInfo = GetDhcpInfo();
                    return $"Computer: {Name}\nDN: {DistinguishedName}\nDescription: {Description}\nOS: {OperatingSystem}\nStatus: {enabledText}\nLast Logon: {lastLogon}\n{dhcpInfo}";
                }
                return $"{Type}: {Name}\nDN: {DistinguishedName}";
            }
        }

        /// <summary>
        /// Ermittelt DHCP-Informationen (Reservation/Lease) für diesen Computer
        /// </summary>
        private string GetDhcpInfo()
        {
            if (!IsComputer) return "";

            try
            {
                // Zugriff auf MainForm-Instanz über statische Referenz
                var mainFormObj = GetMainFormInstance();
                if (mainFormObj == null) return "DHCP: No data available";
                
                if (!(mainFormObj is MainForm mainForm)) return "DHCP: Invalid form reference";

                var reservationInfo = mainForm.CheckComputerReservation(Name);
                var leaseInfo = mainForm.CheckComputerLease(Name);

                var dhcpStatus = new List<string>();
                
                if (!string.IsNullOrEmpty(reservationInfo))
                {
                    dhcpStatus.Add($"Reservation: {reservationInfo}");
                }
                
                if (!string.IsNullOrEmpty(leaseInfo))
                {
                    dhcpStatus.Add($"Lease: {leaseInfo}");
                }

                if (dhcpStatus.Count == 0)
                {
                    return "DHCP: No reservation or lease found";
                }

                return string.Join("\n", dhcpStatus);
            }
            catch (Exception ex)
            {
                return $"DHCP: Error checking status - {ex.Message}";
            }
        }

        /// <summary>
        /// Statische Referenz zur MainForm-Instanz für DHCP-Abfragen
        /// </summary>
        private static WeakReference? _mainFormRef;

        public static void SetMainFormReference(object mainForm)
        {
            _mainFormRef = new WeakReference(mainForm);
        }

        private object? GetMainFormInstance()
        {
            return _mainFormRef?.Target;
        }
    }
}