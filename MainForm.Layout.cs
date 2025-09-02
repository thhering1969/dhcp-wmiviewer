// MainForm.Layout.cs
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Data;

namespace DhcpWmiViewer
{
    public partial class MainForm : Form
    {
        // Hinweis: UI-Felder kommen aus MainForm.Controls.cs - hier werden sie nur verwendet.

        private void InitializeLayout()
        {
            // Suspend layout during construction to avoid early layout events
            this.SuspendLayout();
            
            // Optimize for better resize performance
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | 
                         ControlStyles.UserPaint | 
                         ControlStyles.DoubleBuffer | 
                         ControlStyles.ResizeRedraw, true);

            // Menü erstellen
            var menuStrip = new MenuStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden, Renderer = new ToolStripProfessionalRenderer() };
            menuStrip.Padding = new Padding(4, 2, 0, 2);
            var helpMenu = new ToolStripMenuItem("&Hilfe");
            var securityInfoItem = new ToolStripMenuItem("&Sicherheitsinformationen...");
            var aboutItem = new ToolStripMenuItem("&Über...");
            
            securityInfoItem.Click += (s, e) => AdminRightsChecker.ShowSecurityInfo();
            aboutItem.Click += (s, e) => ShowAboutDialog();
            
            helpMenu.DropDownItems.Add(securityInfoItem);
            helpMenu.DropDownItems.Add(new ToolStripSeparator());
            helpMenu.DropDownItems.Add(aboutItem);
            
            menuStrip.Items.Add(helpMenu);
            this.MainMenuStrip = menuStrip;
            menuStrip.Dock = DockStyle.Top;
            // menuStrip is added later in explicit order

            // --- WICHTIG: BindingSources frühzeitig initialisieren ---
            binding = new BindingSource();
            bindingReservations = new BindingSource();
            bindingLeases = new BindingSource();
            bindingEvents = new BindingSource();

            // Platzhalter-DataTables, damit Grids schon Spalten/Schema "sehen" (optional)
            binding.DataSource = new DataTable();
            bindingReservations.DataSource = new DataTable();
            bindingLeases.DataSource = new DataTable();
            bindingEvents.DataSource = new DataTable();

            var header = new Panel { Dock = DockStyle.Top, Padding = new Padding(8), Height = 76, BackColor = SystemColors.Control };
            header.MinimumSize = new Size(0, 60);
            
            // Einfaches Layout ohne TableLayoutPanel
            var lbl = new Label { Text = "Server:", Location = new Point(0, 8), AutoSize = true };
            txtServer = new TextBox { Text = Environment.MachineName, Location = new Point(70, 5), Width = 200 };

            btnDiscover = new Button { Text = "Discover", Location = new Point(280, 5), Width = 90, Height = 30 };
            btnDiscover.Click += BtnDiscover_Click;

