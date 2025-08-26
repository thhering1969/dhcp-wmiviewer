<#
diagnose.ps1
Erzeugt Diagnose-Artefakte für das aktuelle C#-Projekt:
- kopiert Quellcode/Projektdaten in ein timestamped diagnostics-Verzeichnis
- führt `dotnet build` und `dotnet run` (optional) aus und sammelt Outputs
- sucht nach häufigen Problemen (Duplikate bestimmter Methodennamen)
- erzeugt Git-Bundle (falls Repo vorhanden)
- erzeugt eine ZIP-Datei der Diagnose
- gibt am Ende Pfade/Ergebnis-Nachricht aus

Benutzung:
  1. PowerShell öffnen (als Benutzer, nicht zwingend als Admin)
  2. in Projekt-Root wechseln (dort, wo .sln/.csproj + .cs-Dateien liegen)
  3. ./diagnose.ps1
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'

function Write-Info { param($m) Write-Host $m -ForegroundColor Cyan }
function Write-Ok   { param($m) Write-Host $m -ForegroundColor Green }
function Write-Err  { param($m) Write-Host $m -ForegroundColor Red }

try {
    $root = (Get-Location).ProviderPath
    Write-Info "Working directory: $root"

    $ts = Get-Date -Format "yyyyMMddHHmmss"
    $diagBase = Join-Path $root ("diagnostics_$ts")
    New-Item -Path $diagBase -ItemType Directory -Force | Out-Null

    # 1) Copy useful files
    Write-Info "Copying source files to diagnostics folder..."
    $includePatterns = @("*.cs","*.csproj","*.sln","*.ps1","*.config","*.json","README*","*.resx","*.designer.cs")
    foreach ($pat in $includePatterns) {
        try {
            Copy-Item -Path (Join-Path $root $pat) -Destination $diagBase -Recurse -Force -ErrorAction SilentlyContinue
        } catch { }
    }
    # Also copy entire .git folder metadata (not recommended for public upload, but useful locally for bundle)
    if (Test-Path (Join-Path $root ".git")) {
        try { Copy-Item -Path (Join-Path $root ".git") -Destination (Join-Path $diagBase ".git") -Recurse -Force -ErrorAction SilentlyContinue } catch { }
    }

    # 2) Save environment info
    Write-Info "Writing environment and .NET info..."
    $envFile = Join-Path $diagBase "environment.txt"
    @(
        "Timestamp: $ts",
        "Directory: $root",
        "User: $env:USERNAME",
        "OS: $([System.Environment]::OSVersion.VersionString)",
        "PowerShell: $($PSVersionTable.PSVersion)",
        "`nDotnet --info:`n"
    ) | Out-File -FilePath $envFile -Encoding UTF8

    try { dotnet --info 2>&1 | Out-File -FilePath $envFile -Append -Encoding UTF8 } catch { "dotnet not available" | Out-File -FilePath $envFile -Append -Encoding UTF8 }

    # 3) Run dotnet build
    $buildLog = Join-Path $diagBase "dotnet-build.log"
    Write-Info "Running: dotnet build  (output -> $buildLog)"
    try {
        dotnet build 2>&1 | Tee-Object -FilePath $buildLog
    } catch {
        "`nBuild command failed or dotnet not found.`n$_" | Out-File -FilePath $buildLog -Append -Encoding UTF8
    }

    # 4) Try dotnet run (best-effort) — run in background, capture output with timeout
    $runLog = Join-Path $diagBase "dotnet-run.log"
    $runSucceeded = $false
    try {
        Write-Info "Attempting: dotnet run  (this may block if the app expects UI or input). Output -> $runLog"
        # Run for max 12s to collect initial errors (avoid long-running UI blocking)
        $ps = Start-Process -FilePath "dotnet" -ArgumentList "run" -WorkingDirectory $root -RedirectStandardOutput $runLog -RedirectStandardError $runLog -NoNewWindow -PassThru
        $timeoutSeconds = 12
        $sw = [Diagnostics.Stopwatch]::StartNew()
        while (-not $ps.HasExited -and $sw.Elapsed.TotalSeconds -lt $timeoutSeconds) { Start-Sleep -Milliseconds 200 }
        if (-not $ps.HasExited) {
            # still running -> kill (we only wanted early diagnostics)
            try { $ps.Kill(); Write-Info "dotnet run exceeded $timeoutSeconds s; process terminated (partial output saved)." } catch {}
        }
        $runSucceeded = $ps.ExitCode -eq 0
    } catch {
        "dotnet run failed to start: $_" | Out-File -FilePath $runLog -Append -Encoding UTF8
    }

    # 5) Search for duplicate method names that commonly caused trouble (customize list below)
    Write-Info "Searching for suspicious/duplicate method definitions in .cs files..."
    $methodNames = @(
        "EnsureLeasesColumns",
        "AdjustLeasesColumnWidths",
        "FormatServerIpCellsAfterBind",
        "DgvLeases_CellFormatting",
        "ShowLeasesDebugWindow",
        "DgvLeases_CellMouseDown",
        "ContextMenuLeases_Opening",
        "TryGetCellValue",
        "ReadLeaseRowValuesSafe",
        "InvokeOptionalHandlerAsync",
        "OnCreateReservationFromLeaseAsync",
        "OnChangeReservationFromLeaseRowAsync",
        "TryInvokeDhcpManagerBoolMethodAsync"
    )
    $dupReport = Join-Path $diagBase "duplicate-method-search.txt"
    "" | Out-File -FilePath $dupReport -Encoding UTF8
    foreach ($m in $methodNames) {
        $hits = Select-String -Path (Join-Path $root "*.cs") -Pattern ("\<" + [regex]::Escape($m) + "\>") -AllMatches -ErrorAction SilentlyContinue
        if ($hits) {
            "`n=== Method: $m ===" | Out-File -FilePath $dupReport -Append -Encoding UTF8
            foreach ($h in $hits) {
                ("{0}:{1}: {2}" -f $h.Filename, $h.LineNumber, ($h.Line.Trim())) | Out-File -FilePath $dupReport -Append -Encoding UTF8
            }
        }
    }

    # 6) Git info + optional bundle
    $gitBundlePath = $null
    if (Get-Command git -ErrorAction SilentlyContinue) {
        try {
            $isRepo = & git rev-parse --is-inside-work-tree 2>$null
            if ($LASTEXITCODE -eq 0 -and $isRepo -eq "true") {
                Write-Info "Git repo detected. Creating bundle and collecting git status/log..."
                $gitStatus = Join-Path $diagBase "git-status.txt"
                git status --porcelain=v1 --branch 2>&1 | Out-File -FilePath $gitStatus -Encoding UTF8
                git remote -v 2>&1 | Out-File -FilePath (Join-Path $diagBase "git-remote.txt") -Encoding UTF8
                git log -n 200 --pretty=oneline 2>&1 | Out-File -FilePath (Join-Path $diagBase "git-log.txt") -Encoding UTF8
                $bundleName = "repo_bundle_$ts.bundle"
                $gitBundlePath = Join-Path $diagBase $bundleName
                try {
                    git bundle create $gitBundlePath --all 2>&1 | Out-Null
                    Write-Ok "Git bundle created: $gitBundlePath"
                } catch {
                    Write-Err "git bundle failed: $_"
                    $gitBundlePath = $null
                }
            } else {
                Write-Info "No git repository found here."
            }
        } catch {
            Write-Err "Error while inspecting git: $_"
        }
    } else {
        Write-Info "git not found on PATH; skipping git bundle creation."
    }

    # 7) Collect selected log fragments (build/run errors) near the top
    Write-Info "Extracting top error lines from build/run logs (if any)..."
    $summary = Join-Path $diagBase "quick-summary.txt"
    "" | Out-File -FilePath $summary -Encoding UTF8
    Add-Content -Path $summary -Value "Diagnostics created: $ts`n"
    if (Test-Path $buildLog) {
        Add-Content -Path $summary -Value "`n--- dotnet build (last 200 lines) ---"
        Get-Content $buildLog -Tail 200 | Out-File -FilePath $summary -Append -Encoding UTF8
    }
    if (Test-Path $runLog) {
        Add-Content -Path $summary -Value "`n--- dotnet run (last 200 lines) ---"
        Get-Content $runLog -Tail 200 | Out-File -FilePath $summary -Append -Encoding UTF8
    }

    # 8) Create a ZIP of the diagnostics folder
    $zipPath = Join-Path $root ("diagnostics_$ts.zip")
    Write-Info "Creating ZIP: $zipPath"
    try {
        if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
        Compress-Archive -Path (Join-Path $diagBase "*") -DestinationPath $zipPath -Force
        Write-Ok "ZIP created: $zipPath"
    } catch {
        Write-Err "Compress-Archive failed: $_"
    }

    # 9) Final output
    Write-Ok "Diagnostics folder: $diagBase"
    Write-Ok "Quick summary: $summary"
    if ($gitBundlePath) { Write-Ok "Git bundle: $gitBundlePath" }
    Write-Ok "ZIP: $zipPath"

    Write-Host "`nNext steps:"
    Write-Host " - Inspect $buildLog and $runLog for errors."
    Write-Host " - If you want to share, you can upload $zipPath to a temporary public repo or filehost (see instructions)."

} catch {
    Write-Err "Unexpected error in script: $_"
    exit 1
}
