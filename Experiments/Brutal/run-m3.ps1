# M3 run of the BRUTAL evaluation (GuitarAlchemist/gaia#257): how HallSlice's frame rate affects a Godot view running
# beside it. After the memory gate and one build, it runs HallSlice once with vsync for -SoakSeconds, then once per frame
# cap in -Caps (0 = uncapped) for -Seconds each, with -Pause seconds between runs. For each run it records HallSlice's
# frame rate and frame times (present to present), its CPU, the GPU (whole card, sampled each second) and, from -FpsLog
# (a log where the view prints "FPSLOG <local time> <fps>" each second), the view's frame rate during the run and in the
# pause before it. A cap of -1 is a control: the same length with no HallSlice, for the view's own variation. The view
# holds when its minimum stays within -Tolerance of -MinFps (its counter alternates 59 and 61 on its own).
# -SoakSeconds 0 skips the vsync run. The evidence goes to evidence/m3-<stamp>.md. Windows only.
# Usage: pwsh -File run-m3.ps1 -FpsLog <path> [-Caps 60,120,240,480,1000,0] [-Seconds 15] [-SoakSeconds 60] [-Pause 6]
#        [-MinFps 60] [-Tolerance 2] [-MaxCommitGB 125] [-MinFreeGB 8]
param([Parameter(Mandatory)][string]$FpsLog, [int[]]$Caps = @(60, 120, 240, 480, 1000, 0), [int]$Seconds = 15,
    [int]$SoakSeconds = 60, [int]$Pause = 6, [double]$MinFps = 60, [double]$Tolerance = 2, [double]$MaxCommitGB = 125,
    [double]$MinFreeGB = 8)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$proj = Join-Path $root 'HallSlice'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$out = Join-Path $root 'evidence'
New-Item -ItemType Directory -Force $out | Out-Null
$md = Join-Path $out "m3-$stamp.md"
$lines = [System.Collections.Generic.List[string]]::new()
function Note([string]$s) { $lines.Add($s); Write-Host $s }
function Memory {
    $os = Get-CimInstance Win32_OperatingSystem
    [pscustomobject]@{
        CommitGB = [math]::Round(($os.TotalVirtualMemorySize - $os.FreeVirtualMemory) / 1MB, 1)
        LimitGB = [math]::Round($os.TotalVirtualMemorySize / 1MB, 1)
        FreeGB = [math]::Round($os.FreePhysicalMemory / 1MB, 1)
    }
}
function Gpu { (nvidia-smi --query-gpu=utilization.gpu,memory.used,power.draw --format=csv,noheader,nounits) -split ',\s*' | ForEach-Object { [double]$_ } }
function Median($xs) { $s = @($xs | Sort-Object); if ($s.Count) { $s[[int][math]::Floor($s.Count / 2)] } else { 'n/a' } }
# The view's FPSLOG values stamped within [from, to] (whole local seconds).
function ViewFps([datetime]$from, [datetime]$to) {
    @(Select-String -Path $FpsLog -Pattern '^FPSLOG (\S+) ([\d.]+)' | ForEach-Object {
            $at = [datetime]::ParseExact($_.Matches[0].Groups[1].Value, 'yyyy-MM-ddTHH:mm:ss', $null)
            if ($at -ge $from -and $at -le $to) { [double]$_.Matches[0].Groups[2].Value }
        })
}
function Write-Evidence { Set-Content -Path $md -Value $lines -Encoding utf8; Write-Host "evidence: $md" }

Note "# M3 run, $(Get-Date -Format 'yyyy-MM-dd HH:mm zzz')"
Note ''
Note "- Revision: $(git -C $root rev-parse --short HEAD) on $(git -C $root branch --show-current) (uncommitted changes: $([bool](git -C $root status --porcelain -- .)))"
Push-Location $proj
Note "- .NET SDK: $(dotnet --version)"
Pop-Location
Note "- GPU: $((nvidia-smi --query-gpu=name,driver_version --format=csv,noheader) -join '; ')"
$before = Memory
Note "- Memory before: commit $($before.CommitGB) / $($before.LimitGB) GB, free RAM $($before.FreeGB) GB (budget: commit <= $MaxCommitGB GB, free >= $MinFreeGB GB)"
$godot = @(Get-Process -Name 'godot*' -ErrorAction SilentlyContinue | ForEach-Object Id)
Note "- Godot processes before (the view must be left alone): $($godot -join ', ')"
Note "- Plan: vsync for $SoakSeconds s (0 = skipped), then $(($Caps | ForEach-Object { if ($_ -gt 0) { "cap $_" } elseif ($_ -eq 0) { 'uncapped' } else { 'control' } }) -join ', ') for $Seconds s each, $Pause s apart; the view holds when its minimum is >= $($MinFps - $Tolerance) fps"
if ($before.CommitGB -gt $MaxCommitGB -or $before.FreeGB -lt $MinFreeGB) {
    Note "- BLOCKED by the memory gate: nothing built or run"
    Write-Evidence
    exit 2
}

Push-Location $proj
$build = dotnet build -c Release -nologo 2>&1
$code = $LASTEXITCODE
Pop-Location
if ($code -ne 0) {
    Note "- Build FAILED (exit $code):"
    $build | Select-Object -Last 6 | ForEach-Object { Note "    $_" }
    Write-Evidence
    exit 1
}
Note '- Build: PASS'

