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
        private void btnPickIp_Click(object? sender, EventArgs e)
        {
            try
            {
                // TODO: Hier kannst du echte Daten einsetzen.
                // Aktuell Platzhalter für verfügbare IPs (leere Liste).
                IEnumerable<string> availableIps = Enumerable.Empty<string>();

                // Firewall-Bereich aus AppConstants übernehmen
                string firewallStart = AppConstants.InternetAllowedRangeStart.ToString();
                string firewallEnd   = AppConstants.InternetAllowedRangeEnd.ToString();

                // Reservierte Anzahl – hier ggf. durch echte Logik ersetzen
                int reservedCount = 0;

                using (var pickerForm = new IpPickerForm(availableIps, firewallStart, firewallEnd, reservedCount))
                {
                    // Helper-Funktion: liest per Reflection das private ListBox-Feld 'lstIps' und gibt die Item-Anzahl zurück.
                    int GetListCount()
                    {
                        try
                        {
                            var f = pickerForm.GetType().GetField("lstIps", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (f != null)
                            {
                                var lb = f.GetValue(pickerForm) as ListBox;
                                return lb?.Items.Count ?? 0;
                            }

                            // Fallback: durchsuche Controls nach ListBox
                            var ctrlLb = pickerForm.Controls.Cast<Control>().SelectMany(c => c.Controls.Cast<Control>()).OfType<ListBox>().FirstOrDefault();
                            return ctrlLb?.Items.Count ?? 0;
                        }
                        catch
                        {
                            return 0;
                        }
                    }

                    // Event-Handler: update Titel wenn AvailableIpsChanged gefeuert wird
                    EventHandler handler = (s, ea) =>
                    {
                        try
                        {
                            int count = GetListCount();
                            // safe UI update
                            if (pickerForm.InvokeRequired)
                            {
                                pickerForm.Invoke(new Action(() => pickerForm.Text = $"Verfügbare IPs wählen ({count})"));
                            }
                            else
                            {
                                pickerForm.Text = $"Verfügbare IPs wählen ({count})";
                            }
                        }
                        catch
                        {
                            // swallow
                        }
                    };

                    // Abonnieren (vor ShowDialog)
                    pickerForm.AvailableIpsChanged += handler;

                    // Setze initialen Titel (falls die Liste bereits befüllt wurde)
                    try
                    {
                        int initCount = GetListCount();
                        pickerForm.Text = $"Verfügbare IPs wählen ({initCount})";
                    }
                    catch { /* ignore */ }

                    // Modal anzeigen
                    var dr = pickerForm.ShowDialog(this);

                    // Aufräumen: Unsubscribe
                    pickerForm.AvailableIpsChanged -= handler;

                    if (dr == DialogResult.OK)
                    {
                        // SelectedIp ist public property in IpPickerForm
                        var chosen = pickerForm.SelectedIp;
                        if (!string.IsNullOrEmpty(chosen))
                        {
                            SetIpOnUiThread(chosen);
                        }
                    }
                }
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
