// EditIpDialog.cs

using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    /// <summary>
    /// Dialog zum Ändern einer Reservation-IP und der Description.
    /// Enthält eine Checkbox rechts vom IP-Feld, die sichtbar wird,
    /// sobald die IP verändert wurde. Die Checkbox steuert ausschliesslich,
    /// ob die IP wirklich geändert werden soll.
    /// Die Description kann immer geändert werden.
    /// </summary>
    public class EditIpDialog : Form
    {
        private readonly string originalIp;
        private TextBox txtIp = null!;
        private TextBox txtDescription = null!;
        private CheckBox chkApplyIpChange = null!;
        private Button btnOk = null!;
        private Button btnCancel = null!;

        /// <summary>Die (neue) IP wie im Textfeld (ohne Validierung).</summary>
        public string NewIp => txtIp.Text.Trim();

        /// <summary>
        /// Die (neue) Beschreibung.
        /// Setter erlaubt Vorbelegung (z.B. aus vorhandener Reservation).
        /// Attributes verhindern, dass Designer versucht, den Inhalt zu serialisieren.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Description
        {
            get => txtDescription.Text.Trim();
            set
            {
                if (txtDescription != null)
                    txtDescription.Text = value ?? string.Empty;
            }
        }

        /// <summary>Gibt an, ob die Checkbox gesetzt ist — nur dann soll die IP tatsächlich geändert werden.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ApplyIpChange => chkApplyIpChange.Checked;

        public EditIpDialog(string ip, string scopeStart = "", string scopeEnd = "", string subnetMask = "")
        {
            originalIp = ip ?? string.Empty;
            InitializeComponent();

            // initiale Werte
            txtIp.Text = originalIp;
            txtDescription.Text = string.Empty;

            // Checkbox standardmäßig unsichtbar und unchecked
            chkApplyIpChange.Visible = false;
            chkApplyIpChange.Checked = false;
        }

        private void InitializeComponent()
        {
            Text = "Change Reservation";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            Width = 480;
            Height = 180;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Font = new Font("Segoe UI", 9F);

            var tl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                ColumnCount = 3,
                RowCount = 3,
            };

            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); // label col
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // input col
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));  // checkbox col
            tl.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            tl.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            tl.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

            // IP label
            var lblIp = new Label { Text = "IP:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
            tl.Controls.Add(lblIp, 0, 0);

            // IP textbox
            txtIp = new TextBox { Dock = DockStyle.Fill };
            tl.Controls.Add(txtIp, 1, 0);

            // checkbox (no text) to the right
            chkApplyIpChange = new CheckBox
            {
                Dock = DockStyle.Fill,
                Text = string.Empty,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false
            };
            tl.Controls.Add(chkApplyIpChange, 2, 0);

            // Description label + textbox (span 2 columns)
            var lblDesc = new Label { Text = "Description:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
            tl.Controls.Add(lblDesc, 0, 1);

            txtDescription = new TextBox { Dock = DockStyle.Fill };
            tl.SetColumnSpan(txtDescription, 2);
            tl.Controls.Add(txtDescription, 1, 1);

            // Buttons
            var pnlButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
            btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, Padding = new Padding(6) };
            btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, Padding = new Padding(6) };
            pnlButtons.Controls.Add(btnOk);
            pnlButtons.Controls.Add(btnCancel);
            tl.SetColumnSpan(pnlButtons, 3);
            tl.Controls.Add(pnlButtons, 0, 2);

            Controls.Add(tl);

            // Events
            txtIp.TextChanged += TxtIp_TextChanged;
            btnOk.Click += BtnOk_Click;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        }

        private void TxtIp_TextChanged(object? sender, EventArgs e)
        {
            var current = txtIp.Text.Trim();
            var different = !string.Equals(current, originalIp, StringComparison.OrdinalIgnoreCase);

            // Checkbox nur sichtbar wenn sich IP unterscheidet.
            if (different)
            {
                // Checkbox anzeigen, aber standardmäßig ungecheckt (Schutz vor versehentlichem ändern)
                if (!chkApplyIpChange.Visible)
                {
                    chkApplyIpChange.Visible = true;
                    chkApplyIpChange.Checked = false;
                    // Fokus zeigen, aber nicht aufdringlich: setze kurz Fokus auf Checkbox
                    try { chkApplyIpChange.Focus(); } catch { }
                }
            }
            else
            {
                // Wenn wieder zurück auf original, checkbox verstecken und unchecken
                chkApplyIpChange.Visible = false;
                chkApplyIpChange.Checked = false;
            }
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            // Wenn Checkbox sichtbar und gesetzt -> IP-Änderung ist gewünscht => validiere IP
            if (chkApplyIpChange.Visible && chkApplyIpChange.Checked)
            {
                if (!System.Net.IPAddress.TryParse(NewIp, out _))
                {
                    MessageBox.Show(this, "Die eingegebene IP ist ungültig.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    DialogResult = DialogResult.None; // verhindere Schließen
                    return;
                }
            }

            // Description darf immer gesetzt werden (keine Checkbox)
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
