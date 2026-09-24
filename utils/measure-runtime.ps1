<#
.SYNOPSIS
    Measures Thrum's startup time and its CPU, memory and handle use over time.

.DESCRIPTION
    A release check to run on a real Windows PC. It starts and stops Thrum.exe
    and reads public per-process counters. It needs no administrator rights,
    changes nothing on the machine, writes only into -OutDir and sends nothing
    anywhere.

    Startup: starts Thrum -Launches times and times each one from process start
    to "main window shown and ready for input". The first launch after a Windows
    restart is the cold start; pass -ColdStart on that run so the report labels
    it. Later launches are warm starts.

    Soak: starts Thrum once, waits -SettleSeconds, then samples CPU, private
    memory, working set, handles, threads and GDI/USER objects every
    -IntervalSeconds for -Minutes. It reports the growth rate of each so a leak
    shows up as a number rather than an impression. Leave the PC idle for an idle
    soak; play a game with a controller connected for an active soak.

    Thrum is closed between runs with its own "-command shutdown" message, the
    same path the tray menu uses, so a normal exit is also exercised.

.EXAMPLE
    .\measure-runtime.ps1 -ExePath 'C:\Tools\Thrum\Thrum.exe'

.EXAMPLE
    # Right after a restart, to capture the cold start as launch 1:
    .\measure-runtime.ps1 -ExePath 'C:\Tools\Thrum\Thrum.exe' -ColdStart -SkipSoak
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,
    [int]$Launches = 5,
    [int]$Minutes = 30,
    [int]$IntervalSeconds = 10,
    [int]$SettleSeconds = 30,
    [int]$WindowTimeoutSeconds = 60,
    [switch]$ColdStart,
    [switch]$SkipStartup,
    [switch]$SkipSoak,
    [string]$OutDir = (Join-Path ([IO.Path]::GetTempPath()) ('thrum-measure-' + (Get-Date -Format 'yyyyMMdd-HHmmss')))
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$ExePath = (Resolve-Path -LiteralPath $ExePath).Path
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

Add-Type -Namespace ThrumMeasure -Name Native -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern uint GetGuiResources(System.IntPtr hProcess, uint uiFlags);
'@

function Get-ThrumProcess {
    Get-Process -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and ($_.Path -ieq $ExePath) }
}

function Stop-Thrum {
    param([int]$TimeoutSeconds = 30)
    $running = @(Get-ThrumProcess)
    if ($running.Count -eq 0) { return 'not running' }
    # The running instance receives this as WM_COPYDATA and runs its normal
    # close path (disconnect controllers, save, stop the backend it owns).
    Start-Process -FilePath $ExePath -ArgumentList '-command', 'shutdown' -Wait
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (@(Get-ThrumProcess).Count -eq 0) { return 'clean' }
        Start-Sleep -Milliseconds 250
    }
    foreach ($p in @(Get-ThrumProcess)) { $p.Kill() }
    return 'killed after timeout'
}

function Start-ThrumTimed {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $proc = Start-Process -FilePath $ExePath -PassThru
    $deadline = (Get-Date).AddSeconds($WindowTimeoutSeconds)
    $windowMs = $null
    while ((Get-Date) -lt $deadline) {
        $proc.Refresh()
        if ($proc.HasExited) { throw "Thrum exited during startup (exit code $($proc.ExitCode))." }
        if ($proc.MainWindowHandle -ne [IntPtr]::Zero) { $windowMs = $sw.Elapsed.TotalMilliseconds; break }
        Start-Sleep -Milliseconds 15
    }
    if ($null -eq $windowMs) { throw "No main window within $WindowTimeoutSeconds s." }
    [void]$proc.WaitForInputIdle($WindowTimeoutSeconds * 1000)
    $idleMs = $sw.Elapsed.TotalMilliseconds
    [pscustomobject]@{ Process = $proc; WindowMs = [math]::Round($windowMs); InputIdleMs = [math]::Round($idleMs) }
}

function Get-Slope {
    # Least-squares slope of y over x (per unit of x).
    param([double[]]$X, [double[]]$Y)
    $n = $X.Count
    if ($n -lt 2) { return 0 }
    $mx = ($X | Measure-Object -Average).Average
    $my = ($Y | Measure-Object -Average).Average
    $num = 0.0; $den = 0.0
    for ($i = 0; $i -lt $n; $i++) { $num += ($X[$i] - $mx) * ($Y[$i] - $my); $den += ($X[$i] - $mx) * ($X[$i] - $mx) }
    if ($den -eq 0) { return 0 }
    return $num / $den
}

$summary = [ordered]@{
    Exe            = $ExePath
    FileVersion    = (Get-Item -LiteralPath $ExePath).VersionInfo.ProductVersion
    Windows        = (Get-CimInstance Win32_OperatingSystem).Caption + ' ' + [Environment]::OSVersion.Version
    LogicalCpus    = [Environment]::ProcessorCount
    OutDir         = $OutDir
}

if (@(Get-ThrumProcess).Count -gt 0) {
    Write-Host 'Thrum is already running; closing it first.'
    [void](Stop-Thrum)
}

