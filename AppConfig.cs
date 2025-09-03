// AppConfig.cs
using System;
using System.IO;

namespace DhcpWmiViewer
{
    /// <summary>
    /// Einfache Konfigurationsklasse für Anwendungseinstellungen
    /// </summary>
    internal static class AppConfig
    {
        private static readonly string ConfigFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DhcpWmiViewer",
            "config.txt"
        );

        /// <summary>
        /// Ob automatischer Neustart als Administrator angeboten werden soll
        /// </summary>
        public static bool EnableAutoRestartAsAdmin { get; set; } = true;

        /// <summary>
        /// Ob die Administratorrechte-Warnung beim Start angezeigt werden soll
        /// </summary>
        public static bool ShowAdminWarning { get; set; } = true;

        /// <summary>
        /// Lädt die Konfiguration aus der Datei
        /// </summary>
        public static void Load()
        {
            try
            {
                if (!File.Exists(ConfigFilePath))
                    return;

                var lines = File.ReadAllLines(ConfigFilePath);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    var parts = line.Split('=', 2);
                    if (parts.Length != 2)
                        continue;

                    var key = parts[0].Trim();
                    var value = parts[1].Trim();

                    switch (key.ToLowerInvariant())
                    {
                        case "enableautorestartasadmin":
                            if (bool.TryParse(value, out var autoRestart))
                                EnableAutoRestartAsAdmin = autoRestart;
                            break;

                        case "showadminwarning":
                            if (bool.TryParse(value, out var showWarning))
                                ShowAdminWarning = showWarning;
                            break;
                    }
                }
            }
            catch
            {
                // Fehler beim Laden ignorieren, Standardwerte verwenden
            }
        }

        /// <summary>
        /// Speichert die aktuelle Konfiguration in die Datei
        /// </summary>
        public static void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(ConfigFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var content = $"# DHCP WMI Viewer Konfiguration\n" +
                             $"# Erstellt am: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n" +
                             $"# Automatischer Neustart als Administrator anbieten (true/false)\n" +
                             $"EnableAutoRestartAsAdmin={EnableAutoRestartAsAdmin}\n\n" +
                             $"# Administratorrechte-Warnung beim Start anzeigen (true/false)\n" +
                             $"ShowAdminWarning={ShowAdminWarning}\n";

                File.WriteAllText(ConfigFilePath, content);
            }
            catch
            {
                // Fehler beim Speichern ignorieren
            }
        }

        /// <summary>
        /// Gibt den Pfad zur Konfigurationsdatei zurück
        /// </summary>
        public static string GetConfigFilePath() => ConfigFilePath;
    }
}