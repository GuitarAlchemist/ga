---
name: "Chatbot QA Roundtrip Validate"
description: "Per-commit roundtrip validator for the chatbot-qa /auto-optimize loop. Re-runs the prompt corpus after a proposed fix and rejects the commit if the metric regressed, the canonical-trace gate broke, or a protected path was modified. The Harness rollback equivalent for the chatbot domain per docs/plans/2026-05-14-arch-cherny-adoption-and-tribunal-ci-plan-v2.md §Phase 2."
allowed-tools: Read, Bash, Grep, Glob
last_verified: 2026-05-16
---

# /chatbot-qa-roundtrip-validate

The contract the chatbot `/auto-optimize` loop calls **before every commit**.
Returns `pass` / `reject` per the v2 plan's Harness-Engine rollback wiring.

This skill is intentionally read-only — it runs the oracle, checks the
diff, and returns a verdict. It never edits code, never touches the
oracle, never modifies the baseline. That's `/auto-optimize`'s job to
react to (revert if reject, commit if pass).

## Inputs

| Input            | Source                                                      |
|------------------|-------------------------------------------------------------|
| `before_metric`  | `pass_pct` from the previous oracle run (loop history tail) |
| `after_metric`   | `pass_pct` from the oracle run just performed               |
| `diff_paths`     | `git diff --name-only HEAD` — what the proposed edit changed |
| `baseline_path`  | `state/quality/chatbot-qa/baseline.json` (for gates)        |

In practice the loop passes the first three as values; the skill loads
the baseline itself.

## Verdict rubric

The skill returns `pass` if **all four** conditions hold; `reject`
otherwise. Reject diagnostics include the failing condition by name so
the loop can record a learning rather than just retry.

### 1. Metric did not regress

```
after_metric - before_metric >= -regression_threshold
```

where `regression_threshold` defaults to `0.02` per the baseline's
`_harness.rollback_metadata.regression_threshold` field. A drop equal
to or smaller than the threshold is tolerated as noise; anything
larger is a reject (`metric_regression`).

### 2. Canonical-trace gate did not break

```powershell
pwsh Scripts/compare-trace-to-canonical.ps1 -Sweep
# exit 0 = matched; exit 1 = at least one signature regression
```

If the sweep exits non-zero, reject with `trace_shape_regression`.
The full diagnostic from the sweep is captured into the loop history.

### 3. No protected path modified

```powershell
$baseline = Get-Content state/quality/chatbot-qa/baseline.json -Raw | ConvertFrom-Json
$protected = $baseline.scope_boundary.protected
$changed = git diff --name-only HEAD
foreach ($p in $changed) {
    foreach ($glob in $protected) {
        if ($p -like $glob) {
            return @{ verdict = 'reject'; reason = 'protected_path_modified'; path = $p; glob = $glob }
        }
    }
}
```

Override is operator-only: a commit subject containing
`[allow-protected: <path>]` exempts that one path. The loop never adds
this marker. (If it does, the protected-files-hook catches it at
CI level.)

### 4. Build + fast tests still pass

```powershell
dotnet build AllProjects.slnx -c Release --nologo
# exit 0 required
dotnet test Tests/Apps/GaChatbot.Api.Tests --no-build -c Release `
  --filter "FullyQualifiedName~CanonicalSignatureChecker|FullyQualifiedName~Corpus_" `
  --nologo
# exit 0 required
```

The full `[Explicit] EveryPrompt_SatisfiesItsInvariants` test is NOT
re-run here — that's what produced `after_metric` in step 0. The
fast-test subset confirms the build is intact and the
trace-shape contract didn't blow up.

A build break or fast-test failure is reject `build_or_test_break`.

## Output shape

```json
{
  "verdict": "pass" | "reject",
  "reason": "metric_regression" | "trace_shape_regression" | "protected_path_modified" | "build_or_test_break" | null,
  "before_metric": 0.94,
  "after_metric": 0.96,
  "delta": 0.02,
  "diagnostic": "<one-line human description>",
  "detail": {
    "path": "<if protected>",
    "regressions": ["<from sweep>"],
    "exit_code": <int>
  }
}
```

The loop records this verbatim into `state/quality/chatbot-qa/loop-history.jsonl`.

## Process

1. Read `state/quality/chatbot-qa/baseline.json` for thresholds + protected globs.
2. Compute delta = `after_metric - before_metric`.
3. If `delta < -regression_threshold` → return reject(`metric_regression`).
4. Run `pwsh Scripts/compare-trace-to-canonical.ps1 -Sweep`. If non-zero, return reject(`trace_shape_regression`).
5. Walk `git diff --name-only HEAD`, check each path against `scope_boundary.protected`. If any match, return reject(`protected_path_modified`).
6. Run `dotnet build AllProjects.slnx -c Release` (exit 0 required) then
   `dotnet test ... --filter "FullyQualifiedName~CanonicalSignatureChecker|FullyQualifiedName~Corpus_"` (exit 0 required).
   If either non-zero, return reject(`build_or_test_break`).
7. Return pass.

## When the loop should call this

- After every implementation edit, BEFORE `git commit`.
- The loop should `git stash` if reject, OR `git revert HEAD --no-edit` if a commit slipped through somehow.

## What this skill does NOT cover

- **Multi-LLM review** — that's a PR-time check (auto-invoked on `auto-loop` PRs), not a per-commit check. See `feedback_multi_llm_review_pays_off`.
- **Demerzel tribunal verdict** — that's PR-time, via `qa-verdict-dispatch.yml`.
- **Latency regression** — the corpus emits soft warnings on latency; the loop logs them but they don't gate commits in v1. A future enhancement is a per-prompt latency band as a reject reason.

## Related

- `.claude/skills/auto-optimize/SKILL.md` — the loop driver that calls this skill.
- `state/quality/chatbot-qa/baseline.json` — the contract this skill reads.
- `docs/plans/2026-05-14-arch-cherny-adoption-and-tribunal-ci-plan-v2.md` §Phase 2 — the spec.
- `Scripts/compare-trace-to-canonical.ps1` — the trace-shape gate.
- `docs/runbooks/chatbot-improvement-loop.md` — operator runbook (canonical for the chatbot domain).
