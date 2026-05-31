# emit-ga-retrieval-quality.ps1 — Workflow 2 producer: GA retrieval quality artifact
#
# Reads the latest committed chatbot-qa snapshot at state/quality/chatbot-qa/*.json
# (or state/quality/chatbot-qa/last.json if newer and present), compares it
# against the baseline at state/quality/ga-retrieval/baseline.json, and emits
# dist/ga-retrieval-quality.json — the evidence artifact consumed by
# agent-blackbox via --ga-retrieval-quality-cmd.
#
# This is a POST-PROCESSOR. It never runs the corpus runner itself, never
# spins up GaApi, never touches the chatbot. It reads existing on-disk
# snapshots (committed) and writes a verdict.
#
# Usage:
#   pwsh Scripts/emit-ga-retrieval-quality.ps1
#   pwsh Scripts/emit-ga-retrieval-quality.ps1 -Output dist/ga-retrieval-quality.json
#   pwsh Scripts/emit-ga-retrieval-quality.ps1 -SnapshotPath state/quality/chatbot-qa/2026-05-17.json
#   pwsh Scripts/emit-ga-retrieval-quality.ps1 -Stdout   # emit JSON to stdout (for agent-blackbox)
#
# Verdict thresholds (chosen for the post-#224 baseline at pass_pct=0.94):
#   pass : grounded did not drop AND unsupported did not rise
#   warn : grounded dropped by 1-2 OR unsupported rose by 1-2
#   fail : grounded dropped by 3+ OR unsupported rose by 3+
#
# An environment-degraded snapshot (pass_pct=null + note about HTTP 5xx)
# is reported as oracle_status="degraded" + verdict="warn" — backend
# unavailable is NOT a regression of the chatbot itself.

[CmdletBinding()]
param(
    [string]$Output = "",
    [string]$SnapshotPath = "",
    [string]$BaselinePath = "",
    [switch]$Quiet,
    [switch]$Stdout
)

# -Stdout suppresses informational chatter and prints the artifact JSON
# to standard output instead of writing it to a file. Used by
# agent-blackbox `analyze --ga-retrieval-quality-cmd`.
if ($Stdout) { $Quiet = $true }

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $Output) { $Output = Join-Path $repoRoot "dist/ga-retrieval-quality.json" }
if (-not $BaselinePath) { $BaselinePath = Join-Path $repoRoot "state/quality/ga-retrieval/baseline.json" }

function Write-Info($msg) { if (-not $Quiet) { Write-Host $msg -ForegroundColor Cyan } }
function Write-Warn($msg) { if (-not $Quiet) { Write-Host $msg -ForegroundColor Yellow } }
function Write-Err($msg)  { Write-Host  $msg -ForegroundColor Red }

