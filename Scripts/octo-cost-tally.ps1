# Cost tally for one /chatbot-iterate run.
#
# Reads ~/.claude-octopus/metrics-session.json (the orchestrator's per-
# session cost tracking) and writes a row to state/quality/cost-ledger.jsonl
# so /chatbot-iterate iterations build a cumulative cost history.
#
# Background: L3 promotion (per docs/automation/chatbot-loop.md) requires
# a documented cost-per-auto-merge budget. This script provides the
# measurement piece; the budget cap itself is a policy decision (set via
# -BudgetUsd or refuse auto-merge if cumulative cost exceeds it).
#
# Usage:
#   pwsh Scripts/octo-cost-tally.ps1 -Pr 155 -RunId 1778445897
#   pwsh Scripts/octo-cost-tally.ps1 -Pr 155 -RunId 1778445897 -BudgetUsd 5.00
#   pwsh Scripts/octo-cost-tally.ps1 -Summary           # cumulative report
#   pwsh Scripts/octo-cost-tally.ps1 -Summary -Json     # JSON for tooling
#
# Caveats:
#   - The orchestrator's metrics-session.json zeroes between runs and is
#     local-only (per-host). For accurate cumulative tracking, this
#     script accumulates into state/quality/cost-ledger.jsonl in the
#     repo so all sessions / hosts contribute to the same ledger.
#   - When agent-tool subagents are used instead of /octo:review, the
#     orchestrator metrics file doesn't reflect those costs. Pass the
#     -AgentToolEstimateUsd flag with a manual estimate.

[CmdletBinding(DefaultParameterSetName='Tally')]
param(
    [Parameter(ParameterSetName='Tally', Mandatory=$true)]
    [int]$Pr,

    [Parameter(ParameterSetName='Tally')]
    [string]$RunId,

    [Parameter(ParameterSetName='Tally')]
    [double]$AgentToolEstimateUsd = 0,

    [Parameter(ParameterSetName='Tally')]
    [double]$BudgetUsd,

    [Parameter(ParameterSetName='Summary', Mandatory=$true)]
    [switch]$Summary,

    [Parameter(ParameterSetName='Summary')]
    [int]$Last = 30,

    [Parameter()]
    [switch]$Json,

    [string]$RepoRoot = (Resolve-Path .).Path,
    [string]$MetricsPath = (Join-Path $env:USERPROFILE '.claude-octopus\metrics-session.json')
)

$ErrorActionPreference = 'Stop'

$ledgerDir = Join-Path $RepoRoot 'state/quality'
$ledger    = Join-Path $ledgerDir 'cost-ledger.jsonl'

# ---- Summary mode ----
if ($Summary) {
    if (-not (Test-Path $ledger)) {
        if ($Json) { '{"runs":0,"totalUsd":0,"avgUsd":0,"recent":[]}' }
        else { Write-Host 'No cost ledger yet.' -ForegroundColor Gray }
        exit 0
    }
    $rows = @(Get-Content $ledger | ForEach-Object {
        try { $_ | ConvertFrom-Json } catch { }
    } | Where-Object { $_ })
    $total = ($rows | Measure-Object -Property totalUsd -Sum).Sum
    $count = $rows.Count
    $recent = $rows | Select-Object -Last $Last
    if ($Json) {
        $out = [ordered]@{
            runs = $count
            totalUsd = [Math]::Round($total, 4)
            avgUsd = if ($count -gt 0) { [Math]::Round($total / $count, 4) } else { 0 }
            recent = $recent
        }
        $out | ConvertTo-Json -Depth 5 -Compress
    } else {
        Write-Host ''
        Write-Host "Cost ledger summary" -ForegroundColor Cyan
        Write-Host ("  runs:  {0}" -f $count) -ForegroundColor Gray
        Write-Host ("  total: `$ {0:N4} USD" -f $total) -ForegroundColor Gray
        if ($count -gt 0) {
            Write-Host ("  avg:   `$ {0:N4} USD/run" -f ($total / $count)) -ForegroundColor Gray
        }
        Write-Host ''
        Write-Host "Most recent runs:" -ForegroundColor Cyan
        foreach ($r in $recent) {
            Write-Host ("  PR #{0,-5} {1}  `$ {2,8:N4}  ({3})" -f $r.pr, $r.timestamp, $r.totalUsd, $r.runId) -ForegroundColor Gray
        }
        Write-Host ''
    }
    exit 0
}

# ---- Tally mode ----
$orchestratorCost = 0.0
$orchestratorTokens = 0
$orchestratorToolUses = 0
if (Test-Path $MetricsPath) {
    try {
        $m = Get-Content $MetricsPath -Raw | ConvertFrom-Json
        $orchestratorCost = [double]$m.totals.estimated_cost_usd
        $orchestratorTokens = [int]$m.totals.estimated_tokens
        $orchestratorToolUses = [int]$m.totals.tool_uses
    } catch {
        Write-Warning "Couldn't parse $MetricsPath ($_) — orchestrator cost will be 0."
    }
}

$totalUsd = $orchestratorCost + $AgentToolEstimateUsd

$record = [ordered]@{
    pr           = $Pr
    runId        = $RunId
    timestamp    = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ' -AsUTC)
    orchestratorUsd = [Math]::Round($orchestratorCost, 6)
    agentToolUsd = [Math]::Round($AgentToolEstimateUsd, 6)
    totalUsd     = [Math]::Round($totalUsd, 6)
    tokens       = $orchestratorTokens
    toolUses     = $orchestratorToolUses
}
if ($BudgetUsd -gt 0) {
    # Cumulative check — pull total from ledger + this row
    $cumulative = $totalUsd
    if (Test-Path $ledger) {
        $cumulative += (Get-Content $ledger | ForEach-Object {
            try { ($_ | ConvertFrom-Json).totalUsd } catch { 0 }
        } | Measure-Object -Sum).Sum
    }
    $record.cumulativeUsd = [Math]::Round($cumulative, 6)
    $record.budgetUsd = $BudgetUsd
    $record.budgetState = if ($cumulative -ge $BudgetUsd) { 'over' }
                          elseif ($cumulative -ge ($BudgetUsd * 0.8)) { 'warn' }
                          else { 'ok' }
}

if (-not (Test-Path $ledgerDir)) {
    New-Item -ItemType Directory -Path $ledgerDir -Force | Out-Null
}
$line = $record | ConvertTo-Json -Depth 4 -Compress
Add-Content -Path $ledger -Value $line -Encoding UTF8

if ($Json) {
    $record | ConvertTo-Json -Depth 4 -Compress
}
else {
    Write-Host ''
    Write-Host "Cost row appended: $ledger" -ForegroundColor Green
    Write-Host ("  pr=#{0}  total=`${1:N4}  orchestrator=`${2:N4}  agent-tool=`${3:N4}" -f `
        $Pr, $totalUsd, $orchestratorCost, $AgentToolEstimateUsd) -ForegroundColor DarkGray
    if ($record.budgetState) {
        $colour = switch ($record.budgetState) {
            'ok'   { 'Green' }
            'warn' { 'Yellow' }
            'over' { 'Red' }
        }
        Write-Host ("  cumulative=`${0:N4}  budget=`${1:N2}  state={2}" -f `
            $record.cumulativeUsd, $BudgetUsd, $record.budgetState) -ForegroundColor $colour
    }
    Write-Host ''
}

if ($record.budgetState -eq 'over') {
    exit 2  # over budget — caller should refuse further iterations
}
exit 0
