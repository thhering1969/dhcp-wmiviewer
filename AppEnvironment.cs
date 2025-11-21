// AppEnvironment.cs

using System;
using System.ServiceProcess;

namespace DhcpWmiViewer
{
    /// <summary>
    /// App-weite Umgebungsinformationen / Initialisierung (z. B. ob die App lokal auf einem DHCP-Server oder DC läuft).
    /// Aufruf: AppEnvironment.Initialize();
    /// </summary>
    public static class AppEnvironment
    {
        /// <summary>
        /// True, wenn die Anwendung lokal auf einem Host läuft, der offenbar DHCP-Server-Dienst ausführt.
        /// Wird bei Initialize() gesetzt.
        /// </summary>
        public static bool RunningOnDhcpServer { get; private set; }

        /// <summary>
        /// True, wenn die Anwendung lokal auf einem Host läuft, der offenbar Domain Controller ist.
        /// Wird bei Initialize() gesetzt.
        /// </summary>
        public static bool RunningOnDomainController { get; private set; }

        /// <summary>
        /// Führt Tests aus (best-effort). Fehler werden geschluckt.
        /// </summary>
        public static void Initialize()
        {
            try
            {
                RunningOnDhcpServer = CheckLocalDhcpServer();
            }
            catch
            {
                RunningOnDhcpServer = false;
            }

            try
            {
                RunningOnDomainController = CheckLocalDomainController();
            }
            catch
            {
                RunningOnDomainController = false;
            }
        }

        /// <summary>
        /// Falls später bei Discovery festgestellt wird, dass lokaler Host ein DHCP-Server ist,
        /// kann das hier gesetzt werden.
        /// </summary>
        public static void SetRunningOnDhcpServer(bool value) => RunningOnDhcpServer = value;

        /// <summary>
        /// Falls später bei Discovery festgestellt wird, dass lokaler Host ein Domain Controller ist,
        /// kann das hier gesetzt werden.
        /// </summary>
        public static void SetRunningOnDomainController(bool value) => RunningOnDomainController = value;

        private static bool CheckLocalDhcpServer()
        {
            try
            {
                var services = ServiceController.GetServices();
                foreach (var s in services)
                {
                    if (string.Equals(s.ServiceName, "DHCPServer", StringComparison.OrdinalIgnoreCase)
                        || (!string.IsNullOrEmpty(s.DisplayName) && s.DisplayName.IndexOf("DHCP Server", StringComparison.OrdinalIgnoreCase) >= 0)
                        || (!string.IsNullOrEmpty(s.DisplayName) && s.DisplayName.IndexOf("DHCP", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        try { return s.Status == ServiceControllerStatus.Running; } catch { continue; }
                    }
                }
            }
            catch
            {
                // ignore
            }
            return false;
        }

        private static bool CheckLocalDomainController()
        {
            try
            {
                var services = ServiceController.GetServices();
                foreach (var s in services)
                {
                    // NTDS (Active Directory Domain Services) ist der Hauptdienst für DCs
                    if (string.Equals(s.ServiceName, "NTDS", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(s.ServiceName, "ActiveDirectoryDomainServices", StringComparison.OrdinalIgnoreCase)
                        || (!string.IsNullOrEmpty(s.DisplayName) && 
                            (s.DisplayName.IndexOf("Active Directory Domain Services", StringComparison.OrdinalIgnoreCase) >= 0
                             || s.DisplayName.IndexOf("NTDS", StringComparison.OrdinalIgnoreCase) >= 0)))
                    {
                        try { return s.Status == ServiceControllerStatus.Running; } catch { continue; }
                    }
                }
            }
            catch
            {
                // ignore
            }
            return false;
        }
    }
}