$exe = Join-Path $proj 'bin\Release\net10.0\HallSlice.exe'
$shell = New-Object -ComObject WScript.Shell
$rows = [System.Collections.Generic.List[string]]::new()
$logs = [System.Collections.Generic.List[string]]::new()
function RunOnce([string]$label, [string[]]$extra, [int]$secs) {
    Start-Sleep -Seconds $Pause
    $log = [IO.Path]::GetTempFileName()
    $start = Get-Date
    $p = Start-Process $exe -ArgumentList (@("$secs") + $extra) -RedirectStandardOutput $log -PassThru -WindowStyle Normal
    Start-Sleep -Milliseconds 1500
    # The focus goes to HallSlice, as in M2, so every run differs from the others by its frame rate only.
    $focused = $shell.AppActivate('Hall slice (BRUTAL)')
    $samples = [System.Collections.Generic.List[double[]]]::new()
    while (-not $p.HasExited -and ((Get-Date) - $start).TotalSeconds -lt $secs + 20) { $samples.Add((Gpu)); Start-Sleep -Milliseconds 700 }
    if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }
    $p.WaitForExit()
    $end = Get-Date
    $o = @(Get-Content $log)
    Remove-Item $log
    $logs.Add("### $label (exit $($p.ExitCode), focus taken: $focused)")
    $o | Where-Object { $_ -match '^M2 (frame cap|present mode|first frame|frames|CPU|closed)' } | ForEach-Object { $logs.Add("    $_") }
    $f = [regex]::Match($o -join "`n", 'M2 frames \d+ in [\d.]+ s \((\d+) fps\); frame ms p50 ([\d.]+) p95 [\d.]+ p99 ([\d.]+) max ([\d.]+)')
    $cpu = [regex]::Match($o -join "`n", 'M2 CPU .*?\(([\d.]+) % of').Groups[1].Value
    $hs = if ($f.Success) { "$($f.Groups[1].Value) | $($f.Groups[2].Value) / $($f.Groups[3].Value) / $($f.Groups[4].Value)" } else { 'FAIL: no frame line | ' }
    AddRow $label $hs $cpu $start $end $samples
}
# A control: the same pause and length with no HallSlice, for the view's own variation.
function Idle([int]$secs) {
    Start-Sleep -Seconds $Pause
    $start = Get-Date
    $samples = [System.Collections.Generic.List[double[]]]::new()
    while (((Get-Date) - $start).TotalSeconds -lt $secs) { $samples.Add((Gpu)); Start-Sleep -Milliseconds 700 }
    AddRow 'control (no HallSlice)' 'none | none' 'none' $start (Get-Date) $samples
}
function AddRow([string]$label, [string]$hs, [string]$cpu, [datetime]$start, [datetime]$end, $samples) {
    $view = ViewFps $start.AddSeconds(1) $end
    $base = ViewFps $start.AddSeconds(1 - $Pause) $start.AddSeconds(-1)
    $util = if ($samples.Count) { [math]::Round(($samples | ForEach-Object { $_[0] } | Measure-Object -Average).Average) } else { 'n/a' }
    $watts = if ($samples.Count) { [math]::Round(($samples | ForEach-Object { $_[2] } | Measure-Object -Average).Average) } else { 'n/a' }
    $min = if ($view.Count) { ($view | Measure-Object -Minimum).Minimum } else { $null }
    $verdict = if ($null -eq $min) { 'NOT RUN: no FPSLOG line' } elseif ($min -ge $MinFps - $Tolerance) { 'holds' } else { '**drops**' }
    $rows.Add("| $(Get-Date $start -Format 'HH:mm:ss') $label | $hs | $cpu | $(if ($null -ne $min) { $min } else { 'n/a' }) / $(Median $view) ($($view.Count) s) | $(Median $base) | $util | $watts | $verdict |")
    Write-Host $rows[-1]
}

if ($SoakSeconds -gt 0) { RunOnce 'vsync' @('--vsync') $SoakSeconds }
foreach ($c in $Caps) {
    if ($c -gt 0) { RunOnce "cap $c" @('--cap', "$c") $Seconds } elseif ($c -eq 0) { RunOnce 'uncapped' @() $Seconds } else { Idle $Seconds }
}

$left = @(Get-Process -Name 'HallSlice' -ErrorAction SilentlyContinue)
$godotAfter = @(Get-Process -Name 'godot*' -ErrorAction SilentlyContinue | ForEach-Object Id)
$after = Memory
Note "- Leftover HallSlice processes: $(if ($left.Count) { "FAIL: $($left.Id -join ', ')" } else { 'none (PASS)' })"
Note "- Godot processes after: $(if (-not (Compare-Object $godot $godotAfter)) { 'unchanged (PASS)' } else { "FAIL: $($godotAfter -join ', ')" })"
Note "- Memory after: commit $($after.CommitGB) GB, free RAM $($after.FreeGB) GB"
Note ''
Note '## Results'
Note ''
Note 'Frame times are present to present. GPU columns are the whole card, the view included. "View before" is its median over the pause before the run.'
Note ''
Note '| Run | HallSlice fps | Frame ms p50 / p99 / max | HallSlice CPU % | View fps min / median (seconds) | View before | GPU % | GPU W | View |'
Note '|---|---|---|---|---|---|---|---|---|'
$rows | ForEach-Object { Note $_ }
Note ''
Note '## HallSlice output'
Note ''
$logs | ForEach-Object { Note $_ }
Write-Evidence
