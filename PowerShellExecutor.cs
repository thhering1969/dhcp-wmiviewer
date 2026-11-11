// PowerShellExecutor.cs
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Data;
using System.Management.Automation;
using System.Management.Automation.Remoting;
using System.Management.Automation.Runspaces;

namespace DhcpWmiViewer
{
    /// <summary>
    /// Führt PowerShell-Scripts auf dem DHCP-Server aus.
    /// Pro Zielserver wird ein Remote-RunspacePool (WinRM) geöffnet und wiederverwendet;
    /// läuft die App selbst auf dem DHCP-Server und ist dieser das Ziel, wird lokal ausgeführt.
    /// </summary>
    internal static class PowerShellExecutor
    {
        private sealed class RemoteConnection
        {
            public RemoteConnection(RunspacePool pool) => Pool = pool;

            public RunspacePool Pool { get; }

            public bool IsUsable
            {
                get
                {
                    try { return Pool.RunspacePoolStateInfo.State == RunspacePoolState.Opened; }
                    catch { return false; }
                }
            }
        }

        // Max. parallele Pipelines pro Server (Reservations + Leases laufen parallel)
        private const int MaxRunspacesPerServer = 3;

        // Timeout für den Verbindungsaufbau (WinRM-Default wären 3 Minuten)
        private const int OpenTimeoutMs = 30000;

        private static readonly ConcurrentDictionary<string, RemoteConnection> _remoteConnections = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, object> _hostLocks = new(StringComparer.OrdinalIgnoreCase);

        private static readonly object _localPoolLock = new();
        private static RunspacePool? _localPool;

        static PowerShellExecutor()
        {
            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                // Best effort: Sessions auf den Servern schließen, Exit aber nicht lange blockieren
                try
                {
                    var pools = _remoteConnections.Values.Select(c => c.Pool).ToArray();
                    _remoteConnections.Clear();
                    Task.Run(() => { foreach (var p in pools) DisposeQuietly(p); }).Wait(2000);
                }
                catch { }
            };
        }

        /// <summary>
        /// Verwirft alle offenen Verbindungen (z. B. nach "Clear Credentials").
        /// Das Schließen läuft im Hintergrund, damit die UI bei nicht erreichbaren Servern nicht hängt.
        /// </summary>
        public static void ResetConnections()
        {
            var removed = new List<RunspacePool>();
            foreach (var key in _remoteConnections.Keys.ToArray())
            {
                if (_remoteConnections.TryRemove(key, out var conn))
                    removed.Add(conn.Pool);
            }

            if (removed.Count > 0)
                Task.Run(() => removed.ForEach(DisposeQuietly));

            // RSAT nach "Clear Credentials" wieder probieren
            _rsatDisabled.Clear();
        }

