#requires -Version 7
<#
.SYNOPSIS
    Routing-ambiguity diagnostic — runs IX's DuckDB vector UDFs over the
    SemanticIntentRouter's embedding anchors to explain WHY prompts misroute.

.DESCRIPTION
    1. (unless -SkipEmbed) runs RoutingEvalHarness.DumpRoutingAnchors_ForAmbiguityDiagnostic
       to embed every routed intent's description + example prompts with the
       production embedder/normalization, dumping to
       state/quality/routing-diagnostic/routing-anchors-<date>.json. Needs Ollama.
    2. Loads ix.duckdb_extension and runs Scripts/routing-ambiguity-diagnostic.sql:
       ix_silhouette (per-intent separability) + nearest-wrong-neighbour
       (confusable PAIRS) + ix_pca_project (2-D scatter).
    3. Writes a markdown report + 3 JSON sidecars under state/quality/routing-diagnostic/.

    The output names exactly which intents overlap and which example prompts to
    contrast — the semantic, no-keyword-rule lever for fixing the router.

.PARAMETER SkipEmbed
    Reuse the newest existing routing-anchors-*.json instead of re-embedding.
    Lets you iterate on the SQL without Ollama.

.PARAMETER Extension
    Path to ix.duckdb_extension. Defaults to $env:IX_DUCK_EXT, else
    ../ix/crates/ix-duck-ext/ix.duckdb_extension next to the repo.
#>
[CmdletBinding()]
param(
    [switch] $SkipEmbed,
    [string] $Extension
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$outDir   = Join-Path $repoRoot 'state/quality/routing-diagnostic'
$sqlTemplate = Join-Path $PSScriptRoot 'routing-ambiguity-diagnostic.sql'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function Fail([string] $msg, [int] $code = 1) {
    Write-Host "ROUTING-DIAG FAILED: $msg" -ForegroundColor Red
    exit $code
}

# ── 0. Prerequisites ─────────────────────────────────────────────────────────
$duckdb = (Get-Command duckdb -ErrorAction SilentlyContinue)?.Source
if (-not $duckdb) { Fail "duckdb.exe not on PATH (winget install DuckDB.cli)." 2 }

if (-not $Extension) {
    $Extension = $env:IX_DUCK_EXT
}
if (-not $Extension) {
    $Extension = Join-Path $repoRoot '../ix/crates/ix-duck-ext/ix.duckdb_extension'
}
if (-not (Test-Path $Extension)) {
    Fail "ix.duckdb_extension not found at '$Extension'. Build it: pwsh ../ix/crates/ix-duck-ext/build.ps1, or pass -Extension <path>." 2
}
$Extension = (Resolve-Path $Extension).Path

if (-not (Test-Path $sqlTemplate)) { Fail "SQL template missing at $sqlTemplate." 2 }

# ── 1. Produce / locate the anchor embedding dump ────────────────────────────
if (-not $SkipEmbed) {
    Write-Host "Embedding routing anchors (RoutingEvalHarness.DumpRoutingAnchors)..." -ForegroundColor Cyan
    $testProj = Join-Path $repoRoot 'Tests/Common/GA.Business.ML.Tests/GA.Business.ML.Tests.csproj'
    & dotnet test $testProj --filter 'FullyQualifiedName~DumpRoutingAnchors' --nologo -v q
    if ($LASTEXITCODE -ne 0) {
        Fail "anchor-embedding test failed (exit $LASTEXITCODE) — is Ollama up with nomic-embed-text? Re-run with -SkipEmbed to reuse a prior dump." $LASTEXITCODE
    }
}

$anchors = Get-ChildItem -Path $outDir -Filter 'routing-anchors-*.json' -ErrorAction SilentlyContinue |
           Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $anchors) {
    Fail "no routing-anchors-*.json under $outDir. Run without -SkipEmbed (needs Ollama) to produce one." 2
}
Write-Host "Using anchor dump: $($anchors.Name)" -ForegroundColor Cyan

# ── 2. Substitute placeholders and run DuckDB + IX UDFs ──────────────────────
# DuckDB wants forward slashes in string literals on Windows.
$fs = { param($p) ($p -replace '\\', '/') }
$sql = Get-Content $sqlTemplate -Raw
$sql = $sql.Replace('__EXT__',     (& $fs $Extension))
$sql = $sql.Replace('__ANCHORS__', (& $fs $anchors.FullName))
$sql = $sql.Replace('__OUTDIR__',  (& $fs $outDir))

$tmpSql = Join-Path ([System.IO.Path]::GetTempPath()) "routing-diag-$([System.IO.Path]::GetRandomFileName()).sql"
Set-Content -Path $tmpSql -Value $sql -Encoding UTF8

Write-Host "Running DuckDB + ix UDFs (silhouette, nearest-wrong-neighbour, PCA)..." -ForegroundColor Cyan
$report = (Get-Content $tmpSql -Raw | & $duckdb -unsigned 2>&1 | Out-String)
$duckExit = $LASTEXITCODE
Remove-Item $tmpSql -Force -ErrorAction SilentlyContinue

if ($duckExit -ne 0) {
    Write-Host $report
    Fail "duckdb exited $duckExit (extension load or SQL error above). NOT a clean result." $duckExit
}
# Oracle paranoia: a run that produced no silhouette section is a broken run,
# not a clean one — distinguish 'ran and saw nothing' from 'could not run'.
if ($report -notmatch 'overall_mean_silhouette') {
    Write-Host $report
    Fail "duckdb produced no silhouette output — treating as could-not-run, not clean." 3
}

# ── 3. Assemble the markdown report ──────────────────────────────────────────
$stamp = (Get-Date).ToString('yyyy-MM-dd')
$reportPath = Join-Path $outDir "routing-ambiguity-$stamp.md"
$header = @"
# Routing ambiguity diagnostic — $stamp

Source anchors: ``$($anchors.Name)`` · extension: ``$(Split-Path $Extension -Leaf)`` · engine: DuckDB + ix UDFs.

The ``SemanticIntentRouter`` routes a query to the intent whose example/description
embedding it is closest to (cosine). Low **silhouette** = an intent's example
prompts sit close to other intents' → fragile routing. High **nearest wrong-neighbour
cosine** between two intents names a confusable PAIR; fix by contrasting their
example prompts (the semantic, no-keyword-rule lever).

Sidecars (machine-readable): ``routing-silhouette-by-intent.json``,
``routing-confusable-pairs.json``, ``routing-pca-coords.json``.

"@
Set-Content -Path $reportPath -Value ($header + $report) -Encoding UTF8

Write-Host ""
Write-Host $report
Write-Host "Report written: $reportPath" -ForegroundColor Green
Write-Host "Sidecars: routing-silhouette-by-intent.json, routing-confusable-pairs.json, routing-pca-coords.json" -ForegroundColor Green
