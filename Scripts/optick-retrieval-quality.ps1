#requires -Version 7
<#
.SYNOPSIS
    OPTIC-K retrieval-quality lens — runs IX's DuckDB UDFs (ix_kdist,
    ix_silhouette) over the live OPTIC-K voicing index to surface isolated /
    outlier voicings and measure instrument-invariance of the embedding.

.DESCRIPTION
    Loads ix.duckdb_extension and runs Scripts/optick-retrieval-quality.sql,
    which reads state/voicings/optick.index directly via ix_optick_scan, samples
    voicings (stratified by instrument), and reports kdist local-density +
    per-instrument silhouette. Writes a markdown report + JSON sidecars under
    state/quality/optick-retrieval/.

.PARAMETER Index
    Path to optick.index. Defaults to $env:GA_OPTICK_INDEX, else this repo's
    state/voicings/optick.index, else the primary worktree's copy (the index is
    gitignored, so a fresh worktree won't have its own).

.PARAMETER PerInstrument
    Max voicings sampled per instrument (default 1000 → ~3k total; keeps the
    O(n^2) kdist/silhouette tractable).

.PARAMETER Extension
    Path to ix.duckdb_extension. Defaults to $env:IX_DUCK_EXT, else
    ../ix/crates/ix-duck-ext/ix.duckdb_extension.
#>
[CmdletBinding()]
param(
    [string] $Index,
    [int]    $PerInstrument = 1000,
    [string] $Extension
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$repoRoot    = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$outDir      = Join-Path $repoRoot 'state/quality/optick-retrieval'
$sqlTemplate = Join-Path $PSScriptRoot 'optick-retrieval-quality.sql'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function Fail([string] $msg, [int] $code = 1) {
    Write-Host "OPTICK-QUALITY FAILED: $msg" -ForegroundColor Red
    exit $code
}

# ── 0. Prerequisites ─────────────────────────────────────────────────────────
$duckdb = (Get-Command duckdb -ErrorAction SilentlyContinue)?.Source
if (-not $duckdb) { Fail "duckdb.exe not on PATH (winget install DuckDB.cli)." 2 }

if (-not $Extension) { $Extension = $env:IX_DUCK_EXT }
if (-not $Extension) { $Extension = Join-Path $repoRoot '../ix/crates/ix-duck-ext/ix.duckdb_extension' }
if (-not (Test-Path $Extension)) { Fail "ix.duckdb_extension not found at '$Extension' (build: pwsh ../ix/crates/ix-duck-ext/build.ps1)." 2 }
$Extension = (Resolve-Path $Extension).Path

# Resolve the index — it is gitignored, so a fresh worktree won't have its own.
if (-not $Index) { $Index = $env:GA_OPTICK_INDEX }
if (-not $Index) {
    $candidates = @(
        (Join-Path $repoRoot 'state/voicings/optick.index'),
        # primary worktree (shared .git) — derive from the common dir's parent.
        (Join-Path (Split-Path (& git -C $repoRoot rev-parse --git-common-dir)) 'state/voicings/optick.index')
    )
    $Index = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $Index -or -not (Test-Path $Index)) {
    Fail "optick.index not found (it is gitignored). Pass -Index <path> or set GA_OPTICK_INDEX." 2
}
$Index = (Resolve-Path $Index).Path
if (-not (Test-Path $sqlTemplate)) { Fail "SQL template missing at $sqlTemplate." 2 }
Write-Host "Index: $Index  ·  per-instrument sample: $PerInstrument" -ForegroundColor Cyan

# ── 1. Substitute placeholders + run DuckDB + IX UDFs ────────────────────────
$fs = { param($p) ($p -replace '\\', '/') }
$sql = Get-Content $sqlTemplate -Raw
$sql = $sql.Replace('__EXT__',            (& $fs $Extension))
$sql = $sql.Replace('__INDEX__',          (& $fs $Index))
$sql = $sql.Replace('__OUTDIR__',         (& $fs $outDir))
$sql = $sql.Replace('__PER_INSTRUMENT__', [string]$PerInstrument)

$tmpSql = Join-Path ([System.IO.Path]::GetTempPath()) "optick-quality-$([System.IO.Path]::GetRandomFileName()).sql"
Set-Content -Path $tmpSql -Value $sql -Encoding UTF8

Write-Host "Running DuckDB + IX UDFs (ix_optick_scan / ix_kdist / ix_silhouette)..." -ForegroundColor Cyan
$report = (Get-Content $tmpSql -Raw | & $duckdb -unsigned 2>&1 | Out-String)
$duckExit = $LASTEXITCODE
Remove-Item $tmpSql -Force -ErrorAction SilentlyContinue

if ($duckExit -ne 0) {
    Write-Host $report
    Fail "duckdb exited $duckExit (extension load or SQL error above). NOT a clean result." $duckExit
}
if ($report -notmatch 'overall_silhouette') {
    Write-Host $report
    Fail "duckdb produced no silhouette output — treating as could-not-run, not clean." 3
}

# ── 2. Assemble the markdown report ──────────────────────────────────────────
$stamp = (Get-Date).ToString('yyyy-MM-dd')
$reportPath = Join-Path $outDir "optick-retrieval-$stamp.md"
$header = @"
# OPTIC-K retrieval-quality lens — $stamp

Index: ``$(Split-Path $Index -Leaf)`` · per-instrument sample: $PerInstrument ·
extension: ``$(Split-Path $Extension -Leaf)`` · engine: DuckDB + IX UDFs.

**ix_kdist** (mean distance to k=5 nearest neighbours) is a local-density / OOD
signal — high kdist = an isolated voicing (rare, or a suspect embedding worth
inspecting). **ix_silhouette by instrument** measures how separable
guitar/bass/ukulele are: per OPTIC-K v1.8 the structure is instrument-INVARIANT
(same PC-set across instruments should be close), so **low/near-zero silhouette
is the GOOD, expected outcome** — a high positive value would mean instrument
identity leaked into the embedding.

Sidecars: ``optick-isolated-voicings.json`` (top-200 outliers),
``optick-per-instrument.json``.

"@
Set-Content -Path $reportPath -Value ($header + $report) -Encoding UTF8

Write-Host ""
Write-Host $report
Write-Host "Report written: $reportPath" -ForegroundColor Green
