// MainForm.Layout.cs
namespace DhcpWmiViewer
{
    public partial class MainForm
    {
        private void InitializeLayout()
        {
            SuspendLayout();
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Background;

            var menuStrip = CreateMenu();
            var header = CreateHeader();
            var statusBar = CreateStatusBar();

            // ---- Scopes (oben) ----
            dgv = CreateGrid(headerHeight: 34);
            dgv.SelectionChanged += Dgv_SelectionChanged;
            dgv.CellMouseDown += SelectRowOnRightClick;
            dgv.ContextMenuStrip = CreateScopesContextMenu();
            dgv.DataSource = binding;

            // ---- Reservations ----
            dgvReservations = CreateGrid(headerHeight: 32);
            dgvReservations.CellDoubleClick += DgvReservations_CellDoubleClick;
            dgvReservations.CellMouseDown += SelectRowOnRightClick;
            dgvReservations.ContextMenuStrip = CreateReservationsContextMenu();
            dgvReservations.DataSource = bindingReservations;

            // ---- Leases (explizite Spalten, Breiten werden nach dem Binden berechnet) ----
            dgvLeases = CreateGrid(headerHeight: 32);
            dgvLeases.AutoGenerateColumns = false;
            dgvLeases.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvLeases.ScrollBars = ScrollBars.Vertical;
            dgvLeases.CellMouseDown += SelectRowOnRightClick;
            dgvLeases.ContextMenuStrip = CreateLeasesContextMenu();
            dgvLeases.CellFormatting += DgvLeases_CellFormatting;
            dgvLeases.DataError += (s, e) =>
            {
                Helpers.WriteDebugLog($"Leases DataError: {e.Exception?.Message} at Row={e.RowIndex}, Column={e.ColumnIndex}");
                e.Cancel = true;
            };
            AddLeaseColumns();
            dgvLeases.DataSource = bindingLeases;
            dgvLeases.DataBindingComplete += (s, e) => BeginInvoke(AdjustLeasesColumnWidths);
            var leasesResizeTimer = new System.Windows.Forms.Timer { Interval = 150 };
            leasesResizeTimer.Tick += (s, e) => { leasesResizeTimer.Stop(); AdjustLeasesColumnWidths(); };
            dgvLeases.Resize += (s, e) => { leasesResizeTimer.Stop(); leasesResizeTimer.Start(); };

            // ---- Tabs ----
            tabReservations = CreateTabPage("Reservations", dgvReservations);
            tabLeases = CreateTabPage("Leases", dgvLeases);
            var tabEvents = CreateTabPage("Events", CreateEventsPanel());

            var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(16, 5) };
            tabs.TabPages.AddRange(new[] { tabReservations, tabLeases, tabEvents });

            // Oberer Bereich: Überschrift + Scopes-Karte
            var scopesArea = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
            scopesArea.Controls.Add(Theme.Card(dgv));
            scopesArea.Controls.Add(Theme.SectionTitle("DHCP-Scope(s)", Font));

            // Unterer Bereich: Überschrift + Tabs
            var detailsArea = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
            detailsArea.Controls.Add(tabs);
            detailsArea.Controls.Add(Theme.SectionTitle("Details zum ausgewählten Scope", Font));

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 10,
                Panel1MinSize = 160,
                Panel2MinSize = 120,
                BackColor = Theme.Background
            };
            split.Panel1.Controls.Add(scopesArea);
            split.Panel2.Controls.Add(detailsArea);

            // Oben ca. 1/3, unten ca. 2/3 der Höhe; Events-Tab bekommt etwas mehr Platz
            Shown += (s, e) => TrySetSplitter(split, 0.35);
            tabs.SelectedIndexChanged += (s, e) => { if (tabs.SelectedTab == tabEvents) TrySetSplitter(split, 0.45); };

