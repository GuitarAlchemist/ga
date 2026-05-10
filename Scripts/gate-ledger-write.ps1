# Appends a row to state/quality/gate-ledger.jsonl conforming to
# docs/schemas/gate-ledger.schema.json.
#
# Called by /chatbot-iterate Step 6 (post-merge) — captures what every
# gate said and what the outcome was. The 'merged-with-revert' outcome
# is the gate ROI signal: an entry where the gate said pass but reality
# disagreed is what L3 promotion is trying to drive toward zero.
#
# Usage:
#   pwsh Scripts/gate-ledger-write.ps1 `
#     -Pr 155 `
#     -Branch chatbot/m7b5-dim7-chord-info `
#     -MergedAt (Get-Date -Format o) `
#     -Decision merged-clean `
#     -Tests @{ ran=$true; passed=51; failed=0 } `
#     -AgentToolReview @{
#         verdict='nits-only'
#         mechanism='agent-tool-subagents'
#         findingsCount=3
#         blockingCount=0
#         uniqueFindingsCount=1
#     }
#
# Re-running the same PR overwrites nothing — it appends. Cohort analysis
# downstream is responsible for picking the latest entry per PR.

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][int]$Pr,
    [Parameter(Mandatory=$true)][string]$Branch,
    [string]$MergedAt,
    [hashtable]$DiffStats,
    [hashtable]$Tests,
    [hashtable]$AgentToolReview,
    [hashtable]$OctoReview,
    [hashtable]$Tribunal,
    [Parameter(Mandatory=$true)]
    [ValidateSet('merged-clean','merged-with-followup','merged-with-revert','open','abandoned')]
    [string]$Decision,
    [int]$RollbackOf,
    [string]$IterateRun,
    [string]$Notes,
    [string]$RepoRoot = (Resolve-Path .).Path
)

$ErrorActionPreference = 'Stop'

$record = [ordered]@{
    pr       = $Pr
    branch   = $Branch
    mergedAt = if ($MergedAt) { $MergedAt } else { $null }
    gates    = [ordered]@{}
    decision = $Decision
}

if ($DiffStats) { $record.diffStats = $DiffStats }

if ($Tests) {
    $record.gates.tests = $Tests
}
$record.gates.agentToolReview = if ($AgentToolReview) { $AgentToolReview } else { $null }
$record.gates.octoReview      = if ($OctoReview) { $OctoReview } else { $null }
$record.gates.tribunal        = if ($Tribunal) { $Tribunal } else { $null }

if ($RollbackOf) { $record.rollbackOf = $RollbackOf }
if ($IterateRun) { $record.iterateRun = $IterateRun }
if ($Notes) { $record.notes = $Notes }

$outDir = Join-Path $RepoRoot 'state/quality'
if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}
$outPath = Join-Path $outDir 'gate-ledger.jsonl'

# JSONL = one compact JSON object per line
$line = $record | ConvertTo-Json -Depth 6 -Compress
Add-Content -Path $outPath -Value $line -Encoding UTF8

Write-Host "gate ledger row appended: $outPath" -ForegroundColor Green
Write-Host "  pr=$Pr  decision=$Decision" -ForegroundColor DarkGray
