param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$RuntimeIdentifier = "win-x64"
)

# Publish ioSender for Windows.
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$OutDir = Join-Path $Root "artifacts\publish\$RuntimeIdentifier"
$Project = Join-Path $Root "ioSender\ioSender.csproj"

if (Test-Path $OutDir) {
    Remove-Item -LiteralPath $OutDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

dotnet publish $Project `
    -c Release `
    -r $RuntimeIdentifier `
    --self-contained false `
    --force `
    -o $OutDir
Write-Host "Published to $OutDir"
