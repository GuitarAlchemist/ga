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
# Capture stderr tail BEFORE deleting — rel-002 from PR #229 review. The
# fail-loud branch surfaces this to operators so they see the real MSB3027
# / SDK error instead of a guess.
$errTail = if (Test-Path "$tmpLog.err") {
    (Get-Content "$tmpLog.err" -Tail 30 -ErrorAction SilentlyContinue) -join "`n"
} else { '' }
Remove-Item $tmpLog, "$tmpLog.err" -ErrorAction SilentlyContinue

# Did the test runner actually complete? If a build failure (or any other
# pre-test crash) prevented EveryPrompt_SatisfiesItsInvariants from running,
# none of the completion markers will appear in $out. We MUST detect that
# before any "all passed" claim — otherwise an unattended /auto-optimize
# loop would treat the build failure as a perfect baseline. See
# docs/solutions/tooling/2026-05-16-auto-optimize-oracle-silent-success-build-failure.md.
#
# Marker disjunction covers both VSTest console-logger output ("Passed! - Failed: 0") AND the newer
# Microsoft.Testing.Platform output ("Tests passed", lowercase "failed: 0").
# rel-001 from PR #229 review: add lowercase variants and a deterministic
# "Total tests: N" detector so we can distinguish "ran 1 test that passed"
# from "ran 0 tests" (the --no-build + missing DLL false-green path).
$runnerCompleted = $out -match 'Prompts violating invariants \(\d+\):' `
                -or $out -match '✓ All prompts passed' `
                -or $out -match 'Passed!\s*-?\s*Failed:\s*0' `
                -or $out -match 'Test Run Successful' `
                -or $out -match '(?i)\bTests\s+passed:\s*\d+' `
                -or $out -match '(?i)\bfailed:\s*0\b'

# rel-008: enforce that at least one test actually ran. dotnet test emits
# "Total tests: N" (vstest console) or "test execution summary ... total: N"
# (microsoft.testing.platform). If no count is found OR N == 0, treat as
# runner-did-not-complete regardless of the marker above. This catches the
# --no-build + missing DLL scenario where 0 tests are discovered and the
# runner reports "Test Run Successful".
$totalTestsRan = $null
if ($out -match '(?i)Total tests:\s*(\d+)') {
    $totalTestsRan = [int]$Matches[1]
} elseif ($out -match '(?i)total:\s*(\d+)\s*,\s*duration') {
    # microsoft.testing.platform format
    $totalTestsRan = [int]$Matches[1]
}
if ($null -ne $totalTestsRan -and $totalTestsRan -lt 1) {
    $runnerCompleted = $false
}

# Parse failures: the test aggregates them as a multi-line message after
# "Prompts violating invariants (N):" — extract those lines. rel-004: scope
# the global scan to the SUBSTRING after the marker so ambient `- ` lines
# (NuGet restore, test-discovery bullets, etc.) elsewhere in the log don't
# pollute the failure list.
$failures = @()
$failureMatch = [regex]::Match($out, "Prompts violating invariants \(\d+\):", 'Multiline')
if ($failureMatch.Success) {
    $failureBlock = $out.Substring($failureMatch.Index + $failureMatch.Length)
    # Stop at the next blank-line break OR the test-platform's summary line,
    # whichever comes first, to bound the scan tightly.
    $endIdx = [regex]::Match($failureBlock, '(?m)^\s*$|^(Passed!|Failed!|Test Run|Total tests)').Index
    if ($endIdx -gt 0) { $failureBlock = $failureBlock.Substring(0, $endIdx) }
    # rel-006: renamed from $matches (PowerShell auto-variable) to avoid collision.
    $failureMatches = [regex]::Matches($failureBlock, "^\s*-\s*(.+)$", 'Multiline')
    foreach ($m in $failureMatches) {
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

# rel-003: a green completion marker with a non-zero exit code is still
# a failure. Cross-check; if exit != 0 we treat the whole run as unsafe
# regardless of whether the failure-parser found anything to print.
$oracleHealthy = $runnerCompleted -and ($proc.ExitCode -eq 0)

# Summary
Write-Host ""
if (-not $oracleHealthy) {
    Write-Host "✗ Oracle did NOT run cleanly." -ForegroundColor Red
    Write-Host "  dotnet test exit=$($proc.ExitCode); runner_completed=$runnerCompleted; total_tests_ran=$(if ($null -eq $totalTestsRan) { '<unknown>' } else { $totalTestsRan })." -ForegroundColor Red
    # rel-010: tiered failure-mode messages instead of a single guess.
    if ($errTail -match 'MSB3027|MSB3021|file is locked') {
        Write-Host "  Cause (from stderr): build failed — a DLL is locked by a running host (GaChatbot.Api or GaApi)." -ForegroundColor DarkYellow
        Write-Host "  Fix: Stop-Process -Name 'GaChatbot.Api' -Force; then retry." -ForegroundColor DarkYellow
    } elseif ($errTail -match 'MSB1009|did not specify a project|workload') {
        Write-Host "  Cause (from stderr): SDK / project-resolution failure. Run 'dotnet build' separately and inspect." -ForegroundColor DarkYellow
    } elseif ($null -ne $totalTestsRan -and $totalTestsRan -lt 1) {
        Write-Host "  Cause: 0 tests discovered. Likely --no-build with stale/missing binary, or test-discovery loaded the wrong assembly." -ForegroundColor DarkYellow
    } elseif (-not $runnerCompleted) {
        Write-Host "  Cause: no parseable verdict line found. dotnet test may have crashed before emitting a summary." -ForegroundColor DarkYellow
    } else {
        Write-Host "  Cause: marker found but exit code is non-zero — a non-corpus test (or teardown) failed." -ForegroundColor DarkYellow
    }
    if ($errTail) {
        Write-Host "  --- stderr tail (last 30 lines) ---" -ForegroundColor DarkGray
        foreach ($line in ($errTail -split "`n" | Select-Object -Last 20)) {
            Write-Host "  $line" -ForegroundColor DarkGray
        }
    }
    Write-Host "  Do NOT trust any 0-failure verdict from this run." -ForegroundColor Red
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
    if (-not $oracleHealthy) {
        # Fail-loud shape: explicit nulls (NOT empty arrays) so the
        # /auto-optimize loop's "no metric_value = refuse to read"
        # gate kicks in. See .claude/skills/auto-optimize/SKILL.md.
        # rel-002: stderr_tail surfaces the actual diagnostic to operators
        # and unattended consumers (e.g. CI logs).
        $summary = [ordered]@{
            timestamp             = (Get-Date -Format "o")
            oracle_status         = "build_or_runner_failed"
            metric_value          = $null
            worst_item            = $null
            worst_item_diagnostic = $null
            totalFailures         = $null
            totalWarnings         = $null
            totalTestsRan         = $totalTestsRan
            runnerCompleted       = $runnerCompleted
            failures              = $null
            warnings              = $null
            stderr_tail           = $errTail
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

        # Tighter regex than `^\s+skip:\s*true$` — handles YAML booleans
        # case-insensitively (skip: True, skip: TRUE) and trailing comments.
        # If the denominator is 0 (prompts.yaml missing / all skipped / parse
        # drift), emit `denominator_unavailable` instead of `ok` so the loop
        # sees explicit "metric not measurable" rather than silent null.
        if ($totalActive -lt 1) {
            $oracleStatus = 'denominator_unavailable'
            $metricValue  = $null
        } else {
            $oracleStatus = 'ok'
            $metricValue  = [math]::Round((($totalActive - $failures.Count) / $totalActive), 4)
        }
        $worst = if ($failures.Count -gt 0) { $failures[0] } else { $null }

        $summary = [ordered]@{
            timestamp             = (Get-Date -Format "o")
            oracle_status         = $oracleStatus
            metric_value          = $metricValue
            worst_item            = $worst
            worst_item_diagnostic = $worst
            totalActivePrompts    = $totalActive
            totalTestsRan         = $totalTestsRan
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

    # Guard: only emit a snapshot if the runner actually completed AND
    # exited cleanly. $oracleHealthy is computed once at script scope above;
    # using it here keeps -Snapshot and -Json in sync. Previously this
    # block recomputed $runnerCompleted with a narrower marker disjunction
    # (rel-001 / PR #229 review). Single source of truth now.
    if (-not $oracleHealthy) {
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
