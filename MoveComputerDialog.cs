// MoveComputerDialog.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    /// <summary>
    /// Dialog zum Verschieben eines Computer-Objekts in eine andere OU
    /// </summary>
    public partial class MoveComputerDialog : Form
    {
        private readonly ADTreeItem _computerItem;
        private readonly List<string> _availableOUs;
        
        public string SelectedTargetOU { get; private set; } = "";
        public bool UserConfirmed { get; private set; } = false;

        // UI Controls
        private Label lblComputerInfo = null!;
        private Label lblCurrentOU = null!;
        private Label lblSelectTarget = null!;
        private ComboBox cmbTargetOU = null!;
        private CheckBox chkConfirm = null!;
        private Button btnOK = null!;
        private Button btnCancel = null!;
        private Panel pnlWarning = null!;
        private Label lblWarning = null!;

        public MoveComputerDialog(ADTreeItem computerItem, List<string> availableOUs)
        {
            _computerItem = computerItem ?? throw new ArgumentNullException(nameof(computerItem));
            _availableOUs = availableOUs ?? throw new ArgumentNullException(nameof(availableOUs));
            
            InitializeComponent();
            SetupDialog();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form properties
            this.Text = "Move Computer to OU";
            this.Size = new Size(600, 400);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;

            // Computer Info Label
            lblComputerInfo = new Label
            {
                Text = $"Computer: {_computerItem.Name}",
                Location = new Point(12, 12),
                Size = new Size(560, 23),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.DarkBlue
            };

            // Current OU Label
            lblCurrentOU = new Label
            {
                Text = $"Current Location: {GetFriendlyOUName(_computerItem.DistinguishedName)}",
                Location = new Point(12, 40),
                Size = new Size(560, 40),
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.DarkGreen
            };

            // Select Target Label
            lblSelectTarget = new Label
            {
                Text = "Select target container:",
                Location = new Point(12, 90),
                Size = new Size(200, 23),
                Font = new Font("Segoe UI", 9F)
            };

            // Target OU ComboBox
            cmbTargetOU = new ComboBox
            {
                Location = new Point(12, 115),
                Size = new Size(560, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };

            // Warning Panel
            pnlWarning = new Panel
            {
                Location = new Point(12, 150),
                Size = new Size(560, 80),
                BackColor = Color.LightYellow,
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };

            lblWarning = new Label
            {
                Location = new Point(8, 8),
                Size = new Size(544, 64),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.DarkRed,
                Text = "⚠️ WARNING: This appears to be a critical system computer (Domain Controller, Server, etc.).\nMoving it may affect system functionality. Please proceed with caution!"
            };
            pnlWarning.Controls.Add(lblWarning);

            // Confirmation CheckBox
            chkConfirm = new CheckBox
            {
                Text = "I confirm that I want to move this computer object",
                Location = new Point(12, 250),
                Size = new Size(400, 23),
                Font = new Font("Segoe UI", 9F),
                Checked = false
            };

            // OK Button
            btnOK = new Button
            {
                Text = "Move Computer",
                Location = new Point(420, 320),
                Size = new Size(120, 30),
                Font = new Font("Segoe UI", 9F),
                BackColor = Color.LightGreen,
                UseVisualStyleBackColor = false,
                Enabled = false
            };

            // Cancel Button
            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(290, 320),
                Size = new Size(120, 30),
                Font = new Font("Segoe UI", 9F),
                DialogResult = DialogResult.Cancel
            };

            // Add controls to form
            this.Controls.AddRange(new Control[] {
                lblComputerInfo, lblCurrentOU, lblSelectTarget, cmbTargetOU,
                pnlWarning, chkConfirm, btnOK, btnCancel
            });

            // Event handlers
            cmbTargetOU.SelectedIndexChanged += CmbTargetOU_SelectedIndexChanged;
            chkConfirm.CheckedChanged += ChkConfirm_CheckedChanged;
            btnOK.Click += BtnOK_Click;

            this.ResumeLayout(false);
        }

        private void SetupDialog()
        {
            try
            {
                // Populate target OU ComboBox
                var currentOU = _computerItem.DistinguishedName;
                var filteredOUs = _availableOUs
                    .Where(ou => !string.Equals(ou, currentOU, StringComparison.OrdinalIgnoreCase))
                    .Select(ou => new { DN = ou, FriendlyName = GetFriendlyOUName(ou) })
                    .OrderBy(x => x.FriendlyName)
                    .ToList();

                cmbTargetOU.DisplayMember = "FriendlyName";
                cmbTargetOU.ValueMember = "DN";
                cmbTargetOU.DataSource = filteredOUs;

                // Check if this is a critical system computer
                if (IsCriticalSystemComputer(_computerItem.Name))
                {
                    pnlWarning.Visible = true;
                    // Adjust form height to accommodate warning
                    this.Height = 450;
                    chkConfirm.Location = new Point(12, 300);
                    btnOK.Location = new Point(420, 370);
                    btnCancel.Location = new Point(290, 370);
                }

                DebugLogger.LogFormat("MoveComputerDialog setup completed for computer: {0}", _computerItem.Name);
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("Error setting up MoveComputerDialog: {0}", ex.Message);
                MessageBox.Show($"Error setting up dialog: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CmbTargetOU_SelectedIndexChanged(object? sender, EventArgs e)
        {
            UpdateOKButtonState();
        }

        private void ChkConfirm_CheckedChanged(object? sender, EventArgs e)
        {
            UpdateOKButtonState();
        }

        private void UpdateOKButtonState()
        {
            btnOK.Enabled = cmbTargetOU.SelectedItem != null && chkConfirm.Checked;
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            try
            {
                if (cmbTargetOU.SelectedItem == null)
                {
                    MessageBox.Show("Please select a target container.", "Move Computer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!chkConfirm.Checked)
                {
                    MessageBox.Show("Please confirm the move operation.", "Move Computer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var selectedItem = cmbTargetOU.SelectedItem;
                var targetOU = selectedItem?.GetType().GetProperty("DN")?.GetValue(selectedItem)?.ToString();
                var friendlyName = selectedItem?.GetType().GetProperty("FriendlyName")?.GetValue(selectedItem)?.ToString();

                if (string.IsNullOrEmpty(targetOU))
                {
                    MessageBox.Show("Invalid target container selected.", "Move Computer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Final confirmation for critical systems
                if (IsCriticalSystemComputer(_computerItem.Name))
                {
                    var result = MessageBox.Show(
                        $"Are you absolutely sure you want to move the critical system computer '{_computerItem.Name}' to '{friendlyName}'?\n\nThis action cannot be easily undone and may affect system functionality.",
                        "Final Confirmation - Critical System",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2);

                    if (result != DialogResult.Yes)
                    {
                        return;
                    }
                }

                SelectedTargetOU = targetOU;
                UserConfirmed = true;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("Error in BtnOK_Click: {0}", ex.Message);
                MessageBox.Show($"Error processing move request: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Konvertiert einen Distinguished Name in einen benutzerfreundlichen Namen
        /// </summary>
        private string GetFriendlyOUName(string distinguishedName)
        {
            if (string.IsNullOrEmpty(distinguishedName))
                return "Unknown";

            try
            {
                // Extrahiere die wichtigsten Teile des DN
                var parts = distinguishedName.Split(',');
                var ouParts = parts.Where(p => p.Trim().StartsWith("OU=", StringComparison.OrdinalIgnoreCase))
                                  .Select(p => p.Trim().Substring(3))
                                  .Reverse()
                                  .ToList();

                var cnParts = parts.Where(p => p.Trim().StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                                  .Select(p => p.Trim().Substring(3))
                                  .ToList();

                if (ouParts.Any())
                {
                    return string.Join(" > ", ouParts);
                }
                else if (cnParts.Any())
                {
                    return cnParts.First();
                }
                else
                {
                    return distinguishedName.Length > 50 ? distinguishedName.Substring(0, 47) + "..." : distinguishedName;
                }
            }
            catch
            {
                return distinguishedName.Length > 50 ? distinguishedName.Substring(0, 47) + "..." : distinguishedName;
            }
        }

        /// <summary>
        /// Prüft, ob es sich um einen kritischen System-Computer handelt
        /// </summary>
        private bool IsCriticalSystemComputer(string computerName)
        {
            if (string.IsNullOrEmpty(computerName))
                return false;

            var name = computerName.ToLowerInvariant();
            
            // Bekannte kritische System-Präfixe/Namen
            var criticalPatterns = new[]
            {
                "dc-", "dc01", "dc02", "dc03", "dc1", "dc2", "dc3",
                "domaincontroller", "domain-controller",
                "srv-", "server-", "sql-", "exchange-", "ex-",
                "fs-", "fileserver", "file-server",
                "dns-", "dhcp-", "ca-", "adfs-",
                "hyper-v", "hyperv", "vm-host", "vmhost"
            };

            return criticalPatterns.Any(pattern => name.Contains(pattern));
        }
    }
}