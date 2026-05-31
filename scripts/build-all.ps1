# Build Windows publish + Windows installer + Linux .deb (via WSL) in parallel.
param(
    [switch]$Launch,
    [switch]$NoPause,
    [string]$WslDistro = ""
)

$ErrorActionPreference = "Stop"

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$Scripts = Join-Path $Root "scripts"
$Artifacts = Join-Path $Root "artifacts"
$WinPublish = Join-Path $Artifacts "publish\win-x64"
$WinExe = Join-Path $WinPublish "ioSender.exe"
$WslScriptWin = Join-Path $Scripts "wsl-build-deb.sh"
$WinLog = Join-Path $Artifacts "build-win.log"
$WinInstallerLog = Join-Path $Artifacts "build-win-installer.log"
$WslLog = Join-Path $Artifacts "build-wsl.log"
$WinErr = Join-Path $Artifacts "build-win.err"
$WinInstallerErr = Join-Path $Artifacts "build-win-installer.err"
$WslErr = Join-Path $Artifacts "build-wsl.err"
$ExportDir = Join-Path $env:TEMP "iosender-export"

function Write-Step([string]$Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Ensure-WslInstalled {
    if (-not (Get-Command wsl -ErrorAction SilentlyContinue)) {
        throw "WSL not found. Install WSL + Ubuntu, then re-run."
    }
    $null = wsl --status 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "WSL is installed but not ready. Open Ubuntu once to finish setup."
    }
}

function Get-WslDistroName {
    param([string]$Preferred)

    $distros = @(
        (wsl -l -q 2>$null | ForEach-Object { ($_ -replace '\0', '').Trim() }) |
        Where-Object { $_ -and $_ -notmatch '^\s*$' }
    )
    if ($distros.Count -eq 0) {
        throw "No WSL distributions found. Run: wsl --install -d Ubuntu"
    }

    if ($Preferred -and ($distros -contains $Preferred)) {
        return $Preferred
    }

    if ($Preferred) {
        Write-Warning "Distro '$Preferred' not found; using '$($distros[0])'"
    }

    foreach ($name in @("Ubuntu", "Ubuntu-24.04", "Ubuntu-22.04")) {
        if ($distros -contains $name) { return $name }
    }

    return $distros[0]
}

function Ensure-WslDistroReady {
    param([string]$Distro)

    $output = & wsl.exe -d $Distro --cd ~ -e /bin/bash -lc "printf WSL_READY" 2>&1
    if ($LASTEXITCODE -ne 0 -or (($output -join "`n") -notmatch "WSL_READY")) {
        $message = (($output -join "`n") -replace "`0", "").Trim()
        if ([string]::IsNullOrWhiteSpace($message)) {
            $message = "No output from WSL."
        }
        throw "WSL distro '$Distro' could not start (exit $LASTEXITCODE). Try 'wsl --shutdown' or reboot Windows, then re-run. Details: $message"
    }
}

function Convert-ToWslPath([string]$WindowsPath) {
    $full = [System.IO.Path]::GetFullPath($WindowsPath)
    if ($full -match '^([A-Za-z]):\\(.*)$') {
        $drive = $Matches[1].ToLowerInvariant()
        $rest = $Matches[2] -replace '\\', '/'
        return "/mnt/$drive/$rest"
    }
    throw "Cannot map '$WindowsPath' for WSL."
}

function Invoke-WslBash {
    param(
        [string]$Distro,
        [string]$Script
    )
    & wsl.exe -d $Distro --cd ~ -- bash -lc "$Script"
    if ($LASTEXITCODE -ne 0) {
        throw "wsl bash failed (exit $LASTEXITCODE)"
    }
}

function Escape-BashSingleQuoted([string]$Value) {
    return ($Value -replace "'", "'\''")
}

function Test-WslDirectory {
    param([string]$Distro, [string]$WslPath)
    & wsl.exe -d $Distro --cd ~ -e test -d $WslPath 2>$null
    return $LASTEXITCODE -eq 0
}

