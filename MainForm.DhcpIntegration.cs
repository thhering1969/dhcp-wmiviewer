// MainForm.DhcpIntegration.cs
using System;
using System.Data;
using System.Linq;

namespace DhcpWmiViewer
{
    public partial class MainForm
    {
        /// <summary>
        /// Prüft, ob ein Computer eine DHCP-Reservation hat
        /// </summary>
        public string CheckComputerReservation(string computerName)
        {
            try
            {
                if (reservationTable == null || reservationTable.Rows.Count == 0)
                    return "";

                // Suche nach Computer-Namen in verschiedenen Spalten (auch FQDN-Matches)
                var reservations = reservationTable.AsEnumerable()
                    .Where(row => 
                        (!row.IsNull("Name") && IsComputerNameMatch(row.Field<string>("Name"), computerName)) ||
                        (!row.IsNull("Description") && IsComputerNameMatch(row.Field<string>("Description"), computerName)) ||
                        (!row.IsNull("HostName") && IsComputerNameMatch(row.Field<string>("HostName"), computerName)))
                    .ToList();

                if (reservations.Count == 0)
                    return "";

                // Erste gefundene Reservation verwenden
                var reservation = reservations.First();
                var ipAddress = reservation.Field<string>("IPAddress") ?? "Unknown IP";
                var clientId = reservation.Field<string>("ClientId") ?? "Unknown MAC";
                
                return $"{ipAddress} (MAC: {clientId})";
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("Error checking reservation for {0}: {1}", computerName, ex.Message);
                return "";
            }
        }

        /// <summary>
        /// Prüft, ob ein Computer eine DHCP-Lease hat
        /// </summary>
        public string CheckComputerLease(string computerName)
        {
            try
            {
                if (leaseTable == null || leaseTable.Rows.Count == 0)
                {
                    DebugLogger.LogFormat("CheckComputerLease: No lease table data for {0}", computerName);
                    return "";
                }

                DebugLogger.LogFormat("CheckComputerLease: Searching for {0} in {1} lease entries", computerName, leaseTable.Rows.Count);

                // Suche nach Computer-Namen in verschiedenen Spalten (auch FQDN-Matches)
                var leases = leaseTable.AsEnumerable()
                    .Where(row => 
                        (!row.IsNull("HostName") && IsComputerNameMatch(row.Field<string>("HostName"), computerName)) ||
                        (!row.IsNull("Description") && IsComputerNameMatch(row.Field<string>("Description"), computerName)))
                    .ToList();

                DebugLogger.LogFormat("CheckComputerLease: Found {0} matching leases for {1}", leases.Count, computerName);

                if (leases.Count == 0)
                    return "";

                // Erste gefundene Lease verwenden
                var lease = leases.First();
                var ipAddress = lease.Field<string>("IPAddress") ?? "Unknown IP";
                var clientId = lease.Field<string>("ClientId") ?? "Unknown MAC";
                var expiryTime = lease.Field<string>("LeaseExpiryTime") ?? "Unknown";
                var addressState = lease.Field<string>("AddressState") ?? "Unknown";
                
                return $"{ipAddress} (MAC: {clientId}, State: {addressState}, Expires: {expiryTime})";
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("Error checking lease for {0}: {1}", computerName, ex.Message);
                return "";
            }
        }

        /// <summary>
        /// Prüft, ob ein Computer-Name mit einem DHCP-Eintrag übereinstimmt (auch FQDN)
        /// </summary>
        private bool IsComputerNameMatch(string? dhcpValue, string computerName)
        {
            if (string.IsNullOrEmpty(dhcpValue) || string.IsNullOrEmpty(computerName))
                return false;

            // Exakte Übereinstimmung (case-insensitive)
            if (dhcpValue.Equals(computerName, StringComparison.OrdinalIgnoreCase))
                return true;

            // FQDN-Match: "PCWin11Test.goevb.de" matches "PCWin11Test"
            if (dhcpValue.StartsWith(computerName + ".", StringComparison.OrdinalIgnoreCase))
                return true;

            // Reverse-Match: "PCWin11Test" matches "PCWin11Test.goevb.de"
            if (computerName.StartsWith(dhcpValue + ".", StringComparison.OrdinalIgnoreCase))
                return true;

            // Enthält Computer-Namen (für Description-Felder)
            if (dhcpValue.IndexOf(computerName, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        /// <summary>
        /// Initialisiert die DHCP-Integration für AD-Tooltips
        /// </summary>
        private void InitializeDhcpIntegration()
        {
            try
            {
                // Setze MainForm-Referenz für ADTreeItem
                ADTreeItem.SetMainFormReference(this);
                DebugLogger.Log("DHCP Integration for AD tooltips initialized");
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("Error initializing DHCP integration: {0}", ex.Message);
            }
        }
    }
}