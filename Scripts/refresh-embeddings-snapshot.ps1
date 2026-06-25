#!/usr/bin/env pwsh
# Refresh the embeddings quality snapshot from a LOCAL OPTIC-K index.
#
# WHY THIS EXISTS
# ---------------
# The daily .github/workflows/embeddings-snapshot.yml can only ever emit an
# amber "OPTIC-K index absent on runner" carryforward: state/voicings/optick.index
# is a ~175 MB gitignored binary that hosted CI runners never have. The ONLY way
# to produce a REAL leak_detection measurement is to run ix-embedding-diagnostics
# against the index on a host that actually has it (this dev machine).
#
# Run this after an OPTIC-K rebuild (see .claude/skills/optic-k-rebuild) — or any
# time the dashboard embeddings tile is stuck stale-amber — to drop a real,
# non-degraded snapshot at state/quality/embeddings/<UTC-date>.json. The cron
# workflow's degraded path will NOT overwrite a real snapshot for the same day
# (it guards on the existing file's `degraded` flag), so this measurement sticks.
#
# Idempotent: re-running on the same UTC day overwrites that day's snapshot.
[CmdletBinding()]
param(
  [string]$Index         = "state/voicings/optick.index",
  [string]$IxRepo        = "../ix",
  [int]   $ClusterSample = 5000
)
$ErrorActionPreference = 'Stop'

# Resolve to the ga repo root (this script lives in ga/scripts/).
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

if (-not (Test-Path $Index)) { throw "OPTIC-K index not found at $Index (cwd=$repoRoot)." }
$bytes  = (Get-Item $Index).Length
$sizeMb = [math]::Round($bytes / 1MB, 1)
if ($bytes -le 100MB) {
  throw "Index at $Index is only $sizeMb MB (<100 MB) — looks like a stub/LFS pointer, not a real index."
}

$bin = Join-Path $IxRepo "target/release/baseline-diagnostics.exe"
if (-not (Test-Path $bin)) {
  Write-Host "baseline-diagnostics not built — building (release)..."
  & cargo build --release --manifest-path (Join-Path $IxRepo "Cargo.toml") `
      -p ix-embedding-diagnostics --bin baseline-diagnostics
  if ($LASTEXITCODE -ne 0) { throw "cargo build of baseline-diagnostics failed ($LASTEXITCODE)." }
}

$tmp = Join-Path $env:TEMP "ga-embeddings-refresh"
New-Item -ItemType Directory -Path $tmp -Force | Out-Null
& $bin --index $Index --out-dir $tmp --cluster-sample $ClusterSample
if ($LASTEXITCODE -ne 0) { throw "baseline-diagnostics exited $LASTEXITCODE." }

$date = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd')
$src  = Join-Path $tmp "embedding-diagnostics-$date.json"
if (-not (Test-Path $src)) { throw "Producer did not write $src — check the run output above." }

$dstDir = "state/quality/embeddings"
New-Item -ItemType Directory -Path $dstDir -Force | Out-Null
$dst = Join-Path $dstDir "$date.json"
Move-Item -Force $src $dst

$acc = (Get-Content -Raw $dst -Encoding UTF8 | ConvertFrom-Json).leak_detection.full_classifier_accuracy
Write-Host ""
Write-Host "Wrote REAL embeddings snapshot -> $dst"
Write-Host "  full_classifier_accuracy = $acc   (index $sizeMb MB, 313k voicings)"
Write-Host "  commit it: git add $dst && git commit -m 'chore(quality): real embeddings snapshot $date'"
