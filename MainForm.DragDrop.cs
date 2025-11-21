// MainForm.DragDrop.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    public partial class MainForm
    {
        private TreeNode? _draggedNode = null;
        private TreeNode? _targetNode = null;

        /// <summary>
        /// Wird ausgelöst, wenn ein Drag-Vorgang in der TreeView beginnt
        /// </summary>
        private void TreeViewAD_ItemDrag(object? sender, ItemDragEventArgs e)
        {
            try
            {
                if (e.Item is TreeNode draggedNode)
                {
                    var item = draggedNode.Tag as ADTreeItem;
                    
                    // Nur Computer-Objekte können verschoben werden
                    if (item != null && item.IsComputer)
                    {
                        _draggedNode = draggedNode;
                        DebugLogger.LogFormat("Starting drag operation for computer: {0}", item.Name);
                        
                        // Starte Drag & Drop Operation
                        treeViewAD?.DoDragDrop(draggedNode, DragDropEffects.Move);
                    }
                    else
                    {
                        DebugLogger.LogFormat("Drag operation not allowed for non-computer item: {0}", item?.Name ?? "Unknown");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("Error in TreeViewAD_ItemDrag: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Wird ausgelöst, wenn ein Drag-Objekt in die TreeView eintritt
        /// </summary>
        private void TreeViewAD_DragEnter(object? sender, DragEventArgs e)
        {
            try
            {
                if (e.Data?.GetDataPresent(typeof(TreeNode)) == true)
                {
                    e.Effect = DragDropEffects.Move;
                }
                else
                {
                    e.Effect = DragDropEffects.None;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("Error in TreeViewAD_DragEnter: {0}", ex.Message);
                e.Effect = DragDropEffects.None;
            }
        }

        /// <summary>
        /// Wird ausgelöst, wenn ein Drag-Objekt über die TreeView bewegt wird
        /// </summary>
        private void TreeViewAD_DragOver(object? sender, DragEventArgs e)
        {
            try
            {
                if (treeViewAD == null || e.Data?.GetDataPresent(typeof(TreeNode)) != true)
                {
                    e.Effect = DragDropEffects.None;
                    return;
                }

                // Konvertiere Bildschirmkoordinaten zu TreeView-Koordinaten
                var clientPoint = treeViewAD.PointToClient(new Point(e.X, e.Y));
                var targetNode = treeViewAD.GetNodeAt(clientPoint);

                if (targetNode != null)
                {
                    var targetItem = targetNode.Tag as ADTreeItem;
                    
                    // Prüfe, ob das Ziel ein gültiger Container ist
                    if (IsValidDropTarget(targetItem))
                    {
                        // Highlight des Ziel-Knotens
                        if (_targetNode != targetNode)
                        {
                            // Entferne vorheriges Highlight
                            if (_targetNode != null)
                            {
                                _targetNode.BackColor = Color.Empty;
                            }
                            
                            // Setze neues Highlight
                            _targetNode = targetNode;
                            _targetNode.BackColor = Color.LightBlue;
                            treeViewAD.Invalidate();
                        }
                        
                        e.Effect = DragDropEffects.Move;
                        
                        // Erweitere den Knoten automatisch nach kurzer Zeit
                        if (!targetNode.IsExpanded && targetNode.Nodes.Count > 0)
                        {
                            // Auto-expand nach 1 Sekunde hovering
                            _ = Task.Delay(1000).ContinueWith(t =>
                            {
                                if (_targetNode == targetNode && !targetNode.IsExpanded)
                                {
                                    this.Invoke(new Action(() => targetNode.Expand()));
                                }
                            });
                        }
                    }
                    else
                    {
                        e.Effect = DragDropEffects.None;
                        ClearDropHighlight();
                    }
                }
                else
                {
                    e.Effect = DragDropEffects.None;
                    ClearDropHighlight();
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("Error in TreeViewAD_DragOver: {0}", ex.Message);
                e.Effect = DragDropEffects.None;
                ClearDropHighlight();
            }
        }

        /// <summary>
        /// Wird ausgelöst, wenn ein Drag-Objekt in der TreeView abgelegt wird
        /// </summary>
        private async void TreeViewAD_DragDrop(object? sender, DragEventArgs e)
        {
            try
            {
                ClearDropHighlight();

                if (treeViewAD == null || e.Data?.GetDataPresent(typeof(TreeNode)) != true)
                {
                    return;
                }

                var draggedNode = e.Data.GetData(typeof(TreeNode)) as TreeNode;
                if (draggedNode == null || draggedNode != _draggedNode)
                {
                    return;
                }

                // Konvertiere Bildschirmkoordinaten zu TreeView-Koordinaten
                var clientPoint = treeViewAD.PointToClient(new Point(e.X, e.Y));
                var targetNode = treeViewAD.GetNodeAt(clientPoint);

                if (targetNode == null)
                {
                    return;
                }

                var draggedItem = draggedNode.Tag as ADTreeItem;
                var targetItem = targetNode.Tag as ADTreeItem;

                if (draggedItem == null || !draggedItem.IsComputer || !IsValidDropTarget(targetItem))
                {
                    MessageBox.Show("Invalid drop target. Computer objects can only be moved to OUs or Computer containers.", 
                                  "Move Computer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Prüfe, ob das Ziel anders ist als die aktuelle Position
                if (string.Equals(GetParentDN(draggedItem.DistinguishedName), targetItem?.DistinguishedName, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("The computer is already in this container.", 
                                  "Move Computer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Bestätigungsdialog
                var targetName = GetFriendlyOUName(targetItem?.DistinguishedName ?? "");
                var confirmResult = MessageBox.Show(
                    $"Move computer '{draggedItem.Name}' to '{targetName}'?",
                    "Confirm Move",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirmResult != DialogResult.Yes)
                {
                    return;
                }

                // Zusätzliche Warnung für kritische Computer
                if (IsCriticalSystemComputer(draggedItem.Name))
                {
                    var criticalConfirm = MessageBox.Show(
                        $"WARNING: '{draggedItem.Name}' appears to be a critical system computer.\n\nMoving it may affect system functionality. Are you sure you want to continue?",
                        "Critical System Warning",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2);

                    if (criticalConfirm != DialogResult.Yes)
                    {
                        return;
                    }
                }

                // Führe die Verschiebung durch
                UpdateStatus($"Moving computer '{draggedItem.Name}' to '{targetName}'...");
                
                var success = await MoveComputerToOUAsync(draggedItem.DistinguishedName, targetItem?.DistinguishedName ?? "");
                
                if (success)
                {
                    // Event für Computer-Verschiebung loggen
                    var selectedDC = cmbDomainControllers?.SelectedItem?.ToString();
                    EventLogger.LogComputerMove(
                        draggedItem.Name,
                        GetFriendlyOUName(GetParentDN(draggedItem.DistinguishedName)),
                        targetName,
                        selectedDC ?? Environment.MachineName,
                        "DragDrop"
                    );
                    
                    MessageBox.Show($"Computer '{draggedItem.Name}' successfully moved to '{targetName}'.", 
                                  "Move Computer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    
                    // Aktualisiere AD-Struktur
                    UpdateStatus("Refreshing AD structure...");
                    if (!string.IsNullOrEmpty(selectedDC))
                    {
                        await LoadADStructureAsync(selectedDC);
                    }
                    UpdateStatus("Computer moved successfully.");
                }
                else
                {
                    MessageBox.Show($"Failed to move computer '{draggedItem.Name}'. Check permissions and try again.", 
                                  "Move Computer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    UpdateStatus("Move operation failed.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during drag & drop operation: {ex.Message}", 
                              "Move Computer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatus($"Error moving computer: {ex.Message}");
                DebugLogger.LogFormat("Error in TreeViewAD_DragDrop: {0}", ex.Message);
            }
            finally
            {
                _draggedNode = null;
                _targetNode = null;
            }
        }

        /// <summary>
        /// Prüft, ob ein Ziel-Item ein gültiges Drop-Ziel für Computer-Objekte ist
        /// </summary>
        private bool IsValidDropTarget(ADTreeItem? targetItem)
        {
            if (targetItem == null)
                return false;

            // Computer können in OUs, Container oder Computer-spezifische Container verschoben werden
            return targetItem.IsOU || 
                   targetItem.IsContainer || 
                   (targetItem.Name?.ToLowerInvariant().Contains("computer") == true);
        }

        /// <summary>
        /// Entfernt das Drop-Highlight
        /// </summary>
        private void ClearDropHighlight()
        {
            try
            {
                if (_targetNode != null)
                {
                    _targetNode.BackColor = Color.Empty;
                    _targetNode = null;
                    treeViewAD?.Invalidate();
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("Error clearing drop highlight: {0}", ex.Message);
            }
        }





        /// <summary>
        /// Prüft, ob es sich um einen kritischen System-Computer handelt
        /// </summary>
        private bool IsCriticalSystemComputer(string computerName)
        {
            if (string.IsNullOrEmpty(computerName))
                return false;

            var name = computerName.ToLowerInvariant();
            
            // Bekannte kritische System-Präfixe/Namen
            var criticalPatterns = new[]
            {
                "dc-", "dc01", "dc02", "dc03", "dc1", "dc2", "dc3",
                "domaincontroller", "domain-controller",
                "srv-", "server-", "sql-", "exchange-", "ex-",
                "fs-", "fileserver", "file-server",
                "dns-", "dhcp-", "ca-", "adfs-",
                "hyper-v", "hyperv", "vm-host", "vmhost"
            };

            return criticalPatterns.Any(pattern => name.Contains(pattern));
        }
    }
}