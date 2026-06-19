#requires -Version 7
<#
.SYNOPSIS
    IX ⟷ GA set-theory cross-check — verifies GA's C# set-theory engine against
    IX's independent Rust implementation (ix-bracelet) via the DuckDB UDFs
    ix_icv / ix_prime_form / ix_forte_number.

.DESCRIPTION
    1. (unless -SkipEmit) runs GA's SetTheoryCrossCheckCorpusEmitter to dump
       prime form + interval-class vector for every set class to
       state/quality/ga-ix-settheory/ga-settheory-<date>.json.
    2. Loads ix.duckdb_extension and runs Scripts/ix-ga-settheory-crosscheck.sql:
       ix_icv MUST match GA's ICV for every set class (convention-free);
       prime-form / Forte differences are reported as informational.
    3. Writes a markdown report + JSON sidecars under state/quality/ga-ix-settheory/.

.PARAMETER SkipEmit
    Reuse the newest existing ga-settheory-*.json instead of re-running GA.

.PARAMETER Extension
    Path to ix.duckdb_extension. Defaults to $env:IX_DUCK_EXT, else
    ../ix/crates/ix-duck-ext/ix.duckdb_extension next to the repo.
#>
[CmdletBinding()]
param(
    [switch] $SkipEmit,
    [string] $Extension
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$repoRoot    = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$outDir      = Join-Path $repoRoot 'state/quality/ga-ix-settheory'
$sqlTemplate = Join-Path $PSScriptRoot 'ix-ga-settheory-crosscheck.sql'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function Fail([string] $msg, [int] $code = 1) {
    Write-Host "SETTHEORY-XCHECK FAILED: $msg" -ForegroundColor Red
    exit $code
}

# ── 0. Prerequisites ─────────────────────────────────────────────────────────
$duckdb = (Get-Command duckdb -ErrorAction SilentlyContinue)?.Source
if (-not $duckdb) { Fail "duckdb.exe not on PATH (winget install DuckDB.cli)." 2 }

if (-not $Extension) { $Extension = $env:IX_DUCK_EXT }
if (-not $Extension) { $Extension = Join-Path $repoRoot '../ix/crates/ix-duck-ext/ix.duckdb_extension' }
if (-not (Test-Path $Extension)) {
    Fail "ix.duckdb_extension not found at '$Extension'. Build it: pwsh ../ix/crates/ix-duck-ext/build.ps1, or pass -Extension <path>." 2
}
$Extension = (Resolve-Path $Extension).Path
if (-not (Test-Path $sqlTemplate)) { Fail "SQL template missing at $sqlTemplate." 2 }

# ── 1. Produce / locate the GA corpus ────────────────────────────────────────
if (-not $SkipEmit) {
    Write-Host "Emitting GA set-theory corpus (SetTheoryCrossCheckCorpusEmitter)..." -ForegroundColor Cyan
    $testProj = Join-Path $repoRoot 'Tests/Common/GA.Business.Core.Tests/GA.Business.Core.Tests.csproj'
    & dotnet test $testProj --filter 'FullyQualifiedName~SetTheoryCrossCheckCorpusEmitter' --nologo -v q
    if ($LASTEXITCODE -ne 0) { Fail "GA corpus emitter test failed (exit $LASTEXITCODE)." $LASTEXITCODE }
}

$corpus = Get-ChildItem -Path $outDir -Filter 'ga-settheory-*.json' -ErrorAction SilentlyContinue |
          Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $corpus) { Fail "no ga-settheory-*.json under $outDir. Run without -SkipEmit." 2 }
Write-Host "Using GA corpus: $($corpus.Name)" -ForegroundColor Cyan

# ── 2. Substitute placeholders + run DuckDB + IX UDFs ────────────────────────
$fs = { param($p) ($p -replace '\\', '/') }
$sql = Get-Content $sqlTemplate -Raw
$sql = $sql.Replace('__EXT__',    (& $fs $Extension))
$sql = $sql.Replace('__CORPUS__', (& $fs $corpus.FullName))
$sql = $sql.Replace('__OUTDIR__', (& $fs $outDir))

$tmpSql = Join-Path ([System.IO.Path]::GetTempPath()) "settheory-xcheck-$([System.IO.Path]::GetRandomFileName()).sql"
Set-Content -Path $tmpSql -Value $sql -Encoding UTF8

Write-Host "Running DuckDB + IX set-theory UDFs (ix_icv / ix_prime_form / ix_forte_number)..." -ForegroundColor Cyan
$report = (Get-Content $tmpSql -Raw | & $duckdb -unsigned 2>&1 | Out-String)
$duckExit = $LASTEXITCODE
Remove-Item $tmpSql -Force -ErrorAction SilentlyContinue

if ($duckExit -ne 0) {
    Write-Host $report
    Fail "duckdb exited $duckExit (extension load or SQL error above). NOT a clean result." $duckExit
}
if ($report -notmatch 'agree_pct') {
    Write-Host $report
    Fail "duckdb produced no ICV cross-check output — treating as could-not-run, not clean." 3
}

# ── 3. Assemble the markdown report + flag any ICV disagreement ──────────────
$stamp = (Get-Date).ToString('yyyy-MM-dd')
$reportPath = Join-Path $outDir "ix-ga-settheory-$stamp.md"
$header = @"
# IX ⟷ GA set-theory cross-check — $stamp

Corpus: ``$($corpus.Name)`` · extension: ``$(Split-Path $Extension -Leaf)`` · engine: DuckDB + IX UDFs.

Two independent set-theory implementations — GA's C# ``SetClass`` engine and IX's
Rust ``ix-bracelet`` (via ``ix_icv`` / ``ix_prime_form`` / ``ix_forte_number``).
The **interval-class vector is convention-free**, so ``ix_icv`` MUST equal GA's
ICV for every set class — any mismatch is a real bug. Prime-form / Forte
differences are **informational** (Rahn vs Forte 1973 convention — the gap PR
#414 addressed).

Sidecars: ``settheory-icv-disagreements.json`` (should be empty),
``settheory-primeform-differences.json``.

"@
Set-Content -Path $reportPath -Value ($header + $report) -Encoding UTF8

Write-Host ""
Write-Host $report

# Surface an ICV mismatch loudly — that is the load-bearing assertion.
$icvLine = ($report -split "`n" | Where-Object { $_ -match 'icv_disagree' -or $_ -match '^\|\s*\d' } )
if ($report -match 'icv_disagree') {
    # The data row under the summary header has icv_disagree as the 3rd column.
    $dataRow = ($report -split "`n" | Where-Object { $_ -match '^\|\s*\d+\s*\|' } | Select-Object -First 1)
    if ($dataRow -match '^\|\s*\d+\s*\|\s*\d+\s*\|\s*(\d+)\s*\|') {
        $disagree = [int]$Matches[1]
        if ($disagree -gt 0) {
            Write-Host "WARNING: $disagree ICV disagreement(s) between GA and IX — see $reportPath" -ForegroundColor Yellow
        } else {
            Write-Host "ICV cross-check: GA and IX agree on every set class." -ForegroundColor Green
        }
    }
}
Write-Host "Report written: $reportPath" -ForegroundColor Green
