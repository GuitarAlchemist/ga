#!/usr/bin/env pwsh
# emit-optick-sae.ps1 — Emit a real-corpus OPTIC-K SAE artifact into GA's tree.
#
# Runs the ix `ix-optick-sae` trainer against the real OPTK v4 index and writes
# the dated artifact under state/quality/optick-sae/<YYYY-MM-DD>/. This is the
# producer step behind the optick_sae DuckDB table + the cross-repo
# optick-sae-artifact contract.
#
# Usage:
#   pwsh Scripts/emit-optick-sae.ps1                 # emit today's artifact
#   pwsh Scripts/emit-optick-sae.ps1 -Date 2026-06-14
#   pwsh Scripts/emit-optick-sae.ps1 -IxRepo D:/code/ix
#
# Cadence: the OPTIC-K index changes rarely (only on schema/tag rebuilds), so a
# weekly or monthly run is plenty. This script intentionally registers NO
# scheduler — call it manually or wire it into Task Scheduler / cron yourself.
#
# Why this wrapper exists: the trainer silently falls back to a 1000-voicing
# synthetic corpus if it can't parse the index (missing python deps, OPTK reader
# regression). The guard below REFUSES to bless a synthetic artifact so a
# scheduled run can never quietly pollute the optick_sae trend with a smoke run.

[CmdletBinding()]
param(
    [string]$Date = (Get-Date -Format 'yyyy-MM-dd'),
    [string]$IxRepo,
    [int]$Epochs = 50,
    [int]$BatchSize = 4096
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$index    = Join-Path $repoRoot 'state/voicings/optick.index'
$outDir   = Join-Path $repoRoot "state/quality/optick-sae/$Date"

# Default to the sibling ix clone (../ix), per the peer-clone convention.
if (-not $IxRepo) { $IxRepo = Join-Path (Split-Path $repoRoot -Parent) 'ix' }
if (-not (Test-Path $IxRepo))  { throw "ix sibling repo not found at '$IxRepo'. Pass -IxRepo <path>." }
if (-not (Test-Path $index))   { throw "OPTIC-K index not found at '$index'. Build it first (optic-k-rebuild)." }

# Locate (or build) the trainer binary in the ix sibling repo.
$exe = Join-Path $IxRepo 'target/debug/ix-optick-sae.exe'
if (-not (Test-Path $exe)) {
    Write-Host "Building ix-optick-sae (not found at $exe)…" -ForegroundColor Cyan
    Push-Location $IxRepo
    try { cargo build -p ix-optick-sae; if ($LASTEXITCODE -ne 0) { throw "cargo build failed ($LASTEXITCODE)" } }
    finally { Pop-Location }
}

Write-Host "─── Emitting OPTIC-K SAE artifact for $Date ───" -ForegroundColor Cyan
& $exe train --index $index --output $outDir --epochs $Epochs --batch-size $BatchSize
if ($LASTEXITCODE -ne 0) { throw "trainer exited $LASTEXITCODE" }

# ── Synthetic-fallback guard ──────────────────────────────────────────────────
# The real OPTIC-K corpus is ~313k voicings; the synthetic smoke is 1000. Refuse
# anything that looks synthetic so the trend never silently regresses.
$artifact = Join-Path $outDir 'optick-sae-artifact.json'
if (-not (Test-Path $artifact)) { throw "no artifact written at '$artifact'" }
$json   = Get-Content $artifact -Raw | ConvertFrom-Json
$corpus = [int]$json.input.corpus_size
if ($corpus -le 1000) {
    throw "REFUSING artifact: corpus_size=$corpus looks synthetic (real OPTIC-K is ~313k). " +
          "Verify ix train.py can parse the OPTK index (python deps incl. msgpack; OPTK reader intact)."
}

Write-Host ("✓ real artifact: corpus_size={0}  r2={1}  dead={2}%  alive={3}/{4}" -f `
    $corpus, $json.metrics.reconstruction_r2, $json.metrics.dead_features_pct, `
    $json.features_summary.alive, $json.features_summary.total) -ForegroundColor Green
Write-Host "  $artifact"
