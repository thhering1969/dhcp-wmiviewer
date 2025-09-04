# Debug-Analyse Anweisungen für AD-Hierarchie Problem

## Schritt 1: Anwendung starten und AD-Struktur laden

1. Starten Sie die kompilierte Anwendung
2. Wählen Sie einen Domain Controller aus der Dropdown-Liste
3. Klicken Sie auf "Load AD Structure" oder wechseln Sie zum "Active Directory" Tab

## Schritt 2: Analyse-Dateien finden

Nach dem Laden der AD-Struktur werden automatisch zwei Analyse-Dateien erstellt:

### Datei 1: PowerShell-Analyse
**Pfad:** `%TEMP%\DhcpWmiViewer-PowerShell-Analysis.log`
**Vollständiger Pfad:** `C:\Users\[IhrBenutzername]\AppData\Local\Temp\DhcpWmiViewer-PowerShell-Analysis.log`

### Datei 2: C#-Analyse  
**Pfad:** `%TEMP%\DhcpWmiViewer-AD-Analysis.log`
**Vollständiger Pfad:** `C:\Users\[IhrBenutzername]\AppData\Local\Temp\DhcpWmiViewer-AD-Analysis.log`

## Schritt 3: Dateien öffnen und Inhalt kopieren

1. Öffnen Sie den Windows Explorer
2. Geben Sie in die Adressleiste ein: `%TEMP%`
3. Suchen Sie nach den beiden Dateien:
   - `DhcpWmiViewer-PowerShell-Analysis.log`
   - `DhcpWmiViewer-AD-Analysis.log`
4. Öffnen Sie beide Dateien mit Notepad
5. Kopieren Sie den gesamten Inhalt beider Dateien

## Schritt 4: Analyse-Inhalte bereitstellen

Teilen Sie mir den Inhalt beider Dateien mit. Die Dateien enthalten:

### PowerShell-Analyse:
- Alle gefundenen Computer-OUs mit Anzahl der Computer
- Alle gesammelten Parent-DNs für die Hierarchie
- Finale Ergebnis-Items mit Details

### C#-Analyse:
- Alle Items in Verarbeitungsreihenfolge
- Hierarchie-Analyse mit Parent-Child-Beziehungen
- Root-Items-Erkennung
- Parent-Child-Mapping

## Schritt 5: Status-Anzeige beachten

In der Anwendung sollte in der Status-Leiste unten stehen:
`AD Analysis written to: C:\Users\...\Temp\DhcpWmiViewer-AD-Analysis.log`

## Was die Analyse zeigt:

### Erwartete Root-Items:
Die Analyse sollte zeigen, welche OUs als "ROOT ITEM" erkannt werden. Diese sollten in der TreeView als oberste Knoten erscheinen.

### Mögliche Probleme:
1. **Keine Root-Items gefunden** → Alle OUs haben Parent-DNs, die nicht erkannt werden
2. **Falsche Parent-Child-Zuordnung** → OUs werden nicht korrekt hierarchisch zugeordnet
3. **Fehlende Parent-OUs** → Übergeordnete OUs werden nicht vom PowerShell-Skript geladen

## Beispiel für erwarteten Inhalt:

```
=== HIERARCHY ANALYSIS ===
OU/Container Items: 5
ROOT ITEM: WSUS_GoeVB_Server (DN: OU=WSUS_GoeVB_Server,DC=vmdc3,DC=goevb,DC=de)
ROOT ITEM: WSUS_GoeVB_Workstation (DN: OU=WSUS_GoeVB_Workstation,DC=vmdc3,DC=goevb,DC=de)
Root Items Found: 2
```

## Nächste Schritte:

Nach der Analyse kann ich:
1. Das PowerShell-Skript anpassen, um fehlende Parent-OUs zu laden
2. Die C#-Logik für Root-Erkennung verbessern
3. Die Hierarchie-Aufbau-Logik korrigieren

**Bitte teilen Sie mir den Inhalt beider Analyse-Dateien mit, damit ich das Problem genau identifizieren kann.**