        /// <summary>True für ".", "localhost", 127.0.0.1 und den eigenen Rechnernamen (auch als FQDN).</summary>
        public static bool IsLocalHost(string? server)
        {
            var s = server?.Trim() ?? string.Empty;
            if (s is "" or "." or "127.0.0.1" or "::1") return true;
            if (s.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
            var shortName = s.Split('.')[0];
            return !System.Net.IPAddress.TryParse(s, out _)
                && shortName.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Führt ein Script aus (lokal oder remote, siehe Klassenbeschreibung). Vorher wird das DhcpServer-Modul geladen.
        /// getCredentials wird nur aufgerufen, wenn die Verbindung mit integrierter Anmeldung scheitert.
        /// </summary>
        public static Task<Collection<PSObject>> InvokeScriptAsync(string server, string script, Func<string, PSCredential?>? getCredentials = null)
        {
            return Task.Run(() =>
            {
                var requested = string.IsNullOrWhiteSpace(server) ? "." : server.Trim();
                var targetHost = ResolveTargetHost(requested);
                var wrapperScript = "Import-Module DhcpServer -ErrorAction Stop;" + Environment.NewLine + script;

                var isLocalTarget = IsLocalHost(requested) || IsLocalHost(targetHost);

                if (isLocalTarget && AppEnvironment.RunningOnDhcpServer)
                    return RunOnPool(GetLocalPool(), wrapperScript, "Local PowerShell errors: ");

                var conn = GetOrCreateRemoteConnection(requested, targetHost, getCredentials);
                try
                {
                    return RunOnPool(conn.Pool, wrapperScript, "PowerShell remote errors: ");
                }
                catch (Exception ex) when (IsConnectionBroken(ex))
                {
                    // Verbindung abgebrochen (Idle-Timeout, Server-Neustart) -> genau einmal neu verbinden
                    Helpers.WriteDebugLog($"PowerShellExecutor: connection to {targetHost} broken, reconnecting: {ex.Message}");
                    InvalidateRemoteConnection(targetHost, conn);

                    var fresh = GetOrCreateRemoteConnection(requested, targetHost, getCredentials);
                    return RunOnPool(fresh.Pool, wrapperScript, "PowerShell remote errors: ");
                }
                catch (RuntimeException rex)
                {
                    // Terminierende Script-Fehler (z. B. -ErrorAction Stop)
                    throw new InvalidOperationException("PowerShell remote errors: " + FlattenMessages(rex), rex);
                }
            });
        }

        // ------------------------------------------------------------------
        // RSAT lokal (bevorzugt für DHCP-Befehle), WinRM als Fallback
        // ------------------------------------------------------------------

        /// <summary>Server, für die RSAT lokal nicht funktioniert hat (-> für diese Sitzung WinRM), mit Grund.</summary>
        private static readonly ConcurrentDictionary<string, string> _rsatDisabled = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Zuletzt genutzter Weg je Server ("RSAT" oder "WinRM"), z. B. für die Statusleiste.</summary>
        private static readonly ConcurrentDictionary<string, string> _lastRoute = new(StringComparer.OrdinalIgnoreCase);

        public static string? GetLastRoute(string? server)
            => _lastRoute.TryGetValue(ResolveTargetHost(server), out var r) ? r : null;

        /// <summary>
        /// Führt ein Script mit DhcpServer-Cmdlets aus: zuerst LOKAL über die RSAT-DHCP-Tools
        /// (alle *-DhcpServer*-Cmdlets bekommen -ComputerName per $PSDefaultParameterValues),
        /// bei Zugriffs-/Verbindungsproblemen oder ohne RSAT per WinRM auf dem Server.
        /// Nur für Scripts, die ausschließlich DHCP-Cmdlets nutzen (nicht Events/EventLog).
        /// </summary>
        public static async Task<Collection<PSObject>> InvokeDhcpScriptAsync(string server, string script, Func<string, PSCredential?>? getCredentials = null)
        {
            var requested = string.IsNullOrWhiteSpace(server) ? "." : server.Trim();
            var targetHost = ResolveTargetHost(requested);
            var isLocalTarget = IsLocalHost(requested) || IsLocalHost(targetHost);

            if (!isLocalTarget && DhcpManager.IsRsatDhcpInstalled() && !_rsatDisabled.ContainsKey(targetHost))
            {
                try
                {
                    // Modul-Ladefehler eindeutig markieren -> sicher als "RSAT nicht nutzbar" erkannt
                    var prefix =
                        "if (-not (Get-Module DhcpServer)) { try { Import-Module (Join-Path $env:windir 'System32\\WindowsPowerShell\\v1.0\\Modules\\DhcpServer') -SkipEditionCheck -ErrorAction Stop } catch { throw ('" + RsatModuleLoadFailed + ": ' + $_.Exception.Message) } }" + Environment.NewLine +
                        $"$PSDefaultParameterValues['*-DhcpServer*:ComputerName'] = {Helpers.Quote(targetHost)}" + Environment.NewLine;
                    var result = await Task.Run(() =>
                    {
                        try { return RunOnPool(GetLocalPool(), prefix + script, "RSAT errors: "); }
                        catch (RuntimeException rex) { throw new InvalidOperationException("RSAT errors: " + FlattenMessages(rex), rex); }
                    }).ConfigureAwait(false);
                    _lastRoute[targetHost] = "RSAT";
                    return result;
                }
                catch (Exception ex) when (IsRsatAccessProblem(ex))
                {
                    // z. B. kein DHCP-Admin mit dem Windows-Konto oder RPC blockiert -> WinRM (ggf. mit Credentials)
                    _rsatDisabled[targetHost] = ex.Message;
                    Helpers.WriteDebugLog($"PowerShellExecutor: RSAT for {targetHost} not usable, falling back to WinRM: {ex.Message}");
                }
            }

            var remote = await InvokeScriptAsync(server, script, getCredentials).ConfigureAwait(false);
            _lastRoute[targetHost] = isLocalTarget && AppEnvironment.RunningOnDhcpServer ? "lokal" : "WinRM";
            return remote;
        }

        /// <summary>
        /// true für Fehler, bei denen RSAT grundsätzlich nicht geht (Modul, Rechte, RPC/Erreichbarkeit) –
        /// nicht für fachliche Fehler wie "Reservation existiert bereits".
        /// </summary>
        private const string RsatModuleLoadFailed = "RSAT_MODULE_LOAD_FAILED";

        private static bool IsRsatAccessProblem(Exception ex)
        {
            var msg = FlattenMessages(ex);
            string[] markers =
            {
                RsatModuleLoadFailed, "Execution_Policies", "running scripts is disabled", "Ausführung von Skripts",
                "Zugriff verweigert", "Access is denied", "0x80070005", "AccessDenied",
                "RPC", "0x800706BA", "0x800706BE",
                "Fehler beim Abrufen der Version", "Failed to get version",
                "Import-Module", "not loaded", "nicht geladen", "is not recognized", "wurde nicht als Name"
            };
            return markers.Any(m => msg.Contains(m, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Führt eine Abfrage aus und liefert das Ergebnis als DataTable mit den angegebenen Spalten
        /// (Werte der gleichnamigen Properties als string). <paramref name="preferRsat"/>: siehe <see cref="InvokeDhcpScriptAsync"/>.
        /// </summary>
        public static async Task<DataTable> ExecutePowerShellQueryAsync(
            string server,
            Func<string, PSCredential?>? getCredentials,
            Action<PowerShell> buildPs,
            string[] columns,
            bool preferRsat = false)
        {
            var scriptText = BuildScriptText(buildPs);
            await ConfirmPreviewAsync(scriptText).ConfigureAwait(false);

            var results = preferRsat
                ? await InvokeDhcpScriptAsync(server, scriptText, getCredentials).ConfigureAwait(false)
                : await InvokeScriptAsync(server, scriptText, getCredentials).ConfigureAwait(false);

            var dt = DhcpManager.CreateTable(columns);

            // PSComputerName wurde früher von Invoke-Command ergänzt; bei direkter Remote-Verbindung fehlt es.
            var computerNameCol = dt.Columns.Contains("PSComputerName") ? dt.Columns["PSComputerName"] : null;
            var computerNameFallback = ResolveTargetHost(server);

            dt.BeginLoadData();
            foreach (var r in results)
            {
                var row = dt.NewRow();
                foreach (DataColumn c in dt.Columns)
                {
                    try { row[c] = r.Properties[c.ColumnName]?.Value?.ToString() ?? string.Empty; }
                    catch { row[c] = string.Empty; }
                }

                if (computerNameCol != null && string.IsNullOrEmpty(row[computerNameCol] as string))
                    row[computerNameCol] = computerNameFallback;

                dt.Rows.Add(row);
            }
            dt.EndLoadData();

            return dt;
        }

        /// <summary>Führt eine DHCP-Aktion (Add/Remove/Set) aus – RSAT lokal bevorzugt, sonst WinRM.</summary>
        public static async Task ExecutePowerShellActionAsync(string server, Func<string, PSCredential?> getCredentials, Action<PowerShell> buildAction)
        {
            var scriptText = BuildScriptText(buildAction);
            await ConfirmPreviewAsync(scriptText).ConfigureAwait(false);
            await InvokeDhcpScriptAsync(server, scriptText, getCredentials).ConfigureAwait(false);
        }

        // ------------------------------------------------------------------
        // Interna
        // ------------------------------------------------------------------

        private static string ResolveTargetHost(string? server)
        {
            var requested = string.IsNullOrWhiteSpace(server) ? "." : server.Trim();
            return requested == "." ? Environment.MachineName : requested;
        }

        private static string BuildScriptText(Action<PowerShell> build)
        {
            // PowerShell.Create() ohne Invoke öffnet keinen Runspace -> billig
            using var temp = PowerShell.Create();
            build(temp);
            return Helpers.BuildCommandsText(temp);
        }

        /// <summary>Zeigt (falls aktiviert) die Befehlsvorschau; wirft OperationCanceledException bei Abbruch.</summary>
        private static async Task ConfirmPreviewAsync(string scriptText)
        {
            if (!DhcpManager.ShowCommandPreview) return;

            bool proceed;
            try { proceed = await TerminalPreview.ShowTerminalAsync(scriptText).ConfigureAwait(false); }
            catch { proceed = false; }

            if (!proceed)
                throw new OperationCanceledException("User cancelled command preview.");
        }

        private static RunspacePool GetLocalPool()
        {
            lock (_localPoolLock)
            {
                if (_localPool != null && _localPool.RunspacePoolStateInfo.State == RunspacePoolState.Opened)
                    return _localPool;

                DisposeQuietly(_localPool);
                // Eingebettete PowerShell hat sonst "Restricted" -> .cdxml/.ps1 der Module (z. B. DhcpServer) würden
                // nicht geladen. Bypass gilt nur für die Runspaces dieser App; eine Gruppenrichtlinie hat weiter Vorrang
                // (dann greift der WinRM-Fallback).
                var iss = InitialSessionState.CreateDefault();
                iss.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Bypass;
                var pool = RunspaceFactory.CreateRunspacePool(iss);
                pool.SetMinRunspaces(1);
                pool.SetMaxRunspaces(MaxRunspacesPerServer);
                pool.Open();
                _localPool = pool;
                return pool;
            }
        }

        /// <summary>
        /// Führt das Script auf dem Pool aus und wirft bei Script-Fehlern eine InvalidOperationException.
        /// Verbindungsfehler werden unverändert weitergeworfen (damit neu verbunden werden kann).
        /// </summary>
        private static Collection<PSObject> RunOnPool(RunspacePool pool, string script, string errorPrefix)
        {
            using var ps = PowerShell.Create();
            ps.RunspacePool = pool;
            ps.AddScript(script);
            var results = ps.Invoke();

            if (ps.HadErrors)
            {
                var errs = ps.Streams.Error.Select(e => e.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                if (errs.Length > 0)
                    throw new InvalidOperationException(errorPrefix + string.Join(" | ", errs));
            }

            return results ?? new Collection<PSObject>();
        }

        /// <summary>
        /// Liefert eine offene Verbindung zum Zielhost. Erst integrierte Anmeldung,
        /// bei Auth-/WinRM-Fehlern einmal mit Credentials aus getCredentials.
        /// </summary>
        private static RemoteConnection GetOrCreateRemoteConnection(string server, string targetHost, Func<string, PSCredential?>? getCredentials)
        {
            if (_remoteConnections.TryGetValue(targetHost, out var existing) && existing.IsUsable)
                return existing;

            lock (_hostLocks.GetOrAdd(targetHost, _ => new object()))
            {
                // Ein paralleler Aufruf hat die Verbindung evtl. inzwischen aufgebaut
                if (_remoteConnections.TryGetValue(targetHost, out existing))
                {
                    if (existing.IsUsable) return existing;
                    InvalidateRemoteConnection(targetHost, existing);
                }

                string firstError;
                try
                {
                    return Connect(targetHost, null);
                }
                catch (Exception exFirst)
                {
                    firstError = FlattenMessages(exFirst);
                    if (exFirst is not PSRemotingTransportException && !LooksLikeAuthOrTransport(firstError))
                        throw new InvalidOperationException(firstError, exFirst);
                }

                if (getCredentials == null)
                {
                    throw new InvalidOperationException(
                        $"Remote connection failed (auth/transport) and no credentials are available. Server='{server}', TargetHost='{targetHost}'. Details: {firstError}");
                }

                PSCredential? cred;
                try { cred = getCredentials(targetHost); }
                catch (Exception credEx) { throw new InvalidOperationException("Requesting credentials failed: " + credEx.Message, credEx); }

                if (cred == null)
                    throw new InvalidOperationException($"Remote connection to '{targetHost}' failed and no credentials were provided. Details: {firstError}");

                try
                {
                    return Connect(targetHost, cred);
                }
                catch (Exception exRetry)
                {
                    throw new InvalidOperationException(
                        "Remote connection failed after credential retry: " + firstError + " | " + FlattenMessages(exRetry), exRetry);
                }
            }
        }

        private static RemoteConnection Connect(string targetHost, PSCredential? credential)
        {
            var connectionInfo = new WSManConnectionInfo { ComputerName = targetHost, OpenTimeout = OpenTimeoutMs };
            if (credential != null)
                connectionInfo.Credential = credential;

            var pool = RunspaceFactory.CreateRunspacePool(1, MaxRunspacesPerServer, connectionInfo);
            try
            {
                pool.Open();
            }
            catch
            {
                DisposeQuietly(pool);
                throw;
            }

            var conn = new RemoteConnection(pool);
            _remoteConnections[targetHost] = conn;
            return conn;
        }

        private static void InvalidateRemoteConnection(string targetHost, RemoteConnection conn)
        {
            // Nur entfernen, wenn noch dieselbe Verbindung eingetragen ist
            if (_remoteConnections.TryGetValue(targetHost, out var current) && ReferenceEquals(current, conn))
                _remoteConnections.TryRemove(targetHost, out _);
            DisposeQuietly(conn.Pool);
        }

        private static bool IsConnectionBroken(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                if (e is PSRemotingTransportException or PSRemotingDataStructureException
                      or InvalidRunspacePoolStateException or InvalidRunspaceStateException)
                    return true;
            }
            return false;
        }

        private static bool LooksLikeAuthOrTransport(string message)
        {
            string[] markers =
            {
                "winrm", "transport", "cannot connect", "access denied", "access is denied",
                "zugriff verweigert", "authentication", "authentifizierung", "unauthorized"
            };
            return markers.Any(m => message.Contains(m, StringComparison.OrdinalIgnoreCase));
        }

        private static string FlattenMessages(Exception ex)
        {
            var msgs = new List<string>();
            for (var e = ex; e != null; e = e.InnerException)
            {
                if (!string.IsNullOrWhiteSpace(e.Message) && !msgs.Contains(e.Message))
                    msgs.Add(e.Message);
            }
            return string.Join(" | ", msgs);
        }

        private static void DisposeQuietly(RunspacePool? pool)
        {
            if (pool == null) return;
            try { pool.Dispose(); } catch { }
        }
    }
}
