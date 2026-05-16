---
title: "/auto-optimize chatbot-qa oracle silently reports 'all passed' when build fails"
date: 2026-05-16
category: "tooling"
tags: [auto-optimize, oracle-reliability, silent-success, exitcode-ignored, false-green, iron-law, supervised-first-run]
symptoms: "Scripts/run-prompt-corpus.ps1 prints '✓ All prompts passed' and writes last.json with totalFailures:0 when the underlying `dotnet test` actually failed with build errors (DLL locked by a running GaChatbot.Api process). The /auto-optimize loop would treat this as a healthy 100% pass-rate baseline and proceed to invent fixes for problems that aren't real."
components:
  - Scripts/run-prompt-corpus.ps1
  - .claude/skills/auto-optimize/SKILL.md
  - state/quality/chatbot-qa/baseline.json
  - state/quality/chatbot-qa/last.json
severity: "load-bearing-infrastructure"
problem_type: "false-green-gate"
module: "auto-optimize-skill"
---

# Auto-optimize oracle silent-success on build failure

## Discovery context

First operator-supervised run of `/auto-optimize` on the `chatbot-qa`
domain (per digest 2026-05-16T06:00Z). Branch
`loop/chatbot-qa/2026-05-16`. The cycle 0 baseline call was:

```powershell
pwsh Scripts/run-prompt-corpus.ps1 -Worst 5 -Json state/quality/chatbot-qa/last.json
```

The output looked benign:

```
─── Running chatbot prompt corpus ───

✓ All prompts passed.

Wrote summary to state/quality/chatbot-qa/last.json
```

And `last.json` confirmed:

```json
{ "failures": [], "warnings": [], "totalFailures": 0, "totalWarnings": 0, "exitCode": 1, "timestamp": "..." }
```

The contradiction: `exitCode: 1` paired with "all prompts passed" and
`totalFailures: 0`. That's the smell.

## What actually happened

Running the underlying `dotnet test` directly (bypassing the
wrapper) surfaced the truth: the build **never succeeded**. A running
`GaChatbot.Api` instance (PID 69336) was holding open file handles
on the output DLLs:

```
error MSB3027: Could not copy "...GA.Domain.Repositories.dll"...
Exceeded retry count of 10. Failed. The file is locked by: "GaChatbot.Api (69336)"
error MSB4181: The "MSBuild" task returned false but did not log an error.
```

Build failed → tests never ran → `dotnet test` exited 1. But the
wrapper's failure-detection logic only scans for the line `"Prompts
violating invariants (N):"` in test output. When build fails, that
line never appears, the regex finds 0 matches, and the script
concludes "0 failures = all passed".

The `exitCode: 1` was captured in the JSON but never gated the
verdict.

## Why this matters for /auto-optimize

The Cherny loop's entire premise is that the oracle's metric is
trustworthy. Specifically:

1. Cycle N gets `metric_before = oracle()`.
2. Loop proposes a fix for the worst-scoring item.
3. Cycle N+1 gets `metric_after = oracle()`.
4. Roundtrip validator compares `before` vs `after`.

If the oracle silently returns "100% pass" whenever the build is
broken, then:

- The loop sees a perfect baseline and exits "converged" (no work).
- OR the loop picks a fake worst-item to fix and breaks a healthy
  surface.
- OR the loop's commits trigger build breaks that the oracle
  whitewashes, and the roundtrip validator approves regressions.

All three are catastrophic for unattended-loop semantics. The
oracle is the contract. A silent-success oracle is worse than no
oracle.

## Compounding finding: output-shape mismatch

The `last.json` shape emitted by the script is:

```
{ failures[], warnings[], totalFailures, totalWarnings, exitCode, timestamp }
```

But `.claude/skills/auto-optimize/SKILL.md` Step 3.2 specifies the
oracle MUST produce:

```
{ metric_value, worst_item, worst_item_diagnostic }
```

The script's `-Snapshot` mode emits trend-shaped JSON (`pass_pct`,
`by_category`, ...) but `-Json` mode emits the shape above. Neither
matches what the skill expects. The skill cannot run end-to-end
against the actual oracle without a shape adapter.

## Why the loop did not auto-fix this

Both `Scripts/run-prompt-corpus.ps1` and the SKILL.md files are in
`scope_boundary.protected` per `state/quality/chatbot-qa/baseline.json`.
This is correct: the loop must not rewrite its own oracle or its own
spec. Operator-only fix.

