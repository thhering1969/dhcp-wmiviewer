// ConvertLeaseToReservationDialog.IpIntegration.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    public partial class ConvertLeaseToReservationDialog : Form
    {
        // Fallback-Firewall-Bereich (wenn die Dialog-Instanz keine FirewallStart/FirewallEnd-Props hat)
        private const string DefaultFirewallStart = "192.168.116.180";
        private const string DefaultFirewallEnd   = "192.168.116.254";

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            TryWireIpPickerButton();
        }

        private void TryWireIpPickerButton()
        {
            try
            {
                // Versuche, ein Feld oder Control namens btnPickAvailable zu finden
                Button btn = null;
                try
                {
                    var fi = this.GetType().GetField("btnPickAvailable", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (fi != null) btn = fi.GetValue(this) as Button;
                }
                catch { /* ignore */ }

                if (btn == null)
                {
                    var c = this.Controls.Find("btnPickAvailable", true).FirstOrDefault() as Button;
                    btn = c;
                }

                if (btn != null)
                {
                    // Sicherstellen, dass Handler nicht mehrfach angehängt werden
                    btn.Click -= BtnPickAvailable_Click;
                    btn.Click += BtnPickAvailable_Click;
                    btn.Enabled = true;
                }
            }
            catch
            {
                // swallow
            }
        }

        private async void BtnPickAvailable_Click(object? sender, EventArgs e)
        {
            try
            {
                // Auf UI-Thread arbeiten (ShowDialog ist synchron)
                await ShowIpPickerAndApplySelectionAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => MessageBox.Show(this, "Fehler beim Öffnen des IP-Pickers:\n" + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                }
                else
                {
                    MessageBox.Show(this, "Fehler beim Öffnen des IP-Pickers:\n" + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async Task ShowIpPickerAndApplySelectionAsync()
        {
            // Ensure run on UI thread because ShowDialog must be called from UI thread and we update controls
            if (this.InvokeRequired)
            {
                await (Task)this.Invoke(new Func<Task>(async () =>
                {
                    await ShowIpPickerAndApplySelectionAsync().ConfigureAwait(false);
                }));
                return;
            }

            // 1) Ermittle ScopeId (falls vorhanden)
            string scopeId = TryGetDialogStringProperty(new[] { "ScopeId", "Scope" }) ?? string.Empty;

            // 2) Ermittle Firewall-Bereich (falls Dialog Properties dafür definiert hat)
            var firewallStart = TryGetDialogStringProperty(new[] { "FirewallStart", "FirewallBegin" }) ?? DefaultFirewallStart;
            var firewallEnd   = TryGetDialogStringProperty(new[] { "FirewallEnd", "FirewallStop", "FirewallFinish" }) ?? DefaultFirewallEnd;

            // 3) Hole Reservations und Leases (wenn Lookup-Delegates vorhanden sind)
            var reservedOrLeased = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                // Reservation lookup (if available)
                var resDt = await InvokeLookupIfAvailableAsync(new[] { "ReservationLookup", "ReservationLookupForScopeAsync", "Lookup", "ReservationLookupAsync" }, scopeId).ConfigureAwait(false);
                if (resDt != null)
                {
                    foreach (DataRow r in resDt.Rows)
                    {
                        try
                        {
                            if (resDt.Columns.Contains("IPAddress"))
                            {
                                var ip = r["IPAddress"]?.ToString();
                                if (!string.IsNullOrWhiteSpace(ip)) reservedOrLeased.Add(ip.Trim());
                            }
                            else
                            {
                                // fallback: first column
                                var val = r.ItemArray.FirstOrDefault()?.ToString();
                                if (!string.IsNullOrWhiteSpace(val)) reservedOrLeased.Add(val.Trim());
                            }
                        }
                        catch { /* ignore row */ }
                    }
                }
            }
            catch
            {
                // swallow reservation lookup errors
            }

            try
            {
                // Lease lookup (if available)
                var leaseDt = await InvokeLookupIfAvailableAsync(new[] { "LeaseLookup", "LeaseLookupForScopeAsync", "LeasesLookup", "LookupLeases", "LeaseLookupAsync", "FetchLeases" }, scopeId).ConfigureAwait(false);
                if (leaseDt != null)
                {
                    foreach (DataRow r in leaseDt.Rows)
                    {
                        try
                        {
                            if (leaseDt.Columns.Contains("IPAddress"))
                            {
                                var ip = r["IPAddress"]?.ToString();
                                if (!string.IsNullOrWhiteSpace(ip)) reservedOrLeased.Add(ip.Trim());
                            }
                            else if (leaseDt.Columns.Contains("IP"))
                            {
                                var ip = r["IP"]?.ToString();
                                if (!string.IsNullOrWhiteSpace(ip)) reservedOrLeased.Add(ip.Trim());
                            }
                            else
                            {
                                var val = r.ItemArray.FirstOrDefault()?.ToString();
                                if (!string.IsNullOrWhiteSpace(val)) reservedOrLeased.Add(val.Trim());
                            }
                        }
                        catch { /* ignore row */ }
                    }
                }
            }
            catch
            {
                // swallow lease lookup errors
            }

            // 4) Erzeuge Liste der freien IPs im Firewall-Bereich (exklusive reservedOrLeased)
            var available = new List<string>();
            try
            {
                var rangeStart = IPStringToUInt32(firewallStart);
                var rangeEnd = IPStringToUInt32(firewallEnd);
                if (rangeStart <= rangeEnd)
                {
                    for (uint cur = rangeStart; cur <= rangeEnd; cur++)
                    {
                        var ip = UInt32ToIPString(cur);
                        if (!reservedOrLeased.Contains(ip))
                            available.Add(ip);
                        // safe-guard: avoid extremely large loops (should not occur)
                        if (available.Count > 50000) break;
                    }
                }
            }
            catch
            {
                // fallback: empty available list
            }

            // 5) Display picker dialog
            using (var picker = new IpPickerForm(available, firewallStart, firewallEnd, reservedOrLeased.Count))
            {
                var dr = picker.ShowDialog(this);
                if (dr == DialogResult.OK)
                {
                    var picked = picker.SelectedIp;
                    if (!string.IsNullOrWhiteSpace(picked))
                    {
                        // set txtIp and set apply-checkbox
                        try
                        {
                            var tb = this.GetType().GetField("txtIp", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                                        ?.GetValue(this) as TextBox
                                     ?? this.Controls.Find("txtIp", true).FirstOrDefault() as TextBox;
                            if (tb != null) tb.Text = picked;

                            var chk = this.GetType().GetField("chkApplyIpChange", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                                        ?.GetValue(this) as CheckBox
                                     ?? this.Controls.Find("chkApplyIpChange", true).FirstOrDefault() as CheckBox;
                            if (chk != null) chk.Checked = true;

                            var lbl = this.GetType().GetField("lblApplyInfo", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                                        ?.GetValue(this) as Label
                                     ?? this.Controls.Find("lblApplyInfo", true).FirstOrDefault() as Label;
                            if (lbl != null) lbl.Visible = true;
                        }
                        catch
                        {
                            // ignore UI set errors
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Versucht, ein Lookup-Delegate per Reflection zu finden (Property/Field-Namen in candidateNames).
        /// Erwartet eine Funktion die Task&lt;DataTable&gt; zurückgibt (oder Task mit Result DataTable).
        /// Liefert DataTable oder null.
        /// </summary>
        private async Task<DataTable?> InvokeLookupIfAvailableAsync(string[] candidateNames, string scopeId)
        {
            try
            {
                foreach (var name in candidateNames)
                {
                    // property
                    var pi = this.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (pi != null)
                    {
                        var val = pi.GetValue(this);
                        if (val is Delegate del)
                        {
                            var res = InvokeDelegateReturningTaskOfDataTable(del, scopeId);
                            if (res != null) return await res.ConfigureAwait(false);
                        }
                    }

                    // field
                    var fi = this.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (fi != null)
                    {
                        var val = fi.GetValue(this);
                        if (val is Delegate del)
                        {
                            var res = InvokeDelegateReturningTaskOfDataTable(del, scopeId);
                            if (res != null) return await res.ConfigureAwait(false);
                        }
                    }
                }
            }
            catch
            {
                // swallow
            }
            return null;
        }

        private Task<DataTable>? InvokeDelegateReturningTaskOfDataTable(Delegate del, string scopeId)
        {
            try
            {
                if (del == null) return null;
                var m = del.Method;
                var target = del.Target;

                // try invoke with single string arg
                if (m.GetParameters().Length == 1)
                {
                    var ret = m.Invoke(target, new object[] { scopeId });
                    if (ret is Task<DataTable> tdt) return tdt;
                    if (ret is Task t)
                    {
                        var prop = ret.GetType().GetProperty("Result");
                        if (prop != null)
                        {
                            var inner = prop.GetValue(ret) as DataTable;
                            return Task.FromResult(inner ?? new DataTable());
                        }
                        return Task.FromResult(new DataTable());
                    }
                }

                // try parameterless invoke
                if (m.GetParameters().Length == 0)
                {
                    var ret = m.Invoke(target, Array.Empty<object>());
                    if (ret is Task<DataTable> tdt) return tdt;
                    if (ret is Task) return Task.FromResult(new DataTable());
                }

                // not supported
            }
            catch
            {
                // swallow
            }
            return null;
        }

        private string? TryGetDialogStringProperty(string[] candidateNames)
        {
            try
            {
                foreach (var n in candidateNames)
                {
                    var pi = this.GetType().GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (pi != null)
                    {
                        var v = pi.GetValue(this);
                        if (v != null) return v.ToString();
                    }
                    var fi = this.GetType().GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (fi != null)
                    {
                        var v = fi.GetValue(this);
                        if (v != null) return v.ToString();
                    }
                }
            }
            catch { /* swallow */ }
            return null;
        }

        #region IP-helpers (uint conversion)
        private static uint IPStringToUInt32(string ip)
        {
            if (IPAddress.TryParse(ip, out var a) && a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var bytes = a.GetAddressBytes();
                if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
                return BitConverter.ToUInt32(bytes, 0);
            }
            throw new ArgumentException("Invalid IPv4 address: " + ip);
        }

        private static string UInt32ToIPString(uint value)
        {
            var b = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian) Array.Reverse(b);
            return new IPAddress(b).ToString();
        }
        #endregion
    }
}
