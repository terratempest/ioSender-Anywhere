# Build Windows publish + Windows installer + Linux .deb (via WSL).
param(
    [ValidateSet('All', 'WinPortable', 'WinInstaller', 'LinuxDeb', '')]
    [string]$Target = '',
    [switch]$Launch,
    [switch]$NoPause,
    [switch]$NoExplorer,
    [switch]$Verbose,
    [string]$WslDistro = ''
)

$ErrorActionPreference = "Stop"

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$Scripts = Join-Path $Root "scripts"
$Artifacts = Join-Path $Root "artifacts"
$WinPublish = Join-Path $Artifacts "publish\win-x64"
$WinExe = Join-Path $WinPublish "ioSender.exe"
$Project = Join-Path $Root "ioSender\ioSender.csproj"
$WslScriptWin = Join-Path $Scripts "wsl-build-deb.sh"
$WinLog = Join-Path $Artifacts "build-win.log"
$WinInstallerLog = Join-Path $Artifacts "build-win-installer.log"
$WslLog = Join-Path $Artifacts "build-wsl.log"
$WinErr = Join-Path $Artifacts "build-win.err"
$WinInstallerErr = Join-Path $Artifacts "build-win-installer.err"
$WslErr = Join-Path $Artifacts "build-wsl.err"
$ExportDir = Join-Path $env:TEMP "iosender-export"

$PsHost = if (Get-Command pwsh -ErrorAction SilentlyContinue) { 'pwsh' } else { 'powershell.exe' }
$Quiet = [bool]$NoPause

. (Join-Path $Scripts "BuildConsole.ps1")

function Get-ProjectVersion {
    $version = (& dotnet msbuild $Project -getProperty:Version -nologo -v:q 2>$null | Select-Object -First 1).Trim()
    if ([string]::IsNullOrWhiteSpace($version)) {
        return "0.0.0"
    }
    return $version
}

function Test-WslAvailable {
    if (-not (Get-Command wsl -ErrorAction SilentlyContinue)) {
        return [PSCustomObject]@{ Ready = $false; Detail = 'not installed' }
    }

    $null = wsl --status 2>&1
    if ($LASTEXITCODE -ne 0) {
        return [PSCustomObject]@{ Ready = $false; Detail = 'not ready' }
    }

    $distros = @(
        (wsl -l -q 2>$null | ForEach-Object { ($_ -replace '\0', '').Trim() }) |
        Where-Object { $_ -and $_ -notmatch '^\s*$' }
    )
    if ($distros.Count -eq 0) {
        return [PSCustomObject]@{ Ready = $false; Detail = 'no distros' }
    }

    foreach ($name in @("Ubuntu", "Ubuntu-24.04", "Ubuntu-22.04")) {
        if ($distros -contains $name) {
            return [PSCustomObject]@{ Ready = $true; Detail = "$name ok" }
        }
    }

    return [PSCustomObject]@{ Ready = $true; Detail = "$($distros[0]) ok" }
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
    $Script = $Script -replace "`r`n", "`n" -replace "`r", "`n"
    & wsl.exe -d $Distro --cd ~ -- bash -lc "$Script"
    if ($LASTEXITCODE -ne 0) {
        throw "wsl bash failed (exit $LASTEXITCODE)"
    }
}

function Invoke-WslScriptFile {
    param(
        [string]$Distro,
        [string]$ScriptPath,
        [string[]]$Args = @()
    )

    $wslScript = New-WslBuildScriptPath $ScriptPath
    $wslArgs = @("-d", $Distro, "--cd", "~", "-e", "/bin/bash", $wslScript) + $Args
    & wsl.exe @wslArgs
    if ($LASTEXITCODE -ne 0) {
        throw "wsl script failed (exit $LASTEXITCODE): $ScriptPath"
    }
}

