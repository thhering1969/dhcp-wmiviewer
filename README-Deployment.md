# DHCP WMI Viewer - Deployment Guide

## Verfügbare Versionen

### 1. Standalone EXE (DhcpWmiViewer.exe)
- **Datei**: `DhcpWmiViewer.exe` (im Root-Verzeichnis)
- **Größe**: ~0,15 MB
- **Voraussetzungen**: .NET 9.0 Runtime muss auf dem Zielsystem installiert sein
- **Verwendung**: Direkt ausführbar, wenn .NET 9.0 vorhanden ist

### 2. Portable Single-File EXE (DhcpWmiViewer-Portable.exe) ⭐ EMPFOHLEN
- **Datei**: `DhcpWmiViewer-Portable.exe`
- **Größe**: ~153 MB
- **Voraussetzungen**: Keine - enthält alle notwendigen .NET-Dateien
- **Verwendung**: Direkt ausführbar - einfach die EXE-Datei starten
- **Vorteile**: 
  - Eine einzige Datei
  - Keine Installation erforderlich
  - Funktioniert auf jedem Windows-System

### 3. Portable ZIP-Version (DhcpWmiViewer-Portable.zip)
- **Datei**: `DhcpWmiViewer-Portable.zip`
- **Größe**: ~62 MB (komprimiert)
- **Voraussetzungen**: Keine - enthält alle notwendigen .NET-Dateien
- **Verwendung**: 
  1. ZIP-Datei entpacken
  2. `DhcpWmiViewer.exe` aus dem entpackten Ordner ausführen

## Empfohlene Verwendung

### Für die meisten Anwender ⭐
- **Verwenden Sie die Portable Single-File EXE** (`DhcpWmiViewer-Portable.exe`)
- Eine einzige Datei - einfach zu verwenden
- Keine Installation von .NET erforderlich
- Funktioniert auf jedem Windows-System (Windows 10/11)

### Für Entwicklungsumgebungen mit .NET 9.0
- Verwenden Sie die Standalone EXE (`DhcpWmiViewer.exe`)
- Kleinste Dateigröße
- Schnellster Start

### Für Umgebungen mit Speicherplatz-Beschränkungen
- Verwenden Sie die ZIP-Version (`DhcpWmiViewer-Portable.zip`)
- Komprimiert - kleinste Download-Größe
- Muss entpackt werden vor der Verwendung

## Systemanforderungen

- **Betriebssystem**: Windows 10 (Version 1809) oder höher, Windows 11
- **Architektur**: x64 (64-bit)
- **Berechtigungen**: Administratorrechte für DHCP-Verwaltung
- **Netzwerk**: Zugriff auf DHCP-Server (WMI/RPC)

## Installation

### Portable Single-File EXE (Empfohlen) ⭐
1. Kopieren Sie `DhcpWmiViewer-Portable.exe` auf das Zielsystem
2. Führen Sie die EXE als Administrator aus
3. **Fertig!** - Keine weitere Installation erforderlich

### Standalone Version
1. Kopieren Sie `DhcpWmiViewer.exe` auf das Zielsystem
2. Stellen Sie sicher, dass .NET 9.0 Runtime installiert ist
3. Führen Sie die EXE als Administrator aus

### Portable ZIP-Version
1. Entpacken Sie `DhcpWmiViewer-Portable.zip` in einen Ordner Ihrer Wahl
2. Führen Sie `DhcpWmiViewer.exe` aus dem entpackten Ordner als Administrator aus

## Funktionen

- DHCP-Server-Erkennung und -Verbindung
- Anzeige von DHCP-Scopes und -Leases
- Verwaltung von DHCP-Reservierungen
- Konvertierung von Leases zu Reservierungen
- PowerShell-Integration für erweiterte Funktionen
- Netzwerk-Ping-Tests
- IP-Adress-Verwaltung
- **Automatische Administratorrechte-Überprüfung beim Start**
- **Sicherheitsinformationen und Benutzerkontext-Anzeige**
- **Integrierte Windows-Authentifizierung-Erkennung**

## Sicherheitsfeatures

### Automatische Administratorrechte-Überprüfung
- Die Anwendung prüft beim Start automatisch, ob sie mit Administratorrechten läuft
- Bei fehlenden Rechten wird eine Warnung angezeigt mit der Option, trotzdem fortzufahren
- **Empfehlung**: Starten Sie die Anwendung immer als Administrator

### Sicherheitsinformationen anzeigen
- Menü: **Hilfe → Sicherheitsinformationen**
- Zeigt aktuellen Benutzer, Authentifizierungstyp und Administratorstatus
- Hilfreich zur Diagnose von Verbindungsproblemen

### Integrierte Windows-Authentifizierung
- Automatische Erkennung, ob integrierte Auth verfügbar ist
- Fallback auf Credential-Eingabe bei Remote-Servern
- Caching von Anmeldedaten für bessere Benutzererfahrung

### Konfigurationsoptionen
Die Anwendung erstellt automatisch eine Konfigurationsdatei unter:
`%APPDATA%\DhcpWmiViewer\config.txt`

Verfügbare Einstellungen:
- `EnableAutoRestartAsAdmin=true/false` - Automatischen Neustart als Administrator anbieten
- `ShowAdminWarning=true/false` - Administratorrechte-Warnung beim Start anzeigen

**Beispiel-Konfiguration:**
```
# DHCP WMI Viewer Konfiguration
EnableAutoRestartAsAdmin=true
ShowAdminWarning=true
```

## Fehlerbehebung

### "Anwendung kann nicht gestartet werden"
- **Starten Sie die Anwendung als Administrator** (Rechtsklick → "Als Administrator ausführen")
- Bei Standalone-Version: Installieren Sie .NET 9.0 Runtime
- Bei Portable-Version: Alle Dateien aus dem ZIP extrahieren

### "Keine DHCP-Server gefunden" / "PowerShell-Fehler"
- **Überprüfen Sie Administratorrechte** (Hilfe → Sicherheitsinformationen)
- Überprüfen Sie die Netzwerkverbindung
- Stellen Sie sicher, dass WMI-Zugriff auf den DHCP-Server möglich ist
- Firewall-Einstellungen prüfen (WinRM/RPC-Ports)
- DHCP-Server-Rolle auf dem Zielserver installiert

### "Access Denied" / Authentifizierungsfehler
- Anwendung als Administrator starten
- Bei Remote-Servern: Gültige Anmeldedaten eingeben
- WinRM auf dem Zielserver aktiviert und konfiguriert
- Benutzer muss Mitglied der "DHCP Administrators" Gruppe sein

### Performance-Probleme
- Verwenden Sie die Standalone-Version wenn möglich
- Schließen Sie andere ressourcenintensive Anwendungen
- Bei Remote-Verbindungen: Netzwerklatenz prüfen

## Support

Bei Problemen oder Fragen wenden Sie sich an den Entwickler oder erstellen Sie ein Issue im Repository.