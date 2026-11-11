// NetworkHelper.cs
using System.Net;
using System.Net.Sockets;

namespace DhcpWmiViewer
{
    /// <summary>
    /// IPv4-Hilfsfunktionen: Konvertierung, Bereichsprüfung, Subnetz-Vergleich.
    /// Subnetzmasken als "255.255.255.0" oder Prefix ("24" bzw. "/24").
    /// </summary>
    public static class NetworkHelper
    {
        /// <summary>IPv4-Adresse als uint (Netzwerk-Reihenfolge, damit Vergleiche funktionieren).</summary>
        public static bool TryIpToUInt32(string? ip, out uint value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(ip) || !IPAddress.TryParse(ip.Trim(), out var addr) || addr.AddressFamily != AddressFamily.InterNetwork)
                return false;

            value = ToUInt32(addr);
            return true;
        }

        public static uint ToUInt32(IPAddress address)
        {
            var b = address.GetAddressBytes();
            return (uint)(b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]);
        }

        public static string UInt32ToIp(uint value)
            => new IPAddress(new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value }).ToString();

        /// <summary>Liegt ip im geschlossenen Intervall [start, end]? (false bei ungültigen Werten)</summary>
        public static bool IsIpInRange(string ip, string start, string end)
            => TryIpToUInt32(ip, out var v) && TryIpToUInt32(start, out var s) && TryIpToUInt32(end, out var e) && v >= s && v <= e;

        /// <summary>Liegen beide Adressen im selben Subnetz? (false bei ungültigen Werten)</summary>
        public static bool IsInSameSubnet(string ip, string otherIp, string subnetMask)
            => TryIpToUInt32(ip, out var a) && TryIpToUInt32(otherIp, out var b) && TryParseMask(subnetMask, out var mask)
               && (a & mask) == (b & mask);

        private static bool TryParseMask(string? mask, out uint value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(mask)) return false;

            var s = mask.Trim();
            if (s.Contains('.'))
                return TryIpToUInt32(s, out value);

            if (!int.TryParse(s.TrimStart('/'), out var prefix) || prefix < 0 || prefix > 32)
                return false;

            value = prefix == 0 ? 0u : 0xFFFFFFFFu << (32 - prefix);
            return true;
        }
    }
}
