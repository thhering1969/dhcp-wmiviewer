// DhcpHelper.cs
using System;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;

namespace DhcpWmiViewer
{
    public static class DhcpHelper
    {
        /// <summary>
        /// Konvertiert eine IPv4-Adresse in einen 32-Bit-Integer.
        /// Bei ungültiger Adresse wird 0 zurückgegeben.
        /// </summary>
        public static uint IpToUInt32(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return 0;
            if (!IPAddress.TryParse(ip.Trim(), out var addr)) return 0;
            if (addr.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return 0;

            var bytes = addr.GetAddressBytes();
            // IPAddress.GetAddressBytes liefert Big-endian (network order) auf Windows, aber
            // um konsistente Vergleiche zu ermöglichen, konvertieren wir zu uint manuell:
            return (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
        }

        /// <summary>
        /// Prüft, ob eine IP-Adresse innerhalb eines bestimmten Bereichs liegt.
        /// Liefert false bei fehlerhaften IP-Werten.
        /// </summary>
        public static bool IsIpInRange(string ip, string start, string end)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(end))
                    return false;

                var ipu = IpToUInt32(ip);
                var su = IpToUInt32(start);
                var eu = IpToUInt32(end);

                if (ipu == 0 || su == 0 || eu == 0) return false;

                return ipu >= su && ipu <= eu;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Maskiert einen String für die Verwendung in CSV-Dateien.
        /// </summary>
        public static string EscapeCsv(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;

            if (s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
                return '"' + s.Replace("\"", "\"\"") + '"';

            return s;
        }

        /// <summary>
        /// Exportiert eine DataTable in eine CSV-Datei.
        /// </summary>
        public static void ExportDataTableToCsv(DataTable dt, string filePath)
        {
            if (dt == null) throw new ArgumentNullException(nameof(dt));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (dt.Rows.Count == 0)
                throw new ArgumentException("DataTable is empty", nameof(dt));

            var sb = new StringBuilder();

            // Header-Zeile
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(EscapeCsv(dt.Columns[i].ColumnName));
            }
            sb.AppendLine();

            // Datenzeilen
            foreach (DataRow r in dt.Rows)
            {
                for (int i = 0; i < dt.Columns.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    var val = r[i]?.ToString() ?? string.Empty;
                    sb.Append(EscapeCsv(val));
                }
                sb.AppendLine();
            }

            System.IO.File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Formatiert eine Zeitspanne in ein menschenlesbares Format.
        /// Beispiele: "2d 3h 5m", "4h 2m 1s", "25m 12s", "12s"
        /// </summary>
        public static string FormatTimespan(TimeSpan ts)
        {
            if (ts.TotalSeconds < 0) ts = ts.Duration();

            if (ts.Days > 0)
                return $"{ts.Days}d {ts.Hours}h {ts.Minutes}m";
            if (ts.Hours > 0)
                return $"{ts.Hours}h {ts.Minutes}m {ts.Seconds}s";
            if (ts.Minutes > 0)
                return $"{ts.Minutes}m {ts.Seconds}s";

            return $"{ts.Seconds}s";
        }

        /// <summary>
        /// Konvertiert ein DateTime-Objekt in einen lesbaren String mit Berücksichtigung,
        /// ob das Datum in der Zukunft ("in ...") oder Vergangenheit ("vor ...") liegt.
        /// Wenn DateTime.MinValue übergeben wird, wird "Never" zurückgegeben.
        /// </summary>
        public static string FormatDateTime(DateTime dt)
        {
            if (dt == DateTime.MinValue)
                return "Never";

            var now = DateTime.Now;
            var diff = dt - now;

            if (diff.TotalSeconds > 0) // in der Zukunft
            {
                if (diff.TotalDays > 30)
                    return dt.ToString("yyyy-MM-dd HH:mm");
                return $"{dt:dd.MM. HH:mm} (in {FormatTimespan(diff)})";
            }
            else // in der Vergangenheit
            {
                var past = diff.Duration();
                if (past.TotalDays > 30)
                    return dt.ToString("yyyy-MM-dd HH:mm");
                return $"{dt:dd.MM. HH:mm} (vor {FormatTimespan(past)})";
            }
        }

        /// <summary>
        /// Prüft, ob eine Zeichenkette eine gültige MAC-Adresse darstellt.
        /// Erlaubt Formate mit ":" "-" "." " " oder ohne Trennzeichen.
        /// </summary>
        public static bool IsValidMacAddress(string mac)
        {
            if (string.IsNullOrWhiteSpace(mac))
                return false;

            // Entferne Trennzeichen
            var cleanMac = mac
                .Replace(":", "")
                .Replace("-", "")
                .Replace(".", "")
                .Replace(" ", "")
                .ToUpperInvariant();

            // Länge muss 12 Zeichen sein
            if (cleanMac.Length != 12)
                return false;

            // Muss aus Hex-Zeichen bestehen
            foreach (var c in cleanMac)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F')))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Formatiert eine MAC-Adresse im standardmäßigen DHCP-Format (00-11-22-33-44-55).
        /// Wenn die Eingabe ungültig ist, wird die Originalzeichenfolge zurückgegeben.
        /// </summary>
        public static string FormatMacAddress(string mac)
        {
            if (!IsValidMacAddress(mac))
                return mac ?? string.Empty;

            var cleanMac = mac
                .Replace(":", "")
                .Replace("-", "")
                .Replace(".", "")
                .Replace(" ", "")
                .ToUpperInvariant();

            // Füge Trennzeichen nach je 2 Zeichen ein
            return string.Join("-",
                Enumerable.Range(0, 6)
                    .Select(i => cleanMac.Substring(i * 2, 2)));
        }
    }
}
