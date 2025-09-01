// ConvertLeaseToReservationDialog.Helpers.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    public partial class ConvertLeaseToReservationDialog
    {
        /// <summary>
        /// Klick auf den Button „IP auswählen“ → öffnet IpPickerForm, abonniert AvailableIpsChanged
        /// und aktualisiert den Dialogtitel mit der Anzahl gefundener freier IPs.
        /// </summary>
        private async void btnPickIp_Click(object? sender, EventArgs e)
        {
            try
            {
                Helpers.WriteDebugLog("IP-Picker: btnPickIp_Click invoked");
                // Nutze die zentrale Picker-Logik, die den Bereich berechnet und
                // Reservations/Leases berücksichtigt (siehe anderer Partial).
                await ShowIpPickerAndApplySelectionAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Fehler beim Öffnen des IP-Pickers:\r\n" + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Setzt txtIp sicher im UI-Thread.
        /// </summary>
        private void SetIpOnUiThread(string ip)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => txtIp.Text = ip));
            }
            else
            {
                txtIp.Text = ip;
            }
        }
    }
}
