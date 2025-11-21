// ComputerOnlineChecker.cs
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace DhcpWmiViewer
{
    /// <summary>
    /// Prüft den Online-Status von Computern via Ping
    /// </summary>
    public static class ComputerOnlineChecker
    {
        // Cache für Online-Status (Computer-Name -> Online-Status)
        private static readonly ConcurrentDictionary<string, OnlineStatus> _onlineCache = new();
        
        // Timeout für Ping-Requests
        private const int PING_TIMEOUT_MS = 1000; // 1 Sekunde
        
        /// <summary>
        /// Online-Status eines Computers
        /// </summary>
        public class OnlineStatus
        {
            public bool IsOnline { get; set; }
            public DateTime LastChecked { get; set; }
            public bool IsChecking { get; set; }
            
            public OnlineStatus(bool isOnline = false)
            {
                IsOnline = isOnline;
                LastChecked = DateTime.Now;
                IsChecking = false;
            }
            
            /// <summary>
            /// Ist der Cache-Eintrag noch gültig? (5 Minuten)
            /// </summary>
            public bool IsValid => DateTime.Now - LastChecked < TimeSpan.FromMinutes(5);
        }
        
        /// <summary>
        /// Prüft den Online-Status eines Computers (mit Caching)
        /// </summary>
        public static OnlineStatus GetOnlineStatus(string computerName)
        {
            if (string.IsNullOrEmpty(computerName))
                return new OnlineStatus(false);
                
            // Aus Cache holen
            if (_onlineCache.TryGetValue(computerName, out var cachedStatus))
            {
                // Wenn Cache gültig ist oder Check bereits läuft, zurückgeben
                if (cachedStatus.IsValid || cachedStatus.IsChecking)
                    return cachedStatus;
            }
            
            // Neuen Status erstellen oder abgelaufenen aktualisieren
            var status = cachedStatus ?? new OnlineStatus(false);
            status.IsChecking = true;
            status.LastChecked = DateTime.Now; // Update timestamp sofort
            _onlineCache[computerName] = status;
            
            // Async Ping starten (Fire & Forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    var isOnline = await PingComputerAsync(computerName);
                    status.IsOnline = isOnline;
                    status.LastChecked = DateTime.Now;
                    status.IsChecking = false;
                    
                    DebugLogger.LogFormat("Online check for {0}: {1} (cached until {2})", 
                        computerName, 
                        isOnline ? "ONLINE" : "OFFLINE", 
                        status.LastChecked.AddMinutes(5).ToString("HH:mm:ss"));
                }
                catch (Exception ex)
                {
                    status.IsOnline = false;
                    status.LastChecked = DateTime.Now;
                    status.IsChecking = false;
                    DebugLogger.LogFormat("Online check error for {0}: {1}", computerName, ex.Message);
                }
            });
            
            return status;
        }

        /// <summary>
        /// Forciert einen neuen Online-Check (ignoriert Cache)
        /// </summary>
        public static OnlineStatus ForceRefreshOnlineStatus(string computerName)
        {
            if (string.IsNullOrEmpty(computerName))
                return new OnlineStatus(false);
                
            // Cache-Eintrag als ungültig markieren
            if (_onlineCache.TryGetValue(computerName, out var cachedStatus))
            {
                cachedStatus.LastChecked = DateTime.MinValue; // Force refresh
            }
            
            return GetOnlineStatus(computerName);
        }

        /// <summary>
        /// Pingt einen Computer an (mit FQDN-Fallback)
        /// </summary>
        private static async Task<bool> PingComputerAsync(string computerName)
        {
            using var ping = new Ping();
            
            try
            {
                // Erst Computer-Namen direkt versuchen
                var reply = await ping.SendPingAsync(computerName, PING_TIMEOUT_MS);
                if (reply.Status == IPStatus.Success)
                    return true;
                    
                // Fallback: Mit Domain-Suffix versuchen (falls verfügbar)
                var domainSuffix = GetDomainSuffix();
                if (!string.IsNullOrEmpty(domainSuffix) && !computerName.Contains("."))
                {
                    var fqdnName = $"{computerName}.{domainSuffix}";
                    var fqdnReply = await ping.SendPingAsync(fqdnName, PING_TIMEOUT_MS);
                    if (fqdnReply.Status == IPStatus.Success)
                        return true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Ermittelt das Domain-Suffix aus der aktuellen Umgebung
        /// </summary>
        private static string GetDomainSuffix()
        {
            try
            {
                // Methode 1: Versuche über System.DirectoryServices das DNS-Domain-Suffix zu ermitteln
                try
                {
                    using var root = new System.DirectoryServices.DirectoryEntry("LDAP://RootDSE");
                    var defaultNC = root.Properties["defaultNamingContext"].Value?.ToString();
                    if (!string.IsNullOrEmpty(defaultNC))
                    {
                        // Konvertiere "DC=example,DC=com" zu "example.com"
                        var parts = defaultNC.Split(',')
                                            .Where(part => part.Trim().StartsWith("DC=", StringComparison.OrdinalIgnoreCase))
                                            .Select(part => part.Trim().Substring(3))
                                            .ToArray();
                        if (parts.Length > 0)
                        {
                            return string.Join(".", parts);
                        }
                    }
                }
                catch { /* Fallback to next method */ }

                // Methode 2: Versuche über DNS-Suche
                try
                {
                    var hostName = System.Net.Dns.GetHostName();
                    var hostEntry = System.Net.Dns.GetHostEntry(hostName);
                    var fqdn = hostEntry.HostName;
                    
                    if (!string.IsNullOrEmpty(fqdn) && fqdn.Contains("."))
                    {
                        var domainPart = fqdn.Substring(fqdn.IndexOf('.') + 1);
                        if (!string.IsNullOrEmpty(domainPart))
                        {
                            return domainPart;
                        }
                    }
                }
                catch { /* Fallback to next method */ }

                // Methode 3: Fallback über Environment (ohne hardcoded .de)
                var domain = Environment.UserDomainName;
                if (!string.IsNullOrEmpty(domain) && domain != Environment.MachineName)
                {
                    // Versuche herauszufinden, ob es bereits ein DNS-Name ist
                    if (domain.Contains("."))
                    {
                        return domain.ToLower();
                    }
                    
                    // Letzter Fallback: Probiere .local (typisch für lokale Domains)
                    return domain.ToLower() + ".local";
                }
                
                return "";
            }
            catch
            {
                return "";
            }
        }
        
        /// <summary>
        /// Löscht den Cache (für Refresh)
        /// </summary>
        public static void ClearCache()
        {
            _onlineCache.Clear();
            DebugLogger.Log("Online status cache cleared");
        }
        
        /// <summary>
        /// Gibt Cache-Statistiken zurück
        /// </summary>
        public static string GetCacheStats()
        {
            var total = _onlineCache.Count;
            var online = 0;
            var checking = 0;
            
            foreach (var status in _onlineCache.Values)
            {
                if (status.IsChecking) checking++;
                else if (status.IsOnline) online++;
            }
            
            return $"Cache: {total} entries, {online} online, {checking} checking";
        }
    }
}