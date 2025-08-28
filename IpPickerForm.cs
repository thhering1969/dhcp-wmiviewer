// IpPickerForm.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Globalization;

namespace DhcpWmiViewer
{
    public class IpPickerForm : Form
    {
        private ListBox lstIps;
        private TextBox txtFilter;
        private Button btnOk;
        private Button btnCancel;
        private Label lblInfo;

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
            list.Sort(CompareIpStrings);
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
            };
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

            txtFilter = new TextBox { Dock = DockStyle.Top, Margin = new Padding(0, 6, 0, 6), PlaceholderText = "Filter (z. B. 192.168.116.)" };
            txtFilter.TextChanged += (s, e) => ApplyFilter();

            lstIps = new ListBox { Dock = DockStyle.Fill, SelectionMode = SelectionMode.One };

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
                var f = (txtFilter.Text ?? string.Empty).Trim();

                // Backing-Liste aus Tag oder aus aktueller Items erstellen (falls Tag nicht gesetzt wurde)
                var all = lstIps.Tag as List<string>;
                if (all == null)
                {
                    all = lstIps.Items.Cast<string>().ToList();
                    lstIps.Tag = all;
                }

                var filtered = string.IsNullOrWhiteSpace(f)
                    ? all
                    : all.Where(x => x.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

                lstIps.BeginUpdate();
                lstIps.Items.Clear();
                lstIps.Items.AddRange(filtered.ToArray());
                if (lstIps.Items.Count > 0) lstIps.SelectedIndex = 0;
                lstIps.EndUpdate();

                // Event melden: Liste wurde durch Filterung geändert
                OnAvailableIpsChanged();

                // Titel aktualisieren
                UpdateTitle();

                // OK aktiv / deaktivieren
                btnOk.Enabled = lstIps.Items.Count > 0 && lstIps.SelectedItem != null;
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
                var ai = ParseIpAsUInt(a);
                var bi = ParseIpAsUInt(b);
                return ai.CompareTo(bi);
            }
            catch
            {
                return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static uint ParseIpAsUInt(string ip)
        {
            var parts = ip.Split('.');
            if (parts.Length != 4) throw new FormatException();
            uint v = 0;
            foreach (var p in parts)
            {
                v = (v << 8) + uint.Parse(p, CultureInfo.InvariantCulture);
            }
            return v;
        }
    }
}
