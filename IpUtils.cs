// IpUtils.cs
using System;
using System.Net;

namespace DhcpWmiViewer
{
    public static class IpUtils
    {
        public static bool TryParseToUInt(string? ipStr, out uint ip)
        {
            ip = 0;
            if (string.IsNullOrWhiteSpace(ipStr)) return false;

            if (!IPAddress.TryParse(ipStr.Trim(), out var addr))
                return false;

            var bytes = addr.GetAddressBytes();
            if (bytes.Length == 16) // IPv6-mapped IPv4?
            {
                var ipv4Bytes = new byte[4];
                Array.Copy(bytes, 12, ipv4Bytes, 0, 4);
                bytes = ipv4Bytes;
            }
            if (bytes.Length != 4) return false;

            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            ip = BitConverter.ToUInt32(bytes, 0);
            return true;
        }

        public static uint ToUInt(IPAddress addr)
        {
            var bytes = addr.GetAddressBytes();
            if (bytes.Length == 16)
            {
                var ipv4Bytes = new byte[4];
                Array.Copy(bytes, 12, ipv4Bytes, 0, 4);
                bytes = ipv4Bytes;
            }
            if (bytes.Length != 4) throw new ArgumentException("Only IPv4 supported");
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }

        public static string FromUInt(uint ip)
        {
            var bytes = BitConverter.GetBytes(ip);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return new IPAddress(bytes).ToString();
        }
    }
}
