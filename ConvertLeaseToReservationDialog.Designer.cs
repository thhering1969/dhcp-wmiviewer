// ConvertLeaseToReservationDialog.Designer.cs
// COMPLETE FILE — einfach kopieren & einfügen

using System.Drawing;
using System.Windows.Forms;

#nullable enable

namespace DhcpWmiViewer
{
    partial class ConvertLeaseToReservationDialog
    {
        /// <summary>
        /// Erforderliche Designervariable.
        /// </summary>
        private System.ComponentModel.IContainer? components = null;

        // Controls (werden in InitializeComponent initialisiert)
        private TextBox txtIp = null!;
        private TextBox txtClientId = null!;
        private TextBox txtName = null!;
        private TextBox txtDescription = null!;
        private CheckBox chkApplyIpChange = null!;
        private Label lblApplyInfo = null!;
        private Button btnOk = null!;
        private Button btnCancel = null!;
        // Programmatisch hinzugefügter Button (wird in Code-Behind instanziert)
        private Button btnPickAvailable = null!;

        /// <summary>
        /// Verwendete Ressourcen bereinigen.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Diese Methode wird vom Windows Form-Designer benötigt.
        /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
        /// </summary>
        private void InitializeComponent()
        {
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Font = new Font("Segoe UI", 10f);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(560, 420);
            this.Size = new Size(720, 480);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Text = "Convert Lease / Edit Reservation";
            this.Padding = new Padding(8);

            // --- Bottom area: right-aligned buttons (Cancel | OK) with robust layout ---
            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 80, Padding = new Padding(6) };

            // Right-docked FlowPanel to ensure buttons remain visible on the right
            var rightButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight, // Cancel then OK (OK will be rightmost)
                AutoSize = true,
                WrapContents = false,
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            // Create buttons
            btnCancel = new Button { Text = "Abbrechen", AutoSize = true, Padding = new Padding(8), Margin = new Padding(6) };
            btnOk = new Button { Text = "OK", AutoSize = true, Padding = new Padding(8), Margin = new Padding(6) };

            // Wire click handlers robustly (set DialogResult and Close)
            btnCancel.Click += (s, e) =>
            {
                try { this.DialogResult = DialogResult.Cancel; this.Close(); } catch { }
            };

            btnOk.Click += (s, e) =>
            {
                try { this.DialogResult = DialogResult.OK; this.Close(); } catch { }
            };

            // Add in order: Cancel (left), OK (right)
            rightButtons.Controls.Add(btnCancel);
            rightButtons.Controls.Add(btnOk);

            bottomPanel.Controls.Add(rightButtons);
            this.Controls.Add(bottomPanel);

            // content
            var contentPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6), AutoScroll = true };
            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            content.RowCount = 5;
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // IP label + row
            var lblIp = new Label { Text = "IP-Adresse:", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true, Margin = new Padding(0, 10, 0, 0) };
            content.Controls.Add(lblIp, 0, 0);

            var ipRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = false };
            ipRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            ipRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            ipRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            txtIp = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 6, 8, 0) };
            chkApplyIpChange = new CheckBox { Text = "IP übernehmen", AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right, Margin = new Padding(0, 10, 8, 0), Visible = false };
            lblApplyInfo = new Label { AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, ForeColor = Color.Gray, Text = "IP-Übernahme bestätigen", Padding = new Padding(6, 6, 0, 0), Cursor = Cursors.Hand, Visible = false };

            ipRow.Controls.Add(txtIp, 0, 0);
            ipRow.Controls.Add(chkApplyIpChange, 1, 0);
            ipRow.Controls.Add(lblApplyInfo, 2, 0);

            content.Controls.Add(ipRow, 1, 0);

            // ClientId
            var lblClient = new Label { Text = "ClientId:", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true, Margin = new Padding(0, 10, 0, 0) };
            content.Controls.Add(lblClient, 0, 1);
            txtClientId = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 6, 0, 0) };
            content.Controls.Add(txtClientId, 1, 1);

            // Name
            var lblName = new Label { Text = "Name / Host:", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true, Margin = new Padding(0, 10, 0, 0) };
            content.Controls.Add(lblName, 0, 2);
            txtName = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 6, 0, 0) };
            content.Controls.Add(txtName, 1, 2);

            // Beschreibung
            var lblDesc = new Label { Text = "Beschreibung:", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true, Margin = new Padding(0, 10, 0, 0) };
            content.Controls.Add(lblDesc, 0, 3);
            txtDescription = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, Margin = new Padding(0, 6, 0, 0) };
            txtDescription.Height = 120;
            content.Controls.Add(txtDescription, 1, 3);

            // hint
            var hint = new Label
            {
                Text = "Wenn Sie die IP geändert haben, aktivieren Sie 'IP übernehmen'. Beschreibung wird immer übernommen.",
                AutoSize = false,
                Dock = DockStyle.Fill,
                MaximumSize = new Size(0, 80),
                Margin = new Padding(0, 8, 0, 0)
            };
            content.Controls.Add(hint, 1, 4);

            contentPanel.Controls.Add(content);
            this.Controls.Add(contentPanel);

            // Set Accept/Cancel buttons on the form
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }
    }
}
