# Build ioSender Windows installer from a self-contained win-x64 publish.
param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$InnoSetupCompiler = ""
)

$ErrorActionPreference = "Stop"

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$Project = Join-Path $Root "ioSender\ioSender.csproj"
$Artifacts = Join-Path $Root "artifacts"
$PublishDir = Join-Path $Artifacts "publish\$RuntimeIdentifier-installer"
$InstallerScript = Join-Path $Root "packaging\windows\iosender.iss"
$IconPath = Join-Path $Root "Icon\iosendericon.ico"

function Resolve-InnoSetupCompiler {
    param([string]$RequestedPath)

    if ($RequestedPath) {
        if (Test-Path $RequestedPath) {
            return (Resolve-Path $RequestedPath).Path
        }
        throw "Inno Setup compiler not found at '$RequestedPath'."
    }

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

    throw "Inno Setup compiler (ISCC.exe) was not found. Install Inno Setup or pass -InnoSetupCompiler <path>."
}

function Get-MsBuildProperty {
    param(
        [string]$PropertyName,
        [string]$Fallback
    )

    $value = (& dotnet msbuild $Project "-getProperty:$PropertyName" -nologo -v:q 2>$null | Select-Object -First 1).Trim()
    if ([string]::IsNullOrWhiteSpace($value)) {
        return $Fallback
    }
    return $value
}

$version = Get-MsBuildProperty -PropertyName "Version" -Fallback "0.0.0"
$outputBaseName = "ioSender-Setup-$version-$RuntimeIdentifier"
$iscc = Resolve-InnoSetupCompiler -RequestedPath $InnoSetupCompiler

New-Item -ItemType Directory -Force -Path $Artifacts | Out-Null
if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null

Write-Host "Publishing self-contained $RuntimeIdentifier to $PublishDir"
dotnet publish $Project `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $PublishDir

$exe = Join-Path $PublishDir "ioSender.exe"
if (-not (Test-Path $exe)) {
    throw "Publish output missing: $exe"
}

Write-Host "Building installer with $iscc"
& $iscc `
    "/DAppVersion=$version" `
    "/DSourceDir=$PublishDir" `
    "/DOutputDir=$Artifacts" `
    "/DOutputBaseFilename=$outputBaseName" `
    "/DIconPath=$IconPath" `
    $InstallerScript

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed (exit $LASTEXITCODE)."
}

$installer = Join-Path $Artifacts "$outputBaseName.exe"
if (-not (Test-Path $installer)) {
    throw "Installer was not created: $installer"
}

Write-Host "Built $installer"
