// ConvertLeaseToReservationDialog.IpPicker.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Linq;

#nullable enable

namespace DhcpWmiViewer
{
    /// <summary>
    /// Hilfs-UserControl / Helper für IP-Auswahl-Logik.
    /// Öffentliche Properties sind für den Designer ausgeblendet, damit keine Designer-Serialisierung erfolgt.
    /// </summary>
    public partial class IpPicker : UserControl
    {
        public IpPicker()
        {
            // Laufzeit-Helferklasse; Größe nur Platzhalter
            this.Size = new Size(200, 24);
        }

        // --- Properties (Designer-sicher versteckt) ---

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string? ScopeId { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string? StartRange { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string? EndRange { get; set; }

        // optionale Firewall-Subrange; wenn nicht gesetzt => ganze Scope-Range verwenden
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string? FirewallStart { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string? FirewallEnd { get; set; }

        /// <summary>
        /// Delegate zum Abrufen bestehender Reservations (Schema: Spalte "IPAddress").
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Func<string, Task<DataTable>>? ReservationLookup { get; set; }

        /// <summary>
        /// Delegate zum Abrufen bestehender Leases (Schema: Spalte "IPAddress").
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Func<string, Task<DataTable>>? LeaseLookup { get; set; }

        // --- Öffentliche API ---

        /// <summary>
        /// Liefert die Liste der verfügbaren IPs innerhalb der effektiven Range:
        /// effektiveRange = ScopeRange ∩ FirewallRange (falls FirewallRange gesetzt).
        /// Schon belegte IPs werden ausgeschlossen (Reservations + Leases).
        /// </summary>
        public async Task<List<string>> GetAvailableIpsAsync()
        {
            var result = new List<string>();

            // parse scope range (mandatory)
            if (!TryParseIpRange(StartRange, EndRange, out var scopeStart, out var scopeEnd))
                return result; // keine gültige Scope-Range -> nichts zurückgeben

            // parse firewall range (optional)
            bool haveFw = TryParseIpRange(FirewallStart, FirewallEnd, out var fwStart, out var fwEnd);

            // determine effective range: intersection of scope and firewall (if firewall present), else scope
            uint effStart = scopeStart;
            uint effEnd = scopeEnd;
            if (haveFw)
            {
                // intersection
                if (fwStart > effStart) effStart = fwStart;
                if (fwEnd < effEnd) effEnd = fwEnd;
                // if intersection empty -> return empty
                if (effStart > effEnd) return result;
            }

            // build occupied set from reservations and leases
            var occupied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(ScopeId))
            {
                // Reservations first (if provided)
                if (ReservationLookup != null)
                {
                    try
                    {
                        var dt = await ReservationLookup(ScopeId!).ConfigureAwait(false);
                        if (dt != null && dt.Rows.Count > 0)
                        {
                            foreach (DataRow r in dt.Rows)
                            {
                                try
                                {
                                    var ipObj = r.Table.Columns.Contains("IPAddress") ? r["IPAddress"] : null;
                                    var ipStr = ipObj?.ToString();
                                    if (!string.IsNullOrWhiteSpace(ipStr) && IPAddress.TryParse(ipStr.Trim(), out var ipAddr))
                                        occupied.Add(NormalizeIp(ipAddr));
                                }
                                catch { /* ignore row */ }
                            }
                        }
                    }
                    catch
                    {
                        // swallow: if lookup fails, continue and rely on leases only
                    }
                }

                // Leases (if provided)
                if (LeaseLookup != null)
                {
                    try
                    {
                        var dt = await LeaseLookup(ScopeId!).ConfigureAwait(false);
                        if (dt != null && dt.Rows.Count > 0)
                        {
                            foreach (DataRow r in dt.Rows)
                            {
                                try
                                {
                                    var ipObj = r.Table.Columns.Contains("IPAddress") ? r["IPAddress"] :
                                                r.Table.Columns.Contains("IP") ? r["IP"] : null;
                                    var ipStr = ipObj?.ToString();
                                    if (!string.IsNullOrWhiteSpace(ipStr) && IPAddress.TryParse(ipStr.Trim(), out var ipAddr))
                                        occupied.Add(NormalizeIp(ipAddr));
                                }
                                catch { /* ignore row */ }
                            }
                        }
                    }
                    catch
                    {
                        // swallow
                    }
                }
            }
            else
            {
                // no ScopeId provided, but still try to use ReservationLookup / LeaseLookup if they accept empty key
                if (ReservationLookup != null)
                {
                    try
                    {
                        var dt = await ReservationLookup(string.Empty).ConfigureAwait(false);
                        if (dt != null && dt.Rows.Count > 0)
                        {
                            foreach (DataRow r in dt.Rows)
                            {
                                try
                                {
                                    var ipObj = r.Table.Columns.Contains("IPAddress") ? r["IPAddress"] : null;
                                    var ipStr = ipObj?.ToString();
                                    if (!string.IsNullOrWhiteSpace(ipStr) && IPAddress.TryParse(ipStr.Trim(), out var ipAddr))
                                        occupied.Add(NormalizeIp(ipAddr));
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }

                if (LeaseLookup != null)
                {
                    try
                    {
                        var dt = await LeaseLookup(string.Empty).ConfigureAwait(false);
                        if (dt != null && dt.Rows.Count > 0)
                        {
                            foreach (DataRow r in dt.Rows)
                            {
                                try
                                {
                                    var ipObj = r.Table.Columns.Contains("IPAddress") ? r["IPAddress"] :
                                                r.Table.Columns.Contains("IP") ? r["IP"] : null;
                                    var ipStr = ipObj?.ToString();
                                    if (!string.IsNullOrWhiteSpace(ipStr) && IPAddress.TryParse(ipStr.Trim(), out var ipAddr))
                                        occupied.Add(NormalizeIp(ipAddr));
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }
            }

            // enumerate effective range and exclude occupied
            for (uint cur = effStart; cur <= effEnd; cur++)
            {
                var ip = UintToIp(cur);
                var s = NormalizeIp(ip);
                if (!occupied.Contains(s))
                    result.Add(s);

                // safety - avoid infinite loop on overflow
                if (cur == 0xFFFFFFFF) break;
            }

            return result;
        }

        /// <summary>
        /// Rückwärtskompatibler Wrapper für bisherigen Namen
        /// </summary>
        public Task<List<string>> GetAvailableFirewallIpsAsync() => GetAvailableIpsAsync();

        /// <summary>
        /// Öffnet einen einfachen Auswahl-Dialog (modal) und gibt gewählte IP zurück.
        /// </summary>
        public async Task<string?> PickAvailableIpAsync(IWin32Window? owner)
        {
            var list = await GetAvailableIpsAsync().ConfigureAwait(false);

            // Wechsel zurück zum UI-Kontext, da UI-Elemente erstellt werden müssen.
            if (owner is Control c && c.InvokeRequired)
            {
                return (string?)c.Invoke(new Func<string?>(() => ShowSelectionDialogOnUi(owner, list)));
            }
            else
            {
                return ShowSelectionDialogOnUi(owner, list);
            }
        }

        // --- UI helper (soll auf UI-Thread laufen) ---
        private string? ShowSelectionDialogOnUi(IWin32Window? owner, List<string> choices)
        {
            var dlg = new Form
            {
                Text = "Verfügbare IPs wählen",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(360, 420),
                MinimizeBox = false,
                MaximizeBox = false,
                FormBorderStyle = FormBorderStyle.SizableToolWindow
            };

            var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            var lst = new ListBox { Dock = DockStyle.Fill, SelectionMode = SelectionMode.One };
            var tbFilter = new TextBox { Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 6), PlaceholderText = "Filter (z. B. 192.168.116.)" };

            if (choices != null && choices.Count > 0)
                lst.Items.AddRange(choices.ToArray());

            var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            var btnCancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, AutoSize = true, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };

            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 40, Padding = new Padding(0) };
            btnPanel.Controls.Add(btnOk);
            btnPanel.Controls.Add(btnCancel);

            pnl.Controls.Add(lst);
            pnl.Controls.Add(tbFilter);
            dlg.Controls.Add(pnl);
            dlg.Controls.Add(btnPanel);

            tbFilter.TextChanged += (s, e) =>
            {
                var f = tbFilter.Text?.Trim();
                lst.BeginUpdate();
                lst.Items.Clear();
                if (string.IsNullOrEmpty(f))
                    lst.Items.AddRange(choices.ToArray());
                else
                {
                    var filtered = choices.Where(x => x.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                    lst.Items.AddRange(filtered);
                }
                lst.EndUpdate();
            };

            lst.DoubleClick += (s, e) =>
            {
                if (lst.SelectedItem != null)
                {
                    dlg.DialogResult = DialogResult.OK;
                    dlg.Close();
                }
            };

            lst.SelectedIndexChanged += (s, e) => { btnOk.Enabled = lst.SelectedItem != null; };
            btnOk.Enabled = lst.SelectedItem != null;

            var res = dlg.ShowDialog(owner);
            if (res == DialogResult.OK && lst.SelectedItem != null)
                return lst.SelectedItem.ToString();
            return null;
        }

        // --- Hilfsfunktionen für IP-Konversionen ---
        private static string NormalizeIp(IPAddress ip) => ip.ToString();

        private static bool TryParseIpRange(string? start, string? end, out uint startInt, out uint endInt)
        {
            startInt = 0;
            endInt = 0;
            if (string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(end))
                return false;

            if (!IPAddress.TryParse(start.Trim(), out var sIp)) return false;
            if (!IPAddress.TryParse(end.Trim(), out var eIp)) return false;

            var s = IpToUint(sIp);
            var e = IpToUint(eIp);
            if (s <= e)
            {
                startInt = s;
                endInt = e;
                return true;
            }
            else
            {
                // swap if reversed
                startInt = e;
                endInt = s;
                return true;
            }
        }

        private static uint IpToUint(IPAddress ip)
        {
            var bytes = ip.GetAddressBytes();
            if (bytes.Length == 4)
                return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | (uint)bytes[3];
            throw new ArgumentException("Only IPv4 supported");
        }

        private static IPAddress UintToIp(uint value)
        {
            var b = new byte[4];
            b[0] = (byte)((value >> 24) & 0xFF);
            b[1] = (byte)((value >> 16) & 0xFF);
            b[2] = (byte)((value >> 8) & 0xFF);
            b[3] = (byte)(value & 0xFF);
            return new IPAddress(b);
        }
    }
}
