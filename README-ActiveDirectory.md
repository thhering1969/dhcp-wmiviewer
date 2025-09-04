# Active Directory Integration

Die DhcpWmiViewer-Anwendung wurde um Active Directory-Funktionalität erweitert, um die Verwaltung von Computerobjekten in OUs zu ermöglichen.

## Funktionen

### 1. Domain Controller Discovery
- Automatische Erkennung aller Domain Controller in der Domäne
- Unterstützung für lokale und Remote-DCs
- Analog zur DHCP-Server-Discovery

### 2. AD-Struktur-Anzeige
- TreeView mit allen OUs, die Computerobjekte enthalten
- Hierarchische Darstellung der OU-Struktur
- Anzeige der Anzahl von Computerobjekten pro OU

### 3. Computer-Details
- Doppelklick auf OU zeigt alle Computerobjekte
- Details: Name, DNS-Name, Betriebssystem, letzter Logon, Status
- Sortier- und Filterfunktionen

### 4. Remote-Zugriff über WinRM
- Wenn die Anwendung nicht auf einem DC läuft, wird WinRM verwendet
- Automatische Credential-Verwaltung
- Fallback-Mechanismen bei Authentifizierungsfehlern

## Verwendung

### Active Directory Tab
1. **Discover DCs**: Klicken Sie auf "Discover DCs", um verfügbare Domain Controller zu finden
2. **DC auswählen**: Wählen Sie einen DC aus der Dropdown-Liste
3. **Load AD Structure**: Klicken Sie auf "Load AD Structure", um die OU-Hierarchie zu laden
4. **OU erkunden**: 
   - Expandieren/Kollabieren Sie OUs im TreeView
   - Doppelklick auf eine OU zeigt die Computerobjekte
   - Rechtsklick für Kontextmenü mit zusätzlichen Optionen

### Kontextmenü-Optionen
- **Refresh OU**: Aktualisiert die Computer-Anzahl einer OU
- **Show Computers**: Zeigt alle Computerobjekte in einem separaten Dialog
- **Expand All**: Expandiert alle OUs
- **Collapse All**: Kollabiert alle OUs

## Technische Details

### Neue Klassen
- `ADDiscovery`: Discovery-Funktionen für Domain Controller und OUs
- `ADPowerShellExecutor`: PowerShell-Ausführung für AD-Operationen (analog zu `PowerShellExecutor`)
- `ADOrganizationalUnit`: Datenmodell für OU-Informationen

### Erweiterte Klassen
- `AppEnvironment`: Erkennung, ob die Anwendung auf einem DC läuft
- `MainForm.ActiveDirectory.cs`: UI-Logik für das AD-Tab
- `MainForm.Controls.cs`: Neue Controls für AD-Funktionalität
- `MainForm.Layout.cs`: Layout des AD-Tabs

### PowerShell-Module
Die AD-Funktionalität benötigt das `ActiveDirectory`-PowerShell-Modul:
```powershell
Import-Module ActiveDirectory
```

### WinRM-Konfiguration
Für Remote-Zugriff auf DCs muss WinRM konfiguriert sein:
```powershell
# Auf dem Ziel-DC
Enable-PSRemoting -Force
Set-Item WSMan:\localhost\Client\TrustedHosts * -Force
```

## Berechtigungen

### Lokale Ausführung (auf DC)
- Die Anwendung muss mit einem Account ausgeführt werden, der AD-Leserechte hat
- Standardmäßig haben Domain Users Leserechte auf die meisten AD-Objekte

### Remote-Ausführung (über WinRM)
- Der ausführende Account benötigt:
  - Lokale Anmeldung am Ziel-DC (oder Remote-PowerShell-Berechtigung)
  - AD-Leserechte
  - WinRM-Zugriff

### Empfohlene Berechtigungen
- **Domain Users**: Ausreichend für Lese-Operationen
- **Domain Admins**: Für erweiterte Funktionen (falls später implementiert)

## Fehlerbehebung

### Häufige Probleme

1. **"No Domain Controllers found"**
   - Prüfen Sie die Domänen-Mitgliedschaft
   - Stellen Sie sicher, dass DNS korrekt konfiguriert ist
   - Überprüfen Sie die Netzwerkverbindung

2. **"PowerShell remote errors"**
   - Prüfen Sie WinRM-Konfiguration auf dem Ziel-DC
   - Überprüfen Sie Firewall-Einstellungen
   - Stellen Sie sicher, dass der Account Remote-PowerShell-Rechte hat

3. **"Access denied"**
   - Überprüfen Sie AD-Berechtigungen
   - Verwenden Sie einen Account mit ausreichenden Rechten
   - Prüfen Sie, ob der Account gesperrt oder abgelaufen ist

### Debug-Informationen
- Status-Label zeigt aktuelle Operationen und Fehler
- PowerShell-Fehler werden in der Status-Anzeige dargestellt
- Verwenden Sie das Test-Skript `TestADDiscovery.ps1` für Diagnose

## Erweiterungsmöglichkeiten

### Geplante Features
- Computer-Management (Aktivieren/Deaktivieren)
- OU-Management (Erstellen/Löschen/Verschieben)
- Gruppen-Mitgliedschaften anzeigen
- Integration mit DHCP-Reservierungen (Computer → IP-Adresse)

### Anpassungen
- Icons für TreeView-Knoten
- Erweiterte Filter-Optionen
- Export-Funktionen für Computer-Listen
- Bulk-Operationen auf Computerobjekte

## Kompatibilität

### Unterstützte Windows-Versionen
- Windows Server 2016+
- Windows 10/11 (mit RSAT)

### PowerShell-Versionen
- Windows PowerShell 5.1
- PowerShell 7+ (mit Windows Compatibility)

### Active Directory-Versionen
- Windows Server 2012 R2+
- Funktionslevel 2012 R2+