function Get-LinuxDebExport {
    param(
        [string]$Distro,
        [string]$WindowsExportDir,
        [string]$WslExportDir
    )

    for ($attempt = 0; $attempt -lt 5; $attempt++) {
        $deb = Get-ChildItem $WindowsExportDir -Filter "iosender_*.deb" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($deb) {
            return $deb
        }
        Start-Sleep -Milliseconds 400
    }

    if ($Distro -and $WslExportDir) {
        $copyScript = Join-Path $Scripts "wsl-copy-deb-export.sh"
        try {
            Invoke-WslScriptFile -Distro $Distro -ScriptPath $copyScript -Args @($WslExportDir)
        } catch {
            return $null
        }

        return Get-ChildItem $WindowsExportDir -Filter "iosender_*.deb" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
    }

    return $null
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
        if (-not $Quiet) {
            Write-Host "  Staging source for WSL (network drive)..."
        }
        $staging = Join-Path $env:TEMP "iosender-src-$PID"
        if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $staging | Out-Null

        $robolog = Join-Path $Artifacts "robocopy-staging.log"
        $rc = robocopy $SourceRoot $staging /MIR `
            /XD bin obj .git "artifacts\publish" packaging\debian-staging `
            /NFL /NDL /NJH /NJS /NP `
            /LOG:$robolog
        if ($rc -ge 8) {
            throw "robocopy staging failed (exit $rc). See $robolog"
        }

        $wslSource = Convert-ToWslPath $staging
        if (-not (Test-WslDirectory -Distro $Distro -WslPath $wslSource)) {
            throw "Staged copy still not visible in WSL at $wslSource"
        }
        if (-not $Quiet) {
            Write-Host "  Staged source for WSL."
        }
    }

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

function New-WindowsPortableZip {
    param(
        [string]$SourceDirectory,
        [string]$Version
    )

    if (-not (Test-Path (Join-Path $SourceDirectory "ioSender.exe"))) {
        throw "Windows publish output missing: $SourceDirectory\ioSender.exe"
    }

    $portableName = "ioSender-$Version-win-x64-portable"
    $zipPath = Join-Path $Artifacts "$portableName.zip"
    $stagingRoot = Join-Path $env:TEMP "iosender-portable-$PID"
    $portableRoot = Join-Path $stagingRoot $portableName

    if (Test-Path $stagingRoot) { Remove-Item $stagingRoot -Recurse -Force }
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

    New-Item -ItemType Directory -Force -Path $portableRoot | Out-Null
    Copy-Item (Join-Path $SourceDirectory "*") $portableRoot -Recurse -Force
    Compress-Archive -Path $portableRoot -DestinationPath $zipPath -CompressionLevel Optimal -Force
    Remove-Item $stagingRoot -Recurse -Force

    return $zipPath
}

function Resolve-InnoSetupCompiler {
    $command = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 7\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ }

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    return $null
}

function Test-DotNetSdk {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        return [PSCustomObject]@{
            Ok          = $false
            Detail      = 'not found'
            FailMessage = '.NET SDK not found. Install .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0'
        }
    }

    $version = (& dotnet --version 2>$null).Trim()
    if ([string]::IsNullOrWhiteSpace($version)) {
        return [PSCustomObject]@{
            Ok          = $false
            Detail      = 'unknown'
            FailMessage = 'dotnet --version returned no output.'
        }
    }

    $major = 0
    [void][int]::TryParse(($version -split '\.')[0], [ref]$major)
    if ($major -lt 8) {
        return [PSCustomObject]@{
            Ok          = $false
            Detail      = $version
            FailMessage = ".NET SDK 8.x required (found $version)."
        }
    }

    return [PSCustomObject]@{
        Ok          = $true
        Detail      = $version
        FailMessage = $null
    }
}

function Get-PhasesForTarget {
    param([string]$BuildTarget)

    switch ($BuildTarget) {
        'All' {
            return @('Preflight', 'Clean', 'WslSync', 'WinPublish', 'WinPortable', 'WinInstaller', 'LinuxDeb')
        }
        'WinPortable' {
            return @('Preflight', 'Clean', 'WinPublish', 'WinPortable')
        }
        'WinInstaller' {
            return @('Preflight', 'Clean', 'WinInstaller')
        }
        'LinuxDeb' {
            return @('Preflight', 'Clean', 'WslSync', 'LinuxDeb')
        }
        default {
            throw "Unknown target '$BuildTarget'."
        }
    }
}

function Test-TargetNeedsLinux {
    param([string]$BuildTarget)
    return $BuildTarget -in @('All', 'LinuxDeb')
}

function Test-TargetNeedsInstaller {
    param([string]$BuildTarget)
    return $BuildTarget -in @('All', 'WinInstaller')
}

function Test-TargetNeedsPortable {
    param([string]$BuildTarget)
    return $BuildTarget -in @('All', 'WinPortable')
}

function Start-PowerShellJob {
    param(
        [string]$ScriptPath,
        [string]$LogPath,
        [string]$ErrPath
    )

    $args = "-NoProfile -ExecutionPolicy Bypass -File `"$ScriptPath`""
    return Start-Process -FilePath $PsHost -WorkingDirectory $env:TEMP `
        -ArgumentList $args -PassThru -NoNewWindow `
        -RedirectStandardOutput $LogPath -RedirectStandardError $ErrPath
}

function Clear-WslBuildState {
    param([string]$Distro)

    $cleanScript = Join-Path $Scripts "wsl-clean-build.sh"
    Invoke-WslScriptFile -Distro $Distro -ScriptPath $cleanScript
}

