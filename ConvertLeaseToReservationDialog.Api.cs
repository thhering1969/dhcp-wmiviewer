// ConvertLeaseToReservationDialog.Api.cs
// Repo: https://github.com/thhering1969/kurzzeit-dhcp-wmiviewer.git
// Branch: fix/contextmenu-direct-call
// **KOMPLETTE DATEI** — einfach kopieren & einfügen
//
// Änderungen:
// - InitializeComponent() wird jetzt direkt aufgerufen (keine stille Unterdrückung von Ausnahmen).
// - Defensive, sichtbare Fehlerdiagnose bei Exception in InitializeComponent.
// - Setze sichere Sichtbarkeits-Defaults nach der Initialisierung.

using System;
using System.ComponentModel;
using System.Windows.Forms;

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

            return this;
        }

        // Parameterless constructor (call designer InitializeComponent directly).
        public ConvertLeaseToReservationDialog()
        {
            // IMPORTANT: call the designer-generated InitializeComponent directly.
            // Do not swallow exceptions here; surface them so we can see if designer init fails.
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                // Make the error visible immediately so we don't end up with a silently broken dialog.
                // Use a null owner so message is always visible even if Form isn't fully constructed.
                try
                {
                    MessageBox.Show(null, "FEHLER: InitializeComponent() in ConvertLeaseToReservationDialog hat eine Exception geworfen:\r\n" + ex.ToString(), "Dialog Initialisierung Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch
                {
                    // ignore MessageBox failures, rethrow afterwards
                }

                // Re-throw so upstream callers (MainForm) can catch and show details; prevents using a half-initialized form.
                throw;
            }

            // Defensive defaults to avoid off-screen/hidden behaviour even if designer set odd values.
            try { if (this.StartPosition == FormStartPosition.Manual) this.StartPosition = FormStartPosition.CenterParent; } catch { }
            try { this.ShowInTaskbar = true; } catch { }
            try { this.WindowState = FormWindowState.Normal; } catch { }
            try { this.Opacity = 1.0; } catch { }
            try { this.TopMost = false; } catch { } // keep normal z-order by default
        }

        // Additional convenience overloads -> call parameterless ctor and then initialize fields
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

        // catch-all overload with 9 args (some call sites used 9 args in your project)
        public ConvertLeaseToReservationDialog(string scopeId, string ip, string clientId, string name, string startRange, string endRange, string subnetMask, string prefetchDescription, string dummy = "")
            : this()
        {
            InitFields(scopeId: scopeId, ip: ip, clientId: clientId, name: name, startRange: startRange, endRange: endRange, subnetMask: subnetMask, prefetchDescription: prefetchDescription);
        }

        // Optionally add a public method to transfer values from the dialog to a DTO or similar.
        public (string Ip, string ClientId, string Name, string Description) GetReservationValues()
        {
            return (IpAddress, ClientId, ReservationName, ReservationDescription);
        }
    }
}
