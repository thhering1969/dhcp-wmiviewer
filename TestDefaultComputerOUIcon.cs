// TestDefaultComputerOUIcon.cs
// Test-Anwendung für die Standard-Computer-OU Icon-Funktionalität

using System;
using System.Drawing;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    /// <summary>
    /// Test-Form zur Demonstration der Standard-Computer-OU Icons
    /// </summary>
    public partial class TestDefaultComputerOUIconForm : Form
    {
        private TreeView testTreeView;
        private Panel iconPreviewPanel;

        public TestDefaultComputerOUIconForm()
        {
            InitializeComponent();
            SetupTestData();
        }

        private void InitializeComponent()
        {
            this.Text = "Standard-Computer-OU Icon Test";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;

            // TreeView für Test
            testTreeView = new TreeView
            {
                Dock = DockStyle.Left,
                Width = 400,
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true,
                HideSelection = false,
                FullRowSelect = true,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                DrawMode = TreeViewDrawMode.OwnerDrawText,
                ItemHeight = 20,
                Indent = 16,
                ShowNodeToolTips = true
            };

            // Custom Drawing Event
            testTreeView.DrawNode += TestTreeView_DrawNode;

            // Icon Preview Panel
            iconPreviewPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            iconPreviewPanel.Paint += IconPreviewPanel_Paint;

            // Splitter
            var splitter = new Splitter
            {
                Dock = DockStyle.Left,
                Width = 3
            };

            this.Controls.Add(iconPreviewPanel);
            this.Controls.Add(splitter);
            this.Controls.Add(testTreeView);
        }

        private void SetupTestData()
        {
            // Domain Root
            var domainNode = new TreeNode("🌐 testdomain.local")
            {
                Tag = new ADTreeItem
                {
                    Type = "Domain",
                    Name = "testdomain.local",
                    DistinguishedName = "DC=testdomain,DC=local"
                }
            };

            // Standard Computer Container (Default)
            var computersNode = new TreeNode("Computers [Default]")
            {
                Tag = new ADTreeItem
                {
                    Type = "Container",
                    Name = "CN=Computers",
                    DistinguishedName = "CN=Computers,DC=testdomain,DC=local",
                    ComputerCount = 5,
                    IsDefaultComputerOU = true
                }
            };

            // Standard Computer OU (SBS-Style, Default)
            var sbsComputersNode = new TreeNode("SBSComputers [Default]")
            {
                Tag = new ADTreeItem
                {
                    Type = "OU",
                    Name = "SBSComputers",
                    DistinguishedName = "OU=SBSComputers,DC=testdomain,DC=local",
                    ComputerCount = 12,
                    IsDefaultComputerOU = true
                }
            };

            // Normale OU (nicht Default)
            var workstationsNode = new TreeNode("Workstations")
            {
                Tag = new ADTreeItem
                {
                    Type = "OU",
                    Name = "Workstations",
                    DistinguishedName = "OU=Workstations,DC=testdomain,DC=local",
                    ComputerCount = 8,
                    IsDefaultComputerOU = false
                }
            };

            // Leere Standard-OU (Default, aber leer)
            var emptyDefaultNode = new TreeNode("EmptyDefault [Default]")
            {
                Tag = new ADTreeItem
                {
                    Type = "OU",
                    Name = "EmptyDefault",
                    DistinguishedName = "OU=EmptyDefault,DC=testdomain,DC=local",
                    ComputerCount = 0,
                    IsDefaultComputerOU = true
                }
            };

            // Computer-Beispiele
            var computer1 = new TreeNode("PC001 [Windows 11]")
            {
                Tag = new ADTreeItem
                {
                    Type = "Computer",
                    Name = "PC001",
                    DistinguishedName = "CN=PC001,CN=Computers,DC=testdomain,DC=local",
                    OperatingSystem = "Windows 11",
                    Enabled = true
                }
            };

            var computer2 = new TreeNode("SERVER01 [Windows Server 2022]")
            {
                Tag = new ADTreeItem
                {
                    Type = "Computer",
                    Name = "SERVER01",
                    DistinguishedName = "CN=SERVER01,OU=Servers,DC=testdomain,DC=local",
                    OperatingSystem = "Windows Server 2022",
                    Enabled = true
                }
            };

            // Baue Hierarchie auf
            computersNode.Nodes.Add(computer1);
            workstationsNode.Nodes.Add(computer2);

            domainNode.Nodes.Add(computersNode);
            domainNode.Nodes.Add(sbsComputersNode);
            domainNode.Nodes.Add(workstationsNode);
            domainNode.Nodes.Add(emptyDefaultNode);

            testTreeView.Nodes.Add(domainNode);
            domainNode.Expand();
        }

        private void TestTreeView_DrawNode(object sender, DrawTreeNodeEventArgs e)
        {
            try
            {
                // Standard-Hintergrund zeichnen
                if ((e.State & TreeNodeStates.Selected) != 0)
                {
                    e.Graphics.FillRectangle(SystemBrushes.Highlight, e.Bounds);
                }
                else
                {
                    e.Graphics.FillRectangle(SystemBrushes.Window, e.Bounds);
                }

                // Hole ADTreeItem aus Node.Tag
                if (e.Node.Tag is ADTreeItem item)
                {
                    DrawTestNode(e.Graphics, e.Bounds, e.Node, item, (e.State & TreeNodeStates.Selected) != 0);
                }
                else
                {
                    // Fallback
                    var textColor = (e.State & TreeNodeStates.Selected) != 0 ? SystemColors.HighlightText : Color.Black;
                    using (var textBrush = new SolidBrush(textColor))
                    {
                        e.Graphics.DrawString(e.Node.Text, testTreeView.Font, textBrush, e.Bounds.X + 2, e.Bounds.Y + 2);
                    }
                }
            }
            catch (Exception ex)
            {
                e.DrawDefault = true;
                Console.WriteLine($"Drawing Error: {ex.Message}");
            }
        }

        private void DrawTestNode(Graphics graphics, Rectangle bounds, TreeNode node, ADTreeItem item, bool isSelected)
        {
            var font = testTreeView.Font;
            var textColor = isSelected ? SystemColors.HighlightText : Color.Black;
            
            // Icon-Position
            var iconSize = 16;
            var iconRect = new Rectangle(bounds.X + 2, bounds.Y + 2, iconSize, iconSize);
            
            // Zeichne Icon basierend auf Typ
            if (item.IsOU || item.IsContainer)
            {
                if (item.IsDefaultComputerOU)
                {
                    DrawDefaultComputerOUIcon(graphics, iconRect);
                }
                else
                {
                    DrawFolderIcon(graphics, iconRect, Color.DarkOrange);
                }
            }
            else if (item.IsComputer)
            {
                DrawComputerIcon(graphics, iconRect, Color.Black, true);
            }
            else
            {
                DrawDomainIcon(graphics, iconRect, Color.DarkGreen);
            }

            // Text zeichnen
            var textX = bounds.X + iconSize + 6;
            var displayText = GetDisplayText(item);
            using (var textBrush = new SolidBrush(textColor))
            {
                graphics.DrawString(displayText, font, textBrush, textX, bounds.Y + 2);
            }
        }

        private string GetDisplayText(ADTreeItem item)
        {
            if (item.IsOU || item.IsContainer)
            {
                var displayName = item.Name.StartsWith("CN=") ? item.Name.Substring(3) : item.Name;
                var defaultMarker = item.IsDefaultComputerOU ? " [Default]" : "";
                return item.ComputerCount > 0 ? $"{displayName} ({item.ComputerCount} computers){defaultMarker}" : $"{displayName}{defaultMarker}";
            }
            else if (item.IsComputer)
            {
                var osInfo = !string.IsNullOrEmpty(item.OperatingSystem) ? $" [{item.OperatingSystem}]" : "";
                return $"{item.Name}{osInfo}";
            }
            return item.Name;
        }

        // Icon-Zeichnungs-Methoden (kopiert aus MainForm.CustomDrawing.cs)
        private void DrawDefaultComputerOUIcon(Graphics graphics, Rectangle rect)
        {
            var folderColor = Color.FromArgb(255, 165, 0); // Orange/Gold
            var textColor = Color.White;
            
            using (var folderBrush = new SolidBrush(folderColor))
            using (var textBrush = new SolidBrush(textColor))
            using (var pen = new Pen(Color.Black, 1))
            using (var font = new Font("Arial", 8, FontStyle.Bold))
            {
                var folderRect = new Rectangle(rect.X, rect.Y + 3, rect.Width, rect.Height - 4);
                var tabRect = new Rectangle(rect.X, rect.Y + 1, rect.Width / 2, 3);
                
                graphics.FillRectangle(folderBrush, folderRect);
                graphics.DrawRectangle(pen, folderRect);
                graphics.FillRectangle(folderBrush, tabRect);
                graphics.DrawRectangle(pen, tabRect);
                
                var textRect = new Rectangle(folderRect.X + 2, folderRect.Y + 1, folderRect.Width - 4, folderRect.Height - 2);
                var stringFormat = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                
                graphics.DrawString("D", font, textBrush, textRect, stringFormat);
                
                var starRect = new Rectangle(rect.X + rect.Width - 4, rect.Y, 3, 3);
                graphics.FillEllipse(Brushes.Gold, starRect);
            }
        }

        private void DrawFolderIcon(Graphics graphics, Rectangle rect, Color color)
        {
            using (var brush = new SolidBrush(color))
            using (var pen = new Pen(Color.Black, 1))
            {
                var folderRect = new Rectangle(rect.X + 1, rect.Y + 4, rect.Width - 2, rect.Height - 6);
                var tabRect = new Rectangle(rect.X + 1, rect.Y + 2, rect.Width / 3, 3);
                
                graphics.FillRectangle(brush, folderRect);
                graphics.DrawRectangle(pen, folderRect);
                graphics.FillRectangle(brush, tabRect);
                graphics.DrawRectangle(pen, tabRect);
            }
        }

        private void DrawComputerIcon(Graphics graphics, Rectangle rect, Color color, bool isOnline)
        {
            using (var brush = new SolidBrush(color))
            using (var pen = new Pen(Color.Black, 1))
            {
                var monitorRect = new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 6);
                graphics.FillRectangle(brush, monitorRect);
                graphics.DrawRectangle(pen, monitorRect);
                
                var screenRect = new Rectangle(monitorRect.X + 2, monitorRect.Y + 2, monitorRect.Width - 4, monitorRect.Height - 4);
                graphics.FillRectangle(isOnline ? Brushes.LightGreen : Brushes.DarkGray, screenRect);
                
                if (isOnline)
                {
                    var powerRect = new Rectangle(rect.X + rect.Width - 4, rect.Y + 1, 3, 3);
                    graphics.FillEllipse(Brushes.Lime, powerRect);
                }
                
                var standRect = new Rectangle(rect.X + 6, rect.Y + rect.Height - 3, rect.Width - 12, 2);
                graphics.FillRectangle(brush, standRect);
            }
        }

        private void DrawDomainIcon(Graphics graphics, Rectangle rect, Color color)
        {
            var blueColor = Color.FromArgb(0, 150, 220);
            
            using (var blueBrush = new SolidBrush(blueColor))
            using (var whiteBrush = new SolidBrush(Color.White))
            {
                graphics.FillRectangle(blueBrush, rect);
                
                var size = rect.Width;
                var quarter = size / 4;
                var half = size / 2;
                
                var leftStripe = new Rectangle(rect.X + 1, rect.Y + 1, quarter - 1, size - 2);
                graphics.FillRectangle(whiteBrush, leftStripe);
                
                var topRightStripe = new Rectangle(rect.X + quarter + 1, rect.Y + 1, size - quarter - 2, quarter - 1);
                graphics.FillRectangle(whiteBrush, topRightStripe);
                
                var bottomRightSquare = new Rectangle(rect.X + half + 1, rect.Y + half + 1, quarter - 1, quarter - 1);
                graphics.FillRectangle(whiteBrush, bottomRightSquare);
                
                using (var borderPen = new Pen(Color.FromArgb(0, 120, 180), 1))
                {
                    graphics.DrawRectangle(borderPen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
                }
            }
        }

        private void IconPreviewPanel_Paint(object sender, PaintEventArgs e)
        {
            var graphics = e.Graphics;
            var y = 20;
            var iconSize = 32; // Größere Icons für Preview

            // Titel
            using (var titleFont = new Font("Segoe UI", 12, FontStyle.Bold))
            {
                graphics.DrawString("Icon Preview", titleFont, Brushes.Black, 20, y);
                y += 40;
            }

            // Standard-Computer-OU Icon
            var defaultRect = new Rectangle(20, y, iconSize, iconSize);
            DrawDefaultComputerOUIcon(graphics, defaultRect);
            graphics.DrawString("Standard-Computer-OU (Default)", SystemFonts.DefaultFont, Brushes.Black, 60, y + 8);
            y += 50;

            // Normale OU Icon
            var normalRect = new Rectangle(20, y, iconSize, iconSize);
            DrawFolderIcon(graphics, normalRect, Color.DarkOrange);
            graphics.DrawString("Normale OU/Container", SystemFonts.DefaultFont, Brushes.Black, 60, y + 8);
            y += 50;

            // Computer Icon
            var computerRect = new Rectangle(20, y, iconSize, iconSize);
            DrawComputerIcon(graphics, computerRect, Color.Black, true);
            graphics.DrawString("Computer (Online)", SystemFonts.DefaultFont, Brushes.Black, 60, y + 8);
            y += 50;

            // Domain Icon
            var domainRect = new Rectangle(20, y, iconSize, iconSize);
            DrawDomainIcon(graphics, domainRect, Color.DarkGreen);
            graphics.DrawString("Domain Root", SystemFonts.DefaultFont, Brushes.Black, 60, y + 8);
            y += 50;

            // Beschreibung
            using (var descFont = new Font("Segoe UI", 9, FontStyle.Regular))
            {
                var description = "Die Standard-Computer-OU wird mit einem goldenen Ordner-Icon\n" +
                                "mit weißem 'D' und einem goldenen Stern markiert.\n\n" +
                                "Diese OU wird immer angezeigt, auch wenn sie leer ist.\n\n" +
                                "Funktionen:\n" +
                                "• Automatische Erkennung über wellKnownObjects\n" +
                                "• Spezielle Kennzeichnung im Tooltip\n" +
                                "• [Default] Marker im Text\n" +
                                "• Immer sichtbar, auch bei 0 Computern";
                
                graphics.DrawString(description, descFont, Brushes.DarkBlue, 20, y + 20);
            }
        }

        /// <summary>
        /// Startet die Test-Anwendung
        /// </summary>
        public static void ShowTest()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            var testForm = new TestDefaultComputerOUIconForm();
            testForm.ShowDialog();
        }
    }
}