if (-not $SkipStartup) {
    $rows = @()
    for ($i = 1; $i -le $Launches; $i++) {
        $kind = if ($i -eq 1 -and $ColdStart) { 'cold' } else { 'warm' }
        $r = Start-ThrumTimed
        Start-Sleep -Seconds 3
        $exit = Stop-Thrum
        $rows += [pscustomobject]@{ Launch = $i; Kind = $kind; WindowMs = $r.WindowMs; InputIdleMs = $r.InputIdleMs; Exit = $exit }
        Write-Host ("Launch {0} ({1}): window {2} ms, input-idle {3} ms, exit {4}" -f $i, $kind, $r.WindowMs, $r.InputIdleMs, $exit)
        Start-Sleep -Seconds 2
    }
    $rows | Export-Csv -NoTypeInformation -Path (Join-Path $OutDir 'startup.csv')
    $warm = @($rows | Where-Object Kind -eq 'warm' | ForEach-Object InputIdleMs)
    if ($warm.Count -gt 0) {
        $sorted = $warm | Sort-Object
        $summary.WarmStartMedianMs = $sorted[[int][math]::Floor(($sorted.Count - 1) / 2)]
        $summary.WarmStartMaxMs = ($sorted | Measure-Object -Maximum).Maximum
    }
    $cold = @($rows | Where-Object Kind -eq 'cold')
    if ($cold.Count -gt 0) { $summary.ColdStartMs = $cold[0].InputIdleMs }
    $summary.UncleanExits = @($rows | Where-Object Exit -ne 'clean').Count
}

if (-not $SkipSoak) {
    $r = Start-ThrumTimed
    $proc = $r.Process
    Write-Host "Soak: settling for $SettleSeconds s, then sampling every $IntervalSeconds s for $Minutes min."
    Start-Sleep -Seconds $SettleSeconds
    $samples = @()
    $start = Get-Date
    $proc.Refresh()
    $lastCpu = $proc.TotalProcessorTime
    $lastAt = Get-Date
    while (((Get-Date) - $start).TotalMinutes -lt $Minutes) {
        Start-Sleep -Seconds $IntervalSeconds
        $proc.Refresh()
        if ($proc.HasExited) { throw "Thrum exited during the soak (exit code $($proc.ExitCode))." }
        $now = Get-Date
        $cpu = $proc.TotalProcessorTime
        $cpuPct = 100.0 * ($cpu - $lastCpu).TotalMilliseconds / (($now - $lastAt).TotalMilliseconds * [Environment]::ProcessorCount)
        $lastCpu = $cpu; $lastAt = $now
        $samples += [pscustomobject]@{
            Minute       = [math]::Round(($now - $start).TotalMinutes, 2)
            CpuPercent   = [math]::Round($cpuPct, 3)
            PrivateMB    = [math]::Round($proc.PrivateMemorySize64 / 1MB, 2)
            WorkingSetMB = [math]::Round($proc.WorkingSet64 / 1MB, 2)
            Handles      = $proc.HandleCount
            Threads      = $proc.Threads.Count
            GdiObjects   = [ThrumMeasure.Native]::GetGuiResources($proc.Handle, 0)
            UserObjects  = [ThrumMeasure.Native]::GetGuiResources($proc.Handle, 1)
        }
    }
    $samples | Export-Csv -NoTypeInformation -Path (Join-Path $OutDir 'soak.csv')
    $x = [double[]]@($samples | ForEach-Object Minute)
    $summary.IdleCpuAvgPercent = [math]::Round(($samples | Measure-Object CpuPercent -Average).Average, 3)
    $summary.IdleCpuMaxPercent = [math]::Round(($samples | Measure-Object CpuPercent -Maximum).Maximum, 3)
    $summary.PrivateMBStart = $samples[0].PrivateMB
    $summary.PrivateMBEnd = $samples[-1].PrivateMB
    $summary.PrivateMBPerHour = [math]::Round((Get-Slope $x ([double[]]@($samples | ForEach-Object PrivateMB))) * 60, 2)
    $summary.HandlesPerHour = [math]::Round((Get-Slope $x ([double[]]@($samples | ForEach-Object Handles))) * 60, 1)
    $summary.ThreadsPerHour = [math]::Round((Get-Slope $x ([double[]]@($samples | ForEach-Object Threads))) * 60, 1)
    $summary.GdiPerHour = [math]::Round((Get-Slope $x ([double[]]@($samples | ForEach-Object GdiObjects))) * 60, 1)
    $summary.UserPerHour = [math]::Round((Get-Slope $x ([double[]]@($samples | ForEach-Object UserObjects))) * 60, 1)
    $summary.SoakExit = Stop-Thrum
    # A flat process shows slopes near zero. Small positive memory slopes
    # early in a run are normal (caches warming); handles, threads and GDI/USER
    # objects should not climb at all while nothing changes.
    $summary.LeakSuspected = ($summary.HandlesPerHour -gt 20) -or ($summary.ThreadsPerHour -gt 2) -or
        ($summary.GdiPerHour -gt 10) -or ($summary.UserPerHour -gt 10) -or ($summary.PrivateMBPerHour -gt 20)
}

$summaryObject = [pscustomobject]$summary
$summaryObject | ConvertTo-Json | Set-Content -Path (Join-Path $OutDir 'summary.json') -Encoding UTF8
$summaryObject | Format-List
Write-Host "Results written to $OutDir"
