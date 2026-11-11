// Loc.cs
using System.Globalization;

namespace DhcpWmiViewer
{
    /// <summary>
    /// Zweisprachige Oberfläche (Deutsch/Englisch). Die Sprache folgt der Windows-Anzeigesprache
    /// (CurrentUICulture, inkl. Sprachpaket): Deutsch -> deutsch, sonst englisch.
    /// Festlegen lässt sie sich mit "Language=de" oder "Language=en" in der Konfiguration.
    /// </summary>
    internal static class Loc
    {
        /// <summary>true = deutsche Oberfläche.</summary>
        public static bool IsGerman { get; private set; } =
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("de", StringComparison.OrdinalIgnoreCase);

        /// <summary>Überschreibt die automatische Erkennung ("de"/"en"; alles andere = automatisch).</summary>
        public static void SetLanguage(string? language)
        {
            var lang = language?.Trim().ToLowerInvariant();
            if (lang == "de") IsGerman = true;
            else if (lang == "en") IsGerman = false;
        }

        /// <summary>Text in der aktuellen Sprache.</summary>
        public static string T(string german, string english) => IsGerman ? german : english;
    }
}
