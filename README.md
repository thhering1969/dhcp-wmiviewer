# 🌐 DHCP WMI Viewer

[![.NET](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![Windows](https://img.shields.io/badge/OS-Windows-blue.svg)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![PowerShell](https://img.shields.io/badge/PowerShell-7.5-blue.svg)](https://github.com/PowerShell/PowerShell)

Ein leistungsstarkes Windows Forms Tool zur Verwaltung und Überwachung von DHCP-Servern über WMI und PowerShell.

## 📋 Inhaltsverzeichnis

- [Features](#-features)
- [Screenshots](#-screenshots)
- [Installation](#-installation)
- [Systemanforderungen](#-systemanforderungen)
- [Verwendung](#-verwendung)
- [Architektur](#-architektur)
- [Entwicklung](#-entwicklung)
- [Deployment](#-deployment)
- [Fehlerbehebung](#-fehlerbehebung)
- [Beitragen](#-beitragen)
- [Lizenz](#-lizenz)

## ✨ Features

### 🎯 Kernfunktionen
- **DHCP-Server-Erkennung**: Automatische Erkennung von DHCP-Servern im Active Directory
- **Scope-Verwaltung**: Anzeige und Verwaltung von DHCP-Scopes
- **Lease-Management**: Übersicht über aktive und abgelaufene DHCP-Leases
- **Reservierungen**: Erstellen, Bearbeiten und Löschen von DHCP-Reservierungen
- **Lease-zu-Reservierung**: Einfache Konvertierung von Leases zu permanenten Reservierungen

### 🔧 Erweiterte Features
- **PowerShell-Integration**: Robuste PowerShell-Ausführung mit automatischem Fallback
- **Netzwerk-Tools**: Integrierte Ping-Tests und IP-Adress-Validierung
- **Credential-Management**: Sichere Verwaltung von Anmeldedaten für Remote-Server
- **CSV-Export**: Export von Daten für weitere Analyse
- **Prefetching**: Intelligente Vorab-Ladung von Daten für bessere Performance

### 🛡️ Sicherheit & Administration
- **Automatische Admin-Rechte-Prüfung**: Überprüfung und Warnung bei fehlenden Administratorrechten
- **Integrierte Windows-Authentifizierung**: Nahtlose Authentifizierung mit Windows-Credentials
- **Sicherheitsinformationen**: Detaillierte Anzeige des aktuellen Sicherheitskontexts
- **Event-Logging**: Umfassendes Logging für Diagnose und Audit

### 🎨 Benutzeroberfläche
- **Moderne Windows Forms UI**: Responsive Design mit DPI-Awareness
- **Kontext-Menüs**: Intuitive Rechtsklick-Menüs für alle Aktionen
- **Tabbed Interface**: Übersichtliche Darstellung von Scopes, Reservierungen und Leases
- **Statusanzeigen**: Echtzeit-Feedback über laufende Operationen

## 🖼️ Screenshots

*Screenshots werden hier eingefügt, sobald verfügbar*

## 📦 Installation

### 🚀 Schnellstart (Empfohlen)

**Portable Single-File Version** - Keine Installation erforderlich:

1. Laden Sie `DhcpWmiViewer-Portable.exe` herunter
2. Führen Sie die Datei als Administrator aus
3. **Fertig!** - Alle .NET-Abhängigkeiten sind bereits enthalten

### 📋 Verfügbare Versionen

| Version | Datei | Größe | Voraussetzungen | Beschreibung |
|---------|-------|-------|-----------------|--------------|
| **Portable EXE** ⭐ | `DhcpWmiViewer-Portable.exe` | ~153 MB | Keine | Single-File, Self-Contained |
| **Portable ZIP** | `DhcpWmiViewer-Portable.zip` | ~62 MB | Keine | Entpacken erforderlich |
| **Standalone** | `DhcpWmiViewer.exe` | ~0.15 MB | .NET 9.0 Runtime | Kleinste Dateigröße |

### 🔧 Manuelle Installation

```bash
# Klonen des Repositories
git clone https://github.com/thhering1969/kurzzeit-dhcp-wmiviewer.git
cd kurzzeit-dhcp-wmiviewer

# Build
dotnet build -c Release

# Oder Portable Version erstellen
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## 🖥️ Systemanforderungen

### Minimum
- **OS**: Windows 10 (Version 1809) oder höher
- **Architektur**: x64 (64-bit)
- **RAM**: 512 MB
- **Festplatte**: 200 MB freier Speicherplatz

### Empfohlen
- **OS**: Windows 11
- **RAM**: 2 GB oder mehr
- **Berechtigungen**: Administratorrechte
- **Netzwerk**: Zugriff auf DHCP-Server (WMI/RPC-Ports)

### Abhängigkeiten (bei Standalone-Version)
- **.NET 9.0 Runtime** (Windows Desktop)
- **PowerShell 7.5+** (optional, für erweiterte Features)

## 🚀 Verwendung

### Erste Schritte

1. **Als Administrator starten** (wichtig für DHCP-Zugriff)
2. **DHCP-Server entdecken**: Klick auf "Discover" für automatische AD-Erkennung
3. **Server auswählen**: Wählen Sie einen Server aus der Dropdown-Liste
4. **Scopes laden**: Klick auf "Load Scopes" für Scope-Übersicht

### Hauptfunktionen

#### 🔍 DHCP-Server-Erkennung
```
Menü: Server → Discover DHCP Servers
- Automatische Erkennung über Active Directory
- Manuelle Server-Eingabe möglich
- Lokale DHCP-Server-Erkennung
```

#### 📊 Scope-Management
```
Tab: Scopes
- Übersicht aller DHCP-Scopes
- Scope-Details und Statistiken
- Aktivierung/Deaktivierung von Scopes
```

#### 🏷️ Reservierungen verwalten
```
Tab: Reservations
- Neue Reservierung erstellen
- Bestehende Reservierungen bearbeiten
- Reservierungen löschen
- Bulk-Operationen
```

#### 📋 Lease-Übersicht
```
Tab: Leases
- Aktive und abgelaufene Leases
- Lease-Details anzeigen
- Lease zu Reservierung konvertieren
- Lease-Historie
```

### Konfiguration

Die Anwendung erstellt automatisch eine Konfigurationsdatei:
```
%APPDATA%\DhcpWmiViewer\config.txt
```

**Beispiel-Konfiguration:**
```ini
# DHCP WMI Viewer Konfiguration
EnableAutoRestartAsAdmin=true
ShowAdminWarning=true
DefaultServer=dhcp-server.domain.com
AutoLoadScopes=true
PrefetchReservations=true
```

## 🏗️ Architektur

### Projektstruktur

```
DhcpWmiViewer/
├── 📁 Core Components
│   ├── Program.cs                 # Anwendungsstart
│   ├── MainForm.*.cs             # Haupt-UI (modular aufgeteilt)
│   ├── AppEnvironment.cs         # Umgebungserkennung
│   └── AppConfig.cs              # Konfigurationsverwaltung
│
├── 📁 DHCP Management
│   ├── DhcpManager.*.cs          # DHCP-Operationen (modular)
│   ├── DhcpDiscovery.cs          # Server-Erkennung
│   └── DhcpHelper.cs             # DHCP-Hilfsfunktionen
│
├── 📁 PowerShell Integration
│   ├── PowerShellExecutor.cs     # PowerShell-Ausführung
│   └── PowerShellInitializer.cs  # PowerShell-Initialisierung
│
├── 📁 UI Components
│   ├── *Dialog.cs                # Verschiedene Dialoge
│   ├── WaitDialog.cs             # Fortschrittsanzeige
│   └── CredentialDialog.cs       # Anmeldedaten-Dialog
│
├── 📁 Utilities
│   ├── NetworkHelper.cs          # Netzwerk-Funktionen
│   ├── PingHelper.cs             # Ping-Tests
│   ├── IpUtils.cs                # IP-Adress-Utilities
│   └── Helpers.cs                # Allgemeine Hilfsfunktionen
│
└── 📁 Security & Admin
    ├── AdminRightsChecker.cs     # Admin-Rechte-Prüfung
    └── EventLogger.cs            # Event-Logging
```

### Technologie-Stack

- **Framework**: .NET 9.0 (Windows Forms)
- **PowerShell**: Microsoft.PowerShell.SDK 7.5.2
- **WMI**: System.Management 9.0.8
- **Active Directory**: System.DirectoryServices 9.0.8
- **Deployment**: Single-File Self-Contained

### Design-Prinzipien

- **Modular**: Funktionen in separate Dateien aufgeteilt
- **Robust**: Umfassende Fehlerbehandlung und Fallback-Mechanismen
- **Performance**: Asynchrone Operationen und Prefetching
- **Security**: Automatische Admin-Rechte-Prüfung und sichere Credential-Verwaltung

## 👨‍💻 Entwicklung

### Entwicklungsumgebung einrichten

```bash
# Repository klonen
git clone https://github.com/thhering1969/kurzzeit-dhcp-wmiviewer.git
cd kurzzeit-dhcp-wmiviewer

# Dependencies wiederherstellen
dotnet restore

# Debug-Build
dotnet build

# Anwendung starten
dotnet run
```

### Build-Konfigurationen

```bash
# Debug Build
dotnet build -c Debug

# Release Build
dotnet build -c Release

# Portable Single-File
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# Portable Folder
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false
```

### Code-Stil

- **C# 12** Features verwenden
- **Nullable Reference Types** aktiviert
- **Async/Await** für I/O-Operationen
- **Exception Handling** mit spezifischen Catch-Blöcken
- **XML Documentation** für öffentliche APIs

### Testing

```bash
# Unit Tests ausführen (falls vorhanden)
dotnet test

# Integration Tests
# Manuelle Tests mit verschiedenen DHCP-Server-Konfigurationen
```

## 🚀 Deployment

Detaillierte Deployment-Informationen finden Sie in [README-Deployment.md](README-Deployment.md).

### Automatisierte Builds

```bash
# Alle Versionen erstellen
./build-all.ps1

# Oder manuell:
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o DhcpWmiViewer-Portable-Files
Compress-Archive -Path "DhcpWmiViewer-Portable-Files\*" -DestinationPath "DhcpWmiViewer-Portable.zip"
```

### Verteilung

- **GitHub Releases**: Automatische Erstellung von Release-Artefakten
- **Interne Verteilung**: Portable EXE für einfache Verteilung
- **Enterprise**: MSI-Installer (geplant)

## 🔧 Fehlerbehebung

### Häufige Probleme

#### ❌ "Anwendung kann nicht gestartet werden"
**Lösung:**
- Als Administrator ausführen
- Bei Standalone: .NET 9.0 Runtime installieren
- Antivirus-Software temporär deaktivieren

#### ❌ "Keine DHCP-Server gefunden"
**Lösung:**
```powershell
# PowerShell-Ausführungsrichtlinie prüfen
Get-ExecutionPolicy
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser

# WinRM aktivieren
Enable-PSRemoting -Force

# DHCP-Module verfügbar?
Get-Module -ListAvailable *DHCP*
```

#### ❌ "Access Denied" Fehler
**Lösung:**
- Anwendung als Administrator starten
- Benutzer zur "DHCP Administrators" Gruppe hinzufügen
- WMI-Berechtigungen prüfen

#### ❌ PowerShell-Fehler
**Lösung:**
```powershell
# PowerShell-Version prüfen
$PSVersionTable.PSVersion

# DHCP-Cmdlets testen
Get-Command *DhcpServer*

# Execution Policy prüfen
Get-ExecutionPolicy -List
```

### Debug-Informationen

**Sicherheitsinformationen anzeigen:**
```
Menü: Hilfe → Sicherheitsinformationen
```

**Log-Dateien:**
```
%TEMP%\DhcpWmiViewer-crash.log
Windows Event Log (Application)
```

### Performance-Optimierung

- **Prefetching deaktivieren** bei langsamen Netzwerken
- **Scope-Filter verwenden** bei vielen Scopes
- **Lokale Ausführung** bevorzugen wenn möglich

## 🤝 Beitragen

Beiträge sind willkommen! Bitte beachten Sie:

### Entwicklungsrichtlinien

1. **Fork** des Repositories erstellen
2. **Feature Branch** erstellen (`git checkout -b feature/AmazingFeature`)
3. **Commits** mit aussagekräftigen Nachrichten
4. **Tests** hinzufügen für neue Features
5. **Pull Request** erstellen

### Code-Standards

- **C# Coding Conventions** befolgen
- **XML Documentation** für öffentliche APIs
- **Error Handling** mit spezifischen Exceptions
- **Async/Await** für I/O-Operationen

### Issue-Reporting

Bei Bugs oder Feature-Requests bitte folgende Informationen angeben:

```markdown
**Umgebung:**
- Windows Version: 
- .NET Version: 
- Anwendungsversion: 

**Problem:**
- Beschreibung: 
- Schritte zur Reproduktion: 
- Erwartetes Verhalten: 
- Tatsächliches Verhalten: 

**Logs:**
- Fehlermeldungen: 
- Log-Dateien: 
```

## 📄 Lizenz

Dieses Projekt steht unter der MIT-Lizenz. Siehe [LICENSE](LICENSE) für Details.

## 🙏 Danksagungen

- **Microsoft** für .NET und PowerShell
- **Windows Forms Community** für UI-Komponenten
- **DHCP-Administratoren** für Feedback und Testing

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/thhering1969/kurzzeit-dhcp-wmiviewer/issues)
- **Discussions**: [GitHub Discussions](https://github.com/thhering1969/kurzzeit-dhcp-wmiviewer/discussions)
- **Email**: [Entwickler kontaktieren](mailto:thorsten.hering@goevb.de)

---

**⭐ Wenn Ihnen dieses Projekt gefällt, geben Sie ihm einen Stern auf GitHub!**

*Entwickelt mit ❤️ für Windows-Administratoren*