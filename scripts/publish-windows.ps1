# Publish ioSender for Windows x64
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$OutDir = Join-Path $Root "artifacts\publish\win-x64"
$Project = Join-Path $Root "ioSender\ioSender.csproj"

if (Test-Path $OutDir) {
    Remove-Item -LiteralPath $OutDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

dotnet publish $Project `
    -c Release `
    -r win-x64 `
    --self-contained false `
    --force `
    -o $OutDir
Write-Host "Published to $OutDir"
