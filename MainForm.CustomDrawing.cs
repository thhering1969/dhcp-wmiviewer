// MainForm.CustomDrawing.cs
using System;
using System.Drawing;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    public partial class MainForm
    {
        /// <summary>
        /// Custom Drawing für TreeView - ermöglicht oranges Folder-Icon mit schwarzem Text
        /// </summary>
        private void TreeViewAD_DrawNode(object sender, DrawTreeNodeEventArgs e)
        {
            try
            {
                // Standard-Hintergrund zeichnen
                if ((e.State & TreeNodeStates.Selected) != 0)
                {
                    // Ausgewählter Node - blauer Hintergrund
                    e.Graphics.FillRectangle(SystemBrushes.Highlight, e.Bounds);
                }
                else if ((e.State & TreeNodeStates.Hot) != 0)
                {
                    // Hover-Effekt - hellblauer Hintergrund
                    using (var brush = new SolidBrush(Color.FromArgb(230, 240, 250)))
                    {
                        e.Graphics.FillRectangle(brush, e.Bounds);
                    }
                }
                else
                {
                    // Normaler Hintergrund
                    e.Graphics.FillRectangle(SystemBrushes.Window, e.Bounds);
                }

                // Hole ADTreeItem aus Node.Tag
                if (e.Node.Tag is ADTreeItem item)
                {
                    DrawCustomNode(e.Graphics, e.Bounds, e.Node, item, (e.State & TreeNodeStates.Selected) != 0);
                }
                else
                {
                    // Fallback für Nodes ohne ADTreeItem
                    DrawDefaultNode(e.Graphics, e.Bounds, e.Node, (e.State & TreeNodeStates.Selected) != 0);
                }
            }
            catch (Exception ex)
            {
                // Fallback bei Fehlern - Standard-Drawing
                e.DrawDefault = true;
                DebugLogger.LogFormat("Custom Drawing Error: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Zeichnet einen Node mit ADTreeItem-Informationen
        /// </summary>
        private void DrawCustomNode(Graphics graphics, Rectangle bounds, TreeNode node, ADTreeItem item, bool isSelected)
        {
            var font = treeViewAD.Font;
            var textColor = isSelected ? SystemColors.HighlightText : Color.Black;
            
            // Startposition für Text (mit Einrückung)
            var textX = bounds.X + 2;
            var textY = bounds.Y + 2;
            
            // Icon basierend auf Item-Typ zeichnen
            var iconSize = 16; // Standard Icon-Größe
            var iconRect = new Rectangle(textX, textY, iconSize, iconSize);
            
            if (item.IsOU || item.IsContainer)
            {
                DrawFolderIcon(graphics, iconRect, Color.DarkOrange);
            }
            else if (item.IsComputer)
            {
                var computerColor = item.Enabled ? Color.Black : Color.Gray;
                // Performance-Fix: Verwende gecachten Online-Status ohne neue Ping-Trigger
                var isOnline = GetCachedOnlineStatus(item);
                DrawComputerIcon(graphics, iconRect, computerColor, isOnline);
            }
            else
            {
                // Domain oder andere
                DrawDomainIcon(graphics, iconRect, Color.DarkGreen);
            }

            // Text-Position nach Icon
            textX += iconSize + 4;

            // Zeichne Text in schwarzer Farbe (oder weiß bei Selektion)
            var displayText = GetDisplayTextWithoutIcon(item);
            using (var textBrush = new SolidBrush(textColor))
            {
                graphics.DrawString(displayText, font, textBrush, textX, textY);
            }
        }

        /// <summary>
        /// Gibt den gecachten Online-Status zurück und startet nur bei Bedarf neue Ping-Operationen
        /// </summary>
        private bool GetCachedOnlineStatus(ADTreeItem item)
        {
            if (!item.IsComputer) return false;
            
            // Verwende die OnlineStatus-Property aus ADTreeItem, aber caching-optimiert
            if (item.OnlineStatus == null || !item.OnlineStatus.IsValid)
            {
                // Nur wenn noch kein Check läuft, neuen starten
                var status = ComputerOnlineChecker.GetOnlineStatus(item.Name);
                item.OnlineStatus = status;
                
                // Plane UI-Update für später (wenn der Ping-Check fertig ist)
                if (status.IsChecking)
                {
                    ScheduleTreeViewRefresh();
                }
            }
            
            return item.OnlineStatus?.IsOnline ?? false;
        }

        /// <summary>
        /// Plant ein TreeView-Refresh für nach dem Online-Check
        /// </summary>
        private void ScheduleTreeViewRefresh()
        {
            // Verhindere mehrfache Timer-Starts
            if (_refreshTimer?.Enabled == true) return;

            _refreshTimer ??= new System.Windows.Forms.Timer { Interval = 2000 }; // 2 Sekunden
            _refreshTimer.Tick += (s, e) =>
            {
                try
                {
                    _refreshTimer.Stop();
                    if (treeViewAD.IsHandleCreated && !treeViewAD.Disposing)
                    {
                        treeViewAD.Invalidate();
                    }
                }
                catch { /* ignore */ }
            };
            _refreshTimer.Start();
        }

        private System.Windows.Forms.Timer? _refreshTimer;

        /// <summary>
        /// Zeichnet ein oranges Folder-Icon
        /// </summary>
        private void DrawFolderIcon(Graphics graphics, Rectangle rect, Color color)
        {
            using (var brush = new SolidBrush(color))
            using (var pen = new Pen(Color.Black, 1))
            {
                // Folder-Form zeichnen
                var folderRect = new Rectangle(rect.X + 1, rect.Y + 4, rect.Width - 2, rect.Height - 6);
                var tabRect = new Rectangle(rect.X + 1, rect.Y + 2, rect.Width / 3, 3);
                
                // Folder-Body
                graphics.FillRectangle(brush, folderRect);
                graphics.DrawRectangle(pen, folderRect);
                
                // Folder-Tab
                graphics.FillRectangle(brush, tabRect);
                graphics.DrawRectangle(pen, tabRect);
            }
        }

        /// <summary>
        /// Zeichnet ein Computer-Icon mit Online-Status
        /// </summary>
        private void DrawComputerIcon(Graphics graphics, Rectangle rect, Color color, bool isOnline = false)
        {
            using (var brush = new SolidBrush(color))
            using (var pen = new Pen(Color.Black, 1))
            {
                // Monitor
                var monitorRect = new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 6);
                graphics.FillRectangle(brush, monitorRect);
                graphics.DrawRectangle(pen, monitorRect);
                
                // Screen - Farbe abhängig vom Online-Status
                var screenRect = new Rectangle(monitorRect.X + 2, monitorRect.Y + 2, monitorRect.Width - 4, monitorRect.Height - 4);
                if (isOnline)
                {
                    // Online: Heller, leuchtender Bildschirm
                    graphics.FillRectangle(Brushes.LightGreen, screenRect);
                    
                    // Zusätzlicher "Power"-Indikator (kleiner grüner Punkt)
                    var powerRect = new Rectangle(rect.X + rect.Width - 4, rect.Y + 1, 3, 3);
                    graphics.FillEllipse(Brushes.Lime, powerRect);
                }
                else
                {
                    // Offline: Dunkler/grauer Bildschirm
                    graphics.FillRectangle(Brushes.DarkGray, screenRect);
                }
                
                // Stand
                var standRect = new Rectangle(rect.X + 6, rect.Y + rect.Height - 3, rect.Width - 12, 2);
                graphics.FillRectangle(brush, standRect);
            }
        }

        /// <summary>
        /// Zeichnet ein Domain-Icon
        /// </summary>
        private void DrawDomainIcon(Graphics graphics, Rectangle rect, Color color)
        {
            using (var brush = new SolidBrush(color))
            using (var pen = new Pen(Color.Black, 1))
            {
                // Kreis für Domain
                graphics.FillEllipse(brush, rect);
                graphics.DrawEllipse(pen, rect);
                
                // Kreuz in der Mitte
                var centerX = rect.X + rect.Width / 2;
                var centerY = rect.Y + rect.Height / 2;
                graphics.DrawLine(pen, centerX - 3, centerY, centerX + 3, centerY);
                graphics.DrawLine(pen, centerX, centerY - 3, centerX, centerY + 3);
            }
        }

        /// <summary>
        /// Zeichnet einen Standard-Node (Fallback)
        /// </summary>
        private void DrawDefaultNode(Graphics graphics, Rectangle bounds, TreeNode node, bool isSelected)
        {
            var font = treeViewAD.Font;
            var textColor = isSelected ? SystemColors.HighlightText : Color.Black;
            
            using (var textBrush = new SolidBrush(textColor))
            {
                graphics.DrawString(node.Text, font, textBrush, bounds.X + 2, bounds.Y + 2);
            }
        }

        /// <summary>
        /// Gibt den Display-Text ohne Icon zurück
        /// </summary>
        private string GetDisplayTextWithoutIcon(ADTreeItem item)
        {
            if (item.IsOU || item.IsContainer)
            {
                var displayName = item.GetCleanName();
                return item.ComputerCount > 0 ? $" {displayName} ({item.ComputerCount} computers)" : $" {displayName}";
            }
            else if (item.IsComputer)
            {
                var osInfo = !string.IsNullOrEmpty(item.OperatingSystem) ? $" [{item.OperatingSystem}]" : "";
                return $" {item.Name}{osInfo}";
            }
            else
            {
                return $" {item.Name}";
            }
        }
    }
}