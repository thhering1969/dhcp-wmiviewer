// ADPowerShellExecutor.cs
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace DhcpWmiViewer
{
    /// <summary>
    /// PowerShell-Executor speziell für Active Directory-Operationen.
    /// Analog zu PowerShellExecutor, aber für AD-Module und DC-Verbindungen.
    /// </summary>
    public static class ADPowerShellExecutor
    {
        /// <summary>
        /// Prüft, ob die lokale Maschine ein Domain Controller ist.
        /// </summary>
        private static bool IsLocalDomainController()
        {
            return ADDiscovery.CheckLocalDomainControllerServiceRunning();
        }

        /// <summary>
        /// Führt ein PowerShell-Script für AD-Operationen asynchron aus.
        /// Führt lokal nur aus, wenn die App auf einem DC läuft.
        /// Ansonsten wird per Invoke-Command (WinRM) gegen den Ziel-DC gearbeitet.
        /// </summary>
        public static Task<Collection<PSObject>> InvokeADScriptAsync(string domainController, string script, Func<string, PSCredential?>? getCredentials = null)
        {
            return Task.Run(() =>
            {
                // Normalisiere DC-Parameter
                var requested = string.IsNullOrWhiteSpace(domainController) ? "." : domainController.Trim();
                // Zielhost für Invoke-Command: wenn "." angegeben, verwende Environment.MachineName
                var targetHost = requested == "." ? Environment.MachineName : requested;

                // Prüfe, ob wir lokal auf einem DC laufen; dann darf lokal ausgeführt werden.
                var localIsDC = IsLocalDomainController();

                // Wrapper-Script: Importiere ActiveDirectory-Module
                var wrapperScript = "Import-Module ActiveDirectory -ErrorAction Stop;" + Environment.NewLine + script;

                // Wenn wir lokal auf einem DC sind UND Ziel ist die lokale Maschine -> lokal ausführen
                if (localIsDC &&
                    (requested == "." ||
                     targetHost.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase) ||
                     targetHost.Equals("localhost", StringComparison.OrdinalIgnoreCase)))
                {
                    using (var psLocal = PowerShellInitializer.CreatePowerShell())
                    {
                        psLocal.AddScript(wrapperScript);
                        var localResults = psLocal.Invoke();

                        if (psLocal.HadErrors)
                        {
                            var errs = psLocal.Streams.Error?.Select(e => e.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray() ?? Array.Empty<string>();
                            throw new InvalidOperationException("Local PowerShell errors: " + string.Join(" | ", errs));
                        }

                        return localResults ?? new Collection<PSObject>();
                    }
                }

                // Sonst: Immer remote via Invoke-Command (WinRM). Versuch zuerst ohne Credentials,
                // bei Auth/WinRM-Fehlern einmal mit getCredentials retryen.

                string remoteWrapped = wrapperScript;

                static string[] CollectErrorMessages(PowerShell ps)
                {
                    try
                    {
                        return ps.Streams.Error?.Select(er => er.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray() ?? Array.Empty<string>();
                    }
                    catch
                    {
                        return Array.Empty<string>();
                    }
                }

                // Helper: Do a remote invoke with optional credential, and throw on ps.HadErrors
                Collection<PSObject> DoRemoteInvoke(PSCredential? cred)
                {
                    using (var ps = PowerShellInitializer.CreatePowerShell())
                    {
                        var sb = ScriptBlock.Create(remoteWrapped);
                        ps.AddCommand("Invoke-Command")
                          .AddParameter("ComputerName", targetHost)
                          .AddParameter("ScriptBlock", sb);

                        if (cred != null)
                            ps.AddParameter("Credential", cred);

                        var res = ps.Invoke();
                        if (ps.HadErrors)
                        {
                            var errs = CollectErrorMessages(ps);
                            throw new InvalidOperationException("PowerShell remote errors: " + string.Join(" | ", errs));
                        }
                        return res ?? new Collection<PSObject>();
                    }
                }

                // 1) Versuch ohne Credentials
                Collection<PSObject>? firstResults = null;
                string[] firstErrors = Array.Empty<string>();
                try
                {
                    using (var psTest = PowerShellInitializer.CreatePowerShell())
                    {
                        var sbTest = ScriptBlock.Create(remoteWrapped);
                        psTest.AddCommand("Invoke-Command")
                              .AddParameter("ComputerName", targetHost)
                              .AddParameter("ScriptBlock", sbTest);

                        firstResults = psTest.Invoke();
                        firstErrors = CollectErrorMessages(psTest);

                        if (!psTest.HadErrors)
                            return firstResults ?? new Collection<PSObject>();
                    }
                }
                catch (Exception exFirst)
                {
                    // Sammle Exception-Info für späteren Fallback
                    firstErrors = firstErrors.Concat(new[] { exFirst.Message }).ToArray();
                }

                // 2) Falls Fehler und getCredentials verfügbar: Retry mit Credentials
                if (getCredentials != null)
                {
                    try
                    {
                        var cred = getCredentials(targetHost);
                        if (cred != null)
                        {
                            return DoRemoteInvoke(cred);
                        }
                    }
                    catch (Exception exCred)
                    {
                        // Credential-Versuch fehlgeschlagen, werfe ursprünglichen Fehler
                        var allErrors = firstErrors.Concat(new[] { "Credential retry failed: " + exCred.Message }).ToArray();
                        throw new InvalidOperationException("AD PowerShell execution failed: " + string.Join(" | ", allErrors));
                    }
                }

                // 3) Kein Credential-Callback oder Credential-Versuch fehlgeschlagen
                throw new InvalidOperationException("AD PowerShell execution failed: " + string.Join(" | ", firstErrors));
            });
        }

        /// <summary>
        /// Lädt die AD-Struktur (OUs mit Computerobjekten) über PowerShell.
        /// </summary>
        public static async Task<Collection<PSObject>> LoadADStructureAsync(string domainController, Func<string, PSCredential?>? getCredentials = null)
        {
            var script = @"
# Lade alle OUs mit Computerobjekten
$ous = Get-ADOrganizationalUnit -Filter * -Properties Description | Sort-Object DistinguishedName

$result = @()
foreach ($ou in $ous) {
    # Prüfe, ob die OU Computerobjekte enthält
    $computers = Get-ADComputer -SearchBase $ou.DistinguishedName -SearchScope OneLevel -Filter * -ErrorAction SilentlyContinue
    if ($computers) {
        $computerCount = ($computers | Measure-Object).Count
        $result += [PSCustomObject]@{
            Name = $ou.Name
            DistinguishedName = $ou.DistinguishedName
            Description = if ($ou.Description) { $ou.Description } else { '' }
            ComputerCount = $computerCount
            Path = $ou.DistinguishedName -replace '^[^,]+,', ''
        }
    }
}

$result
";

            return await InvokeADScriptAsync(domainController, script, getCredentials);
        }

        /// <summary>
        /// Lädt die AD-Struktur mit nur den OUs, die Computer-Objekte enthalten, für die Baumansicht.
        /// </summary>
        public static async Task<Collection<PSObject>> LoadADTreeStructureAsync(string domainController, Func<string, PSCredential?>? getCredentials = null)
        {
            var script = @"
# Lade alle Computer zuerst
$allComputers = Get-ADComputer -Filter * -Properties Description, OperatingSystem, LastLogonDate, Enabled | Sort-Object Name

# Sammle alle OUs, die Computer enthalten
$computerOUs = @{}
$result = @()

# Debug: Zeige erste Computer (nur erste 3)
Write-Host ""DEBUG: First few computers:""
$allComputers | Select-Object -First 3 | ForEach-Object { Write-Host ""  $($_.Name): $($_.DistinguishedName)"" }

foreach ($computer in $allComputers) {
    # Extrahiere Parent-OU aus Computer DN
    $parentDN = $computer.DistinguishedName -replace '^CN=[^,]+,', ''
    
    # Write-Host ""DEBUG: Computer $($computer.Name) -> Parent: $parentDN""
    
    # Füge Computer zum Ergebnis hinzu
    $result += [PSCustomObject]@{
        Type = 'Computer'
        Name = $computer.Name
        DistinguishedName = $computer.DistinguishedName
        Description = if ($computer.Description) { $computer.Description } else { '' }
        ParentDN = $parentDN
        ComputerCount = 0
        Enabled = $computer.Enabled
        OperatingSystem = if ($computer.OperatingSystem) { $computer.OperatingSystem } else { '' }
        LastLogonDate = if ($computer.LastLogonDate) { $computer.LastLogonDate.ToString('yyyy-MM-dd HH:mm') } else { '' }
    }
    
    # Merke OU als Computer-enthaltend
    if (-not $computerOUs.ContainsKey($parentDN)) {
        $computerOUs[$parentDN] = 0
    }
    $computerOUs[$parentDN]++
}

Write-Host ""DEBUG: Computer OUs found ($($computerOUs.Keys.Count)):""
$computerOUs.Keys | Sort-Object | ForEach-Object { Write-Host ""  $_ ($($computerOUs[$_]) computers)"" }

# Lade nur die OUs, die Computer enthalten
Write-Host ""DEBUG: Loading OUs for computer containers...""
foreach ($ouDN in $computerOUs.Keys) {
    Write-Host ""DEBUG: Trying to load OU: $ouDN""
    try {
        $ou = Get-ADOrganizationalUnit -Identity $ouDN -Properties Description -ErrorAction SilentlyContinue
        if ($ou) {
            Write-Host ""DEBUG: Found OU: $($ou.Name)""
            $result += [PSCustomObject]@{
                Type = 'OU'
                Name = $ou.Name
                DistinguishedName = $ou.DistinguishedName
                Description = if ($ou.Description) { $ou.Description } else { '' }
                ParentDN = ($ou.DistinguishedName -replace '^[^,]+,', '')
                ComputerCount = $computerOUs[$ouDN]
                Enabled = $true
                OperatingSystem = ''
                LastLogonDate = ''
            }
        } else {
            Write-Host ""DEBUG: Not an OU, trying as container: $ouDN""
            # Erstelle Container-Eintrag
            $containerName = if ($ouDN -match '^(OU|CN)=([^,]+)') { $matches[2] } else { 'Unknown' }
            Write-Host ""DEBUG: Creating container entry: $containerName""
            $result += [PSCustomObject]@{
                Type = 'Container'
                Name = $containerName
                DistinguishedName = $ouDN
                Description = 'Container (not OU)'
                ParentDN = ($ouDN -replace '^[^,]+,', '')
                ComputerCount = $computerOUs[$ouDN]
                Enabled = $true
                OperatingSystem = ''
                LastLogonDate = ''
            }
        }
    } catch {
        # OU konnte nicht geladen werden (möglicherweise Container statt OU)
        # Erstelle einen generischen Eintrag
        $ouName = if ($ouDN -match '^OU=([^,]+)') { $matches[1] } else { $ouDN }
        Write-Host ""DEBUG: Exception creating container: $ouName""
        $result += [PSCustomObject]@{
            Type = 'Container'
            Name = $ouName
            DistinguishedName = $ouDN
            Description = 'Container (exception)'
            ParentDN = ($ouDN -replace '^[^,]+,', '')
            ComputerCount = $computerOUs[$ouDN]
            Enabled = $true
            OperatingSystem = ''
            LastLogonDate = ''
        }
    }
}

Write-Host ""DEBUG: Result summary before hierarchy:""
$result | Group-Object Type | ForEach-Object { Write-Host ""  $($_.Name): $($_.Count)"" }

# Schreibe detaillierte Analyse in Datei
try {
    $analysisPath = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), 'DhcpWmiViewer-PowerShell-Analysis.log')
    $analysisContent = @()
    $analysisContent += ""=== POWERSHELL AD ANALYSIS - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') ===""
    $analysisContent += ""Computer OUs found: $($computerOUs.Keys.Count)""
    $analysisContent += """"
    foreach ($ouDN in ($computerOUs.Keys | Sort-Object)) {
        $analysisContent += ""Computer OU: $ouDN ($($computerOUs[$ouDN]) computers)""
    }
    $analysisContent += """"
    $analysisContent += ""Parent DNs collected: $($uniqueParentDNs.Count)""
    $analysisContent += """"
    foreach ($parentDN in $uniqueParentDNs) {
        $analysisContent += ""Parent DN: $parentDN""
    }
    $analysisContent += """"
    $analysisContent += ""=== FINAL RESULT ITEMS ===""
    $analysisContent += ""Total items: $($result.Count)""
    $analysisContent += """"
    foreach ($item in ($result | Sort-Object Type, DistinguishedName)) {
        $analysisContent += ""$($item.Type): $($item.Name)""
        $analysisContent += ""  DN: $($item.DistinguishedName)""
        $analysisContent += ""  Parent: $($item.ParentDN)""
        if ($item.Type -eq 'Computer') {
            $analysisContent += ""  OS: $($item.OperatingSystem)""
            $analysisContent += ""  Enabled: $($item.Enabled)""
        } else {
            $analysisContent += ""  ComputerCount: $($item.ComputerCount)""
        }
        $analysisContent += """"
    }
    $analysisContent | Out-File -FilePath $analysisPath -Encoding UTF8
    Write-Host ""DEBUG: PowerShell Analysis written to: $analysisPath""
} catch {
    Write-Host ""DEBUG: Error writing PowerShell analysis: $($_.Exception.Message)""
}

# Sammle alle Parent-DNs von Computer-OUs für die Hierarchie
$allParentDNs = @()
foreach ($ouDN in $computerOUs.Keys) {
    $currentDN = $ouDN
    # Gehe die gesamte Hierarchie nach oben bis zur Domain-Ebene
    while ($currentDN -match '^(OU|CN)=' -and $currentDN -notmatch '^DC=') {
        $allParentDNs += $currentDN
        $parentDN = $currentDN -replace '^[^,]+,', ''
        $currentDN = $parentDN
        
        # Sicherheitscheck: Verhindere Endlosschleife
        if ($parentDN -eq $currentDN) { break }
    }
}

# Entferne Duplikate und sortiere nach Tiefe (Eltern zuerst)
$uniqueParentDNs = $allParentDNs | Sort-Object -Unique | Sort-Object { ($_ -split ',').Count }

Write-Host ""DEBUG: All Parent DNs collected ($($uniqueParentDNs.Count)):""
$uniqueParentDNs | ForEach-Object { Write-Host ""  $_"" }

# Lade alle Parent-OUs/Container für die Hierarchie
foreach ($parentDN in $uniqueParentDNs) {
    # Überspringe, wenn bereits als Computer-OU geladen
    if ($computerOUs.ContainsKey($parentDN)) {
        continue
    }
    
    # Überspringe, wenn bereits im Ergebnis
    if ($result | Where-Object { $_.DistinguishedName -eq $parentDN }) {
        continue
    }
    
    try {
        # Versuche als OU zu laden
        $ou = Get-ADOrganizationalUnit -Identity $parentDN -Properties Description -ErrorAction SilentlyContinue
        if ($ou) {
            $result += [PSCustomObject]@{
                Type = 'OU'
                Name = $ou.Name
                DistinguishedName = $ou.DistinguishedName
                Description = if ($ou.Description) { $ou.Description } else { '' }
                ParentDN = ($ou.DistinguishedName -replace '^[^,]+,', '')
                ComputerCount = 0
                Enabled = $true
                OperatingSystem = ''
                LastLogonDate = ''
            }
        } else {
            # Falls nicht als OU ladbar, erstelle Container-Eintrag
            $containerName = if ($parentDN -match '^(OU|CN)=([^,]+)') { $matches[2] } else { 'Unknown' }
            $result += [PSCustomObject]@{
                Type = 'Container'
                Name = $containerName
                DistinguishedName = $parentDN
                Description = 'Container (not OU)'
                ParentDN = ($parentDN -replace '^[^,]+,', '')
                ComputerCount = 0
                Enabled = $true
                OperatingSystem = ''
                LastLogonDate = ''
            }
        }
    } catch {
        # Falls Fehler, erstelle generischen Container
        $containerName = if ($parentDN -match '^(OU|CN)=([^,]+)') { $matches[2] } else { 'Unknown' }
        $result += [PSCustomObject]@{
            Type = 'Container'
            Name = $containerName
            DistinguishedName = $parentDN
            Description = 'Container (auto-created)'
            ParentDN = ($parentDN -replace '^[^,]+,', '')
            ComputerCount = 0
            Enabled = $true
            OperatingSystem = ''
            LastLogonDate = ''
        }
    }
}

Write-Host ""DEBUG: Final result summary:""
$result | Group-Object Type | ForEach-Object { Write-Host ""  $($_.Name): $($_.Count)"" }

Write-Host ""DEBUG: Sample results:""
$result | Select-Object -First 5 | ForEach-Object { Write-Host ""  $($_.Type): $($_.Name) (Parent: $($_.ParentDN))"" }

$result
";

            return await InvokeADScriptAsync(domainController, script, getCredentials);
        }

        /// <summary>
        /// Lädt Computerobjekte aus einer bestimmten OU.
        /// </summary>
        public static async Task<Collection<PSObject>> LoadComputersFromOUAsync(string domainController, string ouDistinguishedName, Func<string, PSCredential?>? getCredentials = null)
        {
            var script = $@"
# Lade Computerobjekte aus der angegebenen OU
$computers = Get-ADComputer -SearchBase '{ouDistinguishedName.Replace("'", "''")}' -SearchScope OneLevel -Filter * -Properties Description, OperatingSystem, OperatingSystemVersion, LastLogonDate, Enabled | Sort-Object Name

$result = @()
foreach ($computer in $computers) {{
    $result += [PSCustomObject]@{{
        Name = $computer.Name
        DNSHostName = $computer.DNSHostName
        DistinguishedName = $computer.DistinguishedName
        Description = if ($computer.Description) {{ $computer.Description }} else {{ '' }}
        OperatingSystem = if ($computer.OperatingSystem) {{ $computer.OperatingSystem }} else {{ '' }}
        OperatingSystemVersion = if ($computer.OperatingSystemVersion) {{ $computer.OperatingSystemVersion }} else {{ '' }}
        LastLogonDate = if ($computer.LastLogonDate) {{ $computer.LastLogonDate.ToString('yyyy-MM-dd HH:mm:ss') }} else {{ '' }}
        Enabled = $computer.Enabled
    }}
}}

$result
";

            return await InvokeADScriptAsync(domainController, script, getCredentials);
        }

        /// <summary>
        /// Ermittelt die Standard-Computer-OU über PowerShell.
        /// </summary>
        public static async Task<Collection<PSObject>> GetDefaultComputerOUAsync(string domainController, Func<string, PSCredential?>? getCredentials = null)
        {
            var script = @"
# Ermittle die Standard-Computer-OU der Domäne
try {
    # Methode 1: Über Get-ADDomain und wellKnownObjects
    $domain = Get-ADDomain
    $defaultComputerContainer = $null
    
    # Suche nach dem Computer-Container in wellKnownObjects
    # GUID für Computer-Container: AA312825768811D1ADED00C04FD8D5CD
    foreach ($wellKnownObj in $domain.wellKnownObjects) {
        if ($wellKnownObj -match 'AA312825768811D1ADED00C04FD8D5CD:(.+)$') {
            $defaultComputerContainer = $matches[1]
            break
        }
    }
    
    # Fallback: Standard CN=Computers Container
    if (-not $defaultComputerContainer) {
        $defaultComputerContainer = ""CN=Computers,$($domain.DistinguishedName)""
    }
    
    Write-Host ""DEBUG: Default Computer Container: $defaultComputerContainer""
    
    # Versuche zusätzliche Informationen zu laden
    $containerInfo = $null
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
                $description = if ($containerInfo.Description) { $containerInfo.Description } else { '' }
                $managedBy = if ($containerInfo.ManagedBy) { $containerInfo.ManagedBy } else { '' }
            }
        } else {
            # Es ist ein Container
            $containerInfo = Get-ADObject -Identity $defaultComputerContainer -Properties Description, ManagedBy -ErrorAction SilentlyContinue
            $containerType = 'Container'
            if ($containerInfo) {
                $containerName = $containerInfo.Name
                $description = if ($containerInfo.Description) { $containerInfo.Description } else { '' }
                $managedBy = if ($containerInfo.ManagedBy) { $containerInfo.ManagedBy } else { '' }
            }
        }
        
        # Zähle Computer in der Standard-OU/Container
        $computers = Get-ADComputer -SearchBase $defaultComputerContainer -SearchScope OneLevel -Filter * -ErrorAction SilentlyContinue
        if ($computers) {
            $computerCount = ($computers | Measure-Object).Count
        }
        
    } catch {
        Write-Host ""DEBUG: Error loading container details: $($_.Exception.Message)""
    }
    
    # Extrahiere Name aus DN falls nicht bereits gesetzt
    if ($containerName -eq 'Unknown' -and $defaultComputerContainer -match '^(OU|CN)=([^,]+)') {
        $containerName = $matches[2]
    }
    
    # Erstelle Ergebnis-Objekt
    $result = [PSCustomObject]@{
        Name = $containerName
        DistinguishedName = $defaultComputerContainer
        Type = $containerType
        Description = $description
        ManagedBy = $managedBy
        ComputerCount = $computerCount
        IsConfigured = $true
        ErrorMessage = ''
        DomainName = $domain.DNSRoot
        DomainDN = $domain.DistinguishedName
    }
    
    Write-Host ""DEBUG: Result created - Name: $($result.Name), Type: $($result.Type), Count: $($result.ComputerCount)""
    
    return $result
    
} catch {
    Write-Host ""ERROR: Failed to determine default computer OU: $($_.Exception.Message)""
    
    # Erstelle Fehler-Objekt
    return [PSCustomObject]@{
        Name = ''
        DistinguishedName = ''
        Type = 'Unknown'
        Description = ''
        ManagedBy = ''
        ComputerCount = 0
        IsConfigured = $false
        ErrorMessage = $_.Exception.Message
        DomainName = ''
        DomainDN = ''
    }
}
";

            return await InvokeADScriptAsync(domainController, script, getCredentials);
        }
    }
}