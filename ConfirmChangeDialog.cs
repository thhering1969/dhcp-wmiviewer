// ConfirmChangeDialog.cs
using static DhcpWmiViewer.Loc;

namespace DhcpWmiViewer
{
    /// <summary>
    /// Einheitliche Zusammenfassung + Bestätigung vor jedem Schreibzugriff auf den DHCP-Server.
    /// Zeigt Server, Scope und eine Gegenüberstellung vorher → nachher; geänderte Werte sind hervorgehoben.
    /// "Abbrechen" ist vorausgewählt (Enter/Esc brechen ab).
    /// </summary>
    internal sealed class ConfirmChangeDialog : Form
    {
        /// <summary>
        /// Ein Feld der Gegenüberstellung. Before = null: neu angelegt; After = null: wird gelöscht.
        /// LogKey: sprachunabhängiger Name fürs EventLog (sonst Label).
        /// </summary>
        public sealed record Field(string Label, string? Before, string? After, string? LogKey = null)
        {
            public bool Changed => !string.Equals(Before ?? "", After ?? "", StringComparison.Ordinal) || Before == null || After == null;
        }

        private static readonly Color ChangedBack = Color.FromArgb(255, 244, 206);
        private static readonly Color DangerColor = Color.FromArgb(196, 43, 28);

        private ConfirmChangeDialog(string title, string action, string server, string scopeId, IReadOnlyList<Field> fields, bool destructive)
        {
            Text = title;
            Font = new Font("Segoe UI", 10F);
            BackColor = Theme.Surface;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = MaximizeBox = false;
            ShowInTaskbar = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(20, 16, 20, 12);

            var root = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };

            root.Controls.Add(new Label
            {
                Text = action,
                AutoSize = true,
                Font = new Font(Font.FontFamily, 12F, FontStyle.Bold),
                ForeColor = destructive ? DangerColor : Theme.TextPrimary,
                Margin = new Padding(0, 0, 0, 4)
            });
            root.Controls.Add(new Label
            {
                Text = $"Server: {server}     Scope: {scopeId}",
                AutoSize = true,
                ForeColor = Theme.TextSecondary,
                Margin = new Padding(0, 0, 0, 12)
            });

            root.Controls.Add(CreateComparisonTable(fields));

            var hint = destructive
                ? T("Diese Aktion kann nicht rückgängig gemacht werden.", "This action cannot be undone.")
                : T("Geänderte Werte sind gelb hervorgehoben.", "Changed values are highlighted in yellow.");
            root.Controls.Add(new Label { Text = hint, AutoSize = true, ForeColor = Theme.TextSecondary, Margin = new Padding(0, 10, 0, 0) });

            var btnRun = destructive ? Theme.PrimaryButton(T("Löschen", "Delete")) : Theme.PrimaryButton(T("Ausführen", "Apply"));
            if (destructive)
            {
                btnRun.BackColor = DangerColor;
                btnRun.FlatAppearance.BorderColor = DangerColor;
                btnRun.FlatAppearance.MouseOverBackColor = Color.FromArgb(170, 35, 22);
                btnRun.FlatAppearance.MouseDownBackColor = Color.FromArgb(140, 28, 18);
            }
            btnRun.DialogResult = DialogResult.OK;
            btnRun.Margin = new Padding(8, 0, 0, 0);

            var btnCancel = Theme.SecondaryButton(T("Abbrechen", "Cancel"));
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Margin = Padding.Empty;

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Right,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, 16, 0, 0)
            };
            buttons.Controls.Add(btnRun);
            buttons.Controls.Add(btnCancel);
            root.Controls.Add(buttons);

            Controls.Add(root);

            // Sicherer Standard: Enter und Esc brechen ab
            AcceptButton = btnCancel;
            CancelButton = btnCancel;
            Shown += (s, e) => btnCancel.Focus();
        }

        private TableLayoutPanel CreateComparisonTable(IReadOnlyList<Field> fields)
        {
            var table = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 3,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
                BackColor = Theme.Surface,
                Margin = Padding.Empty
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            Label Cell(string text, bool header = false, bool highlight = false, bool muted = false) => new()
            {
                Text = text,
                AutoSize = true,
                MaximumSize = new Size(340, 0),
                Padding = new Padding(8, 5, 8, 5),
                Margin = Padding.Empty,
                Dock = DockStyle.Fill,
                BackColor = header ? Theme.HeaderBack : highlight ? ChangedBack : Theme.Surface,
                ForeColor = header || muted ? Theme.TextSecondary : Theme.TextPrimary,
                Font = header || highlight ? new Font(Font, FontStyle.Bold) : Font
            };

            table.Controls.Add(Cell(T("Feld", "Field"), header: true));
            table.Controls.Add(Cell(T("Vorher", "Before"), header: true));
            table.Controls.Add(Cell(T("Nachher", "After"), header: true));

            foreach (var f in fields)
            {
                table.Controls.Add(Cell(f.Label, muted: true));
                table.Controls.Add(Cell(f.Before == null ? T("— (neu)", "— (new)") : Display(f.Before), muted: f.Before == null));
                table.Controls.Add(Cell(f.After == null ? T("— (wird gelöscht)", "— (will be deleted)") : Display(f.After), highlight: f.Changed));
            }
            return table;
        }

        private static string Display(string value) => string.IsNullOrEmpty(value) ? T("(leer)", "(empty)") : value;

        /// <summary>Zeigt die Zusammenfassung; true nur bei ausdrücklicher Bestätigung.</summary>
        public static bool Confirm(IWin32Window owner, string title, string action, string server, string scopeId,
            IReadOnlyList<Field> fields, bool destructive = false)
        {
            using var dlg = new ConfirmChangeDialog(title, action, server, scopeId, fields, destructive);
            return dlg.ShowDialog(owner) == DialogResult.OK;
        }

        /// <summary>Text der tatsächlich geänderten Felder, z. B. für Erfolgsmeldung und EventLog.</summary>
        public static string Summarize(IEnumerable<Field> fields, string separator = "\n")
            => string.Join(separator, fields.Where(f => f.Changed).Select(f =>
                f.Before == null ? $"{f.Label}: {Display(f.After ?? "")}"
                : f.After == null ? $"{f.Label}: {Display(f.Before)} " + T("(gelöscht)", "(deleted)")
                : $"{f.Label}: {Display(f.Before)} → {Display(f.After)}"));

        /// <summary>Wie <see cref="Summarize"/>, aber mit sprachunabhängigen Namen (LogKey) und ";" – fürs EventLog.</summary>
        public static string SummarizeForLog(IEnumerable<Field> fields)
            => string.Join(";", fields.Where(f => f.Changed).Select(f =>
                $"{f.LogKey ?? f.Label}=" + (f.Before == null ? (f.After ?? "") : f.After == null ? f.Before + " (deleted)" : $"{f.Before} -> {f.After}")));
    }
}
