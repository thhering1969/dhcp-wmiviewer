// AppConstants.cs
namespace DhcpWmiViewer
{
    /// <summary>Zentrale, projektweite Konstanten.</summary>
    public static class AppConstants
    {
        // EventLog-Quelle für die Protokollierung von Änderungen
        public const string EventSourceName = "DhcpWmiViewer";
        public const string EventLogName = "Application";

        // Die IP-Bereiche mit Internetfreigabe sind umgebungsspezifisch und stehen deshalb nicht im Code,
        // sondern in der Konfiguration (AppConfig.InternetRanges, Extras → Internet-Bereiche).
    }
}