function Invoke-FreshBuildClean {
    param([string]$BuildTarget)

    $cleanScript = Join-Path $Scripts "clean-release-build.ps1"
    & $PsHost -NoProfile -ExecutionPolicy Bypass -File $cleanScript -Target $BuildTarget -Quiet
    if ($LASTEXITCODE -ne 0) {
        throw "Release clean failed (exit $LASTEXITCODE)."
    }

    $solution = Join-Path $Root "ioSender.net.sln"
    & dotnet restore $solution --force --nologo -v:q
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed (exit $LASTEXITCODE)."
    }
}

function Clear-StaleArtifacts {
    param([string]$BuildTarget)

    Get-ChildItem $ExportDir -Filter "iosender_*.deb" -ErrorAction SilentlyContinue | Remove-Item -Force
    if ($BuildTarget -in @('All', 'WinInstaller')) {
        Get-ChildItem $Artifacts -Filter "ioSender-Setup-*-win-x64.exe" -ErrorAction SilentlyContinue | Remove-Item -Force
    }
    if ($BuildTarget -in @('All', 'WinPortable')) {
        Get-ChildItem $Artifacts -Filter "ioSender-*-win-x64-portable.zip" -ErrorAction SilentlyContinue | Remove-Item -Force
    }
    if ($BuildTarget -in @('All', 'LinuxDeb')) {
        Get-ChildItem $Artifacts -Filter "iosender_*.deb" -ErrorAction SilentlyContinue | Remove-Item -Force
    }
}

function Test-PhaseFailed {
    param([string]$PhaseName)

    if (-not $script:BuildConsolePhases.ContainsKey($PhaseName)) {
        return $false
    }
    return $script:BuildConsolePhases[$PhaseName].Status -eq 'failed'
}

function Test-BuildTargetFailed {
    param([string]$BuildTarget)

    foreach ($phaseName in (Get-PhasesForTarget -BuildTarget $BuildTarget)) {
        if (Test-PhaseFailed -PhaseName $phaseName) {
            return $true
        }
    }
    return $false
}

