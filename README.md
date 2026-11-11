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

1. Laden Sie `DhcpWmiViewer-Portable.exe` von der [Releases-Seite](https://github.com/thhering1969/dhcp-wmiviewer/releases/latest) herunter
2. Führen Sie die Datei als Administrator aus
3. **Fertig!** - Alle .NET-Abhängigkeiten sind bereits enthalten

### 📋 Verfügbare Versionen

Alle Versionen stehen unter [Releases](https://github.com/thhering1969/dhcp-wmiviewer/releases) zum Download bereit.


| Version | Datei | Größe | Voraussetzungen | Beschreibung |
|---------|-------|-------|-----------------|--------------|
| **Portable EXE** ⭐ | `DhcpWmiViewer-Portable.exe` | ~153 MB | Keine | Single-File, Self-Contained |
| **Portable ZIP** | `DhcpWmiViewer-Portable.zip` | ~62 MB | Keine | Entpacken erforderlich |
| **Standalone** | `DhcpWmiViewer.exe` | ~0.15 MB | .NET 9.0 Runtime | Kleinste Dateigröße |

### 🔧 Manuelle Installation

```bash
# Klonen des Repositories
git clone https://github.com/thhering1969/dhcp-wmiviewer.git
cd dhcp-wmiviewer

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
Toolbar: Discover
- Liest die im Active Directory autorisierten DHCP-Server
- Prüft jeden Server vor der Anzeige (parallel, ca. 2,5 s Zeitlimit):
  Computerkonto im AD vorhanden/aktiv, DNS auflösbar, WinRM (5985) erreichbar
- Abgeschaltete Server werden ausgeblendet und in der Statusleiste genannt
- Manuelle Server-Eingabe möglich (Enter = Abfragen); der letzte Server wird gemerkt
```

#### 🔌 Verbindung: RSAT lokal, sonst WinRM
Sind auf dem PC die **RSAT-DHCP-Tools** installiert, laufen alle DHCP-Befehle (Scopes,
Reservations, Leases, Anlegen/Ändern/Löschen, Bereich replizieren) **lokal** mit `-ComputerName`
und dem angemeldeten Windows-Benutzer. Fehlen die Tools oder scheitert RSAT an Rechten/RPC,
nutzt die App **WinRM** auf dem DHCP-Server (ggf. mit eingegebenen Anmeldedaten); nach
„Clear Credentials“ wird RSAT erneut probiert. Events lesen und ins EventLog schreiben läuft
immer über WinRM. Die Statusleiste zeigt den genutzten Weg („via RSAT“ / „via WinRM“).

Es gibt bewusst keine festen Serverlisten im Code – Server werden zur Laufzeit aus dem AD
ermittelt und geprüft. Umgebungsspezifisches (z. B. Internet-Bereiche) steht in der Konfiguration.

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

#### 🌐 Internet-Bereiche (optional)
Reservationen dürfen jede Adresse im Subnetz des Scopes erhalten. Gibt in Ihrer Umgebung z. B. die
**Firewall** vor, welche Adressen ins Internet dürfen, können Sie diese Bereiche unter
**Extras → Internet-Bereiche (Firewall)…** eintragen (einer pro Zeile, z. B. `10.0.0.10-10.0.0.50`).
Dann gilt:

- „IP auswählen“ markiert internetfähige Adressen mit 🌐; das Häkchen **„Nur internetfähige Adressen“**
  ist beim Öffnen gesetzt
- der Bestätigungsdialog zeigt hinter der IP „mit“ bzw. „ohne Internetfreigabe“

Ohne Einträge ist die Funktion ausgeblendet – es wird nichts abgefragt.

Für viele PCs lässt sich eine Vorgabe verteilen: eine Datei `DhcpWmiViewer.config.txt` im Ordner der
EXE (gleiches Format wie unten). Die Einstellungen des Benutzers haben Vorrang.

### Bereich replizieren und Protokoll

Nach jedem Anlegen, Ändern oder Löschen einer Reservation führt die App sofort **„Bereich replizieren“**
für den Scope aus – wie im Kontextmenü der DHCP-Konsole, zum Failover-Partner
(`Invoke-DhcpServerv4FailoverReplication`). Partner und Failover-Beziehung werden dabei live vom
DHCP-Server gelesen.

**Voraussetzung: RSAT-DHCP-Tools auf dem PC, auf dem die App läuft.** „Bereich replizieren“ wird
lokal ausgeführt, damit der PC direkt mit beiden DHCP-Servern spricht. Über WinRM scheitert der
Befehl am Kerberos-Double-Hop (der DHCP-Server darf die Anmeldung nicht an den Partner weitergeben).
Fehlen die Tools, fragt die App, ob sie installiert werden sollen
(`Add-WindowsCapability -Online -Name Rsat.DHCP.Tools~~~~0.0.1.0`, Adminrechte und Windows
Update/WSUS nötig). Bei „Nein“ wird „Bereich replizieren“ übersprungen und als Warnung protokolliert.
„Bereich replizieren“ läuft mit dem angemeldeten Windows-Benutzer, nicht mit in der App
eingegebenen Anmeldedaten.

Die Änderung und das angestoßene „Bereich replizieren“ werden im Application-EventLog **beider** Server
protokolliert (Quelle `DhcpWmiViewer`, Action `Bereich replizieren`). Die Erfolgsmeldung zeigt
das Ergebnis; schlägt „Bereich replizieren“ oder das Protokoll fehl, erscheint sie als Warnung. Hat ein Scope
keine Failover-Beziehung, wird nur auf dem aktiven Server protokolliert.

„Bereich replizieren…“ gibt es auch von Hand: Rechtsklick auf einen Scope in der Tabelle
„DHCP-Scope(s)“ (mit Rückfrage; protokolliert mit `Trigger=Manuell`).

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
# wird automatisch nach jeder erfolgreichen Abfrage gesetzt
LastServer=dhcp-server.domain.com
# optional, über Extras → Internet-Bereiche (leer = aus)
InternetRanges=10.0.0.10-10.0.0.50, 10.0.0.200-10.0.0.254
```

Die PowerShell-Befehlsvorschau (Debug) wird über **Extras → PowerShell-Befehle vor Ausführung anzeigen** eingeschaltet.

## 🏗️ Architektur

### Projektstruktur

```
DhcpWmiViewer/
├── 📁 Core Components
│   ├── Program.cs                 # Anwendungsstart
│   ├── MainForm.cs               # Hauptfenster: Felder, aktiver Server, Credentials
│   ├── MainForm.Layout.cs        # Aufbau der Controls
│   ├── MainForm.Scopes.cs        # Discovery, Scopes, Reservations/Leases laden
│   ├── MainForm.Reservations.cs  # Reservation ändern/löschen
│   ├── MainForm.Leases.cs        # Reservation aus Lease anlegen
│   ├── MainForm.Events.cs        # Events-Tab
│   ├── MainForm.Logging.cs       # Bereich replizieren + EventLog auf beiden Servern
│   ├── AppEnvironment.cs         # Umgebungserkennung
│   ├── AppConstants.cs           # Konstanten (EventLog-Quelle)
│   └── AppConfig.cs              # Konfigurationsverwaltung
│
├── 📁 DHCP Management
│   ├── DhcpManager.Failover.cs   # Bereich replizieren (Failover)
│   ├── DhcpManager.cs            # Abfragen (Scopes, Reservations, Leases)
│   ├── DhcpManager.Reservations.cs # Anlegen/Ändern/Löschen von Reservations
│   ├── DhcpDiscovery.cs          # Server-Erkennung im AD inkl. Erreichbarkeitsprüfung
│   ├── InternetRangesDialog.cs   # Einstellung Internet-Bereiche
│   └── IpRanges.cs               # Scope-Adressraum, Internetfreigabe
│
├── 📁 PowerShell Integration
│   └── PowerShellExecutor.cs     # Ausführung: RSAT lokal, sonst WinRM (wiederverwendete Verbindungen)
│
├── 📁 UI Components
│   ├── Theme.cs                  # Farben und Styling (Windows-11-Look)
│   ├── *Dialog.cs                # Verschiedene Dialoge
│   ├── IpPickerForm.cs           # Auswahl freier IPs (mit Ping-/DNS-Status)
│   ├── TerminalPreview.cs        # Befehlsvorschau (Debug-Modus)
│   ├── WaitDialog.cs             # Fortschrittsanzeige
│   └── CredentialDialog.cs       # Anmeldedaten-Dialog
│
├── 📁 Utilities
│   ├── NetworkHelper.cs          # IPv4-Funktionen
│   ├── CsvExport.cs              # CSV-Export
│   └── Helpers.cs                # Script-Erzeugung, Debug-Log
│
└── 📁 Security & Admin
    └── AdminRightsChecker.cs     # Admin-Rechte-Prüfung
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
git clone https://github.com/thhering1969/dhcp-wmiviewer.git
cd dhcp-wmiviewer

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

Bei jedem Versions-Tag (`v*`) baut der Workflow [.github/workflows/release.yml](.github/workflows/release.yml)
die Portable-EXE und die ZIP-Version und veröffentlicht sie als GitHub-Release:

```bash
git tag v1.1.0
git push origin v1.1.0
```

Lokal manuell erstellen:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o DhcpWmiViewer-Portable-Files
Compress-Archive -Path "DhcpWmiViewer-Portable-Files\*" -DestinationPath "DhcpWmiViewer-Portable.zip"
```

### Verteilung

- **GitHub Releases**: Automatische Erstellung von Release-Artefakten (siehe oben)
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

- **Issues**: [GitHub Issues](https://github.com/thhering1969/dhcp-wmiviewer/issues)
- **Discussions**: [GitHub Discussions](https://github.com/thhering1969/dhcp-wmiviewer/discussions)

---

**⭐ Wenn Ihnen dieses Projekt gefällt, geben Sie ihm einen Stern auf GitHub!**

*Entwickelt mit ❤️ für Windows-Administratoren*