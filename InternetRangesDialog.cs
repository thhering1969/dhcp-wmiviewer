// InternetRangesDialog.cs
using static DhcpWmiViewer.Loc;
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
            Text = T("Internet-Bereiche (Firewall)", "Internet ranges (firewall)");
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
                Text = T("Optional: IP-Bereiche, die ins Internet dürfen (z. B. von der Firewall vorgegeben).\n" +
                         "Adressen daraus werden in der IP-Auswahl mit 🌐 markiert und lassen sich filtern; " +
                         "der Bestätigungsdialog zeigt „mit/ohne Internetfreigabe“.\n\n" +
                         "Ein Bereich pro Zeile, z. B. 10.0.0.10-10.0.0.50 (einzelne Adresse auch möglich). " +
                         "Leer lassen = Funktion aus.",
                         "Optional: IP ranges that may reach the internet (e.g. defined by your firewall).\n" +
                         "Addresses in them are marked with 🌐 in the IP picker and can be filtered; " +
                         "the confirmation shows \"with/without internet access\".\n\n" +
                         "One range per line, e.g. 10.0.0.10-10.0.0.50 (a single address works too). " +
                         "Leave empty = feature off.")
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

            var ok = Theme.PrimaryButton(T("Speichern", "Save"));
            var cancel = Theme.SecondaryButton(T("Abbrechen", "Cancel"));
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
                MessageBox.Show(this, T("Diese Einträge sind keine gültigen Bereiche (Format: von-bis, von ≤ bis):", "These entries are not valid ranges (format: from-to, from ≤ to):") + "\n\n" + string.Join("\n", errors),
                    T("Ungültige Eingabe", "Invalid input"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                AppConfig.SaveInternetRanges(ranges);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, T("Die Einstellung konnte nicht gespeichert werden:", "The setting could not be saved:") + "\n" + ex.Message, T("Fehler", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
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
