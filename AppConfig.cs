// AppConfig.cs
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
            // Optionale Vorgabe neben der EXE (z. B. firmenweit verteilt), danach die Benutzerdatei (hat Vorrang)
            LoadFile(SiteConfigFilePath);
            LoadFile(ConfigFilePath);
        }

        /// <summary>
        /// Optionale Vorgabedatei im Ordner der EXE ("DhcpWmiViewer.config.txt", gleiches Format).
        /// Damit lassen sich z. B. die Internet-Bereiche für alle Kollegen vorbelegen.
        /// </summary>
        private static string SiteConfigFilePath => Path.Combine(
            Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory,
            "DhcpWmiViewer.config.txt");

        private static void LoadFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return;

                var lines = File.ReadAllLines(path);
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

                        case "lastserver":
                            LastServer = value;
                            break;

                        case "language":
                            Language = value;
                            Loc.SetLanguage(value);
                            break;

                        case "internetranges":
                            InternetRanges = ParseRanges(value, out _);
                            break;
                    }
                }
            }
            catch
            {
                // Fehler beim Laden ignorieren, Standardwerte verwenden
            }
        }

        /// <summary>Zuletzt erfolgreich abgefragter DHCP-Server (Vorbelegung beim Start).</summary>
        public static string LastServer { get; private set; } = string.Empty;

        /// <summary>Merkt sich den Server in der Konfigurationsdatei; andere Einträge bleiben erhalten.</summary>
        public static void SaveLastServer(string server)
        {
            if (string.IsNullOrWhiteSpace(server) || server.Equals(LastServer, StringComparison.OrdinalIgnoreCase)) return;
            LastServer = server.Trim();
            try { SaveValue("LastServer", LastServer); }
            catch { /* Nicht kritisch */ }
        }

        /// <summary>
        /// IP-Bereiche mit Internetfreigabe (z. B. von der Firewall vorgegeben), jeweils inklusive.
        /// Leer = Funktion aus (keine 🌐-Markierung, kein Filter). Einstellbar über Extras → Internet-Bereiche.
        /// </summary>
        public static IReadOnlyList<(string Start, string End)> InternetRanges { get; private set; } = Array.Empty<(string, string)>();

        /// <summary>Gespeicherte Sprache: "de", "en" oder leer (= automatisch nach Windows-Anzeigesprache).</summary>
        public static string Language { get; private set; } = string.Empty;

        /// <summary>Speichert die Sprache ("de", "en" oder "" = automatisch); wirkt nach Neustart.</summary>
        public static void SaveLanguage(string language)
        {
            SaveValue("Language", language);
            Language = language;
        }

        /// <summary>Speichert die Internet-Bereiche in der Benutzerdatei (leere Liste = Funktion aus). Wirft bei Schreibfehlern.</summary>
        public static void SaveInternetRanges(IReadOnlyList<(string Start, string End)> ranges)
        {
            SaveValue("InternetRanges", FormatRanges(ranges));
            InternetRanges = ranges;
        }

        /// <summary>"a-b, c-d" (Komma, Semikolon oder Zeilenumbruch als Trenner). Ungültige Einträge landen in <paramref name="errors"/>.</summary>
        public static List<(string Start, string End)> ParseRanges(string text, out List<string> errors)
        {
            var result = new List<(string, string)>();
            errors = new List<string>();
            foreach (var part in (text ?? string.Empty).Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var ips = part.Split('-', 2, StringSplitOptions.TrimEntries);
                var start = ips[0];
                var end = ips.Length == 2 ? ips[1] : ips[0]; // einzelne Adresse erlaubt
                if (NetworkHelper.TryIpToUInt32(start, out var a) && NetworkHelper.TryIpToUInt32(end, out var b) && a <= b)
                    result.Add((start, end));
                else
                    errors.Add(part);
            }
            return result;
        }

        public static string FormatRanges(IEnumerable<(string Start, string End)> ranges)
            => string.Join(", ", ranges.Select(r => r.Start == r.End ? r.Start : $"{r.Start}-{r.End}"));

        /// <summary>Setzt "Key=Wert" in der Benutzerdatei; andere Einträge bleiben erhalten. Ein leerer Wert bleibt stehen und überschreibt so eine Vorgabe neben der EXE.</summary>
        private static void SaveValue(string key, string value)
        {
            var lines = File.Exists(ConfigFilePath) ? File.ReadAllLines(ConfigFilePath).ToList() : new List<string>();
            lines.RemoveAll(l => l.TrimStart().StartsWith(key + "=", StringComparison.OrdinalIgnoreCase));
            lines.Add(key + "=" + value);
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigFilePath)!);
            File.WriteAllLines(ConfigFilePath, lines);
        }

    }
}