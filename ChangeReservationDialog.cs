// ChangeReservationDialog.cs
using static DhcpWmiViewer.Loc;
using System.ComponentModel;
using System.Net;

namespace DhcpWmiViewer
{
    public class ChangeReservationDialog : Form
    {
        // Werden in InitializeComponent() erzeugt
        private CheckBox chkChangeIp = null!;
        private Label lblChkInfo = null!;         // Text rechts neben der Checkbox
        private TextBox txtIp = null!;
        private TextBox txtName = null!;
        private TextBox txtDescription = null!;
        private Button btnOk = null!;
        private Button btnCancel = null!;

        private readonly string _scopeStart;
        private readonly string _scopeEnd;
        private readonly string _scopeMask;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IpChanged => chkChangeIp?.Checked ?? false;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string NewIp => txtIp?.Text.Trim() ?? string.Empty;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string NewName => txtName?.Text.Trim() ?? string.Empty;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string NewDescription => txtDescription?.Text.Trim() ?? string.Empty;

        public ChangeReservationDialog(string currentIp, string currentName, string currentDescription, string scopeStart = "", string scopeEnd = "", string scopeMask = "")
        {
            _scopeStart = scopeStart ?? string.Empty;
            _scopeEnd = scopeEnd ?? string.Empty;
            _scopeMask = scopeMask ?? string.Empty;

            InitializeComponent();

            txtIp.Text = currentIp ?? string.Empty;
            txtName.Text = currentName ?? string.Empty;
            txtDescription.Text = currentDescription ?? string.Empty;

            // Set tooltip for scope/mask so it's still accessible without changing layout
            UpdateScopeTooltip();

            chkChangeIp.Checked = false;
            UpdateIpFieldState();

            // clicking label toggles checkbox
            lblChkInfo.Click += (s, e) => { try { chkChangeIp.Checked = !chkChangeIp.Checked; } catch { } };
        }

        private void InitializeComponent()
        {
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Font = new Font("Segoe UI", 10f);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MinimumSize = new Size(560, 280);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Text = T("Reservation ändern", "Edit reservation");

            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 2,
                RowCount = 4,
                AutoSize = false
            };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // IP row
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // Name
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Description
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 56)); // Buttons

            // Left label for IP
            main.Controls.Add(new Label { Text = T("IP-Adresse:", "IP address:"), Anchor = AnchorStyles.Left | AnchorStyles.Right, AutoSize = true }, 0, 0);

            // IP row: TableLayoutPanel with 3 columns:
            // [txtIp (percent 100)] [chkChangeIp (auto)] [lblChkInfo (auto)]
            var ipRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                AutoSize = false
            };
            ipRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // txtIp stretches
            ipRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));    // checkbox
            ipRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));    // checkbox label

            txtIp = new TextBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 6, 6, 0)
            };

            chkChangeIp = new CheckBox
            {
                Text = string.Empty,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(6, 6, 2, 0)
            };
            chkChangeIp.CheckedChanged += (s, e) => UpdateIpFieldState();

            lblChkInfo = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = SystemColors.GrayText,
                Text = T("IP-Adressänderung bestätigen", "Confirm IP address change"),
                Padding = new Padding(0, 6, 0, 0),
                Margin = new Padding(4, 0, 0, 0),
                Cursor = Cursors.Hand
            };

            ipRow.Controls.Add(txtIp, 0, 0);
            ipRow.Controls.Add(chkChangeIp, 1, 0);
            ipRow.Controls.Add(lblChkInfo, 2, 0);

            main.Controls.Add(ipRow, 1, 0);

            // Name row
            main.Controls.Add(new Label { Text = "Name:", Anchor = AnchorStyles.Left | AnchorStyles.Right, AutoSize = true }, 0, 1);
            txtName = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, Dock = DockStyle.Fill, Margin = new Padding(0, 6, 0, 0) };
            main.Controls.Add(txtName, 1, 1);

            // Description row
            main.Controls.Add(new Label { Text = T("Beschreibung:", "Description:"), Anchor = AnchorStyles.Left | AnchorStyles.Right, AutoSize = true }, 0, 2);
            txtDescription = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom, Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
            main.Controls.Add(txtDescription, 1, 2);

            // Buttons row
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Padding = new Padding(6)
            };
            btnOk = new Button { Text = "OK", AutoSize = true, Padding = new Padding(6) };
            btnCancel = new Button { Text = T("Abbrechen", "Cancel"), AutoSize = true, Padding = new Padding(6) };
            btnOk.Click += BtnOk_Click;
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            buttonPanel.Controls.Add(btnOk);
            buttonPanel.Controls.Add(btnCancel);

            main.Controls.Add(buttonPanel, 0, 3);
            main.SetColumnSpan(buttonPanel, 2);

            this.Controls.Add(main);
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;

            // Apply tooltip at load
            this.Load += (s, e) => UpdateScopeTooltip();
        }

        private void UpdateIpFieldState()
        {
            if (txtIp == null || chkChangeIp == null || lblChkInfo == null) return;

            txtIp.Enabled = chkChangeIp.Checked;
            txtIp.BackColor = txtIp.Enabled ? SystemColors.Window : SystemColors.Control;

            lblChkInfo.ForeColor = chkChangeIp.Checked ? Color.Black : SystemColors.GrayText;
        }

        private void UpdateScopeTooltip()
        {
            if (txtIp == null) return;
            var info = GetScopeInfoText();
            var tt = new ToolTip { ShowAlways = true };
            tt.SetToolTip(txtIp, info);
            // also set on the checkbox label so user can hover there too
            try { tt.SetToolTip(lblChkInfo, info); } catch { }
        }

        private string GetScopeInfoText()
        {
            var hasRange = !string.IsNullOrWhiteSpace(_scopeStart) && !string.IsNullOrWhiteSpace(_scopeEnd);
            var hasMask = !string.IsNullOrWhiteSpace(_scopeMask);

            if (hasRange && hasMask)
                return $"Scope: {_scopeStart}–{_scopeEnd}  Mask: {_scopeMask}";
            if (hasRange)
                return $"Scope: {_scopeStart}–{_scopeEnd}";
            if (hasMask)
                return $"Mask: {_scopeMask}";
            return T("(keine Scope-Info)", "(no scope info)");
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            if (IpChanged)
            {
                var newIp = txtIp.Text.Trim();
                if (!IPAddress.TryParse(newIp, out _))
                {
                    MessageBox.Show(this, T("Die eingegebene IP-Adresse ist ungültig.", "The IP address entered is invalid."), T("Ungültige IP", "Invalid IP"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var ok = false;
                if (!string.IsNullOrWhiteSpace(_scopeStart) && !string.IsNullOrWhiteSpace(_scopeEnd))
                    ok = NetworkHelper.IsIpInRange(newIp, _scopeStart, _scopeEnd);

                if (!ok && !string.IsNullOrWhiteSpace(_scopeMask) && !string.IsNullOrWhiteSpace(_scopeStart))
                    ok = NetworkHelper.IsInSameSubnet(newIp, _scopeStart, _scopeMask);

                if (!ok)
                {
                    string maskInfo = string.IsNullOrWhiteSpace(_scopeMask) ? T("<keine Maske verfügbar>", "<no mask available>") : _scopeMask;
                    MessageBox.Show(this,
                        T($"Die IP ist weder im Scope-Bereich ({_scopeStart} – {_scopeEnd}) noch im selben Subnetz (Maske: {maskInfo}).",
                          $"The IP is neither in the scope range ({_scopeStart} – {_scopeEnd}) nor in the same subnet (mask: {maskInfo})."),
                        T("Ungültige IP", "Invalid IP"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
