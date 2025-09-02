// MainForm.LayoutHelpers.cs

using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;

namespace DhcpWmiViewer
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// Erstellt explizit die Spalten für dgvLeases und mappt DataPropertyName
        /// auf die erwarteten DataTable-Spaltennamen.
        /// </summary>
        private void EnsureLeasesColumns()
        {
            try
            {
                if (dgvLeases == null) return;
                dgvLeases.Columns.Clear();

                void AddCol(string propName, string header, int width = 120)
                {
                    var c = new DataGridViewTextBoxColumn
                    {
                        DataPropertyName = propName, // exact column name in leaseTable
                        Name = propName,
                        HeaderText = header,
                        AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                        Width = width
                    };
                    dgvLeases.Columns.Add(c);
                }

                // Reihenfolge & Spalten bewusst festlegen (Description direkt rechts neben HostName)
                AddCol("IPAddress", "IPAddress", 120);
                AddCol("ClientId", "ClientId", 200);
                AddCol("ClientType", "ClientType", 100);
                AddCol("HostName", "HostName", 160);
                AddCol("Description", "Beschreibung", 200); // NEU: Description rechts neben HostName
                AddCol("AddressState", "AddressState", 100);
                AddCol("LeaseExpiryTime", "LeaseExpiryTime", 160);
                AddCol("ScopeId", "ScopeId", 140);
                AddCol("ServerIP", "ServerIP", 140);
                AddCol("PSComputerName", "PSComputerName", 140);
                AddCol("CimClass", "CimClass", 120);
                AddCol("CimInstanceProperties", "CimInstanceProperties", 200);
                AddCol("CimSystemProperties", "CimSystemProperties", 200);
                // Falls Deine DataTable zusätzliche Felder hat, füge sie hier hinzu.
            }
            catch
            {
                // defensive: falls etwas schiefgeht, lass AutoGenerateColumns greifen (fallback)
                if (dgvLeases != null) dgvLeases.AutoGenerateColumns = true;
            }
        }

        /// <summary>
        /// Robustere Version: setzt feste Breiten für alle Spalten basierend auf preferred widths.
        /// Misst temporär alle Spalten mit AllCells, wendet danach feste widths an und verteilt
        /// Restplatz auf die letzte Spalte.
        /// </summary>
        private void AdjustLeasesColumnWidths()
        {
            try
            {
                // Erweiterte Null-Checks und Schutz vor Race Conditions
                if (dgvLeases == null || dgvLeases.IsDisposed || dgvLeases.Disposing) return;
                if (dgvLeases.Columns == null || dgvLeases.Columns.Count == 0) return;
                
                // Zusätzlicher Schutz: Prüfe ob das DataGridView sichtbar und bereit ist
                if (!dgvLeases.IsHandleCreated || !dgvLeases.Visible) return;

                dgvLeases.SuspendLayout();
                try
                {
                    const int compactWidth = 80;
                    const int extraPadding = 18;
                    const int maxColumnWidth = 1200;
                    const int minColumnWidth = 40;

                    // ------------- 1) Bestimme bevorzugte Breiten für alle Spalten -------------
                    var preferredWidths = new int[dgvLeases.Columns.Count];

                    // Temporär: setze für jede Spalte AutoSizeMode = AllCells und resize, damit GetPreferredWidth gültig ist
                    for (int i = 0; i < dgvLeases.Columns.Count; i++)
                    {
                        try
                        {
                            var col = dgvLeases.Columns[i];
                            if (col == null) 
                            {
                                preferredWidths[i] = compactWidth;
                                continue;
                            }
                            
                            col.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                            // AutoResizeColumn erzwingt ein Layout-Maß
                            try { dgvLeases.AutoResizeColumn(i, DataGridViewAutoSizeColumnMode.AllCells); } catch { }
                            int pref = col.Width;
                            try { pref = col.GetPreferredWidth(DataGridViewAutoSizeColumnMode.AllCells, true); } catch { /* ignore */ }
                            preferredWidths[i] = Math.Min(maxColumnWidth, Math.Max(minColumnWidth, pref + extraPadding));
                        }
                        catch { preferredWidths[i] = compactWidth; }
                    }

                    // ------------- 2) Wende die berechneten Breiten als feste Widths an -------------
                    dgvLeases.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
                    int totalPreferred = 0;
                    for (int i = 0; i < dgvLeases.Columns.Count; i++)
                    {
                        try
                        {
                            var col = dgvLeases.Columns[i];
                            if (col == null) continue;
                            
                            col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                            col.Width = preferredWidths[i];
                            col.MinimumWidth = minColumnWidth;
                            totalPreferred += col.Width;
                        }
                        catch { /* ignore individual column errors */ }
                    }

                    // ------------- 3) Falls Gesamtbreite kleiner als Anzeige, fülle Rest sinnvoll (Description bevorzugt) -------------
                    int displayWidth = Math.Max(0, dgvLeases.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
                    if (displayWidth <= 0) displayWidth = Math.Max(displayWidth, this.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);

                    if (totalPreferred < displayWidth)
                    {
                        int extra = displayWidth - totalPreferred;
                        if (extra > 0)
                        {
                            // Prefer a descriptive column to take leftover space
                            DataGridViewColumn? target = null;
                            if (dgvLeases.Columns.Contains("Description")) target = dgvLeases.Columns["Description"]; 
                            if (target == null && dgvLeases.Columns.Contains("HostName")) target = dgvLeases.Columns["HostName"]; 
                            if (target == null) target = dgvLeases.Columns[dgvLeases.Columns.Count - 1];
                            if (target != null)
                            {
                                int newWidth = Math.Min(maxColumnWidth, target.Width + extra);
                                target.Width = newWidth;
                            }
                        }
                    }

                    // ------------- 4) Scrollbar Entscheidung -------------
                    try
                    {
                        int totalWidth = dgvLeases.Columns.Cast<DataGridViewColumn>().Sum(cc => cc?.Width ?? 0);
                        dgvLeases.ScrollBars = (totalWidth > displayWidth) ? ScrollBars.Both : ScrollBars.Vertical;
                    }
                    catch { /* ignore scrollbar calculation errors */ }

                    // final refresh
                    dgvLeases.Refresh();
                    dgvLeases.PerformLayout();
                }
                finally
                {
                    try { dgvLeases.ResumeLayout(true); } catch { /* ignore resume layout errors */ }
                }
            }
            catch (Exception ex)
            {
                // Log the error for debugging but don't crash the application
                System.Diagnostics.Debug.WriteLine($"AdjustLeasesColumnWidths error: {ex.Message}");
                try { UpdateStatus("Spaltenbreiten-Anpassung übersprungen."); } catch { }
            }
        }

        /// <summary>
        /// Passt Events-Grid Spalten bei Resize / Bind an (kleiner Helfer).
        /// </summary>
        private void AdjustEventsColumnWidths()
        {
            try
            {
                if (dgvEvents == null || dgvEvents.Columns.Count == 0) return;

                // setze Message-Spalte auf Fill, sichere Breiten für die anderen
                for (int i = 0; i < dgvEvents.Columns.Count; i++)
                {
                    var c = dgvEvents.Columns[i];
                    if (c.Name == "Message")
                    {
                        c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                        c.MinimumWidth = 200;
                    }
                    else
                    {
                        c.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                        if (c.Width <= 0) c.Width = 80;
                        c.MinimumWidth = 60;
                    }
                }

                // force row autosize (damit mehrzeilige Messages sichtbar werden)
                try { dgvEvents.AutoResizeRows(DataGridViewAutoSizeRowsMode.AllCells); } catch { }
            }
            catch
            {
                // swallow
            }
        }

        /// <summary>
        /// DataError event handler for leases DataGridView to prevent crashes from column mismatches
        /// </summary>
        private void DgvLeases_DataError(object? sender, DataGridViewDataErrorEventArgs e)
        {
            try
            {
                // Log the error for debugging
                System.Diagnostics.Debug.WriteLine($"DataGridView DataError: {e.Exception?.Message} at Row={e.RowIndex}, Column={e.ColumnIndex}");
                
                // Suppress the error dialog by setting Cancel to true
                e.Cancel = true;
                
                // Optionally show a user-friendly message in the status bar
                UpdateStatus($"Datenfehler in Zeile {e.RowIndex + 1} behoben.");
            }
            catch
            {
                // If anything goes wrong in the error handler, just suppress the error
                e.Cancel = true;
            }
        }

        /// <summary>
        /// Formatierung: leere/platzhalter ServerIP-Zellen als '-' grau/italic anzeigen.
        /// Wird nach dem Binden (DataSource gesetzt) aufgerufen.
        /// </summary>
        private void FormatServerIpCellsAfterBind()
        {
            if (dgvLeases == null || dgvLeases.Columns.Count == 0) return;
            if (!dgvLeases.Columns.Contains("ServerIP")) return;

            // Iteriere Rows und setze Anzeige für leere Werte
            foreach (DataGridViewRow row in dgvLeases.Rows)
            {
                try
                {
                    if (row.IsNewRow) continue;
                    var cell = row.Cells["ServerIP"];
                    if (cell == null) continue;
                    var val = (cell.Value?.ToString() ?? "").Trim();

                    bool isMask = string.IsNullOrEmpty(val)
                        || val.Equals("255.0.0.0", StringComparison.OrdinalIgnoreCase)
                        || val.Equals("255.255.0.0", StringComparison.OrdinalIgnoreCase)
                        || val.Equals("255.255.255.0", StringComparison.OrdinalIgnoreCase)
                        || val.Equals("255.255.255.255", StringComparison.OrdinalIgnoreCase)
                        || val.Equals("0.0.0.0", StringComparison.OrdinalIgnoreCase);

                    if (isMask)
                    {
                        cell.Value = "-";
                        cell.Style.ForeColor = Color.Gray;
                        cell.Style.Font = new Font(dgvLeases.Font, FontStyle.Italic);
                    }
                    else
                    {
                        cell.Style.ForeColor = dgvLeases.ForeColor;
                        cell.Style.Font = dgvLeases.Font;
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// CellFormatting handler ersetzt Masken/Platzhalter in ServerIP-Zellen visuell mit '-'
        /// (die zugrunde liegenden Daten bleiben unverändert).
        /// </summary>
        private void DgvLeases_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            try
            {
                if (dgvLeases == null || e.ColumnIndex < 0) return;

                var col = dgvLeases.Columns[e.ColumnIndex];
                if (col == null) return;

                // Wir greifen nur in der ServerIP-Spalte ein
                if (!string.Equals(col.Name, "ServerIP", StringComparison.OrdinalIgnoreCase)) return;

                var raw = e.Value?.ToString() ?? string.Empty;
                raw = raw.Trim();

                // bekannte "Masken/Platzhalter" erkennen
                if (string.IsNullOrEmpty(raw)
                    || raw.Equals("255.0.0.0", StringComparison.OrdinalIgnoreCase)
                    || raw.Equals("255.255.0.0", StringComparison.OrdinalIgnoreCase)
                    || raw.Equals("255.255.255.0", StringComparison.OrdinalIgnoreCase)
                    || raw.Equals("255.255.255.255", StringComparison.OrdinalIgnoreCase)
                    || raw.Equals("0.0.0.0", StringComparison.OrdinalIgnoreCase))
                {
                    // Anzeige ersetzen — Daten bleiben unangetastet
                    e.Value = "-";
                    e.CellStyle.ForeColor = Color.Gray;
                    e.CellStyle.Font = new Font(dgvLeases.Font, FontStyle.Italic);
                    e.FormattingApplied = true;
                }
                else
                {
                    // Stelle sicher, dass normale Werte standardmäßig formatiert werden
                    e.CellStyle.ForeColor = dgvLeases.ForeColor;
                    e.CellStyle.Font = dgvLeases.Font;
                }
            }
            catch
            {
                // swallow: Formatting darf nicht crashen
            }
        }

        /// <summary>
        /// Debug-Fenster: zeigt Index/Name/Header/Width/PreferredWidth/AutoSizeMode für dgvLeases.
        /// </summary>
        private void ShowLeasesDebugWindow()
        {
            var dlg = new Form
            {
                Text = "Leases Columns Debug",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(900, 460),
                Font = this.Font
            };

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };

            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colIndex", HeaderText = "Index" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName", HeaderText = "Name" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colHeader", HeaderText = "HeaderText" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colWidth", HeaderText = "Width" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPreferred", HeaderText = "PreferredWidth(AllCells)" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colAuto", HeaderText = "AutoSizeMode" });

            var btnRefresh = new Button { Text = "Refresh", AutoSize = true, Padding = new Padding(6) };
            var btnRunAdjust = new Button { Text = "Run Adjust", AutoSize = true, Padding = new Padding(6) };
            var btnCopy = new Button { Text = "Copy", AutoSize = true, Padding = new Padding(6) };
            var pnl = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(6) };
            pnl.Controls.Add(btnRefresh);
            pnl.Controls.Add(btnRunAdjust);
            pnl.Controls.Add(btnCopy);

            dlg.Controls.Add(grid);
            dlg.Controls.Add(pnl);

            void FillGrid()
            {
                grid.Rows.Clear();
                if (dgvLeases == null || dgvLeases.Columns.Count == 0)
                {
                    grid.Rows.Add("no columns", "", "", "", "", "");
                    return;
                }

                for (int i = 0; i < dgvLeases.Columns.Count; i++)
                {
                    var c = dgvLeases.Columns[i];
                    int preferred = -1;
                    try { preferred = c.GetPreferredWidth(DataGridViewAutoSizeColumnMode.AllCells, true); } catch { }
                    grid.Rows.Add(i, c.Name ?? "", c.HeaderText ?? "", c.Width, preferred, c.AutoSizeMode.ToString());
                }
            }

            btnRefresh.Click += (s, e) => FillGrid();
            btnRunAdjust.Click += (s, e) =>
            {
                try
                {
                    AdjustLeasesColumnWidths();
                    FillGrid();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "AdjustLeasesColumnWidths error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            btnCopy.Click += (s, e) =>
            {
                try
                {
                    var lines = grid.Rows.Cast<DataGridViewRow>()
                        .Select(r => string.Join("\t", r.Cells.Cast<DataGridViewCell>().Select(c => c.Value?.ToString() ?? "")))
                        .Where(l => !string.IsNullOrEmpty(l));
                    var all = string.Join(Environment.NewLine, lines);
                    Clipboard.SetText(all);
                    MessageBox.Show(this, "Copied to clipboard.", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Copy failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            FillGrid();
            dlg.ShowDialog(this);
        }

        /// <summary>
        /// Führt eine Aktion mittels BeginInvoke aus, sobald ein Window-Handle vorhanden ist.
        /// (Schutz gegen disposed/disposing Controls)
        /// </summary>
        private void SafeBeginInvoke(Action action)
        {
            if (action == null) return;

            if (this.IsDisposed || this.Disposing) return;

            if (this.IsHandleCreated)
            {
                try { this.BeginInvoke(action); }
                catch { /* ignore */ }
                return;
            }

            EventHandler? handler = null;
            handler = (s, e) =>
            {
                try
                {
                    this.HandleCreated -= handler!;
                    if (this.IsDisposed || this.Disposing) return;
                    try { this.BeginInvoke(action); }
                    catch { /* ignore */ }
                }
                catch { /* ignore */ }
            };

            this.HandleCreated += handler;
        }
    }
}
