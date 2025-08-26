// NetworkHelper.cs

using System;
using System.Collections.Generic;
using System.Net;

namespace DhcpWmiViewer
{
    /// <summary>
    /// Hilfsfunktionen für IPv4-Operationen: Konvertierung, Bereichsprüfung und Subnetz-Vergleich.
    /// Unterstützt Subnetzmasken in dotted-decimal ("255.255.255.0") oder als Prefix ("24" oder "/24").
    /// </summary>
    public static class NetworkHelper
    {
        /// <summary>
        /// Wandelt eine IPv4-Stringadresse in ein uint (netzwerkreihenfolge) um.
        /// Wirkt mit BitConverter und berücksichtigt Endianness.
        /// </summary>
        public static uint IpToUInt32(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) throw new ArgumentNullException(nameof(ip));
            var bytes = IPAddress.Parse(ip).GetAddressBytes();
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }

        /// <summary>
        /// Versucht, eine IPv4-Adresse in uint zu konvertieren; gibt false bei ungültiger Eingabe.
        /// </summary>
        public static bool TryIpToUInt32(string ip, out uint value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(ip)) return false;
            if (!IPAddress.TryParse(ip, out var addr)) return false;
            try
            {
                var bytes = addr.GetAddressBytes();
                if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
                value = BitConverter.ToUInt32(bytes, 0);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Prüft, ob <paramref name="ip"/> im geschlossenen Intervall [start,end] liegt.
        /// Erwartet gültige IPv4-Strings.
        /// </summary>
        public static bool IsIpInRange(string ip, string start, string end)
        {
            try
            {
                if (!TryIpToUInt32(ip, out var ipu)) return false;
                if (!TryIpToUInt32(start, out var su)) return false;
                if (!TryIpToUInt32(end, out var eu)) return false;
                return ipu >= su && ipu <= eu;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Wandelt eine Subnetzmaske in ein IPAddress-Objekt. Akzeptiert dotted decimal oder Prefix.
        /// Beispiele: "255.255.255.0", "24", "/24".
        /// </summary>
        public static IPAddress MaskToIPAddress(string mask)
        {
            if (string.IsNullOrWhiteSpace(mask)) throw new ArgumentNullException(nameof(mask));

            var s = mask.Trim();
            // dotted decimal
            if (s.Contains("."))
            {
                return IPAddress.Parse(s);
            }

            // prefix (mit oder ohne '/')
            if (s.StartsWith("/")) s = s.Substring(1);

            if (!int.TryParse(s, out int prefix) || prefix < 0 || prefix > 32)
                throw new FormatException("Invalid subnet mask/prefix.");

            uint maskVal = prefix == 0 ? 0u : 0xFFFFFFFFu << (32 - prefix);
            var bytes = BitConverter.GetBytes(maskVal);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return new IPAddress(bytes);
        }

        /// <summary>
        /// Prüft, ob zwei IPv4-Adressen im selben Subnetz liegen (gegeben durch subnetMask,
        /// die entweder dotted-decimal oder Prefix sein kann).
        /// </summary>
        public static bool IsInSameSubnet(string ip, string otherIp, string subnetMask)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(subnetMask)) return false;
                if (!TryIpToUInt32(ip, out var ipU)) return false;
                if (!TryIpToUInt32(otherIp, out var otherU)) return false;

                var maskAddr = MaskToIPAddress(subnetMask);
                var maskBytes = maskAddr.GetAddressBytes();
                if (BitConverter.IsLittleEndian) Array.Reverse(maskBytes);
                var maskU = BitConverter.ToUInt32(maskBytes, 0);

                return (ipU & maskU) == (otherU & maskU);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Hilfsmethode: gibt aus Prefix-Länge (z. B. 24) die dotted-decimal-Maske zurück.
        /// Liefert null bei ungültigem Eingabewert.
        /// </summary>
        public static string? PrefixToDottedDecimal(int prefix)
        {
            if (prefix < 0 || prefix > 32) return null;
            uint maskVal = prefix == 0 ? 0u : 0xFFFFFFFFu << (32 - prefix);
            var bytes = BitConverter.GetBytes(maskVal);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return new IPAddress(bytes).ToString();
        }

        /// <summary>
        /// Wandelt ein uint (netzwerkreihenfolge) in einen IPv4-String um.
        /// </summary>
        public static string UInt32ToIp(uint value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return new IPAddress(bytes).ToString();
        }

        /// <summary>
        /// Gibt eine Sequenz von IPv4-Adressen (inklusive Start und Ende) als Strings zurück.
        /// Iteriert vom kleineren zum größeren Wert; bei ungültigen Eingaben wird yield break ausgeführt.
        /// </summary>
        public static IEnumerable<string> GetIpRange(string startIp, string endIp)
        {
            if (!TryIpToUInt32(startIp, out var si)) yield break;
            if (!TryIpToUInt32(endIp, out var ei)) yield break;

            if (si > ei) { var t = si; si = ei; ei = t; }

            for (uint v = si; v <= ei; v++)
            {
                yield return UInt32ToIp(v);
                if (v == UInt32.MaxValue) yield break; // safety gegen Overflow
            }
        }
    }
}
