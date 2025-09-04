// MainForm.ADContextMenu.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    public partial class MainForm
    {
        // Cache für DHCP-Status um wiederholte Suchen zu vermeiden
        private readonly Dictionary<string, (ComputerDhcpStatus Status, DateTime CachedAt)> _dhcpStatusCache = new();
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Erweitert das bestehende AD Context-Menü um DHCP-spezifische Funktionen
        /// </summary>
        private void SetupADContextMenuDhcpIntegration(ContextMenuStrip contextMenuAD)
        {
            try
            {
                DebugLogger.LogFormat("SetupADContextMenuDhcpIntegration called - adding DHCP menu items to context menu with {0} existing items", contextMenuAD.Items.Count);
                
                // Separator vor DHCP-Funktionen
                var separator = new ToolStripSeparator();
                contextMenuAD.Items.Add(separator);

                // DHCP-spezifische Menüpunkte
                var menuItemConvertLease = new ToolStripMenuItem("🔄 Convert Lease to Reservation");
                var menuItemChangeReservation = new ToolStripMenuItem("⚙️ Change Reservation");
                var menuItemShowDhcpInfo = new ToolStripMenuItem("ℹ️ Show DHCP Info");

                // Initially hidden - will be shown for computer nodes only
                menuItemConvertLease.Visible = false;
                menuItemChangeReservation.Visible = false;
                menuItemShowDhcpInfo.Visible = false;

                contextMenuAD.Items.Add(menuItemConvertLease);
                contextMenuAD.Items.Add(menuItemChangeReservation);
                contextMenuAD.Items.Add(menuItemShowDhcpInfo);

                DebugLogger.LogFormat("DHCP menu items added successfully - context menu now has {0} items", contextMenuAD.Items.Count);

                // Event Handler für DHCP-Menüpunkte
                menuItemConvertLease.Click += async (s, e) => await OnConvertLeaseToReservation();
                menuItemChangeReservation.Click += async (s, e) => await OnChangeComputerReservation();
                menuItemShowDhcpInfo.Click += async (s, e) => await OnShowComputerDhcpInfo();

                // Opening Event wird jetzt vom Haupt-Handler in Layout.cs aufgerufen
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("Error setting up AD context menu DHCP integration: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Wird aufgerufen bevor Context-Menü geöffnet wird - zeigt sofort Menü an und lädt DHCP-Info asynchron nach
        /// </summary>
        public void OnADContextMenuOpening(ContextMenuStrip contextMenu)
        {
            try
            {
                DebugLogger.LogFormat("OnADContextMenuOpening called - context menu has {0} items", contextMenu.Items.Count);
                
                // Hole Computer-Node
                var selectedNode = treeViewAD?.SelectedNode;
                var computerItem = selectedNode?.Tag as ADTreeItem;
                
                DebugLogger.LogFormat("TreeView selected node: '{0}', Tag type: {1}", 
                                    selectedNode?.Text ?? "null", 
                                    selectedNode?.Tag?.GetType().Name ?? "null");
                
                // Debug: TreeView Node Details
                if (selectedNode != null)
                {
                    DebugLogger.LogFormat("TreeView Node Details:");
                    DebugLogger.LogFormat("  Node.Text: '{0}'", selectedNode.Text);
                    DebugLogger.LogFormat("  Node.Name: '{0}'", selectedNode.Name ?? "empty");
                    DebugLogger.LogFormat("  Node.FullPath: '{0}'", selectedNode.FullPath ?? "null");
                    DebugLogger.LogFormat("  Node.Tag: {0}", selectedNode.Tag != null ? "exists" : "null");
                    
                    if (selectedNode.Tag != null)
                    {
                        DebugLogger.LogFormat("  Tag.GetType(): {0}", selectedNode.Tag.GetType().Name);
                        DebugLogger.LogFormat("  Tag.ToString(): '{0}'", selectedNode.Tag.ToString() ?? "null");
                    }
                }
                
                // Debug: Highlight which object is in context focus
                if (selectedNode != null)
                {
                    var nodeType = computerItem?.IsComputer == true ? "Computer" : 
                                   computerItem?.IsOU == true ? "OU" : "Unknown";
                    DebugLogger.LogFormat("AD Context Menu opened for {0}: '{1}' (Path: {2})", 
                                        nodeType, computerItem?.Name ?? selectedNode.Text, 
                                        computerItem?.DistinguishedName ?? selectedNode.FullPath);
                }
                
                // DHCP-Menüpunkte finden
                var convertLeaseItem = contextMenu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(x => x.Text.Contains("Convert Lease"));
                var changeReservationItem = contextMenu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(x => x.Text.Contains("Change Reservation"));
                var showInfoItem = contextMenu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(x => x.Text.Contains("DHCP Info"));
                
                DebugLogger.LogFormat("DHCP Menu items found - Convert: {0}, Change: {1}, Info: {2}", 
                                    convertLeaseItem != null ? "Yes" : "No",
                                    changeReservationItem != null ? "Yes" : "No", 
                                    showInfoItem != null ? "Yes" : "No");

                // Debug: Liste alle Menu Items auf
                DebugLogger.LogFormat("All context menu items ({0}):", contextMenu.Items.Count);
                for (int i = 0; i < contextMenu.Items.Count; i++)
                {
                    var item = contextMenu.Items[i];
                    DebugLogger.LogFormat("  [{0}] {1}: '{2}' (Visible: {3})", 
                                        i, item.GetType().Name, 
                                        item is ToolStripMenuItem menuItem ? menuItem.Text : item.ToString(),
                                        item.Visible);
                }

                // Standard: Alle DHCP-Menüpunkte ausblenden
                if (convertLeaseItem != null) 
                {
                    convertLeaseItem.Visible = false;
                    convertLeaseItem.Enabled = false;
                }
                if (changeReservationItem != null) 
                {
                    changeReservationItem.Visible = false; 
                    changeReservationItem.Enabled = false;
                }
                if (showInfoItem != null) 
                {
                    showInfoItem.Visible = false;
                    showInfoItem.Enabled = false;
                }

                // Computer-Node Detection Debug
                DebugLogger.LogFormat("=== COMPUTER DETECTION ANALYSIS ===");
                DebugLogger.LogFormat("  selectedNode != null: {0}", selectedNode != null);
                DebugLogger.LogFormat("  computerItem != null: {0}", computerItem != null);
                if (computerItem != null)
                {
                    DebugLogger.LogFormat("  computerItem.Name: '{0}'", computerItem.Name ?? "null");
                    DebugLogger.LogFormat("  computerItem.Type: '{0}'", computerItem.Type ?? "null");
                    DebugLogger.LogFormat("  computerItem.IsComputer: {0} (= Type == 'Computer')", computerItem.IsComputer);
                    DebugLogger.LogFormat("  computerItem.IsOU: {0} (= Type == 'OU')", computerItem.IsOU);
                    DebugLogger.LogFormat("  computerItem.IsContainer: {0} (= Type == 'Container')", computerItem.IsContainer);
                    DebugLogger.LogFormat("  computerItem.DistinguishedName: '{0}'", computerItem.DistinguishedName ?? "null");
                    DebugLogger.LogFormat("  computerItem.OperatingSystem: '{0}'", computerItem.OperatingSystem ?? "null");
                    
                    // AUTOMATIC PROBLEM DIAGNOSIS
                    if (!computerItem.IsComputer && !string.IsNullOrEmpty(computerItem.Name))
                    {
                        DebugLogger.LogFormat("  🔍 PROBLEM DETECTED: Node '{0}' should be a computer but Type='{1}'", computerItem.Name, computerItem.Type);
                        
                        // Check if this looks like a computer node
                        bool looksLikeComputer = !string.IsNullOrEmpty(computerItem.OperatingSystem) || 
                                               computerItem.DistinguishedName?.StartsWith("CN=", StringComparison.OrdinalIgnoreCase) == true ||
                                               selectedNode.Text.Contains("Windows", StringComparison.OrdinalIgnoreCase);
                        
                        if (looksLikeComputer)
                        {
                            DebugLogger.LogFormat("  🔧 AUTOMATIC FIX: This looks like a computer - overriding Type field");
                            computerItem.Type = "Computer";
                            DebugLogger.LogFormat("  ✅ FIXED: computerItem.IsComputer is now {0}", computerItem.IsComputer);
                        }
                    }
                }

                // AGGRESSIVE FIX: Behandle ALLE Nodes mit Computer-ähnlichen Eigenschaften als Computer
                bool isComputerNodeFallback = false;
                bool forceComputerDetection = false;
                
                if (computerItem != null)
                {
                    // Multiple detection methods
                    bool hasOperatingSystem = !string.IsNullOrEmpty(computerItem.OperatingSystem);
                    bool hasComputerDN = computerItem.DistinguishedName?.StartsWith("CN=", StringComparison.OrdinalIgnoreCase) == true;
                    bool nodeTextSuggestsComputer = selectedNode?.Text?.Contains("[Windows", StringComparison.OrdinalIgnoreCase) == true ||
                                                   selectedNode?.Text?.Contains("Pro]", StringComparison.OrdinalIgnoreCase) == true ||
                                                   selectedNode?.Text?.Contains("Server", StringComparison.OrdinalIgnoreCase) == true ||
                                                   selectedNode?.Text?.Contains("Domain Controller", StringComparison.OrdinalIgnoreCase) == true;
                    
                    // Any of these suggests this is a computer
                    if (hasOperatingSystem || hasComputerDN || nodeTextSuggestsComputer)
                    {
                        if (!computerItem.IsComputer)
                        {
                            DebugLogger.LogFormat("  🔧 AGGRESSIVE FIX: Detected computer characteristics - forcing computer mode");
                            DebugLogger.LogFormat("    hasOperatingSystem: {0}", hasOperatingSystem);
                            DebugLogger.LogFormat("    hasComputerDN: {0}", hasComputerDN); 
                            DebugLogger.LogFormat("    nodeTextSuggestsComputer: {0}", nodeTextSuggestsComputer);
                            
                            isComputerNodeFallback = true;
                            forceComputerDetection = true;
                        }
                    }
                    
                    // EXTRA AGGRESSIVE: If node text clearly shows it's a computer, force it
                    if (!computerItem.IsComputer && !isComputerNodeFallback)
                    {
                        string nodeText = selectedNode?.Text ?? "";
                        if (nodeText.Contains("WIN11TEST", StringComparison.OrdinalIgnoreCase) ||
                            nodeText.Contains("LAP", StringComparison.OrdinalIgnoreCase) ||
                            nodeText.Contains("PC", StringComparison.OrdinalIgnoreCase))
                        {
                            DebugLogger.LogFormat("  🔧 EXTRA AGGRESSIVE: Node name pattern suggests computer: '{0}'", nodeText);
                            isComputerNodeFallback = true;
                            forceComputerDetection = true;
                        }
                    }
                    
                    // NUCLEAR OPTION: Check parent OU path for computer indicators
                    if (!computerItem.IsComputer && !isComputerNodeFallback)
                    {
                        string fullPath = selectedNode?.FullPath ?? "";
                        if (fullPath.Contains("Workstation", StringComparison.OrdinalIgnoreCase) ||
                            fullPath.Contains("Computer", StringComparison.OrdinalIgnoreCase) ||
                            fullPath.Contains("WIN11", StringComparison.OrdinalIgnoreCase) ||
                            fullPath.Contains("Desktop", StringComparison.OrdinalIgnoreCase))
                        {
                            DebugLogger.LogFormat("  🔧 NUCLEAR OPTION: Parent OU path suggests computer: '{0}'", fullPath);
                            isComputerNodeFallback = true;
                            forceComputerDetection = true;
                        }
                    }
                    
                    // ABSOLUTE LAST RESORT: If it's a leaf node (no children) and not an OU, treat as computer
                    if (!computerItem.IsComputer && !computerItem.IsOU && !isComputerNodeFallback && selectedNode != null)
                    {
                        bool isLeafNode = selectedNode.Nodes.Count == 0;
                        if (isLeafNode)
                        {
                            DebugLogger.LogFormat("  🔧 LAST RESORT: Leaf node that's not OU - assuming computer");
                            isComputerNodeFallback = true; 
                            forceComputerDetection = true;
                        }
                    }
                }

                // Zeige DHCP Menu Items wenn es ein Computer ist (original oder detected)
                bool treatAsComputer = computerItem != null && (computerItem.IsComputer || isComputerNodeFallback);
                
                // Only show DHCP items for computer nodes (debug mode disabled)
                bool debugMode = false; // Debug mode disabled - working correctly!
                bool showDhcpItems = treatAsComputer || debugMode;
                
                DebugLogger.LogFormat("DHCP MENU DECISION: treatAsComputer={0}, debugMode={1}, showDhcpItems={2}", 
                    treatAsComputer, debugMode, showDhcpItems);
                
                if (showDhcpItems)
                {
                    DebugLogger.LogFormat("✅ DHCP menu items shown for computer: {0}", computerItem?.Name ?? "unknown");
                    
                    // Zeige erstmal Loading-Status
                    if (convertLeaseItem != null) 
                    {
                        convertLeaseItem.Text = "🔄 Convert Lease to Reservation (Checking...)";
                        convertLeaseItem.Visible = true;
                        convertLeaseItem.Enabled = false;
                    }
                    
                    if (changeReservationItem != null) 
                    {
                        changeReservationItem.Text = "⚙️ Change Reservation (Checking...)";
                        changeReservationItem.Visible = true;
                        changeReservationItem.Enabled = false;
                    }
                    
                    if (showInfoItem != null) 
                    {
                        showInfoItem.Text = "ℹ️ Show DHCP Info (Loading...)";
                        showInfoItem.Visible = true;
                        showInfoItem.Enabled = false;
                    }

                    // Starte DHCP-Suche im Hintergrund (fire & forget)
                    _ = Task.Run(async () => await UpdateDhcpMenuItemsAsync(contextMenu, computerItem.Name));
                }
                else
                {
                    DebugLogger.LogFormat("❌ NOT A COMPUTER NODE - DHCP menu items HIDDEN");
                    DebugLogger.LogFormat("  treatAsComputer = {0}", treatAsComputer);
                    DebugLogger.LogFormat("  Reason: computerItem == null: {0}", computerItem == null);
                    if (computerItem != null)
                    {
                        DebugLogger.LogFormat("  computerItem.IsComputer: {0}", computerItem.IsComputer);
                        DebugLogger.LogFormat("  isComputerNodeFallback: {0}", isComputerNodeFallback);
                        DebugLogger.LogFormat("  forceComputerDetection: {0}", forceComputerDetection);
                        DebugLogger.LogFormat("  Node text: '{0}'", selectedNode?.Text ?? "null");
                        
                        // Show all detection criteria that failed
                        bool hasOperatingSystem = !string.IsNullOrEmpty(computerItem.OperatingSystem);
                        bool hasComputerDN = computerItem.DistinguishedName?.StartsWith("CN=", StringComparison.OrdinalIgnoreCase) == true;
                        DebugLogger.LogFormat("  hasOperatingSystem: {0} ('{1}')", hasOperatingSystem, computerItem.OperatingSystem ?? "null");
                        DebugLogger.LogFormat("  hasComputerDN: {0} ('{1}')", hasComputerDN, computerItem.DistinguishedName ?? "null");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("Error in AD context menu opening: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Aktualisiert DHCP-Menüpunkte asynchron basierend auf DHCP-Status
        /// </summary>
        private async Task UpdateDhcpMenuItemsAsync(ContextMenuStrip contextMenu, string computerName)
        {
            try
            {
                // Prüfe Cache zuerst
                ComputerDhcpStatus dhcpStatus;
                var cacheKey = computerName.ToLowerInvariant();
                
                if (_dhcpStatusCache.TryGetValue(cacheKey, out var cachedEntry) && 
                    DateTime.Now - cachedEntry.CachedAt < _cacheExpiration)
                {
                    // Verwende Cache
                    dhcpStatus = cachedEntry.Status;
                    DebugLogger.LogFormat("Using cached DHCP status for computer: {0}", computerName);
                }
                else
                {
                    // DHCP Status abrufen (kann dauern)
                    dhcpStatus = await GetComputerDhcpStatusAsync(computerName);
                    
                    // In Cache speichern
                    _dhcpStatusCache[cacheKey] = (dhcpStatus, DateTime.Now);
                }
                
                // UI-Thread für Menü-Updates
                if (contextMenu.IsDisposed) return; // Safety check

                this.Invoke(new Action(() =>
                {
                    try
                    {
                        var convertLeaseItem = contextMenu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(x => x.Text.Contains("Convert Lease"));
                        var changeReservationItem = contextMenu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(x => x.Text.Contains("Change Reservation"));
                        var showInfoItem = contextMenu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(x => x.Text.Contains("DHCP Info"));

                        // Configure basierend auf DHCP-Status
                        if (convertLeaseItem != null)
                        {
                            if (dhcpStatus.HasLease)
                            {
                                convertLeaseItem.Text = $"🔄 Convert Lease to Reservation ({dhcpStatus.LeaseIP})";
                                convertLeaseItem.Visible = true;
                                convertLeaseItem.Enabled = true;
                            }
                            else
                            {
                                convertLeaseItem.Visible = false;
                            }
                        }

                        if (changeReservationItem != null)
                        {
                            if (dhcpStatus.HasReservation)
                            {
                                changeReservationItem.Text = $"⚙️ Change Reservation ({dhcpStatus.ReservationIP})";
                                changeReservationItem.Visible = true;
                                changeReservationItem.Enabled = true;
                            }
                            else
                            {
                                changeReservationItem.Visible = false;
                            }
                        }

                        if (showInfoItem != null)
                        {
                            if (dhcpStatus.HasLease || dhcpStatus.HasReservation)
                            {
                                showInfoItem.Text = "ℹ️ Show DHCP Info";
                                showInfoItem.Visible = true;
                                showInfoItem.Enabled = true;
                            }
                            else
                            {
                                showInfoItem.Text = "ℹ️ Show DHCP Info (No DHCP data found)";
                                showInfoItem.Visible = true;
                                showInfoItem.Enabled = false;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.LogFormat("Error updating DHCP menu items on UI thread: {0}", ex.Message);
                    }
                }));
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("Error updating DHCP menu items: {0}", ex.Message);
                
                // Bei Fehler - deaktiviere Menüpunkte
                if (!contextMenu.IsDisposed)
                {
                    this.Invoke(new Action(() =>
                    {
                        var items = contextMenu.Items.OfType<ToolStripMenuItem>().Where(x => 
                            x.Text.Contains("Convert Lease") || 
                            x.Text.Contains("Change Reservation") || 
                            x.Text.Contains("DHCP Info"));
                        
                        foreach (var item in items)
                        {
                            item.Visible = false;
                        }
                    }));
                }
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