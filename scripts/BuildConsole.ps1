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
    if ($script:BuildConsoleQuiet -or $script:BuildConsolePhaseOrder.Count -eq 0) {
        return
    }
    $elapsed = if ($script:BuildConsoleElapsed) {
        $script:BuildConsoleElapsed.Elapsed
    } else {
        [TimeSpan]::Zero
    }
    Render-PhaseBoard -Elapsed $elapsed
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
        [string]$Message,
        [ConsoleColor]$ForegroundColor = [ConsoleColor]::Gray
    )

    Write-Host $Message -ForegroundColor $ForegroundColor
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
        '2' = 'WinPortable'
        '3' = 'WinInstaller'
        '4' = 'LinuxDeb'
    }

    while ($true) {
        Write-Host ""
        Write-Host " ioSender release build" -ForegroundColor Yellow
        Write-Host " Version $($script:BuildConsoleVersion)"
        Write-Host " -------------------------------------------------------"
        Write-Host "  [1] Build all          portable + installer + .deb"
        Write-Host "  [2] Windows portable   .zip"
        Write-Host "  [3] Windows installer  .exe"
        Write-Host ("  [4] Linux .deb         WSL: {0}" -f $WslStatus)
        Write-Host " -------------------------------------------------------"

        $choice = Read-Host "  Choice [1]"
        if ([string]::IsNullOrWhiteSpace($choice)) {
            $choice = '1'
        }

        if ($menuMap.ContainsKey($choice)) {
            return $menuMap[$choice]
        }

        Write-Host "  Invalid choice '$choice'. Enter 1, 2, 3, or 4." -ForegroundColor Red
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

    Write-Host ""
    Write-Host " Prerequisites"
    foreach ($check in $Checks) {
        $status = if ($check.Ok) { 'ok' } else { 'FAIL' }
        $color = if ($check.Ok) { 'Green' } else { 'Red' }
        Write-Host ("   {0,-12} {1,-12} {2}" -f $check.Label, $check.Detail, $status) -ForegroundColor $color
        if (-not $check.Ok) {
            throw $check.FailMessage
        }
    }
    Write-Host ""
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

    if ($script:BuildConsoleQuiet) {
        $label = switch ($Status) {
            'running' { '...' }
            'done'    { 'ok' }
            'failed'  { 'FAIL' }
            'skipped' { 'skip' }
            default   { '   ' }
        }
        Write-Host ("  [{0}] {1}" -f $label, $Name)
        if ($Message) {
            Write-Host "        $Message"
        }
    } elseif ($script:BuildConsoleBoardTop -ge 0) {
        Update-PhaseBoard
    } elseif ($Status -in @('running', 'done', 'failed', 'skipped')) {
        Write-Host ("  {0}: {1}" -f (Get-PhaseDisplayName $Name), $Status)
        if ($Message) {
            Write-Host "        $Message"
        }
    }
}

