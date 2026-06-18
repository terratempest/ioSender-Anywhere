# Build Windows packages and WSL-backed Linux packages.
param(
    [ValidateSet('All', 'Windows', 'Linux', 'WinPortable', 'WinInstaller', 'LinuxDeb', 'LinuxRpm', 'LinuxAppImage', '')]
    [string]$Target = '',
    [Alias('Rid')]
    [ValidateSet('', 'win-x64', 'win-arm64', 'linux-x64', 'linux-arm64')]
    [string]$RuntimeIdentifier = '',
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
$Project = Join-Path $Root "ioSender\ioSender.csproj"
$ExportDir = Join-Path $env:TEMP "iosender-export"
$PsHost = if (Get-Command pwsh -ErrorAction SilentlyContinue) { 'pwsh' } else { 'powershell.exe' }
$Quiet = [bool]$NoPause

$WindowsRids = @('win-x64', 'win-arm64')
$LinuxRids = @('linux-x64', 'linux-arm64')

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
        throw "WSL distro '$Distro' could not start (exit $LASTEXITCODE). Details: $message"
    }
}

function Get-WslHostRid {
    param([string]$Distro)

    $arch = (& wsl.exe -d $Distro --cd ~ -e /bin/bash -lc "uname -m" 2>$null | Select-Object -First 1).Trim()
    switch ($arch) {
        { $_ -in @('x86_64', 'amd64') } { return 'linux-x64' }
        { $_ -in @('aarch64', 'arm64') } { return 'linux-arm64' }
        default { throw "Unsupported WSL architecture '$arch'." }
    }
}