function Invoke-ReleaseBuild {
    param([string]$BuildTarget)

    $version = Get-ProjectVersion
    Initialize-BuildConsole -Version $version -Root $Root -Quiet:$Quiet

    $wslHint = Test-WslAvailable
    $wslStatus = if ($wslHint.Ready) { $wslHint.Detail } else { "X $($wslHint.Detail)" }

    if ([string]::IsNullOrWhiteSpace($BuildTarget)) {
        $BuildTarget = Show-TargetMenu -WslStatus $wslStatus
    }

    $needsLinux = Test-TargetNeedsLinux -BuildTarget $BuildTarget
    $needsInstaller = Test-TargetNeedsInstaller -BuildTarget $BuildTarget
    $needsPortable = Test-TargetNeedsPortable -BuildTarget $BuildTarget
    $needsPublish = $BuildTarget -in @('All', 'WinPortable')

    Initialize-PhaseBoard -Phases (Get-PhasesForTarget -BuildTarget $BuildTarget)

    $totalSw = [System.Diagnostics.Stopwatch]::StartNew()
    Set-BuildConsoleStopwatch -Stopwatch $totalSw

    Set-PhaseStatus -Name 'Preflight' -Status 'running'

    $checks = @()
    $dotnetCheck = Test-DotNetSdk
    $checks += [PSCustomObject]@{
        Label       = '.NET SDK'
        Detail      = $dotnetCheck.Detail
        Ok          = $dotnetCheck.Ok
        FailMessage = $dotnetCheck.FailMessage
    }

    if ($needsInstaller) {
        $iscc = Resolve-InnoSetupCompiler
        $checks += [PSCustomObject]@{
            Label       = 'Inno Setup'
            Detail      = if ($iscc) { Split-Path (Split-Path $iscc -Parent) -Leaf } else { 'not found' }
            Ok          = [bool]$iscc
            FailMessage = 'Inno Setup (ISCC.exe) not found. Install Inno Setup 6 or 7.'
        }
    }

    if ($needsLinux) {
        $checks += [PSCustomObject]@{
            Label       = 'WSL'
            Detail      = if ($wslHint.Ready) { $wslHint.Detail } else { $wslHint.Detail }
            Ok          = $wslHint.Ready
            FailMessage = 'WSL is required for Linux .deb builds. Install WSL + Ubuntu.'
        }
    }

    Show-PreflightPanel -Checks $checks
    Set-PhaseStatus -Name 'Preflight' -Status 'done'

    New-Item -ItemType Directory -Force -Path $Artifacts | Out-Null
    New-Item -ItemType Directory -Force -Path $ExportDir | Out-Null
    Clear-StaleArtifacts -BuildTarget $BuildTarget

    Set-PhaseStatus -Name 'Clean' -Status 'running'
    Invoke-FreshBuildClean -BuildTarget $BuildTarget
    Set-PhaseStatus -Name 'Clean' -Status 'done'

    $publishScript = Join-Path $Scripts "publish-windows.ps1"
    $installerScript = Join-Path $Scripts "package-windows-installer.ps1"
    $distro = $null
    $wslExport = $null
    $wslSh = $null

    if ($needsLinux) {
        Ensure-WslInstalled
        $distro = Get-WslDistroName -Preferred $WslDistro
        Ensure-WslDistroReady -Distro $distro

        Set-PhaseStatus -Name 'WslSync' -Status 'running'
        Sync-SourceToWsl -Distro $distro -SourceRoot $Root
        Clear-WslBuildState -Distro $distro
        Set-PhaseStatus -Name 'WslSync' -Status 'done'

        $wslExport = Convert-ToWslPath $ExportDir
        $wslSh = New-WslBuildScriptPath $WslScriptWin
    } else {
        Set-PhaseStatus -Name 'WslSync' -Status 'skipped'
        Set-PhaseStatus -Name 'LinuxDeb' -Status 'skipped'
    }

    $winPs = $null
    $wslPs = $null
    $winInstallerPs = $null
    $parallelJobs = @{}

    if ($needsPublish) {
        foreach ($log in @($WinLog, $WinErr)) {
            if (Test-Path $log) { Remove-Item $log -Force }
        }
        Set-PhaseStatus -Name 'WinPublish' -Status 'running' -LogPath $WinLog -ErrPath $WinErr
        $winPs = Start-PowerShellJob -ScriptPath $publishScript -LogPath $WinLog -ErrPath $WinErr
        $parallelJobs['WinPublish'] = @{
            Process     = $winPs
            LogPath     = $WinLog
            ErrPath     = $WinErr
            SuccessPath = $WinExe
        }
    } else {
        Set-PhaseStatus -Name 'WinPublish' -Status 'skipped'
    }

    if ($needsLinux) {
        foreach ($log in @($WslLog, $WslErr)) {
            if (Test-Path $log) { Remove-Item $log -Force }
        }
        $wslArgs = "-d $distro --cd ~ -e /bin/bash `"$wslSh`" `"$wslExport`""
        Set-PhaseStatus -Name 'LinuxDeb' -Status 'running' -LogPath $WslLog -ErrPath $WslErr
        $wslPs = Start-Process -FilePath "wsl.exe" -WorkingDirectory $env:TEMP `
            -ArgumentList $wslArgs -PassThru -NoNewWindow `
            -RedirectStandardOutput $WslLog -RedirectStandardError $WslErr
        $parallelJobs['LinuxDeb'] = @{
            Process     = $wslPs
            LogPath     = $WslLog
            ErrPath     = $WslErr
            SuccessPath = $ExportDir
            RequireDeb  = $true
        }
    }

    if ($parallelJobs.Count -gt 0) {
        Start-PhaseBoardDisplay
        $null = Wait-BuildJobs -JobMap $parallelJobs
    }

    if (-not $needsPublish) {
        Set-PhaseStatus -Name 'WinPortable' -Status 'skipped'
    }
    if (-not $needsInstaller) {
        Set-PhaseStatus -Name 'WinInstaller' -Status 'skipped'
    }

    $winZip = $null
    if ($needsPortable) {
        if (Test-Path $WinExe) {
            Set-PhaseStatus -Name 'WinPortable' -Status 'running'
            $winZip = New-WindowsPortableZip -SourceDirectory $WinPublish -Version $version
            Set-PhaseStatus -Name 'WinPortable' -Status 'done' -Message $winZip
        } else {
            Set-PhaseStatus -Name 'WinPortable' -Status 'failed' -Message 'ioSender.exe missing'
        }
    }

    if ($needsInstaller) {
        if ($BuildTarget -eq 'All' -and -not (Test-Path $WinExe)) {
            Set-PhaseStatus -Name 'WinInstaller' -Status 'skipped' -Message 'skipped (publish failed)'
        } else {
            foreach ($log in @($WinInstallerLog, $WinInstallerErr)) {
                if (Test-Path $log) { Remove-Item $log -Force }
            }
            Set-PhaseStatus -Name 'WinInstaller' -Status 'running' -LogPath $WinInstallerLog -ErrPath $WinInstallerErr
            Start-PhaseBoardDisplay
            $winInstallerPs = Start-PowerShellJob -ScriptPath $installerScript -LogPath $WinInstallerLog -ErrPath $WinInstallerErr
            $installerJobs = @{
                WinInstaller = @{
                    Process     = $winInstallerPs
                    LogPath     = $WinInstallerLog
                    ErrPath     = $WinInstallerErr
                    SuccessPath = $null
                }
            }
            $null = Wait-BuildJobs -JobMap $installerJobs

            $winInstallerCheck = Get-ChildItem (Join-Path $Artifacts "ioSender-Setup-*-win-x64.exe") -ErrorAction SilentlyContinue |
                Sort-Object LastWriteTime -Descending |
                Select-Object -First 1
            if ($script:BuildConsolePhases['WinInstaller'].Status -eq 'done' -and -not $winInstallerCheck) {
                Set-PhaseStatus -Name 'WinInstaller' -Status 'failed' -Message 'setup .exe missing'
            } elseif ($winInstallerCheck) {
                Set-PhaseStatus -Name 'WinInstaller' -Status 'done' -Message $winInstallerCheck.Name
            }
        }
    }

    if ($needsLinux) {
        $debExport = Get-LinuxDebExport -Distro $distro -WindowsExportDir $ExportDir -WslExportDir $wslExport

        if ($debExport) {
            Copy-Item $debExport.FullName (Join-Path $Artifacts $debExport.Name) -Force
            Set-PhaseStatus -Name 'LinuxDeb' -Status 'done' -Message $debExport.Name
        } else {
            Set-PhaseStatus -Name 'LinuxDeb' -Status 'failed' -Message '.deb missing from export'
        }
    }

    $deb = $null
    if (-not (Test-PhaseFailed -PhaseName 'LinuxDeb')) {
        $deb = Get-ChildItem (Join-Path $Artifacts "iosender_*.deb") -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
    }

    $winInstaller = $null
    if (-not (Test-PhaseFailed -PhaseName 'WinInstaller')) {
        $winInstaller = Get-ChildItem (Join-Path $Artifacts "ioSender-Setup-*-win-x64.exe") -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
    }

    $results = @()
    if ($BuildTarget -in @('All', 'WinPortable')) {
        $portableOk = (-not (Test-PhaseFailed -PhaseName 'WinPortable')) -and
            (-not (Test-PhaseFailed -PhaseName 'WinPublish')) -and
            ($null -ne $winZip -and (Test-Path $winZip))
        $results += [PSCustomObject]@{
            Label  = 'Windows portable'
            Status = if ($portableOk) { 'ok' } else { 'FAIL' }
            Path   = if ($portableOk) { $winZip } else { $null }
        }
    }
    if ($BuildTarget -in @('All', 'WinInstaller')) {
        $installerOk = (-not (Test-PhaseFailed -PhaseName 'WinInstaller')) -and
            ($null -ne $winInstaller)
        $results += [PSCustomObject]@{
            Label  = 'Windows installer'
            Status = if ($installerOk) { 'ok' } else { 'FAIL' }
            Path   = if ($installerOk) { $winInstaller.FullName } else { $null }
        }
    }
    if ($BuildTarget -in @('All', 'LinuxDeb')) {
        $debOk = (-not (Test-PhaseFailed -PhaseName 'LinuxDeb')) -and ($null -ne $deb)
        $results += [PSCustomObject]@{
            Label  = 'Linux .deb'
            Status = if ($debOk) { 'ok' } else { 'FAIL' }
            Path   = if ($debOk) { $deb.FullName } else { $null }
        }
    }

    $totalSw.Stop()
    $buildFailed = (Test-BuildTargetFailed -BuildTarget $BuildTarget) -or
        (@($results | Where-Object { $_.Status -ne 'ok' }).Count -gt 0)

    Finish-PhaseBoard

    if ($buildFailed) {
        Show-FailureLogs
    }
    if ($Verbose) {
        Show-VerbosePhaseLogs
    }

    Show-BuildSummary -Results $results -Elapsed $totalSw.Elapsed -Failed:$buildFailed

    if ($buildFailed) {
        throw "One or more builds failed. Check logs in $Artifacts"
    }

    if ($Launch -and (Test-Path $WinExe)) {
        Start-Process $WinExe
    }

    if (-not $NoExplorer) {
        Start-Process explorer.exe $Artifacts
    }
}

$exitCode = 0
try {
    Invoke-ReleaseBuild -BuildTarget $Target
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    $exitCode = 1
} finally {
    if (-not $NoPause) {
        Read-Host "`nPress Enter to close"
    }
}

exit $exitCode
