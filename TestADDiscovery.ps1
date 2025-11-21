# Test-Skript für AD Discovery Funktionalität

Write-Host "Testing AD Discovery functionality..." -ForegroundColor Green

# Test 1: Domain Controller Discovery
Write-Host "`n1. Testing Domain Controller Discovery:" -ForegroundColor Yellow
try {
    $dcs = Get-ADDomainController -Filter * | Select-Object Name, HostName, Site, OperatingSystem
    if ($dcs) {
        Write-Host "Found Domain Controllers:" -ForegroundColor Green
        $dcs | Format-Table -AutoSize
    } else {
        Write-Host "No Domain Controllers found" -ForegroundColor Red
    }
} catch {
    Write-Host "Error discovering DCs: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 2: OU Discovery with Computer Objects
Write-Host "`n2. Testing OU Discovery with Computer Objects:" -ForegroundColor Yellow
try {
    $ous = Get-ADOrganizationalUnit -Filter * | Sort-Object DistinguishedName
    $ousWithComputers = @()
    
    foreach ($ou in $ous) {
        $computers = Get-ADComputer -SearchBase $ou.DistinguishedName -SearchScope OneLevel -Filter * -ErrorAction SilentlyContinue
        if ($computers) {
            $computerCount = ($computers | Measure-Object).Count
            $ousWithComputers += [PSCustomObject]@{
                Name = $ou.Name
                DistinguishedName = $ou.DistinguishedName
                ComputerCount = $computerCount
            }
        }
    }
    
    if ($ousWithComputers) {
        Write-Host "Found OUs with Computer Objects:" -ForegroundColor Green
        $ousWithComputers | Format-Table -AutoSize
    } else {
        Write-Host "No OUs with computer objects found" -ForegroundColor Red
    }
} catch {
    Write-Host "Error discovering OUs: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3: Check if running on DC
Write-Host "`n3. Testing DC Service Detection:" -ForegroundColor Yellow
try {
    $ntdsService = Get-Service -Name "NTDS" -ErrorAction SilentlyContinue
    if ($ntdsService) {
        Write-Host "NTDS Service found: $($ntdsService.Status)" -ForegroundColor Green
        if ($ntdsService.Status -eq "Running") {
            Write-Host "This machine appears to be a Domain Controller" -ForegroundColor Green
        } else {
            Write-Host "NTDS service is not running" -ForegroundColor Yellow
        }
    } else {
        Write-Host "NTDS service not found - this is not a Domain Controller" -ForegroundColor Yellow
    }
} catch {
    Write-Host "Error checking DC service: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 4: Test WinRM connectivity to a DC
Write-Host "`n4. Testing WinRM connectivity:" -ForegroundColor Yellow
try {
    $localDC = Get-ADDomainController -Discover -Service PrimaryDC
    if ($localDC) {
        $dcHostName = $localDC.HostName[0].ToString()  # Convert from collection to string
        Write-Host "Testing WinRM to: $dcHostName" -ForegroundColor Cyan
        $result = Test-WSMan -ComputerName $dcHostName -ErrorAction SilentlyContinue
        if ($result) {
            Write-Host "WinRM connection successful" -ForegroundColor Green
        } else {
            Write-Host "WinRM connection failed" -ForegroundColor Red
        }
    }
} catch {
    Write-Host "Error testing WinRM: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nAD Discovery test completed." -ForegroundColor Green