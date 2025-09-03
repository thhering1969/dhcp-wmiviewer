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
        // Fallback-Firewall-Bereiche (für Rückwärtskompatibilität - werden durch FirewallConfig ersetzt)
        private const string DefaultFirewallStart1 = "192.168.116.180";
        private const string DefaultFirewallEnd1   = "192.168.116.254";
        private const string DefaultFirewallStart2 = "192.168.116.4";
        private const string DefaultFirewallEnd2   = "192.168.116.48";
        
        // Primärer Bereich (für Rückwärtskompatibilität)
        private const string DefaultFirewallStart = DefaultFirewallStart1;
        private const string DefaultFirewallEnd = DefaultFirewallEnd1;

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            TryWireIpPickerButton();
        }

        private void TryWireIpPickerButton()
        {
            try
            {
                // Versuche, Button 'btnPickAvailable' zu finden (nur dieser wird dynamisch verdrahtet).
                // Der Designer-Button 'btnPickIp' ist bereits über InitializeComponent verdrahtet
                // und darf hier NICHT erneut belegt werden, sonst öffnet sich der Picker doppelt.
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
                try { Helpers.WriteDebugLog("IP-Picker: marshaling to UI thread"); } catch { }
                await (Task)this.Invoke(new Func<Task>(async () =>
                {
                    try { Helpers.WriteDebugLog("IP-Picker: entered on UI thread (invoked)"); } catch { }
                    await ShowIpPickerAndApplySelectionAsync().ConfigureAwait(false);
                }));
                return;
            }

            try { Helpers.WriteDebugLog("IP-Picker: ShowIpPickerAndApplySelectionAsync start"); } catch { }

            // 1) Ermittle ScopeId (falls vorhanden)
            string scopeId = TryGetDialogStringProperty(new[] { "ScopeId", "Scope" }) ?? string.Empty;

            // 2) Ermittle Firewall-Bereich (falls Dialog Properties dafür definiert hat, sonst aus AppConstants)
            var (defaultStart, defaultEnd) = AppConstants.GetCombinedFirewallRange();
            var firewallStart = TryGetDialogStringProperty(new[] { "FirewallStart", "FirewallBegin" }) ?? defaultStart;
            var firewallEnd   = TryGetDialogStringProperty(new[] { "FirewallEnd", "FirewallStop", "FirewallFinish" }) ?? defaultEnd;

            // 3) Hole Reservations und Leases (wenn Lookup-Delegates vorhanden sind)
            var reservedOrLeased = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                // Verwende bevorzugt bereits vorab geladene Tabellen aus dem Dialog (falls vorhanden)
                DataTable? resDt = null;
                try
                {
                    var pi = this.GetType().GetProperty("PrefetchedReservations", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                    resDt = (DataTable?)pi?.GetValue(this);
                }
                catch { }
                if (resDt == null)
                {
                    // Reservation lookup (if available)
                    resDt = await InvokeLookupIfAvailableAsync(new[] { "ReservationLookup", "ReservationLookupForScopeAsync", "Lookup", "ReservationLookupAsync" }, scopeId).ConfigureAwait(false);
                }
                if (resDt != null)
                {
                    foreach (DataRow r in resDt.Rows)
                    {
                        try
                        {
                            if (resDt.Columns.Contains("IPAddress"))
                            {
                                var ip = r["IPAddress"]?.ToString();
                                if (!string.IsNullOrWhiteSpace(ip))
                                {
                                    if (IPAddress.TryParse(ip.Trim(), out var ipAddr))
                                        reservedOrLeased.Add(ipAddr.ToString());
                                    else
                                        reservedOrLeased.Add(ip.Trim());
                                }
                            }
                            else
                            {
                                // fallback: first column
                                var val = r.ItemArray.FirstOrDefault()?.ToString();
                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    if (IPAddress.TryParse(val.Trim(), out var ipAddr))
                                        reservedOrLeased.Add(ipAddr.ToString());
                                    else
                                        reservedOrLeased.Add(val.Trim());
                                }
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
                // Leases: ebenfalls zuerst Prefetch prüfen
                DataTable? leaseDt = null;
                try
                {
                    var pi = this.GetType().GetProperty("PrefetchedLeases", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                    leaseDt = (DataTable?)pi?.GetValue(this);
                }
                catch { }
                if (leaseDt == null)
                {
                    // Lease lookup (if available)
                    leaseDt = await InvokeLookupIfAvailableAsync(new[] { "LeaseLookup", "LeaseLookupForScopeAsync", "LeasesLookup", "LookupLeases", "LeaseLookupAsync", "FetchLeases" }, scopeId).ConfigureAwait(false);
                }
                if (leaseDt != null)
                {
                    foreach (DataRow r in leaseDt.Rows)
                    {
                        try
                        {
                            if (leaseDt.Columns.Contains("IPAddress"))
                            {
                                var ip = r["IPAddress"]?.ToString();
                                if (!string.IsNullOrWhiteSpace(ip))
                                {
                                    if (IPAddress.TryParse(ip.Trim(), out var ipAddr))
                                        reservedOrLeased.Add(ipAddr.ToString());
                                    else
                                        reservedOrLeased.Add(ip.Trim());
                                }
                            }
                            else if (leaseDt.Columns.Contains("IP"))
                            {
                                var ip = r["IP"]?.ToString();
                                if (!string.IsNullOrWhiteSpace(ip))
                                {
                                    if (IPAddress.TryParse(ip.Trim(), out var ipAddr))
                                        reservedOrLeased.Add(ipAddr.ToString());
                                    else
                                        reservedOrLeased.Add(ip.Trim());
                                }
                            }
                            else
                            {
                                var val = r.ItemArray.FirstOrDefault()?.ToString();
                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    if (IPAddress.TryParse(val.Trim(), out var ipAddr))
                                        reservedOrLeased.Add(ipAddr.ToString());
                                    else
                                        reservedOrLeased.Add(val.Trim());
                                }
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

            // Log a short preview of reserved/leased set
            try
            {
                var preview = reservedOrLeased.Take(10).ToArray();
                Helpers.WriteDebugLog($"IP-Picker ReservedOrLeased Count={reservedOrLeased.Count} Preview=[{string.Join(", ", preview)}]{(reservedOrLeased.Count > preview.Length ? ", …" : string.Empty)}");
            }
            catch { }

            // 4) Erzeuge Liste der freien IPs in beiden Firewall-Bereichen (exklusive reservedOrLeased)
            var available = new List<string>();
            try
            {
                // Verwende alle Firewall-Bereiche aus AppConstants
                available = AppConstants.GetAvailableFirewallIps(reservedOrLeased);
                
                Helpers.WriteDebugLog($"IP-Picker: Using firewall ranges from AppConstants");
                Helpers.WriteDebugLog($"IP-Picker: Firewall ranges: {AppConstants.InternetAllowedRangeString}");

                try
                {
                    var ap = available.Take(10).ToArray();
                    Helpers.WriteDebugLog($"IP-Picker Available Count: {available.Count} Preview=[{string.Join(", ", ap)}]{(available.Count > ap.Length ? ", …" : string.Empty)}");
                }
                catch { }
            }
            catch (Exception ex)
            {
                // fallback: empty available list
                Helpers.WriteDebugLog($"IP-Picker: Error generating available IPs: {ex.Message}");
                available = new List<string>();
            }

            // 5) Display picker dialog - zeige alle Firewall-Bereiche in der Anzeige
            var rangeDisplay = AppConstants.InternetAllowedRangeString;
            var (displayStart, displayEnd) = AppConstants.GetCombinedFirewallRange();
            using (var picker = new IpPickerForm(available, displayStart, displayEnd, reservedOrLeased.Count))
            {
                try { Helpers.WriteDebugLog($"IP-Picker: showing dialog with {available.Count} items"); } catch { }
                var dr = picker.ShowDialog(this);
                try { Helpers.WriteDebugLog($"IP-Picker: dialog closed with {dr}"); } catch { }
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

                            try
                            {
                                MessageBox.Show(this, "IP ausgewählt. Bitte mit OK bestätigen, um die Reservation anzulegen.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            catch { }
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
