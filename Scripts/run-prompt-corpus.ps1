# run-prompt-corpus.ps1 — Run the GA chatbot prompt corpus + report failures
#
# Usage:
#   pwsh Scripts/run-prompt-corpus.ps1                  # run full corpus, summarize
#   pwsh Scripts/run-prompt-corpus.ps1 -Worst 3         # only print the 3 worst failures
#   pwsh Scripts/run-prompt-corpus.ps1 -Json out.json   # also write machine-readable summary
#   pwsh Scripts/run-prompt-corpus.ps1 -Snapshot        # also write state/quality/chatbot-qa/YYYY-MM-DD.json
#
# Intended for three callers:
#   1. Humans running a quick health check on the deployed chatbot.
#   2. The chatbot-improvement Cherny loop — picks the worst-scoring prompt
#      each iteration as the next target to fix.
#   3. The daily chatbot-qa-snapshot CI workflow — emits a trend-shaped
#      JSON consumed by ix-quality-trend (ChatbotQaSnapshot schema in
#      ix/crates/ix-quality-trend/src/snapshot.rs).
#
# The corpus and its invariants live in
# Tests/Apps/GaChatbot.Api.Tests/Corpus/prompts.yaml. PromptCorpusTests.cs
# is the canonical runner; this script is a thin wrapper that surfaces
# results in a loop-friendly shape.

[CmdletBinding()]
param(
    [int]$Worst = 0,
    [string]$Json = "",
    [switch]$NoBuild,
    [switch]$Snapshot
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

if ($Snapshot) {
    # Trend-shaped snapshot for ix-quality-trend. Schema lives in
    # ix/crates/ix-quality-trend/src/snapshot.rs (ChatbotQaSnapshot,
    # ChatbotCategoryStats). Field names are snake_case to match serde
    # default deserialization (no rename_all attribute on the struct).

    # Guard: only emit a snapshot if the runner actually completed.
    # Otherwise we'd report `pass_pct: 100` for a crashed run with
    # zero parsed failures — actively misleading.
    $runnerCompleted = $out -match 'Prompts violating invariants \(\d+\):' `
                    -or $out -match '✓ All prompts passed' `
                    -or $out -match 'Passed!\s*-?\s*Failed:\s*0' `
                    -or $out -match 'Test Run Successful'
    if (-not $runnerCompleted) {
        Write-Host ""
        Write-Host "Snapshot skipped: corpus runner did not complete (test output did not match a known completion marker). Inspect the run before trusting any pass-rate number." -ForegroundColor Red
        exit $proc.ExitCode
    }

    # Walk prompts.yaml to recover totals and per-category populations.
    # The corpus is the source of truth for "what was attempted"; the
    # parsed test output gives us "what failed".
    $promptsYaml = Join-Path $repoRoot "Tests/Apps/GaChatbot.Api.Tests/Corpus/prompts.yaml"
    $categoryTotals = @{}
    $totalPrompts = 0
    $skippedPrompts = 0
    $inPromptBlock = $false
    $currentCategory = $null
    $currentSkip = $false

    function _Flush {
        if (-not $script:inPromptBlock) { return }
        if ($script:currentSkip) {
            $script:skippedPrompts++
        } else {
            $script:totalPrompts++
            if ($script:currentCategory) {
                if (-not $script:categoryTotals.ContainsKey($script:currentCategory)) {
                    $script:categoryTotals[$script:currentCategory] = 0
                }
                $script:categoryTotals[$script:currentCategory]++
            }
        }
    }

    Get-Content $promptsYaml | ForEach-Object {
        if ($_ -match '^\s*-\s*prompt:') {
            _Flush
            $script:inPromptBlock = $true
            $script:currentCategory = $null
            $script:currentSkip = $false
        }
        elseif ($script:inPromptBlock -and $_ -match '^\s+category:\s*"?([^"#]+?)"?\s*(#.*)?$') {
            if ($null -eq $script:currentCategory) {
                $script:currentCategory = $matches[1].Trim()
            }
        }
        elseif ($script:inPromptBlock -and $_ -match '^\s+skip:\s*true') {
            $script:currentSkip = $true
        }
    }
    _Flush  # flush the last entry

    # Failures carry their category in a "[category] 'prompt'" prefix
    # emitted by PromptCorpusTests.EvaluatePromptAsync (line ~160).
    $categoryFailures = @{}
    foreach ($f in $failures) {
        if ($f -match '^\[([^\]]+)\]') {
            $cat = $matches[1].Trim()
            if (-not $categoryFailures.ContainsKey($cat)) {
                $categoryFailures[$cat] = 0
            }
            $categoryFailures[$cat]++
        }
    }

    $byCategory = [ordered]@{}
    foreach ($k in ($categoryTotals.Keys | Sort-Object)) {
        $total = $categoryTotals[$k]
        $fails = if ($categoryFailures.ContainsKey($k)) { $categoryFailures[$k] } else { 0 }
        $passPct = if ($total -gt 0) {
            [math]::Round((($total - $fails) / $total) * 100, 2)
        } else { 0 }
        $byCategory[$k] = [ordered]@{
            pass_pct = $passPct
            total   = $total
        }
    }

    $overallPassPct = if ($totalPrompts -gt 0) {
        [math]::Round((($totalPrompts - $failures.Count) / $totalPrompts) * 100, 2)
    } else { 0 }

    $date = Get-Date -Format "yyyy-MM-dd"
    $snapshotDir = Join-Path $repoRoot "state/quality/chatbot-qa"
    if (-not (Test-Path $snapshotDir)) {
        New-Item -ItemType Directory -Path $snapshotDir -Force | Out-Null
    }
    $snapshotPath = Join-Path $snapshotDir "$date.json"

    $snap = [ordered]@{
        timestamp     = (Get-Date -Format "o")
        total_prompts = $totalPrompts
        pass_pct      = $overallPassPct
        by_category   = $byCategory
    }
    $snap | ConvertTo-Json -Depth 4 | Set-Content $snapshotPath -Encoding UTF8
    Write-Host ""
    Write-Host "Wrote trend snapshot to $snapshotPath" -ForegroundColor Cyan
    Write-Host "  total=$totalPrompts (+$skippedPrompts skipped)  failed=$($failures.Count)  pass_pct=$overallPassPct%" -ForegroundColor Cyan
}

exit $proc.ExitCode
