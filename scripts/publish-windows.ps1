# Publish ioSender for Windows x64
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$OutDir = Join-Path $Root "artifacts\publish\win-x64"
dotnet publish (Join-Path $Root "src\ioSender\ioSender.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -o $OutDir
Write-Host "Published to $OutDir"
