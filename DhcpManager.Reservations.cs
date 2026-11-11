// DhcpManager.Reservations.cs
using System.Data;
using System.Management.Automation;

namespace DhcpWmiViewer
{
    public static partial class DhcpManager
    {
        /// <summary>Legt eine Reservation an (z. B. aus einer Lease).</summary>
        public static async Task CreateReservationAsync(
            string server, string scopeId, string ipAddress, string clientId, string name, string description,
            Func<string, PSCredential?> getCredentials)
        {
            Helpers.WriteDebugLog($"CreateReservation: Server={server}, ScopeId={scopeId}, IP={ipAddress}, ClientId={clientId}, Name={name}");

            if (await ReservationExistsAsync(server, scopeId, ipAddress, getCredentials).ConfigureAwait(false))
                throw new InvalidOperationException(Loc.T($"Die IP-Adresse {ipAddress} ist bereits reserviert.", $"IP address {ipAddress} is already reserved."));

            await AddReservationAsync(server, scopeId, ipAddress, clientId, name, description, getCredentials).ConfigureAwait(false);
        }

        /// <summary>
        /// Ändert die IP einer Reservation: neue Reservation anlegen, alte entfernen.
        /// Schlägt das Entfernen fehl, wird zurückgerollt (neue entfernen, alte wiederherstellen).
        /// </summary>
        public static async Task ChangeReservationIpAsync(
            string server, string scopeId, string oldIp, string newIp, string clientId, string name, string description,
            Func<string, PSCredential?> getCredentials)
        {
            Helpers.WriteDebugLog($"ChangeReservationIp: Server={server}, ScopeId={scopeId}, {oldIp} -> {newIp}, ClientId={clientId}");

            if (await ReservationExistsAsync(server, scopeId, newIp, getCredentials).ConfigureAwait(false))
                throw new InvalidOperationException(Loc.T($"Die IP-Adresse {newIp} ist bereits reserviert.", $"IP address {newIp} is already reserved."));

            await AddReservationAsync(server, scopeId, newIp, clientId, name, description, getCredentials).ConfigureAwait(false);

            try
            {
                // Alte Reservation gezielt über die IP entfernen (eindeutig; über die ClientId
                // könnte auch die gerade angelegte neue Reservation getroffen werden).
                await RemoveReservationAsync(server, oldIp, getCredentials).ConfigureAwait(false);

                if (await ReservationExistsAsync(server, scopeId, oldIp, getCredentials).ConfigureAwait(false))
                    throw new InvalidOperationException("Removing old reservation appears to have failed (old IP still present).");
            }
            catch (Exception removeEx)
            {
                Helpers.WriteDebugLog("Removing old reservation failed, rolling back: " + removeEx);

                var rollbackErrors = new List<string>();
                try { await RemoveReservationAsync(server, newIp, getCredentials).ConfigureAwait(false); }
                catch (Exception ex) { rollbackErrors.Add("Fail removing new reservation: " + ex.Message); }

                try { await RunAddAsync(server, scopeId, oldIp, clientId, name, description, getCredentials).ConfigureAwait(false); }
                catch (Exception ex) { rollbackErrors.Add("Fail re-adding old reservation: " + ex.Message); }

                if (rollbackErrors.Count > 0)
                    throw new InvalidOperationException("Remove old reservation failed and rollback had errors: " + string.Join(" | ", rollbackErrors), removeEx);

                throw new InvalidOperationException("Remove old reservation failed but rollback succeeded (old reservation restored). Original error: " + removeEx.Message, removeEx);
            }
        }

        /// <summary>Ändert Name/Beschreibung/ClientId einer Reservation (leere Werte bleiben unverändert).</summary>
        public static async Task UpdateReservationAsync(
            string server, string scopeId, string ipAddress, string clientId, string name, string description,
            Func<string, PSCredential?> getCredentials)
        {
            Helpers.WriteDebugLog($"UpdateReservation: Server={server}, ScopeId={scopeId}, IP={ipAddress}, ClientId={clientId}, Name={name}");

            await PowerShellExecutor.ExecutePowerShellActionAsync(server, getCredentials, ps =>
            {
                ps.AddCommand("Set-DhcpServerv4Reservation")
                  .SafeAddParameter("IPAddress", ipAddress)
                  .SafeAddParameter("Name", name)
                  .SafeAddParameter("Description", description)
                  .SafeAddParameter("ClientId", clientId)
                  .SafeAddParameter("ErrorAction", "Stop");
            }).ConfigureAwait(false);

            if (!await ReservationExistsAsync(server, scopeId, ipAddress, getCredentials).ConfigureAwait(false))
                throw new InvalidOperationException("Reservation not found after Set-DhcpServerv4Reservation.");
        }

