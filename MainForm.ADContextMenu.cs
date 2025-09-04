// MainForm.ADContextMenu.cs
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    public partial class MainForm
    {
        /// <summary>
        /// Erweitert das bestehende AD Context-Menü um DHCP-spezifische Funktionen
        /// </summary>
        private void SetupADContextMenuDhcpIntegration(ContextMenuStrip contextMenuAD)
        {
            try
            {
                // Separator vor DHCP-Funktionen
                var separator = new ToolStripSeparator();
                contextMenuAD.Items.Add(separator);

                // DHCP-spezifische Menüpunkte
                var menuItemConvertLease = new ToolStripMenuItem("🔄 Convert Lease to Reservation");
                var menuItemChangeReservation = new ToolStripMenuItem("⚙️ Change Reservation");
                var menuItemShowDhcpInfo = new ToolStripMenuItem("ℹ️ Show DHCP Info");

                contextMenuAD.Items.Add(menuItemConvertLease);
                contextMenuAD.Items.Add(menuItemChangeReservation);
                contextMenuAD.Items.Add(menuItemShowDhcpInfo);

                // Event Handler für DHCP-Menüpunkte
                menuItemConvertLease.Click += async (s, e) => await OnConvertLeaseToReservation();
                menuItemChangeReservation.Click += async (s, e) => await OnChangeComputerReservation();
                menuItemShowDhcpInfo.Click += async (s, e) => await OnShowComputerDhcpInfo();

                // Context-Menü Opening Event - zeigt nur relevante Menüpunkte
                contextMenuAD.Opening += async (s, e) => await OnADContextMenuOpening(contextMenuAD);
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("Error setting up AD context menu DHCP integration: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Wird aufgerufen bevor Context-Menü geöffnet wird - konfiguriert DHCP-Menüpunkte dynamisch
        /// </summary>
        private async Task OnADContextMenuOpening(ContextMenuStrip contextMenu)
        {
            try
            {
                // Hole Computer-Node
                var selectedNode = treeViewAD?.SelectedNode;
                var computerItem = selectedNode?.Tag as ADTreeItem;
                
                // Standard: Alle DHCP-Menüpunkte ausblenden
                var convertLeaseItem = contextMenu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(x => x.Text.Contains("Convert Lease"));
                var changeReservationItem = contextMenu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(x => x.Text.Contains("Change Reservation"));
                var showInfoItem = contextMenu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(x => x.Text.Contains("DHCP Info"));

                if (convertLeaseItem != null) convertLeaseItem.Visible = false;
                if (changeReservationItem != null) changeReservationItem.Visible = false;
                if (showInfoItem != null) showInfoItem.Visible = false;

                // Nur für Computer-Nodes
                if (computerItem == null || !computerItem.IsComputer)
                    return;

                // Quick DHCP Status Check (ohne UI-Blocking)
                var dhcpStatus = await GetComputerDhcpStatusAsync(computerItem.Name);
                
                // Menüpunkte basierend auf DHCP-Status konfigurieren
                if (dhcpStatus.HasLease && convertLeaseItem != null)
                {
                    convertLeaseItem.Visible = true;
                    convertLeaseItem.Text = $"🔄 Convert Lease to Reservation ({dhcpStatus.LeaseIP})";
                }

                if (dhcpStatus.HasReservation && changeReservationItem != null)
                {
                    changeReservationItem.Visible = true;
                    changeReservationItem.Text = $"⚙️ Change Reservation ({dhcpStatus.ReservationIP})";
                }

                if (showInfoItem != null && (dhcpStatus.HasLease || dhcpStatus.HasReservation))
                {
                    showInfoItem.Visible = true;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("Error in AD context menu opening: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Konvertiert Lease zu Reservation für den ausgewählten Computer
        /// </summary>
        private async Task OnConvertLeaseToReservation()
        {
            try
            {
                var selectedNode = treeViewAD?.SelectedNode;
                var computerItem = selectedNode?.Tag as ADTreeItem;
                
                if (computerItem == null || !computerItem.IsComputer)
                {
                    MessageBox.Show("Please select a computer first.", "Convert Lease to Reservation", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                UpdateStatus($"Finding lease for computer '{computerItem.Name}'...");
                
                // Finde Lease für diesen Computer
                var dhcpStatus = await GetComputerDhcpStatusAsync(computerItem.Name);
                if (!dhcpStatus.HasLease)
                {
                    MessageBox.Show($"No lease found for computer '{computerItem.Name}'.", "Convert Lease to Reservation", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    UpdateStatus("Ready.");
                    return;
                }

                // Öffne Convert Dialog mit gefundener Lease
                using var convertDialog = new ConvertLeaseToReservationDialog();
                
                // Pre-populate Dialog mit Computer-Daten
                convertDialog.PopulateFromLeaseData(computerItem.Name, dhcpStatus.LeaseIP, dhcpStatus.MAC, dhcpStatus.ScopeId);
                
                if (convertDialog.ShowDialog(this) == DialogResult.OK)
                {
                    UpdateStatus("Lease successfully converted to reservation.");
                    
                    // Refresh DHCP-Daten falls möglich
                    if (!string.IsNullOrEmpty(dhcpStatus.ScopeId))
                    {
                        await TryInvokeRefreshReservations(dhcpStatus.ScopeId);
                    }
                }
                else
                {
                    UpdateStatus("Convert operation cancelled.");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error converting lease: {ex.Message}");
                DebugLogger.LogFormat("Error in OnConvertLeaseToReservation: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Ändert Reservation für den ausgewählten Computer
        /// </summary>
        private async Task OnChangeComputerReservation()
        {
            try
            {
                var selectedNode = treeViewAD?.SelectedNode;
                var computerItem = selectedNode?.Tag as ADTreeItem;
                
                if (computerItem == null || !computerItem.IsComputer)
                {
                    MessageBox.Show("Please select a computer first.", "Change Reservation", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                UpdateStatus($"Finding reservation for computer '{computerItem.Name}'...");
                
                // Finde Reservation für diesen Computer
                var dhcpStatus = await GetComputerDhcpStatusAsync(computerItem.Name);
                if (!dhcpStatus.HasReservation)
                {
                    MessageBox.Show($"No reservation found for computer '{computerItem.Name}'.", "Change Reservation", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    UpdateStatus("Ready.");
                    return;
                }

                // Öffne Change Reservation Dialog
                using var changeDialog = new ChangeReservationDialog(
                    dhcpStatus.ReservationIP, 
                    dhcpStatus.ReservationName ?? computerItem.Name, 
                    dhcpStatus.ReservationDescription ?? "",
                    dhcpStatus.ScopeStart, 
                    dhcpStatus.ScopeEnd, 
                    dhcpStatus.ScopeMask);

                if (changeDialog.ShowDialog(this) == DialogResult.OK)
                {
                    // Führe Änderung aus
                    var server = GetServerNameOrDefault();
                    var success = await UpdateComputerReservation(server, dhcpStatus.ScopeId, dhcpStatus.MAC, changeDialog);
                    
                    if (success)
                    {
                        UpdateStatus("Reservation successfully updated.");
                        
                        // Refresh DHCP-Daten
                        await TryInvokeRefreshReservations(dhcpStatus.ScopeId);
                    }
                    else
                    {
                        UpdateStatus("Failed to update reservation.");
                    }
                }
                else
                {
                    UpdateStatus("Change operation cancelled.");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error changing reservation: {ex.Message}");
                DebugLogger.LogFormat("Error in OnChangeComputerReservation: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Zeigt DHCP-Informationen für den ausgewählten Computer
        /// </summary>
        private async Task OnShowComputerDhcpInfo()
        {
            try
            {
                var selectedNode = treeViewAD?.SelectedNode;
                var computerItem = selectedNode?.Tag as ADTreeItem;
                
                if (computerItem == null || !computerItem.IsComputer)
                {
                    MessageBox.Show("Please select a computer first.", "DHCP Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                UpdateStatus($"Gathering DHCP info for '{computerItem.Name}'...");
                
                var dhcpStatus = await GetComputerDhcpStatusAsync(computerItem.Name);
                
                var info = $"DHCP Information for Computer: {computerItem.Name}\n\n";
                
                if (dhcpStatus.HasLease)
                {
                    info += $"📋 LEASE INFORMATION:\n";
                    info += $"  IP Address: {dhcpStatus.LeaseIP}\n";
                    info += $"  MAC Address: {dhcpStatus.MAC}\n";
                    info += $"  Scope: {dhcpStatus.ScopeId}\n";
                    info += $"  Server: {dhcpStatus.Server}\n\n";
                }
                
                if (dhcpStatus.HasReservation)
                {
                    info += $"📌 RESERVATION INFORMATION:\n";
                    info += $"  IP Address: {dhcpStatus.ReservationIP}\n";
                    info += $"  Name: {dhcpStatus.ReservationName}\n";
                    info += $"  Description: {dhcpStatus.ReservationDescription}\n";
                    info += $"  Scope: {dhcpStatus.ScopeId}\n\n";
                }
                
                if (!dhcpStatus.HasLease && !dhcpStatus.HasReservation)
                {
                    info += "No DHCP lease or reservation found for this computer.\n";
                }

                MessageBox.Show(info, $"DHCP Info - {computerItem.Name}", MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateStatus("Ready.");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error gathering DHCP info: {ex.Message}");
                DebugLogger.LogFormat("Error in OnShowComputerDhcpInfo: {0}", ex.Message);
            }
        }

        /// <summary>
        /// DHCP-Status für einen Computer
        /// </summary>
        public class ComputerDhcpStatus
        {
            public bool HasLease { get; set; }
            public bool HasReservation { get; set; }
            
            // Lease-Daten
            public string LeaseIP { get; set; } = "";
            public string MAC { get; set; } = "";
            
            // Reservation-Daten  
            public string ReservationIP { get; set; } = "";
            public string ReservationName { get; set; } = "";
            public string ReservationDescription { get; set; } = "";
            
            // Gemeinsame Daten
            public string ScopeId { get; set; } = "";
            public string Server { get; set; } = "";
            public string ScopeStart { get; set; } = "";
            public string ScopeEnd { get; set; } = "";
            public string ScopeMask { get; set; } = "";
        }

        /// <summary>
        /// Ermittelt DHCP-Status (Lease/Reservation) für einen Computer
        /// </summary>
        private async Task<ComputerDhcpStatus> GetComputerDhcpStatusAsync(string computerName)
        {
            var status = new ComputerDhcpStatus();
            
            try
            {
                var server = GetServerNameOrDefault();
                status.Server = server;
                
                // Durchsuche alle Scopes nach diesem Computer
                var scopeTable = await DhcpManager.QueryScopesAsync(server, s =>
                {
                    // Verwende gecachte Credentials falls vorhanden
                    var normalizedServer = s.Trim().ToLowerInvariant();
                    if (_integratedAuthServers.Contains(normalizedServer))
                        return null;
                    if (_credentialCache.TryGetValue(normalizedServer, out var cachedCred))
                        return cachedCred;
                    return null;
                });

                if (scopeTable?.Rows.Count > 0)
                {
                    // Durchsuche jeden Scope nach Leases und Reservations
                    foreach (DataRow scopeRow in scopeTable.Rows)
                    {
                        var scopeId = scopeRow["ScopeId"]?.ToString() ?? "";
                        if (string.IsNullOrEmpty(scopeId)) continue;

                        // Hole Scope-Details für späteren Gebrauch
                        if (string.IsNullOrEmpty(status.ScopeStart))
                        {
                            status.ScopeStart = scopeRow["StartRange"]?.ToString() ?? "";
                            status.ScopeEnd = scopeRow["EndRange"]?.ToString() ?? "";
                            status.ScopeMask = scopeRow["SubnetMask"]?.ToString() ?? "";
                        }

                        // Suche in Leases
                        if (!status.HasLease)
                        {
                            var leaseStatus = await FindComputerInLeases(server, scopeId, computerName);
                            if (leaseStatus.Found)
                            {
                                status.HasLease = true;
                                status.LeaseIP = leaseStatus.IP;
                                status.MAC = leaseStatus.MAC;
                                status.ScopeId = scopeId;
                            }
                        }

                        // Suche in Reservations
                        if (!status.HasReservation)
                        {
                            var reservationStatus = await FindComputerInReservations(server, scopeId, computerName);
                            if (reservationStatus.Found)
                            {
                                status.HasReservation = true;
                                status.ReservationIP = reservationStatus.IP;
                                status.ReservationName = reservationStatus.Name;
                                status.ReservationDescription = reservationStatus.Description;
                                status.ScopeId = scopeId;
                                if (string.IsNullOrEmpty(status.MAC)) status.MAC = reservationStatus.MAC;
                            }
                        }

                        // Optimierung: Stoppe wenn beide gefunden
                        if (status.HasLease && status.HasReservation) break;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("Error getting computer DHCP status for '{0}': {1}", computerName, ex.Message);
            }
            
            return status;
        }

        /// <summary>
        /// Sucht Computer in Leases eines Scopes
        /// </summary>
        private async Task<(bool Found, string IP, string MAC)> FindComputerInLeases(string server, string scopeId, string computerName)
        {
            try
            {
                var leaseTable = await DhcpManager.QueryLeasesAsync(server, scopeId, s =>
                {
                    var normalizedServer = s.Trim().ToLowerInvariant();
                    if (_integratedAuthServers.Contains(normalizedServer))
                        return null;
                    if (_credentialCache.TryGetValue(normalizedServer, out var cachedCred))
                        return cachedCred;
                    return null;
                }, 1000); // Limit für Performance

                if (leaseTable?.Rows.Count > 0)
                {
                    foreach (DataRow row in leaseTable.Rows)
                    {
                        var hostName = row["HostName"]?.ToString() ?? "";
                        
                        // Vergleiche Computer-Name (case-insensitive, mit/ohne Domain)
                        if (string.Equals(hostName, computerName, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(hostName.Split('.')[0], computerName, StringComparison.OrdinalIgnoreCase))
                        {
                            return (true, row["IPAddress"]?.ToString() ?? "", row["ClientId"]?.ToString() ?? "");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("Error searching leases in scope {0}: {1}", scopeId, ex.Message);
            }
            
            return (false, "", "");
        }

        /// <summary>
        /// Sucht Computer in Reservations eines Scopes
        /// </summary>
        private async Task<(bool Found, string IP, string Name, string Description, string MAC)> FindComputerInReservations(string server, string scopeId, string computerName)
        {
            try
            {
                var reservationTable = await DhcpManager.QueryReservationsAsync(server, scopeId, s =>
                {
                    var normalizedServer = s.Trim().ToLowerInvariant();
                    if (_integratedAuthServers.Contains(normalizedServer))
                        return null;
                    if (_credentialCache.TryGetValue(normalizedServer, out var cachedCred))
                        return cachedCred;
                    return null;
                });

                if (reservationTable?.Rows.Count > 0)
                {
                    foreach (DataRow row in reservationTable.Rows)
                    {
                        var name = row["Name"]?.ToString() ?? "";
                        
                        // Vergleiche Computer-Name
                        if (string.Equals(name, computerName, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(name.Split('.')[0], computerName, StringComparison.OrdinalIgnoreCase))
                        {
                            return (true, 
                                   row["IPAddress"]?.ToString() ?? "", 
                                   name,
                                   row["Description"]?.ToString() ?? "",
                                   row["ClientId"]?.ToString() ?? "");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("Error searching reservations in scope {0}: {1}", scopeId, ex.Message);
            }
            
            return (false, "", "", "", "");
        }

        /// <summary>
        /// Aktualisiert eine Computer-Reservation
        /// </summary>
        private async Task<bool> UpdateComputerReservation(string server, string scopeId, string mac, ChangeReservationDialog dialog)
        {
            try
            {
                if (dialog.IpChanged)
                {
                    // IP-Adresse ändern
                    var success = await DhcpManager.UpdateReservationAsync(server, scopeId, mac, dialog.NewIp, dialog.NewName, dialog.NewDescription, s =>
                    {
                        var normalizedServer = s.Trim().ToLowerInvariant();
                        if (_integratedAuthServers.Contains(normalizedServer))
                            return null;
                        return _credentialCache.TryGetValue(normalizedServer, out var cachedCred) ? cachedCred : null;
                    });
                    return success;
                }
                else
                {
                    // Nur Name/Description ändern (falls möglich)
                    return await DhcpManager.UpdateReservationDetailsAsync(server, scopeId, mac, dialog.NewName, dialog.NewDescription, s =>
                    {
                        var normalizedServer = s.Trim().ToLowerInvariant();
                        if (_integratedAuthServers.Contains(normalizedServer))
                            return null;
                        return _credentialCache.TryGetValue(normalizedServer, out var cachedCred) ? cachedCred : null;
                    });
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("Error updating computer reservation: {0}", ex.Message);
                return false;
            }
        }
    }
}