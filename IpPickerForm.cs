// IpPickerForm.cs
using static DhcpWmiViewer.Loc;
using System.Globalization;
using System.Net.NetworkInformation;

namespace DhcpWmiViewer
{
    public class IpPickerForm : Form
    {
        private ListBox lstIps = null!;
        private TextBox txtFilter = null!;
        private Button btnOk = null!;
        private Button btnCancel = null!;
        private Label lblInfo = null!;
        private CheckBox chkInternetOnly = null!;
        private ToolTip tt = null!;
        private int _lastToolTipIndex = -1;
        private readonly ScopeAddressSpace _space;
        private int _lastTopIndex = -1;

        public string SelectedIp { get; private set; } = string.Empty;

        /// <param name="availableIps">Freie Adressen im Subnetz des Scopes.</param>
        /// <param name="space">Adressraum; internetfähige Adressen werden markiert und können gefiltert werden.</param>
        public IpPickerForm(IEnumerable<string> availableIps, ScopeAddressSpace space, int reservedCount)
        {
            _space = space;
            InitializeComponent();

            // Internet-Bereiche je Zeile, damit nichts abgeschnitten wird; Label-Höhe passt sich an.
            // Ohne konfigurierte Bereiche ist die Funktion ausgeblendet (nur ein Hinweis, wo man sie einschaltet).
            var info = T($"Subnetz: {space.Subnet.Display}  •  Belegt: {reservedCount.ToString(CultureInfo.InvariantCulture)}", $"Subnet: {space.Subnet.Display}  •  In use: {reservedCount.ToString(CultureInfo.InvariantCulture)}");
            if (space.InternetConfigured)
            {
                var internetText = space.Internet.IsEmpty
                    ? T(" keine im Subnetz", " none in this subnet")
                    : string.Concat(space.Internet.Ranges.Select(r => $"\n      {NetworkHelper.UInt32ToIp(r.Start)} – {NetworkHelper.UInt32ToIp(r.End)}"));
                info += $"\n🌐 Internet:{internetText}";
            }
            lblInfo.Text = info;
            lblInfo.Height = TextRenderer.MeasureText(lblInfo.Text, lblInfo.Font).Height + 8;
            if (!space.InternetConfigured)
                new ToolTip().SetToolTip(lblInfo, T("Internet-Bereiche markieren: Extras → Internet-Bereiche (Firewall)…", "Mark internet ranges: Extras → Internet ranges (firewall)…"));

            // Filter-Beispiel aus dem Subnetz ableiten (statt fester Adresse)
            var firstOctets = string.Join(".", NetworkHelper.UInt32ToIp(space.Subnet.Ranges.FirstOrDefault().Start).Split('.').Take(3));
            try { txtFilter.PlaceholderText = space.Subnet.IsEmpty ? "Filter" : T($"Filter (z. B. {firstOctets}.)", $"Filter (e.g. {firstOctets}.)"); } catch { }

            // Standard: nur internetfähige Adressen (wenn es im Subnetz welche gibt)
            chkInternetOnly.Visible = space.InternetConfigured;
            chkInternetOnly.Checked = !space.Internet.IsEmpty;
            chkInternetOnly.Enabled = !space.Internet.IsEmpty;
            chkInternetOnly.CheckedChanged += (s, e) => ApplyFilter();

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

            // Liste aufbauen (berücksichtigt Text- und Internet-Filter)
            ApplyFilter();

            // Auswahl-Änderung: OK aktivieren
            lstIps.SelectedIndexChanged += (s, e) => btnOk.Enabled = lstIps.SelectedItem != null;

            // Ping/DNS nur für sichtbare Zeilen: bei Scrollen/Größenänderung nachladen
            var scrollWatch = new System.Windows.Forms.Timer { Interval = 300 };
            scrollWatch.Tick += (s, e) =>
            {
                if (lstIps.TopIndex == _lastTopIndex) return;
                _lastTopIndex = lstIps.TopIndex;
                TriggerResolveForVisible();
            };
            scrollWatch.Start();
            FormClosed += (s, e) => { scrollWatch.Stop(); scrollWatch.Dispose(); _resolveCts?.Cancel(); };
            lstIps.Resize += (s, e) => TriggerResolveForVisible();
        }

        private void InitializeComponent()
        {
            this.Text = T("Verfügbare IPs wählen (0)", "Choose an available IP (0)");
            this.Size = new Size(480, 560);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 10f);
            this.Padding = new Padding(8);
            this.FormBorderStyle = FormBorderStyle.Sizable;

