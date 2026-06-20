#requires -Version 7
<#
.SYNOPSIS
    OPTIC-K SAE feature-coverage lens — runs DuckDB (+ IX ix_kdist) over an
    ix-optick-sae training artifact to report dictionary coverage, reconcile the
    manifest against the artifact's declared metrics, verify top-k sparsity, and
    surface voicings isolated in SAE feature space.

.DESCRIPTION
    Picks the newest dated artifact dir under state/quality/optick-sae/<date>/
    (one holding optick-sae-artifact.json + feature_manifest.jsonl +
    feature_activations.parquet), loads ix.duckdb_extension, runs
    Scripts/sae-feature-coverage.sql, and writes a markdown report + JSON sidecars
    under state/quality/optick-sae/<date>/coverage/.

    Cross-repo note: the artifact is PRODUCED by the ix sibling (ix-optick-sae) per
    docs/contracts/2026-05-02-optick-sae-artifact.contract.md. This lens only READS
    it — it is the GA-side coverage view, not a re-train.

.PARAMETER ArtifactDir
    A specific state/quality/optick-sae/<date>/ dir. Default: the newest dir that
    contains all three required files.

.PARAMETER Sample
    Max voicings read from the activations parquet for the O(n^2) ix_kdist pass
    (default 3000). The coverage/reconcile/sparsity sections use the full manifest.

.PARAMETER Extension
    Path to ix.duckdb_extension. Defaults to $env:IX_DUCK_EXT, else
    ../ix/crates/ix-duck-ext/ix.duckdb_extension.
