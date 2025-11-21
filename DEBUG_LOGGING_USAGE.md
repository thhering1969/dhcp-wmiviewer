# Debug Logger Usage Guide

## Übersicht

Das DhcpWmiViewer-Projekt verwendet ein duales Logging-System:

1. **EventLogger** - Lokaler Windows EventLog-Logger
2. **MainForm.LogGuiEventAsync** - Remote-Logging über PowerShell
3. **DebugLogger** - Vereinheitlichte Schnittstelle für beide Mechanismen

## Verwendung

### 1. Debug-Ausgaben (nur in Debug-Builds)

```csharp
// Einfache Debug-Nachricht mit automatischen Caller-Informationen
DebugLogger.LogDebug("Verarbeitung gestartet");

// Ausgabe: [DEBUG] MainForm.ProcessData:123 - Verarbeitung gestartet
```

### 2. Info-Level Logging

```csharp
// Einfache Info-Nachricht (lokal + remote)
DebugLogger.LogInfo("DHCP-Server erfolgreich verbunden");

// Mit zusätzlichen GUI-Action-Details
DebugLogger.LogInfo("Scope geladen", "LoadScope", "192.168.1.0/24", "192.168.1.100", "25 Leases gefunden");
```

### 3. Error-Level Logging

```csharp
// Einfacher Fehler
DebugLogger.LogError("Verbindung zum DHCP-Server fehlgeschlagen");

// Mit Exception
try 
{
    // ... Code ...
}
catch (Exception ex)
{
    DebugLogger.LogError("DHCP-Operation fehlgeschlagen", ex, "DhcpOperation", scopeId, clientIp);
}
```

### 4. GUI-Action Logging

```csharp
// Für Benutzer-Aktionen in der GUI
DebugLogger.LogGuiAction("ReservationCreated", "192.168.1.0/24", "192.168.1.100", "MAC: 00:11:22:33:44:55");
DebugLogger.LogGuiAction("ScopeRefreshed", "192.168.1.0/24");
DebugLogger.LogGuiAction("LeaseDeleted", "192.168.1.0/24", "192.168.1.150");
```

### 5. Performance-Logging (nur in Debug-Builds)

```csharp
// Manuelle Performance-Messung
var stopwatch = Stopwatch.StartNew();
// ... zeitkritische Operation ...
stopwatch.Stop();
DebugLogger.LogPerformance("DHCP-Scope-Load", stopwatch.Elapsed);

// Automatische Performance-Messung mit using-Statement
using (DebugLogger.MeasurePerformance("ComplexOperation"))
{
    // ... zeitkritische Operation ...
    // Automatische Zeitmessung und Logging beim Dispose
}
```

## Log-Ausgaben

### Debug-Builds
- **Debug-Ausgaben**: Visual Studio Debug-Fenster + `%TEMP%\DhcpWmiViewer-debug.log`
- **Info/Error**: Windows EventLog + Remote-Server EventLog + Debug-Ausgaben
- **Performance**: Debug-Ausgaben

### Release-Builds
- **Debug-Ausgaben**: Werden komplett entfernt (Conditional Compilation)
- **Info/Error**: Windows EventLog + Remote-Server EventLog
- **Performance**: Werden komplett entfernt

## Log-Dateien

| Datei | Zweck | Ort |
|-------|-------|-----|
| `DhcpWmiViewer-debug.log` | Debug-Ausgaben | `%TEMP%` |
| `DhcpWmiViewer-eventlog-fallback.log` | EventLog-Fallback | `%TEMP%` |
| `DhcpWmiViewer-crash.log` | Unbehandelte Exceptions | `%TEMP%` |

## Konfiguration

### EventLog-Konfiguration
```csharp
// In AppConstants.cs
public const string EventSourceName = "DhcpWmiViewer";
public const string EventLogName = "Application";
```

### Initialisierung
```csharp
// In Program.cs - EventLogger wird automatisch initialisiert
EventLogger.Initialize(AppConstants.EventSourceName, AppConstants.EventLogName, tryCreateSource: true);

// In MainForm.Core.cs - DebugLogger wird automatisch initialisiert
DebugLogger.Initialize(this);
```

## Best Practices

1. **Debug-Ausgaben**: Verwenden Sie `LogDebug()` für detaillierte Entwicklungsinformationen
2. **Info-Logging**: Verwenden Sie `LogInfo()` für wichtige Anwendungsereignisse
3. **Error-Logging**: Verwenden Sie `LogError()` für alle Fehler mit Exception-Details
4. **GUI-Actions**: Verwenden Sie `LogGuiAction()` für alle Benutzer-Interaktionen
5. **Performance**: Verwenden Sie `MeasurePerformance()` für zeitkritische Operationen

## Beispiel-Integration

```csharp
public async Task LoadDhcpScopesAsync()
{
    using (DebugLogger.MeasurePerformance("LoadDhcpScopes"))
    {
        try
        {
            DebugLogger.LogDebug("Starte DHCP-Scope-Laden");
            DebugLogger.LogInfo("DHCP-Scopes werden geladen", "LoadScopes");
            
            // ... DHCP-Logik ...
            
            DebugLogger.LogGuiAction("ScopesLoaded", "", "", $"{scopeCount} Scopes geladen");
            DebugLogger.LogInfo($"DHCP-Scopes erfolgreich geladen: {scopeCount} Scopes");
        }
        catch (Exception ex)
        {
            DebugLogger.LogError("Fehler beim Laden der DHCP-Scopes", ex, "LoadScopes");
            throw;
        }
    }
}
```

## Troubleshooting

### EventLog-Probleme
- **Keine Admin-Rechte**: EventSource wird nicht erstellt, Fallback auf Temp-Datei
- **Remote-Verbindung fehlgeschlagen**: Nur lokales Logging aktiv

### Debug-Ausgaben fehlen
- **Release-Build**: Debug-Ausgaben werden komplett entfernt
- **Debug-Build**: Prüfen Sie das Visual Studio Debug-Fenster und die Temp-Datei

### Performance-Impact
- **Debug-Builds**: Minimaler Impact durch Conditional Compilation
- **Release-Builds**: Kein Impact, da Debug-Code entfernt wird