function Start-PhaseBoardDisplay {
    if ($script:BuildConsoleQuiet -or $script:BuildConsoleBoardTop -ge 0) {
        return
    }

    $elapsed = if ($script:BuildConsoleElapsed) {
        $script:BuildConsoleElapsed.Elapsed
    } else {
        [TimeSpan]::Zero
    }
    Render-PhaseBoard -Elapsed $elapsed
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

    if (-not (Test-CanUseConsoleCursor)) {
        if ($script:BuildConsoleBoardTop -lt 0) {
            Write-Host ""
            foreach ($line in $Lines) {
                Write-Host $line
            }
            $script:BuildConsoleBoardTop = 0
            $script:BuildConsoleBoardLines = $Lines.Count
        }
        return
    }

    $width = [Math]::Max(80, $Host.UI.RawUI.WindowSize.Width)

    if ($script:BuildConsoleBoardTop -lt 0) {
        Write-Host ""
        $script:BuildConsoleBoardTop = $Host.UI.RawUI.CursorPosition.Y
        foreach ($line in $Lines) {
            Write-Host $line
        }
        $script:BuildConsoleBoardLines = $Lines.Count
        return
    }

    $rowCount = [Math]::Max($Lines.Count, $script:BuildConsoleBoardLines)
    for ($i = 0; $i -lt $rowCount; $i++) {
        $y = $script:BuildConsoleBoardTop + $i
        if ($y -ge ($Host.UI.RawUI.BufferSize.Height - 1)) {
            break
        }

        $Host.UI.RawUI.CursorPosition = New-Object System.Management.Automation.Host.Coordinates(0, $y)
        $text = if ($i -lt $Lines.Count) { $Lines[$i] } else { '' }
        if ($text.Length -gt ($width - 1)) {
            $text = $text.Substring(0, $width - 1)
        }
        Write-Host ($text.PadRight($width - 1)) -NoNewline
    }

    $afterY = [Math]::Min(
        $script:BuildConsoleBoardTop + $Lines.Count,
        $Host.UI.RawUI.BufferSize.Height - 1
    )
    $Host.UI.RawUI.CursorPosition = New-Object System.Management.Automation.Host.Coordinates(0, $afterY)
    $script:BuildConsoleBoardLines = $Lines.Count
}

function Render-PhaseBoard {
    param(
        [TimeSpan]$Elapsed,
        [System.Diagnostics.Process[]]$RunningProcesses = @()
    )

    if ($script:BuildConsoleQuiet) {
        return
    }

    $lines = Get-PhaseBoardLines -Elapsed $Elapsed
    Write-PhaseBoardLines -Lines $lines
}

function Finish-PhaseBoard {
    if ($script:BuildConsoleQuiet) {
        return
    }

    if ($script:BuildConsoleBoardTop -ge 0) {
        Write-Host ""
        $script:BuildConsoleBoardTop = -1
        $script:BuildConsoleBoardLines = 0
    }
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

        Write-Host ""
        Write-Host " --- $name failed ---" -ForegroundColor Red
        if ($null -ne $phase.ExitCode) {
            Write-Host " exit code: $($phase.ExitCode)" -ForegroundColor DarkRed
        }

        $paths = @($phase.LogPath, $phase.ErrPath) | Where-Object { $_ -and (Test-Path $_) }
        foreach ($path in $paths) {
            Write-Host " log: $path" -ForegroundColor DarkGray
            if ($Verbose) {
                Get-Content $path | Write-Host
            } else {
                Get-Content $path -Tail $TailLines | Write-Host
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

    Write-Host ""
    if ($Failed) {
        Write-Host (" -- Build failed ({0}) --" -f (Format-ElapsedLong $Elapsed)) -ForegroundColor Red
    } else {
        Write-Host (" -- Build complete ({0}) --" -f (Format-ElapsedLong $Elapsed)) -ForegroundColor Green
    }
    Write-Host " -----------------------------------------------------------"
    Write-Host ("  {0,-20} {1,-8} Path" -f 'Artifact', 'Status')

    foreach ($result in $Results) {
        $status = $result.Status
        $color = switch ($status) {
            'ok' { 'Green' }
            'FAIL' { 'Red' }
            'skip' { 'DarkGray' }
            default { 'Gray' }
        }
        $path = if ($result.Path) { $result.Path } else { '-' }
        Write-Host ("  {0,-20} {1,-8} {2}" -f $result.Label, $status, $path) -ForegroundColor $color
    }

    Write-Host " -----------------------------------------------------------"
}

function Show-VerbosePhaseLogs {
    foreach ($name in $script:BuildConsolePhaseOrder) {
        $phase = $script:BuildConsolePhases[$name]
        if ($phase.Status -notin @('done', 'failed')) { continue }
        foreach ($path in @($phase.LogPath, $phase.ErrPath)) {
            if (-not $path -or -not (Test-Path $path)) { continue }
            Write-Host ""
            Write-Host " --- $name : $path ---" -ForegroundColor DarkGray
            Get-Content $path | Write-Host
        }
    }
}