            lblInfo = new Label { Dock = DockStyle.Top, Height = 46, TextAlign = ContentAlignment.TopLeft, AutoEllipsis = true };
            chkInternetOnly = new BigCheckBox
            {
                Dock = DockStyle.Top,
                Text = T("🌐  Nur internetfähige Adressen", "🌐  Internet-capable addresses only"),
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                Height = 38,
                Cursor = Cursors.Hand
            };
            tt = new ToolTip();
            try { tt.AutoPopDelay = 5000; tt.InitialDelay = 300; tt.ReshowDelay = 200; } catch { }

            txtFilter = new TextBox { Dock = DockStyle.Top, Margin = new Padding(0, 6, 0, 6) };
            try { txtFilter.PlaceholderText = "Filter"; } catch { } // Beispiel wird im Konstruktor aus dem Subnetz gesetzt
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
            btnCancel = new Button { Text = T("Abbrechen", "Cancel"), AutoSize = true, Padding = new Padding(8) };

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
                    MessageBox.Show(this, T("Bitte zuerst eine IP auswählen.", "Please select an IP first."), T("Hinweis", "Note"), MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            panel.Controls.Add(chkInternetOnly);
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

                var internetOnly = chkInternetOnly?.Checked == true;
                var filtered = all
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Where(x => !internetOnly || _space.IsInternet(x))
                    .Where(x => string.IsNullOrWhiteSpace(f) || x.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                if (lstIps != null)
                {
                    lstIps.BeginUpdate();
                    lstIps.Items.Clear();
                    if (filtered.Count > 0) lstIps.Items.AddRange(filtered.ToArray());
                    if (lstIps.Items.Count > 0) lstIps.SelectedIndex = 0;
                    lstIps.EndUpdate();
                }

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
            this.Text = T($"Verfügbare IPs wählen ({count.ToString(CultureInfo.InvariantCulture)})", $"Choose an available IP ({count.ToString(CultureInfo.InvariantCulture)})");
        }

        private static int CompareIpStrings(string a, string b)
        {
            if (NetworkHelper.TryIpToUInt32(a, out var ai) && NetworkHelper.TryIpToUInt32(b, out var bi))
                return ai.CompareTo(bi);
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
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

            // Markierung für Adressen mit Internetfreigabe (rechtsbündig)
            if (_space.IsInternet(ip))
            {
                var tagRect = new Rectangle(e.Bounds.Left, e.Bounds.Top, e.Bounds.Width - 8, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, "🌐 Internet", e.Font, tagRect, isSelected ? fore : Theme.Accent,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }
            e.DrawFocusRectangle();
        }

        private void TriggerResolveForVisible()
        {
            try
            {
                _resolveCts?.Cancel();
                _resolveCts = new CancellationTokenSource();
                var ct = _resolveCts.Token;
                // Nur sichtbare Zeilen (plus etwas Vorlauf) und nur noch nicht geprüfte Adressen
                var top = Math.Max(0, lstIps.TopIndex);
                var visible = lstIps.ItemHeight > 0 ? lstIps.ClientSize.Height / lstIps.ItemHeight + 10 : 40;
                var ips = lstIps.Items.Cast<object?>()
                    .Skip(top).Take(visible)
                    .Select(x => x?.ToString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s) && !_ipStatus.ContainsKey(s))
                    .ToArray();
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

    /// <summary>Checkbox mit größerem, farbigem Kästchen (Standard-Checkbox lässt sich nicht skalieren/einfärben).</summary>
    internal sealed class BigCheckBox : CheckBox
    {
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int BoxSize { get; set; } = 18;
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color BoxColor { get; set; } = Color.FromArgb(16, 124, 16); // Grün

        public BigCheckBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? BackColor);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var color = Enabled ? BoxColor : SystemColors.GrayText;
            var box = new Rectangle(Padding.Left + 1, (Height - BoxSize) / 2, BoxSize, BoxSize);

            if (Checked)
            {
                using var fill = new SolidBrush(color);
                g.FillRectangle(fill, box);
                using var pen = new Pen(Color.White, 2.2f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
                g.DrawLines(pen, new[]
                {
                    new PointF(box.Left + BoxSize * 0.22f, box.Top + BoxSize * 0.52f),
                    new PointF(box.Left + BoxSize * 0.42f, box.Top + BoxSize * 0.72f),
                    new PointF(box.Left + BoxSize * 0.78f, box.Top + BoxSize * 0.30f)
                });
            }
            else
            {
                using var bg = new SolidBrush(Color.White);
                g.FillRectangle(bg, box);
            }
            using (var border = new Pen(color, 2f))
                g.DrawRectangle(border, box);

            var textRect = new Rectangle(box.Right + 10, 0, Width - box.Right - 10, Height);
            TextRenderer.DrawText(g, Text, Font, textRect, Enabled ? ForeColor : SystemColors.GrayText,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(g, new Rectangle(textRect.Left - 3, 4, Width - textRect.Left, Height - 8));
        }
    }
}
