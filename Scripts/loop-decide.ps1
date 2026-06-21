#!/usr/bin/env pwsh
# loop-decide.ps1 — the self-termination controller for /auto-optimize (Phase 2).
#
# Reads the per-cycle ledger the loop is writing (state/quality/loops/<domain>.iterations.jsonl)
# for the CURRENT run (loop_id) and returns a governance decision so the loop stops
# from the *durable trajectory*, not a fixed iteration count or its own optimism:
#
#   continue          — still improving / not enough signal to stop
#   stop-plateau      — last <PlateauWindow> cycles all moved the metric < <PlateauThreshold>
#   halt-oscillating  — >= 2 regressed cycles this run (the fix-A-breaks-B thrash)
#   halt-misfire      — the oracle couldn't run (verdict=couldnt_run) — NEVER treat as progress
#
# Precedence: misfire > oscillating > plateau > continue (oracle-broken is most urgent).
# This mirrors the loop_convergence view's `shape` (build-views.sql) but adds the
# recent-window plateau check the in-loop decision needs. Emits a JSON decision to
# stdout and always exits 0 (success = "decision computed"); the caller reads .decision.
#
#   pwsh Scripts/loop-decide.ps1 -Domain chatbot-qa -LoopId chatbot-qa-20260615T1830Z
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$Domain,
    [Parameter(Mandatory)] [string]$LoopId,
    [int]$PlateauWindow = 5,
    [double]$PlateauThreshold = 0.005,
    [int]$OscillationRegressions = 2,
    # Override the ledger path (tests point this at a fixture).
    [string]$LedgerPath = ''
)
$ErrorActionPreference = 'Stop'
if (-not $LedgerPath) { $LedgerPath = "state/quality/loops/$Domain.iterations.jsonl" }

$rows = @()
if (Test-Path $LedgerPath) {
    $rows = Get-Content $LedgerPath |
        Where-Object { $_.Trim() } |
        ForEach-Object { $_ | ConvertFrom-Json } |
        Where-Object { $_.loop_id -eq $LoopId } |
        Sort-Object iteration
}

$iterations  = @($rows).Count
$misfires    = @($rows | Where-Object { $_.verdict -eq 'couldnt_run' }).Count
$regressions = @($rows | Where-Object { $_.verdict -eq 'regressed' }).Count

# Recent-window plateau: the last PlateauWindow cycles that actually ran (delta not null)
# all moved the metric by less than the threshold (absolute, matching loop_convergence).
$ran     = @($rows | Where-Object { $_.metric_delta -ne $null })
$recent  = @($ran | Select-Object -Last $PlateauWindow)
$recentDeltas = @($recent | ForEach-Object { [double]$_.metric_delta })
$isPlateau = ($recent.Count -ge $PlateauWindow) -and
             (@($recentDeltas | Where-Object { [math]::Abs($_) -ge $PlateauThreshold }).Count -eq 0)

$decision, $reason =
    if ($misfires -gt 0) {
        'halt-misfire', "oracle misfired on $misfires cycle(s) — verdict=couldnt_run; refusing to treat a non-run as progress"
    } elseif ($regressions -ge $OscillationRegressions) {
        'halt-oscillating', "$regressions regressed cycles (>= $OscillationRegressions) — loop is thrashing; escalate for human/next-layer work"
    } elseif ($isPlateau) {
        'stop-plateau', "last $PlateauWindow cycles all moved $($recent[0].metric_name) by < $PlateauThreshold — metric stopped responding to this toolbox"
    } else {
        'continue', "still improving or insufficient signal to stop ($iterations cycle(s), $regressions regression(s))"
    }

[pscustomobject]@{
    decision      = $decision
    reason        = $reason
    loop_id       = $LoopId
    domain        = $Domain
    iterations    = $iterations
    regressions   = $regressions
    misfires      = $misfires
    recent_deltas = $recentDeltas
} | ConvertTo-Json -Compress
exit 0
