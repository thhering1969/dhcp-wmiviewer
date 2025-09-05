# Standard-Computer-OU Ermittlung

## 🎯 **Überblick**

Diese Funktionalität ermittelt die Standard-Organizational Unit (OU) oder den Container, in den neue Computer-Objekte standardmäßig aufgenommen werden, wenn sie einer Active Directory-Domäne beitreten.

## 🔍 **Warum ist das wichtig?**

In verschiedenen AD-Umgebungen kann die Standard-Computer-OU unterschiedlich konfiguriert sein:

- **Standard-Installation**: `CN=Computers,DC=domain,DC=com`
- **Small Business Server (SBS)**: Oft angepasste OUs wie `OU=SBSComputers,DC=domain,DC=local`
- **Enterprise-Umgebungen**: Meist angepasste OUs wie `OU=Workstations,DC=domain,DC=com`

## 🛠 **Implementierte Methoden**

### **1. DirectoryServices-basierte Methode**

```csharp
// Einfache Ermittlung der Standard-OU
string defaultOU = ADDiscovery.GetDefaultComputerOU(domainController);

// Detaillierte Informationen
DefaultComputerOUInfo ouInfo = ADDiscovery.GetDefaultComputerOUInfo(domainController);
```

**Funktionsweise:**
- Verbindet sich zur Domänen-Root über LDAP
- Liest das `wellKnownObjects` Attribut der Domäne
- Sucht nach der Computer-Container GUID: `AA312825768811D1ADED00C04FD8D5CD`
- Fallback auf Standard-Container `CN=Computers`

### **2. PowerShell-basierte Methode**

```csharp
// Asynchrone PowerShell-Abfrage
var results = await ADPowerShellExecutor.GetDefaultComputerOUAsync(domainController, getCredentials);
```

**Funktionsweise:**
- Verwendet `Get-ADDomain` PowerShell-Cmdlet
- Durchsucht `wellKnownObjects` nach Computer-Container
- Lädt zusätzliche Informationen über `Get-ADOrganizationalUnit` oder `Get-ADObject`
- Zählt vorhandene Computer-Objekte

## 📊 **Rückgabe-Informationen**

Die `DefaultComputerOUInfo` Klasse enthält:

```csharp
public class DefaultComputerOUInfo
{
    public string Name { get; set; }                    // z.B. "Computers"
    public string DistinguishedName { get; set; }       // z.B. "CN=Computers,DC=domain,DC=com"
    public string Type { get; set; }                    // "Container" oder "OrganizationalUnit"
    public string Description { get; set; }             // Beschreibung (falls vorhanden)
    public string ManagedBy { get; set; }               // Verwaltet von (falls konfiguriert)
    public int ComputerCount { get; set; }              // Anzahl Computer in der OU
    public bool IsConfigured { get; set; }              // Erfolgreich ermittelt
    public string ErrorMessage { get; set; }            // Fehlermeldung (falls Fehler)
}
```

## 🔧 **Verwendungsbeispiele**

### **Beispiel 1: Einfache Abfrage**

```csharp
try
{
    var defaultOU = ADDiscovery.GetDefaultComputerOU();
    Console.WriteLine($"Standard-Computer-OU: {defaultOU}");
}
catch (Exception ex)
{
    Console.WriteLine($"Fehler: {ex.Message}");
}
```

### **Beispiel 2: Detaillierte Informationen**

```csharp
var ouInfo = ADDiscovery.GetDefaultComputerOUInfo("dc1.domain.com");

if (ouInfo.IsConfigured && !ouInfo.HasError)
{
    Console.WriteLine($"Name: {ouInfo.Name}");
    Console.WriteLine($"Typ: {ouInfo.Type}");
    Console.WriteLine($"DN: {ouInfo.DistinguishedName}");
    Console.WriteLine($"Computer-Anzahl: {ouInfo.ComputerCount}");
    
    if (ouInfo.IsContainer)
        Console.WriteLine("Dies ist ein Container (nicht OU)");
    else if (ouInfo.IsOrganizationalUnit)
        Console.WriteLine("Dies ist eine Organizational Unit");
}
else
{
    Console.WriteLine($"Fehler: {ouInfo.ErrorMessage}");
}
```

### **Beispiel 3: PowerShell-Methode**

```csharp
try
{
    var results = await ADPowerShellExecutor.GetDefaultComputerOUAsync("dc1.domain.com");
    
    if (results.Count > 0)
    {
        var result = results[0];
        var name = result.Properties["Name"]?.Value?.ToString();
        var dn = result.Properties["DistinguishedName"]?.Value?.ToString();
        var type = result.Properties["Type"]?.Value?.ToString();
        var computerCount = result.Properties["ComputerCount"]?.Value?.ToString();
        
        Console.WriteLine($"Standard-OU: {name} ({type})");
        Console.WriteLine($"DN: {dn}");
        Console.WriteLine($"Computer: {computerCount}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"PowerShell-Fehler: {ex.Message}");
}
```

## 🧪 **Test-Funktionalität**

### **PowerShell-Test-Skript**

```powershell
# Führe das Test-Skript aus
.\test-default-computer-ou.ps1
```

### **C#-Test-Klasse**

```csharp
// Führe den Test aus
await TestDefaultComputerOU.RunTest("dc1.domain.com");

// Zeige Verwendungshinweise
TestDefaultComputerOU.ShowUsage();
```

## 🔍 **Technische Details**

### **wellKnownObjects Attribut**

Das `wellKnownObjects` Attribut der Domäne enthält GUIDs für bekannte Container:

- **Computer-Container**: `AA312825768811D1ADED00C04FD8D5CD`
- **Users-Container**: `A9D1CA15768811D1ADED00C04FD8D5CD`
- **Domain Controllers**: `A361B2FFFFD211D1AA4B00C04FD7D83A`

Format: `B:32:GUID:DistinguishedName`

### **Unterschied Container vs. OU**

- **Container (CN=)**: Standard-Container, weniger Verwaltungsoptionen
- **Organizational Unit (OU=)**: Vollwertige OU mit GPO-Verknüpfung, Delegation, etc.

### **SBS-spezifische Konfiguration**

Small Business Server konfiguriert oft eine angepasste OU:
- Verwendet `redircmp.exe` Tool zur Umleitung
- Ändert das `wellKnownObjects` Attribut entsprechend
- Neue Computer landen dann in der angepassten OU

## 🚀 **Integration in die Hauptanwendung**

Die Funktionalität kann in verschiedenen Bereichen der Anwendung verwendet werden:

1. **AD-Discovery**: Anzeige der Standard-OU in der Baumansicht
2. **Computer-Management**: Information, wo neue Computer landen würden
3. **Reporting**: Übersicht über OU-Konfiguration
4. **Troubleshooting**: Diagnose von Computer-Aufnahme-Problemen

## ⚠ **Wichtige Hinweise**

- **Berechtigungen**: Erfordert Lesezugriff auf die Domänen-Root
- **Netzwerk**: Funktioniert sowohl lokal (auf DCs) als auch remote
- **Fehlerbehandlung**: Alle Methoden haben robuste Fehlerbehandlung
- **Performance**: DirectoryServices-Methode ist schneller, PowerShell-Methode detaillierter

## 📝 **Changelog**

- **v1.0**: Initiale Implementierung mit DirectoryServices und PowerShell-Methoden
- Unterstützung für Container und OUs
- Detaillierte Informationsabfrage
- Test-Funktionalität und Dokumentation