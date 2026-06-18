# Remove intermediate and publish outputs so release builds always compile from current source.
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('All', 'Windows', 'Linux', 'WinPortable', 'WinInstaller', 'LinuxDeb', 'LinuxRpm', 'LinuxAppImage')]
    [string]$Target,
    [Alias('Rid')]
    [ValidateSet('', 'win-x64', 'win-arm64', 'linux-x64', 'linux-arm64')]
    [string]$RuntimeIdentifier = '',
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$Solution = Join-Path $Root "ioSender.net.sln"
$Artifacts = Join-Path $Root "artifacts"

function Get-PublishDirsForTarget {
    param([string]$BuildTarget)

    $windows = @('win-x64', 'win-arm64')
    $linux = @('linux-x64', 'linux-arm64')
    if ($RuntimeIdentifier) {
        if ($RuntimeIdentifier.StartsWith('win')) {
            $windows = @($RuntimeIdentifier)
            $linux = @()
        } elseif ($RuntimeIdentifier.StartsWith('linux')) {
            $windows = @()
            $linux = @($RuntimeIdentifier)
        }
    }
    $windowsInstallers = @($windows | ForEach-Object { "$_-installer" })

    switch ($BuildTarget) {
        'All' { return @($windows + $windowsInstallers + $linux) }
        'Windows' { return @($windows + $windowsInstallers) }
        'Linux' { return @($linux) }
        'WinPortable' { return @($windows) }
        'WinInstaller' { return @($windowsInstallers) }
        'LinuxDeb' { return @($linux) }
        'LinuxRpm' { return @($linux) }
        'LinuxAppImage' { return @($linux) }
    }
    return @()
}

function Remove-BuildTreeDirs {
    param([string]$SearchRoot)

    Get-ChildItem -Path $SearchRoot -Directory -Recurse -Force -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Name -in @('bin', 'obj') -and
            $_.FullName -notmatch '[\\/]\.git[\\/]'
        } |
        ForEach-Object {
            Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
        }
}

if (-not $Quiet) {
    Write-Host "Cleaning Release build outputs and intermediates..."
}

& dotnet clean $Solution -c Release --nologo -v:q
if ($LASTEXITCODE -ne 0) {
    throw "dotnet clean failed (exit $LASTEXITCODE)."
}

Remove-BuildTreeDirs -SearchRoot $Root

foreach ($name in (Get-PublishDirsForTarget -BuildTarget $Target)) {
    $publishDir = Join-Path $Artifacts "publish\$name"
    if (Test-Path $publishDir) {
        Remove-Item -LiteralPath $publishDir -Recurse -Force
        if (-not $Quiet) {
            Write-Host "  removed $publishDir"
        }
    }
}

$stagingNames = @('debian-staging', 'rpm-staging', 'appimage-staging')
foreach ($name in $stagingNames) {
    $staging = Join-Path $Root "packaging\$name"
    if (Test-Path $staging) {
        Remove-Item -LiteralPath $staging -Recurse -Force
        if (-not $Quiet) {
            Write-Host "  removed $staging"
        }
    }
}

if (-not $Quiet) {
    Write-Host "Clean complete."
}
