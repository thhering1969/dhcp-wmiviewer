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

            var header = new Panel { Dock = DockStyle.Top, Padding = new Padding(8), Height = 76 };
            
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

            lblStatus = new Label { Dock = DockStyle.Top, Height = 26, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 2, 6, 2) };
            pb = new ProgressBar { Dock = DockStyle.Top, Height = 10, Visible = false };

            var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            var split = new SplitContainer 
            { 
                Dock = DockStyle.Fill, 
                Orientation = Orientation.Horizontal, 
                SplitterDistance = 35,
                FixedPanel = FixedPanel.None,
                IsSplitterFixed = false,
                SplitterWidth = 4
            };

            // Top: scopes grid
            dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoGenerateColumns = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                DefaultCellStyle = { WrapMode = DataGridViewTriState.False },
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                RowTemplate = { Height = 22 }, // Fixed row height for better performance
                VirtualMode = false
            };
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect = false;
            dgv.RowHeadersVisible = true;
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
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                DefaultCellStyle = { WrapMode = DataGridViewTriState.False },
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                RowTemplate = { Height = 22 }, // Fixed row height for better performance
                VirtualMode = false
            };
            dgvReservations.ColumnHeadersDefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
            dgvReservations.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvReservations.MultiSelect = false;
            dgvReservations.RowHeadersVisible = true;
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
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                DefaultCellStyle = { WrapMode = DataGridViewTriState.False },
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                RowTemplate = { Height = 22 }, // Fixed row height for better performance
                ScrollBars = ScrollBars.Vertical, // default, adjust inside AdjustLeasesColumnWidths()
                VirtualMode = false
            };
            dgvLeases.ColumnHeadersDefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
            dgvLeases.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvLeases.MultiSelect = false;
            dgvLeases.RowHeadersVisible = true;
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
                VirtualMode = false
            };
            dgvEvents.ColumnHeadersDefaultCellStyle.Font = new Font(Font, FontStyle.Bold);

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

            Controls.Add(content);
            Controls.Add(pb);
            Controls.Add(lblStatus);
            Controls.Add(header);

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
