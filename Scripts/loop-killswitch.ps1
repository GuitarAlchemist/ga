# Always-on kill switch for /chatbot-iterate's loop mode and any
# autonomous-iteration scheduler running in the background.
#
# Purpose (L4 prerequisite per docs/automation/chatbot-loop.md):
# at L4 the loop runs without an interactive terminal. The human's
# kill mechanism can't be Ctrl-C. This script:
#
#   1. Writes state/.loop-halted (a sentinel /chatbot-iterate checks
#      before EVERY iteration and refuses to start when present).
#   2. Looks for any running 'loop' background tasks (Claude Code
#      background commands containing 'chatbot-iterate' or 'loop'
#      in their description / cwd) and reports them so the operator
#      can stop them.
#   3. Optionally with -Force: signals running powershell processes
#      that match the loop pattern, asking them to terminate.
#
# This script NEVER force-kills processes by default — that requires
# -Force AND -Yes. The default mode is "halt new iterations + report
# what's running so the operator can decide".
#
# Usage:
#   pwsh Scripts/loop-killswitch.ps1               # halt new + report
#   pwsh Scripts/loop-killswitch.ps1 -Reset        # remove the halt sentinel
#   pwsh Scripts/loop-killswitch.ps1 -Status       # just report current state
#   pwsh Scripts/loop-killswitch.ps1 -Force -Yes   # halt + stop existing
#
# Companion: /chatbot-iterate Step 1 should check the sentinel and
# refuse to start when present (added in this batch).

[CmdletBinding()]
param(
    [switch]$Reset,
    [switch]$Status,
    [switch]$Force,
    [switch]$Yes,
    [string]$Reason,
    [string]$RepoRoot = (Resolve-Path .).Path
)

$ErrorActionPreference = 'Stop'

$stateDir = Join-Path $RepoRoot 'state'
$sentinel = Join-Path $stateDir '.loop-halted'

function Show-Sentinel {
    if (Test-Path $sentinel) {
        $content = Get-Content $sentinel -Raw
        Write-Host "Sentinel: PRESENT — loop iterations are HALTED" -ForegroundColor Red
        Write-Host "  path: $sentinel" -ForegroundColor DarkGray
        Write-Host "  content:" -ForegroundColor DarkGray
        $content -split "`n" | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
    }
    else {
        Write-Host "Sentinel: ABSENT — iterations allowed" -ForegroundColor Green
        Write-Host "  expected at: $sentinel" -ForegroundColor DarkGray
    }
}

function Find-LoopProcesses {
    # Look for powershell / pwsh / node processes whose command line
    # references chatbot-iterate, the loop skill, or octo orchestrate.sh
    # — pragmatic best-effort, this isn't a sandbox.
    $patterns = @('chatbot-iterate','octo:loop','octo-loop','loop-skill','orchestrate\.sh')
    $matches = @()
    try {
        $procs = Get-CimInstance Win32_Process -Filter "Name='powershell.exe' OR Name='pwsh.exe' OR Name='node.exe'"
        foreach ($p in $procs) {
            $cmd = $p.CommandLine
            if (-not $cmd) { continue }
            foreach ($pat in $patterns) {
                if ($cmd -match $pat) {
                    $matches += [pscustomobject]@{
                        Pid = $p.ProcessId
                        Name = $p.Name
                        Pattern = $pat
                        Command = ($cmd.Substring(0, [Math]::Min(120, $cmd.Length)))
                    }
                    break
                }
            }
        }
    } catch {
        Write-Host "  (couldn't enumerate processes: $_)" -ForegroundColor Yellow
    }
    return $matches
}

# ---- Reset mode ----
if ($Reset) {
    if (Test-Path $sentinel) {
        Remove-Item $sentinel -Force
        Write-Host "Sentinel removed — iterations may resume." -ForegroundColor Green
    } else {
        Write-Host "Sentinel was already absent — no change." -ForegroundColor Gray
    }
    exit 0
}

# ---- Status mode ----
if ($Status) {
    Write-Host ''
    Show-Sentinel
    Write-Host ''
    $running = Find-LoopProcesses
    if ($running.Count -gt 0) {
        Write-Host "Loop-pattern processes RUNNING ($($running.Count)):" -ForegroundColor Yellow
        foreach ($r in $running) {
            Write-Host "  PID $($r.Pid) [$($r.Name)] matched '$($r.Pattern)'" -ForegroundColor Yellow
            Write-Host "    $($r.Command)..." -ForegroundColor DarkGray
        }
    } else {
        Write-Host "No loop-pattern processes detected." -ForegroundColor Green
    }
    Write-Host ''
    exit 0
}

# ---- Halt mode (default) ----
$reasonText = if ($Reason) { $Reason } else { "Halted by Scripts/loop-killswitch.ps1 at $(Get-Date -Format o)" }

if (-not (Test-Path $stateDir)) {
    New-Item -ItemType Directory -Path $stateDir -Force | Out-Null
}

$payload = @"
HALTED AT: $(Get-Date -Format o)
REASON: $reasonText
HOST: $env:COMPUTERNAME
USER: $env:USERNAME

Remove this file (or run: pwsh Scripts/loop-killswitch.ps1 -Reset)
to allow /chatbot-iterate iterations to resume.

This sentinel is read by /chatbot-iterate Step 1 — when present,
the skill refuses to pick a new item and exits cleanly. Existing
in-flight iterations are NOT terminated by this sentinel; use
-Force to signal them too.
"@

Set-Content -Path $sentinel -Value $payload -Encoding UTF8
Write-Host ''
Write-Host "Sentinel created — new iterations halted." -ForegroundColor Red
Write-Host "  path: $sentinel" -ForegroundColor DarkGray

$running = Find-LoopProcesses
if ($running.Count -eq 0) {
    Write-Host ''
    Write-Host "No loop-pattern processes detected. Nothing to stop." -ForegroundColor Green
    exit 0
}

Write-Host ''
Write-Host "Loop-pattern processes still RUNNING ($($running.Count)):" -ForegroundColor Yellow
foreach ($r in $running) {
    Write-Host "  PID $($r.Pid) [$($r.Name)] matched '$($r.Pattern)'" -ForegroundColor Yellow
    Write-Host "    $($r.Command)..." -ForegroundColor DarkGray
}

if (-not $Force) {
    Write-Host ''
    Write-Host "Re-run with -Force -Yes to terminate these processes too." -ForegroundColor Cyan
    Write-Host "(Default mode only halts NEW iterations, not running ones.)" -ForegroundColor DarkGray
    exit 0
}

if (-not $Yes) {
    Write-Host ''
    Write-Host "-Force without -Yes is a dry-run for safety. Re-run with both flags to actually terminate." -ForegroundColor Yellow
    exit 0
}

# Force + Yes: terminate
foreach ($r in $running) {
    try {
        Stop-Process -Id $r.Pid -Force -ErrorAction Stop
        Write-Host "  killed PID $($r.Pid)" -ForegroundColor Red
    } catch {
        Write-Host "  failed to kill PID $($r.Pid): $_" -ForegroundColor Magenta
    }
}
Write-Host ''
Write-Host "Done. Sentinel remains in place. Run with -Reset to resume." -ForegroundColor DarkGray
