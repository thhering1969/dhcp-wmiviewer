// ConvertLeaseToReservationDialog.cs
using System;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using System.Threading.Tasks;

namespace DhcpWmiViewer
{
    // Diese Partial-Datei stellt die öffentliche API (Properties/Constructors/InitFields)
    // zur Verfügung — genau EINE Stelle im Projekt definiert diese Member, um Duplicate-Definitionen zu vermeiden.
    public partial class ConvertLeaseToReservationDialog : Form
    {
        // Parameterless constructor (Designer)
        public ConvertLeaseToReservationDialog()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                try { MessageBox.Show(null, "FEHLER: InitializeComponent() hat eine Exception geworfen:\r\n" + ex.ToString(), "Dialog Initialisierung Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
                throw;
            }

            // Defensive defaults:
            try { if (this.StartPosition == FormStartPosition.Manual) this.StartPosition = FormStartPosition.CenterParent; } catch { }
            try { this.ShowInTaskbar = true; } catch { }
            try { this.WindowState = FormWindowState.Normal; } catch { }
            try { this.Opacity = 1.0; } catch { }
            try { this.TopMost = false; } catch { }
        }

        // -------- Public properties (single authoritative definition) --------
        // Marked to avoid WinForms designer code-serialization warnings (WFO1000)

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public string IpAddress
        {
            get => txtIp?.Text.Trim() ?? string.Empty;
            set { if (txtIp != null) txtIp.Text = value ?? string.Empty; }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public string ClientId
        {
            get => txtClientId?.Text.Trim() ?? string.Empty;
            set { if (txtClientId != null) txtClientId.Text = value ?? string.Empty; }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public string HostName
        {
            get => txtName?.Text.Trim() ?? string.Empty;
            set { if (txtName != null) txtName.Text = value ?? string.Empty; }
        }

        // Alias to avoid shadowing Form.Name
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public string ReservationHostName
        {
            get => HostName;
            set => HostName = value;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public string Description
        {
            get => txtDescription?.Text.Trim() ?? string.Empty;
            set { if (txtDescription != null) txtDescription.Text = value ?? string.Empty; }
        }

        // Optional delegates that callers (MainForm) can set by reflection or directly.
        // Use the same signature as most of your code expects: Func<string, Task<DataTable>>
        // (keeps compatibility).
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public Func<string, Task<DataTable>>? ReservationLookup { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public Func<string, Task<DataTable>>? LeaseLookup { get; set; }

        // Optionale bereits vorab geladene Tabellen (vom aufrufenden MainForm übergeben)
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public DataTable? PrefetchedReservations { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public DataTable? PrefetchedLeases { get; set; }

        /// <summary>
        /// Convenience: populate common fields in one call (callable after construction).
        /// Returns 'this' to allow fluent style.
        /// </summary>
        public ConvertLeaseToReservationDialog InitFields(
            string scopeId = "",
            string ip = "",
            string clientId = "",
            string name = "",
            string startRange = "",
            string endRange = "",
            string subnetMask = "",
            string prefetchDescription = "")
        {
            try
            {
                // set via properties (they guard nulls)
                try { this.IpAddress = ip ?? string.Empty; } catch { }
                try { this.ClientId = clientId ?? string.Empty; } catch { }
                try { this.HostName = name ?? string.Empty; } catch { }
                if (!string.IsNullOrEmpty(prefetchDescription))
                {
                    try { this.Description = prefetchDescription; } catch { }
                }
            }
            catch { /* defensive */ }

            // best-effort: forward simple scope/range values to an embedded ipPicker control if present
            try
            {
                var found = this.Controls.Find("ipPicker", true);
                if (found != null && found.Length > 0 && found[0] is IpPicker ipPickerCtrl)
                {
                    try { if (!string.IsNullOrWhiteSpace(scopeId)) ipPickerCtrl.ScopeId = scopeId; } catch { }
                    try { if (!string.IsNullOrWhiteSpace(startRange)) ipPickerCtrl.StartRange = startRange; } catch { }
                    try { if (!string.IsNullOrWhiteSpace(endRange)) ipPickerCtrl.EndRange = endRange; } catch { }

                    // forward delegates if they exist and match signature
                    try { if (this.ReservationLookup != null) ipPickerCtrl.ReservationLookup = this.ReservationLookup; } catch { }
                    try { if (this.LeaseLookup != null) ipPickerCtrl.LeaseLookup = this.LeaseLookup; } catch { }
                }
            }
            catch { /* swallow */ }

            return this;
        }

        /// <summary>
        /// Returns the current reservation values (convenience).
        /// </summary>
        public (string Ip, string ClientId, string Name, string Description) GetReservationValues()
        {
            return (this.IpAddress, this.ClientId, this.HostName, this.Description);
        }
    }
}
