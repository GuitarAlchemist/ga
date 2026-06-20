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

$runnerLines = & dotnet @args 2>&1
$dotnetExitCode = $LASTEXITCODE
$runnerOut = ($runnerLines | ForEach-Object { $_.ToString() }) -join "`n"
$out = $runnerOut

# Direct invocation keeps the same combined stream a terminal sees. Older
# Start-Process redirection lost NUnit's failure block on this runner, which
# made valid corpus failures look like build crashes. Keep a tail for fail-loud
# diagnostics even though stdout/stderr are now intentionally combined.
$errTail = if ($runnerOut) { ($runnerOut -split "`r?`n" | Select-Object -Last 30) -join "`n" } else { '' }

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
$runnerCompleted = $runnerOut -match 'Prompts violating invariants \(\d+\):' `
                -or $runnerOut -match '✓ All prompts passed' `
                -or $runnerOut -match 'Passed!\s*-?\s*Failed:\s*0' `
                -or $runnerOut -match 'Test Run Successful' `
                -or $runnerOut -match '(?i)\bTests\s+passed:\s*\d+' `
                -or $runnerOut -match '(?i)\bfailed:\s*0\b'

# rel-008: enforce that at least one test actually ran. dotnet test emits
# "Total tests: N" (vstest console) or "test execution summary ... total: N"
# (microsoft.testing.platform). If no count is found OR N == 0, treat as
# runner-did-not-complete regardless of the marker above. This catches the
# --no-build + missing DLL scenario where 0 tests are discovered and the
# runner reports "Test Run Successful".
$totalTestsRan = $null
if ($runnerOut -match '(?i)Total tests:\s*(\d+)') {
    $totalTestsRan = [int]$Matches[1]
} elseif ($runnerOut -match '(?i)total:\s*(\d+)\s*,\s*duration') {
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
$failureMatch = [regex]::Match($runnerOut, "Prompts violating invariants \(\d+\):", 'Multiline')
if ($failureMatch.Success) {
    $failureBlock = $runnerOut.Substring($failureMatch.Index + $failureMatch.Length)
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
if ($runnerOut -match "Warnings \(\d+\):") {
    $warnMatches = [regex]::Matches($runnerOut, "^\s*!\s*(.+)$", "Multiline")
    foreach ($m in $warnMatches) {
        $line = $m.Groups[1].Value.Trim()
        if ($line) { $warnings += $line }
    }
    $warnings = @($warnings | Select-Object -Unique)
}

# rel-003 originally treated every non-zero exit code as unsafe. That was too
# strict for this oracle: the canonical NUnit test exits 1 when prompt
# invariants fail, but those failures are precisely the metric signal the loop
# needs. A non-zero exit code is only infrastructure failure when the runner did
# not complete or failed without the corpus verdict block.
$oracleUsable = $runnerCompleted -and ($dotnetExitCode -eq 0 -or $failureMatch.Success)

# Summary
Write-Host ""
if (-not $oracleUsable) {
    Write-Host "✗ Oracle did NOT run cleanly." -ForegroundColor Red
    Write-Host "  dotnet test exit=$dotnetExitCode; runner_completed=$runnerCompleted; total_tests_ran=$(if ($null -eq $totalTestsRan) { '<unknown>' } else { $totalTestsRan })." -ForegroundColor Red
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
    if (-not $oracleUsable) {
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
            exitCode              = $dotnetExitCode
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
        foreach ($line in Get-Content $promptsYamlForMetric) {
            if ($line -match '^\s*-\s*prompt:') {
                if ($inBlock -and -not $blockSkipped) { $totalActive++ }
                $inBlock = $true
                $blockSkipped = $false
            } elseif ($inBlock -and $line -match '^\s+skip:\s*true') {
                $blockSkipped = $true
            }
        }
        if ($inBlock -and -not $blockSkipped) { $totalActive++ }

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
        # rel-007: renamed from $worst — case-insensitive collision with the
        # [int]$Worst parameter (line 24) coerced this failure STRING to Int32
        # and crashed the -Json write whenever a prompt failed (the one moment
        # the oracle must emit a metric). Same bug class as the rel-006 $matches
        # rename above. Keep this name distinct from the $Worst param.
        $worstItem = if ($failures.Count -gt 0) { $failures[0] } else { $null }

        $summary = [ordered]@{
            timestamp             = (Get-Date -Format "o")
            oracle_status         = $oracleStatus
            metric_value          = $metricValue
            worst_item            = $worstItem
            worst_item_diagnostic = $worstItem
            totalActivePrompts    = $totalActive
            totalTestsRan         = $totalTestsRan
            totalFailures         = $failures.Count
            totalWarnings         = $warnings.Count
            failures              = $failures
            warnings              = $warnings
            exitCode              = $dotnetExitCode
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

    # Guard: only emit a snapshot if the runner actually completed. Prompt
    # invariant failures are valid oracle signal, even though NUnit exits 1.
    # $oracleUsable is computed once at script scope above; using it here keeps
    # -Snapshot and -Json in sync. Previously this
    # block recomputed $runnerCompleted with a narrower marker disjunction
    # (rel-001 / PR #229 review). Single source of truth now.
    if (-not $oracleUsable) {
        Write-Host ""
        Write-Host "Snapshot skipped: corpus runner did not complete (test output did not match a known completion marker). Inspect the run before trusting any pass-rate number." -ForegroundColor Red
        exit $dotnetExitCode
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

    # Silent-rot fix (#43-followup): when the backend is degraded we used to
    # write `pass_pct: null` and call it a day. Eight days running of that
    # punched a `null` hole in the ix-quality-trend chart and nobody noticed
    # because the workflow stayed green ("green CI on dead workflow" —
    # `feedback_green_but_dead` rule). The new shape:
    #   - keep `pass_pct: null` so consumers that already special-case null
    #     (ix-quality-trend, emit-ga-retrieval-quality) keep working
    #   - add `degraded: true` so the JSON itself carries the diagnosis
    #   - add `last_known_good_pass_pct` + `last_known_good_date` carried
    #     forward from prior on-disk snapshots so the trend chart sees a
    #     flat line instead of a null gap
    #   - exit the script with code 2 so the workflow can route the
    #     degradation to the algedonic inbox + tracking issue
    function _FindLastKnownGood {
        param([string]$Dir, [string]$TodayStem)
        if (-not (Test-Path $Dir)) { return $null }
        # Iterate snapshots newest -> oldest, skipping today's file (which we
        # are about to overwrite) and skipping baseline.json / last.json
        # which are not date-stamped trend points.
        $candidates = Get-ChildItem -Path $Dir -Filter '*.json' -File |
            Where-Object { $_.BaseName -match '^\d{4}-\d{2}-\d{2}$' -and $_.BaseName -ne $TodayStem } |
            Sort-Object BaseName -Descending
        foreach ($f in $candidates) {
            try {
                $obj = Get-Content -Raw -Path $f.FullName -Encoding UTF8 | ConvertFrom-Json
                if ($null -ne $obj.pass_pct) {
                    return [pscustomobject]@{ pass_pct = [double]$obj.pass_pct; date = $f.BaseName; source = 'snapshot' }
                }
                # Walk further back through previously-degraded files that
                # themselves carried a last_known_good — chain the carryforward
                # so we don't lose history through a long degraded streak.
                if ($null -ne $obj.last_known_good_pass_pct) {
                    return [pscustomobject]@{
                        pass_pct = [double]$obj.last_known_good_pass_pct
                        date     = if ($obj.last_known_good_date) { [string]$obj.last_known_good_date } else { $f.BaseName }
                        source   = 'carryforward'
                    }
                }
            } catch { continue }
        }
        # Final fallback: the operator-pinned baseline.json. baseline.primary_baseline
        # is a 0..1 fraction; the trend snapshot uses 0..100 percent. Convert.
        $baselineFile = Join-Path $Dir 'baseline.json'
        if (Test-Path $baselineFile) {
            try {
                $baseObj = Get-Content -Raw -Path $baselineFile -Encoding UTF8 | ConvertFrom-Json
                if ($null -ne $baseObj.primary_baseline) {
                    # PowerShell's ConvertFrom-Json auto-parses ISO-8601 strings
                    # into [datetime]. Format back to ISO to keep the snapshot
                    # field a string in every locale.
                    $bdate = if ($baseObj.established_at -is [datetime]) {
                        $baseObj.established_at.ToUniversalTime().ToString("o")
                    } elseif ($baseObj.established_at) {
                        [string]$baseObj.established_at
                    } else {
                        'baseline.json'
                    }
                    return [pscustomobject]@{
                        pass_pct = [math]::Round([double]$baseObj.primary_baseline * 100, 2)
                        date     = $bdate
                        source   = 'baseline'
                    }
                }
            } catch {}
        }
        return $null
    }

    $snap = [ordered]@{
        timestamp     = (Get-Date -Format "o")
        total_prompts = $totalPrompts
    }
    if ($environmentDegraded) {
        $lkg = _FindLastKnownGood -Dir $snapshotDir -TodayStem $date
        $snap.pass_pct = $null
        $snap.degraded = $true
        $snap.degraded_reason = "backend_unavailable"
        $snap.note = "Environment degraded: $($envFailureFailures.Count)/$($failures.Count) failures were HTTP 5xx / network errors. pass_pct omitted — backend (Ollama / OPTIC-K index / deps) was unavailable, not the chatbot itself failing."
        if ($lkg) {
            $snap.last_known_good_pass_pct   = $lkg.pass_pct
            $snap.last_known_good_date       = $lkg.date
            $snap.last_known_good_source     = $lkg.source   # 'snapshot' | 'carryforward' | 'baseline'
        } else {
            $snap.last_known_good_pass_pct   = $null
            $snap.last_known_good_date       = $null
            $snap.last_known_good_source     = $null
        }
        # Don't write a by_category breakdown either — every category is
        # 0% for the same reason, and the per-category line would lock in
        # a misleading low baseline on the dashboard.
    } else {
        $snap.pass_pct = $overallPassPct
        $snap.degraded = $false
        $snap.by_category = $byCategory
    }
    $snap | ConvertTo-Json -Depth 4 | Set-Content $snapshotPath -Encoding UTF8
    Write-Host ""
    Write-Host "Wrote trend snapshot to $snapshotPath" -ForegroundColor Cyan

    # ── Unified Quality Gate Ledger (v1) — also emit a row ──
    # See ix/docs/contracts/2026-05-24-quality-gate-ledger.contract.md.
    # The per-domain snapshot file above stays as-is (it's the rich detail);
    # this is the cross-cutting one-row-per-run substrate that
    # ix_quality_gate_history + the dashboard tile read.
    try {
        $ledgerScript = Join-Path $PSScriptRoot 'gate-ledger-write-v1.ps1'
        if (Test-Path $ledgerScript) {
            $ledgerArgs = @{
                Source       = 'chatbot-qa'
                Domain       = 'chatbot'
                MetricName   = 'pass_pct'
                EvidenceKind = 'file'
                EvidenceRef  = "state/quality/chatbot-qa/$date.json"
                RepoRoot     = $repoRoot
            }
            if ($environmentDegraded) {
                $ledgerArgs.Decision    = 'skip'
                $ledgerArgs.MetricValue = if ($null -ne $snap.last_known_good_pass_pct) {
                    [double]$snap.last_known_good_pass_pct
                } else { 0.0 }
                $extraObj = [ordered]@{
                    value_unknown   = $true
                    degraded_reason = $snap.degraded_reason
                }
                if ($null -ne $snap.last_known_good_pass_pct) {
                    $extraObj.last_known_good_pass_pct = $snap.last_known_good_pass_pct
                    $extraObj.last_known_good_date     = $snap.last_known_good_date
                    $extraObj.last_known_good_source   = $snap.last_known_good_source
                }
                $ledgerArgs.ExtraJson = ($extraObj | ConvertTo-Json -Depth 4 -Compress)
            } else {
                # pass_pct is 0..100; a useful default threshold is 90.
                $ledgerArgs.Decision        = if ($overallPassPct -ge 90) { 'pass' } else { 'fail' }
                $ledgerArgs.MetricValue     = [double]$overallPassPct
                $ledgerArgs.MetricThreshold = 90.0
            }
            & $ledgerScript @ledgerArgs | Out-Null
        }
    } catch {
        Write-Warning "gate-ledger v1 emit failed (snapshot still written): $_"
    }

    if ($environmentDegraded) {
        $lkgPctDisplay = if ($snap.last_known_good_pass_pct -ne $null) { "$($snap.last_known_good_pass_pct)% (from $($snap.last_known_good_date))" } else { "<none on disk>" }
        Write-Host "  DEGRADED: backend unavailable; pass_pct=null; last_known_good=$lkgPctDisplay" -ForegroundColor Yellow
        # Exit code 2 = degraded-but-snapshot-written. The workflow uses this
        # to decide whether to emit an algedonic signal. We still want the
        # snapshot file committed (the carryforward IS the dashboard signal),
        # but we want the workflow to flag the run as needing attention.
        exit 2
    } else {
        Write-Host "  total=$totalPrompts (+$skippedPrompts skipped)  failed=$($failures.Count)  pass_pct=$overallPassPct%" -ForegroundColor Cyan
    }
}

exit $dotnetExitCode
