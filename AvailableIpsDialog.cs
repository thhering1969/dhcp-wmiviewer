// AvailableIpsDialog.cs

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    public class AvailableIpsDialog : Form
    {
        private DataGridView dgv;
        private TextBox txtFilter;
        private Button btnOk;
        private Button btnCancel;
        private readonly List<string> _ips;
        private readonly HashSet<string>? _reservedIps;

        public string SelectedIp { get; private set; } = string.Empty;

        /// <summary>
        /// candidates: alle Kandidaten (z. B. aus NetworkHelper.GetIpRange)
        /// reservedIps: optional die Menge der bereits reservierten IPs (werden ausgeblendet)
        /// </summary>
        public AvailableIpsDialog(IEnumerable<string> candidates, HashSet<string>? reservedIps = null, int maxItems = 20000)
        {
            _ips = (candidates ?? Enumerable.Empty<string>()).ToList();
            if (_ips.Count > maxItems) throw new ArgumentException($"Zu viele Einträge ({_ips.Count}). Bereich einschränken.");

            _reservedIps = reservedIps;
            InitializeComponent();

            // initial befüllte und gefilterte Liste anzeigen (nicht-reservierte, sortiert)
            var visible = _ips
                .Where(ip => _reservedIps == null || !_reservedIps.Contains(ip))
                .OrderBy(ip =>
                {
                    try { return NetworkHelper.IpToUInt32(ip); }
                    catch { return uint.MaxValue; }
                })
                .ToList();

            PopulateGrid(visible);
        }

        private void InitializeComponent()
        {
            this.Text = "Verfügbare IPs auswählen";
            this.Size = new Size(520, 600);
            this.StartPosition = FormStartPosition.CenterParent;

            txtFilter = new TextBox { Dock = DockStyle.Top, PlaceholderText = "Filter (z. B. 116.180)" };
            txtFilter.TextChanged += (s, e) => ApplyFilter(txtFilter.Text.Trim());

            dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            dgv.Columns.Add("ip", "IP-Adresse");
            dgv.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) SelectCurrentAndClose(); };

            var flow = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 48, Padding = new Padding(6) };
            btnOk = new Button { Text = "OK", AutoSize = true, Enabled = false, Padding = new Padding(6) };
            btnCancel = new Button { Text = "Abbrechen", AutoSize = true, Padding = new Padding(6) };
            btnOk.Click += (s, e) => SelectCurrentAndClose();
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            flow.Controls.Add(btnOk);
            flow.Controls.Add(btnCancel);

            this.Controls.Add(dgv);
            this.Controls.Add(txtFilter);
            this.Controls.Add(flow);

            dgv.SelectionChanged += (s, e) => btnOk.Enabled = dgv.SelectedRows.Count > 0;
        }

        private void PopulateGrid(IEnumerable<string> list)
        {
            dgv.Rows.Clear();
            foreach (var ip in list) dgv.Rows.Add(ip);
        }

        private void ApplyFilter(string filter)
        {
            IEnumerable<string> filtered = _ips;

            // 1) Basisfilter (nicht-reserviert)
            if (_reservedIps != null)
                filtered = filtered.Where(i => !_reservedIps.Contains(i));

            // 2) Textfilter (falls vorhanden)
            if (!string.IsNullOrWhiteSpace(filter))
                filtered = filtered.Where(i => i.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);

            // 3) Sortierung nach numerischer IP
            var ordered = filtered.OrderBy(ip =>
            {
                try { return NetworkHelper.IpToUInt32(ip); }
                catch { return uint.MaxValue; }
            });

            PopulateGrid(ordered);
        }

        private void SelectCurrentAndClose()
        {
            if (dgv.SelectedRows.Count == 0) return;
            SelectedIp = dgv.SelectedRows[0].Cells[0].Value?.ToString() ?? string.Empty;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