            var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 6, 12, 10), BackColor = Theme.Background, Margin = Padding.Empty };
            content.Controls.Add(split);

            // Zeilen: Menü | Toolbar | Inhalt | Statusleiste
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Theme.Background };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, statusBar.Height));
            layout.Controls.Add(menuStrip, 0, 0);
            layout.Controls.Add(header, 0, 1);
            layout.Controls.Add(content, 0, 2);
            layout.Controls.Add(statusBar, 0, 3);

            Controls.Add(layout);
            MainMenuStrip = menuStrip;

            ResizeBegin += (s, e) => SuspendLayout();
            ResizeEnd += (s, e) => ResumeLayout(true);

            ResumeLayout(true);
        }

        private MenuStrip CreateMenu()
        {
            var menuStrip = new MenuStrip
            {
                Dock = DockStyle.Fill,
                GripStyle = ToolStripGripStyle.Hidden,
                Padding = new Padding(6, 2, 0, 2),
                Margin = Padding.Empty,
                BackColor = Theme.Surface,
                RenderMode = ToolStripRenderMode.System
            };

            var extrasMenu = new ToolStripMenuItem("&Extras");
            extrasMenu.DropDownItems.Add("&Internet-Bereiche (Firewall)...", null, (s, e) =>
            {
                if (InternetRangesDialog.Edit(this))
                    UpdateStatus(AppConfig.InternetRanges.Count == 0
                        ? "Internet-Bereiche entfernt – Markierung in der IP-Auswahl ist aus."
                        : "Internet-Bereiche gespeichert: " + AppConfig.FormatRanges(AppConfig.InternetRanges));
            });
            extrasMenu.DropDownItems.Add(new ToolStripSeparator());
            var previewItem = new ToolStripMenuItem("PowerShell-Befehle vor Ausführung &anzeigen (Debug)")
            {
                CheckOnClick = true,
                Checked = DhcpManager.ShowCommandPreview
            };
            previewItem.CheckedChanged += (s, e) => DhcpManager.ShowCommandPreview = previewItem.Checked;
            extrasMenu.DropDownItems.Add(previewItem);
            extrasMenu.DropDownItems.Add(new ToolStripSeparator());
            extrasMenu.DropDownItems.Add("Gespeicherte A&nmeldedaten löschen", null, (s, e) => ClearCredentialsWithInfo());

            var helpMenu = new ToolStripMenuItem("&Hilfe");
            helpMenu.DropDownItems.Add("&Sicherheitsinformationen...", null, (s, e) => AdminRightsChecker.ShowSecurityInfo());
            helpMenu.DropDownItems.Add(new ToolStripSeparator());
            helpMenu.DropDownItems.Add("&Über...", null, (s, e) => ShowAboutDialog());

            menuStrip.Items.Add(extrasMenu);
            menuStrip.Items.Add(helpMenu);
            return menuStrip;
        }

        /// <summary>Toolbar: Server-Auswahl und Aktionen links, weitere Aktionen rechts.</summary>
        private Control CreateHeader()
        {
            var bar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Theme.Surface,
                Padding = new Padding(12, 10, 12, 10),
                Margin = Padding.Empty
            };
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            // Untere Trennlinie
            bar.Paint += (s, e) =>
            {
                using var pen = new Pen(Theme.Border);
                e.Graphics.DrawLine(pen, 0, bar.Height - 1, bar.Width, bar.Height - 1);
            };

            var tips = new ToolTip();

            var lbl = new Label
            {
                Text = "Server",
                AutoSize = true,
                ForeColor = Theme.TextSecondary,
                Margin = new Padding(0, 7, 8, 0)
            };
            txtServer = new TextBox
            {
                // Vorbelegung: zuletzt genutzter Server; sonst der eigene Rechner nur, wenn er DHCP-Server ist
                Text = AppConfig.LastServer.Length > 0 ? AppConfig.LastServer
                     : AppEnvironment.RunningOnDhcpServer ? Environment.MachineName : string.Empty,
                Width = 220,
                PlaceholderText = "Servername oder IP",
                Margin = new Padding(0, 3, 6, 0)
            };
            // Enter im Serverfeld startet die Abfrage
            txtServer.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter || !btnQuery.Enabled) return;
                e.SuppressKeyPress = true;
                btnQuery.PerformClick();
            };
            tips.SetToolTip(txtServer, "DHCP-Server, dessen Scopes abgefragt werden (Enter = Abfragen)");

            cmbDiscoveredServers = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 180,
                Margin = new Padding(0, 3, 6, 0)
            };
            // Auswahl in der Liste übernimmt den Server ins Textfeld (Textfeld = maßgeblich)
            cmbDiscoveredServers.SelectedIndexChanged += (s, e) =>
            {
                if (cmbDiscoveredServers.SelectedItem is { } item) txtServer.Text = item.ToString();
            };
            tips.SetToolTip(cmbDiscoveredServers, "Im Active Directory gefundene DHCP-Server");

            btnDiscover = Theme.SecondaryButton("Discover");
            btnDiscover.Click += BtnDiscover_Click;
            tips.SetToolTip(btnDiscover, "DHCP-Server im Active Directory suchen");

            btnQuery = Theme.PrimaryButton("Query Scopes");
            btnQuery.Margin = new Padding(6, 0, 0, 0);
            btnQuery.Click += BtnQuery_Click;
            tips.SetToolTip(btnQuery, "Scopes des Servers abfragen");

            var left = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty, Dock = DockStyle.Fill };
            left.Controls.AddRange(new Control[] { lbl, txtServer, cmbDiscoveredServers, btnDiscover, btnQuery });

            var btnClearCredentials = Theme.SecondaryButton("Clear Credentials");
            btnClearCredentials.Click += (s, e) => ClearCredentialsWithInfo();
            tips.SetToolTip(btnClearCredentials, "Gespeicherte Anmeldedaten und offene Verbindungen verwerfen");

            btnExportCsv = Theme.SecondaryButton("Export CSV");
            btnExportCsv.Margin = Padding.Empty;
            btnExportCsv.Enabled = false;
            btnExportCsv.Click += BtnExportCsv_Click;
            tips.SetToolTip(btnExportCsv, "Scope-Liste als CSV speichern");

            var right = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            right.Controls.AddRange(new Control[] { btnClearCredentials, btnExportCsv });

            bar.Controls.Add(left, 0, 0);
            bar.Controls.Add(right, 1, 0);
            return bar;
        }

        /// <summary>Statusleiste unten: Statustext links, Fortschrittsbalken rechts (nur wenn beschäftigt).</summary>
        private Control CreateStatusBar()
        {
            var bar = new Panel { Dock = DockStyle.Fill, Height = 30, BackColor = Theme.Surface, Margin = Padding.Empty, Padding = new Padding(12, 1, 12, 0) };
            bar.Paint += (s, e) =>
            {
                using var pen = new Pen(Theme.Border);
                e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
            };

            lblStatus = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.TextSecondary, AutoEllipsis = true };

            var pbHost = new Panel { Dock = DockStyle.Right, Width = 160, Padding = new Padding(0, 9, 0, 9), Visible = false };
            pb = new ProgressBar { Dock = DockStyle.Fill, Visible = false, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30 };
            pb.VisibleChanged += (s, e) => pbHost.Visible = pb.Visible;
            pbHost.Controls.Add(pb);

            bar.Controls.Add(lblStatus);
            bar.Controls.Add(pbHost);
            return bar;
        }

        private static TabPage CreateTabPage(string title, Control content)
        {
            var page = new TabPage(title) { BackColor = Theme.Surface, Padding = new Padding(1) };
            content.Dock = DockStyle.Fill;
            page.Controls.Add(content);
            return page;
        }

        private void ClearCredentialsWithInfo()
        {
            ClearCredentialCache();
            MessageBox.Show(this, "Credential cache cleared. Next connection will prompt for credentials again if needed.",
                "Credentials Cleared", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>Einheitlich formatiertes, schreibgeschütztes Grid mit Zeilenauswahl.</summary>
        private DataGridView CreateGrid(int headerHeight)
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                ColumnHeadersHeight = headerHeight,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                Font = Font,
                MinimumSize = new Size(200, 120)
            };
            Theme.StyleGrid(grid);

            // Kopfzeilenhöhe an Schrift/DPI anpassen
            grid.Resize += (s, e) =>
            {
                var textHeight = TextRenderer.MeasureText("Ag", grid.ColumnHeadersDefaultCellStyle.Font ?? grid.Font).Height;
                grid.ColumnHeadersHeight = Math.Max(headerHeight, textHeight + headerHeight / 3);
            };
            return grid;
        }

        private static void TrySetSplitter(SplitContainer split, double topShare)
        {
            try { split.SplitterDistance = Math.Max(180, (int)(split.Height * topShare)); } catch { }
        }

        // ------------------------------------------------------------------
        // Leases-Grid: Spalten, Breiten, Formatierung
        // ------------------------------------------------------------------

        private void AddLeaseColumns()
        {
            void Add(string name, string header, int width) => dgvLeases.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = name,
                Name = name,
                HeaderText = header,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Width = width
            });

            Add("IPAddress", "IPAddress", 120);
            Add("ClientId", "ClientId", 200);
            Add("ClientType", "ClientType", 100);
            Add("HostName", "HostName", 160);
            Add("Description", "Beschreibung", 200);
            Add("AddressState", "AddressState", 100);
            Add("LeaseExpiryTime", "LeaseExpiryTime", 160);
            Add("ScopeId", "ScopeId", 140);
            Add("ServerIP", "ServerIP", 140);
            Add("PSComputerName", "PSComputerName", 140);
        }

        /// <summary>
        /// Setzt feste Spaltenbreiten anhand des Inhalts; Restplatz bekommt "Description".
        /// </summary>
        private void AdjustLeasesColumnWidths()
        {
            if (dgvLeases.IsDisposed || !dgvLeases.IsHandleCreated || !dgvLeases.Visible || dgvLeases.Columns.Count == 0) return;

            const int extraPadding = 18, maxWidth = 1200, minWidth = 40;

            dgvLeases.SuspendLayout();
            try
            {
                dgvLeases.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
                var total = 0;
                foreach (DataGridViewColumn col in dgvLeases.Columns)
                {
                    var preferred = col.GetPreferredWidth(DataGridViewAutoSizeColumnMode.AllCells, true);
                    col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                    col.MinimumWidth = minWidth;
                    col.Width = Math.Clamp(preferred + extraPadding, minWidth, maxWidth);
                    total += col.Width;
                }

                var displayWidth = Math.Max(0, dgvLeases.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
                if (total < displayWidth)
                {
                    var target = dgvLeases.Columns["Description"] ?? dgvLeases.Columns["HostName"] ?? dgvLeases.Columns[dgvLeases.Columns.Count - 1];
                    target.Width = Math.Min(maxWidth, target.Width + displayWidth - total);
                    total = dgvLeases.Columns.Cast<DataGridViewColumn>().Sum(c => c.Width);
                }

                dgvLeases.ScrollBars = total > displayWidth ? ScrollBars.Both : ScrollBars.Vertical;
            }
            catch (Exception ex)
            {
                Helpers.WriteDebugLog("AdjustLeasesColumnWidths error: " + ex.Message);
            }
            finally
            {
                dgvLeases.ResumeLayout(true);
            }
        }

        /// <summary>Platzhalter/Masken in ServerIP (z. B. 255.255.255.0) werden grau als "-" angezeigt.</summary>
        private void DgvLeases_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.ColumnIndex < 0 || dgvLeases.Columns[e.ColumnIndex].Name != "ServerIP" || e.CellStyle == null) return;

            var raw = e.Value?.ToString()?.Trim() ?? string.Empty;
            if (raw is "" or "255.0.0.0" or "255.255.0.0" or "255.255.255.0" or "255.255.255.255" or "0.0.0.0")
            {
                e.Value = "-";
                e.CellStyle.ForeColor = Color.Gray;
                e.CellStyle.Font = _italicGridFont ??= new Font(dgvLeases.Font, FontStyle.Italic);
                e.FormattingApplied = true;
            }
        }

        private Font? _italicGridFont;

        private void AdjustReservationsColumnWidths()
        {
            dgvReservations.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
            if (dgvReservations.Columns["Description"] is { } desc)
            {
                desc.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                // Restbreite füllen, aber nicht breiter erzwingen als nötig (sonst unnötige H-Scrollleiste)
                desc.MinimumWidth = 160;
            }
        }
    }
}
