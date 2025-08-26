// MainForm.Leases.Handlers.cs
// DIESE DATEI WIRD KOMPLETT ANGEZEIGT — EINFACH KOPIEREN & EINFÜGEN

using System;
using System.Data;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// Wird aufgerufen, wenn der Benutzer "Create reservation from lease..." im Leases-Contextmenu auswählt.
        /// Öffnet ConvertLeaseToReservationDialog, führt CreateReservationFromLeaseAsync (DhcpManager) aus
        /// und aktualisiert danach ggf. UI/Bindings.
        /// </summary>
        private async Task OnCreateReservationFromLeaseAsync()
        {
            try
            {
                if (dgvLeases == null || dgvLeases.SelectedRows.Count == 0)
                {
                    MessageBox.Show(this, "Bitte zuerst eine Lease-Zeile auswählen.", "Keine Auswahl", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var leaseRow = dgvLeases.SelectedRows[0];
                var (oldIp, clientId, hostName) = ReadLeaseRowValuesSafe(leaseRow);

                if (string.IsNullOrWhiteSpace(oldIp))
                {
                    MessageBox.Show(this, "Die ausgewählte Zeile enthält keine IP-Adresse.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var scopeId = TryGetScopeIdFromSelection();
                if (string.IsNullOrWhiteSpace(scopeId))
                {
                    MessageBox.Show(this, "Bitte zuerst einen Scope oben auswählen.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string startRange = string.Empty, endRange = string.Empty, subnetMask = string.Empty;
                try
                {
                    if (dgv != null && dgv.SelectedRows.Count > 0)
                    {
                        var srow = dgv.SelectedRows[0];
                        startRange = srow.Cells["StartRange"]?.Value?.ToString() ?? string.Empty;
                        endRange = srow.Cells["EndRange"]?.Value?.ToString() ?? string.Empty;
                        subnetMask = srow.Cells["SubnetMask"]?.Value?.ToString() ?? string.Empty;
                    }
                }
                catch { /* ignore */ }

                var server = GetServerNameOrDefault();

                string prefetchedDescription = string.Empty;
                try
                {
                    var resTable = await DhcpManager.QueryReservationsAsync(server, scopeId, s => GetCredentialsForServer(s));
                    if (resTable != null)
                    {
                        foreach (DataRow dr in resTable.Rows)
                        {
                            var ipVal = dr["IPAddress"]?.ToString() ?? string.Empty;
                            if (string.Equals(ipVal, oldIp, StringComparison.OrdinalIgnoreCase))
                            {
                                prefetchedDescription = dr["Description"]?.ToString() ?? string.Empty;
                                break;
                            }
                        }
                    }
                }
                catch { /* ignore */ }

                using var dlg = new ConvertLeaseToReservationDialog(
                    scopeId,
                    oldIp,
                    clientId,
                    hostName,
                    startRange,
                    endRange,
                    subnetMask,
                    // lookup callback
                    async (sc) =>
                    {
                        try { return await DhcpManager.QueryReservationsAsync(server, sc, s => GetCredentialsForServer(s)).ConfigureAwait(false); }
                        catch { return new DataTable(); }
                    },
                    // delete callback -> delegate to MainForm's ReservationDeleteForScopeAndIpAsync
                    async (sc, ip) =>
                    {
                        try
                        {
                            var deleted = await ReservationDeleteForScopeAndIpAsync(sc, ip).ConfigureAwait(false);
                            if (deleted)
                            {
                                try
                                {
                                    this.BeginInvoke(new Action(() =>
                                    {
                                        _ = TryInvokeRefreshReservations(sc);
                                        _ = TryInvokeRefreshLeases(sc);
                                    }));
                                }
                                catch { }
                            }
                            return deleted;
                        }
                        catch { return false; }
                    }
                );

                // prefill description via reflection (best-effort)
                if (!string.IsNullOrWhiteSpace(prefetchedDescription))
                {
                    try
                    {
                        var f = dlg.GetType().GetField("txtDescription", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        if (f != null)
                        {
                            var tb = f.GetValue(dlg) as TextBox;
                            if (tb != null) tb.Text = prefetchedDescription;
                        }
                    }
                    catch { /* ignore */ }
                }

                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                var chosenIp = dlg.IpAddress?.Trim() ?? oldIp;
                var chosenClientId = dlg.ClientId?.Trim() ?? clientId;
                var chosenName = dlg.Name ?? hostName;
                var chosenDescription = dlg.Description ?? prefetchedDescription ?? string.Empty;

                var confirm = MessageBox.Show(this,
                    $"Reservation erstellen:\n\nScope: {scopeId}\nClientId: {chosenClientId}\nIP: {chosenIp}\nName: {chosenName}\n\nFortfahren?",
                    "Bestätigen",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirm != DialogResult.Yes) return;

                try
                {
                    this.Enabled = false;
                    var serverToUse = GetServerNameOrDefault();

                    // ===== Option A (direkter, typ-sicherer Aufruf ohne Reflection) =====
                    var created = false;
                    try
                    {
                        // Call the DhcpManager method directly. This will be resolved at compile time
                        // to the signature present in your DhcpManager partials. We handle both
                        // Task and Task<bool> return shapes at runtime.
                        var maybeTask = DhcpManager.CreateReservationFromLeaseAsync(
                            serverToUse,
                            scopeId,
                            chosenIp,
                            chosenClientId,
                            chosenName,
                            chosenDescription,
                            s => GetCredentialsForServer(s)
                        );

                        // If the method returns Task<bool>, the following pattern-match will assign/await it.
                        if (maybeTask is Task<bool> tb)
                        {
                            created = await tb.ConfigureAwait(false);
                        }
                        else if (maybeTask is Task t)
                        {
                            await t.ConfigureAwait(false);
                            // assume success if no explicit bool result
                            created = true;
                        }
                        else
                        {
                            // unlikely: method returned something else — treat as failure
                            created = false;
                        }
                    }
                    catch (Exception exCreate)
                    {
                        // Wenn der direkte Aufruf fehlschlägt, melde es dem Benutzer.
                        created = false;
                        MessageBox.Show(this, "Fehler beim Erstellen der Reservation: " + exCreate.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    // prüfen ob erstellt
                    if (!created)
                    {
                        MessageBox.Show(this, "Reservation konnte nicht erstellt werden (DhcpManager-Methode fehlgeschlagen).", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    MessageBox.Show(this, "Reservation erfolgreich erstellt.", "Erfolg", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    await TryInvokeRefreshReservations(scopeId);
                    await TryInvokeRefreshLeases(scopeId);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Fehler beim Erstellen der Reservation:\n" + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally { this.Enabled = true; }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Unerwarteter Fehler: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Wird aufgerufen, wenn der Benutzer "Change reservation IP..." im Leases-Contextmenu auswählt.
        /// Implementiert: Prefetch von Description (falls vorhanden), öffnet ChangeReservationDialog,
        /// und führt ChangeReservationIpAsync oder UpdateReservationPropertiesAsync aus.
        /// </summary>
        private async Task OnChangeReservationFromLeaseRowAsync()
        {
            try
            {
                if (dgvLeases == null || dgvLeases.SelectedRows.Count == 0)
                {
                    MessageBox.Show(this, "Bitte zuerst eine Lease-Zeile auswählen.", "Keine Auswahl", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var leaseRow = dgvLeases.SelectedRows[0];
                var oldIp = leaseRow.Cells["IPAddress"]?.Value?.ToString() ?? string.Empty;
                var clientId = leaseRow.Cells["ClientId"]?.Value?.ToString() ?? string.Empty;
                var hostName = leaseRow.Cells["HostName"]?.Value?.ToString() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(oldIp))
                {
                    MessageBox.Show(this, "Die ausgewählte Zeile enthält keine IP-Adresse.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Scope ermitteln
                var scopeId = TryGetScopeIdFromSelection();
                if (string.IsNullOrWhiteSpace(scopeId))
                {
                    MessageBox.Show(this, "Bitte zuerst einen Scope oben auswählen.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string startRange = string.Empty, endRange = string.Empty, subnetMask = string.Empty;
                try
                {
                    if (dgv != null && dgv.SelectedRows.Count > 0)
                    {
                        var srow = dgv.SelectedRows[0];
                        startRange = srow.Cells["StartRange"]?.Value?.ToString() ?? string.Empty;
                        endRange = srow.Cells["EndRange"]?.Value?.ToString() ?? string.Empty;
                        subnetMask = srow.Cells["SubnetMask"]?.Value?.ToString() ?? string.Empty;
                    }
                }
                catch { /* ignore */ }

                var server = GetServerNameOrDefault();

                // Prefetch description from reservations (if any) - best-effort
                string prefetchedDescription = string.Empty;
                try
                {
                    var resTable = await DhcpManager.QueryReservationsAsync(server, scopeId, s => GetCredentialsForServer(s)!);
                    if (resTable != null)
                    {
                        foreach (DataRow dr in resTable.Rows)
                        {
                            var ipVal = dr["IPAddress"]?.ToString() ?? string.Empty;
                            if (string.Equals(ipVal, oldIp, StringComparison.OrdinalIgnoreCase))
                            {
                                prefetchedDescription = dr["Description"]?.ToString() ?? string.Empty;
                                break;
                            }
                        }
                    }
                }
                catch
                {
                    // ignore - best-effort
                }

                using var dlg = new ChangeReservationDialog(oldIp, hostName, prefetchedDescription, startRange, endRange, subnetMask);

                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                var newIp = dlg.NewIp?.Trim() ?? string.Empty;
                var newDescription = dlg.NewDescription?.Trim() ?? string.Empty;
                var newName = dlg.NewName ?? hostName;

                if (dlg.IpChanged)
                {
                    if (!IPAddress.TryParse(newIp, out _))
                    {
                        MessageBox.Show(this, "Die eingegebene IP ist ungültig.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var confirm = MessageBox.Show(this, $"Reservation ändern:\n\nScope: {scopeId}\nClientId: {clientId}\nVon: {oldIp}\nNach: {newIp}\n\nFortfahren?", "Bestätigen", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (confirm != DialogResult.Yes) return;

                    try
                    {
                        this.Enabled = false;

                        await DhcpManager.ChangeReservationIpAsync(
                            server,
                            scopeId,
                            oldIp,
                            newIp,
                            clientId,
                            newName,
                            newDescription,
                            s => GetCredentialsForServer(s)!
                        );

                        MessageBox.Show(this, "Reservation erfolgreich geändert.", "Erfolg", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        // LOG: IP-Änderung protokollieren (zentral)
                        try
                        {
                            await LogGuiEventAsync(
                                "ChangeReservationIp",
                                scopeId,
                                newIp,
                                $"OldIp={oldIp};ClientId={clientId};OldName={hostName};NewName={newName};NewDesc={newDescription}");
                        }
                        catch { /* swallow logging errors */ }

                        await TryInvokeRefreshReservations(scopeId);
                        await TryInvokeRefreshLeases(scopeId);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "Fehler beim Ändern der Reservation:\n" + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    finally
                    {
                        this.Enabled = true;
                    }
                }
                else
                {
                    // Nur Description/Name aktualisieren -> UpdateReservationPropertiesAsync verwenden
                    try
                    {
                        this.Enabled = false;

                        await DhcpManager.UpdateReservationPropertiesAsync(
                            server,
                            scopeId,
                            oldIp,
                            clientId,
                            newName,
                            newDescription,
                            s => GetCredentialsForServer(s)!
                        );

                        MessageBox.Show(this, "Reservation-Eigenschaften erfolgreich aktualisiert.", "Erfolg", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        // LOG: Properties-Update protokollieren (zentral)
                        try
                        {
                            await LogGuiEventAsync(
                                "UpdateReservationProperties",
                                scopeId,
                                oldIp,
                                $"ClientId={clientId};OldName={hostName};NewName={newName};OldDesc={prefetchedDescription};NewDesc={newDescription}");
                        }
                        catch { /* swallow */ }

                        await TryInvokeRefreshReservations(scopeId);
                        await TryInvokeRefreshLeases(scopeId);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "Fehler beim Aktualisieren der Reservationseigenschaften:\n" + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    finally
                    {
                        this.Enabled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Unerwarteter Fehler: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
