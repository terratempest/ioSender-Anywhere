# Console UI for ioSender release builds (dot-sourced by build-all.ps1).

$script:BuildConsoleVersion = $null
$script:BuildConsoleRoot = $null
$script:BuildConsoleQuiet = $false
$script:BuildConsolePhases = @{}
$script:BuildConsolePhaseOrder = @()
$script:BuildConsoleBoardLines = 0
$script:BuildConsoleBoardTop = -1
$script:BuildConsoleSpinnerIndex = 0
$script:BuildConsoleSpinners = @('|', '/', '-', '\')
$script:BuildConsoleElapsed = $null

function Test-CanUseConsoleCursor {
    if ($Host.Name -ne 'ConsoleHost') {
        return $false
    }

    try {
        $null = $Host.UI.RawUI.WindowSize
        $null = $Host.UI.RawUI.CursorPosition
        return $true
    } catch {
        return $false
    }
}

function Set-BuildConsoleStopwatch {
    param([System.Diagnostics.Stopwatch]$Stopwatch)
    $script:BuildConsoleElapsed = $Stopwatch
}

function Update-PhaseBoard {
    return
}

function Initialize-BuildConsole {
    param(
        [string]$Version,
        [string]$Root,
        [switch]$Quiet
    )

    $script:BuildConsoleVersion = $Version
    $script:BuildConsoleRoot = $Root
    $script:BuildConsoleQuiet = [bool]$Quiet
}

function Write-BuildLine {
    param(
        [string]$Message
    )

    [Console]::Out.WriteLine($Message)
}

function Write-PlainLine {
    param([string]$Message = "")

    [Console]::Out.WriteLine($Message)
}

function Format-ElapsedShort {
    param([TimeSpan]$Elapsed)

    if ($Elapsed.TotalHours -ge 1) {
        return $Elapsed.ToString('h\:mm\:ss')
    }
    return $Elapsed.ToString('mm\:ss')
}

function Format-ElapsedLong {
    param([TimeSpan]$Elapsed)

    if ($Elapsed.TotalHours -ge 1) {
        return "{0}h {1}m {2}s" -f [int]$Elapsed.TotalHours, $Elapsed.Minutes, $Elapsed.Seconds
    }
    if ($Elapsed.TotalMinutes -ge 1) {
        return "{0}m {1}s" -f [int]$Elapsed.TotalMinutes, $Elapsed.Seconds
    }
    return "{0}s" -f [int]$Elapsed.TotalSeconds
}

function Show-TargetMenu {
    param(
        [string]$WslStatus
    )

    if ($script:BuildConsoleQuiet) {
        throw "Target is required when using -NoPause (quiet mode). Example: -Target All"
    }

    $menuMap = @{
        '1' = 'All'
        '2' = 'Windows'
        '3' = 'Linux'
        '4' = 'WinPortable'
        '5' = 'WinInstaller'
        '6' = 'LinuxDeb'
        '7' = 'LinuxRpm'
        '8' = 'LinuxAppImage'
    }

    while ($true) {
        Write-PlainLine
        Write-PlainLine " ioSender release build"
        Write-PlainLine " Version $($script:BuildConsoleVersion)"
        Write-PlainLine " -------------------------------------------------------"
        Write-PlainLine "  [1] Build all          Windows + Linux packages"
        Write-PlainLine "  [2] Windows            .zip + installer"
        Write-PlainLine ("  [3] Linux              .deb + .rpm + AppImage  WSL: {0}" -f $WslStatus)
        Write-PlainLine "  [4] Windows portable   .zip"
        Write-PlainLine "  [5] Windows installer  .exe"
        Write-PlainLine ("  [6] Linux .deb         WSL: {0}" -f $WslStatus)
        Write-PlainLine ("  [7] Linux .rpm         WSL: {0}" -f $WslStatus)
        Write-PlainLine ("  [8] Linux AppImage     WSL: {0}" -f $WslStatus)
        Write-PlainLine " -------------------------------------------------------"

        $choice = Read-Host "  Choice [1]"
        if ([string]::IsNullOrWhiteSpace($choice)) {
            $choice = '1'
        }

        if ($menuMap.ContainsKey($choice)) {
            return $menuMap[$choice]
        }

        Write-PlainLine "  Invalid choice '$choice'. Enter 1 through 8."
    }
}

function Show-PreflightPanel {
    param(
        [object[]]$Checks
    )

    if ($script:BuildConsoleQuiet) {
        foreach ($check in $Checks) {
            if (-not $check.Ok) {
                throw $check.FailMessage
            }
        }
        return
    }

    Write-PlainLine
    Write-PlainLine " Prerequisites"
    foreach ($check in $Checks) {
        $status = if ($check.Ok) { 'ok' } else { 'FAIL' }
        Write-PlainLine ("  [{0}] {1}: {2}" -f $status, $check.Label, $check.Detail)
        if (-not $check.Ok) {
            throw $check.FailMessage
        }
    }
    Write-PlainLine
}

function Initialize-PhaseBoard {
    param(
        [string[]]$Phases
    )

    $script:BuildConsolePhases = @{}
    $script:BuildConsolePhaseOrder = @($Phases)
    foreach ($name in $Phases) {
        $script:BuildConsolePhases[$name] = [PSCustomObject]@{
            Name      = $name
            Status    = 'pending'
            StartedAt = $null
            LogPath   = $null
            ErrPath   = $null
            Message   = $null
            ExitCode  = $null
        }
    }
    $script:BuildConsoleBoardLines = 0
    $script:BuildConsoleBoardTop = -1
}

function Set-PhaseStatus {
    param(
        [string]$Name,
        [ValidateSet('pending', 'running', 'done', 'failed', 'skipped')]
        [string]$Status,
        [string]$LogPath = $null,
        [string]$ErrPath = $null,
        [string]$Message = $null,
        [int]$ExitCode = $null
    )

    if (-not $script:BuildConsolePhases.ContainsKey($Name)) {
        return
    }

    $phase = $script:BuildConsolePhases[$Name]
    $phase.Status = $Status
    if ($LogPath) { $phase.LogPath = $LogPath }
    if ($ErrPath) { $phase.ErrPath = $ErrPath }
    if ($Message) { $phase.Message = $Message }
    if ($null -ne $ExitCode) { $phase.ExitCode = $ExitCode }

    if ($Status -eq 'running' -and -not $phase.StartedAt) {
        $phase.StartedAt = [DateTime]::UtcNow
    }

    if ($script:BuildConsoleQuiet -or $Status -in @('running', 'done', 'failed', 'skipped')) {
        $label = switch ($Status) {
            'running' { '...' }
            'done'    { 'ok' }
            'failed'  { 'FAIL' }
            'skipped' { 'skip' }
            default   { '   ' }
        }
        Write-PlainLine ("  [{0}] {1}" -f $label, $Name)
        if ($Message) {
            Write-PlainLine "        $Message"
        }
    }
}

function Start-PhaseBoardDisplay {
    return
}

function Get-PhaseDisplayName {
    param([string]$Name)

    switch ($Name) {
        'Preflight'    { return 'Preflight' }
        'Clean'        { return 'Clean' }
        'WslSync'      { return 'WSL sync' }
        'WinPublish'   { return 'Win publish' }
        'WinPortable'  { return 'Win portable' }
        'WinInstaller' { return 'Win installer' }
        'LinuxDeb'     { return 'Linux .deb' }
        'LinuxRpm'     { return 'Linux .rpm' }
        'LinuxAppImage' { return 'Linux AppImage' }
        default        { return $Name }
    }
}

function Get-LogTailLine {
    param(
        [string]$LogPath,
        [string]$ErrPath
    )

    foreach ($path in @($LogPath, $ErrPath)) {
        if (-not $path -or -not (Test-Path $path)) { continue }
        $lines = Get-Content $path -Tail 20 -ErrorAction SilentlyContinue |
            ForEach-Object { $_.TrimEnd() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        if ($lines -and $lines.Count -gt 0) {
            return $lines[-1]
        }
    }
    return $null
}

function Get-PhaseBoardLines {
    param([TimeSpan]$Elapsed)

    $script:BuildConsoleSpinnerIndex = ($script:BuildConsoleSpinnerIndex + 1) % 4
    $spinner = $script:BuildConsoleSpinners[$script:BuildConsoleSpinnerIndex]

    $elapsedText = Format-ElapsedShort $Elapsed
    $lines = @()
    $header = (" ioSender release build  v{0}  {1}" -f $script:BuildConsoleVersion, $elapsedText)
    $lines += $header.PadRight(63).Substring(0, 63)
    $lines += " -----------------------------------------------------------"

    foreach ($name in $script:BuildConsolePhaseOrder) {
        $phase = $script:BuildConsolePhases[$name]
        $display = Get-PhaseDisplayName $name
        $elapsedCol = ''
        $statusText = $phase.Status

        switch ($phase.Status) {
            'running' {
                $statusText = "running $spinner"
                if ($phase.StartedAt) {
                    $phaseElapsed = ([DateTime]::UtcNow - $phase.StartedAt)
                    $elapsedCol = (Format-ElapsedShort $phaseElapsed).PadLeft(8)
                }
            }
            'done' { $statusText = 'done' }
            'failed' { $statusText = 'FAIL' }
            'skipped' { $statusText = 'skipped' }
            default { $statusText = 'pending' }
        }

        $lines += ("  {0,-16} {1,-12}{2}" -f $display, $statusText, $elapsedCol)
    }

    $lines += " -----------------------------------------------------------"

    $tails = @()
    foreach ($name in $script:BuildConsolePhaseOrder) {
        $phase = $script:BuildConsolePhases[$name]
        if ($phase.Status -ne 'running') { continue }
        $tail = Get-LogTailLine -LogPath $phase.LogPath -ErrPath $phase.ErrPath
        if ($tail) {
            $display = Get-PhaseDisplayName $name
            if ($tail.Length -gt 72) {
                $tail = $tail.Substring($tail.Length - 72)
            }
            $tails += ("  {0}  > {1}" -f $display.PadRight(12), $tail)
        }
    }
    if ($tails.Count -eq 0) {
        $lines += "  (waiting for build output...)"
    } else {
        $lines += $tails
    }

    return $lines
}

function Write-PhaseBoardLines {
    param([string[]]$Lines)

    Write-PlainLine
    foreach ($line in $Lines) {
        Write-PlainLine $line
    }
    $script:BuildConsoleBoardLines = $Lines.Count
}

function Render-PhaseBoard {
    param(
        [TimeSpan]$Elapsed,
        [System.Diagnostics.Process[]]$RunningProcesses = @()
    )

    return
}

function Finish-PhaseBoard {
    return
}

function Wait-BuildJobs {
    param(
        [hashtable]$JobMap,
        [TimeSpan]$PollInterval = [TimeSpan]::FromSeconds(1)
    )

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $processes = @($JobMap.Values | ForEach-Object { $_.Process } | Where-Object { $_ })

    while ($true) {
        $anyRunning = $false
        foreach ($entry in $JobMap.GetEnumerator()) {
            $phaseName = $entry.Key
            $job = $entry.Value
            if (-not $job.Process) { continue }

            $job.Process.Refresh()
            if (-not $job.Process.HasExited) {
                $anyRunning = $true
                if ($script:BuildConsolePhases[$phaseName].Status -ne 'running') {
                    Set-PhaseStatus -Name $phaseName -Status 'running' -LogPath $job.LogPath -ErrPath $job.ErrPath
                }
            }
        }

        $elapsed = if ($script:BuildConsoleElapsed) {
            $script:BuildConsoleElapsed.Elapsed
        } else {
            $sw.Elapsed
        }
        Render-PhaseBoard -Elapsed $elapsed

        if (-not $anyRunning) {
            break
        }
        Start-Sleep -Milliseconds $PollInterval.TotalMilliseconds
    }

    foreach ($entry in $JobMap.GetEnumerator()) {
        $phaseName = $entry.Key
        $job = $entry.Value
        if (-not $job.Process) { continue }

        $job.Process.WaitForExit()
        $job.Process.Refresh()
        $exit = $job.Process.ExitCode
        if ($null -eq $exit) {
            $exit = 0
        }

        $ok = ($exit -eq 0)
        if ($ok -and $job.RequireDeb) {
            $ok = $null -ne (
                Get-ChildItem $job.SuccessPath -Filter 'iosender_*.deb' -ErrorAction SilentlyContinue |
                Select-Object -First 1
            )
        } elseif ($ok -and $job.SuccessPath) {
            $ok = Test-Path $job.SuccessPath
        }

        Set-PhaseStatus -Name $phaseName -Status $(if ($ok) { 'done' } else { 'failed' }) -ExitCode $exit
    }

    $elapsed = if ($script:BuildConsoleElapsed) {
        $script:BuildConsoleElapsed.Elapsed
    } else {
        $sw.Elapsed
    }
    Render-PhaseBoard -Elapsed $elapsed

    return $sw.Elapsed
}

function Show-FailureLogs {
    param(
        [int]$TailLines = 25,
        [switch]$Verbose
    )

    foreach ($name in $script:BuildConsolePhaseOrder) {
        $phase = $script:BuildConsolePhases[$name]
        if ($phase.Status -ne 'failed') { continue }

        Write-PlainLine
        Write-PlainLine " --- $name failed ---"
        if ($null -ne $phase.ExitCode) {
            Write-PlainLine " exit code: $($phase.ExitCode)"
        }

        $paths = @($phase.LogPath, $phase.ErrPath) | Where-Object { $_ -and (Test-Path $_) }
        foreach ($path in $paths) {
            Write-PlainLine " log: $path"
            if ($Verbose) {
                Get-Content $path | ForEach-Object { Write-PlainLine $_ }
            } else {
                Get-Content $path -Tail $TailLines | ForEach-Object { Write-PlainLine $_ }
            }
        }
    }
}

function Show-BuildSummary {
    param(
        [object[]]$Results,
        [TimeSpan]$Elapsed,
        [switch]$Failed
    )

    Write-PlainLine
    if ($Failed) {
        Write-PlainLine (" -- Build failed ({0}) --" -f (Format-ElapsedLong $Elapsed))
    } else {
        Write-PlainLine (" -- Build complete ({0}) --" -f (Format-ElapsedLong $Elapsed))
    }
    Write-PlainLine " -----------------------------------------------------------"
    Write-PlainLine ("  {0,-24} {1,-8}" -f 'Artifact', 'Status')

    foreach ($result in $Results) {
        $status = $result.Status
        $path = if ($result.Path) { $result.Path } else { '-' }
        Write-PlainLine ("  {0,-24} {1,-8}" -f $result.Label, $status)
        Write-PlainLine ("      {0}" -f $path)
    }

    Write-PlainLine " -----------------------------------------------------------"
}

function Show-VerbosePhaseLogs {
    foreach ($name in $script:BuildConsolePhaseOrder) {
        $phase = $script:BuildConsolePhases[$name]
        if ($phase.Status -notin @('done', 'failed')) { continue }
        foreach ($path in @($phase.LogPath, $phase.ErrPath)) {
            if (-not $path -or -not (Test-Path $path)) { continue }
            Write-PlainLine
            Write-PlainLine " --- $name : $path ---"
            Get-Content $path | ForEach-Object { Write-PlainLine $_ }
        }
    }
}
