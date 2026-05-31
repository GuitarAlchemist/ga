# afk-harness-status.ps1 — instrumentation writer for the GA chatbot AFK harness.
#
# The harness (.claude/skills/ga-chatbot-afk-harness) calls this at every phase
# so the dashboard (tools/afk-dashboard/index.html) always has fresh, truthful
# state — the human can see exactly what the autonomous loop is doing without
# reading the transcript. It does TWO things per call:
#   1. Appends one event line to state/quality/chatbot-qa/afk-runs/<run_id>.jsonl
#   2. Merges the provided fields into .../afk-runs/latest-status.json
#
# This is deliberately the ONLY way the harness reports progress: code, not the
# LLM's memory, owns the status — so a crashed/killed loop still leaves an
# honest last-known state on disk ("instrument before you ship").
#
# Usage:
#   pwsh Scripts/afk-harness-status.ps1 -RunId 20260529T204500Z-afk `
#        -State running -Phase improve -Iteration 3 -Commits 2 `
#        -DetPct 0.94 -SemPct 0.88 -TargetMetric 0.97 -Branch afk/chatbot-qa `
#        -Kind commit -Event "accepted fix for grad-014 (+2.0pp)"
#
# State values:  preflight | running | blocked | degraded | done | killed
# Kind values:   phase | target_selected | fix_proposed | roundtrip | commit |
#                revert | degraded | blocked | exit

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RunId,
    [string]$State,
    [string]$Phase,
    [Nullable[int]]$Iteration,
    [Nullable[int]]$Commits,
    [Nullable[int]]$MaxIterations,
    [Nullable[double]]$DetPct,        # deterministic oracle pass_pct (0..1)
    [Nullable[double]]$SemPct,        # semantic judge-panel pass_pct (0..1)
    [Nullable[double]]$TargetMetric,  # exit target (0..1)
    [string]$Branch,
    [string]$Kind,                    # event kind (see header)
    [string]$Event,                   # human-readable event detail
    [string]$Blocker,                 # if set, appended to blockers[] and state implied blocked
    [string]$Domain = "chatbot-qa"
)

$ErrorActionPreference = "Stop"
$repoRoot   = Split-Path $PSScriptRoot -Parent
$runsDir    = Join-Path $repoRoot "state/quality/$Domain/afk-runs"
$statusPath = Join-Path $runsDir "latest-status.json"
$eventsPath = Join-Path $runsDir "$RunId.jsonl"
if (-not (Test-Path $runsDir)) { New-Item -ItemType Directory -Path $runsDir -Force | Out-Null }

$nowUtc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")

# ── 1. Append an event line (only when something happened) ────────────────────
if ($Kind -or $Event) {
    $evt = [ordered]@{
        ts     = $nowUtc
        run_id = $RunId
        kind   = if ($Kind) { $Kind } else { "phase" }
        detail = if ($Event) { $Event } else { $Phase }
    }
    if ($null -ne $Iteration) { $evt["iteration"] = [int]$Iteration }
    if ($null -ne $DetPct)    { $evt["det"] = [double]$DetPct }   # lets the dashboard plot a trend
    if ($null -ne $SemPct)    { $evt["sem"] = [double]$SemPct }
    ($evt | ConvertTo-Json -Compress) | Add-Content -Path $eventsPath -Encoding utf8
}

# ── 2. Merge fields into latest-status.json ───────────────────────────────────
$status = @{}
if (Test-Path $statusPath) {
    try { $status = Get-Content -Raw $statusPath | ConvertFrom-Json -AsHashtable } catch { $status = @{} }
}
if (-not $status.ContainsKey("schema_version")) { $status["schema_version"] = 1 }
if (-not $status.ContainsKey("run_id") -or $status["run_id"] -ne $RunId) {
    # New run → reset started_at, blockers.
    $status["run_id"]     = $RunId
    $status["started_at"] = $nowUtc
    $status["blockers"]   = @()
}
$status["domain"]      = $Domain
$status["updated_at"]  = $nowUtc
$status["events_file"] = "afk-runs/$RunId.jsonl"
$status["killswitch"]  = "state/quality/$Domain/.STOP"
$status["global_halt"] = "state/.loop-halted"

if ($State)            { $status["state"]          = $State }
if ($Phase)            { $status["phase"]          = $Phase }
if ($null -ne $Iteration)     { $status["iteration"]      = [int]$Iteration }
if ($null -ne $MaxIterations) { $status["max_iterations"] = [int]$MaxIterations }
if ($null -ne $Commits)       { $status["commits"]        = [int]$Commits }
if ($null -ne $TargetMetric)  { $status["target_metric"]  = [double]$TargetMetric }
if ($Branch)           { $status["branch"]         = $Branch }
if ($Event)            { $status["last_event"]     = $Event }

if ($null -ne $DetPct -or $null -ne $SemPct) {
    $cur = if ($status.ContainsKey("current")) { $status["current"] } else { @{} }
    if ($null -ne $DetPct) { $cur["deterministic_pass_pct"] = [double]$DetPct }
    if ($null -ne $SemPct) { $cur["semantic_pass_pct"]      = [double]$SemPct }
    $status["current"] = $cur
}
if ($Blocker) {
    $b = @($status["blockers"]) + $Blocker | Where-Object { $_ } | Select-Object -Unique
    $status["blockers"] = @($b)
    if (-not $State) { $status["state"] = "blocked" }
}

($status | ConvertTo-Json -Depth 8) | Set-Content -Path $statusPath -Encoding utf8
Write-Host "afk-status: run=$RunId state=$($status['state']) phase=$($status['phase']) -> $statusPath"
