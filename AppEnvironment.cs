// AppEnvironment.cs
using System.ServiceProcess;

namespace DhcpWmiViewer
{
    /// <summary>
    /// App-weite Umgebungsinformationen, z. B. ob die App direkt auf einem DHCP-Server läuft
    /// (dann werden Befehle für den lokalen Host ohne WinRM ausgeführt).
    /// </summary>
    public static class AppEnvironment
    {
        /// <summary>True, wenn auf diesem Host der Dienst "DHCPServer" läuft.</summary>
        public static bool RunningOnDhcpServer { get; private set; }

        public static void Initialize() => RunningOnDhcpServer = IsDhcpServerServiceRunning();

        public static void SetRunningOnDhcpServer(bool value) => RunningOnDhcpServer = value;

        /// <summary>
        /// Prüft gezielt den Dienst "DHCPServer". (Nicht über den Anzeigenamen: "DHCP-Client"
        /// läuft auf jedem Windows-Rechner und würde sonst fälschlich als DHCP-Server erkannt.)
        /// </summary>
        public static bool IsDhcpServerServiceRunning()
        {
            try
            {
                using var service = new ServiceController("DHCPServer");
                return service.Status == ServiceControllerStatus.Running;
            }
            catch
            {
                // Dienst nicht installiert oder keine Berechtigung
                return false;
            }
        }
    }
}
