<#
.SYNOPSIS
    API Quality Gate — run by every agent before marking a BACKLOG item done.

.DESCRIPTION
    1. Builds GaApi and its test project.
    2. Runs integration tests (Category=Integration).
    3. Prints a pass/fail summary with test counts.
    4. Exits 0 only when the build succeeds AND no tests are failing.

.PARAMETER SkipBuild
    Skip the build step (useful when the binary is already up-to-date).

.PARAMETER Verbose
    Show individual test names in the summary.

.EXAMPLE
    pwsh Scripts/api-quality-check.ps1
    pwsh Scripts/api-quality-check.ps1 -SkipBuild
    pwsh Scripts/api-quality-check.ps1 -Verbose
#>
param(
    [switch]$SkipBuild,
    [switch]$Verbose
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location $repoRoot

$exitCode = 0

# ── helpers ────────────────────────────────────────────────────────────────────
function Write-Header($text) {
    Write-Host ""
    Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  $text" -ForegroundColor Cyan
    Write-Host "══════════════════════════════════════════════════" -ForegroundColor Cyan
}

function Write-Ok($text)   { Write-Host "  ✓ $text" -ForegroundColor Green }
function Write-Fail($text) { Write-Host "  ✗ $text" -ForegroundColor Red }
function Write-Info($text) { Write-Host "  · $text" -ForegroundColor Gray }

# ── 1. build ───────────────────────────────────────────────────────────────────
if (-not $SkipBuild) {
    Write-Header "Build"
    $buildOutput = dotnet build Apps/ga-server/GaApi/GaApi.csproj -c Debug --no-restore 2>&1
    $buildSuccess = $LASTEXITCODE -eq 0

    if ($buildSuccess) {
        $warnings = ($buildOutput | Select-String "Warning\(s\)" | Select-Object -Last 1)
        Write-Ok "GaApi built successfully  $warnings"
    } else {
        Write-Fail "Build FAILED"
        $buildOutput | Select-String "error " | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
        exit 1
    }

    # Also build the test project
    $testBuild = dotnet build Tests/Apps/GaApi.Tests/GaApi.Tests.csproj -c Debug --no-restore 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Ok "GaApi.Tests built successfully"
    } else {
        Write-Fail "GaApi.Tests build FAILED"
        $testBuild | Select-String "error " | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
        exit 1
    }
}

# ── 2. run integration tests ───────────────────────────────────────────────────
Write-Header "Integration Tests"

$trxFile = Join-Path $env:TEMP "gaapi-quality-$(Get-Date -Format 'yyyyMMddHHmmss').trx"

$testArgs = @(
    "test", "Tests/Apps/GaApi.Tests/GaApi.Tests.csproj",
    "--filter", "Category=Integration",
    "--no-build",
    "--logger", "trx;LogFileName=$trxFile",
    "--logger", "console;verbosity=minimal"
)

dotnet @testArgs 2>&1 | ForEach-Object {
    if ($Verbose -or $_ -match "Failed|Error|FAIL") {
        Write-Host "  $_"
    }
}
$testExitCode = $LASTEXITCODE

# ── 3. parse TRX for summary ───────────────────────────────────────────────────
$passed = 0; $failed = 0; $skipped = 0

if (Test-Path $trxFile) {
    [xml]$trx = Get-Content $trxFile
    $counters = $trx.TestRun.ResultSummary.Counters
    if ($counters) {
        $passed  = [int]$counters.passed
        $failed  = [int]$counters.failed
        $skipped = [int]($counters.notExecuted) + [int]($counters.ignored)
    }

    if ($Verbose -and $failed -gt 0) {
        Write-Host ""
        Write-Host "  Failed tests:" -ForegroundColor Red
        $trx.TestRun.Results.UnitTestResult |
            Where-Object { $_.outcome -eq 'Failed' } |
            ForEach-Object { Write-Host "    - $($_.testName)" -ForegroundColor Red }
    }
} else {
    Write-Info "TRX file not found — using exit code only"
}

# ── 4. summary ─────────────────────────────────────────────────────────────────
Write-Header "Summary"

Write-Info "Passed : $passed"
if ($skipped -gt 0) { Write-Info "Skipped: $skipped" }

if ($failed -gt 0 -or $testExitCode -ne 0) {
    Write-Fail "Failed : $failed  (exit code $testExitCode)"
    $exitCode = 1
} else {
    Write-Ok  "All $passed integration tests passed"
}

Write-Host ""
exit $exitCode
