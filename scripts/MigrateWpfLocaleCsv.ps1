# Merges CNC.Controls.WPF locale CSV rows into Avalonia/Config CSVs without dropping coverage.
# Run from repo root: pwsh scripts/MigrateWpfLocaleCsv.ps1

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location $repoRoot

$configPages = [System.Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
@(
    'appconfigview', 'basicconfigcontrol', 'grblconfigcontrol', 'grblconfigview',
    'jogconfigcontrol', 'joguiconfigcontrol', 'steppercalibrationwizard',
    'stripgcodeconfigcontrol', 'trinamicview', 'pidtunercontrol'
) | ForEach-Object { [void]$configPages.Add($_) }

function Get-PageFromLine([string]$line) {
    if ($line -notmatch '^[^:]+:([^,]+)\.baml,') { return $null }
    return $Matches[1].ToLowerInvariant()
}

function Get-TargetAssembly([string]$page) {
    if ($page -and $configPages.Contains($page)) { return 'CNC.Controls.Config' }
    return 'CNC.Controls.Avalonia'
}

function Transform-WpfLine([string]$line, [string]$culture) {
    if ([string]::IsNullOrWhiteSpace($line) -or $line[0] -eq '#') { return $line }
    $page = Get-PageFromLine $line
    if (-not $page) { return $line }

    $assembly = Get-TargetAssembly $page
    $out = $line -replace 'CNC\.Controls\.WPF', $assembly
    # Normalize embedded culture marker for consistency (loader ignores it).
    $out = $out -replace '\.g\.en-US\.resources', ".g.$culture.resources"
    return $out
}

function Get-LineKey([string]$line) {
    $comma = $line.IndexOf(',')
    if ($comma -lt 0) { return $line }
    $second = $line.IndexOf(',', $comma + 1)
    if ($second -lt 0) { return $line }
    return $line.Substring(0, $second)
}

function Read-LinesFromGit([string]$gitPath) {
    $text = git show "HEAD:$gitPath" 2>$null
    if (-not $text) { return @() }
    return $text -split "`n" | ForEach-Object { $_.TrimEnd("`r") }
}

function Read-LinesFromDisk([string]$path) {
    if (-not (Test-Path $path)) { return @() }
    return Get-Content -Path $path -Encoding UTF8
}

$cultures = Get-ChildItem -Path 'Locale' -Directory | ForEach-Object { $_.Name }
$stats = @()

foreach ($culture in $cultures) {
    $csvDir = Join-Path (Join-Path 'Locale' $culture) 'csv'
    if (-not (Test-Path $csvDir)) { continue }

    $wpfGit = "Locale/$culture/csv/CNC.Controls.WPF.resources.$culture.csv"
    $wpfLines = Read-LinesFromDisk (Join-Path $csvDir "CNC.Controls.WPF.resources.$culture.csv")
    if ($wpfLines.Count -eq 0) { $wpfLines = Read-LinesFromGit $wpfGit }

    $ruExtraLines = @()
    if ($culture -eq 'ru-RU') {
        $ruExtraLines = Read-LinesFromDisk (Join-Path $csvDir 'CNC.Controls.WPF.resources.CSV')
        if ($ruExtraLines.Count -eq 0) { $ruExtraLines = Read-LinesFromGit 'Locale/ru-RU/csv/CNC.Controls.WPF.resources.CSV' }
    }

    $avaloniaPath = Join-Path $csvDir ("CNC.Controls.Avalonia.resources.{0}.csv" -f $culture)
    $avaloniaLines = Read-LinesFromDisk $avaloniaPath

    $avaloniaMap = [System.Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    $configMap = [System.Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)

    foreach ($line in $wpfLines) {
        $t = Transform-WpfLine $line $culture
        if ([string]::IsNullOrWhiteSpace($t)) { continue }
        $key = Get-LineKey $t
        $page = Get-PageFromLine $t
        $map = if ($page -and $configPages.Contains($page)) { $configMap } else { $avaloniaMap }
        $map[$key] = $t
    }

    foreach ($line in $ruExtraLines) {
        $t = Transform-WpfLine $line $culture
        if ([string]::IsNullOrWhiteSpace($t)) { continue }
        $key = Get-LineKey $t
        $page = Get-PageFromLine $t
        $map = if ($page -and $configPages.Contains($page)) { $configMap } else { $avaloniaMap }
        $map[$key] = $t
    }

    foreach ($line in $avaloniaLines) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line[0] -eq '#') { continue }
        $key = Get-LineKey $line
        $page = Get-PageFromLine $line
        $map = if ($page -and $configPages.Contains($page)) { $configMap } else { $avaloniaMap }
        $map[$key] = $line
    }

    $avaloniaOut = Join-Path $csvDir "CNC.Controls.Avalonia.resources.$culture.csv"
    $configOut = Join-Path $csvDir "CNC.Controls.Config.resources.$culture.csv"

    $avaloniaSorted = $avaloniaMap.Values | Sort-Object
    $configSorted = $configMap.Values | Sort-Object

    [System.IO.File]::WriteAllLines($avaloniaOut, $avaloniaSorted, [System.Text.UTF8Encoding]::new($false))
    if ($configSorted.Count -gt 0) {
        [System.IO.File]::WriteAllLines($configOut, $configSorted, [System.Text.UTF8Encoding]::new($false))
    }

    $stats += [pscustomobject]@{
        Culture = $culture
        WpfSource = $wpfLines.Count
        RuExtra = $ruExtraLines.Count
        AvaloniaPrior = $avaloniaLines.Count
        AvaloniaOut = $avaloniaSorted.Count
        ConfigOut = $configSorted.Count
    }
}

$stats | Format-Table -AutoSize
Write-Host "Done. WPF CSV rows merged into Avalonia/Config resource files."