function Sync-SourceToWsl {
    param(
        [string]$Distro,
        [string]$SourceRoot
    )

    $wslSource = Convert-ToWslPath $SourceRoot

    if (-not (Test-WslDirectory -Distro $Distro -WslPath $wslSource)) {
        Write-Host "  $wslSource not visible in WSL (network drive?). Staging via robocopy to TEMP..."
        $staging = Join-Path $env:TEMP "iosender-src-$PID"
        if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $staging | Out-Null

        $robolog = Join-Path $Artifacts "robocopy-staging.log"
        $rc = robocopy $SourceRoot $staging /MIR `
            /XD bin obj .git "artifacts\publish" packaging\debian-staging `
            /NFL /NDL /NJH /NJS /NP `
            /LOG:$robolog
        # robocopy: 0-7 = success
        if ($rc -ge 8) {
            throw "robocopy staging failed (exit $rc). See $robolog"
        }

        $wslSource = Convert-ToWslPath $staging
        if (-not (Test-WslDirectory -Distro $Distro -WslPath $wslSource)) {
            throw "Staged copy still not visible in WSL at $wslSource"
        }
        Write-Host "  Staged at $staging"
    }

    Write-Host "  rsync $wslSource/ -> ~/ioSender-build/"
    $src = Escape-BashSingleQuoted ($wslSource.TrimEnd('/') + '/')
    $rsyncScript = "set -eu; mkdir -p ~/ioSender-build; rsync -a --delete --exclude=obj --exclude=bin --exclude=.git --exclude=packaging/debian-staging '$src' ~/ioSender-build/"
    Invoke-WslBash -Distro $Distro -Script $rsyncScript
}

function New-WslBuildScriptPath([string]$SourcePath) {
    $content = [System.IO.File]::ReadAllText($SourcePath) -replace "`r`n", "`n" -replace "`r", "`n"
    $tempWin = Join-Path $env:TEMP "iosender-wsl-build-$PID.sh"
    [System.IO.File]::WriteAllText($tempWin, $content, [System.Text.UTF8Encoding]::new($false))
    return (Convert-ToWslPath $tempWin)
}

Write-Host "ioSender release build" -ForegroundColor Yellow
Write-Host "Repo: $Root"

New-Item -ItemType Directory -Force -Path $Artifacts | Out-Null
New-Item -ItemType Directory -Force -Path $ExportDir | Out-Null
Get-ChildItem $ExportDir -Filter "iosender_*.deb" -ErrorAction SilentlyContinue | Remove-Item -Force
Get-ChildItem $Artifacts -Filter "ioSender-Setup-*-win-x64.exe" -ErrorAction SilentlyContinue | Remove-Item -Force

Ensure-WslInstalled
$distro = Get-WslDistroName -Preferred $WslDistro
Ensure-WslDistroReady -Distro $distro

Write-Step "Syncing source to ~/ioSender-build (rsync)"
Sync-SourceToWsl -Distro $distro -SourceRoot $Root

$wslExport = Convert-ToWslPath $ExportDir
$wslSh = New-WslBuildScriptPath $WslScriptWin
$publishScript = Join-Path $Scripts "publish-windows.ps1"
$installerScript = Join-Path $Scripts "package-windows-installer.ps1"

Write-Step "Starting parallel builds (Windows folder + WSL: $distro)"

$winArgs = "-NoProfile -ExecutionPolicy Bypass -File `"$publishScript`""
$winInstallerArgs = "-NoProfile -ExecutionPolicy Bypass -File `"$installerScript`""
$wslArgs = "-d $distro --cd ~ -e /bin/bash `"$wslSh`" `"$wslExport`""

$winPs = Start-Process -FilePath "powershell.exe" -WorkingDirectory $env:TEMP `
    -ArgumentList $winArgs -PassThru -NoNewWindow `
    -RedirectStandardOutput $WinLog -RedirectStandardError $WinErr

$wslPs = Start-Process -FilePath "wsl.exe" -WorkingDirectory $env:TEMP `
    -ArgumentList $wslArgs -PassThru -NoNewWindow `
    -RedirectStandardOutput $WslLog -RedirectStandardError $WslErr

Write-Host "Windows folder PID $($winPs.Id)  |  WSL PID $($wslPs.Id)"
Write-Host "Waiting for Windows folder publish before starting installer (logs: artifacts\build-win*.log, artifacts\build-wsl.log)..."

$sw = [System.Diagnostics.Stopwatch]::StartNew()
while (-not $winPs.HasExited) {
    $pending = @()
    $pending += "Windows folder"
    if (-not $wslPs.HasExited) { $pending += "WSL" }
    Write-Host ("  [{0}] waiting on: {1}" -f $sw.Elapsed.ToString('mm\:ss'), ($pending -join ', '))
    Start-Sleep -Seconds 3
}

$winPs.WaitForExit()
$winPs.Refresh()

$winInstallerPs = $null
if ($winPs.ExitCode -eq 0 -and (Test-Path $WinExe)) {
    Write-Step "Starting Windows installer build"
    $winInstallerPs = Start-Process -FilePath "powershell.exe" -WorkingDirectory $env:TEMP `
        -ArgumentList $winInstallerArgs -PassThru -NoNewWindow `
        -RedirectStandardOutput $WinInstallerLog -RedirectStandardError $WinInstallerErr
    Write-Host "Windows installer PID $($winInstallerPs.Id)"
} else {
    Write-Host "Skipping Windows installer because the folder publish failed." -ForegroundColor Yellow
}

while ((($null -ne $winInstallerPs) -and -not $winInstallerPs.HasExited) -or -not $wslPs.HasExited) {
    $pending = @()
    if (($null -ne $winInstallerPs) -and -not $winInstallerPs.HasExited) { $pending += "Windows installer" }
    if (-not $wslPs.HasExited) { $pending += "WSL" }
    Write-Host ("  [{0}] waiting on: {1}" -f $sw.Elapsed.ToString('mm\:ss'), ($pending -join ', '))
    Start-Sleep -Seconds 3
}
$sw.Stop()

if ($null -ne $winInstallerPs) {
    $winInstallerPs.WaitForExit()
    $winInstallerPs.Refresh()
}
$wslPs.WaitForExit()
$wslPs.Refresh()

$winInstallerExit = "skipped"
if ($null -ne $winInstallerPs) {
    $winInstallerExit = $winInstallerPs.ExitCode
}

Write-Step "Windows publish finished (exit $($winPs.ExitCode))"
if (Test-Path $WinLog) { Get-Content $WinLog | Write-Host }
if (Test-Path $WinErr) { Get-Content $WinErr | Write-Host }

Write-Step "Windows installer finished (exit $winInstallerExit)"
if (Test-Path $WinInstallerLog) { Get-Content $WinInstallerLog | Write-Host }
if (Test-Path $WinInstallerErr) { Get-Content $WinInstallerErr | Write-Host }

Write-Step "WSL .deb build finished (exit $($wslPs.ExitCode))"
if (Test-Path $WslLog) { Get-Content $WslLog | Write-Host }
if (Test-Path $WslErr) { Get-Content $WslErr | Write-Host }

$debExport = Get-ChildItem $ExportDir -Filter "iosender_*.deb" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($debExport) {
    Copy-Item $debExport.FullName (Join-Path $Artifacts $debExport.Name) -Force
    Write-Host "Copied .deb to $Artifacts\$($debExport.Name)"
}

$deb = Get-ChildItem (Join-Path $Artifacts "iosender_*.deb") -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

$winInstaller = Get-ChildItem (Join-Path $Artifacts "ioSender-Setup-*-win-x64.exe") -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

$winOk = (Test-Path $WinExe)
$winInstallerOk = ($null -ne $winInstaller)
$wslOk = ($null -ne $deb)

$buildFailed = $false
if (-not $winOk) {
    Write-Host "Windows build failed (ioSender.exe missing)." -ForegroundColor Red
    if ($winPs.ExitCode -ne 0) { Write-Host "  exit code: $($winPs.ExitCode)" -ForegroundColor DarkRed }
    $buildFailed = $true
}

if (-not $winInstallerOk) {
    Write-Host "Windows installer build failed (setup .exe missing)." -ForegroundColor Red
    if (($null -ne $winInstallerPs) -and $winInstallerPs.ExitCode -ne 0) { Write-Host "  exit code: $($winInstallerPs.ExitCode)" -ForegroundColor DarkRed }
    $buildFailed = $true
}

if (-not $wslOk) {
    Write-Host "WSL build failed (.deb missing)." -ForegroundColor Red
    if ($wslPs.ExitCode -ne 0) { Write-Host "  exit code: $($wslPs.ExitCode)" -ForegroundColor DarkRed }
    $buildFailed = $true
}

if ($buildFailed) {
    throw "One or more builds failed. Check logs in $Artifacts"
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " Build complete" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host " Windows folder:    $WinExe"
Write-Host " Windows installer: $($winInstaller.FullName)"
Write-Host " Linux installer:   $($deb.FullName)"

if ($Launch) {
    Write-Step "Launching Windows build"
    Start-Process $WinExe
}

Write-Step "Opening artifacts folder"
Start-Process explorer.exe $Artifacts

if (-not $NoPause) {
    Write-Host ""
    Read-Host "Press Enter to close"
}
