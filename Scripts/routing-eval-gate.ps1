#!/usr/bin/env pwsh
# routing-eval-gate.ps1 — Phase 3 CI ratchet for semantic-intent routing.
#
# Compares a freshly-emitted routing-eval report (from
# RoutingEvalHarness.RunBaseline_EmitReport) against the newest *committed*
# routing-eval baseline and FAILS (exit 1) if in-scope routing accuracy — or
# the out-of-scope decline rate — regressed beyond tolerance.
#
# The routing harness measures with the embedder + threshold pinned in the
# report's routerConfig (nomic-embed-text @ minConfidence 0.55), which is the
# SAME backend in-runner Ollama provides in CI — so the comparison is
# apples-to-apples (unlike chatbot-qa pass_pct, where CPU vs cloud differ).
#
# Paranoia rule (see feedback_auto_optimize_oracle_paranoia): a report that
# could not run (missing file, zero prompts) is a FAILURE, never a silent pass.
#
#   pwsh Scripts/routing-eval-gate.ps1 -NewReport state/quality/routing-eval-2026-06-15.json
#
[CmdletBinding()]
param(
    # The report just emitted by the harness this CI run.
    [Parameter(Mandatory)] [string] $NewReport,
    # Floor to compare against. Default: newest committed routing-eval-*.json
    # that is NOT $NewReport.
    [string] $Baseline,
    # Allowed drop before the gate fails (matches baseline.json regression_threshold).
    [double] $Tolerance = 0.02,
    # Emits a one-line gate-ledger row alongside the human report when set.
    [string] $LedgerPath
)
$ErrorActionPreference = 'Stop'

function Read-Overall([string]$path) {
    if (-not (Test-Path $path)) {
        throw "GATE FAIL (couldnt_run): report not found at '$path'. " +
              "The harness did not produce output — treat as failure, never a pass."
    }
    $doc = Get-Content -Raw $path | ConvertFrom-Json
    $o = $doc.overall
    if ($null -eq $o -or [int]$o.Total -le 0) {
        throw "GATE FAIL (couldnt_run): '$path' has no scored prompts (Total<=0). " +
              "Backend likely unavailable — treat as failure."
    }
    return $o
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
if (-not (Test-Path $NewReport)) {
    Write-Host ("GATE FAIL (couldnt_run): report not found at '{0}'. " -f $NewReport) -ForegroundColor Red
    Write-Host "The harness did not produce output — treat as failure, never a pass." -ForegroundColor Red
    exit 1
}
$newPath  = Resolve-Path $NewReport
$new      = Read-Overall $newPath

# Resolve the baseline floor.
if (-not $Baseline) {
    $candidates = Get-ChildItem (Join-Path $repoRoot 'state/quality') -Filter 'routing-eval-*.json' |
        Where-Object { $_.FullName -ne $newPath.Path } |
        Sort-Object Name -Descending
    if (-not $candidates) {
        Write-Host "GATE SKIP: no prior routing-eval baseline committed — recording only, nothing to ratchet against." -ForegroundColor Yellow
        exit 0
    }
    $Baseline = $candidates[0].FullName
}
$base = Read-Overall $Baseline

$baseName = Split-Path $Baseline -Leaf
$dInScope = [math]::Round($new.InScopeAccuracy - $base.InScopeAccuracy, 4)
$dOos     = [math]::Round($new.OosDeclineRate  - $base.OosDeclineRate,  4)

Write-Host ""
Write-Host "─── Routing-eval ratchet ───────────────────────────────" -ForegroundColor Cyan
Write-Host ("  baseline      : {0}" -f $baseName)
Write-Host ("  in-scope acc  : {0:P1} -> {1:P1}  (Δ {2:+0.0%;-0.0%;0.0%})" -f $base.InScopeAccuracy, $new.InScopeAccuracy, $dInScope)
Write-Host ("  OOS decline   : {0:P1} -> {1:P1}  (Δ {2:+0.0%;-0.0%;0.0%})" -f $base.OosDeclineRate, $new.OosDeclineRate, $dOos)
Write-Host ("  tolerance     : {0:P1}" -f $Tolerance)

$reasons = @()
if ($new.InScopeAccuracy -lt ($base.InScopeAccuracy - $Tolerance)) {
    $reasons += ("in-scope accuracy {0:P1} below floor {1:P1} (baseline {2:P1} − tol {3:P1})" -f `
        $new.InScopeAccuracy, ($base.InScopeAccuracy - $Tolerance), $base.InScopeAccuracy, $Tolerance)
}
if ($new.OosDeclineRate -lt ($base.OosDeclineRate - $Tolerance)) {
    $reasons += ("OOS-decline rate {0:P1} below floor {1:P1} — router is over-accepting out-of-scope prompts" -f `
        $new.OosDeclineRate, ($base.OosDeclineRate - $Tolerance))
}

$decision = if ($reasons.Count -gt 0) { 'fail' } else { 'pass' }

if ($LedgerPath) {
    $row = [ordered]@{
        ts            = (Get-Date).ToUniversalTime().ToString('o')
        source        = 'routing-eval-gate'
        domain        = 'routing'
        decision      = $decision
        metric        = 'inScopeAccuracy'
        metric_value  = $new.InScopeAccuracy
        baseline      = $baseName
        baseline_value= $base.InScopeAccuracy
        delta         = $dInScope
        tolerance     = $Tolerance
    } | ConvertTo-Json -Compress
    Add-Content -Path $LedgerPath -Value $row
}

if ($decision -eq 'fail') {
    Write-Host ""
    foreach ($r in $reasons) { Write-Host "  ✗ $r" -ForegroundColor Red }
    Write-Host "GATE FAIL: routing regressed beyond tolerance." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "  ✓ routing held at or above the committed floor." -ForegroundColor Green
Write-Host "GATE PASS." -ForegroundColor Green
exit 0
