// ConvertLeaseToReservationDialog.Api.cs
// Repo: https://github.com/thhering1969/kurzzeit-dhcp-wmiviewer.git
// Branch: fix/contextmenu-direct-call
// **KOMPLETTE DATEI** — einfach kopieren & einfügen
//
// Änderungen:
// - InitializeComponent() wird jetzt direkt aufgerufen (keine stille Unterdrückung von Ausnahmen).
// - Defensive, sichtbare Fehlerdiagnose bei Exception in InitializeComponent.
// - Setze sichere Sichtbarkeits-Defaults nach der Initialisierung.
// - Weiterleitung (forward) von ScopeId/FirewallRange/ReservationLookup an eingebettetes IpPicker-Control.

using System;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using System.Threading.Tasks;
using System.Linq;
using System.Reflection;

namespace DhcpWmiViewer
{
    // COMPLETE FILE
    public partial class ConvertLeaseToReservationDialog : Form
    {
        // Provide safe public properties that map to the designer controls (if present).
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string IpAddress
        {
            get => (txtIp?.Text ?? string.Empty).Trim();
            set { if (txtIp != null) txtIp.Text = value ?? string.Empty; }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string ClientId
        {
            get => (txtClientId?.Text ?? string.Empty).Trim();
            set { if (txtClientId != null) txtClientId.Text = value ?? string.Empty; }
        }

        /// <summary>
        /// Dialog-internen Namen für die Reservation — vermeidet Shadowing von Control.Name.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string ReservationName
        {
            get => (txtName?.Text ?? string.Empty).Trim();
            set { if (txtName != null) txtName.Text = value ?? string.Empty; }
        }

        /// <summary>
        /// Beschreibungs-Text der Reservation. Settable wrapper to avoid readonly properties.
        /// Marked to avoid designer code-serialization warnings (WFO1000).
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string ReservationDescription
        {
            get => (txtDescription?.Text ?? string.Empty).Trim();
            set { if (txtDescription != null) txtDescription.Text = value ?? string.Empty; }
        }

        // Convenience: populate common fields in one call. Returns 'this' to allow inline usage.
        private ConvertLeaseToReservationDialog InitFields(string scopeId = "", string ip = "", string clientId = "", string name = "", string startRange = "", string endRange = "", string subnetMask = "", string prefetchDescription = "")
        {
            try
            {
                // set fields via our wrappers (they check for null controls)
                this.IpAddress = ip ?? string.Empty;
                this.ClientId = clientId ?? string.Empty;
                this.ReservationName = name ?? string.Empty;
                if (!string.IsNullOrEmpty(prefetchDescription)) this.ReservationDescription = prefetchDescription;
            }
            catch { /* defensive */ }

            // --- forward lookup + scope info to embedded IpPicker control (if present) ---
            try
            {
                // Versuche Feld "ipPicker" (private field generated/angelegt) zu finden
                IpPicker? ipPickerCtrl = null;
                var fi = this.GetType().GetField("ipPicker", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (fi != null) ipPickerCtrl = fi.GetValue(this) as IpPicker;

                // Falls nicht als Feld vorhanden: suche control-Tree nach Name "ipPicker"
                if (ipPickerCtrl == null)
                {
                    var found = this.Controls.Find("ipPicker", true).FirstOrDefault();
                    if (found is IpPicker ipc) ipPickerCtrl = ipc;
                }

                if (ipPickerCtrl != null)
                {
                    // Setze Scope / Ranges (nutze hier gegebene Parameter falls gesetzt)
                    try { if (!string.IsNullOrWhiteSpace(scopeId)) ipPickerCtrl.ScopeId = scopeId; } catch { }
                    try { if (!string.IsNullOrWhiteSpace(startRange)) ipPickerCtrl.StartRange = startRange; } catch { }
                    try { if (!string.IsNullOrWhiteSpace(endRange)) ipPickerCtrl.EndRange = endRange; } catch { }

                    // Versuche Firewall-Start/End aus Dialog-Eigenschaften oder Default
                    try
                    {
                        var pFwStart = this.GetType().GetProperty("FirewallStart", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        var pFwEnd = this.GetType().GetProperty("FirewallEnd", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        var fwStart = pFwStart?.GetValue(this) as string;
                        var fwEnd = pFwEnd?.GetValue(this) as string;

                        if (!string.IsNullOrWhiteSpace(fwStart)) ipPickerCtrl.FirewallStart = fwStart;
                        if (!string.IsNullOrWhiteSpace(fwEnd)) ipPickerCtrl.FirewallEnd = fwEnd;
                    }
                    catch { /* ignore */ }

                    // Forward ReservationLookup-Delegate (falls vom MainForm injiziert).
                    try
                    {
                        // Property
                        var pi = this.GetType().GetProperty("ReservationLookup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (pi != null)
                        {
                            var val = pi.GetValue(this);
                            if (val is Func<string, Task<DataTable>> fn) ipPickerCtrl.ReservationLookup = fn;
                        }

                        // Field (fallback)
                        var fi2 = this.GetType().GetField("ReservationLookup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (fi2 != null)
                        {
                            var val2 = fi2.GetValue(this);
                            if (val2 is Func<string, Task<DataTable>> fn2) ipPickerCtrl.ReservationLookup = fn2;
                        }
                    }
                    catch { /* swallow: forward best-effort */ }
                }
                else
                {
                    // Optional: Debug-Log damit man weiß, dass kein ipPicker gefunden wurde
                    try { Helpers.WriteDebugLog("InitFields: no ipPicker control found to wire ReservationLookup/ScopeId."); } catch { }
                }
            }
            catch (Exception ex)
            {
                try { Helpers.WriteDebugLog("InitFields: failed to wire IpPicker: " + ex); } catch { }
            }

            return this;
        }

        // Parameterless constructor (call designer InitializeComponent directly).
        public ConvertLeaseToReservationDialog()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                try
                {
                    MessageBox.Show(null, "FEHLER: InitializeComponent() in ConvertLeaseToReservationDialog hat eine Exception geworfen:\r\n" + ex.ToString(), "Dialog Initialisierung Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch { }
                throw;
            }

            // Defensive defaults
            try { if (this.StartPosition == FormStartPosition.Manual) this.StartPosition = FormStartPosition.CenterParent; } catch { }
            try { this.ShowInTaskbar = true; } catch { }
            try { this.WindowState = FormWindowState.Normal; } catch { }
            try { this.Opacity = 1.0; } catch { }
            try { this.TopMost = false; } catch { }
        }

        // Additional convenience overloads
        public ConvertLeaseToReservationDialog(string scopeId, string ip)
            : this()
        {
            InitFields(scopeId: scopeId, ip: ip);
        }

        public ConvertLeaseToReservationDialog(string scopeId, string ip, string clientId)
            : this()
        {
            InitFields(scopeId: scopeId, ip: ip, clientId: clientId);
        }

        public ConvertLeaseToReservationDialog(string scopeId, string ip, string clientId, string name)
            : this()
        {
            InitFields(scopeId: scopeId, ip: ip, clientId: clientId, name: name);
        }

        public ConvertLeaseToReservationDialog(string scopeId, string ip, string clientId, string name, string startRange, string endRange, string subnetMask)
            : this()
        {
            InitFields(scopeId: scopeId, ip: ip, clientId: clientId, name: name, startRange: startRange, endRange: endRange, subnetMask: subnetMask);
        }

        // catch-all overload with 9 args
        public ConvertLeaseToReservationDialog(string scopeId, string ip, string clientId, string name, string startRange, string endRange, string subnetMask, string prefetchDescription, string dummy = "")
            : this()
        {
            InitFields(scopeId: scopeId, ip: ip, clientId: clientId, name: name, startRange: startRange, endRange: endRange, subnetMask: subnetMask, prefetchDescription: prefetchDescription);
        }

        public (string Ip, string ClientId, string Name, string Description) GetReservationValues()
        {
            return (IpAddress, ClientId, ReservationName, ReservationDescription);
        }
    }
}
