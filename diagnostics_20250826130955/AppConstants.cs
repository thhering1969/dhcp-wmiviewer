// AppConstants.cs
using System;
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

        // --- Internet-erlaubter IP-Bereich ---
        /// <summary>
        /// Start-IP des erlaubten Bereichs (inklusive).
        /// </summary>
        public static readonly IPAddress InternetAllowedRangeStart = IPAddress.Parse("192.168.116.180");

        /// <summary>
        /// End-IP des erlaubten Bereichs (inklusive).
        /// <-- Hier angepasst: 192.168.116.254 -->
        /// </summary>
        public static readonly IPAddress InternetAllowedRangeEnd = IPAddress.Parse("192.168.116.254");

        /// <summary>
        /// String-Repräsentation des Bereichs, z.B. "192.168.116.180-192.168.116.254".
        /// </summary>
        public static string InternetAllowedRangeString => $"{InternetAllowedRangeStart}-{InternetAllowedRangeEnd}";

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
        /// Prüft, ob die angegebene IP innerhalb des erlaubten Internet-Bereichs liegt.
        /// </summary>
        public static bool IsIpAllowedForInternet(IPAddress ip)
        {
            if (ip == null) return false;
            if (ip.AddressFamily != AddressFamily.InterNetwork) return false; // nur IPv4

            uint start = ToUInt32(InternetAllowedRangeStart);
            uint end = ToUInt32(InternetAllowedRangeEnd);
            uint val = ToUInt32(ip);

            // defensiver Tausch falls die Grenzen versehentlich vertauscht wurden
            if (start > end)
            {
                var t = start;
                start = end;
                end = t;
            }

            return val >= start && val <= end;
        }

        private static uint ToUInt32(IPAddress ip)
        {
            var bytes = ip.GetAddressBytes();
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes); // Big-endian (network order) erwarten für Vergleich
            return BitConverter.ToUInt32(bytes, 0);
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
