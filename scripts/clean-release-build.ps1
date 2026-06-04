# Remove intermediate and publish outputs so release builds always compile from current source.
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('All', 'WinPortable', 'WinInstaller', 'LinuxDeb')]
    [string]$Target,
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$Solution = Join-Path $Root "ioSender.net.sln"
$Artifacts = Join-Path $Root "artifacts"

function Get-PublishDirsForTarget {
    param([string]$BuildTarget)

    switch ($BuildTarget) {
        'All' { return @('win-x64', 'win-x64-installer', 'linux-x64') }
        'WinPortable' { return @('win-x64') }
        'WinInstaller' { return @('win-x64-installer') }
        'LinuxDeb' { return @('linux-x64') }
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

$debStaging = Join-Path $Root "packaging\debian-staging"
if (Test-Path $debStaging) {
    Remove-Item -LiteralPath $debStaging -Recurse -Force
    if (-not $Quiet) {
        Write-Host "  removed $debStaging"
    }
}

if (-not $Quiet) {
    Write-Host "Clean complete."
}
