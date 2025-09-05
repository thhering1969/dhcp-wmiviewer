# Test-Skript für die Ermittlung der Standard-Computer-OU
# Dieses Skript zeigt verschiedene Methoden zur Ermittlung der Standard-Computer-OU

Write-Host "=== ERMITTLUNG DER STANDARD-COMPUTER-OU ===" -ForegroundColor Green
Write-Host ""

# Methode 1: Über Get-ADDomain und wellKnownObjects
Write-Host "Methode 1: Über Get-ADDomain und wellKnownObjects" -ForegroundColor Yellow
try {
    Import-Module ActiveDirectory -ErrorAction Stop
    
    $domain = Get-ADDomain
    Write-Host "Domäne: $($domain.DNSRoot)" -ForegroundColor Cyan
    Write-Host "Domänen-DN: $($domain.DistinguishedName)" -ForegroundColor Cyan
    Write-Host ""
    
    $defaultComputerContainer = $null
    
    # Suche nach dem Computer-Container in wellKnownObjects
    # GUID für Computer-Container: AA312825768811D1ADED00C04FD8D5CD
    Write-Host "Durchsuche wellKnownObjects..." -ForegroundColor Gray
    foreach ($wellKnownObj in $domain.wellKnownObjects) {
        Write-Host "  wellKnownObject: $wellKnownObj" -ForegroundColor DarkGray
        if ($wellKnownObj -match 'AA312825768811D1ADED00C04FD8D5CD:(.+)$') {
            $defaultComputerContainer = $matches[1]
            Write-Host "  ✓ Computer-Container gefunden!" -ForegroundColor Green
            break
        }
    }
    
    # Fallback: Standard CN=Computers Container
    if (-not $defaultComputerContainer) {
        $defaultComputerContainer = "CN=Computers,$($domain.DistinguishedName)"
        Write-Host "  ⚠ Fallback auf Standard-Container" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "Standard-Computer-Container: $defaultComputerContainer" -ForegroundColor Green
    
    # Zusätzliche Informationen laden
    $containerType = 'Unknown'
    $containerName = 'Unknown'
    $description = ''
    $managedBy = ''
    $computerCount = 0
    
    try {
        if ($defaultComputerContainer -match '^OU=') {
            # Es ist eine OU
            $containerInfo = Get-ADOrganizationalUnit -Identity $defaultComputerContainer -Properties Description, ManagedBy -ErrorAction SilentlyContinue
            $containerType = 'OrganizationalUnit'
            if ($containerInfo) {
                $containerName = $containerInfo.Name
                $description = if ($containerInfo.Description) { $containerInfo.Description } else { '(keine Beschreibung)' }
                $managedBy = if ($containerInfo.ManagedBy) { $containerInfo.ManagedBy } else { '(nicht verwaltet)' }
            }
        } else {
            # Es ist ein Container
            $containerInfo = Get-ADObject -Identity $defaultComputerContainer -Properties Description, ManagedBy -ErrorAction SilentlyContinue
            $containerType = 'Container'
            if ($containerInfo) {
                $containerName = $containerInfo.Name
                $description = if ($containerInfo.Description) { $containerInfo.Description } else { '(keine Beschreibung)' }
                $managedBy = if ($containerInfo.ManagedBy) { $containerInfo.ManagedBy } else { '(nicht verwaltet)' }
            }
        }
        
        # Zähle Computer in der Standard-OU/Container
        $computers = Get-ADComputer -SearchBase $defaultComputerContainer -SearchScope OneLevel -Filter * -ErrorAction SilentlyContinue
        if ($computers) {
            $computerCount = ($computers | Measure-Object).Count
        }
        
    } catch {
        Write-Host "Fehler beim Laden der Container-Details: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    # Extrahiere Name aus DN falls nicht bereits gesetzt
    if ($containerName -eq 'Unknown' -and $defaultComputerContainer -match '^(OU|CN)=([^,]+)') {
        $containerName = $matches[2]
    }
    
    Write-Host ""
    Write-Host "=== DETAILS DER STANDARD-COMPUTER-OU ===" -ForegroundColor Green
    Write-Host "Name: $containerName" -ForegroundColor White
    Write-Host "Typ: $containerType" -ForegroundColor White
    Write-Host "Distinguished Name: $defaultComputerContainer" -ForegroundColor White
    Write-Host "Beschreibung: $description" -ForegroundColor White
    Write-Host "Verwaltet von: $managedBy" -ForegroundColor White
    Write-Host "Anzahl Computer: $computerCount" -ForegroundColor White
    
} catch {
    Write-Host "Fehler: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== ALTERNATIVE METHODEN ===" -ForegroundColor Green

# Methode 2: Über Registry (nur lokal auf DC)
Write-Host ""
Write-Host "Methode 2: Registry-Abfrage (nur auf Domain Controller)" -ForegroundColor Yellow
try {
    $regPath = "HKLM:\SYSTEM\CurrentControlSet\Services\NTDS\Parameters"
    if (Test-Path $regPath) {
        Write-Host "NTDS Registry-Pfad gefunden - dies ist ein Domain Controller" -ForegroundColor Green
        
        # Weitere Registry-Werte könnten hier abgefragt werden
        $dsa = Get-ItemProperty -Path $regPath -Name "DSA Database file" -ErrorAction SilentlyContinue
        if ($dsa) {
            Write-Host "NTDS Database: $($dsa.'DSA Database file')" -ForegroundColor Cyan
        }
    } else {
        Write-Host "Kein Domain Controller - Registry-Methode nicht verfügbar" -ForegroundColor Gray
    }
} catch {
    Write-Host "Registry-Abfrage fehlgeschlagen: $($_.Exception.Message)" -ForegroundColor Red
}

# Methode 3: Über ADSI (DirectoryEntry)
Write-Host ""
Write-Host "Methode 3: ADSI DirectoryEntry" -ForegroundColor Yellow
try {
    $rootDSE = New-Object System.DirectoryServices.DirectoryEntry("LDAP://RootDSE")
    $defaultNC = $rootDSE.Properties["defaultNamingContext"].Value
    Write-Host "Default Naming Context: $defaultNC" -ForegroundColor Cyan
    
    $domain = New-Object System.DirectoryServices.DirectoryEntry("LDAP://$defaultNC")
    $domain.RefreshCache(@("wellKnownObjects"))
    
    if ($domain.Properties.Contains("wellKnownObjects")) {
        Write-Host "wellKnownObjects gefunden:" -ForegroundColor Cyan
        foreach ($wellKnownObj in $domain.Properties["wellKnownObjects"]) {
            if ($wellKnownObj -match "AA312825768811D1ADED00C04FD8D5CD") {
                Write-Host "  Computer-Container: $wellKnownObj" -ForegroundColor Green
            }
        }
    }
} catch {
    Write-Host "ADSI-Abfrage fehlgeschlagen: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== VERWENDUNG IN SBS UND ANDEREN UMGEBUNGEN ===" -ForegroundColor Green
Write-Host ""
Write-Host "In Small Business Server (SBS) und anderen Umgebungen kann die Standard-Computer-OU" -ForegroundColor White
Write-Host "von der Standard-Konfiguration abweichen:" -ForegroundColor White
Write-Host ""
Write-Host "• Standard: CN=Computers,DC=domain,DC=com" -ForegroundColor Gray
Write-Host "• SBS: Oft OU=SBSComputers,DC=domain,DC=local oder ähnlich" -ForegroundColor Gray
Write-Host "• Enterprise: Oft angepasste OUs wie OU=Workstations,DC=domain,DC=com" -ForegroundColor Gray
Write-Host ""
Write-Host "Die wellKnownObjects-Methode funktioniert in allen Fällen und zeigt die" -ForegroundColor White
Write-Host "tatsächlich konfigurierte Standard-OU an." -ForegroundColor White

Write-Host ""
Write-Host "=== FERTIG ===" -ForegroundColor Green