        /// <summary>Löscht eine Reservation (über die IP-Adresse).</summary>
        public static async Task DeleteReservationAsync(
            string server, string scopeId, string ipAddress,
            Func<string, PSCredential?> getCredentials)
        {
            Helpers.WriteDebugLog($"DeleteReservation: Server={server}, ScopeId={scopeId}, IP={ipAddress}");

            await RemoveReservationAsync(server, ipAddress, getCredentials).ConfigureAwait(false);

            if (await ReservationExistsAsync(server, scopeId, ipAddress, getCredentials).ConfigureAwait(false))
                throw new InvalidOperationException("Removing reservation appears to have failed (IP still present).");
        }

        // ------------------------------------------------------------------
        // Interna
        // ------------------------------------------------------------------

        private static async Task<bool> ReservationExistsAsync(string server, string scopeId, string ipAddress, Func<string, PSCredential?> getCredentials)
        {
            var reservations = await QueryReservationsAsync(server, scopeId, getCredentials).ConfigureAwait(false);
            return reservations.Rows.Cast<DataRow>()
                .Any(r => string.Equals(r["IPAddress"]?.ToString(), ipAddress, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Legt die Reservation an und probiert dabei mehrere Schreibweisen der ClientId
        /// (z. B. "aa-bb-cc-dd-ee-ff", "01-aa-...", ohne Trennzeichen), bis eine akzeptiert wird.
        /// </summary>
        private static async Task AddReservationAsync(
            string server, string scopeId, string ipAddress, string clientId, string name, string description,
            Func<string, PSCredential?> getCredentials)
        {
            var errors = new List<string>();
            foreach (var candidate in BuildClientIdCandidates(clientId))
            {
                try
                {
                    await RunAddAsync(server, scopeId, ipAddress, candidate, name, description, getCredentials).ConfigureAwait(false);

                    if (await ReservationExistsAsync(server, scopeId, ipAddress, getCredentials).ConfigureAwait(false))
                    {
                        Helpers.WriteDebugLog($"Add-DhcpServerv4Reservation succeeded with ClientId='{candidate}'");
                        return;
                    }
                    errors.Add($"ClientId='{candidate}': Add did not create a reservation (no error text).");
                }
                catch (Exception ex)
                {
                    errors.Add($"ClientId='{candidate}': {ex.Message}");
                    Helpers.WriteDebugLog($"Add-DhcpServerv4Reservation failed with ClientId='{candidate}': {ex}");
                }
            }

            throw new InvalidOperationException("Failed to add reservation for " + ipAddress + ". Attempts:\n" + string.Join("\n", errors));
        }

        private static Task RunAddAsync(
            string server, string scopeId, string ipAddress, string clientId, string name, string description,
            Func<string, PSCredential?> getCredentials)
        {
            return PowerShellExecutor.ExecutePowerShellActionAsync(server, getCredentials, ps =>
            {
                ps.AddCommand("Add-DhcpServerv4Reservation")
                  .SafeAddParameter("ScopeId", scopeId)
                  .SafeAddParameter("IPAddress", ipAddress)
                  .SafeAddParameter("ClientId", clientId)
                  .SafeAddParameter("Name", name)
                  .SafeAddParameter("Description", description)
                  .SafeAddParameter("ErrorAction", "Stop");
            });
        }

        private static Task RemoveReservationAsync(string server, string ipAddress, Func<string, PSCredential?> getCredentials)
        {
            return PowerShellExecutor.ExecutePowerShellActionAsync(server, getCredentials, ps =>
            {
                ps.AddCommand("Remove-DhcpServerv4Reservation")
                  .SafeAddParameter("IPAddress", ipAddress)
                  .SafeAddParameter("Confirm", new SwitchParameter(false))
                  .SafeAddParameter("ErrorAction", "Stop");
            });
        }

        /// <summary>Mögliche Schreibweisen einer ClientId (MAC), in der Reihenfolge, in der sie probiert werden.</summary>
        private static List<string> BuildClientIdCandidates(string clientIdRaw)
        {
            var raw = clientIdRaw?.Trim() ?? string.Empty;
            var noSeparators = raw.Replace(":", "").Replace("-", "");
            var candidates = new List<string>();

            // 12 Hex-Zeichen -> als "AA-BB-CC-DD-EE-FF" formatieren
            if (noSeparators.Length == 12 && noSeparators.All(Uri.IsHexDigit))
            {
                var upper = noSeparators.ToUpperInvariant();
                var formatted = string.Join("-", Enumerable.Range(0, 6).Select(i => upper.Substring(i * 2, 2)));
                candidates.Add(formatted);
                candidates.Add("01-" + formatted);
            }
            else if (raw.Length > 0)
            {
                candidates.Add(raw);
                if (!raw.StartsWith("01-", StringComparison.OrdinalIgnoreCase))
                    candidates.Add("01-" + raw);
            }

            candidates.Add(raw);
            candidates.Add(noSeparators);
            candidates.Add(raw.ToLowerInvariant());
            candidates.Add(raw.ToUpperInvariant());

            var result = candidates.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
            if (result.Count == 0) result.Add(string.Empty); // ohne ClientId versuchen
            return result;
        }
    }
}