# Locate the snapshot to read. Priority:
#   1. -SnapshotPath argument
#   2. state/quality/chatbot-qa/last.json (operator's most recent local run)
#   3. The lexicographically-latest state/quality/chatbot-qa/YYYY-MM-DD.json
function Resolve-Snapshot {
    if ($SnapshotPath) {
        $p = Resolve-Path $SnapshotPath -ErrorAction Stop
        return $p.Path
    }
    $chatbotDir = Join-Path $repoRoot "state/quality/chatbot-qa"
    if (-not (Test-Path $chatbotDir)) {
        throw "No chatbot-qa directory at $chatbotDir. Cannot build retrieval-quality artifact without an upstream snapshot."
    }
    $lastJson = Join-Path $chatbotDir "last.json"
    $dated = Get-ChildItem $chatbotDir -Filter "*.json" |
             Where-Object { $_.Name -match '^\d{4}-\d{2}-\d{2}\.json$' } |
             Sort-Object Name -Descending |
             Select-Object -First 1

    $candidates = @()
    if (Test-Path $lastJson) { $candidates += (Get-Item $lastJson) }
    if ($dated) { $candidates += $dated }
    if ($candidates.Count -eq 0) {
        throw "No chatbot-qa snapshot found under $chatbotDir. Run Scripts/run-prompt-corpus.ps1 -Snapshot first."
    }
    return ($candidates | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}

$snapshotFile = Resolve-Snapshot
Write-Info "─── GA retrieval quality producer ───"
Write-Info "  snapshot:  $snapshotFile"
Write-Info "  baseline:  $BaselinePath"
Write-Info "  output:    $Output"

if (-not (Test-Path $BaselinePath)) {
    throw "Baseline missing at $BaselinePath. Seed it before computing deltas — refusing to fabricate a baseline."
}

$snapshot = Get-Content $snapshotFile -Raw | ConvertFrom-Json
$baseline = Get-Content $BaselinePath -Raw | ConvertFrom-Json

# Translate a chatbot-qa snapshot into the four retrieval-quality counters.
#
# The current chatbot-qa runner reports per-invariant pass/fail, not the
# four GA-retrieval categories directly. The mapping is:
#   grounded            = total_prompts - totalFailures   (passed all invariants)
#   unsupported         = totalFailures                   (invariant violation = unsupported claim)
#   corpus_misses       = 0  (placeholder — runner does not yet split routing-miss from invariant-fail)
#   injection_refusals  = 0  (placeholder — corpus does not yet contain injection prompts)
#
# When the snapshot is environment-degraded (Ollama unavailable, HTTP 5xx),
# the counters are all null and the verdict is "warn" — the chatbot itself
# did not regress.
function Get-Counters($snap) {
    # Two snapshot shapes: the trend-style (`total_prompts`, `pass_pct`) and
    # the fail-loud style (`totalActivePrompts`, `totalFailures`).
    $hasFailLoudShape = ($null -ne $snap.totalActivePrompts) -or ($null -ne $snap.totalFailures)
    $hasTrendShape    = ($null -ne $snap.total_prompts) -or ($null -ne $snap.pass_pct)

    # Environment-degraded short-circuit (trend shape with pass_pct == null + note).
    if ($hasTrendShape -and ($null -eq $snap.pass_pct)) {
        return @{
            degraded = $true
            note     = $snap.note
            total    = [int]($snap.total_prompts ?? 0)
            counters = $null
        }
    }

    if ($hasFailLoudShape) {
        $total = [int]($snap.totalActivePrompts ?? 0)
        $failures = [int]($snap.totalFailures ?? 0)
        $grounded = [Math]::Max(0, $total - $failures)
        return @{
            degraded = $false
            total    = $total
            counters = @{
                grounded            = $grounded
                unsupported         = $failures
                corpus_misses       = 0
                injection_refusals  = 0
                total               = $total
            }
        }
    }

    if ($hasTrendShape) {
        $total = [int]($snap.total_prompts ?? 0)
        $passPct = [double]($snap.pass_pct ?? 0)
        $grounded = [int][Math]::Round($total * ($passPct / 100.0))
        $unsupported = [Math]::Max(0, $total - $grounded)
        return @{
            degraded = $false
            total    = $total
            counters = @{
                grounded            = $grounded
                unsupported         = $unsupported
                corpus_misses       = 0
                injection_refusals  = 0
                total               = $total
            }
        }
    }

    throw "Snapshot $snapshotFile does not match a known chatbot-qa shape."
}

function Get-BaselineCounters($base) {
    # Baseline file may carry an explicit `current` block (preferred) or
    # an embedded snapshot under `seed_snapshot`. Prefer explicit counters.
    if ($null -ne $base.counters) { return $base.counters }
    if ($null -ne $base.seed_snapshot) { return (Get-Counters $base.seed_snapshot).counters }
    throw "Baseline at $BaselinePath has neither .counters nor .seed_snapshot — cannot build deltas."
}

$current = Get-Counters $snapshot
$baseCounters = Get-BaselineCounters $baseline

if ($current.degraded) {
    Write-Warn "  snapshot is environment-degraded ($($current.note)); verdict will be 'warn' (backend, not chatbot)."
    $verdict = "warn"
    $regressions = @()
    $deltas = @{
        grounded_delta            = $null
        unsupported_delta         = $null
        corpus_misses_delta       = $null
        injection_refusals_delta  = $null
        regressions               = $regressions
    }
    $currentBlock = @{
        grounded            = $null
        unsupported         = $null
        corpus_misses       = $null
        injection_refusals  = $null
        total               = $current.total
        note                = $current.note
    }
} else {
    $c = $current.counters
    $b = $baseCounters
    $groundedDelta    = [int]$c.grounded            - [int]$b.grounded
    $unsupportedDelta = [int]$c.unsupported         - [int]$b.unsupported
    $missesDelta      = [int]$c.corpus_misses       - [int]$b.corpus_misses
    $injectionDelta   = [int]$c.injection_refusals  - [int]$b.injection_refusals

    # Verdict thresholds chosen for the post-#224 baseline at pass_pct=0.94
    # (47/50 grounded). Tune after a few PRs accumulate signal.
    $verdict = "pass"
    if ($groundedDelta -le -3 -or $unsupportedDelta -ge 3) {
        $verdict = "fail"
    } elseif ($groundedDelta -le -1 -or $unsupportedDelta -ge 1) {
        $verdict = "warn"
    }

    # Pull concrete regression examples from the snapshot's `failures` list
    # when available (fail-loud shape) so reviewers see WHICH prompts moved.
    $regressions = @()
    if ($null -ne $snapshot.failures) {
        foreach ($f in @($snapshot.failures)) {
            $regressions += @{
                prompt = [string]$f
                before = "passed at baseline"
                after  = "violated invariant"
            }
        }
    }

    $deltas = @{
        grounded_delta            = $groundedDelta
        unsupported_delta         = $unsupportedDelta
        corpus_misses_delta       = $missesDelta
        injection_refusals_delta  = $injectionDelta
        regressions               = $regressions
    }
    $currentBlock = $c
}

# Build the artifact (schema_version 1).
$producedAt = (Get-Date).ToUniversalTime().ToString("o")
$baselineRefRel = (Resolve-Path -Relative $BaselinePath).Replace('\','/')
$snapshotRefRel = (Resolve-Path -Relative $snapshotFile).Replace('\','/')

$artifact = [ordered]@{
    schema_version = 1
    produced_at    = $producedAt
    baseline_ref   = $baselineRefRel
    snapshot_ref   = $snapshotRefRel
    current        = $currentBlock
    baseline       = $baseCounters
    deltas         = $deltas
    verdict        = $verdict
}

$json = $artifact | ConvertTo-Json -Depth 8
if ($Stdout) {
    # Emit JSON to stdout for agent-blackbox consumption. Nothing else writes to
    # stdout in this mode (informational lines are gated on -Quiet).
    [Console]::Out.Write($json)
} else {
    $outDir = Split-Path $Output -Parent
    if ($outDir -and -not (Test-Path $outDir)) {
        New-Item -ItemType Directory -Path $outDir -Force | Out-Null
    }
    $json | Set-Content $Output -Encoding UTF8

    # ── Unified Quality Gate Ledger (v1) — also emit a row ──
    # See ix/docs/contracts/2026-05-24-quality-gate-ledger.contract.md.
    try {
        $ledgerScript = Join-Path $PSScriptRoot 'gate-ledger-write-v1.ps1'
        if (Test-Path $ledgerScript) {
            $ledgerDecision = switch ($verdict) {
                'pass' { 'pass' }
                'warn' { 'warn' }
                'fail' { 'fail' }
                default { 'skip' }
            }
            # Headline metric is grounded count; threshold is grounded baseline.
            $ledgerValue = if ($null -ne $currentBlock.grounded) { [double]$currentBlock.grounded } else { 0.0 }
            $ledgerThreshold = if ($null -ne $baseCounters.grounded) { [double]$baseCounters.grounded } else { $null }
            $extra = [ordered]@{
                verdict             = $verdict
                unsupported         = $currentBlock.unsupported
                corpus_misses       = $currentBlock.corpus_misses
                injection_refusals  = $currentBlock.injection_refusals
            }
            if ($current.degraded) {
                $extra.degraded         = $true
                $extra.value_unknown    = $true
            }
            $ledgerArgs = @{
                RepoRoot     = $repoRoot
                Source       = 'ga-retrieval'
                Domain       = 'chatbot'
                Decision     = $ledgerDecision
                MetricName   = 'grounded_count'
                MetricValue  = $ledgerValue
                EvidenceKind = 'file'
                EvidenceRef  = $Output
                ExtraJson    = ($extra | ConvertTo-Json -Depth 4 -Compress)
            }
            if ($null -ne $ledgerThreshold) {
                $ledgerArgs.MetricThreshold = $ledgerThreshold
            }
            & $ledgerScript @ledgerArgs | Out-Null
        }
    } catch {
        Write-Warning "gate-ledger v1 emit failed (artifact still written): $_"
    }
}

if (-not $Stdout) {
    Write-Info ""
    Write-Info "─── verdict: $verdict ───"
    if (-not $current.degraded) {
        Write-Info ("  grounded:           {0} (baseline {1}, Δ {2:+#;-#;0})" -f $currentBlock.grounded, $baseCounters.grounded, $deltas.grounded_delta)
        Write-Info ("  unsupported:        {0} (baseline {1}, Δ {2:+#;-#;0})" -f $currentBlock.unsupported, $baseCounters.unsupported, $deltas.unsupported_delta)
        Write-Info ("  corpus_misses:      {0} (baseline {1}, Δ {2:+#;-#;0})" -f $currentBlock.corpus_misses, $baseCounters.corpus_misses, $deltas.corpus_misses_delta)
        Write-Info ("  injection_refusals: {0} (baseline {1}, Δ {2:+#;-#;0})" -f $currentBlock.injection_refusals, $baseCounters.injection_refusals, $deltas.injection_refusals_delta)
    }
    Write-Info "  wrote artifact to: $Output"
}

# Exit-code policy mirrors run-prompt-corpus.ps1: emit the artifact and exit 0
# even on warn/fail. The consumer (agent-blackbox) is the one that gates.
exit 0