function Test-WslCommandAvailable {
    param(
        [string]$Distro,
        [string]$CommandName
    )

    if (-not $Distro) {
        return $false
    }

    & wsl.exe -d $Distro --cd ~ -e /bin/bash -lc "command -v '$CommandName' >/dev/null 2>&1" 2>$null
    return $LASTEXITCODE -eq 0
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

function Escape-BashSingleQuoted([string]$Value) {
    return ($Value -replace "'", "'\''")
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
            [Console]::Out.WriteLine("  Staging source for WSL...")
        }
        $staging = Join-Path $env:TEMP "iosender-src-$PID"
        if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $staging | Out-Null

        $robolog = Join-Path $Artifacts "robocopy-staging.log"
        $rc = robocopy $SourceRoot $staging /MIR `
            /XD bin obj .git "artifacts\publish" packaging\debian-staging packaging\rpm-staging packaging\appimage-staging `
            /NFL /NDL /NJH /NJS /NP `
            /LOG:$robolog
        if ($rc -ge 8) {
            throw "robocopy staging failed (exit $rc). See $robolog"
        }

        $wslSource = Convert-ToWslPath $staging
        if (-not (Test-WslDirectory -Distro $Distro -WslPath $wslSource)) {
            throw "Staged copy still not visible in WSL at $wslSource"
        }
    }

    $src = Escape-BashSingleQuoted ($wslSource.TrimEnd('/') + '/')
    $rsyncScript = "set -eu; mkdir -p ~/ioSender-build; rsync -a --delete --exclude=obj --exclude=bin --exclude=.git --exclude=packaging/debian-staging --exclude=packaging/rpm-staging --exclude=packaging/appimage-staging '$src' ~/ioSender-build/"
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
        [string]$Version,
        [string]$Rid
    )

    if (-not (Test-Path (Join-Path $SourceDirectory "ioSender.exe"))) {
        throw "Windows publish output missing: $SourceDirectory\ioSender.exe"
    }

    $portableName = "ioSender-$Version-$Rid-portable"
    $zipPath = Join-Path $Artifacts "$portableName.zip"
    $stagingRoot = Join-Path $env:TEMP "iosender-portable-$PID-$Rid"
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
            FailMessage = '.NET SDK not found. Install .NET 8 SDK.'
        }
    }

    $version = (& dotnet --version 2>$null).Trim()
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

function Test-TargetIncludesWindows {
    param([string]$BuildTarget)
    return $BuildTarget -in @('All', 'Windows', 'WinPortable', 'WinInstaller')
}

function Test-TargetIncludesLinux {
    param([string]$BuildTarget)
    return $BuildTarget -in @('All', 'Linux', 'LinuxDeb', 'LinuxRpm', 'LinuxAppImage')
}

function Get-WindowsPackageTargets {
    param([string]$BuildTarget)
    switch ($BuildTarget) {
        { $_ -in @('All', 'Windows') } { return @('WinPortable', 'WinInstaller') }
        'WinPortable' { return @('WinPortable') }
        'WinInstaller' { return @('WinInstaller') }
        default { return @() }
    }
}

function Get-LinuxPackageTargets {
    param([string]$BuildTarget)
    switch ($BuildTarget) {
        { $_ -in @('All', 'Linux') } { return @('LinuxDeb', 'LinuxRpm', 'LinuxAppImage') }
        'LinuxDeb' { return @('LinuxDeb') }
        'LinuxRpm' { return @('LinuxRpm') }
        'LinuxAppImage' { return @('LinuxAppImage') }
        default { return @() }
    }
}

function Assert-TargetRidCompatibility {
    param([string]$BuildTarget)

    if (-not $RuntimeIdentifier) { return }

    $isWindowsTarget = Test-TargetIncludesWindows -BuildTarget $BuildTarget
    $isLinuxTarget = Test-TargetIncludesLinux -BuildTarget $BuildTarget
    if ($BuildTarget -in @('WinPortable', 'WinInstaller', 'Windows') -and -not $RuntimeIdentifier.StartsWith('win')) {
        throw "$BuildTarget requires a Windows RID."
    }
    if ($BuildTarget -in @('LinuxDeb', 'LinuxRpm', 'LinuxAppImage', 'Linux') -and -not $RuntimeIdentifier.StartsWith('linux')) {
        throw "$BuildTarget requires a Linux RID."
    }
    if (-not $isWindowsTarget -and -not $isLinuxTarget) {
        throw "Unknown target '$BuildTarget'."
    }
}

function Get-SelectedWindowsRids {
    param([string]$BuildTarget)

    if (-not (Test-TargetIncludesWindows -BuildTarget $BuildTarget)) { return @() }
    if ($RuntimeIdentifier) {
        if ($RuntimeIdentifier.StartsWith('win')) { return @($RuntimeIdentifier) }
        return @()
    }
    return $WindowsRids
}

function Get-SelectedLinuxRids {
    param(
        [string]$BuildTarget
    )

    if (-not (Test-TargetIncludesLinux -BuildTarget $BuildTarget)) { return @() }
    if ($RuntimeIdentifier) {
        if ($RuntimeIdentifier.StartsWith('linux')) { return @($RuntimeIdentifier) }
        return @()
    }
    return $LinuxRids
}

function Get-DebArch {
    param([string]$Rid)
    switch ($Rid) {
        'linux-x64' { return 'amd64' }
        'linux-arm64' { return 'arm64' }
        default { throw "Unsupported Debian RID '$Rid'." }
    }
}

function Get-LinuxArtifactPattern {
    param(
        [string]$PackageTarget,
        [string]$Rid
    )

    switch ($PackageTarget) {
        'LinuxDeb' { return "iosender_*_$(Get-DebArch -Rid $Rid).deb" }
        'LinuxRpm' { return "ioSender-*-$Rid.rpm" }
        'LinuxAppImage' { return "ioSender-*-$Rid.AppImage" }
        default { throw "Unsupported Linux package target '$PackageTarget'." }
    }
}

function Get-PhasesForTarget {
    param(
        [string[]]$WinRids,
        [string[]]$LinuxBuildRids,
        [string[]]$WinPackages,
        [string[]]$LinuxPackages
    )

    $phases = @('Preflight', 'Clean')
    if ($LinuxBuildRids.Count -gt 0) {
        $phases += 'WslSync'
    }
    if ($WinPackages -contains 'WinPortable') {
        foreach ($rid in $WinRids) {
            $phases += "WinPublish:$rid"
            $phases += "WinPortable:$rid"
        }
    }
    if ($WinPackages -contains 'WinInstaller') {
        foreach ($rid in $WinRids) {
            $phases += "WinInstaller:$rid"
        }
    }
    foreach ($package in $LinuxPackages) {
        foreach ($rid in $LinuxBuildRids) {
            $phases += "${package}:$rid"
        }
    }
    return $phases
}

function Test-PhaseFailed {
    param([string]$PhaseName)

    if (-not $script:BuildConsolePhases.ContainsKey($PhaseName)) {
        return $false
    }
    return $script:BuildConsolePhases[$PhaseName].Status -eq 'failed'
}

function Test-AnyPhaseFailed {
    foreach ($phaseName in $script:BuildConsolePhaseOrder) {
        if (Test-PhaseFailed -PhaseName $phaseName) {
            return $true
        }
    }
    return $false
}

function ConvertTo-ProcessArgumentLine {
    param([string[]]$Arguments)

    return (($Arguments | ForEach-Object {
        if ($_ -match '[\s"]') {
            '"' + ($_ -replace '"', '\"') + '"'
        } else {
            $_
        }
    }) -join ' ')
}

function Get-PhaseLogPath {
    param(
        [string]$PhaseName,
        [string]$Extension
    )

    $safeName = $PhaseName -replace '[^A-Za-z0-9_.-]', '-'
    return (Join-Path $Artifacts "build-$safeName.$Extension")
}

function Invoke-PhaseProcess {
    param(
        [string]$PhaseName,
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$LogPath,
        [string]$ErrPath,
        [string]$SuccessPath = $null
    )

    foreach ($log in @($LogPath, $ErrPath)) {
        if ($log -and (Test-Path $log)) { Remove-Item $log -Force }
    }

    Set-PhaseStatus -Name $PhaseName -Status 'running' -LogPath $LogPath -ErrPath $ErrPath
    Start-PhaseBoardDisplay

    $argumentLine = ConvertTo-ProcessArgumentLine -Arguments $Arguments
    $process = Start-Process -FilePath $FilePath -WorkingDirectory $Root `
        -ArgumentList $argumentLine -PassThru -Wait -WindowStyle Hidden `
        -RedirectStandardOutput $LogPath -RedirectStandardError $ErrPath

    $ok = $process.ExitCode -eq 0
    if ($ok -and $SuccessPath) {
        $ok = Test-Path $SuccessPath
    }

    Set-PhaseStatus -Name $PhaseName -Status $(if ($ok) { 'done' } else { 'failed' }) -ExitCode $process.ExitCode
    return $ok
}

function Invoke-FreshBuildClean {
    param([string]$BuildTarget)

    $cleanScript = Join-Path $Scripts "clean-release-build.ps1"
    $args = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $cleanScript, '-Target', $BuildTarget, '-Quiet')
    if ($RuntimeIdentifier) {
        $args += @('-RuntimeIdentifier', $RuntimeIdentifier)
    }

    & $PsHost @args
    if ($LASTEXITCODE -ne 0) {
        throw "Release clean failed (exit $LASTEXITCODE)."
    }

    $solution = Join-Path $Root "ioSender.net.sln"
    & dotnet restore $solution --force --nologo -v:q
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed (exit $LASTEXITCODE)."
    }
}

function Clear-WslBuildState {
    param([string]$Distro)

    $cleanScript = Join-Path $Scripts "wsl-clean-build.sh"
    Invoke-WslScriptFile -Distro $Distro -ScriptPath $cleanScript
}

function Clear-StaleArtifacts {
    param(
        [string[]]$WinRids,
        [string[]]$LinuxBuildRids,
        [string[]]$WinPackages,
        [string[]]$LinuxPackages
    )

    Get-ChildItem $ExportDir -File -ErrorAction SilentlyContinue | Remove-Item -Force

    if ($WinPackages -contains 'WinInstaller') {
        foreach ($rid in $WinRids) {
            Get-ChildItem $Artifacts -Filter "ioSender-Setup-*-$rid.exe" -ErrorAction SilentlyContinue | Remove-Item -Force
        }
    }
    if ($WinPackages -contains 'WinPortable') {
        foreach ($rid in $WinRids) {
            Get-ChildItem $Artifacts -Filter "ioSender-*-$rid-portable.zip" -ErrorAction SilentlyContinue | Remove-Item -Force
        }
    }
    foreach ($package in $LinuxPackages) {
        foreach ($rid in $LinuxBuildRids) {
            $pattern = Get-LinuxArtifactPattern -PackageTarget $package -Rid $rid
            Get-ChildItem $Artifacts -Filter $pattern -ErrorAction SilentlyContinue | Remove-Item -Force
        }
    }
}

function Get-LinuxExport {
    param(
        [string]$Distro,
        [string]$WindowsExportDir,
        [string]$WslExportDir,
        [string]$PackageTarget,
        [string]$Rid
    )

    $pattern = Get-LinuxArtifactPattern -PackageTarget $PackageTarget -Rid $Rid
    for ($attempt = 0; $attempt -lt 5; $attempt++) {
        $artifact = Get-ChildItem $WindowsExportDir -Filter $pattern -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($artifact) {
            return $artifact
        }
        Start-Sleep -Milliseconds 400
    }

    if ($Distro -and $WslExportDir) {
        $copyScript = Join-Path $Scripts "wsl-copy-deb-export.sh"
        try {
            Invoke-WslScriptFile -Distro $Distro -ScriptPath $copyScript -Args @($WslExportDir, $Rid)
        } catch {
            return $null
        }

        return Get-ChildItem $WindowsExportDir -Filter $pattern -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
    }

    return $null
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

    Assert-TargetRidCompatibility -BuildTarget $BuildTarget

    $targetMayNeedLinux = (Test-TargetIncludesLinux -BuildTarget $BuildTarget) -and
        (-not $RuntimeIdentifier -or $RuntimeIdentifier.StartsWith('linux'))
    $distro = $null
    $wslHostRid = 'linux-x64'
    if ($targetMayNeedLinux -and $wslHint.Ready) {
        $distro = Get-WslDistroName -Preferred $WslDistro
        Ensure-WslDistroReady -Distro $distro
        $wslHostRid = Get-WslHostRid -Distro $distro
    }

    $winPackages = Get-WindowsPackageTargets -BuildTarget $BuildTarget
    $linuxPackages = Get-LinuxPackageTargets -BuildTarget $BuildTarget
    $winRids = Get-SelectedWindowsRids -BuildTarget $BuildTarget
    $linuxBuildRids = Get-SelectedLinuxRids -BuildTarget $BuildTarget
    $needsLinux = $linuxBuildRids.Count -gt 0
    $needsInstaller = (($winPackages -contains 'WinInstaller') -and $winRids.Count -gt 0)

    Initialize-PhaseBoard -Phases (Get-PhasesForTarget -WinRids $winRids -LinuxBuildRids $linuxBuildRids -WinPackages $winPackages -LinuxPackages $linuxPackages)

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
            Detail      = if ($wslHint.Ready) { "$($wslHint.Detail), $wslHostRid" } else { $wslHint.Detail }
            Ok          = $wslHint.Ready
            FailMessage = 'WSL is required for Linux package builds. Install WSL + Ubuntu.'
        }
        foreach ($rid in $linuxBuildRids) {
            $ridDetail = if ($rid -eq $wslHostRid) { "$rid native" } else { "$rid cross-build" }
            $checks += [PSCustomObject]@{
                Label       = 'Linux RID'
                Detail      = $ridDetail
                Ok          = $true
                FailMessage = $null
            }
        }
        if ($linuxPackages -contains 'LinuxRpm') {
            $hasRpmBuild = Test-WslCommandAvailable -Distro $distro -CommandName 'rpmbuild'
            $checks += [PSCustomObject]@{
                Label       = 'rpmbuild'
                Detail      = if ($hasRpmBuild) { 'found' } else { 'not found' }
                Ok          = $hasRpmBuild
                FailMessage = 'rpmbuild is required for Linux .rpm builds. Install in WSL: sudo apt install -y rpm'
            }
        }
        if ($linuxPackages -contains 'LinuxAppImage') {
            $hasAppImageTool = Test-WslCommandAvailable -Distro $distro -CommandName 'appimagetool'
            $checks += [PSCustomObject]@{
                Label       = 'appimagetool'
                Detail      = if ($hasAppImageTool) { 'found' } else { 'not found' }
                Ok          = $hasAppImageTool
                FailMessage = 'appimagetool is required for Linux AppImage builds. Install appimagetool in WSL and ensure it is on PATH.'
            }
        }
    }

    Show-PreflightPanel -Checks $checks
    Set-PhaseStatus -Name 'Preflight' -Status 'done'

    New-Item -ItemType Directory -Force -Path $Artifacts | Out-Null
    New-Item -ItemType Directory -Force -Path $ExportDir | Out-Null
    Clear-StaleArtifacts -WinRids $winRids -LinuxBuildRids $linuxBuildRids -WinPackages $winPackages -LinuxPackages $linuxPackages

    Set-PhaseStatus -Name 'Clean' -Status 'running'
    Invoke-FreshBuildClean -BuildTarget $BuildTarget
    Set-PhaseStatus -Name 'Clean' -Status 'done'

    $wslExport = $null
    $wslSh = $null
    if ($needsLinux) {
        Ensure-WslInstalled
        if (-not $distro) {
            $distro = Get-WslDistroName -Preferred $WslDistro
            Ensure-WslDistroReady -Distro $distro
            $wslHostRid = Get-WslHostRid -Distro $distro
        }

        Set-PhaseStatus -Name 'WslSync' -Status 'running'
        Sync-SourceToWsl -Distro $distro -SourceRoot $Root
        Clear-WslBuildState -Distro $distro
        Set-PhaseStatus -Name 'WslSync' -Status 'done'

        $wslExport = Convert-ToWslPath $ExportDir
        $wslSh = New-WslBuildScriptPath (Join-Path $Scripts "wsl-build-deb.sh")
    }

    $results = @()
    $publishScript = Join-Path $Scripts "publish-windows.ps1"
    $installerScript = Join-Path $Scripts "package-windows-installer.ps1"

    if ($winPackages -contains 'WinPortable') {
        foreach ($rid in $winRids) {
            $publishDir = Join-Path $Artifacts "publish\$rid"
            $exe = Join-Path $publishDir "ioSender.exe"
            $phase = "WinPublish:$rid"
            $log = Get-PhaseLogPath -PhaseName $phase -Extension 'log'
            $err = Get-PhaseLogPath -PhaseName $phase -Extension 'err'
            [void](Invoke-PhaseProcess -PhaseName $phase -FilePath $PsHost -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $publishScript, '-RuntimeIdentifier', $rid) -LogPath $log -ErrPath $err -SuccessPath $exe)

            $zip = $null
            $zipPhase = "WinPortable:$rid"
            if (Test-Path $exe) {
                Set-PhaseStatus -Name $zipPhase -Status 'running'
                try {
                    $zip = New-WindowsPortableZip -SourceDirectory $publishDir -Version $version -Rid $rid
                    Set-PhaseStatus -Name $zipPhase -Status 'done' -Message $zip
                } catch {
                    Set-PhaseStatus -Name $zipPhase -Status 'failed' -Message $_.Exception.Message
                }
            } else {
                Set-PhaseStatus -Name $zipPhase -Status 'failed' -Message 'ioSender.exe missing'
            }

            $ok = (-not (Test-PhaseFailed -PhaseName $phase)) -and
                (-not (Test-PhaseFailed -PhaseName $zipPhase)) -and
                ($zip -and (Test-Path $zip))
            $results += [PSCustomObject]@{
                Label  = "Win zip $rid"
                Status = if ($ok) { 'ok' } else { 'FAIL' }
                Path   = if ($ok) { $zip } else { $null }
            }
        }
    }

    if ($winPackages -contains 'WinInstaller') {
        foreach ($rid in $winRids) {
            $phase = "WinInstaller:$rid"
            $log = Get-PhaseLogPath -PhaseName $phase -Extension 'log'
            $err = Get-PhaseLogPath -PhaseName $phase -Extension 'err'
            $installer = Join-Path $Artifacts "ioSender-Setup-$version-$rid.exe"
            [void](Invoke-PhaseProcess -PhaseName $phase -FilePath $PsHost -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $installerScript, '-RuntimeIdentifier', $rid) -LogPath $log -ErrPath $err -SuccessPath $installer)

            $ok = (-not (Test-PhaseFailed -PhaseName $phase)) -and (Test-Path $installer)
            $results += [PSCustomObject]@{
                Label  = "Win setup $rid"
                Status = if ($ok) { 'ok' } else { 'FAIL' }
                Path   = if ($ok) { $installer } else { $null }
            }
        }
    }

    foreach ($package in $linuxPackages) {
        foreach ($rid in $linuxBuildRids) {
            $phase = "${package}:$rid"
            $log = Get-PhaseLogPath -PhaseName $phase -Extension 'log'
            $err = Get-PhaseLogPath -PhaseName $phase -Extension 'err'
            [void](Invoke-PhaseProcess -PhaseName $phase -FilePath "wsl.exe" -Arguments @('-d', $distro, '--cd', '~', '-e', '/bin/bash', $wslSh, $wslExport, $package, $rid) -LogPath $log -ErrPath $err)

            $artifact = $null
            if (-not (Test-PhaseFailed -PhaseName $phase)) {
                $artifact = Get-LinuxExport -Distro $distro -WindowsExportDir $ExportDir -WslExportDir $wslExport -PackageTarget $package -Rid $rid
                if ($artifact) {
                    Copy-Item $artifact.FullName (Join-Path $Artifacts $artifact.Name) -Force
                    Set-PhaseStatus -Name $phase -Status 'done' -Message $artifact.Name
                } else {
                    Set-PhaseStatus -Name $phase -Status 'failed' -Message 'artifact missing from export'
                }
            }

            $ok = (-not (Test-PhaseFailed -PhaseName $phase)) -and ($null -ne $artifact)
            $results += [PSCustomObject]@{
                Label  = "$package $rid"
                Status = if ($ok) { 'ok' } else { 'FAIL' }
                Path   = if ($ok) { (Join-Path $Artifacts $artifact.Name) } else { $null }
            }
        }
    }

    $totalSw.Stop()
    $buildFailed = (Test-AnyPhaseFailed) -or (@($results | Where-Object { $_.Status -ne 'ok' }).Count -gt 0)

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

    if ($Launch) {
        $launchExe = Join-Path $Artifacts "publish\win-x64\ioSender.exe"
        if (-not (Test-Path $launchExe)) {
            $launchExe = Get-ChildItem (Join-Path $Artifacts "publish") -Filter "ioSender.exe" -Recurse -ErrorAction SilentlyContinue |
                Select-Object -First 1 -ExpandProperty FullName
        }
        if ($launchExe -and (Test-Path $launchExe)) {
            Start-Process $launchExe
        }
    }

    if (-not $NoExplorer) {
        Start-Process explorer.exe $Artifacts
    }
}

$exitCode = 0
try {
    Invoke-ReleaseBuild -BuildTarget $Target
} catch {
    [Console]::Out.WriteLine($_.Exception.Message)
    $exitCode = 1
} finally {
    if (-not $NoPause) {
        Read-Host "`nPress Enter to close"
    }
}

exit $exitCode
