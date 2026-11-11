// Theme.cs
// Zentrale Farben und Styling-Helfer (heller Windows-11-Look).
namespace DhcpWmiViewer
{
    internal static class Theme
    {
        public static readonly Color Accent = Color.FromArgb(0, 103, 192);          // Windows-11-Blau
        public static readonly Color AccentHover = Color.FromArgb(0, 90, 170);
        public static readonly Color AccentPressed = Color.FromArgb(0, 75, 145);
        public static readonly Color Surface = Color.White;
        public static readonly Color Background = Color.FromArgb(243, 243, 243);
        public static readonly Color Border = Color.FromArgb(222, 222, 222);
        public static readonly Color HeaderBack = Color.FromArgb(248, 248, 248);
        public static readonly Color AlternateRow = Color.FromArgb(247, 250, 253);
        public static readonly Color Selection = Color.FromArgb(204, 228, 247);
        public static readonly Color TextPrimary = Color.FromArgb(27, 27, 27);
        public static readonly Color TextSecondary = Color.FromArgb(96, 96, 96);

        /// <summary>Hervorgehobene Hauptaktion (blau, weiße Schrift).</summary>
        public static Button PrimaryButton(string text)
        {
            var b = BaseButton(text);
            b.BackColor = Accent;
            b.ForeColor = Color.White;
            b.FlatAppearance.BorderColor = Accent;
            b.FlatAppearance.MouseOverBackColor = AccentHover;
            b.FlatAppearance.MouseDownBackColor = AccentPressed;
            b.EnabledChanged += (s, e) => b.BackColor = b.Enabled ? Accent : Color.FromArgb(160, 190, 220);
            return b;
        }

        /// <summary>Normale Aktion (weiß mit dezentem Rahmen).</summary>
        public static Button SecondaryButton(string text)
        {
            var b = BaseButton(text);
            b.BackColor = Surface;
            b.ForeColor = TextPrimary;
            b.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 245, 245);
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(232, 232, 232);
            return b;
        }

        private static Button BaseButton(string text) => new()
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10, 3, 10, 3),
            Margin = new Padding(0, 0, 6, 0),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };

        /// <summary>Ruhiges Grid: nur horizontale Linien, Zebra-Streifen, helle Auswahl.</summary>
        public static void StyleGrid(DataGridView grid)
        {
            grid.BorderStyle = BorderStyle.None;
            grid.BackgroundColor = Surface;
            grid.GridColor = Color.FromArgb(235, 235, 235);
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.RowHeadersVisible = false;
            grid.EnableHeadersVisualStyles = false;

            var header = grid.ColumnHeadersDefaultCellStyle;
            header.BackColor = HeaderBack;
            header.ForeColor = TextSecondary;
            header.SelectionBackColor = HeaderBack;
            header.SelectionForeColor = TextSecondary;
            header.Font = new Font(grid.Font.FontFamily, grid.Font.Size, FontStyle.Bold);
            header.Padding = new Padding(6, 0, 6, 0);
            header.WrapMode = DataGridViewTriState.False;

            var cell = grid.DefaultCellStyle;
            cell.BackColor = Surface;
            cell.ForeColor = TextPrimary;
            cell.SelectionBackColor = Selection;
            cell.SelectionForeColor = TextPrimary;
            cell.Padding = new Padding(6, 0, 6, 0);
            cell.WrapMode = DataGridViewTriState.False;

            grid.AlternatingRowsDefaultCellStyle.BackColor = AlternateRow;
            grid.RowTemplate.Height = 26;

            // Doppelpuffer gegen Flackern beim Scrollen (Property ist nicht öffentlich)
            typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(grid, true);
        }

        /// <summary>Weiße "Karte" mit 1px-Rahmen, in die ein Control eingebettet wird.</summary>
        public static Panel Card(Control inner, Padding? padding = null)
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Border, Padding = new Padding(1) };
            var body = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Padding = padding ?? Padding.Empty };
            inner.Dock = DockStyle.Fill;
            body.Controls.Add(inner);
            card.Controls.Add(body);
            return card;
        }

        /// <summary>Abschnittsüberschrift über einem Bereich.</summary>
        public static Label SectionTitle(string text, Font baseFont) => new()
        {
            Text = text,
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 30,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = TextPrimary,
            Font = new Font(baseFont.FontFamily, baseFont.Size + 1.5f, FontStyle.Bold),
            Padding = new Padding(2, 0, 0, 4)
        };
    }
}
