param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$RuntimeIdentifier = "win-x64",
[string]$PublishDir = "",
[string]$IntermediateDir = ""
)

# Publish ioSender for Windows.
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Artifacts = Join-Path $Root "artifacts"
$OutDir = if ($PublishDir) { $PublishDir } else { Join-Path $Artifacts "publish\$RuntimeIdentifier" }
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

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for $RuntimeIdentifier (exit $LASTEXITCODE)."
}

Write-Host "Published to $OutDir"
