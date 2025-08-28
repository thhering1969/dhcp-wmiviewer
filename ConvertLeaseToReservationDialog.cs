// ConvertLeaseToReservationDialog.cs

using System;
using System.Drawing;
using System.Net;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    public class ConvertLeaseToReservationDialog : Form
    {
        private readonly string _scopeId;
        private readonly string _originalIp;
        private readonly string _startRange;
        private readonly string _endRange;
        private readonly string _subnetMask;

        private TextBox txtIp = null!;
        private TextBox txtClientId = null!;
        private TextBox txtName = null!;
        private TextBox txtDescription = null!;
        private CheckBox chkApplyIpChange = null!;
        private Button btnOk = null!;
        private Button btnCancel = null!;

        public string IpAddress => txtIp.Text.Trim();
        public string ClientId => txtClientId.Text.Trim();
        public string Name => txtName.Text.Trim();
        public string Description => txtDescription.Text.Trim();
        public bool ApplyIpChange => chkApplyIpChange?.Checked ?? false;

        public ConvertLeaseToReservationDialog(
            string scopeId,
            string ipAddress,
            string clientId,
            string hostName,
            string startRange,
            string endRange,
            string subnetMask)
        {
            _scopeId = scopeId ?? string.Empty;
            _originalIp = ipAddress ?? string.Empty;
            _startRange = startRange ?? string.Empty;
            _endRange = endRange ?? string.Empty;
            _subnetMask = subnetMask ?? string.Empty;

            InitializeComponent();

            // prefill
            txtIp.Text = ipAddress ?? string.Empty;
            txtClientId.Text = clientId ?? string.Empty;
            txtName.Text = hostName ?? string.Empty;
            txtDescription.Text = string.Empty; // if you have existing Description you can pass it in and set here

            chkApplyIpChange.Visible = false;
            chkApplyIpChange.Checked = false;
        }

        private void InitializeComponent()
        {
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Font = new Font("Segoe UI", 10f);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MinimumSize = new Size(520, 320);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Text = "Convert Lease / Edit Reservation";

            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 2,
                RowCount = 6,
                AutoSize = true
            };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            for (int i = 0; i < 6; i++)
                main.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

            // IP row: textbox + checkbox on right
            main.Controls.Add(new Label { Text = "IP-Adresse:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);

            var ipRowPanel = new TableLayoutPanel
            {
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                AutoSize = true
            };
            ipRowPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            ipRowPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            txtIp = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right };
            txtIp.TextChanged += TxtIp_TextChanged;
            chkApplyIpChange = new CheckBox { Text = "IP übernehmen", Anchor = AnchorStyles.Right, AutoSize = true, Visible = false, Checked = false };

            ipRowPanel.Controls.Add(txtIp, 0, 0);
            ipRowPanel.Controls.Add(chkApplyIpChange, 1, 0);

            main.Controls.Add(ipRowPanel, 1, 0);

            // ClientId
            main.Controls.Add(new Label { Text = "ClientId:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
            txtClientId = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right };
            main.Controls.Add(txtClientId, 1, 1);

            // Name
            main.Controls.Add(new Label { Text = "Name / Host:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 2);
            txtName = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right };
            main.Controls.Add(txtName, 1, 2);

            // Description
            main.Controls.Add(new Label { Text = "Beschreibung:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 3);
            txtDescription = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, Multiline = true, Height = 80 };
            main.Controls.Add(txtDescription, 1, 3);

            // Hint
            var hint = new Label
            {
                Text = "Wenn Sie die IP geändert haben, aktivieren Sie 'IP übernehmen'. Beschreibung wird immer übernommen.",
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            main.Controls.Add(hint, 1, 4);

            // Buttons
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8),
                AutoSize = true
            };

            btnOk = new Button { Text = "OK", AutoSize = true, Padding = new Padding(6) };
            btnCancel = new Button { Text = "Abbrechen", AutoSize = true, Padding = new Padding(6) };

            btnOk.Click += BtnOk_Click;
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            buttonPanel.Controls.Add(btnOk);
            buttonPanel.Controls.Add(btnCancel);

            this.Controls.Add(main);
            this.Controls.Add(buttonPanel);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;

            main.BringToFront();
        }

        private void TxtIp_TextChanged(object? sender, EventArgs e)
        {
            try
            {
                var current = txtIp.Text.Trim();
                var changed = !string.Equals(current, _originalIp, StringComparison.OrdinalIgnoreCase);
                if (changed)
                {
                    chkApplyIpChange.Visible = true;
                    if (!chkApplyIpChange.Checked) chkApplyIpChange.Checked = false;
                }
                else
                {
                    chkApplyIpChange.Visible = false;
                    chkApplyIpChange.Checked = false;
                }
            }
            catch { /* ignore */ }
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            var newIp = txtIp.Text.Trim();
            if (!IPAddress.TryParse(newIp, out _))
            {
                MessageBox.Show(this, "Die eingegebene IP ist ungültig.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // optional: validate range/subnet (wie in EditIpDialog) if desired
            bool ok = true;
            if (!string.IsNullOrWhiteSpace(_startRange) && !string.IsNullOrWhiteSpace(_endRange))
            {
                ok = NetworkHelper.IsIpInRange(newIp, _startRange, _endRange);
            }
            if (!ok && !string.IsNullOrWhiteSpace(_subnetMask) && !string.IsNullOrWhiteSpace(_startRange))
            {
                ok = NetworkHelper.IsInSameSubnet(newIp, _startRange, _subnetMask);
            }

            if (!ok)
            {
                string maskInfo = string.IsNullOrWhiteSpace(_subnetMask) ? "<keine Maske verfügbar>" : _subnetMask;
                MessageBox.Show(this,
                    $"Die IP ist weder im Scope-Bereich ({_startRange} – {_endRange}) noch im selben Subnetz (Maske: {maskInfo}).",
                    "Ungültige IP",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
