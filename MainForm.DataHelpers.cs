// MainForm.DataHelpers.cs
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;

namespace DhcpWmiViewer
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// Schreibt Spalten und erste Zeilen der leaseTable in eine Temp-Datei zur Analyse.
        /// </summary>
        private void DumpLeaseTableDebug(DataTable leaseTable)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("LeaseTable columns and sample rows");
                sb.AppendLine("Columns:");
                foreach (DataColumn col in leaseTable.Columns)
                    sb.AppendLine($"{col.Ordinal}\t{col.ColumnName}");

                sb.AppendLine();
                int maxRows = Math.Min(20, leaseTable.Rows.Count);
                sb.AppendLine($"First {maxRows} rows (index, column, value):");
                for (int r = 0; r < maxRows; r++)
                {
                    var row = leaseTable.Rows[r];
                    sb.AppendLine($"--- Row {r} ---");
                    for (int c = 0; c < leaseTable.Columns.Count; c++)
                    {
                        var cname = leaseTable.Columns[c].ColumnName;
                        var val = row[c]?.ToString() ?? "";
                        sb.AppendLine($"{c}\t{cname}\t{val}");
                    }
                }

                var p = Path.Combine(Path.GetTempPath(), "lease_debug_full.txt");
                File.WriteAllText(p, sb.ToString(), Encoding.UTF8);
                try { MessageBox.Show(this, "Wrote lease debug to: " + p, "Debug", MessageBoxButtons.OK, MessageBoxIcon.Information); } catch { }
            }
            catch (Exception ex)
            {
                try { MessageBox.Show(this, "Dump failed: " + ex.Message); } catch { }
            }
        }

        /// <summary>
        /// Heuristische Korrektur: ersetzt typische Masken/Platzhalter in ServerIP durch
        /// eine plausiblere IP aus der Zeile oder leert die Zelle.
        /// </summary>
        private void FixServerIpValues(DataTable leaseTable)
        {
            if (leaseTable == null || leaseTable.Columns.Count == 0) return;
            if (!leaseTable.Columns.Contains("ServerIP")) return;

            var knownMasks = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "255.0.0.0", "255.255.0.0", "255.255.255.0", "255.255.255.255", "0.0.0.0"
            };

            bool IsMaskOrEmpty(string? txt)
            {
                if (string.IsNullOrWhiteSpace(txt)) return true;
                txt = txt.Trim();
                if (knownMasks.Contains(txt)) return true;
                return false;
            }

            bool IsValidIpv4(string? txt)
            {
                if (string.IsNullOrWhiteSpace(txt)) return false;
                txt = txt.Trim();
                if (knownMasks.Contains(txt)) return false;
                if (IPAddress.TryParse(txt, out var ipa))
                    return ipa.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
                return false;
            }

            string? FindCandidateIpFromRow(DataRow row)
            {
                // Suche zuerst offensichtliche IP-Felder in logischer Reihenfolge
                var tryCols = new[] { "ServerIP", "IPAddress", "ScopeId", "PSComputerName" }
                              .Concat(row.Table.Columns.Cast<DataColumn>().Select(c => c.ColumnName))
                              .Distinct();
                foreach (var colName in tryCols)
                {
                    try
                    {
                        string? v = null;
                        if (row.Table.Columns.Contains(colName)) v = row[colName]?.ToString();
                        if (IsValidIpv4(v)) return v;
                    }
                    catch { }
                }
                // nichts gefunden
                return null;
            }

            foreach (DataRow r in leaseTable.Rows)
            {
                try
                {
                    var cur = r["ServerIP"]?.ToString() ?? string.Empty;

                    if (IsMaskOrEmpty(cur))
                    {
                        var candidate = FindCandidateIpFromRow(r);
                        if (!string.IsNullOrWhiteSpace(candidate))
                        {
                            r["ServerIP"] = candidate;
                        }
                        else
                        {
                            r["ServerIP"] = string.Empty;
                        }
                    }
                }
                catch
                {
                    // ignore per-row errors
                }
            }
        }

        /// <summary>
        /// Dumps environment/runtime info and the control tree (this MainForm) to:
        ///   %TEMP%\dhcp_layout_dump.txt
        /// Call this BEFORE opening the ChangeReservationDialog to get an accurate snapshot.
        /// </summary>
        private void DumpLayoutAndEnv(string hint = "")
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"DumpTime: {DateTime.Now:O}");
                if (!string.IsNullOrEmpty(hint)) sb.AppendLine("Hint: " + hint);
                sb.AppendLine();

                // environment/runtime
                try
                {
                    sb.AppendLine("Runtime: " + RuntimeInformation.FrameworkDescription);
                    sb.AppendLine("Environment.Version: " + Environment.Version.ToString());
                }
                catch { }

                // EXE path & timestamp (for certainty which binary runs)
                try
                {
                    var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "<unknown>";
                    sb.AppendLine("Running EXE: " + exe);
                    if (File.Exists(exe))
                    {
                        var fi = new FileInfo(exe);
                        sb.AppendLine("EXE LastWriteTimeUtc: " + fi.LastWriteTimeUtc.ToString("O"));
                        sb.AppendLine("EXE Length: " + fi.Length);
                    }
                }
                catch { }

                // recursively dump controls of the active form (MainForm)
                void Recurse(Control c, int level)
                {
                    var indent = new string(' ', level * 2);
                    sb.AppendLine($"{indent}{c.GetType().Name} Name='{c.Name}' Visible={c.Visible} Enabled={c.Enabled} Bounds={c.Bounds} Location={c.Location} Size={c.Size} RightToLeft={c.RightToLeft}");
                    if (c.Font != null) sb.AppendLine($"{indent}  Font={c.Font.Name} {c.Font.Size}pt Style={c.Font.Style}");
                    if (c is Label lbl) sb.AppendLine($"{indent}  Label.Text='{lbl.Text}' AutoSize={lbl.AutoSize} TextAlign={lbl.TextAlign}");
                    if (c is CheckBox cb) sb.AppendLine($"{indent}  CheckBox.Text='{cb.Text}' AutoSize={cb.AutoSize} Checked={cb.Checked}");
                    try { sb.AppendLine($"{indent}  ZIndex={(c.Parent != null ? c.Parent.Controls.GetChildIndex(c).ToString() : "<no parent>")}"); } catch { }
                    foreach (Control ch in c.Controls) Recurse(ch, level + 1);
                }

                Recurse(this, 0);

                // Also try to find the checkbox/label by name globally
                var allLbls = this.Controls.Find("lblChkInfo", true);
                var allChks = this.Controls.Find("chkChangeIp", true);
                sb.AppendLine();
                sb.AppendLine($"Found chkChangeIp instances: {allChks.Length}");
                foreach (var c in allChks) sb.AppendLine($" - {c.GetType().Name} parent={(c.Parent?.Name ?? c.Parent?.GetType().Name ?? "<null>")} Bounds={c.Bounds} Visible={c.Visible}");
                sb.AppendLine($"Found lblChkInfo instances: {allLbls.Length}");
                foreach (var c in allLbls) sb.AppendLine($" - {c.GetType().Name} parent={(c.Parent?.Name ?? c.Parent?.GetType().Name ?? "<null>")} Bounds={c.Bounds} Visible={c.Visible}");

                var path = Path.Combine(Path.GetTempPath(), "dhcp_layout_dump.txt");
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

                try
                {
                    MessageBox.Show(this, $"Layout dump written to: {path}", "Layout Dump", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch { }
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "dhcp_layout_dump_error.txt"), ex.ToString()); } catch { }
            }
        }
    }
}
