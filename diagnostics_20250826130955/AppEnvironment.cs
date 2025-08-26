// AppEnvironment.cs

using System;
using System.ServiceProcess;

namespace DhcpWmiViewer
{
    /// <summary>
    /// App-weite Umgebungsinformationen / Initialisierung (z. B. ob die App lokal auf einem DHCP-Server läuft).
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
        }

        /// <summary>
        /// Falls später bei Discovery festgestellt wird, dass lokaler Host ein DHCP-Server ist,
        /// kann das hier gesetzt werden.
        /// </summary>
        public static void SetRunningOnDhcpServer(bool value) => RunningOnDhcpServer = value;

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
    }
}
