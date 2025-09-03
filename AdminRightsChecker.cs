// AdminRightsChecker.cs
using System;
using System.Linq;
using System.Security.Principal;
using System.Windows.Forms;

namespace DhcpWmiViewer
{
    /// <summary>
    /// Überprüft, ob die Anwendung mit Administratorrechten gestartet wurde
    /// </summary>
    internal static class AdminRightsChecker
    {
        private static bool _hasShownAdminWarning = false;
        
        /// <summary>
        /// Prüft, ob die aktuelle Anwendung mit Administratorrechten läuft
        /// </summary>
        public static bool IsRunningAsAdministrator()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Setzt den Status der Admin-Warnung zurück (für Testzwecke oder spezielle Szenarien)
        /// </summary>
        public static void ResetAdminWarningStatus()
        {
            _hasShownAdminWarning = false;
        }
        
        /// <summary>
        /// Zeigt eine Warnung an, wenn die Anwendung nicht als Administrator läuft
        /// und bietet automatischen Neustart als Administrator an
        /// </summary>
        public static void CheckAndWarnIfNotAdmin()
        {
            try
            {
                // Verhindere mehrfache Anzeige der Admin-Warnung
                if (_hasShownAdminWarning)
                    return;
                    
                if (!IsRunningAsAdministrator() && AppConfig.ShowAdminWarning)
                {
                    _hasShownAdminWarning = true; // Markiere als angezeigt, bevor der Dialog gezeigt wird
                var message = "⚠️ WARNUNG: Administratorrechte erforderlich\n\n" +
                             "Diese Anwendung wurde nicht als Administrator gestartet.\n" +
                             "Für die DHCP-Verwaltung sind Administratorrechte erforderlich.\n\n" +
                             "Mögliche Probleme:\n" +
                             "• Keine Verbindung zu DHCP-Servern möglich\n" +
                             "• PowerShell-Befehle schlagen fehl\n" +
                             "• WMI-Zugriff wird verweigert\n\n";

                if (AppConfig.EnableAutoRestartAsAdmin)
                {
                    message += "Möchten Sie die Anwendung automatisch als Administrator neu starten?";
                    
                    var result = MessageBox.Show(
                        message,
                        "DHCP WMI Viewer - Administratorrechte erforderlich",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button1
                    );

                    switch (result)
                    {
                        case DialogResult.Yes:
                            // Automatischer Neustart als Administrator
                            if (RestartAsAdministrator())
                            {
                                Environment.Exit(0);
                            }
                            else
                            {
                                // Neustart fehlgeschlagen - zeige Fehlermeldung, aber setze Flag nicht zurück
                                // um weitere Admin-Warnungen zu verhindern
                                MessageBox.Show(
                                    "Automatischer Neustart als Administrator fehlgeschlagen.\n" +
                                    "Bitte starten Sie die Anwendung manuell als Administrator.",
                                    "Neustart fehlgeschlagen",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning
                                );
                            }
                            break;

                        case DialogResult.No:
                            // Ohne Administratorrechte fortfahren
                            break;

                        case DialogResult.Cancel:
                            // Anwendung beenden
                            Environment.Exit(0);
                            break;
                    }
                }
                else
                {
                    message += "Empfehlung:\n" +
                              "Starten Sie die Anwendung als Administrator neu.\n" +
                              "(Rechtsklick → 'Als Administrator ausführen')\n\n" +
                              "Möchten Sie trotzdem fortfahren?";

                    var result = MessageBox.Show(
                        message,
                        "DHCP WMI Viewer - Administratorrechte erforderlich",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2
                    );

                    if (result == DialogResult.No)
                    {
                        Environment.Exit(0);
                    }
                }
                }
            }
            catch (Exception ex)
            {
                // Bei Fehlern in der Admin-Überprüfung: Warnung loggen aber fortfahren
                try
                {
                    System.Diagnostics.Debug.WriteLine($"AdminRightsChecker.CheckAndWarnIfNotAdmin failed: {ex}");
                }
                catch { }
                // Nicht erneut werfen - Anwendung soll trotzdem starten
            }
        }

        /// <summary>
        /// Versucht die Anwendung als Administrator neu zu starten
        /// </summary>
        public static bool RestartAsAdministrator()
        {
            try
            {
                var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                var exePath = currentProcess.MainModule?.FileName;
                
                if (string.IsNullOrEmpty(exePath))
                {
                    return false;
                }

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    Verb = "runas", // Fordert Administratorrechte an
                    Arguments = string.Join(" ", Environment.GetCommandLineArgs().Skip(1)) // Ursprüngliche Argumente weiterleiten
                };

                System.Diagnostics.Process.Start(startInfo);
                return true;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // Benutzer hat UAC-Dialog abgebrochen oder andere Win32-Fehler
                if (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
                {
                    MessageBox.Show(
                        "Der Benutzer hat die Administratorrechte-Anfrage abgebrochen.\n" +
                        "Die Anwendung wird ohne Administratorrechte fortgesetzt.",
                        "UAC abgebrochen",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Prüft die Benutzeridentität und gibt Informationen zurück
        /// </summary>
        public static string GetCurrentUserInfo()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    var isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                    var userName = identity.Name ?? "Unbekannt";
                    var authType = identity.AuthenticationType ?? "Unbekannt";
                    var isAuthenticated = identity.IsAuthenticated;

                    return $"Benutzer: {userName}\n" +
                           $"Authentifizierung: {authType}\n" +
                           $"Authentifiziert: {(isAuthenticated ? "Ja" : "Nein")}\n" +
                           $"Administrator: {(isAdmin ? "Ja" : "Nein")}";
                }
            }
            catch (Exception ex)
            {
                return $"Fehler beim Abrufen der Benutzerinformationen: {ex.Message}";
            }
        }

        /// <summary>
        /// Prüft, ob die integrierte Windows-Authentifizierung verfügbar ist
        /// </summary>
        public static bool IsIntegratedAuthAvailable()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    return identity.IsAuthenticated && 
                           !string.IsNullOrEmpty(identity.Name) &&
                           (identity.AuthenticationType?.Equals("NTLM", StringComparison.OrdinalIgnoreCase) == true ||
                            identity.AuthenticationType?.Equals("Kerberos", StringComparison.OrdinalIgnoreCase) == true ||
                            identity.AuthenticationType?.Equals("Negotiate", StringComparison.OrdinalIgnoreCase) == true);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Zeigt detaillierte Informationen über die aktuelle Sicherheitskontext
        /// </summary>
        public static void ShowSecurityInfo()
        {
            var info = GetCurrentUserInfo();
            var integratedAuth = IsIntegratedAuthAvailable();
            
            var message = $"Sicherheitsinformationen:\n\n{info}\n\n" +
                         $"Integrierte Authentifizierung: {(integratedAuth ? "Verfügbar" : "Nicht verfügbar")}\n\n" +
                         "Hinweis: Für optimale Funktionalität sollte die Anwendung\n" +
                         "als Administrator mit integrierter Windows-Authentifizierung laufen.";

            MessageBox.Show(
                message,
                "DHCP WMI Viewer - Sicherheitsinformationen",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
    }
}