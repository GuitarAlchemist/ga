# Verifies the most recent /octo:review run actually produced findings,
# rather than silently returning empty because every specialist failed.
#
# Background: as of 2026-05-10, /octo:review can return {"findings": []}
# even when all multi-LLM specialists failed to spawn (Windows PATH-with-
# spaces bug in the orchestrator's child-process env). The empty array is
# indistinguishable from "no real findings", which lets a broken gate
# masquerade as a green gate.
#
# This script inspects the per-agent .md result files for the most recent
# run and exits non-zero if every specialist failed. Wire it into
# /chatbot-iterate Step 4 (gate verification) so a dark gate hard-blocks
# instead of silently passing.
#
# Exit codes:
#   0 = at least one specialist produced output (gate alive — verdict may
#       still be empty findings, but the gate ran)
#   2 = all specialists failed (GATE DARK — do NOT trust the verdict)
#   3 = no run found (no findings JSON, no specialists to inspect)
#
# Usage:
#   pwsh Scripts/octo-gate-liveness.ps1                 # latest run
#   pwsh Scripts/octo-gate-liveness.ps1 -Timestamp 123  # specific run id
#   pwsh Scripts/octo-gate-liveness.ps1 -Json           # machine-readable
#
# See docs/solutions/tooling/2026-05-10-octo-plugin-install-corruption-silent-gate-failure.md.

[CmdletBinding()]
param(
    [string]$Timestamp,
    [switch]$Json,
    [string]$ResultsDir = (Join-Path $env:USERPROFILE '.claude-octopus\results')
)

$ErrorActionPreference = 'Stop'

function Write-JsonOrText {
    param(
        [int]$ExitCode,
        [string]$State,
        [int]$Total,
        [int]$Failed,
        [int]$Live,
        [string[]]$FailedAgents,
        [string]$Detail
    )
    if ($Json) {
        $obj = [ordered]@{
            exitCode = $ExitCode
            state    = $State
            timestamp = $Timestamp
            total    = $Total
            failed   = $Failed
            live     = $Live
            failedAgents = $FailedAgents
            detail   = $Detail
        }
        $obj | ConvertTo-Json -Compress
    }
    else {
        $colour = switch ($State) {
            'dark'  { 'Red' }
            'live'  { 'Green' }
            'mixed' { 'Yellow' }
            'no-run' { 'Gray' }
            default { 'White' }
        }
        Write-Host ''
        Write-Host "Octo gate liveness: $State" -ForegroundColor $colour
        Write-Host "  timestamp: $Timestamp" -ForegroundColor DarkGray
        Write-Host "  $Live live / $Failed failed (of $Total specialists)" -ForegroundColor DarkGray
        if ($FailedAgents.Count -gt 0) {
            foreach ($a in $FailedAgents) {
                Write-Host "    $a" -ForegroundColor DarkYellow
            }
        }
        if ($Detail) { Write-Host "  $Detail" -ForegroundColor DarkGray }
        Write-Host ''
    }
}

if (-not (Test-Path $ResultsDir)) {
    Write-JsonOrText -ExitCode 3 -State 'no-run' -Total 0 -Failed 0 -Live 0 -FailedAgents @() `
        -Detail "results dir not found: $ResultsDir"
    exit 3
}

# Resolve the run timestamp from the latest findings JSON unless caller passed one.
if (-not $Timestamp) {
    $latest = Get-ChildItem -Path $ResultsDir -Filter 'review-findings-*.json' -ErrorAction SilentlyContinue |
              Sort-Object LastWriteTime -Descending |
              Select-Object -First 1
    if (-not $latest) {
        Write-JsonOrText -ExitCode 3 -State 'no-run' -Total 0 -Failed 0 -Live 0 -FailedAgents @() `
            -Detail 'no review-findings JSON found'
        exit 3
    }
    $Timestamp = $latest.BaseName -replace '^review-findings-', ''
}

$pattern = "*-review-r1-*-$Timestamp.md"
$resultFiles = Get-ChildItem -Path $ResultsDir -Filter $pattern -ErrorAction SilentlyContinue

if (-not $resultFiles) {
    Write-JsonOrText -ExitCode 3 -State 'no-run' -Total 0 -Failed 0 -Live 0 -FailedAgents @() `
        -Detail "no per-agent result files matching $pattern"
    exit 3
}

# A specialist counts as "failed" if its result file ends with "Status: FAILED"
# (the codex/gemini env-127 path) OR contains "(no output captured)" (any
# spawn that produced no review body). Claude-sonnet via Agent Teams also
# falls into this bucket when the SubagentStop hook doesn't capture content
# — file ends after the dispatch header.
$failedAgents = @()
$total = $resultFiles.Count
foreach ($f in $resultFiles) {
    $tail = (Get-Content -Path $f.FullName -Tail 30 -ErrorAction SilentlyContinue) -join "`n"
    $size = $f.Length
    # Heuristics in order of certainty:
    #   1. Explicit FAILED line — codex/gemini env-127 case
    #   2. "(no output captured)" — spawn produced no body
    #   3. File <= 300 bytes and lacks JSON brace — likely just dispatch header
    $failed = $false
    if ($tail -match 'Status:\s*FAILED' -or $tail -match '\(no output captured\)') {
        $failed = $true
    }
    elseif ($size -le 300 -and ($tail -notmatch '\{')) {
        $failed = $true
    }
    if ($failed) {
        $failedAgents += $f.BaseName
    }
}

$live = $total - $failedAgents.Count

if ($total -gt 0 -and $failedAgents.Count -eq $total) {
    Write-JsonOrText -ExitCode 2 -State 'dark' -Total $total -Failed $failedAgents.Count -Live $live `
        -FailedAgents $failedAgents `
        -Detail 'All specialists failed — gate verdict is meaningless. Re-run with PATH scrubbed (see Scripts/octo-review-clean.ps1) or use direct codex/gemini CLIs.'
    exit 2
}

if ($failedAgents.Count -gt 0) {
    Write-JsonOrText -ExitCode 0 -State 'mixed' -Total $total -Failed $failedAgents.Count -Live $live `
        -FailedAgents $failedAgents `
        -Detail "$live of $total specialists ran — verdict reflects a partial signal."
    exit 0
}

Write-JsonOrText -ExitCode 0 -State 'live' -Total $total -Failed 0 -Live $live -FailedAgents @() `
    -Detail "All $total specialists produced output."
exit 0
