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
$IntermediateDir = if ($IntermediateDir) { $IntermediateDir } else { Join-Path $Artifacts "msbuild\publish\$RuntimeIdentifier" }
$BaseIntermediateOutputPath = Join-Path $IntermediateDir "obj"
$BaseOutputPath = Join-Path $IntermediateDir "bin"
$Project = Join-Path $Root "ioSender\ioSender.csproj"

if (Test-Path $OutDir) {
    Remove-Item -LiteralPath $OutDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
New-Item -ItemType Directory -Force -Path $BaseIntermediateOutputPath | Out-Null
New-Item -ItemType Directory -Force -Path $BaseOutputPath | Out-Null

dotnet publish $Project `
    -c Release `
    -r $RuntimeIdentifier `
    --self-contained false `
    -p:BaseIntermediateOutputPath=$BaseIntermediateOutputPath `
    -p:BaseOutputPath=$BaseOutputPath `
    --force `
    -o $OutDir
Write-Host "Published to $OutDir"
