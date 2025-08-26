// ConvertLeaseToReservationDialog.cs

using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    public partial class ConvertLeaseToReservationDialog : Form
    {
        private readonly string _scopeId;
        private readonly string _originalIp;
        private readonly string _startRange;
        private readonly string _endRange;
        private readonly string _subnetMask;

        // optionaler Lookup-Callback: gibt DataTable mit Reservations für ein Scope zurück
        private readonly Func<string, Task<DataTable>>? _reservationLookupByScope;

        // optionaler Delete-Callback: versucht Reservation im Scope für IP zu löschen; returns true wenn gelöscht
        private readonly Func<string, string, Task<bool>>? _reservationDeleteByScopeAndIp;

        private CancellationTokenSource? _pingCts;

        public string IpAddress => txtIp.Text.Trim();
        public string ClientId => txtClientId.Text.Trim();
        public new string Name => txtName.Text.Trim();
        public string Description => txtDescription.Text.Trim();
        public bool ApplyIpChange => chkApplyIpChange?.Checked ?? false;

        /// <summary>
        /// Neuer Konstruktor: optional kann eine Funktion übergeben werden, die für ein Scope (scopeId)
        /// die Reservations-Tabelle (DataTable) asynchron zurückliefert.
        /// </summary>
        public ConvertLeaseToReservationDialog(
            string scopeId,
            string ipAddress,
            string clientId,
            string hostName,
            string startRange,
            string endRange,
            string subnetMask,
            Func<string, Task<DataTable>>? reservationLookupByScope = null,
            Func<string, string, Task<bool>>? reservationDeleteByScopeAndIp = null)
        {
            _scopeId = scopeId ?? string.Empty;
            _originalIp = ipAddress ?? string.Empty;
            _startRange = startRange ?? string.Empty;
            _endRange = endRange ?? string.Empty;
            _subnetMask = subnetMask ?? string.Empty;

            _reservationLookupByScope = reservationLookupByScope;
            _reservationDeleteByScopeAndIp = reservationDeleteByScopeAndIp;

            InitializeComponent();

            // Programmgesteuertes Hinzufügen des "Auswählen..."-Buttons rechts von IP-Feld
            try
            {
                var parent = txtIp?.Parent as TableLayoutPanel;
                if (parent != null)
                {
                    parent.ColumnCount = Math.Max(parent.ColumnCount, 4);
                    while (parent.ColumnStyles.Count < 4) parent.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

                    // btnPickAvailable ist als Feld in der Designer-Partial deklariert, wird hier instanziert
                    btnPickAvailable = new Button { Text = "...", AutoSize = true, Margin = new Padding(4, 6, 0, 0), Padding = new Padding(6) };
                    btnPickAvailable.Click += async (s, e) => await BtnPickAvailable_ClickAsync();

                    // Füge in Spalte 3 (Index-basiert) ein
                    parent.Controls.Add(btnPickAvailable, 3, 0);
                }
            }
            catch
            {
                // UI-Erweiterung darf nicht fehlschlagen — ignoriere Fehler
            }

            // Hook events (keine direkten Referenzen im Designer nötig)
            txtIp.TextChanged += TxtIp_TextChanged;
            chkApplyIpChange.CheckedChanged += ChkApplyIpChange_CheckedChanged;
            btnOk.Click += BtnOk_Click;
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            // Prefill fields
            txtIp.Text = ipAddress ?? string.Empty;
            txtClientId.Text = clientId ?? string.Empty;
            txtName.Text = hostName ?? string.Empty;
            txtDescription.Text = string.Empty;

            chkApplyIpChange.Visible = false;
            chkApplyIpChange.Checked = false;
            lblApplyInfo.Visible = false;

            UpdateOkButtonState();
        }

        private void TxtIp_TextChanged(object? sender, EventArgs e)
        {
            try
            {
                var current = txtIp.Text.Trim();
                var changed = !string.Equals(current, _originalIp, StringComparison.OrdinalIgnoreCase);
                if (changed)
                {
                    chkApplyIpChange.Visible = true;
                    lblApplyInfo.Visible = true;
                    lblApplyInfo.ForeColor = Color.Black;
                    lblApplyInfo.Text = "IP übernehmen bestätigen";
                    lblApplyInfo.Tag = null;
                }
                else
                {
                    chkApplyIpChange.Visible = false;
                    chkApplyIpChange.Checked = false;
                    lblApplyInfo.Visible = false;
                    lblApplyInfo.Tag = null;
                }

                // Abbrechen laufender Prüfungen
                CancelPendingPing();
                UpdateOkButtonState();

                // Wenn Checkbox sichtbar + angehakt, erst Reservation prüfen, dann ping
                if (chkApplyIpChange.Visible && chkApplyIpChange.Checked)
                {
                    _ = CheckReservationThenPingAsync(txtIp.Text.Trim());
                }
            }
            catch { /* swallow - UI should be resilient */ }
        }

        private void ChkApplyIpChange_CheckedChanged(object? sender, EventArgs e)
        {
            if (chkApplyIpChange.Checked)
            {
                _ = CheckReservationThenPingAsync(txtIp.Text.Trim());
            }
            else
            {
                CancelPendingPing();
                if (lblApplyInfo != null)
                {
                    lblApplyInfo.Text = "IP-Übernahme bestätigen";
                    lblApplyInfo.ForeColor = Color.Gray;
                    lblApplyInfo.Tag = null;
                }
                UpdateOkButtonState();
            }
        }

        private void CancelPendingPing()
        {
            try
            {
                if (_pingCts != null && !_pingCts.IsCancellationRequested)
                {
                    _pingCts.Cancel();
                    _pingCts.Dispose();
                }
            }
            catch { }
            finally { _pingCts = null; }
        }

        /// <summary>
        /// Prüft (asynchron) per optionaler Lookup-Funktion, ob bereits eine Reservation existiert.
        /// Fragt ggf. zum Löschen; fährt anschließend mit Ping-Check fort.
        /// </summary>
        private async Task CheckReservationThenPingAsync(string ip)
        {
            CancelPendingPing();
            _pingCts = new CancellationTokenSource();
            var ct = _pingCts.Token;

            if (!IPAddress.TryParse(ip, out _))
            {
                if (this.IsHandleCreated) this.BeginInvoke(new Action(() => { SetStatusLabel("Ungültige IP", Color.OrangeRed, "invalid"); UpdateOkButtonState(); }));
                return;
            }

            // 1) Reservation check (best-effort) wenn lookup callback vorhanden
            if (_reservationLookupByScope != null)
            {
                try
                {
                    if (this.IsHandleCreated) this.BeginInvoke(new Action(() => { SetStatusLabel("Suche Reservation...", Color.Gray, "checking"); UpdateOkButtonState(); }));

                    var dt = await _reservationLookupByScope.Invoke(_scopeId).ConfigureAwait(false);

                    bool found = false;
                    string? foundName = null;
                    if (dt != null)
                    {
                        foreach (DataRow r in dt.Rows)
                        {
                            try
                            {
                                var ipVal = (r.Table.Columns.Contains("IPAddress") ? r["IPAddress"]?.ToString() : null) ?? string.Empty;
                                if (string.Equals(ipVal?.Trim(), ip, StringComparison.OrdinalIgnoreCase))
                                {
                                    found = true;
                                    var rawName = (r.Table.Columns.Contains("Name") ? r["Name"]?.ToString() : null)
                                                  ?? (r.Table.Columns.Contains("Description") ? r["Description"]?.ToString() : null);
                                    if (!string.IsNullOrWhiteSpace(rawName))
                                    {
                                        var idx = rawName.IndexOf('.');
                                        foundName = idx > 0 ? rawName.Substring(0, idx) : rawName;
                                    }
                                    break;
                                }
                            }
                            catch { }
                        }
                    }

                    if (this.IsHandleCreated && !ct.IsCancellationRequested)
                    {
                        if (found)
                        {
                            bool userWantsDelete = false;
                            this.Invoke(new Action(() =>
                            {
                                var shown = string.IsNullOrWhiteSpace(foundName) ? "unbekannt" : foundName;
                                SetStatusLabel($"Reservation vorhanden: {shown}", Color.OrangeRed, "reserved");
                                UpdateOkButtonState();

                                var dlgRes = MessageBox.Show(this,
                                    $"Für IP {ip} existiert bereits eine Reservation (Name: {shown}).\nMöchten Sie diese Reservation löschen, damit Sie die IP übernehmen können?",
                                    "Reservation vorhanden",
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Question);

                                userWantsDelete = (dlgRes == DialogResult.Yes);
                            }));

                            if (userWantsDelete && _reservationDeleteByScopeAndIp != null)
                            {
                                bool deleted = false;
                                try
                                {
                                    deleted = await _reservationDeleteByScopeAndIp.Invoke(_scopeId, ip).ConfigureAwait(false);
                                }
                                catch { deleted = false; }

                                if (this.IsHandleCreated && !ct.IsCancellationRequested)
                                {
                                    this.BeginInvoke(new Action(() =>
                                    {
                                        if (deleted)
                                        {
                                            SetStatusLabel("Reservation gelöscht", Color.Green, "deleted");
                                            UpdateOkButtonState();
                                            _ = StartPingCheckAsync(ip);
                                        }
                                        else
                                        {
                                            SetStatusLabel("Löschen fehlgeschlagen / Reservation bleibt", Color.OrangeRed, "reserved");
                                            UpdateOkButtonState();
                                        }
                                    }));
                                }
                            }
                            else
                            {
                                // user didn't want to delete -> remain reserved
                            }
                        }
                        else
                        {
                            this.BeginInvoke(new Action(() =>
                            {
                                SetStatusLabel("Keine Reservation gefunden, pinge...", Color.Gray, "checking");
                                UpdateOkButtonState();
                                _ = StartPingCheckAsync(ip);
                            }));
                        }
                    }

                    return;
                }
                catch (Exception)
                {
                    if (this.IsHandleCreated && !ct.IsCancellationRequested)
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            SetStatusLabel("Reservation-Prüfung fehlgeschlagen", Color.OrangeRed, "error");
                            UpdateOkButtonState();
                            _ = StartPingCheckAsync(ip);
                        }));
                    }
                    return;
                }
            }

            // Wenn kein reservationLookup übergeben wurde -> nur ping ausführen
            _ = StartPingCheckAsync(ip);
        }

        /// <summary>
        /// Ping-Check wie zuvor, aktualisiert lblApplyInfo mit Ergebnis.
        /// </summary>
        private async Task StartPingCheckAsync(string ip)
        {
            CancelPendingPing();
            _pingCts = new CancellationTokenSource();
            var ct = _pingCts.Token;

            if (!IPAddress.TryParse(ip, out _))
            {
                SetStatusLabel("Ungültige IP", Color.OrangeRed, "invalid");
                UpdateOkButtonState();
                return;
            }

            try
            {
                SetStatusLabel("Prüfe (Ping)...", Color.Gray, "checking");
                UpdateOkButtonState();

                bool replied = await PingHelper.PingOnceAsync(ip, 700, ct).ConfigureAwait(false);

                if (this.IsHandleCreated && !ct.IsCancellationRequested)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        if (replied)
                            SetStatusLabel("IP antwortet — möglicherweise belegt", Color.Red, "occupied");
                        else
                            SetStatusLabel("IP scheint frei", Color.Green, "free");

                        UpdateOkButtonState();
                    }));
                }
            }
            catch
            {
                if (this.IsHandleCreated)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        SetStatusLabel("Prüfung fehlgeschlagen", Color.OrangeRed, "error");
                        UpdateOkButtonState();
                    }));
                }
            }
        }

        private void SetStatusLabel(string text, Color color, string tag)
        {
            if (lblApplyInfo == null) return;
            lblApplyInfo.Text = text;
            lblApplyInfo.ForeColor = color;
            lblApplyInfo.Tag = tag;
            lblApplyInfo.Visible = true;
        }

        /// <summary>
        /// Programmgesteuerter Handler für den "..."-Button: lädt Reservations (falls Callback vorhanden),
        /// erzeugt IP-Kandidaten aus _startRange.._endRange, filtert reservierte IPs und zeigt AvailableIpsDialog.
        /// </summary>
        private async Task BtnPickAvailable_ClickAsync()
        {
            try
            {
                // 1) Falls kein Range vorhanden, abbrechen
                if (string.IsNullOrWhiteSpace(_startRange) || string.IsNullOrWhiteSpace(_endRange))
                {
                    MessageBox.Show(this, "Kein gültiger Bereich (Start/End) verfügbar.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // 2) Reservierte IPs sammeln (best-effort) via Callback
                var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (_reservationLookupByScope != null)
                {
                    try
                    {
                        var dt = await _reservationLookupByScope.Invoke(_scopeId).ConfigureAwait(false);
                        if (dt != null)
                        {
                            foreach (DataRow r in dt.Rows)
                            {
                                try
                                {
                                    var ipVal = (r.Table.Columns.Contains("IPAddress") ? r["IPAddress"]?.ToString() : null) ?? string.Empty;
                                    if (!string.IsNullOrWhiteSpace(ipVal)) reserved.Add(ipVal.Trim());
                                }
                                catch { /* ignore problematic rows */ }
                            }
                        }
                    }
                    catch
                    {
                        // Lookup failed - continue without reserved data
                    }
                }

                // 3) Kandidaten generieren und reservierte entfernen
                // Erzeuge Roh-Kandidaten
                var rawCandidates = NetworkHelper.GetIpRange(_startRange, _endRange).ToList();

                // Filtere reservierte IPs
                var candidates = rawCandidates.Where(ip => !reserved.Contains(ip)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                // Defensive: stelle sicher, dass alle Kandidaten wirklich im numerischen Bereich [_startRange, _endRange] liegen.
                // (Schützt gegen fehlerhafte Kurzschreibweisen oder falsche Inputs)
                if (NetworkHelper.TryIpToUInt32(_startRange, out var startU) && NetworkHelper.TryIpToUInt32(_endRange, out var endU))
                {
                    if (startU > endU) { var t = startU; startU = endU; endU = t; }
                    candidates = candidates
                        .Where(ip => NetworkHelper.TryIpToUInt32(ip, out var u) && u >= startU && u <= endU)
                        .ToList();
                }

                // Sortiere numerisch
                candidates = candidates.OrderBy(ip => NetworkHelper.IpToUInt32(ip)).ToList();

                if (candidates.Count == 0)
                {
                    MessageBox.Show(this, "Keine freie IP im Bereich gefunden.", "Keine IPs", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // reserved an Dialog übergeben, damit dieser reservierte IPs ausblendet (zusätzliche Sicherheit)
                using var dlg = new AvailableIpsDialog(candidates, reserved);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    txtIp.Text = dlg.SelectedIp;
                    // trigger change logic (falls nötig)
                    TxtIp_TextChanged(null, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Fehler beim Laden verfügbarer IPs: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnOk_Click(object? sender, EventArgs e)
        {
            // NOTE: IP-Validierung erfolgt erst nach der Spezialprüfung, weil wir die IP ggf. von einer
            // vorhandenen Reservation übernehmen und das Feld vorab ungültig sein könnte.
            string hostName = txtName.Text?.Trim() ?? string.Empty;
            string clientId = txtClientId.Text?.Trim() ?? string.Empty;
            string newIp = txtIp.Text.Trim();

            // === SPEZIALPRÜFUNG für Hostnames wie PC25xxx... oder LAP25xxx... ===
            if (!string.IsNullOrEmpty(hostName) &&
                (hostName.StartsWith("PC25", StringComparison.OrdinalIgnoreCase) ||
                 hostName.StartsWith("LAP25", StringComparison.OrdinalIgnoreCase)))
            {
                // Teil ohne Präfix und Domain extrahieren
                var withoutPrefix = hostName.Substring(4); // "PC25"/"LAP25" -> 4 Zeichen
                var idx = withoutPrefix.IndexOf('.');
                if (idx > 0)
                {
                    var keyPart = withoutPrefix.Substring(0, idx); // z.B. "Infrastruktur"
                    var searchPc = "PC" + keyPart;
                    var searchLap = "LAP" + keyPart;

                    if (_reservationLookupByScope != null)
                    {
                        try
                        {
                            // best-effort lookup
                            var dt = await _reservationLookupByScope.Invoke(_scopeId).ConfigureAwait(false);

                            string? existingName = null;
                            string? existingIp = null;

                            if (dt != null)
                            {
                                foreach (DataRow r in dt.Rows)
                                {
                                    try
                                    {
                                        var rawName = (r.Table.Columns.Contains("Name") ? r["Name"]?.ToString() : null)
                                                      ?? (r.Table.Columns.Contains("Description") ? r["Description"]?.ToString() : null);

                                        if (string.IsNullOrWhiteSpace(rawName)) continue;

                                        // strip domain part if present
                                        var dotIdx = rawName.IndexOf('.');
                                        var shortName = dotIdx > 0 ? rawName.Substring(0, dotIdx) : rawName;

                                        if (shortName.StartsWith(searchPc, StringComparison.OrdinalIgnoreCase) ||
                                            shortName.StartsWith(searchLap, StringComparison.OrdinalIgnoreCase))
                                        {
                                            existingName = shortName;
                                            existingIp = (r.Table.Columns.Contains("IPAddress") ? r["IPAddress"]?.ToString() : null) ?? string.Empty;
                                            break;
                                        }
                                    }
                                    catch { /* ignore problematic rows */ }
                                }
                            }

                            if (!string.IsNullOrEmpty(existingName) && !string.IsNullOrWhiteSpace(existingIp))
                            {
                                bool userWantsAdopt = false;
                                this.Invoke(new Action(() =>
                                {
                                    var shown = string.IsNullOrWhiteSpace(existingName) ? "unbekannt" : existingName;
                                    var res = MessageBox.Show(this,
                                        $"Es existiert bereits eine Reservierung '{shown}' mit IP {existingIp}.\n\n" +
                                        "Möchten Sie diese IP übernehmen und die alte Reservierung löschen?",
                                        "Reservierung gefunden",
                                        MessageBoxButtons.YesNo,
                                        MessageBoxIcon.Question);

                                    userWantsAdopt = (res == DialogResult.Yes);
                                }));

                                if (userWantsAdopt)
                                {
                                    if (_reservationDeleteByScopeAndIp != null)
                                    {
                                        bool deleted = false;
                                        try
                                        {
                                            deleted = await _reservationDeleteByScopeAndIp.Invoke(_scopeId, existingIp).ConfigureAwait(false);
                                        }
                                        catch { deleted = false; }

                                        if (!deleted)
                                        {
                                            // Löschen fehlgeschlagen -> informieren und abbrechen
                                            if (this.IsHandleCreated)
                                            {
                                                this.BeginInvoke(new Action(() =>
                                                {
                                                    MessageBox.Show(this, "Löschen der existierenden Reservation ist fehlgeschlagen. Vorgang abgebrochen.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                }));
                                            }
                                            return;
                                        }

                                        // Löschen erfolgreich -> IP im Dialog übernehmen und weiter mit normalem Ablauf
                                        newIp = existingIp;
                                        if (this.IsHandleCreated)
                                        {
                                            this.BeginInvoke(new Action(() =>
                                            {
                                                txtIp.Text = existingIp;
                                                SetStatusLabel("IP von existierender Reservation übernommen", Color.Green, "adopted");
                                                UpdateOkButtonState();
                                            }));
                                        }
                                    }
                                    else
                                    {
                                        // keine Lösch-Funktion verfügbar
                                        if (this.IsHandleCreated)
                                        {
                                            this.BeginInvoke(new Action(() =>
                                            {
                                                MessageBox.Show(this, "Keine Lösch-Funktion verfügbar. Die alte Reservation kann nicht entfernt werden.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                            }));
                                        }
                                        return;
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Lookup fehlgeschlagen -> ignorieren (der normale Ablauf macht später noch Prüfungen)
                        }
                    }
                }
            }

            // --- jetzt gültige IP prüfen (nach Spezialfall) ---
            if (!IPAddress.TryParse(newIp, out _))
            {
                MessageBox.Show(this, "Die eingegebene IP ist ungültig.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 1) nochmal Lookup prüfen (synchron awaited) — vorhandener Code, unverändert
            if (_reservationLookupByScope != null)
            {
                try
                {
                    SetStatusLabel("Suche Reservation...", Color.Gray, "checking");
                    UpdateOkButtonState();

                    var dt = await _reservationLookupByScope.Invoke(_scopeId).ConfigureAwait(false);

                    bool found = false;
                    string? foundName = null;
                    if (dt != null)
                    {
                        foreach (DataRow r in dt.Rows)
                        {
                            try
                            {
                                var ipVal = (r.Table.Columns.Contains("IPAddress") ? r["IPAddress"]?.ToString() : null) ?? string.Empty;
                                if (string.Equals(ipVal?.Trim(), newIp, StringComparison.OrdinalIgnoreCase))
                                {
                                    found = true;
                                    var rawName = (r.Table.Columns.Contains("Name") ? r["Name"]?.ToString() : null)
                                                  ?? (r.Table.Columns.Contains("Description") ? r["Description"]?.ToString() : null);
                                    if (!string.IsNullOrWhiteSpace(rawName))
                                    {
                                        var idx = rawName.IndexOf('.');
                                        foundName = idx > 0 ? rawName.Substring(0, idx) : rawName;
                                    }
                                    break;
                                }
                            }
                            catch { }
                        }
                    }

                    if (found)
                    {
                        var proceedDelete = false;
                        this.Invoke(new Action(() =>
                        {
                            var shown = string.IsNullOrWhiteSpace(foundName) ? "unbekannt" : foundName;
                            SetStatusLabel($"Reservation vorhanden: {shown}", Color.OrangeRed, "reserved");
                            UpdateOkButtonState();
                            var res = MessageBox.Show(this, $"Für die IP {newIp} existiert bereits eine Reservation (Name: {shown}).\nMöchten Sie sie löschen?", "Reservation vorhanden", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            proceedDelete = (res == DialogResult.Yes);
                        }));

                        if (proceedDelete)
                        {
                            if (_reservationDeleteByScopeAndIp != null)
                            {
                                bool deleted = false;
                                try
                                {
                                    deleted = await _reservationDeleteByScopeAndIp.Invoke(_scopeId, newIp).ConfigureAwait(false);
                                }
                                catch { deleted = false; }

                                if (this.IsHandleCreated)
                                {
                                    this.BeginInvoke(new Action(() =>
                                    {
                                        if (deleted)
                                        {
                                            SetStatusLabel("Reservation gelöscht", Color.Green, "deleted");
                                            UpdateOkButtonState();
                                        }
                                        else
                                        {
                                            SetStatusLabel("Löschen fehlgeschlagen", Color.OrangeRed, "reserved");
                                            UpdateOkButtonState();
                                            MessageBox.Show(this, "Löschen der Reservation ist fehlgeschlagen. Vorgang abgebrochen.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        }
                                    }));
                                }

                                if (!deleted)
                                    return; // abort
                            }
                            else
                            {
                                this.Invoke(new Action(() =>
                                {
                                    MessageBox.Show(this, "Keine Lösch-Funktion verfügbar. Die Reservation besteht weiterhin.", "Reservation vorhanden", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                }));
                                return;
                            }
                        }
                        else
                        {
                            // user doesn't want to delete -> abort
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (this.IsHandleCreated)
                    {
                        var res = DialogResult.None;
                        this.BeginInvoke(new Action(() =>
                        {
                            SetStatusLabel("Reservation-Prüfung fehlgeschlagen", Color.OrangeRed, "error");
                            UpdateOkButtonState();
                            res = MessageBox.Show(this, $"Prüfung auf bestehende Reservation ist fehlgeschlagen: {ex.Message}\nMöchten Sie trotzdem fortfahren?", "Prüfung fehlgeschlagen", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                            if (res != DialogResult.Yes) { /* continue, ping will run */ }
                        }));
                        await Task.Delay(50).ConfigureAwait(false);
                    }
                }
            }

            // 2) Ping prüfen falls aktiviert
            if (chkApplyIpChange.Checked)
            {
                try
                {
                    SetStatusLabel("Prüfe (Ping)...", Color.Gray, "checking");
                    UpdateOkButtonState();

                    bool replied = await PingHelper.PingOnceAsync(newIp, 800, CancellationToken.None).ConfigureAwait(false);

                    if (replied)
                    {
                        if (this.IsHandleCreated)
                        {
                            this.BeginInvoke(new Action(() =>
                            {
                                SetStatusLabel("IP antwortet — bereits belegt", Color.Red, "occupied");
                                UpdateOkButtonState();
                                MessageBox.Show(this, "Die gewünschte IP antwortet bereits im Netzwerk. Bitte wählen Sie eine andere IP.", "IP belegt", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }));
                        }
                        return;
                    }
                    else
                    {
                        if (this.IsHandleCreated)
                        {
                            this.BeginInvoke(new Action(() => { SetStatusLabel("IP scheint frei", Color.Green, "free"); }));
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (this.IsHandleCreated)
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            SetStatusLabel("Prüfung fehlgeschlagen", Color.OrangeRed, "error");
                            var res = MessageBox.Show(this, $"Prüfung der IP ist fehlgeschlagen: {ex.Message}\nMöchten Sie trotzdem fortfahren?", "Prüfung fehlgeschlagen", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                            if (res != DialogResult.Yes) return;
                        }));
                    }
                }
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void UpdateOkButtonState()
        {
            bool ipValid = IPAddress.TryParse(txtIp.Text.Trim(), out _);
            bool ipOccupied = lblApplyInfo != null && string.Equals(lblApplyInfo.Tag as string, "occupied", StringComparison.OrdinalIgnoreCase);
            bool ipReserved = lblApplyInfo != null && string.Equals(lblApplyInfo.Tag as string, "reserved", StringComparison.OrdinalIgnoreCase);
            btnOk.Enabled = ipValid && !ipOccupied && !ipReserved;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            CancelPendingPing();
            base.OnFormClosing(e);
        }
    }
}
