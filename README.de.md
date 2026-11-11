[English](README.md) | **Deutsch**

# 🌐 DHCP WMI Viewer

[![.NET](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![OS](https://img.shields.io/badge/OS-Windows-blue.svg)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Release](https://img.shields.io/github/v/release/thhering1969/dhcp-wmiviewer)](https://github.com/thhering1969/dhcp-wmiviewer/releases/latest)

Ein Windows-Tool für die tägliche Arbeit mit **Windows-DHCP-Servern**: Scopes, Reservierungen und Leases
anzeigen, Leases in Reservierungen umwandeln, Reservierungen ändern oder löschen – mit Bestätigung vor
jeder Änderung und automatischem **„Bereich replizieren“** zum Failover-Partner.

## Highlights

- **Server-Erkennung aus dem Active Directory** – zeigt nur DHCP-Server, die noch existieren und erreichbar
  sind (Computerkonto aktiv, WinRM-Port erreichbar); abgeschaltete Server werden ausgeblendet
- **Bestätigung vor jeder Änderung** – Tabelle vorher/nachher mit allen Feldern, Änderungen hervorgehoben;
  „Abbrechen“ ist vorausgewählt
- **Automatisches „Bereich replizieren“** – nach jeder Änderung wird der Scope zum Failover-Partner repliziert,
  wie im Kontextmenü der DHCP-Konsole (`Invoke-DhcpServerv4FailoverReplication`); zusätzlich manuell per
  Rechtsklick auf einen Scope
- **Nachvollziehbarkeit** – jede Änderung und jede Replikation steht im Application-EventLog **beider**
  Failover-Partner
- **RSAT zuerst, WinRM als Fallback** – nutzt die lokalen RSAT-DHCP-Tools, sonst PowerShell-Remoting auf dem
  DHCP-Server; der Fenstertitel zeigt den genutzten Weg
- **IP-Auswahl** – freie Adressen des Scopes mit Ping-/DNS-Status; markiert optional Adressen mit
  Internetfreigabe (z. B. von der Firewall vorgegeben)
- **Events aller DHCP-Server** – das Änderungsprotokoll der App, zusammengeführt von allen aktiven Servern
- **Portabel** – eine einzige EXE, keine Installation, kein .NET nötig

## Download

`DhcpWmiViewer-Portable.exe` aus dem [aktuellen Release](https://github.com/thhering1969/dhcp-wmiviewer/releases/latest) herunterladen.

| Datei | Größe | Hinweis |
|---|---|---|
| `DhcpWmiViewer-Portable.exe` ⭐ | ~153 MB | Eine Datei, alles enthalten – einfach starten |
| `DhcpWmiViewer-Portable.zip` | ~64 MB | Dieselbe App als Ordner; entpacken, `DhcpWmiViewer.exe` starten |

Ohne Adminrechte gestartet, bietet die App einen Neustart als Administrator an.

## Voraussetzungen

- Windows 10/11 oder Windows Server, x64, Domänenmitglied (für Discover)
- Ein Konto mit DHCP-Verwaltungsrechten (z. B. Mitglied der *DHCP-Administratoren*)
- Zugriff auf die DHCP-Server über **eines** von beiden:
  - **RSAT-DHCP-Tools** auf dem PC (empfohlen; nötig für „Bereich replizieren“) – die App bietet bei Bedarf
    die Installation an (`Add-WindowsCapability -Online -Name Rsat.DHCP.Tools~~~~0.0.1.0`), oder
  - **WinRM** (PowerShell-Remoting, TCP 5985) auf den DHCP-Servern
- EventLog lesen und schreiben läuft immer über WinRM

## Bedienung

1. App starten, DHCP-Server eintragen oder **Discover** klicken und auswählen; **Query Scopes** (oder Enter)
2. Scope auswählen – Reservierungen und Leases werden in den Tabs darunter geladen
3. Tab **Leases**: Rechtsklick → *Create reservation from lease…* (mit IP-Auswahl)
4. Tab **Reservations**: Rechtsklick → *Ändern…* (Name, Beschreibung, IP) oder *Löschen*;
   Doppelklick öffnet ebenfalls „Ändern“
5. Tab **Events**: Änderungen mit dieser App, von allen aktiven DHCP-Servern
6. Scope-Tabelle: Rechtsklick → *Bereich replizieren…* von Hand
7. **Export CSV** speichert die Scope-Liste

Der zuletzt abgefragte Server wird gemerkt.

### Warum RSAT für „Bereich replizieren“?

Über WinRM scheitert `Invoke-DhcpServerv4FailoverReplication` am Kerberos-*Double-Hop*: Der DHCP-Server darf
Ihre Anmeldung nicht an den Partner weitergeben. Mit den RSAT-Tools läuft der Befehl auf Ihrem PC und spricht
direkt mit beiden Servern – genau wie die DHCP-Konsole.

## Konfiguration

Einstellungen stehen in `%APPDATA%\DhcpWmiViewer\config.txt`:

```ini
EnableAutoRestartAsAdmin=true
ShowAdminWarning=true
# wird nach jeder erfolgreichen Abfrage gesetzt
LastServer=dhcp01.example.com
# optional, siehe unten (leer = aus)
InternetRanges=10.0.0.10-10.0.0.50, 10.0.0.200-10.0.0.254
```

**Internet-Bereiche (optional):** Dürfen nur bestimmte Adressen ins Internet (z. B. von der Firewall
vorgegeben), tragen Sie diese unter *Extras → Internet-Bereiche (Firewall)…* ein. Die IP-Auswahl markiert sie
dann mit 🌐 und kann danach filtern; der Bestätigungsdialog zeigt „mit/ohne Internetfreigabe“.
Ohne Einträge ist die Funktion ausgeblendet.

**Vorgabe für viele PCs:** eine `DhcpWmiViewer.config.txt` (gleiches Format) neben die EXE legen, z. B. auf
einer Netzwerkfreigabe. Die Einstellungen des Benutzers haben Vorrang.

*Extras → PowerShell-Befehle vor Ausführung anzeigen* zeigt jeden Befehl vor der Ausführung (Debug).

## Fehlerbehebung

| Problem | Was prüfen |
|---|---|
| Discover findet keine Server | PC muss Domänenmitglied sein; DHCP-Server müssen im AD autorisiert sein |
| Server wird bei Discover ausgeblendet | Computerkonto deaktiviert/fehlt oder WinRM-Port 5985 nicht erreichbar (VPN?) |
| „Zugriff verweigert“ | Konto braucht DHCP-Verwaltungsrechte; *Clear Credentials* und anderes Konto eingeben |
| „Bereich replizieren“ schlägt fehl | RSAT-DHCP-Tools installieren; der Scope braucht eine Failover-Beziehung |
| RSAT-Installation hängt/scheitert | Windows Update/WSUS muss optionale Features (*Features on Demand*) liefern |

Log-Dateien in `%TEMP%`: `DhcpWmiViewer-error.log`, `DhcpWmiViewer-crash.log`,
`DhcpWmiViewer-eventlog-fallback.log`, `dhcp_debug_log.txt`.

## Selbst bauen

Benötigt das .NET 9 SDK.

```powershell
git clone https://github.com/thhering1969/dhcp-wmiviewer.git
cd dhcp-wmiviewer
dotnet build
# portable Single-File-EXE
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

Jedes Versions-Tag (`v*`) startet [`.github/workflows/release.yml`](.github/workflows/release.yml); der
Workflow baut Portable-EXE und ZIP und veröffentlicht ein GitHub-Release.

### Projektstruktur

| Datei | Zweck |
|---|---|
| `MainForm*.cs` | Hauptfenster: Layout, Scopes, Reservierungen, Leases, Events, Protokoll/Replikation |
| `DhcpManager*.cs` | DHCP-Abfragen und -Änderungen, „Bereich replizieren“, RSAT-Installation |
| `PowerShellExecutor.cs` | Ausführung: zuerst RSAT lokal, sonst WinRM (Verbindungen wiederverwendet) |
| `DhcpDiscovery.cs` | AD-Discovery inkl. Erreichbarkeitsprüfung |
| `ConfirmChangeDialog.cs` | Bestätigung vorher/nachher |
| `IpPickerForm.cs`, `IpRanges.cs` | IP-Auswahl, Scope-Adressraum, Internet-Bereiche |
| `AppConfig.cs`, `InternetRangesDialog.cs` | Einstellungen |
| `Theme.cs`, `WaitDialog.cs` | Aussehen, Fortschrittsanzeige |

## Mitmachen

Issues und Pull Requests sind willkommen – siehe [CONTRIBUTING.md](CONTRIBUTING.md).
Fehler bitte über [GitHub Issues](https://github.com/thhering1969/dhcp-wmiviewer/issues) melden und dabei
keine internen Servernamen oder Adressen angeben.

## Lizenz

[MIT](LICENSE)
