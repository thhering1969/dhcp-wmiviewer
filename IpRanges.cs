// IpRanges.cs
namespace DhcpWmiViewer
{
    /// <summary>Menge von IP-Bereichen (jeweils inklusive).</summary>
    public sealed class IpRangeSet
    {
        public static readonly IpRangeSet Empty = new(Array.Empty<(uint, uint)>());

        public IReadOnlyList<(uint Start, uint End)> Ranges { get; }

        public IpRangeSet(IReadOnlyList<(uint Start, uint End)> ranges) => Ranges = ranges;

        public bool IsEmpty => Ranges.Count == 0;

        /// <summary>z. B. "10.0.0.10-10.0.0.50, 10.0.0.200-10.0.0.254"</summary>
        public string Display => string.Join(", ", Ranges.Select(r => $"{NetworkHelper.UInt32ToIp(r.Start)}-{NetworkHelper.UInt32ToIp(r.End)}"));

        public bool Contains(string ip)
            => NetworkHelper.TryIpToUInt32(ip, out var v) && Ranges.Any(r => v >= r.Start && v <= r.End);

        /// <summary>Alle Adressen der Bereiche ohne die in <paramref name="used"/> enthaltenen.</summary>
        public List<string> GetAvailable(ISet<string> used)
        {
            var available = new List<string>();
            foreach (var (start, end) in Ranges)
            {
                for (var ip = (ulong)start; ip <= end; ip++)
                {
                    var s = NetworkHelper.UInt32ToIp((uint)ip);
                    if (!used.Contains(s)) available.Add(s);
                }
            }
            return available;
        }
    }

    /// <summary>
    /// Adressraum eines Scopes: alle Host-Adressen des Subnetzes (Reservationen dürfen überall im Scope liegen)
    /// und die Teilmenge mit Internetfreigabe (Bereiche aus <see cref="AppConfig.InternetRanges"/>).
    /// </summary>
    public sealed class ScopeAddressSpace
    {
        public IpRangeSet Subnet { get; }
        public IpRangeSet Internet { get; }

        /// <summary>false = keine Internet-Bereiche konfiguriert -> Funktion ausgeblendet.</summary>
        public bool InternetConfigured { get; }

        public ScopeAddressSpace(IpRangeSet subnet, IpRangeSet internet, bool internetConfigured)
        {
            Subnet = subnet;
            Internet = internet;
            InternetConfigured = internetConfigured;
        }

        public bool IsInternet(string ip) => Internet.Contains(ip);

        /// <summary>z. B. "10.0.0.20 (mit Internetfreigabe)" für Bestätigungen; ohne konfigurierte Bereiche nur die IP.</summary>
        public string Describe(string ip)
            => string.IsNullOrWhiteSpace(ip) || !InternetConfigured ? ip : IsInternet(ip) ? Loc.T($"{ip} (mit Internetfreigabe)", $"{ip} (with internet access)") : Loc.T($"{ip} (ohne Internetfreigabe)", $"{ip} (without internet access)");
    }

    public static class IpRanges
    {
        /// <summary>
        /// Adressraum des Scopes: Subnetz (ohne Netz- und Broadcast-Adresse; ersatzweise Start-/End-Adresse)
        /// und die konfigurierten Internet-Bereiche, beschnitten auf dieses Subnetz.
        /// </summary>
        public static ScopeAddressSpace ForScope(string? scopeId, string? subnetMask, string? startRange, string? endRange)
        {
            var subnet = IpRangeSet.Empty;
            if (NetworkHelper.TryIpToUInt32(scopeId, out var net) && NetworkHelper.TryIpToUInt32(subnetMask, out var mask) && mask != 0)
            {
                var first = (net & mask) + 1;
                var last = (net & mask | ~mask) - 1;
                if (first <= last) subnet = new IpRangeSet(new[] { (first, last) });
            }
            else if (NetworkHelper.TryIpToUInt32(startRange, out var s) && NetworkHelper.TryIpToUInt32(endRange, out var e))
            {
                subnet = new IpRangeSet(new[] { (Math.Min(s, e), Math.Max(s, e)) });
            }

            var configured = AppConfig.InternetRanges;
            var firewall = configured.Select(ToUIntRange).ToList();
            var internet = subnet.IsEmpty
                ? new IpRangeSet(firewall)
                : new IpRangeSet(firewall
                    .SelectMany(f => subnet.Ranges.Select(r => (Start: Math.Max(f.Start, r.Start), End: Math.Min(f.End, r.End))))
                    .Where(r => r.Start <= r.End)
                    .ToList());

            return new ScopeAddressSpace(subnet, internet, configured.Count > 0);
        }

        private static (uint Start, uint End) ToUIntRange((string Start, string End) range)
        {
            if (!NetworkHelper.TryIpToUInt32(range.Start, out var a) || !NetworkHelper.TryIpToUInt32(range.End, out var b))
                throw new FormatException(Loc.T($"Ungültiger Internet-Bereich {range.Start}-{range.End}", $"Invalid internet range {range.Start}-{range.End}"));
            return (Math.Min(a, b), Math.Max(a, b));
        }
    }
}
