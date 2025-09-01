// IpPickerForm.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace DhcpWmiViewer
{
    public class IpPickerForm : Form
    {
        private ListBox lstIps;
        private TextBox txtFilter;
        private Button btnOk;
        private Button btnCancel;
        private Label lblInfo;
        private ToolTip tt;
        private int _lastToolTipIndex = -1;

        public string SelectedIp { get; private set; } = string.Empty;

        // Event: wird gefeuert, wenn die sichtbare Liste geändert wurde (Initial + Filter)
        public event EventHandler? AvailableIpsChanged;

        protected virtual void OnAvailableIpsChanged()
            => AvailableIpsChanged?.Invoke(this, EventArgs.Empty);

        public IpPickerForm(IEnumerable<string> availableIps, string firewallStart, string firewallEnd, int reservedCount)
        {
            InitializeComponent();

            // sichere Darstellung der Zahlen / Strings
            lblInfo.Text = $"Firewall-Bereich: {firewallStart} - {firewallEnd}  •  Reserviert: {reservedCount.ToString(CultureInfo.InvariantCulture)}";

            // Vollständige Liste sortieren und als Backing im Tag speichern
            var list = (availableIps ?? Enumerable.Empty<string>()).ToList();
            try
            {
                list.Sort(CompareIpStrings);
            }
            catch
            {
                // fallback: stabiler Sort
                list = list.OrderBy(x => x).ToList();
            }

            lstIps.Tag = list;

            lstIps.BeginUpdate();
            lstIps.Items.Clear();
            lstIps.Items.AddRange(list.ToArray());
            lstIps.EndUpdate();

            if (lstIps.Items.Count > 0)
            {
                lstIps.SelectedIndex = 0;
            }

            // Titel (zahlt immer 0..N, kein 'O' als Buchstabe)
            UpdateTitle();

            // Event melden: Liste wurde initial befüllt
            OnAvailableIpsChanged();

            // OK nur aktiv, wenn Auswahl vorhanden
            btnOk.Enabled = lstIps.Items.Count > 0 && lstIps.SelectedItem != null;

            // Auswahl-Änderungs-Handler: OK aktivieren und Event feuern
            lstIps.SelectedIndexChanged += (s, e) =>
            {
                btnOk.Enabled = lstIps.SelectedItem != null;
                OnAvailableIpsChanged();
                TriggerResolveForVisible();
            };
            // initial resolve
            TriggerResolveForVisible();
        }

        private void InitializeComponent()
        {
            this.Text = "Verfügbare IPs wählen (0)";
            this.Size = new Size(420, 520);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 10f);
            this.Padding = new Padding(8);
            this.FormBorderStyle = FormBorderStyle.Sizable;

            lblInfo = new Label { Dock = DockStyle.Top, Height = 28, TextAlign = ContentAlignment.MiddleLeft };
            tt = new ToolTip();
            try { tt.AutoPopDelay = 5000; tt.InitialDelay = 300; tt.ReshowDelay = 200; } catch { }

            txtFilter = new TextBox { Dock = DockStyle.Top, Margin = new Padding(0, 6, 0, 6) };
            try { txtFilter.PlaceholderText = "Filter (z. B. 192.168.116.)"; } catch { } // PlaceholderText on older frameworks may not exist
            txtFilter.TextChanged += (s, e) => ApplyFilter();

            lstIps = new ListBox { Dock = DockStyle.Fill, SelectionMode = SelectionMode.One, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 20 };
            lstIps.DrawItem += LstIps_DrawItem;
            lstIps.MouseMove += LstIps_MouseMove;
            lstIps.MouseLeave += (s, e) => { try { tt.Hide(lstIps); } catch { } _lastToolTipIndex = -1; };

            lstIps.DoubleClick += (s, e) =>
            {
                if (lstIps.SelectedItem != null)
                {
                    SelectedIp = lstIps.SelectedItem.ToString() ?? string.Empty;
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            };

            // Bottom-Buttons: OK rechts, Abbrechen links
            var bottom = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(6),
                AutoSize = false
            };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // spacer
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            btnOk = new Button { Text = "OK", AutoSize = true, Padding = new Padding(8) };
            btnCancel = new Button { Text = "Abbrechen", AutoSize = true, Padding = new Padding(8) };

            btnOk.Click += (s, e) =>
            {
                if (lstIps.SelectedItem != null)
                {
                    SelectedIp = lstIps.SelectedItem.ToString() ?? string.Empty;
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    MessageBox.Show(this, "Bitte zuerst eine IP auswählen.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            // Buttons in TableLayoutPanel einfügen (OK rechts)
            bottom.Controls.Add(new Panel(), 0, 0); // spacer
            bottom.Controls.Add(btnOk, 1, 0);
            bottom.Controls.Add(btnCancel, 2, 0);

            // Layout
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };
            panel.Controls.Add(lstIps);
            panel.Controls.Add(txtFilter);
            panel.Controls.Add(lblInfo);

            this.Controls.Add(panel);
            this.Controls.Add(bottom);
        }

        private void ApplyFilter()
        {
            try
            {
                var f = (txtFilter?.Text ?? string.Empty).Trim();

                // Backing-Liste aus Tag oder aus aktueller Items erstellen (falls Tag nicht gesetzt wurde)
                var all = lstIps?.Tag as List<string>;
                if (all == null)
                {
                    all = lstIps?.Items.Cast<object?>()
                             .Where(x => x != null)
                             .Select(x => x!.ToString() ?? string.Empty)
                             .ToList() ?? new List<string>();
                    if (lstIps != null) lstIps.Tag = all;
                }

                var filtered = string.IsNullOrWhiteSpace(f)
                    ? all
                    : all.Where(x => !string.IsNullOrEmpty(x) && x.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

                if (lstIps != null)
                {
                    lstIps.BeginUpdate();
                    lstIps.Items.Clear();
                    if (filtered.Count > 0) lstIps.Items.AddRange(filtered.ToArray());
                    if (lstIps.Items.Count > 0) lstIps.SelectedIndex = 0;
                    lstIps.EndUpdate();
                }

                // Event melden: Liste wurde durch Filterung geändert
                OnAvailableIpsChanged();

                // Titel aktualisieren
                UpdateTitle();

                // OK aktiv / deaktivieren
                if (btnOk != null && lstIps != null)
                    btnOk.Enabled = lstIps.Items.Count > 0 && lstIps.SelectedItem != null;
                TriggerResolveForVisible();
            }
            catch
            {
                // ignore filter errors
            }
        }

        private void UpdateTitle()
        {
            var count = lstIps?.Items.Count ?? 0;
            // explizit Formatierung als Ziffer, um Verwechslung mit Buchstabe 'O' zu vermeiden
            this.Text = $"Verfügbare IPs wählen ({count.ToString(CultureInfo.InvariantCulture)})";
        }

        private static int CompareIpStrings(string a, string b)
        {
            if (a == null && b == null) return 0;
            if (a == null) return -1;
            if (b == null) return 1;
            try
            {
                var ai = ParseIpAsUIntSafe(a);
                var bi = ParseIpAsUIntSafe(b);
                return ai.CompareTo(bi);
            }
            catch
            {
                return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static uint ParseIpAsUIntSafe(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) throw new FormatException();
            var parts = ip.Split('.');
            if (parts.Length != 4) throw new FormatException();
            uint v = 0;
            foreach (var p in parts)
            {
                if (!byte.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out var b))
                    throw new FormatException();
                v = (v << 8) + b;
            }
            return v;
        }

        // ---- Visual status: ping/name coloring ----
        private enum IpStatus { Unknown, FreeNoPingGreen, DnsOnlyOrange, ActivePingRed }
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, IpStatus> _ipStatus = new System.Collections.Concurrent.ConcurrentDictionary<string, IpStatus>(StringComparer.OrdinalIgnoreCase);
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _ipReverseName = new System.Collections.Concurrent.ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource? _resolveCts;

        private void LstIps_DrawItem(object? sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0 || lstIps == null) return;
            var ip = lstIps.Items[e.Index]?.ToString() ?? string.Empty;
            _ipStatus.TryGetValue(ip, out var st);

            // Status color swatch (indicator) and background/foreground
            Color statusColor = Color.Transparent;
            Color normalBack = e.BackColor;
            switch (st)
            {
                case IpStatus.FreeNoPingGreen: statusColor = Color.FromArgb(76, 175, 80); normalBack = Color.FromArgb(220, 245, 225); break;
                case IpStatus.DnsOnlyOrange: statusColor = Color.FromArgb(255, 152, 0); normalBack = Color.FromArgb(255, 238, 210); break;
                case IpStatus.ActivePingRed: statusColor = Color.FromArgb(244, 67, 54); normalBack = Color.FromArgb(255, 224, 224); break;
                default: break;
            }

            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color back = isSelected ? SystemColors.Highlight : normalBack;
            Color fore = isSelected ? SystemColors.HighlightText : e.ForeColor;

            using (var b = new SolidBrush(back)) e.Graphics.FillRectangle(b, e.Bounds);
            // draw left indicator bar
            if (statusColor.A > 0)
            {
                var barRect = new Rectangle(e.Bounds.Left + 2, e.Bounds.Top + 2, 6, e.Bounds.Height - 4);
                using (var sb = new SolidBrush(statusColor)) e.Graphics.FillRectangle(sb, barRect);
            }

            var textRect = new Rectangle(e.Bounds.Left + 12, e.Bounds.Top, e.Bounds.Width - 14, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, ip, e.Font, textRect, fore, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            e.DrawFocusRectangle();
        }

        private void TriggerResolveForVisible()
        {
            try
            {
                _resolveCts?.Cancel();
                _resolveCts = new CancellationTokenSource();
                var ct = _resolveCts.Token;
                var ips = lstIps?.Items.Cast<object?>().Select(x => x?.ToString() ?? string.Empty).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray() ?? Array.Empty<string>();
                if (ips.Length == 0) return;

                Task.Run(async () =>
                {
                    var throttler = new SemaphoreSlim(8);
                    var tasks = ips.Select(async ip =>
                    {
                        try
                        {
                            await throttler.WaitAsync(ct).ConfigureAwait(false);
                            var (status, rev) = await EvaluateIpStatusAsync(ip, ct).ConfigureAwait(false);
                            _ipStatus[ip] = status;
                            if (!string.IsNullOrWhiteSpace(rev)) _ipReverseName[ip] = rev;
                            try { this.BeginInvoke(new Action(() => lstIps.Invalidate())); } catch { }
                        }
                        catch { }
                        finally { try { throttler.Release(); } catch { } }
                    }).ToArray();
                    try { await Task.WhenAll(tasks).ConfigureAwait(false); } catch { }
                }, ct);
            }
            catch { }
        }

        private static async Task<(IpStatus status, string reverseName)> EvaluateIpStatusAsync(string ip, CancellationToken ct)
        {
            try
            {
                // Use nslookup for reliable DNS resolution
                string revName = "";
                bool hasName = false;
                
                try
                {
                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "nslookup",
                            Arguments = ip,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };
                    
                    process.Start();
                    var output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync(ct);
                    
                    // Parse nslookup output for hostname
                    var lines = output.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.Contains("Name:") && !line.Contains("Server:"))
                        {
                            var namePart = line.Split(':')[1]?.Trim();
                            if (!string.IsNullOrWhiteSpace(namePart) && !namePart.Contains("nslookup"))
                            {
                                revName = namePart;
                                hasName = true;
                                break;
                            }
                        }
                    }
                }
                catch { hasName = false; }

                // Quick ping check with shorter timeout for faster response
                bool pingOk = false;
                try
                {
                    using (var p = new Ping())
                    {
                        var reply = await p.SendPingAsync(ip, 200); // Reduced timeout for speed
                        pingOk = (reply?.Status == IPStatus.Success);
                    }
                }
                catch { pingOk = false; }

                // Return status based on DNS resolution (primary) and ping (secondary)
                if (pingOk) return (IpStatus.ActivePingRed, revName);
                return hasName ? (IpStatus.DnsOnlyOrange, revName) : (IpStatus.FreeNoPingGreen, "");
            }
            catch { return (IpStatus.Unknown, ""); }
        }

        private void LstIps_MouseMove(object? sender, MouseEventArgs e)
        {
            try
            {
                if (lstIps == null || tt == null) return;
                int idx = lstIps.IndexFromPoint(e.Location);
                if (idx < 0 || idx >= lstIps.Items.Count) { if (_lastToolTipIndex != -1) { tt.Hide(lstIps); _lastToolTipIndex = -1; } return; }
                if (idx == _lastToolTipIndex) return;
                var ip = lstIps.Items[idx]?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(ip)) return;
                _lastToolTipIndex = idx;
                if (_ipStatus.TryGetValue(ip, out var st) && (st == IpStatus.DnsOnlyOrange || st == IpStatus.ActivePingRed) && _ipReverseName.TryGetValue(ip, out var name) && !string.IsNullOrWhiteSpace(name))
                {
                    // Show tooltip near cursor; avoid setting a static tooltip on the control
                    tt.Show(name, lstIps, e.Location.X + 16, e.Location.Y + 16, 3000);
                }
                else
                {
                    tt.Hide(lstIps);
                }
            }
            catch { }
        }
    }
}
