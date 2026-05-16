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

# Did the test runner actually complete? If a build failure (or any other
# pre-test crash) prevented EveryPrompt_SatisfiesItsInvariants from running,
# none of the completion markers will appear in $out. We MUST detect that
# before any "all passed" claim — otherwise an unattended /auto-optimize
# loop would treat the build failure as a perfect baseline. See
# docs/solutions/tooling/2026-05-16-auto-optimize-oracle-silent-success-build-failure.md.
$runnerCompleted = $out -match 'Prompts violating invariants \(\d+\):' `
                -or $out -match '✓ All prompts passed' `
                -or $out -match 'Passed!\s*-?\s*Failed:\s*0' `
                -or $out -match 'Test Run Successful'

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
if (-not $runnerCompleted) {
    Write-Host "✗ Oracle did NOT run to completion." -ForegroundColor Red
    Write-Host "  dotnet test exit=$($proc.ExitCode); no parseable verdict line in output." -ForegroundColor Red
    Write-Host "  Common cause: a running GaChatbot.Api (or GaApi) is holding open bin/Debug/net10.0/*.dll." -ForegroundColor DarkYellow
    Write-Host "  Stop the host (Stop-Process -Id <pid> -Force) and retry. Do NOT trust any 0-failure verdict from this run." -ForegroundColor DarkYellow
} elseif ($failures.Count -eq 0) {
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
    if (-not $runnerCompleted) {
        # Fail-loud shape: explicit nulls (NOT empty arrays) so the
        # /auto-optimize loop's "no metric_value = refuse to read"
        # gate kicks in. See .claude/skills/auto-optimize/SKILL.md.
        $summary = [ordered]@{
            timestamp             = (Get-Date -Format "o")
            oracle_status         = "build_or_runner_failed"
            metric_value          = $null
            worst_item            = $null
            worst_item_diagnostic = $null
            totalFailures         = $null
            totalWarnings         = $null
            failures              = $null
            warnings              = $null
            exitCode              = $proc.ExitCode
        }
    } else {
        # Walk prompts.yaml once to recover the active-prompt total so
        # we can express metric_value as pass-rate. Mirrors the walk in
        # the -Snapshot branch below; kept inline to avoid changing the
        # snapshot logic in this fix.
        $promptsYamlForMetric = Join-Path $repoRoot "Tests/Apps/GaChatbot.Api.Tests/Corpus/prompts.yaml"
        $totalActive = 0
        $inBlock = $false
        $blockSkipped = $false
        $flushTotal = {
            if ($script:inBlock -and -not $script:blockSkipped) { $script:totalActive++ }
            $script:inBlock = $false
            $script:blockSkipped = $false
        }
        Get-Content $promptsYamlForMetric | ForEach-Object {
            if ($_ -match '^\s*-\s*prompt:') {
                & $flushTotal
                $script:inBlock = $true
            } elseif ($script:inBlock -and $_ -match '^\s+skip:\s*true') {
                $script:blockSkipped = $true
            }
        }
        & $flushTotal

        $metricValue = if ($totalActive -gt 0) {
            [math]::Round((($totalActive - $failures.Count) / $totalActive), 4)
        } else { $null }
        $worst = if ($failures.Count -gt 0) { $failures[0] } else { $null }

        $summary = [ordered]@{
            timestamp             = (Get-Date -Format "o")
            oracle_status         = "ok"
            metric_value          = $metricValue
            worst_item            = $worst
            worst_item_diagnostic = $worst
            totalActivePrompts    = $totalActive
            totalFailures         = $failures.Count
            totalWarnings         = $warnings.Count
            failures              = $failures
            warnings              = $warnings
            exitCode              = $proc.ExitCode
        }
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

    # Detect "environment failure" — when nearly every failure is an HTTP
    # 500/connection error, the chatbot backend (Ollama, OPTIC-K index,
    # ...) is unavailable rather than the chatbot answering badly.
    # Recording pass_pct=0 in that case actively pollutes the trend line
    # with a false floor. Emit null + a note instead so the dashboard
    # shows "no useful signal" rather than "the chatbot collapsed."
    $envFailureFailures = @($failures | Where-Object {
        $_ -match 'HTTP 5\d\d' -or `
        $_ -match 'connection refused' -or `
        $_ -match 'timed out' -or `
        $_ -match 'SocketException' -or `
        $_ -match 'No such host'
    })
    $envFailureRatio = if ($failures.Count -gt 0) {
        $envFailureFailures.Count / $failures.Count
    } else { 0 }
    $environmentDegraded = $envFailureRatio -ge 0.9

    $date = Get-Date -Format "yyyy-MM-dd"
    $snapshotDir = Join-Path $repoRoot "state/quality/chatbot-qa"
    if (-not (Test-Path $snapshotDir)) {
        New-Item -ItemType Directory -Path $snapshotDir -Force | Out-Null
    }
    $snapshotPath = Join-Path $snapshotDir "$date.json"

    $snap = [ordered]@{
        timestamp     = (Get-Date -Format "o")
        total_prompts = $totalPrompts
    }
    if ($environmentDegraded) {
        $snap.pass_pct = $null
        $snap.note = "Environment degraded: $($envFailureFailures.Count)/$($failures.Count) failures were HTTP 5xx / network errors. pass_pct omitted — backend (Ollama / OPTIC-K index / deps) was unavailable, not the chatbot itself failing."
        # Don't write a by_category breakdown either — every category is
        # 0% for the same reason, and the per-category line would lock in
        # a misleading low baseline on the dashboard.
    } else {
        $snap.pass_pct = $overallPassPct
        $snap.by_category = $byCategory
    }
    $snap | ConvertTo-Json -Depth 4 | Set-Content $snapshotPath -Encoding UTF8
    Write-Host ""
    Write-Host "Wrote trend snapshot to $snapshotPath" -ForegroundColor Cyan
    Write-Host "  total=$totalPrompts (+$skippedPrompts skipped)  failed=$($failures.Count)  pass_pct=$overallPassPct%" -ForegroundColor Cyan
}

exit $proc.ExitCode
