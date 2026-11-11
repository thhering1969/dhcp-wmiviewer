// InternetRangesDialog.cs
namespace DhcpWmiViewer
{
    /// <summary>
    /// Einstellung der IP-Bereiche mit Internetfreigabe (z. B. von der Firewall vorgegeben).
    /// Optional: Ohne Einträge ist die Funktion aus (keine 🌐-Markierung, kein Filter in der IP-Auswahl).
    /// </summary>
    internal sealed class InternetRangesDialog : Form
    {
        private readonly TextBox _txt;

        private InternetRangesDialog()
        {
            Text = "Internet-Bereiche (Firewall)";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            Font = new Font("Segoe UI", 9.5f);
            ClientSize = new Size(460, 330);
            Padding = new Padding(14);
            BackColor = Theme.Background;

            var intro = new Label
            {
                Dock = DockStyle.Top,
                Height = 92,
                Text = "Optional: IP-Bereiche, die ins Internet dürfen (z. B. von der Firewall vorgegeben).\n" +
                       "Adressen daraus werden in der IP-Auswahl mit 🌐 markiert und lassen sich filtern; " +
                       "der Bestätigungsdialog zeigt „mit/ohne Internetfreigabe“.\n\n" +
                       "Ein Bereich pro Zeile, z. B. 10.0.0.10-10.0.0.50 (einzelne Adresse auch möglich). " +
                       "Leer lassen = Funktion aus."
            };

            _txt = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                AcceptsReturn = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 10f),
                Text = string.Join(Environment.NewLine, AppConfig.InternetRanges.Select(r => r.Start == r.End ? r.Start : $"{r.Start}-{r.End}"))
            };

            var ok = Theme.PrimaryButton("Speichern");
            var cancel = Theme.SecondaryButton("Abbrechen");
            cancel.DialogResult = DialogResult.Cancel;
            ok.Click += (s, e) => Save();

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 10, 0, 0)
            };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);

            Controls.Add(_txt);
            Controls.Add(intro);
            Controls.Add(buttons);
            CancelButton = cancel;
        }

        private void Save()
        {
            var ranges = AppConfig.ParseRanges(_txt.Text, out var errors);
            if (errors.Count > 0)
            {
                MessageBox.Show(this, "Diese Einträge sind keine gültigen Bereiche (Format: von-bis, von ≤ bis):\n\n" + string.Join("\n", errors),
                    "Ungültige Eingabe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                AppConfig.SaveInternetRanges(ranges);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Die Einstellung konnte nicht gespeichert werden:\n" + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>Zeigt den Dialog; true, wenn gespeichert wurde.</summary>
        public static bool Edit(IWin32Window owner)
        {
            using var dlg = new InternetRangesDialog();
            return dlg.ShowDialog(owner) == DialogResult.OK;
        }
    }
}