## Proposed operator-side fix (NOT applied by the loop)

Three independent changes, all operator-only:

### Fix 1: gate verdict on $proc.ExitCode

In `Scripts/run-prompt-corpus.ps1`, immediately after `$proc.ExitCode`
is captured, fail loudly if the test runner didn't produce a
recognized completion marker AND exit code != 0:

```powershell
$runnerCompleted = $out -match 'Prompts violating invariants \(\d+\):' `
                -or $out -match '✓ All prompts passed' `
                -or $out -match 'Test Run Successful'

if (-not $runnerCompleted -and $proc.ExitCode -ne 0) {
    Write-Host "✗ Oracle could not run — test runner exited $($proc.ExitCode) without parseable verdict." -ForegroundColor Red
    Write-Host "  Most common cause: build failure (e.g. running GaChatbot.Api locking DLLs)." -ForegroundColor DarkYellow
    # Emit a fail-marker JSON so callers don't see stale 'all passed' state.
    if ($Json) {
        @{
            timestamp     = (Get-Date -Format "o")
            oracle_status = "build_or_runner_failed"
            exitCode      = $proc.ExitCode
            failures      = $null   # explicit null, NOT empty array
            metric_value  = $null
        } | ConvertTo-Json | Set-Content $Json
    }
    exit $proc.ExitCode
}
```

The `-Snapshot` mode already has this guard (`Snapshot skipped:
corpus runner did not complete`). The `-Json` mode does not. Mirror
the snapshot guard into the `-Json` path.

### Fix 2: emit the metric-value shape the skill expects

Augment the `-Json` payload with the canonical fields:

```powershell
$metricValue = if ($totalPrompts -gt 0) {
    [math]::Round((($totalPrompts - $failures.Count) / $totalPrompts), 4)
} else { $null }

$summary = @{
    timestamp              = (Get-Date -Format "o")
    metric_value           = $metricValue
    worst_item             = if ($failures.Count -gt 0) { $failures[0] } else { $null }
    worst_item_diagnostic  = if ($failures.Count -gt 0) { $failures[0] } else { $null }
    totalFailures          = $failures.Count
    totalWarnings          = $warnings.Count
    failures               = $failures
    warnings               = $warnings
    exitCode               = $proc.ExitCode
}
```

Worst-item ordering: the existing `-Worst` parameter only truncates
display; failure-array ordering is determined by `PromptCorpusTests`
emission order (YAML order). True "worst" ranking would need
per-prompt scoring — out of scope for v1, but the loop can treat
"first failure in YAML order" as worst until ranking exists.

### Fix 3: SKILL.md acknowledges the shape adapter is operator-owned

Add a paragraph to `.claude/skills/auto-optimize/SKILL.md` Step 3.2:

> The oracle JSON contract is `{metric_value, worst_item,
> worst_item_diagnostic}`. If a domain's oracle emits a different
> shape, the operator must either upgrade the oracle to emit these
> fields directly, or commit a shape adapter alongside the baseline.
> Auto-optimize will refuse to read a `last.json` that lacks
> `metric_value`.

This makes the contract explicit and rejects ambiguous baseline
runs instead of silently parsing the wrong shape.

## Loop behavior after this incident

Cycle 1 was **aborted** before any code edit. Branch
`loop/chatbot-qa/2026-05-16` was left clean (no commits). Lock
released. `state/quality/chatbot-qa/loop-history.jsonl` records:

```json
{"exit_status":"aborted-oracle-unreliable","findings":["oracle-silent-success","output-shape-mismatch"]}
```

This is the correct behavior: the loop refused to run on
untrustworthy ground.

## Related

- `docs/solutions/tooling/2026-05-10-octo-plugin-install-corruption-silent-gate-failure.md` — same false-green pattern, different surface (multi-LLM review gate).
- `docs/runbooks/chatbot-improvement-loop.md` — operator runbook; should call out "verify oracle returns non-null metric_value before relying on baseline".
- `feedback_check_ci_before_next_chunk` — sibling rule: local green ≠ truly green. Same family.

## The takeaway

**Every oracle must be paranoid about its own runtime.** A green
report from an oracle that never ran is the worst possible failure
mode for an unattended loop. The Cherny pattern only works if the
oracle reliably distinguishes "I ran and saw zero failures" from "I
couldn't run". Treat oracle silent-success as a P0 reliability bug,
not a polish item.
