// ConvertLeaseToReservationDialog.Designer.cs
// **KOMPLETTE DATEI** — bitte so übernehmen

namespace DhcpWmiViewer
{
    partial class ConvertLeaseToReservationDialog
    {
        /// <summary>
        /// Erforderliche Designervariable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.Label lblIp;
        private System.Windows.Forms.TextBox txtIp;
        private System.Windows.Forms.Button btnPickIp;
        private System.Windows.Forms.Label lblClientId;
        private System.Windows.Forms.TextBox txtClientId;
        private System.Windows.Forms.Label lblName;
        private System.Windows.Forms.TextBox txtName;
        private System.Windows.Forms.Label lblDescription;
        private System.Windows.Forms.TextBox txtDescription;
        private System.Windows.Forms.Label lblHint;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;

        /// <summary>
        /// Verwendete Ressourcen bereinigen.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Vom Windows Form-Designer generierter Code

        /// <summary>
        /// Erforderliche Methode für die Designerunterstützung.
        /// </summary>
        private void InitializeComponent()
        {
            this.lblIp = new System.Windows.Forms.Label();
            this.txtIp = new System.Windows.Forms.TextBox();
            this.btnPickIp = new System.Windows.Forms.Button();
            this.lblClientId = new System.Windows.Forms.Label();
            this.txtClientId = new System.Windows.Forms.TextBox();
            this.lblName = new System.Windows.Forms.Label();
            this.txtName = new System.Windows.Forms.TextBox();
            this.lblDescription = new System.Windows.Forms.Label();
            this.txtDescription = new System.Windows.Forms.TextBox();
            this.lblHint = new System.Windows.Forms.Label();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lblIp
            // 
            this.lblIp.AutoSize = true;
            this.lblIp.Location = new System.Drawing.Point(12, 15);
            this.lblIp.Name = "lblIp";
            this.lblIp.Size = new System.Drawing.Size(61, 13);
            this.lblIp.TabIndex = 0;
            this.lblIp.Text = "IP-Adresse:";
            // 
            // txtIp
            // 
            this.txtIp.Location = new System.Drawing.Point(120, 12);
            this.txtIp.Name = "txtIp";
            this.txtIp.Size = new System.Drawing.Size(260, 20);
            this.txtIp.TabIndex = 1;
            // 
            // btnPickIp
            // 
            this.btnPickIp.Location = new System.Drawing.Point(386, 10);
            this.btnPickIp.Name = "btnPickIp";
            this.btnPickIp.Size = new System.Drawing.Size(90, 23);
            this.btnPickIp.TabIndex = 2;
            this.btnPickIp.Text = "IP wählen...";
            this.btnPickIp.UseVisualStyleBackColor = true;
            this.btnPickIp.Click += new System.EventHandler(this.btnPickIp_Click);
            // 
            // lblClientId
            // 
            this.lblClientId.AutoSize = true;
            this.lblClientId.Location = new System.Drawing.Point(12, 45);
            this.lblClientId.Name = "lblClientId";
            this.lblClientId.Size = new System.Drawing.Size(47, 13);
            this.lblClientId.TabIndex = 3;
            this.lblClientId.Text = "ClientId:";
            // 
            // txtClientId
            // 
            this.txtClientId.Location = new System.Drawing.Point(120, 42);
            this.txtClientId.Name = "txtClientId";
            this.txtClientId.Size = new System.Drawing.Size(356, 20);
            this.txtClientId.TabIndex = 4;
            // 
            // lblName
            // 
            this.lblName.AutoSize = true;
            this.lblName.Location = new System.Drawing.Point(12, 75);
            this.lblName.Name = "lblName";
            this.lblName.Size = new System.Drawing.Size(70, 13);
            this.lblName.TabIndex = 5;
            this.lblName.Text = "Name / Host:";
            // 
            // txtName
            // 
            this.txtName.Location = new System.Drawing.Point(120, 72);
            this.txtName.Name = "txtName";
            this.txtName.Size = new System.Drawing.Size(356, 20);
            this.txtName.TabIndex = 6;
            // 
            // lblDescription
            // 
            this.lblDescription.AutoSize = true;
            this.lblDescription.Location = new System.Drawing.Point(12, 105);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Size = new System.Drawing.Size(69, 13);
            this.lblDescription.TabIndex = 7;
            this.lblDescription.Text = "Beschreibung:";
            // 
            // txtDescription
            // 
            this.txtDescription.Location = new System.Drawing.Point(120, 102);
            this.txtDescription.Multiline = true;
            this.txtDescription.Name = "txtDescription";
            this.txtDescription.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtDescription.Size = new System.Drawing.Size(356, 120);
            this.txtDescription.TabIndex = 8;
            // 
            // lblHint
            // 
            this.lblHint.AutoSize = true;
            this.lblHint.Location = new System.Drawing.Point(12, 230);
            this.lblHint.Name = "lblHint";
            this.lblHint.Size = new System.Drawing.Size(355, 13);
            this.lblHint.TabIndex = 9;
            this.lblHint.Text = "Wenn Sie die IP geändert haben, aktivieren Sie 'IP übernehmen'. Beschreibung wir" +
    "d";
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOk.Location = new System.Drawing.Point(401, 260);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 11;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(320, 260);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 10;
            this.btnCancel.Text = "Abbrechen";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // ConvertLeaseToReservationDialog
            // 
            this.AcceptButton = this.btnOk;
            this.CancelButton = this.btnCancel;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(494, 295);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.lblHint);
            this.Controls.Add(this.txtDescription);
            this.Controls.Add(this.lblDescription);
            this.Controls.Add(this.txtName);
            this.Controls.Add(this.lblName);
            this.Controls.Add(this.txtClientId);
            this.Controls.Add(this.lblClientId);
            this.Controls.Add(this.btnPickIp);
            this.Controls.Add(this.txtIp);
            this.Controls.Add(this.lblIp);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ConvertLeaseToReservationDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Convert Lease / Edit Reservation";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion
    }
}
