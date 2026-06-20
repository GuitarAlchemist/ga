#requires -Version 7
<#
.SYNOPSIS
    Per-query routing/retrieval drift lens — runs IX's DuckDB UDFs (ix_kdist,
    ix_silhouette) over the live query-embedding sink to flag out-of-distribution
    queries and measure the RUNTIME separability of each routed intent.

.DESCRIPTION
    Reads state/quality/query-embeddings/*.jsonl (written by QueryEmbeddingLog on
    the live routing path — the exact vector the SemanticIntentRouter scored with),
    loads ix.duckdb_extension, runs Scripts/query-drift-lens.sql, and writes a
    markdown report + JSON sidecars under state/quality/query-embeddings/drift/.

    This is the GA-side LOCAL view of cross-repo Contract B; the ix sibling's
    ix-duck analyst bench is the canonical OOD authority. The lens needs no ix
    checkout — only the built extension and the on-disk JSONL.

.PARAMETER Dir
    Directory of query-embedding JSONL files. Default: $env:GA_QUERY_EMBEDDING_DIR,
    else state/quality/query-embeddings (matches QueryEmbeddingLog.ResolveDirectory).

.PARAMETER Sample
    Max queries (after embedder-pinning) fed to the O(n^2) ix_kdist/ix_silhouette
    pass (default 3000).

.PARAMETER Extension
    Path to ix.duckdb_extension. Defaults to $env:IX_DUCK_EXT, else
    ../ix/crates/ix-duck-ext/ix.duckdb_extension.
#>
[CmdletBinding()]
param(
    [string] $Dir,
    [int]    $Sample = 3000,
    [string] $Extension
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$repoRoot    = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$sqlTemplate = Join-Path $PSScriptRoot 'query-drift-lens.sql'

function Fail([string] $msg, [int] $code = 1) {
    Write-Host "QUERY-DRIFT FAILED: $msg" -ForegroundColor Red
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

# ── 1. Resolve the sink dir + assert it has data ─────────────────────────────
if (-not $Dir) { $Dir = $env:GA_QUERY_EMBEDDING_DIR }
if (-not $Dir) { $Dir = Join-Path $repoRoot 'state/quality/query-embeddings' }
if (-not (Test-Path $Dir)) {
    Fail "query-embedding dir '$Dir' does not exist. The sink is gitignored and only fills when the live router runs (GaApi). Exercise routing, then re-run." 2
}
$jsonl = Get-ChildItem -Path $Dir -Filter '*.jsonl' -ErrorAction SilentlyContinue
if (-not $jsonl) {
    Fail "no *.jsonl under '$Dir'. Nothing routed yet (or GA_QUERY_EMBEDDING_NO_LOG=1 suppressed writes). Exercise the live router, then re-run." 2
}
$outDir = Join-Path $Dir 'drift'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$glob = Join-Path $Dir '*.jsonl'
Write-Host "Sink: $Dir  ·  $($jsonl.Count) file(s)  ·  sample: $Sample" -ForegroundColor Cyan

# ── 2. Substitute placeholders + run DuckDB + IX UDFs ────────────────────────
$fs = { param($p) ($p -replace '\\', '/') }
$sql = Get-Content $sqlTemplate -Raw
$sql = $sql.Replace('__EXT__',    (& $fs $Extension))
$sql = $sql.Replace('__GLOB__',   (& $fs $glob))
$sql = $sql.Replace('__OUTDIR__', (& $fs $outDir))
$sql = $sql.Replace('__SAMPLE__', [string]$Sample)

$tmpSql = Join-Path ([System.IO.Path]::GetTempPath()) "query-drift-$([System.IO.Path]::GetRandomFileName()).sql"
Set-Content -Path $tmpSql -Value $sql -Encoding UTF8

Write-Host "Running DuckDB + IX UDFs (ix_kdist OOD / ix_silhouette runtime separability)..." -ForegroundColor Cyan
$report = (Get-Content $tmpSql -Raw | & $duckdb -unsigned 2>&1 | Out-String)
$duckExit = $LASTEXITCODE
Remove-Item $tmpSql -Force -ErrorAction SilentlyContinue

if ($duckExit -ne 0) {
    Write-Host $report
    Fail "duckdb exited $duckExit (extension load or SQL error above). NOT a clean result." $duckExit
}
# Oracle paranoia: a run that produced no kdist verdict is a broken run.
if ($report -notmatch 'QUERY_DRIFT_OK=') {
    Write-Host $report
    Fail "duckdb produced no drift verdict — treating as could-not-run, not clean." 3
}

# ── 3. Assemble the markdown report ──────────────────────────────────────────
$stamp = (Get-Date).ToString('yyyy-MM-dd')
$reportPath = Join-Path $outDir "query-drift-$stamp.md"
$header = @"
# Per-query routing/retrieval drift lens — $stamp

Sink: ``$(Split-Path $Dir -Leaf)`` · $($jsonl.Count) JSONL file(s) · sample: $Sample ·
extension: ``$(Split-Path $Extension -Leaf)`` · engine: DuckDB + IX UDFs.

These are the EXACT vectors the ``SemanticIntentRouter`` scored on (not re-embeds).
**ix_kdist** is the out-of-distribution signal — a query far from all its neighbours
is unlike what the router usually sees (a coverage gap, or a genuinely novel ask).
**ix_silhouette by routed intent** is the RUNTIME counterpart to
``routing-ambiguity-diagnostic`` (which measures the design-time ANCHOR geometry):
low/negative here = real users' queries for that intent land amongst another
intent's — fix by contrasting example prompts, never a keyword rule.

GA-side local view of cross-repo Contract B (ix-duck is the canonical OOD bench).
Sidecars: ``query-drift-isolated.json`` (top-200 OOD), ``query-drift-per-intent.json``.

"@
Set-Content -Path $reportPath -Value ($header + $report) -Encoding UTF8

Write-Host ""
Write-Host $report
Write-Host "Report written: $reportPath" -ForegroundColor Green
