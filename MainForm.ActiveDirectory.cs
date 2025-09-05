// MainForm.ActiveDirectory.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    public partial class MainForm
    {
        // Win32 API für TreeView-Scroll-Kontrolle
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll")]
        private static extern int SetScrollPos(IntPtr hWnd, int nBar, int nPos, bool bRedraw);
        
        [DllImport("user32.dll")]
        private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);
        
        private const int WM_VSCROLL = 0x0115;
        private const int SB_TOP = 6;
        private const int SB_VERT = 1;
        private const int SB_LINEUP = 0;
        
        private System.Windows.Forms.Timer? scrollTimer;
        
        /// <summary>
        /// Scrollt das TreeView explizit nach ganz oben mit Win32 API
        /// </summary>
        private void ForceTreeViewScrollToTop(TreeView treeView)
        {
            try
            {
                DebugLogger.Log("Starting aggressive Win32 scroll to top");
                
                // Mehrere Win32-Methoden kombinieren
                
                // 1. Scroll-Position auf 0 setzen
                SetScrollPos(treeView.Handle, SB_VERT, 0, true);
                
                // 2. WM_VSCROLL mit SB_TOP
                SendMessage(treeView.Handle, WM_VSCROLL, (IntPtr)SB_TOP, IntPtr.Zero);
                
                // 3. Mehrere SB_LINEUP-Befehle (nach oben scrollen)
                for (int i = 0; i < 50; i++)
                {
                    SendMessage(treeView.Handle, WM_VSCROLL, (IntPtr)SB_LINEUP, IntPtr.Zero);
                }
                
                // 4. Nochmals SB_TOP
                SendMessage(treeView.Handle, WM_VSCROLL, (IntPtr)SB_TOP, IntPtr.Zero);
                
                // 5. TopNode setzen
                if (treeView.Nodes.Count > 0)
                {
                    treeView.TopNode = treeView.Nodes[0];
                }
                
                DebugLogger.Log("Aggressive Win32 scroll completed");
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("Win32 scroll failed: {0}", ex.Message);
            }
        }
        
        /// <summary>
        /// Radikale TreeView-Reset-Methode
        /// </summary>
        private void ResetTreeViewScrollPosition(TreeView treeView)
        {
            try
            {
                if (treeView.Nodes.Count == 0) return;
                
                DebugLogger.Log("Starting radical TreeView reset");
                
                // 1. Alle Knoten kollabieren
                treeView.CollapseAll();
                
                // 2. Win32 Scroll nach oben
                ForceTreeViewScrollToTop(treeView);
                
                // 3. Ersten Knoten als TopNode setzen
                treeView.TopNode = treeView.Nodes[0];
                treeView.SelectedNode = treeView.Nodes[0];
                
                // 4. Ersten Knoten expandieren
                treeView.Nodes[0].Expand();
                
                // 5. Nochmals Win32 Scroll
                ForceTreeViewScrollToTop(treeView);
                
                // 6. EnsureVisible für ersten Knoten
                treeView.Nodes[0].EnsureVisible();
                
                // 7. TreeView refreshen
                treeView.Refresh();
                treeView.Invalidate();
                
                DebugLogger.Log("Radical TreeView reset completed");
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("Radical reset failed: {0}", ex.Message);
            }
        }
        
        /// <summary>
        /// Startet einen Timer, der kontinuierlich versucht, das TreeView nach oben zu scrollen
        /// </summary>
        private void StartScrollTimer(TreeView treeView)
        {
            try
            {
                // Stoppe vorherigen Timer falls vorhanden
                scrollTimer?.Stop();
                scrollTimer?.Dispose();
                
                scrollTimer = new System.Windows.Forms.Timer();
                scrollTimer.Interval = 100; // Alle 100ms
                
                int attempts = 0;
                scrollTimer.Tick += (sender, e) =>
                {
                    try
                    {
                        attempts++;
                        DebugLogger.LogFormat("Timer attempt {0} to scroll to top", attempts);
                        
                        if (treeView.Nodes.Count > 0)
                        {
                            // Aggressive Scroll-Korrektur
                            ForceTreeViewScrollToTop(treeView);
                            
                            // Prüfe, ob der erste Knoten sichtbar ist
                            var firstNode = treeView.Nodes[0];
                            if (treeView.TopNode == firstNode)
                            {
                                DebugLogger.Log("Timer successful - stopping timer");
                                scrollTimer?.Stop();
                                scrollTimer?.Dispose();
                                scrollTimer = null;
                                return;
                            }
                        }
                        
                        // Nach 50 Versuchen aufgeben (5 Sekunden)
                        if (attempts >= 50)
                        {
                            DebugLogger.Log("Timer giving up after 50 attempts");
                            scrollTimer?.Stop();
                            scrollTimer?.Dispose();
                            scrollTimer = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.LogFormat("Timer error: {0}", ex.Message);
                    }
                };
                
                scrollTimer.Start();
                DebugLogger.Log("Scroll timer started");
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("Failed to start scroll timer: {0}", ex.Message);
            }
        }

        // Timer für automatisches Refresh der Online-Status
        private System.Windows.Forms.Timer? _adRefreshTimer;

        /// <summary>
        /// Startet automatisches Refresh der Computer-Online-Status im AD TreeView
        /// </summary>
        private void StartADOnlineStatusRefresh()
        {
            try
            {
                _adRefreshTimer?.Stop();
                _adRefreshTimer?.Dispose();

                _adRefreshTimer = new System.Windows.Forms.Timer
                {
                    Interval = 30000 // 30 Sekunden
                };

                _adRefreshTimer.Tick += async (s, e) =>
                {
                    try
                    {
                        await RefreshComputerOnlineStatusAsync();
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.LogFormat("AD Online Status Refresh Error: {0}", ex.Message);
                    }
                };

                _adRefreshTimer.Start();
                DebugLogger.Log("AD Online Status Auto-Refresh started (30s interval)");
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("Failed to start AD refresh timer: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Stoppt automatisches Refresh der Computer-Online-Status
        /// </summary>
        private void StopADOnlineStatusRefresh()
        {
            try
            {
                _adRefreshTimer?.Stop();
                _adRefreshTimer?.Dispose();
                _adRefreshTimer = null;
                DebugLogger.Log("AD Online Status Auto-Refresh stopped");
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("Error stopping AD refresh timer: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Refresht die Online-Status aller Computer im AD TreeView
        /// </summary>
        private async Task RefreshComputerOnlineStatusAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    var computerNodes = new List<TreeNode>();
                    CollectComputerNodes(treeViewAD.Nodes, computerNodes);

                    DebugLogger.LogFormat("Refreshing online status for {0} computers", computerNodes.Count);

                    // Starte Online-Checks für alle Computer (async)
                    var computerItems = computerNodes
                        .Where(n => n.Tag is ADTreeItem item && item.IsComputer)
                        .Select(n => n.Tag as ADTreeItem)
                        .Where(item => item != null)
                        .ToList();

                    foreach (var item in computerItems)
                    {
                        // Trigger fresh online check (ignores cache)
                        _ = Task.Run(() => ComputerOnlineChecker.ForceRefreshOnlineStatus(item.Name));
                    }

                    // Schedule UI update in 3 seconds
                    var updateTimer = new System.Windows.Forms.Timer { Interval = 3000 };
                    updateTimer.Tick += (s, e) =>
                    {
                        try
                        {
                            updateTimer.Stop();
                            updateTimer.Dispose();
                            
                            if (treeViewAD.IsHandleCreated && !treeViewAD.Disposing)
                            {
                                this.BeginInvoke(new Action(() =>
                                {
                                    treeViewAD.Invalidate();
                                    DebugLogger.LogFormat("AD TreeView refreshed - {0} computers checked", computerItems.Count);
                                }));
                            }
                        }
                        catch { /* ignore */ }
                    };
                    updateTimer.Start();
                }
                catch (Exception ex)
                {
                    DebugLogger.LogFormat("Error in RefreshComputerOnlineStatusAsync: {0}", ex.Message);
                }
            });
        }

        /// <summary>
        /// Sammelt alle Computer-Nodes aus dem TreeView
        /// </summary>
        private void CollectComputerNodes(TreeNodeCollection nodes, List<TreeNode> computerNodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is ADTreeItem item && item.IsComputer)
                {
                    computerNodes.Add(node);
                }
                
                // Rekursiv für Child-Nodes
                if (node.Nodes.Count > 0)
                {
                    CollectComputerNodes(node.Nodes, computerNodes);
                }
            }
        }

        /// <summary>
        /// Manual refresh für Online-Status (wird von Button aufgerufen)
        /// </summary>
        private async void RefreshOnlineStatus()
        {
            try
            {
                await RefreshComputerOnlineStatusAsync();
                DebugLogger.Log("Manual online status refresh completed");
            }
            catch (Exception ex)
            {
                DebugLogger.LogFormat("Manual online status refresh failed: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Lädt die verfügbaren Domain Controller und füllt die ComboBox.
        /// </summary>
        private async Task LoadDomainControllersAsync(ComboBox cmbDCs)
        {
            try
            {
                lblADStatus.Text = "Discovering Domain Controllers...";
                lblADStatus.ForeColor = System.Drawing.Color.Blue;
                
                // Disable buttons during discovery
                btnLoadAD.Enabled = false;
                btnRefreshAD.Enabled = false;

                await Task.Run(() =>
                {
                    var discoveredDCs = ADDiscovery.DiscoverDomainControllersInAD();
                    
                    this.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            cmbDCs.Items.Clear();
                            
                            if (discoveredDCs.Any())
                            {
                                foreach (var dc in discoveredDCs.OrderBy(d => d))
                                {
                                    cmbDCs.Items.Add(dc);
                                }

                                // Prüfe, ob lokaler Host ein DC ist und in der Liste steht
                                var localMachine = Environment.MachineName;
                                var localDC = discoveredDCs.FirstOrDefault(dc => 
                                    dc.Equals(localMachine, StringComparison.OrdinalIgnoreCase) ||
                                    dc.StartsWith(localMachine + ".", StringComparison.OrdinalIgnoreCase));

                                if (!string.IsNullOrEmpty(localDC))
                                {
                                    cmbDCs.SelectedItem = localDC;
                                    AppEnvironment.SetRunningOnDomainController(true);
                                }
                                else if (cmbDCs.Items.Count > 0)
                                {
                                    cmbDCs.SelectedIndex = 0;
                                }

                                lblADStatus.Text = $"Found {discoveredDCs.Count} Domain Controller(s)";
                                lblADStatus.ForeColor = System.Drawing.Color.Green;
                            }
                            else
                            {
                                lblADStatus.Text = "No Domain Controllers found";
                                lblADStatus.ForeColor = System.Drawing.Color.Red;
                            }
                        }
                        catch (Exception ex)
                        {
                            lblADStatus.Text = $"Error: {ex.Message}";
                            lblADStatus.ForeColor = System.Drawing.Color.Red;
                        }
                        finally
                        {
                            btnLoadAD.Enabled = true;
                            btnRefreshAD.Enabled = cmbDCs.SelectedItem != null;
                        }
                    }));
                });
            }
            catch (Exception ex)
            {
                lblADStatus.Text = $"Discovery failed: {ex.Message}";
                lblADStatus.ForeColor = System.Drawing.Color.Red;
                btnLoadAD.Enabled = true;
            }
        }

        /// <summary>
        /// Lädt die AD-Struktur (OUs mit Computerobjekten) vom angegebenen DC.
        /// </summary>
        private async Task LoadADStructureAsync(string domainController)
        {
            try
            {
                lblADStatus.Text = $"Loading AD structure from {domainController}...";
                lblADStatus.ForeColor = System.Drawing.Color.Blue;
                
                btnRefreshAD.Enabled = false;
                
                // Stoppe automatisches Refresh beim Laden neuer Daten
                StopADOnlineStatusRefresh();
                
                treeViewAD.Nodes.Clear();

                await WaitDialog.RunAsync(this, "Loading Active Directory structure with computers...", async () =>
                {
                    try
                    {
                        var results = await ADPowerShellExecutor.LoadADTreeStructureAsync(domainController, GetCredentialsForServer);
                        
                        this.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                PopulateADTreeViewWithComputers(results);
                                var ouCount = treeViewAD.Nodes.Cast<TreeNode>().Sum(n => CountOUsInNode(n));
                                var computerCount = treeViewAD.Nodes.Cast<TreeNode>().Sum(n => CountComputersInNode(n));
                                lblADStatus.Text = $"Loaded {ouCount} OUs/Containers with {computerCount} computers (only OUs containing computers shown)";
                                lblADStatus.ForeColor = System.Drawing.Color.Green;
                                
                                // Starte automatisches Online-Status-Refresh nach erfolgreichem Laden
                                if (computerCount > 0)
                                {
                                    StartADOnlineStatusRefresh();
                                    DebugLogger.LogFormat("Started AD Online Status refresh for {0} computers", computerCount);
                                }
                            }
                            catch (Exception ex)
                            {
                                lblADStatus.Text = $"Error populating tree: {ex.Message}";
                                lblADStatus.ForeColor = System.Drawing.Color.Red;
                            }
                            finally
                            {
                                btnRefreshAD.Enabled = true;
                            }
                        }));
                    }
                    catch (Exception ex)
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            lblADStatus.Text = $"Load failed: {ex.Message}";
                            lblADStatus.ForeColor = System.Drawing.Color.Red;
                            btnRefreshAD.Enabled = true;
                        }));
                    }
                });
            }
            catch (Exception ex)
            {
                lblADStatus.Text = $"Load failed: {ex.Message}";
                lblADStatus.ForeColor = System.Drawing.Color.Red;
                btnRefreshAD.Enabled = true;
            }
        }

        /// <summary>
        /// Füllt das TreeView mit den AD-Daten.
        /// </summary>
        private void PopulateADTreeView(System.Collections.ObjectModel.Collection<PSObject> results)
        {
            treeViewAD.BeginUpdate();
            try
            {
                treeViewAD.Nodes.Clear();

                // Gruppiere OUs nach ihrer Hierarchie
                var ouDict = new Dictionary<string, TreeNode>();
                var ous = new List<ADOrganizationalUnit>();

                foreach (var result in results)
                {
                    var ou = new ADOrganizationalUnit
                    {
                        Name = result.Properties["Name"]?.Value?.ToString() ?? "",
                        DistinguishedName = result.Properties["DistinguishedName"]?.Value?.ToString() ?? "",
                        Description = result.Properties["Description"]?.Value?.ToString() ?? "",
                        ComputerCount = Convert.ToInt32(result.Properties["ComputerCount"]?.Value ?? 0)
                    };
                    ous.Add(ou);
                }

                // Sortiere OUs nach DN-Länge (Eltern zuerst)
                ous = ous.OrderBy(ou => ou.DistinguishedName.Split(',').Length).ToList();

                foreach (var ou in ous)
                {
                    var nodeText = $"{ou.Name} ({ou.ComputerCount} computers)";
                    var node = new TreeNode(nodeText)
                    {
                        Tag = ou,
                        ToolTipText = $"DN: {ou.DistinguishedName}\nDescription: {ou.Description}\nComputers: {ou.ComputerCount}"
                    };

                    // Finde Parent-OU
                    var parentDN = GetParentDN(ou.DistinguishedName);
                    if (!string.IsNullOrEmpty(parentDN) && ouDict.ContainsKey(parentDN))
                    {
                        ouDict[parentDN].Nodes.Add(node);
                    }
                    else
                    {
                        treeViewAD.Nodes.Add(node);
                    }

                    ouDict[ou.DistinguishedName] = node;
                }

                // Expandiere die ersten Ebenen
                foreach (TreeNode rootNode in treeViewAD.Nodes)
                {
                    rootNode.Expand();
                }
            }
            finally
            {
                treeViewAD.EndUpdate();
            }
        }

        /// <summary>
        /// Ermittelt den Parent-DN einer OU.
        /// </summary>
        private string GetParentDN(string distinguishedName)
        {
            if (string.IsNullOrEmpty(distinguishedName)) return "";
            
            var parts = distinguishedName.Split(',');
            if (parts.Length <= 1) return "";
            
            return string.Join(",", parts.Skip(1));
        }

        /// <summary>
        /// Aktualisiert eine einzelne OU im TreeView.
        /// </summary>
        private async Task RefreshOUAsync(TreeNode node, ADOrganizationalUnit ou)
        {
            try
            {
                var selectedDC = cmbDomainControllers.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(selectedDC)) return;

                lblADStatus.Text = $"Refreshing {ou.Name}...";
                lblADStatus.ForeColor = System.Drawing.Color.Blue;

                // Lade Computer-Count neu
                var script = $@"
$computers = Get-ADComputer -SearchBase '{ou.DistinguishedName.Replace("'", "''")}' -SearchScope OneLevel -Filter * -ErrorAction SilentlyContinue
$count = ($computers | Measure-Object).Count
[PSCustomObject]@{{ ComputerCount = $count }}
";

                var results = await ADPowerShellExecutor.InvokeADScriptAsync(selectedDC, script, GetCredentialsForServer);
                
                if (results.Any())
                {
                    var newCount = Convert.ToInt32(results[0].Properties["ComputerCount"]?.Value ?? 0);
                    ou.ComputerCount = newCount;
                    node.Text = $"{ou.Name} ({newCount} computers)";
                    node.ToolTipText = $"DN: {ou.DistinguishedName}\nDescription: {ou.Description}\nComputers: {newCount}";
                }

                lblADStatus.Text = "Refresh completed";
                lblADStatus.ForeColor = System.Drawing.Color.Green;
            }
            catch (Exception ex)
            {
                lblADStatus.Text = $"Refresh failed: {ex.Message}";
                lblADStatus.ForeColor = System.Drawing.Color.Red;
            }
        }

        /// <summary>
        /// Zeigt die Computerobjekte einer OU in einem separaten Dialog.
        /// </summary>
        private async Task ShowComputersInOUAsync(ADOrganizationalUnit ou)
        {
            try
            {
                var selectedDC = cmbDomainControllers.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(selectedDC)) return;

                await WaitDialog.RunAsync(this, $"Loading computers from {ou.Name}...", async () =>
                {
                    var results = await ADPowerShellExecutor.LoadComputersFromOUAsync(selectedDC, ou.DistinguishedName, GetCredentialsForServer);
                    
                    this.BeginInvoke(new Action(() =>
                    {
                        ShowComputersDialog(ou, results);
                    }));
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to load computers: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Zeigt einen Dialog mit den Computerobjekten einer OU.
        /// </summary>
        private void ShowComputersDialog(ADOrganizationalUnit ou, System.Collections.ObjectModel.Collection<PSObject> computers)
        {
            var dialog = new Form
            {
                Text = $"Computers in {ou.Name}",
                Size = new System.Drawing.Size(800, 600),
                StartPosition = FormStartPosition.CenterParent,
                ShowInTaskbar = false,
                MinimizeBox = false,
                MaximizeBox = true
            };

            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };

            // Definiere Spalten
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Computer Name", DataPropertyName = "Name", Width = 150 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "DNSHostName", HeaderText = "DNS Name", DataPropertyName = "DNSHostName", Width = 200 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "OperatingSystem", HeaderText = "OS", DataPropertyName = "OperatingSystem", Width = 150 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "LastLogonDate", HeaderText = "Last Logon", DataPropertyName = "LastLogonDate", Width = 130 });
            dgv.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "Enabled", DataPropertyName = "Enabled", Width = 70 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "Description", DataPropertyName = "Description", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

            // Erstelle DataTable
            var dt = new DataTable();
            dt.Columns.Add("Name", typeof(string));
            dt.Columns.Add("DNSHostName", typeof(string));
            dt.Columns.Add("OperatingSystem", typeof(string));
            dt.Columns.Add("LastLogonDate", typeof(string));
            dt.Columns.Add("Enabled", typeof(bool));
            dt.Columns.Add("Description", typeof(string));

            foreach (var computer in computers)
            {
                var row = dt.NewRow();
                row["Name"] = computer.Properties["Name"]?.Value?.ToString() ?? "";
                row["DNSHostName"] = computer.Properties["DNSHostName"]?.Value?.ToString() ?? "";
                row["OperatingSystem"] = computer.Properties["OperatingSystem"]?.Value?.ToString() ?? "";
                row["LastLogonDate"] = computer.Properties["LastLogonDate"]?.Value?.ToString() ?? "";
                row["Enabled"] = Convert.ToBoolean(computer.Properties["Enabled"]?.Value ?? false);
                row["Description"] = computer.Properties["Description"]?.Value?.ToString() ?? "";
                dt.Rows.Add(row);
            }

            dgv.DataSource = dt;

            var statusLabel = new Label
            {
                Text = $"Showing {dt.Rows.Count} computers from OU: {ou.DistinguishedName}",
                Dock = DockStyle.Bottom,
                Height = 25,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 4, 6, 4)
            };

            dialog.Controls.Add(dgv);
            dialog.Controls.Add(statusLabel);

            dialog.ShowDialog(this);
        }

        /// <summary>
        /// Füllt das TreeView mit der erweiterten AD-Struktur (OUs und Computer).
        /// </summary>
        private void PopulateADTreeViewWithComputers(System.Collections.ObjectModel.Collection<PSObject> results)
        {
            treeViewAD.BeginUpdate();
            try
            {
                treeViewAD.Nodes.Clear();

                // Ermittle die Standard-Computer-OU der Domäne (mit Timeout und separatem Thread)
                var selectedDC = cmbDomainControllers.SelectedItem?.ToString();
                var defaultComputerOU = "";
                
                try
                {
                    DebugLogger.LogFormat("Attempting to determine default computer OU for DC: {0}", selectedDC ?? "localhost");
                    
                    // Führe die Standard-OU Bestimmung in einem separaten Task mit Timeout aus
                    var defaultOUTask = Task.Run(() => {
                        try
                        {
                            return ADDiscovery.GetDefaultComputerOU(selectedDC);
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.LogFormat("Exception in GetDefaultComputerOU task: {0}", ex.Message);
                            return "";
                        }
                    });
                    
                    // Warte maximal 3 Sekunden auf das Ergebnis
                    if (defaultOUTask.Wait(TimeSpan.FromSeconds(3)))
                    {
                        defaultComputerOU = defaultOUTask.Result ?? "";
                        DebugLogger.LogFormat("Default Computer OU determined: {0}", defaultComputerOU);
                    }
                    else
                    {
                        DebugLogger.LogFormat("Timeout while determining default computer OU - continuing without it");
                        defaultComputerOU = "";
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.LogFormat("Error in default computer OU task handling: {0} - {1}", ex.Message, ex.GetType().Name);
                    defaultComputerOU = "";
                }

                // Konvertiere PSObjects zu ADTreeItems
                var items = new List<ADTreeItem>();
                foreach (var result in results)
                {
                    var distinguishedName = result.Properties["DistinguishedName"]?.Value?.ToString() ?? "";
                    var item = new ADTreeItem
                    {
                        Type = result.Properties["Type"]?.Value?.ToString() ?? "",
                        Name = result.Properties["Name"]?.Value?.ToString() ?? "",
                        DistinguishedName = distinguishedName,
                        Description = result.Properties["Description"]?.Value?.ToString() ?? "",
                        ParentDN = result.Properties["ParentDN"]?.Value?.ToString() ?? "",
                        ComputerCount = Convert.ToInt32(result.Properties["ComputerCount"]?.Value ?? 0),
                        Enabled = Convert.ToBoolean(result.Properties["Enabled"]?.Value ?? true),
                        OperatingSystem = result.Properties["OperatingSystem"]?.Value?.ToString() ?? "",
                        LastLogonDate = result.Properties["LastLogonDate"]?.Value?.ToString() ?? "",
                        // Markiere als Standard-Computer-OU wenn DN übereinstimmt
                        IsDefaultComputerOU = !string.IsNullOrEmpty(defaultComputerOU) && 
                                            string.Equals(distinguishedName, defaultComputerOU, StringComparison.OrdinalIgnoreCase)
                    };
                    items.Add(item);
                }

                // Stelle sicher, dass die Standard-Computer-OU immer in der Liste ist, auch wenn sie leer ist
                try
                {
                    if (!string.IsNullOrEmpty(defaultComputerOU))
                    {
                        var existingDefaultOU = items.FirstOrDefault(i => 
                            string.Equals(i.DistinguishedName, defaultComputerOU, StringComparison.OrdinalIgnoreCase));
                        
                        if (existingDefaultOU == null)
                        {
                            // Standard-OU ist nicht in der Liste (weil leer) - füge sie hinzu
                            try
                            {
                                var defaultOUInfo = ADDiscovery.GetDefaultComputerOUInfo(selectedDC);
                                if (defaultOUInfo.IsConfigured && !defaultOUInfo.HasError)
                                {
                                    var defaultOUItem = new ADTreeItem
                                    {
                                        Type = defaultOUInfo.IsOrganizationalUnit ? "OU" : "Container",
                                        Name = defaultOUInfo.Name,
                                        DistinguishedName = defaultOUInfo.DistinguishedName,
                                        Description = defaultOUInfo.Description,
                                        ParentDN = ExtractParentDN(defaultOUInfo.DistinguishedName),
                                        ComputerCount = defaultOUInfo.ComputerCount,
                                        Enabled = true,
                                        IsDefaultComputerOU = true
                                    };
                                    items.Add(defaultOUItem);
                                    DebugLogger.LogFormat("Added empty default computer OU to tree: {0}", defaultOUInfo.Name);
                                }
                            }
                            catch (Exception ex)
                            {
                                DebugLogger.LogFormat("Error adding default computer OU: {0}", ex.Message);
                            }
                        }
                        else
                        {
                            // Standard-OU ist bereits in der Liste - markiere sie
                            existingDefaultOU.IsDefaultComputerOU = true;
                            DebugLogger.LogFormat("Marked existing OU as default: {0}", existingDefaultOU.Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.LogFormat("Critical error in default computer OU processing: {0} - {1}", ex.Message, ex.GetType().Name);
                    DebugLogger.LogFormat("Stack trace: {0}", ex.StackTrace);
                    // Weiter mit der normalen Verarbeitung, auch wenn die Standard-OU Funktionalität fehlschlägt
                }

                // Erstelle Dictionary für schnelle Suche
                var nodeDict = new Dictionary<string, TreeNode>();
                
                // Extrahiere Domänen-Name aus dem ersten Item
                var domainName = "";
                if (items.Any())
                {
                    var firstItem = items.First();
                    domainName = ExtractDomainFromDN(firstItem.DistinguishedName);
                }
                
                // Erstelle Domänen-Root-Knoten
                TreeNode domainRootNode = null;
                if (!string.IsNullOrEmpty(domainName))
                {
                    domainRootNode = new TreeNode($"🌐 {domainName}")
                    {
                        Tag = new ADTreeItem 
                        { 
                            Name = domainName, 
                            Type = "Domain", 
                            DistinguishedName = string.Join(",", domainName.Split('.').Select(part => $"DC={part}"))
                        },
                        ToolTipText = $"Active Directory Domain: {domainName}",
                        ForeColor = System.Drawing.Color.DarkGreen
                    };
                    treeViewAD.Nodes.Add(domainRootNode);
                    DebugLogger.LogFormat("Created domain root node: {0}", domainName);
                }
                
                // Sortiere Items: OUs/Container zuerst (nach DN-Länge, Eltern zuerst), dann Computer
                var sortedItems = items.OrderBy(i => i.IsComputer ? 1 : 0)
                                      .ThenBy(i => i.DistinguishedName.Split(',').Length)
                                      .ThenBy(i => i.DistinguishedName)
                                      .ToList();

                // Debug: Zeige alle OUs/Container in Sortierreihenfolge
                DebugLogger.Log("OUs/Container in processing order:");
                foreach (var item in sortedItems.Where(i => !i.IsComputer).Take(20))
                {
                    var depth = item.DistinguishedName.Split(',').Length;
                    var indent = new string(' ', (depth - 3) * 2);
                    DebugLogger.LogFormat("{0}- {1}: {2} (Depth: {3})", indent, item.Type, item.Name, depth);
                    DebugLogger.LogFormat("{0}  DN: {1}", indent, item.DistinguishedName);
                    DebugLogger.LogFormat("{0}  Parent: {1}", indent, item.ParentDN);
                }

                // Debug: Zeige Statistiken
                var ouCount = sortedItems.Count(i => i.IsOU);
                var containerCount = sortedItems.Count(i => i.IsContainer);
                var computerCount = sortedItems.Count(i => i.IsComputer);
                DebugLogger.LogSeparator("AD TREE STRUCTURE ANALYSIS");
                DebugLogger.LogFormat("Processing {0} OUs, {1} Containers, {2} Computers", ouCount, containerCount, computerCount);
                
                // Schreibe detaillierte Analyse in Datei (fire and forget)
                _ = Task.Run(() => WriteDetailedADAnalysisToFile(sortedItems));
                
                // Debug: Zeige erste 10 Items
                DebugLogger.Log("First 10 items:");
                foreach (var item in sortedItems.Take(10))
                {
                    DebugLogger.LogFormat("- {0}: {1} (DN: {2})", item.Type, item.Name, item.DistinguishedName);
                    DebugLogger.LogFormat("  Parent: {0}", item.ParentDN);
                }

                foreach (var item in sortedItems)
                {
                    var node = new TreeNode(item.DisplayText)
                    {
                        Tag = item,
                        ToolTipText = item.ToolTipText
                    };

                    // Setze Farbe basierend auf Typ und Status
                    if (item.IsComputer)
                    {
                        node.ForeColor = item.Enabled ? System.Drawing.Color.Black : System.Drawing.Color.Gray;
                    }
                    else if (item.IsOU || item.IsContainer)
                    {
                        // OUs und Container behalten schwarzen Text
                        node.ForeColor = System.Drawing.Color.Black;
                    }

                    // Füge OUs/Container zuerst hinzu
                    if (item.IsOU || item.IsContainer)
                    {
                        DebugLogger.LogFormat("Processing {0} '{1}' (DN: {2}, Parent: {3})", item.Type, item.Name, item.DistinguishedName, item.ParentDN);
                        
                        // Finde Parent-Node für OU/Container
                        if (!string.IsNullOrEmpty(item.ParentDN) && nodeDict.ContainsKey(item.ParentDN))
                        {
                            DebugLogger.LogFormat("Adding {0} to parent {1}", item.Name, item.ParentDN);
                            nodeDict[item.ParentDN].Nodes.Add(node);
                        }
                        else
                        {
                            // Prüfe, ob Parent-DN leer ist oder nur Domain-Komponenten enthält
                            var isTopLevel = string.IsNullOrEmpty(item.ParentDN) || 
                                           (!item.ParentDN.Contains("OU=") && !item.ParentDN.Contains("CN=")) ||
                                           item.ParentDN.StartsWith("DC=");
                            
                            DebugLogger.LogFormat("Adding {0} to root (Parent: {1}, InDict: {2}, IsTopLevel: {3})", 
                                item.Name, item.ParentDN, nodeDict.ContainsKey(item.ParentDN ?? ""), isTopLevel);
                            
                            // Prüfe, ob dies ein Root-Container ist (direkt unter der Domäne)
                            if (isTopLevel && domainRootNode != null)
                            {
                                DebugLogger.LogFormat("Adding {0} as child of domain root", item.Name);
                                domainRootNode.Nodes.Add(node);
                            }
                            else
                            {
                                // Alle OUs/Container direkt unter der Domain werden als Root-Knoten hinzugefügt
                                treeViewAD.Nodes.Add(node);
                            }
                        }
                        nodeDict[item.DistinguishedName] = node;
                    }
                    else if (item.IsComputer)
                    {
                        DebugLogger.LogFormat("Processing Computer '{0}' (Parent: {1})", item.Name, item.ParentDN);
                        
                        // Finde Parent-Node für Computer
                        if (!string.IsNullOrEmpty(item.ParentDN) && nodeDict.ContainsKey(item.ParentDN))
                        {
                            DebugLogger.LogFormat("Adding computer {0} to parent {1}", item.Name, item.ParentDN);
                            nodeDict[item.ParentDN].Nodes.Add(node);
                        }
                        else
                        {
                            DebugLogger.LogFormat("FALLBACK - Adding computer {0} to root (Parent: {1}, InDict: {2})", item.Name, item.ParentDN, nodeDict.ContainsKey(item.ParentDN ?? ""));
                            DebugLogger.LogFormat("Available keys in nodeDict: {0}", string.Join(", ", nodeDict.Keys.Take(5)));
                            DebugLogger.LogFormat("Total nodeDict entries: {0}", nodeDict.Count);
                            
                            // Prüfe, ob Parent-DN ähnliche Einträge hat
                            if (!string.IsNullOrEmpty(item.ParentDN))
                            {
                                var similarKeys = nodeDict.Keys.Where(k => k.Contains(item.ParentDN.Split(',')[0])).Take(3);
                                DebugLogger.LogFormat("Similar keys for '{0}': {1}", item.ParentDN, string.Join(", ", similarKeys));
                            }
                            
                            treeViewAD.Nodes.Add(node);
                        }
                    }
                }

                // Expandiere den Domänen-Root-Knoten automatisch
                if (domainRootNode != null)
                {
                    domainRootNode.Expand();
                    DebugLogger.LogFormat("Expanded domain root node: {0}", domainName);
                    
                    // Expandiere die ersten paar Container unter der Domäne
                    for (int i = 0; i < Math.Min(3, domainRootNode.Nodes.Count); i++)
                    {
                        var containerNode = domainRootNode.Nodes[i];
                        var item = (ADTreeItem)containerNode.Tag;
                        if (item.IsOU || item.IsContainer)
                        {
                            containerNode.Expand();
                            DebugLogger.LogFormat("Expanded container: {0}", item.Name);
                        }
                    }
                }
                
                // Expandiere auch andere Root-Knoten (falls vorhanden)
                for (int i = 0; i < Math.Min(3, treeViewAD.Nodes.Count); i++)
                {
                    var rootNode = treeViewAD.Nodes[i];
                    if (rootNode != domainRootNode) // Nicht den Domänen-Knoten nochmal expandieren
                    {
                        rootNode.Expand();
                        
                        // Expandiere nur die ersten paar Child-Knoten
                        for (int j = 0; j < Math.Min(2, rootNode.Nodes.Count); j++)
                        {
                            var childNode = rootNode.Nodes[j];
                            var item = (ADTreeItem)childNode.Tag;
                            if (item.IsOU || item.IsContainer)
                            {
                                childNode.Expand();
                            }
                        }
                    }
                }

                DebugLogger.LogFormat("Total root nodes created: {0}", treeViewAD.Nodes.Count);
                for (int i = 0; i < Math.Min(3, treeViewAD.Nodes.Count); i++)
                {
                    var rootItem = (ADTreeItem)treeViewAD.Nodes[i].Tag;
                    DebugLogger.LogFormat("Root node {0}: {1} ({2})", i, rootItem.Name, rootItem.Type);
                }

                // Stelle sicher, dass der erste Knoten sichtbar ist
                if (treeViewAD.Nodes.Count > 0)
                {
                    DebugLogger.LogSeparator("FINAL TREEVIEW STATE");
                    DebugLogger.LogFormat("TreeView Size: {0}, Location: {1}, Visible: {2}", treeViewAD.Size, treeViewAD.Location, treeViewAD.Visible);
                    DebugLogger.LogFormat("TreeView Parent: {0}, Dock: {1}", treeViewAD.Parent?.GetType().Name, treeViewAD.Dock);
                    DebugLogger.LogFormat("TreeView Nodes Count: {0}", treeViewAD.Nodes.Count);
                    
                    // Debug: Zeige alle Root-Knoten
                    DebugLogger.Log("Root nodes in TreeView:");
                    for (int i = 0; i < treeViewAD.Nodes.Count; i++)
                    {
                        var rootNode = treeViewAD.Nodes[i];
                        var rootItem = (ADTreeItem)rootNode.Tag;
                        DebugLogger.LogFormat("  [{0}] {1}: {2} (Children: {3})", i, rootItem.Type, rootItem.Name, rootNode.Nodes.Count);
                    }
                    
                    // Schreibe TreeView-Analyse in Datei
                    _ = Task.Run(() => WriteTreeViewAnalysisToFile(treeViewAD));
                    
                    // Verwende die radikale Reset-Methode
                    ResetTreeViewScrollPosition(treeViewAD);
                    
                    // Force TreeView refresh
                    treeViewAD.Refresh();
                    treeViewAD.Update();
                    
                    // Expand first node to make it visible
                    if (treeViewAD.Nodes.Count > 0)
                    {
                        treeViewAD.Nodes[0].Expand();
                        treeViewAD.SelectedNode = treeViewAD.Nodes[0];
                    }
                    
                    DebugLogger.LogFormat("Set TopNode to {0}", ((ADTreeItem)treeViewAD.Nodes[0].Tag).Name);
                    DebugLogger.LogFormat("TreeView after refresh - Nodes visible: {0}, First node expanded: {1}", 
                        treeViewAD.Nodes.Count, treeViewAD.Nodes.Count > 0 ? treeViewAD.Nodes[0].IsExpanded : false);
                    
                    // Detaillierte TreeView-Analyse in Log
                    var nodeNames = string.Join(", ", treeViewAD.Nodes.Cast<TreeNode>().Select(n => ((ADTreeItem)n.Tag).Name));
                    DebugLogger.LogFormat("AD-Struktur geladen - Root-Knoten: {0}", nodeNames);
                    
                    // Erweiterte TreeView-Diagnose
                    DebugLogger.LogSeparator("EXTENDED TREEVIEW DIAGNOSIS");
                    DebugLogger.LogFormat("TreeView Handle Created: {0}", treeViewAD.IsHandleCreated);
                    DebugLogger.LogFormat("TreeView Focused: {0}", treeViewAD.Focused);
                    DebugLogger.LogFormat("TreeView TabStop: {0}", treeViewAD.TabStop);
                    DebugLogger.LogFormat("TreeView TopLevelControl: {0}", treeViewAD.TopLevelControl?.GetType().Name ?? "null");
                    
                    // Parent-Chain-Analyse
                    var parent = treeViewAD.Parent;
                    var level = 0;
                    while (parent != null && level < 5)
                    {
                        DebugLogger.LogFormat("Parent Level {0}: {1} (Visible: {2}, Size: {3})", 
                            level, parent.GetType().Name, parent.Visible, parent.Size);
                        parent = parent.Parent;
                        level++;
                    }
                    
                    // TreeView-Bounds-Analyse
                    DebugLogger.LogFormat("TreeView Bounds: {0}", treeViewAD.Bounds);
                    DebugLogger.LogFormat("TreeView ClientRectangle: {0}", treeViewAD.ClientRectangle);
                    DebugLogger.LogFormat("TreeView DisplayRectangle: {0}", treeViewAD.DisplayRectangle);
                    
                    // Tab-System-Analyse
                    var tabControl = treeViewAD.FindForm()?.Controls.OfType<TabControl>().FirstOrDefault();
                    if (tabControl != null)
                    {
                        DebugLogger.LogFormat("TabControl found - Selected Tab: {0}", tabControl.SelectedTab?.Text ?? "null");
                        DebugLogger.LogFormat("TabControl Tab Count: {0}", tabControl.TabCount);
                        var adTab = tabControl.TabPages.Cast<TabPage>().FirstOrDefault(t => t.Text.Contains("Active Directory"));
                        if (adTab != null)
                        {
                            DebugLogger.LogFormat("AD Tab found - Visible: {0}, Selected: {1}", adTab.Visible, tabControl.SelectedTab == adTab);
                            DebugLogger.LogFormat("AD Tab Size: {0}, Controls: {1}", adTab.Size, adTab.Controls.Count);
                        }
                    }
                    
                    // Force TreeView to front
                    treeViewAD.BringToFront();
                    treeViewAD.Parent?.BringToFront();
                    
                    // Scroll-Position-Analyse
                    if (treeViewAD.Nodes.Count > 0)
                    {
                        var topNode = treeViewAD.TopNode;
                        DebugLogger.LogFormat("TreeView TopNode: {0}", topNode != null ? ((ADTreeItem)topNode.Tag).Name : "null");
                        DebugLogger.LogFormat("TreeView VisibleCount: {0}", treeViewAD.VisibleCount);
                        
                        // Prüfe, ob alle Root-Knoten im sichtbaren Bereich sind
                        for (int i = 0; i < Math.Min(treeViewAD.Nodes.Count, 5); i++)
                        {
                            var node = treeViewAD.Nodes[i];
                            var bounds = node.Bounds;
                            var isVisible = bounds.Y >= 0 && bounds.Y < treeViewAD.ClientRectangle.Height;
                            DebugLogger.LogFormat("Root Node {0}: {1} - Bounds: {2}, Visible in viewport: {3}", 
                                i, ((ADTreeItem)node.Tag).Name, bounds, isVisible);
                        }
                    }
                    
                    DebugLogger.LogFormat("TreeView brought to front - Final visibility check completed");
                }
            }
            finally
            {
                treeViewAD.EndUpdate();
                
                // Nach dem Update nochmals sicherstellen, dass wir oben sind
                if (treeViewAD.Nodes.Count > 0)
                {
                    // Verwende BeginInvoke um sicherzustellen, dass das Layout abgeschlossen ist
                    this.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            DebugLogger.Log("Starting final BeginInvoke scroll correction");
                            
                            // Nochmals die radikale Reset-Methode
                            ResetTreeViewScrollPosition(treeViewAD);
                            
                            // Zusätzlich: Focus setzen
                            treeViewAD.Focus();
                            
                            // Starte den kontinuierlichen Scroll-Timer
                            StartScrollTimer(treeViewAD);
                            
                            DebugLogger.Log("Final BeginInvoke scroll correction completed with timer");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"DEBUG C#: Error in final scroll: {ex.Message}");
                        }
                    }));
                }
            }
        }

        /// <summary>
        /// Zählt die OUs in einem TreeNode und seinen Kindern.
        /// </summary>
        private int CountOUsInNode(TreeNode node)
        {
            int count = 0;
            if (node.Tag is ADTreeItem item && (item.IsOU || item.IsContainer))
                count = 1;

            foreach (TreeNode child in node.Nodes)
            {
                count += CountOUsInNode(child);
            }
            return count;
        }

        /// <summary>
        /// Zählt die Computer in einem TreeNode und seinen Kindern.
        /// </summary>
        private int CountComputersInNode(TreeNode node)
        {
            int count = 0;
            if (node.Tag is ADTreeItem item && item.IsComputer)
                count = 1;

            foreach (TreeNode child in node.Nodes)
            {
                count += CountComputersInNode(child);
            }
            return count;
        }

        /// <summary>
        /// Pingt einen Computer und zeigt das Ergebnis an.
        /// </summary>
        private async Task PingComputerAsync(string computerName)
        {
            try
            {
                lblADStatus.Text = $"Pinging {computerName}...";
                lblADStatus.ForeColor = System.Drawing.Color.Blue;

                var result = await PingHelper.PingAsync(computerName);
                
                if (result.Success)
                {
                    lblADStatus.Text = $"Ping to {computerName}: {result.RoundtripTime}ms";
                    lblADStatus.ForeColor = System.Drawing.Color.Green;
                    
                    MessageBox.Show(this, 
                        $"Ping to {computerName} successful!\n\nRoundtrip time: {result.RoundtripTime}ms\nStatus: {result.Status}", 
                        "Ping Result", 
                        MessageBoxButtons.OK, 
                        MessageBoxIcon.Information);
                }
                else
                {
                    lblADStatus.Text = $"Ping to {computerName} failed: {result.Status}";
                    lblADStatus.ForeColor = System.Drawing.Color.Red;
                    
                    MessageBox.Show(this, 
                        $"Ping to {computerName} failed!\n\nStatus: {result.Status}", 
                        "Ping Result", 
                        MessageBoxButtons.OK, 
                        MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                lblADStatus.Text = $"Ping error: {ex.Message}";
                lblADStatus.ForeColor = System.Drawing.Color.Red;
                
                MessageBox.Show(this, 
                    $"Error pinging {computerName}:\n\n{ex.Message}", 
                    "Ping Error", 
                    MessageBoxButtons.OK, 
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Wird aufgerufen, wenn der Active Directory Tab ausgewählt wird.
        /// </summary>
        private async Task OnActiveDirectoryTabSelectedAsync()
        {
            // Diese Methode ist für zukünftige Erweiterungen vorgesehen
            // Die automatische Initialisierung erfolgt bereits über den SelectedIndexChanged-Handler
            await Task.CompletedTask;
        }

        /// <summary>
        /// Schreibt eine TreeView-Analyse in eine Datei für Debugging.
        /// </summary>
        private async Task WriteTreeViewAnalysisToFile(TreeView treeView)
        {
            try
            {
                var filePath = Path.Combine(Path.GetTempPath(), "DhcpWmiViewer-TreeView-Analysis.log");
                var sb = new System.Text.StringBuilder();
                
                sb.AppendLine($"=== TREEVIEW ANALYSIS - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                sb.AppendLine($"TreeView Properties:");
                sb.AppendLine($"  Visible: {treeView.Visible}");
                sb.AppendLine($"  Enabled: {treeView.Enabled}");
                sb.AppendLine($"  Size: {treeView.Size}");
                sb.AppendLine($"  Location: {treeView.Location}");
                sb.AppendLine($"  Dock: {treeView.Dock}");
                sb.AppendLine($"  Parent: {treeView.Parent?.GetType().Name ?? "NULL"}");
                sb.AppendLine($"  Nodes Count: {treeView.Nodes.Count}");
                sb.AppendLine();
                
                sb.AppendLine("=== ROOT NODES ===");
                for (int i = 0; i < treeView.Nodes.Count; i++)
                {
                    var rootNode = treeView.Nodes[i];
                    var rootItem = (ADTreeItem)rootNode.Tag;
                    sb.AppendLine($"[{i}] {rootItem.Type}: {rootItem.Name}");
                    sb.AppendLine($"    DN: {rootItem.DistinguishedName}");
                    sb.AppendLine($"    Parent: {rootItem.ParentDN ?? "NULL"}");
                    sb.AppendLine($"    Children: {rootNode.Nodes.Count}");
                    sb.AppendLine($"    Text: '{rootNode.Text}'");
                    sb.AppendLine($"    Visible: {rootNode.IsVisible}");
                    sb.AppendLine($"    Expanded: {rootNode.IsExpanded}");
                    sb.AppendLine();
                    
                    // Zeige erste 5 Kinder
                    if (rootNode.Nodes.Count > 0)
                    {
                        sb.AppendLine($"    First {Math.Min(5, rootNode.Nodes.Count)} children:");
                        for (int j = 0; j < Math.Min(5, rootNode.Nodes.Count); j++)
                        {
                            var childNode = rootNode.Nodes[j];
                            var childItem = (ADTreeItem)childNode.Tag;
                            sb.AppendLine($"      [{j}] {childItem.Type}: {childItem.Name}");
                        }
                        if (rootNode.Nodes.Count > 5)
                        {
                            sb.AppendLine($"      ... and {rootNode.Nodes.Count - 5} more");
                        }
                        sb.AppendLine();
                    }
                }
                
                await File.WriteAllTextAsync(filePath, sb.ToString());
                
                // Zeige Pfad in Status
                if (lblADStatus != null)
                {
                    this.Invoke(new Action(() =>
                    {
                        lblADStatus.Text = $"TreeView Analysis written to: {filePath}";
                        lblADStatus.ForeColor = System.Drawing.Color.Green;
                    }));
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogError("Failed to write TreeView analysis", ex);
            }
        }

        /// <summary>
        /// Schreibt eine detaillierte AD-Analyse in eine Datei für Debugging.
        /// </summary>
        private async Task WriteDetailedADAnalysisToFile(List<ADTreeItem> items)
        {
            try
            {
                var filePath = Path.Combine(Path.GetTempPath(), "DhcpWmiViewer-AD-Analysis.log");
                var sb = new System.Text.StringBuilder();
                
                sb.AppendLine($"=== AD STRUCTURE ANALYSIS - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                sb.AppendLine($"Total Items: {items.Count}");
                sb.AppendLine($"OUs: {items.Count(i => i.IsOU)}");
                sb.AppendLine($"Containers: {items.Count(i => i.IsContainer)}");
                sb.AppendLine($"Computers: {items.Count(i => i.IsComputer)}");
                sb.AppendLine();
                
                sb.AppendLine("=== ALL ITEMS (sorted by processing order) ===");
                foreach (var item in items)
                {
                    var depth = item.DistinguishedName.Split(',').Length;
                    var indent = new string(' ', Math.Max(0, (depth - 3) * 2));
                    sb.AppendLine($"{indent}- {item.Type}: {item.Name} (Depth: {depth})");
                    sb.AppendLine($"{indent}  DN: {item.DistinguishedName}");
                    sb.AppendLine($"{indent}  Parent: {item.ParentDN ?? "NULL"}");
                    if (item.IsComputer)
                    {
                        sb.AppendLine($"{indent}  OS: {item.OperatingSystem}");
                        sb.AppendLine($"{indent}  Enabled: {item.Enabled}");
                    }
                    else
                    {
                        sb.AppendLine($"{indent}  ComputerCount: {item.ComputerCount}");
                    }
                    sb.AppendLine();
                }
                
                sb.AppendLine("=== HIERARCHY ANALYSIS ===");
                var ouItems = items.Where(i => i.IsOU || i.IsContainer).ToList();
                sb.AppendLine($"OU/Container Items: {ouItems.Count}");
                
                // Analysiere Parent-Child-Beziehungen
                var parentChildMap = new Dictionary<string, List<string>>();
                var rootItems = new List<string>();
                
                foreach (var item in ouItems)
                {
                    if (string.IsNullOrEmpty(item.ParentDN) || 
                        (!item.ParentDN.Contains("OU=") && !item.ParentDN.Contains("CN=")))
                    {
                        rootItems.Add(item.DistinguishedName);
                        sb.AppendLine($"ROOT ITEM: {item.Name} (DN: {item.DistinguishedName})");
                    }
                    else
                    {
                        if (!parentChildMap.ContainsKey(item.ParentDN))
                            parentChildMap[item.ParentDN] = new List<string>();
                        parentChildMap[item.ParentDN].Add(item.DistinguishedName);
                    }
                }
                
                sb.AppendLine($"Root Items Found: {rootItems.Count}");
                sb.AppendLine($"Parent-Child Relationships: {parentChildMap.Count}");
                
                sb.AppendLine();
                sb.AppendLine("=== PARENT-CHILD MAPPING ===");
                foreach (var kvp in parentChildMap)
                {
                    sb.AppendLine($"Parent: {kvp.Key}");
                    foreach (var child in kvp.Value)
                    {
                        var childItem = items.FirstOrDefault(i => i.DistinguishedName == child);
                        sb.AppendLine($"  -> Child: {childItem?.Name ?? "UNKNOWN"} ({child})");
                    }
                    sb.AppendLine();
                }
                
                await File.WriteAllTextAsync(filePath, sb.ToString());
                
                // Zeige Pfad in Status
                if (lblADStatus != null)
                {
                    this.Invoke(new Action(() =>
                    {
                        lblADStatus.Text = $"AD Analysis written to: {filePath}";
                        lblADStatus.ForeColor = System.Drawing.Color.Blue;
                    }));
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogError("Failed to write AD analysis", ex);
            }
        }

        /// <summary>
        /// Extrahiert den Domänen-Namen aus einem Distinguished Name.
        /// </summary>
        private string ExtractDomainFromDN(string dn)
        {
            if (string.IsNullOrEmpty(dn)) return "";
            
            try
            {
                var parts = dn.Split(',');
                var dcParts = parts
                    .Where(p => p.Trim().StartsWith("DC=", StringComparison.OrdinalIgnoreCase))
                    .Select(p => p.Trim().Substring(3))
                    .ToArray();
                
                return string.Join(".", dcParts);
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Extrahiert den Parent-DN aus einem Distinguished Name.
        /// </summary>
        private string ExtractParentDN(string dn)
        {
            if (string.IsNullOrEmpty(dn)) return "";
            
            try
            {
                var firstCommaIndex = dn.IndexOf(',');
                if (firstCommaIndex == -1) return "";
                
                return dn.Substring(firstCommaIndex + 1).Trim();
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Prüft, ob ein Item ein Root-Container ist (direkt unter der Domäne).
        /// </summary>
        private bool IsRootContainer(ADTreeItem item)
        {
            if (string.IsNullOrEmpty(item.DistinguishedName)) return false;
            
            try
            {
                // Zähle die Anzahl der DC-Komponenten im DN
                var parts = item.DistinguishedName.Split(',');
                var dcCount = parts.Count(p => p.Trim().StartsWith("DC=", StringComparison.OrdinalIgnoreCase));
                var nonDcCount = parts.Length - dcCount;
                
                // Root-Container haben nur eine Nicht-DC-Komponente (sich selbst)
                return nonDcCount == 1;
            }
            catch
            {
                return false;
            }
        }
    }
}