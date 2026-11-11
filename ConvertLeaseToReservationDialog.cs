// ConvertLeaseToReservationDialog.cs
using static DhcpWmiViewer.Loc;
using System.ComponentModel;
using System.Data;
using System.Net;

namespace DhcpWmiViewer
{
    /// <summary>
    /// Dialog zum Anlegen einer Reservation aus einer Lease.
    /// Der Aufrufer setzt die Felder über die Properties; über "IP auswählen" kann eine freie IP
    /// aus <see cref="AllowedRanges"/> (Subnetz des Scopes; internetfähige Adressen markiert) gewählt werden.
    /// </summary>
    public partial class ConvertLeaseToReservationDialog : Form
    {
        public ConvertLeaseToReservationDialog()
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterParent;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string IpAddress
        {
            get => txtIp.Text.Trim();
            set => txtIp.Text = value ?? string.Empty;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string ClientId
        {
            get => txtClientId.Text.Trim();
            set => txtClientId.Text = value ?? string.Empty;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string HostName
        {
            get => txtName.Text.Trim();
            set => txtName.Text = value ?? string.Empty;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Description
        {
            get => txtDescription.Text.Trim();
            set => txtDescription.Text = value ?? string.Empty;
        }

        /// <summary>Scope, für den Reservations/Leases nachgeladen werden (falls nicht vorab übergeben).</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string ScopeId { get; set; } = string.Empty;

        /// <summary>Adressraum des Scopes: freie IPs werden aus dem Subnetz angeboten, internetfähige markiert.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ScopeAddressSpace? AddressSpace { get; set; }

        /// <summary>Bereits geladene Reservations (bevorzugt vor <see cref="ReservationLookup"/>).</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DataTable? PrefetchedReservations { get; set; }

        /// <summary>Bereits geladene Leases.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DataTable? PrefetchedLeases { get; set; }

        /// <summary>Lädt Reservations für einen Scope nach, falls keine vorab übergeben wurden.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Func<string, Task<DataTable>>? ReservationLookup { get; set; }

        private async void btnPickIp_Click(object? sender, EventArgs e)
        {
            try
            {
                await ShowIpPickerAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, T("Fehler beim Öffnen der IP-Auswahl:", "Error opening the IP picker:") + "\r\n" + ex.Message, T("Fehler", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ShowIpPickerAsync()
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var reservations = PrefetchedReservations;
            if (reservations == null && ReservationLookup != null && !string.IsNullOrWhiteSpace(ScopeId))
            {
                try { reservations = await ReservationLookup(ScopeId); }
                catch (Exception ex) { Helpers.WriteDebugLog("IP-Picker: reservation lookup failed: " + ex.Message); }
            }
            AddIpAddresses(reservations, used);
            AddIpAddresses(PrefetchedLeases, used);

            var space = AddressSpace;
            if (space == null || space.Subnet.IsEmpty)
            {
                MessageBox.Show(this, T("Der Adressbereich dieses Scopes konnte nicht ermittelt werden.", "The address range of this scope could not be determined."), T("IP-Auswahl", "IP picker"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var available = space.Subnet.GetAvailable(used);
            Helpers.WriteDebugLog($"IP-Picker: used={used.Count}, available={available.Count}, subnet={space.Subnet.Display}, internet={space.Internet.Display}");

            using var picker = new IpPickerForm(available, space, used.Count);
            if (picker.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(picker.SelectedIp))
                return;

            txtIp.Text = picker.SelectedIp;
            MessageBox.Show(this, T("IP ausgewählt. Bitte mit OK bestätigen, um die Reservation anzulegen.", "IP selected. Confirm with OK to create the reservation."), T("Hinweis", "Note"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Übernimmt die IPs aus Spalte "IPAddress" (bzw. "IP" oder der ersten Spalte) normalisiert in <paramref name="target"/>.
        /// </summary>
        private static void AddIpAddresses(DataTable? table, HashSet<string> target)
        {
            if (table == null || table.Columns.Count == 0) return;

            var col = table.Columns.Contains("IPAddress") ? table.Columns["IPAddress"]
                    : table.Columns.Contains("IP") ? table.Columns["IP"]
                    : table.Columns[0];

            foreach (DataRow row in table.Rows)
            {
                var text = row[col!]?.ToString()?.Trim();
                if (string.IsNullOrEmpty(text)) continue;
                target.Add(IPAddress.TryParse(text, out var ip) ? ip.ToString() : text);
            }
        }
    }
}
