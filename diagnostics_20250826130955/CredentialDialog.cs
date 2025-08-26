// CredentialDialog.cs
using System;
using System.ComponentModel;
using System.Drawing;
using System.Management.Automation;
using System.Security;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    internal class CredentialDialog : Form
    {
        private TextBox txtUser = null!;
        private TextBox txtPwd = null!;
        private Button btnOk = null!;
        private Button btnCancel = null!;
        private Label lblInfo = null!;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string? Server { get; set; }

        public string? UserName => txtUser.Text;
        public string? Password => txtPwd.Text;

        public CredentialDialog()
        {
            InitializeComponent();
            Load += CredentialDialog_Load;
        }

        private void InitializeComponent()
        {
            Text = "Remote Credentials";
            Width = 420;
            Height = 190;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // Info-Label
            lblInfo = new Label 
            { 
                Text = "Provide credentials for remote server:",
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(8, 8, 8, 0),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // Hauptpanel mit Eingabefeldern
            var panel = new TableLayoutPanel 
            { 
                Dock = DockStyle.Fill, 
                Padding = new Padding(8),
                ColumnCount = 2, 
                RowCount = 3 
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            // Username
            panel.Controls.Add(new Label 
            { 
                Text = "Username:", 
                Anchor = AnchorStyles.Left | AnchorStyles.Top, 
                AutoSize = true 
            }, 0, 0);
            
            txtUser = new TextBox 
            { 
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 3, 0, 3)
            };
            panel.Controls.Add(txtUser, 1, 0);

            // Password
            panel.Controls.Add(new Label 
            { 
                Text = "Password:", 
                Anchor = AnchorStyles.Left | AnchorStyles.Top, 
                AutoSize = true 
            }, 0, 1);
            
            txtPwd = new TextBox 
            { 
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                UseSystemPasswordChar = true,
                Margin = new Padding(0, 3, 0, 3)
            };
            panel.Controls.Add(txtPwd, 1, 1);

            // Button-Panel
            var flow = new FlowLayoutPanel 
            { 
                Dock = DockStyle.Fill, 
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true
            };
            
            btnOk = new Button 
            { 
                Text = "OK", 
                DialogResult = DialogResult.OK, 
                Padding = new Padding(6),
                Margin = new Padding(3),
                AutoSize = true
            };
            
            btnCancel = new Button 
            { 
                Text = "Cancel", 
                DialogResult = DialogResult.Cancel, 
                Padding = new Padding(6),
                Margin = new Padding(3),
                AutoSize = true
            };
            
            flow.Controls.Add(btnOk);
            flow.Controls.Add(btnCancel);
            panel.Controls.Add(flow, 1, 2);

            // Controls hinzufügen
            Controls.Add(panel);
            Controls.Add(lblInfo);

            // Event-Handler
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private void CredentialDialog_Load(object? sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(Server))
            {
                lblInfo.Text = $"Provide credentials for {Server}:";
                txtUser.Text = $"{Environment.UserDomainName}\\{Environment.UserName}";
            }
            txtPwd.Focus();
            txtPwd.SelectAll();
        }
    }
}