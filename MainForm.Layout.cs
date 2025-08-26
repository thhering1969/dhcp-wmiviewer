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
            var tl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1 };
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260)); // Platz für Export + Debug

            var lbl = new Label { Text = "Server:", Anchor = AnchorStyles.Left, AutoSize = true };
            txtServer = new TextBox { Text = Environment.MachineName, Anchor = AnchorStyles.Left | AnchorStyles.Right };

            btnDiscover = new Button { Text = "Discover", AutoSize = true, Padding = new Padding(6) };
            btnDiscover.Click += BtnDiscover_Click;

            cmbDiscoveredServers = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left | AnchorStyles.Right };
            cmbDiscoveredServers.Width = 250;

            btnQuery = new Button { Text = "Query Scopes", AutoSize = true, Padding = new Padding(6) };
            btnQuery.Click += BtnQuery_Click;

            btnExportCsv = new Button { Text = "Export CSV", AutoSize = true, Padding = new Padding(6) };
            btnExportCsv.Enabled = false;
            btnExportCsv.Click += BtnExportCsv_Click;

            // Debug-Button (zeigt Debug-Fenster für dgvLeases-Spalten)
            var btnDebugColumns = new Button { Text = "Debug cols", AutoSize = true, Padding = new Padding(6) };
            btnDebugColumns.Click += (s, e) => ShowLeasesDebugWindow();

            var pnlTopRight = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
            pnlTopRight.Controls.Add(btnExportCsv);
            pnlTopRight.Controls.Add(btnDebugColumns);

            tl.Controls.Add(lbl, 0, 0);
            tl.Controls.Add(txtServer, 1, 0);
            tl.Controls.Add(btnDiscover, 2, 0);
            tl.Controls.Add(cmbDiscoveredServers, 3, 0);
            tl.Controls.Add(btnQuery, 4, 0);
            tl.Controls.Add(pnlTopRight, 5, 0);

            header.Controls.Add(tl);

            lblStatus = new Label { Dock = DockStyle.Top, Height = 26, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 2, 6, 2) };
            pb = new ProgressBar { Dock = DockStyle.Top, Height = 10, Visible = false };

            var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 300 };

            // Top: scopes grid
            dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoGenerateColumns = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                DefaultCellStyle = { WrapMode = DataGridViewTriState.True },
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells
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
                DefaultCellStyle = { WrapMode = DataGridViewTriState.True },
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells
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
                DefaultCellStyle = { WrapMode = DataGridViewTriState.True },
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                ScrollBars = ScrollBars.Vertical // default, adjust inside AdjustLeasesColumnWidths()
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

            // Ensure explicit columns for leases (maps DataPropertyName => DataTable column name)
            EnsureLeasesColumns();

            // Bind the leases grid to the binding source (bindingLeases is set above)
            dgvLeases.DataSource = bindingLeases;

            // IMPORTANT: run Adjust via SafeBeginInvoke to ensure binding/layout finished
            dgvLeases.DataBindingComplete += (s, e) => SafeBeginInvoke(AdjustLeasesColumnWidths);
            dgvLeases.Resize += (s, e) => SafeBeginInvoke(AdjustLeasesColumnWidths);

            // --- Events tab (neu) ---
            var tabs = new TabControl { Dock = DockStyle.Fill };
            var tabReservations = new TabPage("Reservations");
            var tabLeases = new TabPage("Leases");
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

            // wire up buttons; implementations live in MainForm.Events.cs
            btnFetchEvents.Click += async (s, e) => await FetchAndBindEventsAsync();
            btnClearEvents.Click += (s, e) =>
            {
                try { eventsTable?.Rows.Clear(); } catch { }
            };

            eventsTopInner.Controls.Add(btnFetchEvents);
            eventsTopInner.Controls.Add(btnClearEvents);
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
                DefaultCellStyle = { WrapMode = DataGridViewTriState.True }, // Textumbruch aktivieren
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells     // Zeilenhöhe an Inhalt anpassen
            };
            dgvEvents.ColumnHeadersDefaultCellStyle.Font = new Font(Font, FontStyle.Bold);

            // explicit columns - Message column is Fill to take remaining space
            dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "TimeCreated", HeaderText = "Time (UTC)", DataPropertyName = "TimeCreated", Width = 160, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "EntryType", HeaderText = "Type", DataPropertyName = "EntryType", Width = 90, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "Source", HeaderText = "Source", DataPropertyName = "Source", Width = 140, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            dgvEvents.Columns.Add(new DataGridViewTextBoxColumn { Name = "InstanceId", HeaderText = "Id", DataPropertyName = "InstanceId", Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
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
            dgvEvents.Resize += (s, e) =>
            {
                SafeBeginInvoke(AdjustEventsColumnWidths);
                SafeBeginInvoke(() => dgvEvents.AutoResizeRows(DataGridViewAutoSizeRowsMode.AllCells));
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

            // Resume layout after adding all controls
            this.ResumeLayout(true);
        }
    }
}
