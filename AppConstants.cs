// AppConstants.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace DhcpWmiViewer
{
    /// <summary>
    /// Zentrale, projektweite Konstanten und einfache Environment-Initialisierung.
    /// </summary>
    public static class AppConstants
    {
        // EventLog / Source Namen (einheitlich im ganzen Projekt verwenden)
        public const string EventSourceName = "DhcpWmiViewer";
        public const string EventLogName = "Application";

        /// <summary>
        /// App-weite Flag, ob die Anwendung auf einem DHCP-Server läuft.
        /// Default = false; kann beim Programmstart per InitializeRunningEnvironment gesetzt
        /// oder später von der Discovery-Logik aktualisiert werden.
        /// </summary>
        public static bool RunningOnDhcpServer { get; set; } = false;

        // --- Internet-erlaubte IP-Bereiche (Firewall-Bereiche) ---
        /// <summary>
        /// Start-IP des ersten erlaubten Bereichs (inklusive).
        /// </summary>
        public static readonly IPAddress InternetAllowedRange1Start = IPAddress.Parse("192.168.116.180");

        /// <summary>
        /// End-IP des ersten erlaubten Bereichs (inklusive).
        /// </summary>
        public static readonly IPAddress InternetAllowedRange1End = IPAddress.Parse("192.168.116.254");

        /// <summary>
        /// Start-IP des zweiten erlaubten Bereichs (inklusive).
        /// </summary>
        public static readonly IPAddress InternetAllowedRange2Start = IPAddress.Parse("192.168.116.4");

        /// <summary>
        /// End-IP des zweiten erlaubten Bereichs (inklusive).
        /// </summary>
        public static readonly IPAddress InternetAllowedRange2End = IPAddress.Parse("192.168.116.48");

        // Rückwärtskompatibilität - primärer Bereich
        /// <summary>
        /// Start-IP des erlaubten Bereichs (inklusive) - für Rückwärtskompatibilität.
        /// </summary>
        public static readonly IPAddress InternetAllowedRangeStart = InternetAllowedRange1Start;

        /// <summary>
        /// End-IP des erlaubten Bereichs (inklusive) - für Rückwärtskompatibilität.
        /// </summary>
        public static readonly IPAddress InternetAllowedRangeEnd = InternetAllowedRange1End;

        /// <summary>
        /// String-Repräsentation aller Firewall-Bereiche.
        /// </summary>
        public static string InternetAllowedRangeString => 
            $"{InternetAllowedRange2Start}-{InternetAllowedRange2End} und {InternetAllowedRange1Start}-{InternetAllowedRange1End}";

        /// <summary>
        /// String-Repräsentation der ursprünglichen beiden Bereiche (für Rückwärtskompatibilität).
        /// </summary>
        public static string InternetAllowedRangeStringLegacy => $"{InternetAllowedRange2Start}-{InternetAllowedRange2End} und {InternetAllowedRange1Start}-{InternetAllowedRange1End}";

        /// <summary>
        /// String-Repräsentation des primären Bereichs für Rückwärtskompatibilität.
        /// </summary>
        public static string InternetAllowedRangePrimaryString => $"{InternetAllowedRangeStart}-{InternetAllowedRangeEnd}";

        /// <summary>
        /// Prüft, ob die angegebene IP (als string) innerhalb des erlaubten Internet-Bereichs liegt.
        /// Liefert false bei ungültiger IP oder Nicht-IPv4.
        /// </summary>
        public static bool IsIpAllowedForInternet(string ipString)
        {
            if (!IPAddress.TryParse(ipString, out var ip))
                return false;
            return IsIpAllowedForInternet(ip);
        }

        /// <summary>
        /// Prüft, ob die angegebene IP innerhalb eines der erlaubten Internet-Bereiche liegt.
        /// </summary>
        public static bool IsIpAllowedForInternet(IPAddress ip)
        {
            if (ip == null) return false;
            if (ip.AddressFamily != AddressFamily.InterNetwork) return false; // nur IPv4

            uint val = ToUInt32(ip);

            // Prüfe Bereich 1: 192.168.116.180-192.168.116.254
            uint start1 = ToUInt32(InternetAllowedRange1Start);
            uint end1 = ToUInt32(InternetAllowedRange1End);
            
            // defensiver Tausch falls die Grenzen versehentlich vertauscht wurden
            if (start1 > end1)
            {
                var t = start1;
                start1 = end1;
                end1 = t;
            }

            if (val >= start1 && val <= end1)
                return true;

            // Prüfe Bereich 2: 192.168.116.4-192.168.116.48
            uint start2 = ToUInt32(InternetAllowedRange2Start);
            uint end2 = ToUInt32(InternetAllowedRange2End);
            
            // defensiver Tausch falls die Grenzen versehentlich vertauscht wurden
            if (start2 > end2)
            {
                var t = start2;
                start2 = end2;
                end2 = t;
            }

            return val >= start2 && val <= end2;
        }

        private static uint ToUInt32(IPAddress ip)
        {
            var bytes = ip.GetAddressBytes();
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes); // Big-endian (network order) erwarten für Vergleich
            return BitConverter.ToUInt32(bytes, 0);
        }

        /// <summary>
        /// Ermittelt den kombinierten Firewall-Bereich (niedrigste Start-IP bis höchste End-IP).
        /// </summary>
        /// <returns>Tuple mit Start- und End-IP des kombinierten Bereichs</returns>
        public static (string Start, string End) GetCombinedFirewallRange()
        {
            var allStarts = new[]
            {
                ToUInt32(InternetAllowedRange1Start),
                ToUInt32(InternetAllowedRange2Start)
            };

            var allEnds = new[]
            {
                ToUInt32(InternetAllowedRange1End),
                ToUInt32(InternetAllowedRange2End)
            };

            var minStart = allStarts.Min();
            var maxEnd = allEnds.Max();

            return (UInt32ToIPString(minStart), UInt32ToIPString(maxEnd));
        }

        /// <summary>
        /// Ermittelt alle verfügbaren IPs in den Firewall-Bereichen.
        /// </summary>
        /// <param name="excludeIps">IPs die ausgeschlossen werden sollen (bereits reserviert/vergeben)</param>
        /// <returns>Liste der verfügbaren IPs</returns>
        public static List<string> GetAvailableFirewallIps(HashSet<string>? excludeIps = null)
        {
            excludeIps ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var availableIps = new List<string>();

            // Alle Firewall-Bereiche durchgehen
            var ranges = new[]
            {
                (InternetAllowedRange1Start, InternetAllowedRange1End),
                (InternetAllowedRange2Start, InternetAllowedRange2End)
            };

            foreach (var (start, end) in ranges)
            {
                var startIp = ToUInt32(start);
                var endIp = ToUInt32(end);

                if (startIp == 0 || endIp == 0)
                    continue;

                // Sicherstellen, dass Start <= End
                if (startIp > endIp)
                {
                    var temp = startIp;
                    startIp = endIp;
                    endIp = temp;
                }

                for (uint ip = startIp; ip <= endIp; ip++)
                {
                    var ipString = UInt32ToIPString(ip);
                    if (!excludeIps.Contains(ipString))
                        availableIps.Add(ipString);
                }
            }

            return availableIps;
        }

        /// <summary>
        /// Konvertiert UInt32 zu IP-String.
        /// </summary>
        private static string UInt32ToIPString(uint ip)
        {
            try
            {
                var bytes = BitConverter.GetBytes(ip);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(bytes);
                return new IPAddress(bytes).ToString();
            }
            catch
            {
                return "0.0.0.0";
            }
        }

        /// <summary>
        /// Optionale, leichte Initialisierung: kurzer Service-Check (best-effort).
        /// Setzt RunningOnDhcpServer wenn ein DHCP-Serverdienst lokal vorhanden und running ist.
        /// Fehler werden geschluckt; die Entdeckung per AD/Discover sollte zusätzlich entscheiden.
        /// </summary>
        public static void InitializeRunningEnvironment()
        {
            try
            {
                // Minimaler best-effort check: delegiere an DhcpDiscovery (falls verfügbar)
                // DhcpDiscovery.TryCheckLocalDhcpService ist robust gegen Fehler.
                try
                {
                    RunningOnDhcpServer = DhcpDiscovery.CheckLocalDhcpServiceRunning();
                }
                catch
                {
                    RunningOnDhcpServer = false;
                }
            }
            catch
            {
                RunningOnDhcpServer = false;
            }
        }
    }
}
