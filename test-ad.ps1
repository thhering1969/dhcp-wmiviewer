# Test AD Structure
Write-Host "=== Testing AD Structure ==="

# Lade erste 3 Computer
$computers = Get-ADComputer -Filter * -Properties Description, OperatingSystem, LastLogonDate, Enabled | Sort-Object Name | Select-Object -First 3

Write-Host "=== First 3 Computers ==="
foreach ($comp in $computers) {
    Write-Host "Computer: $($comp.Name)"
    Write-Host "  DN: $($comp.DistinguishedName)"
    
    # Extrahiere Parent-OU
    $parentDN = $comp.DistinguishedName -replace '^CN=[^,]+,', ''
    Write-Host "  Parent: $parentDN"
    
    # Prüfe ob Parent-OU existiert
    try {
        $parentOU = Get-ADOrganizationalUnit -Identity $parentDN -ErrorAction Stop
        Write-Host "  Parent OU exists: $($parentOU.Name)"
    } catch {
        Write-Host "  Parent is not an OU, trying as container..."
        try {
            $parentContainer = Get-ADObject -Identity $parentDN -ErrorAction Stop
            Write-Host "  Parent Container: $($parentContainer.Name) (Type: $($parentContainer.ObjectClass))"
        } catch {
            Write-Host "  Parent not found: $($_.Exception.Message)"
        }
    }
    Write-Host ""
}

Write-Host "=== All OUs with Computers ==="
$allComputers = Get-ADComputer -Filter * | Select-Object -First 10
$computerOUs = @{}

foreach ($computer in $allComputers) {
    $parentDN = $computer.DistinguishedName -replace '^CN=[^,]+,', ''
    if ($computerOUs.ContainsKey($parentDN)) {
        $computerOUs[$parentDN]++
    } else {
        $computerOUs[$parentDN] = 1
    }
}

foreach ($ouDN in $computerOUs.Keys) {
    Write-Host "OU/Container: $ouDN (Computers: $($computerOUs[$ouDN]))"
    
    try {
        $ou = Get-ADOrganizationalUnit -Identity $ouDN -ErrorAction Stop
        Write-Host "  Type: OU, Name: $($ou.Name)"
    } catch {
        try {
            $container = Get-ADObject -Identity $ouDN -ErrorAction Stop
            Write-Host "  Type: $($container.ObjectClass), Name: $($container.Name)"
        } catch {
            Write-Host "  Type: Unknown, Error: $($_.Exception.Message)"
        }
    }
}