#!/usr/bin/env pwsh
# loop-record.ps1 — the deterministic per-cycle ledger WRITER for /auto-optimize
# and the AFK/qa harness (loop observability, Phase 1; ix-duck loop lens).
#
# Appends ONE row per loop cycle to state/quality/loops/<domain>.iterations.jsonl
# (append-only JSONL, one file per domain — the layout the reader
# Scripts/loop-decide.ps1 and build-views.sql's `loops/*.iterations.jsonl` glob
# already expect; loop_id is a COLUMN the views filter on, not the filename).
#
# Why a script instead of inline JSON in each skill: the row was hand-built in
# PowerShell inside /auto-optimize Step 3.8, so a cycle that exited early — or an
# AFK/qa run that never had the step at all — silently emitted nothing. Both
# surfaces now call this one writer, so the schema can't drift and a real run
# actually populates the ledger ix-duck reads.
#
# Paranoia rule (docs/solutions/.../auto-optimize-oracle-silent-success...): an
# oracle that couldn't run (oracle_status=couldnt_run) is recorded with
# metric_after/metric_delta = null and verdict=couldnt_run — NEVER as progress.
#
#   pwsh Scripts/loop-record.ps1 -LoopId chatbot-qa-20260617T1830Z -Domain chatbot-qa `
#       -Iteration 1 -MetricName pass_pct -MetricBefore 0.88 -MetricAfter 0.91 `
#       -Verdict improved -WorstItem p-set-theory-12 -ArtifactEdited Common/...A.cs `
#       -CommitSha 1a2b3c4 -RoundtripPassed $true
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$LoopId,
    [Parameter(Mandatory)] [string]$Domain,
    [Parameter(Mandatory)] [int]$Iteration,
    [Parameter(Mandatory)] [string]$MetricName,
    # improved | regressed | plateau | error | couldnt_run
    [Parameter(Mandatory)] [ValidateSet('improved','regressed','plateau','error','couldnt_run')]
    [string]$Verdict,
    # ok | couldnt_run | error
    [ValidateSet('ok','couldnt_run','error')] [string]$OracleStatus = 'ok',
    [Nullable[double]]$MetricBefore = $null,
    [Nullable[double]]$MetricAfter = $null,
    [Nullable[double]]$MetricDelta = $null,
    [string]$WorstItem = '',
    [string]$ArtifactEdited = '',
    [string]$CommitSha = '',
    [switch]$RoundtripPassed,
    # RFC3339 UTC; defaults to now. (Pass-through so tests/replays can pin it.)
    [string]$Ts = '',
    # Override the ledger file (tests point this at a temp path).
    [string]$LedgerPath = '',
    # Permit __seed__/__test__ domains or loop_ids (sentinels only — never prod rows).
    [switch]$AllowSentinel
)
$ErrorActionPreference = 'Stop'

# Production rows must carry a REAL domain/loop_id — the views drop __seed__ and a
# __test__ domain would pollute cross-iteration clustering. Guard unless asked.
$sentinels = @('__seed__','__test__')
if (-not $AllowSentinel -and ($sentinels -contains $Domain -or $sentinels -contains $LoopId)) {
    throw "loop-record: '$Domain'/'$LoopId' is a sentinel — refusing to write a sentinel as a production row. " +
          "Pass -AllowSentinel only for fixtures/the committed seed."
}

if (-not $Ts) { $Ts = (Get-Date).ToUniversalTime().ToString('o') }
if (-not $LedgerPath) { $LedgerPath = "state/quality/loops/$Domain.iterations.jsonl" }

# Fail-closed: a misfire is never progress. Null the after/delta regardless of
# what the caller passed, and pin the verdict if the oracle couldn't run.
if ($OracleStatus -eq 'couldnt_run') {
    $MetricAfter = $null
    $MetricDelta = $null
    if ($Verdict -ne 'couldnt_run') { $Verdict = 'couldnt_run' }
}
elseif ($null -eq $MetricDelta -and $null -ne $MetricBefore -and $null -ne $MetricAfter) {
    # Derive delta from before/after so callers can't transpose them.
    $MetricDelta = [math]::Round([double]$MetricAfter - [double]$MetricBefore, 6)
}

# Ordered exactly as state/quality/_fixtures/loop-iterations.sample.jsonl.
$row = [ordered]@{
    loop_id          = $LoopId
    domain           = $Domain
    iteration        = $Iteration
    ts               = $Ts
    oracle_status    = $OracleStatus
    metric_name      = $MetricName
    metric_before    = $MetricBefore
    metric_after     = $MetricAfter
    metric_delta     = $MetricDelta
    verdict          = $Verdict
    worst_item       = $WorstItem
    artifact_edited  = $ArtifactEdited
    commit_sha       = $CommitSha
    roundtrip_passed = [bool]$RoundtripPassed
}

$dir = Split-Path -Parent $LedgerPath
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }

$line = $row | ConvertTo-Json -Compress
Add-Content -Path $LedgerPath -Value $line

# Echo the appended row + path so the caller (and a watching human) can confirm.
[pscustomobject]@{ ledger = $LedgerPath; row = $line } | ConvertTo-Json -Compress
