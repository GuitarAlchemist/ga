# run-prompt-corpus.ps1 — Run the GA chatbot prompt corpus + report failures
#
# Usage:
#   pwsh Scripts/run-prompt-corpus.ps1                  # run full corpus, summarize
#   pwsh Scripts/run-prompt-corpus.ps1 -Worst 3         # only print the 3 worst failures
#   pwsh Scripts/run-prompt-corpus.ps1 -Json out.json   # also write machine-readable summary
#
# Intended for two callers:
#   1. Humans running a quick health check on the deployed chatbot.
#   2. The chatbot-improvement Cherny loop — picks the worst-scoring prompt
#      each iteration as the next target to fix.
#
# The corpus and its invariants live in
# Tests/Apps/GaChatbot.Api.Tests/Corpus/prompts.yaml. PromptCorpusTests.cs
# is the canonical runner; this script is a thin wrapper that surfaces
# results in a loop-friendly shape.

[CmdletBinding()]
param(
    [int]$Worst = 0,
    [string]$Json = "",
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$testProj = Join-Path $repoRoot "Tests/Apps/GaChatbot.Api.Tests/GaChatbot.Api.Tests.csproj"

Write-Host "─── Running chatbot prompt corpus ───" -ForegroundColor Cyan

# Use the [Explicit] full-corpus test. --filter overrides the Explicit gate.
$args = @(
    "test", $testProj,
    "-c", "Debug",
    "--filter", "FullyQualifiedName~PromptCorpusTests.EveryPrompt_SatisfiesItsInvariants",
    "--logger", "console;verbosity=normal"
)
if ($NoBuild) { $args += "--no-build" }

$tmpLog = New-TemporaryFile
$proc = Start-Process -FilePath dotnet -ArgumentList $args -NoNewWindow -Wait -PassThru `
    -RedirectStandardOutput $tmpLog -RedirectStandardError "$tmpLog.err"

$out = Get-Content $tmpLog -Raw
Remove-Item $tmpLog, "$tmpLog.err" -ErrorAction SilentlyContinue

# Parse failures: the test aggregates them as a multi-line message after
# "Prompts violating invariants (N):" — extract those lines.
$failures = @()
if ($out -match "Prompts violating invariants \((\d+)\):\s*(?:\r?\n\s*-\s*(.+))+") {
    $matches = [regex]::Matches($out, "^\s*-\s*(.+)$", "Multiline")
    foreach ($m in $matches) {
        $line = $m.Groups[1].Value.Trim()
        if ($line) { $failures += $line }
    }
}

$warnings = @()
if ($out -match "Warnings \(\d+\):") {
    $warnMatches = [regex]::Matches($out, "^\s*!\s*(.+)$", "Multiline")
    foreach ($m in $warnMatches) {
        $line = $m.Groups[1].Value.Trim()
        if ($line) { $warnings += $line }
    }
}

# Summary
Write-Host ""
if ($failures.Count -eq 0) {
    Write-Host "✓ All prompts passed." -ForegroundColor Green
} else {
    Write-Host "✗ $($failures.Count) prompt(s) failed invariants:" -ForegroundColor Red
    $toShow = if ($Worst -gt 0) { $failures | Select-Object -First $Worst } else { $failures }
    foreach ($f in $toShow) {
        Write-Host "  - $f" -ForegroundColor Yellow
    }
}

if ($warnings.Count -gt 0) {
    Write-Host ""
    Write-Host "$($warnings.Count) latency warning(s):" -ForegroundColor DarkYellow
    foreach ($w in $warnings | Select-Object -First 5) {
        Write-Host "  ! $w" -ForegroundColor DarkYellow
    }
}

if ($Json) {
    $summary = @{
        timestamp = (Get-Date -Format "o")
        totalFailures = $failures.Count
        totalWarnings = $warnings.Count
        failures = $failures
        warnings = $warnings
        exitCode = $proc.ExitCode
    }
    $summary | ConvertTo-Json -Depth 4 | Set-Content $Json -Encoding UTF8
    Write-Host ""
    Write-Host "Wrote summary to $Json" -ForegroundColor Cyan
}

exit $proc.ExitCode
