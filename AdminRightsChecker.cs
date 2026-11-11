// AdminRightsChecker.cs
using static DhcpWmiViewer.Loc;
using System.Security.Principal;

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
                var message = T("⚠️ WARNUNG: Administratorrechte erforderlich\n\n" +
                               "Diese Anwendung wurde nicht als Administrator gestartet.\n" +
                               "Für die DHCP-Verwaltung sind Administratorrechte erforderlich.\n\n" +
                               "Mögliche Probleme:\n" +
                               "• Keine Verbindung zu DHCP-Servern möglich\n" +
                               "• PowerShell-Befehle schlagen fehl\n\n",
                               "⚠️ WARNING: administrator rights required\n\n" +
                               "This application was not started as administrator.\n" +
                               "DHCP management requires administrator rights.\n\n" +
                               "Possible problems:\n" +
                               "• No connection to DHCP servers\n" +
                               "• PowerShell commands fail\n\n");

                if (AppConfig.EnableAutoRestartAsAdmin)
                {
                    message += T("Möchten Sie die Anwendung automatisch als Administrator neu starten?", "Restart the application as administrator now?");
                    
                    var result = MessageBox.Show(
                        message,
                        T("DHCP WMI Viewer - Administratorrechte erforderlich", "DHCP WMI Viewer - administrator rights required"),
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
                                    T("Automatischer Neustart als Administrator fehlgeschlagen.\n" +
                                      "Bitte starten Sie die Anwendung manuell als Administrator.",
                                      "Restarting as administrator failed.\n" +
                                      "Please start the application as administrator manually."),
                                    T("Neustart fehlgeschlagen", "Restart failed"),
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
                    message += T("Empfehlung:\n" +
                                "Starten Sie die Anwendung als Administrator neu.\n" +
                                "(Rechtsklick → 'Als Administrator ausführen')\n\n" +
                                "Möchten Sie trotzdem fortfahren?",
                                "Recommendation:\n" +
                                "Restart the application as administrator.\n" +
                                "(Right-click → 'Run as administrator')\n\n" +
                                "Continue anyway?");

                    var result = MessageBox.Show(
                        message,
                        T("DHCP WMI Viewer - Administratorrechte erforderlich", "DHCP WMI Viewer - administrator rights required"),
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
                        T("Der Benutzer hat die Administratorrechte-Anfrage abgebrochen.\n" +
                          "Die Anwendung wird ohne Administratorrechte fortgesetzt.",
                          "The administrator rights request was cancelled.\n" +
                          "The application continues without administrator rights."),
                        T("UAC abgebrochen", "UAC cancelled"),
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
                    var userName = identity.Name ?? T("Unbekannt", "Unknown");
                    var authType = identity.AuthenticationType ?? T("Unbekannt", "Unknown");
                    var isAuthenticated = identity.IsAuthenticated;

                    var yes = T("Ja", "Yes"); var no = T("Nein", "No");
                    return T("Benutzer: ", "User: ") + userName + "\n" +
                           T("Authentifizierung: ", "Authentication: ") + authType + "\n" +
                           T("Authentifiziert: ", "Authenticated: ") + (isAuthenticated ? yes : no) + "\n" +
                           "Administrator: " + (isAdmin ? yes : no);
                }
            }
            catch (Exception ex)
            {
                return T("Fehler beim Abrufen der Benutzerinformationen: ", "Error reading user information: ") + ex.Message;
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
            
            var message = T("Sicherheitsinformationen:", "Security information:") + $"\n\n{info}\n\n" +
                         T("Integrierte Authentifizierung: ", "Integrated authentication: ") + (integratedAuth ? T("Verfügbar", "available") : T("Nicht verfügbar", "not available")) + "\n\n" +
                         T("Hinweis: Für optimale Funktionalität sollte die Anwendung\nals Administrator mit integrierter Windows-Authentifizierung laufen.",
                           "Note: for best results run the application\nas administrator with integrated Windows authentication.");

            MessageBox.Show(
                message,
                T("DHCP WMI Viewer - Sicherheitsinformationen", "DHCP WMI Viewer - security information"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
    }
}