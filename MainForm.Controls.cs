// MainForm.Controls.cs

#nullable enable
using System.Data;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    // Nur Feld-/Control-Deklarationen (zentrale Stelle).
    public partial class MainForm
    {
        // Header / Top controls
        private TextBox txtServer = null!;
        private ComboBox cmbDiscoveredServers = null!;
        private Button btnDiscover = null!;
        private Button btnQuery = null!;
        private Button btnExportCsv = null!;
        private Label lblStatus = null!;
        private ProgressBar pb = null!;

        // Scope grid
        private DataGridView dgv = null!;

        // Reservations and Leases & Events
        private DataGridView dgvReservations = null!;
        private DataGridView dgvLeases = null!;
        private DataGridView dgvEvents = null!;

        // Binding sources / tables (UI-related)
        private BindingSource binding = null!;
        private BindingSource bindingReservations = null!;
        private BindingSource bindingLeases = null!;
        private BindingSource bindingEvents = null!;

        // Events table (data)
        private DataTable eventsTable = null!;

        // Context menus & event buttons
        private ContextMenuStrip contextMenuLeases = null!;
        private Button btnFetchEvents = null!;
        private Button btnClearEvents = null!;
        private NumericUpDown nudEventsLookbackDays = null!;
        private NumericUpDown nudEventsMax = null!;
        
        // Tab references for dynamic text updates
        private TabPage tabLeases = null!;
        private TabPage tabReservations = null!;
    }
}