            cmbDiscoveredServers = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(380, 5), Width = 150 };

            btnQuery = new Button { Text = "Query Scopes", Location = new Point(540, 5), Width = 110, Height = 30 };
            btnQuery.Click += BtnQuery_Click;

            btnExportCsv = new Button { Text = "Export CSV", Width = 90, Height = 30, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnExportCsv.Enabled = false;
            btnExportCsv.Click += BtnExportCsv_Click;

            // Debug-Button (zeigt Debug-Fenster für dgvLeases-Spalten)
            var btnDebugColumns = new Button { Text = "Debug", Width = 60, Height = 30, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnDebugColumns.Click += (s, e) => ShowLeasesDebugWindow();

            // Clear Credentials Cache Button
            var btnClearCredentials = new Button { Text = "Clear Credentials", Width = 120, Height = 30, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnClearCredentials.Click += (s, e) => 
            {
                ClearCredentialCache();
                MessageBox.Show(this, "Credential cache cleared. Next connection will prompt for credentials again if needed.", 
                    "Credentials Cleared", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            // Position right-aligned buttons
            header.SizeChanged += (s, e) => {
                if (header.Width > 0) {
                    int rightMargin = 8;
                    int spacing = 5;
                    int y = 5;
                    
                    btnDebugColumns.Location = new Point(header.Width - btnDebugColumns.Width - rightMargin, y);
                    btnExportCsv.Location = new Point(btnDebugColumns.Left - btnExportCsv.Width - spacing, y);
                    btnClearCredentials.Location = new Point(btnExportCsv.Left - btnClearCredentials.Width - spacing, y);
                }
            };

            header.Controls.Add(lbl);
            header.Controls.Add(txtServer);
            header.Controls.Add(btnDiscover);
            header.Controls.Add(cmbDiscoveredServers);
            header.Controls.Add(btnQuery);
            header.Controls.Add(btnClearCredentials);
            header.Controls.Add(btnExportCsv);
            header.Controls.Add(btnDebugColumns);

            // Status und Progressbar unter dem Header, über dem Content
            lblStatus = new Label { Dock = DockStyle.Top, Height = 26, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 2, 6, 2) };
            pb = new ProgressBar { Dock = DockStyle.Top, Height = 10, Visible = false, Minimum = 0, Maximum = 100, Style = ProgressBarStyle.Continuous };

            var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8), BackColor = SystemColors.Window };
            content.SuspendLayout();
            var split = new SplitContainer 
            { 
                Dock = DockStyle.Fill, 
                Orientation = Orientation.Horizontal, 
                // Make top panel about 1/3 of the form height by default so the bottom gets ~2/3
                SplitterDistance = Math.Max(160, (int)(this.ClientSize.Height * 0.35)),
                FixedPanel = FixedPanel.None,
                IsSplitterFixed = false,
                SplitterWidth = 6,
                Panel1MinSize = 160,
                Panel2MinSize = 120
            };
            // Re-adjust once the form is shown (handles high DPI / different window sizes)
            this.Shown += (s, e) => {
                try { split.SplitterDistance = Math.Max(160, (int)(this.ClientSize.Height * 0.35)); } catch { /* ignore */ }
            };

            // Top: scopes grid
            dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoGenerateColumns = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells, // size to headers and content
                DefaultCellStyle = { WrapMode = DataGridViewTriState.False },
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                RowTemplate = { Height = 22 }, // Fixed row height for better performance
                VirtualMode = false,
                ColumnHeadersVisible = true,
                ColumnHeadersHeight = 36,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing
            };
            dgv.MinimumSize = new Size(200, 160);
            dgv.Padding = new Padding(0, 12, 0, 0); // extra space below header area to avoid overlay
            dgv.Resize += (s, e) => {
                try { dgv.ColumnHeadersHeight = Math.Max(36, TextRenderer.MeasureText("Ag", dgv.ColumnHeadersDefaultCellStyle.Font ?? dgv.Font).Height + 12); } catch {}
            };
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
            // Ensure readable headers across DPI/themes
            dgv.EnableHeadersVisualStyles = false;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = SystemColors.Control;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = SystemColors.ControlText;
            dgv.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            // Keep explicit header height without AutoSize to avoid DPI truncation
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect = false;
            dgv.RowHeadersVisible = true;
            // Ensure visible contrast lines and headers
            dgv.GridColor = SystemColors.ControlDark;
            dgv.BackgroundColor = SystemColors.Window;
            dgv.BorderStyle = BorderStyle.FixedSingle;
            dgv.AdvancedColumnHeadersBorderStyle.All = DataGridViewAdvancedCellBorderStyle.Single;
            dgv.AdvancedCellBorderStyle.All = DataGridViewAdvancedCellBorderStyle.Single;
            dgv.SelectionChanged += Dgv_SelectionChanged;

            // binde die zuvor erstellte BindingSource an das Scope-Grid
            dgv.DataSource = binding;

            // Bottom: reservations grid + leases + events (TabControl)
            dgvReservations = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoGenerateColumns = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells, // size to headers and content
                DefaultCellStyle = { WrapMode = DataGridViewTriState.False },
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                RowTemplate = { Height = 22 }, // Fixed row height for better performance
                VirtualMode = false,
                ColumnHeadersVisible = true,
                ColumnHeadersHeight = 32,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing
            };
            dgvReservations.MinimumSize = new Size(200, 120);
            dgvReservations.Resize += (s, e) => {
                try { dgvReservations.ColumnHeadersHeight = Math.Max(32, TextRenderer.MeasureText("Ag", dgvReservations.ColumnHeadersDefaultCellStyle.Font ?? dgvReservations.Font).Height + 10); } catch {}
            };
            dgvReservations.ColumnHeadersDefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
            // Ensure readable headers across DPI/themes
            dgvReservations.EnableHeadersVisualStyles = false;
            dgvReservations.ColumnHeadersDefaultCellStyle.BackColor = SystemColors.Control;
            dgvReservations.ColumnHeadersDefaultCellStyle.ForeColor = SystemColors.ControlText;
            dgvReservations.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            dgvReservations.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
            dgvReservations.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvReservations.MultiSelect = false;
            dgvReservations.RowHeadersVisible = true;
            dgvReservations.GridColor = SystemColors.ControlDark;
            dgvReservations.BackgroundColor = SystemColors.Window;
            dgvReservations.BorderStyle = BorderStyle.FixedSingle;
            dgvReservations.AdvancedColumnHeadersBorderStyle.All = DataGridViewAdvancedCellBorderStyle.Single;
            dgvReservations.AdvancedCellBorderStyle.All = DataGridViewAdvancedCellBorderStyle.Single;
            dgvReservations.CellDoubleClick += DgvReservations_CellDoubleClick;

            // existing inline CellMouseDown selection remains (keeps previous behavior)
            dgvReservations.CellMouseDown += (sender, e) =>
            {
                if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
                {
                    dgvReservations.ClearSelection();
                    dgvReservations.Rows[e.RowIndex].Selected = true;
                    if (dgvReservations.Rows[e.RowIndex].Cells.Count > 0)
                        dgvReservations.CurrentCell = dgvReservations.Rows[e.RowIndex].Cells[0];
                }
            };

            // Delegate context menu setup to the dedicated partial (MainForm.ReservationsHandlers.cs)
            SetupReservationsContextMenu();

            // Create leases grid with explicit (not auto) columns
            dgvLeases = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoGenerateColumns = false, // IMPORTANT: we define columns explicitly
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None, // we compute exact widths after binding
                DefaultCellStyle = { WrapMode = DataGridViewTriState.False },
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                RowTemplate = { Height = 22 }, // Fixed row height for better performance
                ScrollBars = ScrollBars.Vertical, // default, AdjustLeasesColumnWidths will toggle if needed
                VirtualMode = false,
                ColumnHeadersVisible = true,
                ColumnHeadersHeight = 32,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing
            };
            dgvLeases.MinimumSize = new Size(200, 120);
            dgvLeases.Resize += (s, e) => {
                try { dgvLeases.ColumnHeadersHeight = Math.Max(32, TextRenderer.MeasureText("Ag", dgvLeases.ColumnHeadersDefaultCellStyle.Font ?? dgvLeases.Font).Height + 10); } catch {}
            };
            dgvLeases.ColumnHeadersDefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
            // Ensure readable headers across DPI/themes
            dgvLeases.EnableHeadersVisualStyles = false;
            dgvLeases.ColumnHeadersDefaultCellStyle.BackColor = SystemColors.Control;
            dgvLeases.ColumnHeadersDefaultCellStyle.ForeColor = SystemColors.ControlText;
            dgvLeases.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            dgvLeases.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
            dgvLeases.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvLeases.MultiSelect = false;
            dgvLeases.RowHeadersVisible = true;
            dgvLeases.GridColor = SystemColors.ControlDark;
            dgvLeases.BackgroundColor = SystemColors.Window;
            dgvLeases.BorderStyle = BorderStyle.FixedSingle;
            dgvLeases.AdvancedColumnHeadersBorderStyle.All = DataGridViewAdvancedCellBorderStyle.Single;
            dgvLeases.AdvancedCellBorderStyle.All = DataGridViewAdvancedCellBorderStyle.Single;
            dgvLeases.CellMouseDown += DgvLeases_CellMouseDown;

            contextMenuLeases = new ContextMenuStrip();
            contextMenuLeases.Opening += ContextMenuLeases_Opening;
            dgvLeases.ContextMenuStrip = contextMenuLeases;

            // Register a dynamic formatter to replace mask placeholders visually
            dgvLeases.CellFormatting += DgvLeases_CellFormatting;
            
            // Add DataError handler to prevent crashes from column mismatches
            dgvLeases.DataError += DgvLeases_DataError;

            // Ensure explicit columns for leases (maps DataPropertyName => DataTable column name)
            EnsureLeasesColumns();

            // Bind the leases grid to the binding source (bindingLeases is set above)
            dgvLeases.DataSource = bindingLeases;

            // IMPORTANT: run Adjust via SafeBeginInvoke to ensure binding/layout finished
            dgvLeases.DataBindingComplete += (s, e) => SafeBeginInvoke(() => {
                try { 
                    // Delay execution to ensure DataGridView is fully ready
                    System.Threading.Tasks.Task.Delay(100).ContinueWith(_ => {
                        if (!dgvLeases.IsDisposed && dgvLeases.IsHandleCreated) {
                            this.BeginInvoke(new Action(() => {
                                try { AdjustLeasesColumnWidths(); } catch { /* ignore errors */ }
                            }));
                        }
                    });
                } catch { /* ignore errors */ }
            });
            // Throttled resize handler to reduce layout calculations
            System.Windows.Forms.Timer resizeTimer = null;
            dgvLeases.Resize += (s, e) => {
                resizeTimer?.Stop();
                resizeTimer?.Dispose();
                resizeTimer = new System.Windows.Forms.Timer { Interval = 150 };
                resizeTimer.Tick += (ts, te) => {
                    resizeTimer.Stop();
                    resizeTimer.Dispose();
                    SafeBeginInvoke(() => {
                        try { AdjustLeasesColumnWidths(); } catch { /* ignore errors */ }
                    });
                };
                resizeTimer.Start();
            };

            // --- Events tab (neu) ---
            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabReservations = new TabPage("Reservations");
            tabLeases = new TabPage("Leases");
            var tabEvents = new TabPage("Events");

            tabReservations.Controls.Add(dgvReservations);
            tabLeases.Controls.Add(dgvLeases);

            // Events UI - wichtige Reihenfolge: Top-Leiste zuerst, dann DataGridView (Dock-Logik)
            var eventsPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };

            // Verwende ein einfaches Panel für die Top-Leiste statt FlowLayoutPanel
            var eventsTop = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6) };

            // Buttons in einem FlowLayoutPanel, aber dieses ist NICHT AutoSize (verhindert Shrink)
            var eventsTopInner = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = false,
                Height = 32,
                WrapContents = false
            };

            btnFetchEvents = new Button { Text = "Fetch Events", AutoSize = true, Padding = new Padding(6) };
            btnClearEvents = new Button { Text = "Clear", AutoSize = true, Padding = new Padding(6) };

            // Config controls
            nudEventsLookbackDays = new NumericUpDown { Minimum = 1, Maximum = 30, Value = 14, Width = 60, Increment = 1 };
            var lblLookback = new Label { Text = "Days:", AutoSize = true, Padding = new Padding(4, 9, 4, 0) };
            nudEventsMax = new NumericUpDown { Minimum = 50, Maximum = 5000, Value = 200, Width = 80, Increment = 50 };
            var lblMax = new Label { Text = "Max:", AutoSize = true, Padding = new Padding(12, 9, 4, 0) };

            // wire up buttons; show wait dialog during fetch
            btnFetchEvents.Click += async (s, e) =>
            {
                await WaitDialog.RunAsync(this, "Events werden geladen…", async () =>
                {
                    await FetchAndBindEventsAsync();
                });
            };
            btnClearEvents.Click += (s, e) =>
            {
                try { eventsTable?.Rows.Clear(); } catch { }
            };

            eventsTopInner.Controls.Add(btnFetchEvents);
            eventsTopInner.Controls.Add(btnClearEvents);
            eventsTopInner.Controls.Add(lblLookback);
            eventsTopInner.Controls.Add(nudEventsLookbackDays);
            eventsTopInner.Controls.Add(lblMax);
            eventsTopInner.Controls.Add(nudEventsMax);
            eventsTop.Controls.Add(eventsTopInner);
            eventsPanel.Controls.Add(eventsTop);

            // Create dgvEvents AFTER adding top bar so docking keeps header visible
            dgvEvents = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,               // platz sparen
                DefaultCellStyle = { WrapMode = DataGridViewTriState.False }, // Disable text wrapping for performance
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,     // Fixed row height for performance
                RowTemplate = { Height = 22 }, // Fixed row height
                VirtualMode = false,
                ColumnHeadersVisible = true,
                ColumnHeadersHeight = 25,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            };
            dgvEvents.ColumnHeadersDefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
            // Ensure readable headers across DPI/themes
            dgvEvents.EnableHeadersVisualStyles = false;
            dgvEvents.ColumnHeadersDefaultCellStyle.BackColor = SystemColors.Control;
            dgvEvents.ColumnHeadersDefaultCellStyle.ForeColor = SystemColors.ControlText;
            dgvEvents.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            dgvEvents.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;

            dgvEvents.GridColor = SystemColors.ControlDark;
            dgvEvents.BackgroundColor = SystemColors.Window;
            dgvEvents.BorderStyle = BorderStyle.FixedSingle;
            dgvEvents.AdvancedColumnHeadersBorderStyle.All = DataGridViewAdvancedCellBorderStyle.Single;
            dgvEvents.AdvancedCellBorderStyle.All = DataGridViewAdvancedCellBorderStyle.Single;
            // explicit columns - Message column is Fill to take remaining space
            dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "TimeCreated", HeaderText = "Time (UTC)", DataPropertyName = "TimeCreated", Width = 160, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "EntryType", HeaderText = "Type", DataPropertyName = "EntryType", Width = 90, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "Source", HeaderText = "Source", DataPropertyName = "Source", Width = 140, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "InstanceId", HeaderText = "Id", DataPropertyName = "InstanceId", Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "Server", HeaderText = "Server", DataPropertyName = "Server", Width = 140, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "Message", HeaderText = "Message", DataPropertyName = "Message", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 220 });

            // bind events grid to the previously created bindingEvents (placeholder set earlier)
            dgvEvents.DataSource = bindingEvents;

            // slight adjustments after binding/resize
            dgvEvents.DataBindingComplete += (s, e) =>
            {
                try
                {
                    // zwinge Zeilen-Neuberechnung und ensure Message column fill
                    dgvEvents.AutoResizeRows(DataGridViewAutoSizeRowsMode.AllCells);
                    if (dgvEvents.RowTemplate != null) dgvEvents.RowTemplate.Height = Math.Max(20, dgvEvents.RowTemplate.Height);
                }
                catch { /* swallow */ }
            };
            // Throttled resize handler for events grid
            System.Windows.Forms.Timer eventsResizeTimer = null;
            dgvEvents.Resize += (s, e) =>
            {
                eventsResizeTimer?.Stop();
                eventsResizeTimer?.Dispose();
                eventsResizeTimer = new System.Windows.Forms.Timer { Interval = 200 };
                eventsResizeTimer.Tick += (ts, te) => {
                    eventsResizeTimer.Stop();
                    eventsResizeTimer.Dispose();
                    SafeBeginInvoke(AdjustEventsColumnWidths);
                    // Skip AutoResizeRows for better performance - fixed height is set
                };
                eventsResizeTimer.Start();
            };

            // add dgvEvents after Top controls
            eventsPanel.Controls.Add(dgvEvents);

            tabEvents.Controls.Add(eventsPanel);

            tabs.TabPages.Add(tabReservations);
            tabs.TabPages.Add(tabLeases);
            tabs.TabPages.Add(tabEvents);

            // --- Robustheits- und Sichtbarkeits-Helpers für Events ---
            eventsPanel.MinimumSize = new Size(300, 160);

            tabs.SelectedIndexChanged += (s, e) =>
            {
                try
                {
                    if (tabs.SelectedTab == tabEvents)
                    {
                        try
                        {
                            // Stelle sicher, dass der SplitContainer dem Events-Tab genug Platz gibt
                            if (split != null)
                            {
                                split.SplitterDistance = Math.Max(180, (int)(this.ClientSize.Height * 0.45));
                            }
                        }
                        catch { /* ignore */ }
                    }
                }
                catch { /* ignore */ }
            };

            eventsPanel.Resize += (s, e) =>
            {
                try
                {
                    if (eventsTop != null) eventsTop.Height = Math.Max(36, eventsTop.Height);
                    if (dgvEvents != null)
                    {
                        dgvEvents.Dock = DockStyle.Fill;
                        dgvEvents.MinimumSize = new Size(200, 80);
                        dgvEvents.BringToFront();
                    }
                }
                catch { /* swallow */ }
            };

            split.Panel1.Controls.Add(dgv);
            split.Panel2.Controls.Add(tabs);

            content.Controls.Add(split);
            content.ResumeLayout(true);

            // Build a deterministic layout using a TableLayoutPanel (rows: Menu | Header | Status | Progress | Content)
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, AutoSize = false };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));                 // Menu
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, header.Height));  // Header fixed height
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));                 // Status
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));                 // Progress (0 when invisible)
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));            // Content

            // Ensure proper docking inside TLP cells
            menuStrip.Dock = DockStyle.Fill;
            header.Dock = DockStyle.Fill;
            lblStatus.Dock = DockStyle.Fill;
            pb.Dock = DockStyle.Fill;
            content.Dock = DockStyle.Fill;

            layout.Controls.Add(menuStrip, 0, 0);
            layout.Controls.Add(header,   0, 1);
            layout.Controls.Add(lblStatus,0, 2);
            layout.Controls.Add(pb,       0, 3);
            layout.Controls.Add(content,  0, 4);

            // Add the TLP as the single root control
            Controls.Add(layout);
            Controls.SetChildIndex(layout, 0);

            // initialize events table (implementation lives in MainForm.Events.cs)
            InitEventsTable();

            // DEBUG/Fallback: setze Mindesthöhe + initiales Layout-Pass, entfernt werden kann dies später
            try
            {
                if (dgvEvents != null)
                {
                    dgvEvents.MinimumSize = new Size(200, 120);
                    dgvEvents.Visible = true;
                }
                SafeBeginInvoke(() =>
                {
                    try
                    {
                        this.PerformLayout();
                        this.Refresh();
                        if (dgv != null) dgv.Refresh();
                        if (dgvReservations != null) dgvReservations.Refresh();
                        if (dgvLeases != null) dgvLeases.Refresh();
                        if (dgvEvents != null) dgvEvents.Refresh();
                    }
                    catch { }
                });
            }
            catch { }

            // Add resize optimization to reduce flicker and improve performance
            this.ResizeBegin += (s, e) => this.SuspendLayout();
            this.ResizeEnd += (s, e) => this.ResumeLayout(true);
            
            // Resume layout after adding all controls
            this.ResumeLayout(true);
        }
    }
}
