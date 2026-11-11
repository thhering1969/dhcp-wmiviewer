// Helpers.cs
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;

namespace DhcpWmiViewer
{
    internal static class Helpers
    {
        private static readonly string DebugLogFile = Path.Combine(Path.GetTempPath(), "dhcp_debug_log.txt");

        /// <summary>Schreibt eine Zeile in Debug-Output und %TEMP%\dhcp_debug_log.txt (Fehler werden ignoriert).</summary>
        public static void WriteDebugLog(string text)
        {
            try
            {
                Debug.WriteLine($"[DhcpWmiViewer] {text}");
                File.AppendAllText(DebugLogFile, $"[{DateTime.UtcNow:O}] {text}{Environment.NewLine}", Encoding.UTF8);
            }
            catch { }
        }

        /// <summary>
        /// Erzeugt aus den Commands einer PowerShell-Instanz den Script-Text für die Remote-Ausführung.
        /// Alle Commands einer Instanz bilden EINE Pipeline (AddCommand hängt an) und werden mit " | " verbunden.
        /// </summary>
        public static string BuildCommandsText(PowerShell ps)
        {
            var commands = ps.Commands.Commands
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.CommandText))
                .ToList();
            if (commands.Count == 0)
                return "<no commands>";

            var sb = new StringBuilder();
            for (var i = 0; i < commands.Count; i++)
            {
                if (i > 0) sb.Append(" | ");
                sb.Append(commands[i].CommandText);
                foreach (CommandParameter p in commands[i].Parameters)
                {
                    if (!string.IsNullOrWhiteSpace(p?.Name))
                        AppendParameter(sb, p.Name, p.Value);
                }
            }
            return sb.AppendLine().ToString();
        }

        private static void AppendParameter(StringBuilder sb, string name, object? value)
        {
            switch (value)
            {
                case null:
                    sb.Append(" -").Append(name); // Switch ohne Wert
                    return;
                case SwitchParameter sw:
                    sb.Append(" -").Append(name).Append(sw.IsPresent ? ":$true" : ":$false");
                    return;
                case bool b:
                    sb.Append(" -").Append(name).Append(b ? ":$true" : ":$false");
                    return;
                case string s:
                    sb.Append(" -").Append(name).Append(' ').Append(Quote(s));
                    return;
                case sbyte or byte or short or ushort or int or uint or long or ulong:
                    sb.Append(" -").Append(name).Append(' ').Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    return;
                case IEnumerable list:
                    var items = list.Cast<object?>().Where(i => i != null).Select(i => Quote(i!.ToString() ?? "")).ToArray();
                    if (items.Length > 0)
                        sb.Append(" -").Append(name).Append(' ').Append(string.Join(",", items));
                    return;
                default:
                    sb.Append(" -").Append(name).Append(' ').Append(Quote(value.ToString() ?? ""));
                    return;
            }
        }

        /// <summary>Liefert den Wert als PowerShell-String-Literal in einfachen Anführungszeichen.</summary>
        public static string Quote(string value) => "'" + EscapeForPowerShell(value) + "'";

        /// <summary>
        /// Maskiert einen Wert für die Verwendung innerhalb eines PowerShell-Strings in einfachen Anführungszeichen.
        /// PowerShell behandelt auch typografische Anführungszeichen (‘ ’ ‚ ‛) als Single-Quote, daher werden alle verdoppelt.
        /// </summary>
        public static string EscapeForPowerShell(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var sb = new StringBuilder(value.Length + 4);
            foreach (var ch in value)
            {
                sb.Append(ch);
                if (ch == '\'' || ch == '‘' || ch == '’' || ch == '‚' || ch == '‛')
                    sb.Append(ch);
            }
            return sb.ToString();
        }
    }

    internal static class PowerShellExtensions
    {
        /// <summary>Fügt den Parameter nur hinzu, wenn der Wert nicht null/leer ist.</summary>
        public static PowerShell SafeAddParameter(this PowerShell ps, string name, object? value)
        {
            if (value != null && !string.IsNullOrWhiteSpace(value.ToString()))
                ps.AddParameter(name, value);
            return ps;
        }
    }
}
