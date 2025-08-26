// Helpers.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    internal static class Helpers
    {
        private static readonly string DebugLogFile = Path.Combine(Path.GetTempPath(), "dhcp_debug_log.txt");

        public static void WriteDebugLog(string text)
        {
            try
            {
                Debug.WriteLine($"[DhcpWmiViewer] {text}");
                var line = $"[{DateTime.UtcNow:O}] {text}{Environment.NewLine}";
                File.AppendAllText(DebugLogFile, line, Encoding.UTF8);
            }
            catch { }
        }

        public static IEnumerable<string> BuildClientIdCandidates(string clientIdRaw)
        {
            var list = new List<string>();
            var formatted = FormatClientId(clientIdRaw);
            if (!string.IsNullOrWhiteSpace(formatted))
            {
                list.Add(formatted);
                if (!formatted.StartsWith("01-", StringComparison.OrdinalIgnoreCase))
                    list.Add("01-" + formatted);
            }
            if (!string.IsNullOrWhiteSpace(clientIdRaw) && !list.Contains(clientIdRaw))
                list.Add(clientIdRaw);
            if (!string.IsNullOrWhiteSpace(clientIdRaw))
            {
                var noSeparators = clientIdRaw.Replace(":", "").Replace("-", "");
                if (!list.Contains(noSeparators))
                    list.Add(noSeparators);
            }
            list.Add(clientIdRaw?.ToLowerInvariant() ?? "");
            list.Add(clientIdRaw?.ToUpperInvariant() ?? "");
            return list.Distinct().Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        }

        private static string FormatClientId(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                return string.Empty;

            var s = clientId.Trim().ToUpperInvariant().Replace(":", string.Empty).Replace("-", string.Empty);
            if (s.Length == 12 && s.All(Uri.IsHexDigit))
            {
                var sb = new StringBuilder();
                for (int i = 0; i < 12; i += 2)
                {
                    if (i > 0) sb.Append('-');
                    sb.Append(s.Substring(i, 2));
                }
                return sb.ToString();
            }
            return clientId;
        }

        public static List<string> GetPowerShellErrors(PowerShell ps)
        {
            var list = new List<string>();
            try
            {
                if (ps?.Streams?.Error != null)
                {
                    foreach (ErrorRecord err in ps.Streams.Error)
                    {
                        try
                        {
                            var fq = err.FullyQualifiedErrorId ?? "<no-id>";
                            var cat = err.CategoryInfo != null ? err.CategoryInfo.ToString() : "<no-category>";
                            var msg = err.Exception?.Message ?? err.ToString();
                            list.Add($"{fq} | {cat} | {msg}");
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return list;
        }

        public static string EscapeForPowerShell(object o)
        {
            if (o == null) return string.Empty;
            var s = o.ToString();
            return s?.Replace("'", "''") ?? "";
        }

        public static string BuildCommandsText(PowerShell ps)
        {
            var sb = new StringBuilder();
            if (ps?.Commands?.Commands == null || ps.Commands.Commands.Count == 0)
            {
                return "<no commands>";
            }

            foreach (Command cmd in ps.Commands.Commands)
            {
                if (cmd == null) continue;

                sb.Append(cmd.CommandText);

                if (cmd.Parameters != null)
                {
                    foreach (CommandParameter p in cmd.Parameters)
                    {
                        sb.Append(" -").Append(p.Name);
                        if (p.Value != null)
                            sb.Append(" '").Append(EscapeForPowerShell(p.Value)).Append("'");
                    }
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Prüft, ob die angegebenen Parameter zu einem existierenden ParameterSet des Cmdlets passen.
        /// Liefert ein Tupel (isValid, diagnosticMessage).
        /// Diese Methode führt das Get-Command Script lokal aus (server=".").
        /// </summary>
        public static async Task<(bool IsValid, string DiagnosticMessage)> IsCmdletParameterCombinationValid(
            string cmdletName,
            IEnumerable<string> providedParameterNames)
        {
            var diagnosticMessage = string.Empty;
            try
            {
                var script = $@"
                    $c = Get-Command -Name '{EscapeForPowerShell(cmdletName)}' -ErrorAction Stop
                    if ($null -eq $c) {{ return $null }}
                    $c.ParameterSets | ForEach-Object {{
                        ($_.Parameters.Keys) -join ','
                    }} | ConvertTo-Json -Compress
                ";

                Collection<PSObject>? results = null;
                try
                {
                    // Führe lokal aus (Helpers kennt kein Server-Kontext)
                    results = await PowerShellExecutor.InvokeScriptAsync(".", script).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    diagnosticMessage = $"ParameterSets für '{cmdletName}' nicht verfügbar ({ex.Message}), Prüfung übersprungen";
                    return (true, diagnosticMessage);
                }

                if (results == null || results.Count == 0)
                {
                    diagnosticMessage = $"ParameterSets für '{cmdletName}' nicht verfügbar, Prüfung übersprungen";
                    return (true, diagnosticMessage);
                }

                var json = results[0]?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(json))
                {
                    diagnosticMessage = $"Keine ParameterSets für '{cmdletName}' gefunden";
                    return (true, diagnosticMessage);
                }

                string[] sets;
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        sets = doc.RootElement.EnumerateArray()
                            .Select(e => e.GetString() ?? "")
                            .ToArray();
                    }
                    else
                    {
                        diagnosticMessage = $"Unerwartetes JSON-Format für '{cmdletName}'";
                        return (true, diagnosticMessage);
                    }
                }
                catch (Exception je)
                {
                    diagnosticMessage = $"JSON-Parsingfehler: {je.Message}";
                    return (true, diagnosticMessage);
                }

                var provided = new HashSet<string>(
                    providedParameterNames
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Select(n => n.Trim()),
                    StringComparer.OrdinalIgnoreCase);

                if (!provided.Any())
                {
                    diagnosticMessage = "Keine Parameter angegeben";
                    return (true, diagnosticMessage);
                }

                foreach (var setRaw in sets)
                {
                    var setParams = setRaw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    if (provided.All(p => setParams.Contains(p)))
                    {
                        diagnosticMessage = $"Parameter passen zu Set: [{setRaw}]";
                        return (true, diagnosticMessage);
                    }
                }

                diagnosticMessage = $"Kein ParameterSet von '{cmdletName}' enthält alle Parameter";
                return (false, diagnosticMessage);
            }
            catch (Exception ex)
            {
                diagnosticMessage = $"Prüfungsfehler: {ex.Message}";
                return (true, diagnosticMessage);
            }
        }
    }
}
