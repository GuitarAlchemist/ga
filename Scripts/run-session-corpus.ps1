# run-session-corpus.ps1 — Replay the multi-turn chatbot session corpus + emit a snapshot
#
# Usage:
#   pwsh Scripts/run-session-corpus.ps1                 # replay, summarize
#   pwsh Scripts/run-session-corpus.ps1 -Snapshot       # also write state/quality/chatbot-qa-sessions/YYYY-MM-DD.json
#   pwsh Scripts/run-session-corpus.ps1 -NoBuild        # skip build (DLL must be current)
#
# The multi-turn counterpart to run-prompt-corpus.ps1. Drives
# SessionCorpusTests.EverySession_SatisfiesItsInvariants against GaChatbot.Api,
# parses the canonical summary line it prints, and emits a trend-shaped JSON the
# improvement loop can target on the SESSION axis (lost context, follow-up
# fallback) — distinct from the single-turn pass_pct.
#
# Oracle-paranoia (see docs/solutions/tooling/2026-05-16-auto-optimize-oracle-
# silent-success-build-failure.md): if the replay could not run (build failed,
# host down, zero turns), this writes NO snapshot and exits 2 — a run that
# couldn't execute must never read as a passing baseline.

[CmdletBinding()]
param(
    [switch]$NoBuild,
    [switch]$Snapshot
)
$ErrorActionPreference = 'Stop'
# Capture native dotnet output as UTF-8 so the test's → / … glyphs survive the
# pipe; we still sanitize to ASCII before persisting the snapshot.
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$repoRoot = Split-Path $PSScriptRoot -Parent
$testProj = Join-Path $repoRoot 'Tests/Apps/GaChatbot.Api.Tests/GaChatbot.Api.Tests.csproj'

Write-Host '─── Replaying chatbot session corpus ───' -ForegroundColor Cyan

$dotnetArgs = @(
    'test', $testProj, '-c', 'Debug',
    '--filter', 'FullyQualifiedName~SessionCorpusTests.EverySession_SatisfiesItsInvariants',
    '--logger', 'console;verbosity=normal'
)
if ($NoBuild) { $dotnetArgs += '--no-build' }

$out = (& dotnet @dotnetArgs 2>&1 | ForEach-Object { $_.ToString() }) -join "`n"
$dotnetExit = $LASTEXITCODE

# Canonical summary line emitted by the test:
#   "Sessions: 4  ·  turns: 11/16 passed  ·  session_pass_pct=68.8%"
$summary = [regex]::Match($out, 'Sessions:\s*(\d+)\s*.*?turns:\s*(\d+)/(\d+)\s*passed.*?session_pass_pct=([\d.]+)%')
if (-not $summary.Success) {
    Write-Host '✗ Oracle did NOT run cleanly — no session summary line found.' -ForegroundColor Red
    Write-Host "  dotnet test exit=$dotnetExit. Likely a build failure or host that never booted." -ForegroundColor Red
    if ($out -match 'MSB3027|MSB3021|file is locked') {
        Write-Host '  Cause: a DLL is locked by a running host (GaChatbot.Api). Stop it and retry.' -ForegroundColor DarkYellow
    }
    ($out -split "`r?`n" | Select-Object -Last 20) | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }
    exit 2
}

$sessions   = [int]$summary.Groups[1].Value
$passedT    = [int]$summary.Groups[2].Value
$totalT     = [int]$summary.Groups[3].Value
$passPct    = [double]$summary.Groups[4].Value / 100.0

if ($totalT -lt 1) {
    Write-Host '✗ Oracle ran but replayed 0 turns — treating as couldnt_run.' -ForegroundColor Red
    exit 2
}

# Failure lines: after "violating invariants (N):", each "  - ..." bullet.
$failures = @()
$fm = [regex]::Match($out, 'violating invariants \(\d+\):')
if ($fm.Success) {
    $block = $out.Substring($fm.Index + $fm.Length)
    $end = [regex]::Match($block, '(?m)^\s*$|^(But was|Test Run|Total tests|Passed!|Failed!)').Index
    if ($end -gt 0) { $block = $block.Substring(0, $end) }
    foreach ($m in [regex]::Matches($block, '^\s*-\s*(.+)$', 'Multiline')) {
        $line = $m.Groups[1].Value.Trim()
        if ($line) { $failures += $line }
    }
    # Normalize to clean ASCII for the committed artifact: arrow → '->',
    # ellipsis → '...', then drop any other non-ASCII so the snapshot is
    # diff-stable regardless of the runner's console code page.
    $failures = @($failures | ForEach-Object {
        ($_ -replace '→', ' -> ' -replace '…', '...' -replace '[^\x20-\x7E]', '').Trim()
    } | Select-Object -Unique)
}

Write-Host ''
Write-Host ("Sessions: {0}  ·  turns: {1}/{2} passed  ·  session_pass_pct={3:P1}" -f $sessions, $passedT, $totalT, $passPct) `
    -ForegroundColor ($(if ($failures.Count -eq 0) { 'Green' } else { 'Yellow' }))
foreach ($f in $failures) { Write-Host "  ✗ $f" -ForegroundColor Red }

if ($Snapshot) {
    $snapDir = Join-Path $repoRoot 'state/quality/chatbot-qa-sessions'
    New-Item -ItemType Directory -Force -Path $snapDir | Out-Null
    $stamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd')
    $snapPath = Join-Path $snapDir "$stamp.json"
    $snap = [ordered]@{
        schema_version   = 1
        date             = $stamp
        metric           = 'session_pass_pct'
        sessions         = $sessions
        turns_total      = $totalT
        turns_passed     = $passedT
        session_pass_pct = [math]::Round($passPct, 4)
        failures         = $failures
        degraded         = $false
        source           = 'run-session-corpus'
    }
    $snap | ConvertTo-Json -Depth 5 | Set-Content -Path $snapPath -Encoding utf8
    Write-Host "Wrote session snapshot to $snapPath" -ForegroundColor Cyan
}

# Mirror run-prompt-corpus: invariant failures are the metric signal, not an
# infrastructure error — exit 0 so the loop reads the snapshot, not a crash.
exit 0