#>
[CmdletBinding()]
param(
    [string] $ArtifactDir,
    [int]    $Sample = 3000,
    [string] $Extension
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$repoRoot    = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$saeRoot     = Join-Path $repoRoot 'state/quality/optick-sae'
$sqlTemplate = Join-Path $PSScriptRoot 'sae-feature-coverage.sql'

function Fail([string] $msg, [int] $code = 1) {
    Write-Host "SAE-COVERAGE FAILED: $msg" -ForegroundColor Red
    exit $code
}

# ── 0. Prerequisites ─────────────────────────────────────────────────────────
$duckdb = (Get-Command duckdb -ErrorAction SilentlyContinue)?.Source
if (-not $duckdb) { Fail "duckdb.exe not on PATH (winget install DuckDB.cli)." 2 }

if (-not $Extension) { $Extension = $env:IX_DUCK_EXT }
if (-not $Extension) { $Extension = Join-Path $repoRoot '../ix/crates/ix-duck-ext/ix.duckdb_extension' }
if (-not (Test-Path $Extension)) { Fail "ix.duckdb_extension not found at '$Extension' (build: pwsh ../ix/crates/ix-duck-ext/build.ps1)." 2 }
$Extension = (Resolve-Path $Extension).Path
if (-not (Test-Path $sqlTemplate)) { Fail "SQL template missing at $sqlTemplate." 2 }

# ── 1. Resolve the artifact dir (newest complete one unless pinned) ──────────
function Complete-ArtifactDir([string] $d) {
    (Test-Path (Join-Path $d 'optick-sae-artifact.json')) -and
    (Test-Path (Join-Path $d 'feature_manifest.jsonl'))   -and
    (Test-Path (Join-Path $d 'feature_activations.parquet'))
}

if ($ArtifactDir) {
    if (-not (Complete-ArtifactDir $ArtifactDir)) {
        Fail "ArtifactDir '$ArtifactDir' is missing one of optick-sae-artifact.json / feature_manifest.jsonl / feature_activations.parquet." 2
    }
} else {
    if (-not (Test-Path $saeRoot)) { Fail "no $saeRoot — no SAE artifacts to lens. Run the ix-optick-sae trainer first." 2 }
    $ArtifactDir = Get-ChildItem -Path $saeRoot -Directory -ErrorAction SilentlyContinue |
                   Where-Object { Complete-ArtifactDir $_.FullName } |
                   Sort-Object Name -Descending | Select-Object -First 1 -ExpandProperty FullName
    if (-not $ArtifactDir) {
        Fail "no complete artifact dir under $saeRoot (need artifact.json + manifest.jsonl + activations.parquet together — the 124-dim parquet lands from ix). Run the ix-optick-sae trainer." 2
    }
}
$ArtifactDir = (Resolve-Path $ArtifactDir).Path
$outDir = Join-Path $ArtifactDir 'coverage'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
Write-Host "Artifact dir: $ArtifactDir  ·  kdist sample: $Sample" -ForegroundColor Cyan

# ── 2. Substitute placeholders + run DuckDB + IX UDFs ────────────────────────
$fs = { param($p) ($p -replace '\\', '/') }
$sql = Get-Content $sqlTemplate -Raw
$sql = $sql.Replace('__EXT__',         (& $fs $Extension))
$sql = $sql.Replace('__ARTIFACT__',    (& $fs (Join-Path $ArtifactDir 'optick-sae-artifact.json')))
$sql = $sql.Replace('__MANIFEST__',    (& $fs (Join-Path $ArtifactDir 'feature_manifest.jsonl')))
$sql = $sql.Replace('__ACTIVATIONS__', (& $fs (Join-Path $ArtifactDir 'feature_activations.parquet')))
$sql = $sql.Replace('__OUTDIR__',      (& $fs $outDir))
$sql = $sql.Replace('__SAMPLE__',      [string]$Sample)

$tmpSql = Join-Path ([System.IO.Path]::GetTempPath()) "sae-coverage-$([System.IO.Path]::GetRandomFileName()).sql"
Set-Content -Path $tmpSql -Value $sql -Encoding UTF8

Write-Host "Running DuckDB + IX UDFs (coverage / reconcile / sparsity / ix_kdist)..." -ForegroundColor Cyan
$report = (Get-Content $tmpSql -Raw | & $duckdb -unsigned 2>&1 | Out-String)
$duckExit = $LASTEXITCODE
Remove-Item $tmpSql -Force -ErrorAction SilentlyContinue

if ($duckExit -ne 0) {
    Write-Host $report
    Fail "duckdb exited $duckExit (extension load or SQL error above). NOT a clean result." $duckExit
}
# Oracle paranoia: a run that produced no reconcile verdict is a broken run.
if ($report -notmatch 'RECONCILE_CONSISTENT=') {
    Write-Host $report
    Fail "duckdb produced no reconcile verdict — treating as could-not-run, not clean." 3
}

# ── 3. Assemble the markdown report ──────────────────────────────────────────
$stamp = (Get-Date).ToString('yyyy-MM-dd')
$reportPath = Join-Path $outDir "sae-coverage-$stamp.md"
$header = @"
# OPTIC-K SAE feature-coverage lens — $stamp

Artifact: ``$(Split-Path $ArtifactDir -Leaf)`` · kdist sample: $Sample ·
extension: ``$(Split-Path $Extension -Leaf)`` · engine: DuckDB + IX UDFs.

**Reconcile**: the manifest is recomputed independently and checked against the
artifact's DECLARED ``metrics`` — ``consistent = false`` means the manifest and the
artifact disagree (a producer bug or a mismatched file pairing), and the lens
exit-fails. **Coverage**: ``dead_pct`` is the fraction of dictionary atoms that
never fire — high dead% = an over-sized dictionary. **Top-k sparsity**: a top-k
SAE must fire exactly ``k_sparse`` features per voicing; a different ``mean_active``
means the parquet and the declared model disagree. **ix_kdist**: high values name
voicings that sit alone in SAE feature space — rare harmonic structure or suspect
rows worth inspecting.

Sidecars: ``sae-top-features.json`` (most-active), ``sae-dead-features.json``
(never-fired), ``sae-isolated-voicings.json`` (top-200 by kdist).

"@
Set-Content -Path $reportPath -Value ($header + $report) -Encoding UTF8

Write-Host ""
Write-Host $report
Write-Host "Report written: $reportPath" -ForegroundColor Green

# Surface an inconsistent reconcile (a real finding) via a clean exit code.
if ($report -match 'RECONCILE_CONSISTENT=false') {
    Write-Host "RECONCILE INCONSISTENT — manifest disagrees with the artifact's declared metrics." -ForegroundColor Yellow
    exit 4
}
