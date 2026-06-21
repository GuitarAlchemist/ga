---
module: Scripts/run-prompt-corpus.ps1
tags: [auto-optimize, oracle, powershell, chatbot-qa, variable-collision, fail-closed]
problem_type: tooling-defect
related:
  - docs/solutions/tooling/2026-05-16-auto-optimize-oracle-silent-success-build-failure.md
---

# chatbot-qa oracle crashed on every real failure — `$worst` / `$Worst` case collision

## Symptom

Running the `/auto-optimize` loop on `chatbot-qa`, the oracle
`Scripts/run-prompt-corpus.ps1` crashed with:

```
Cannot convert value "[scales-keys] 'Show me the notes in C major' → canonical
signature mismatch: ..." to type "System.Int32".
The input string '...' was not in a correct format.
```

It crashed **before writing `last.json`**, so the loop's structured input was
never refreshed → the fail-closed gate read it as `couldnt_run` → `halt-misfire`.
Net effect: **the oracle failed precisely when a prompt failed an invariant** —
the one moment it must emit a metric. All-pass runs and the build-failure
fail-loud branch never tripped it, so it hid until a real corpus failure appeared.

## Root cause

PowerShell variables are **case-insensitive**. The script declares an `[int]`
parameter on line 24:

```powershell
param(
    [int]$Worst = 0,
    ...
)
```

and later assigns the worst *failure line* (a string) to `$worst` on line 227:

```powershell
$worst = if ($failures.Count -gt 0) { $failures[0] } else { $null }
```

`$worst` and `$Worst` are the **same variable**. Because `$Worst` is
type-constrained to `[int]`, assigning the failure string coerces it to Int32
and throws. The `else { $null }` branch is why it only fires on failure: `$null`
casts to int cleanly, so a 0-failure run (or the build-fail branch, which never
reaches line 227) silently succeeded.

This is the **same bug class** the script's own line-105 comment records fixing
once already (`rel-006`: renamed `$matches` → `$failureMatches` to avoid the
`$Matches` auto-variable collision). The lesson didn't generalise to other
type-constrained names.

## Fix

Rename the local to a name that can't collide with the `[int]$Worst` param
(`rel-007`):

```powershell
$worstItem = if ($failures.Count -gt 0) { $failures[0] } else { $null }
...
worst_item            = $worstItem
worst_item_diagnostic = $worstItem
```

Verified: with the fix, a run carrying 2 real failures wrote
`oracle_status: ok`, `metric_value: 0.9615` (50/52 active prompts) instead of
crashing — a usable metric on the exact input that crashed before.

## Why it matters / how to apply

- **Oracle reliability is the loop's foundation.** This is the *second*
  chatbot-qa oracle defect after the 2026-05-16 silent-success incident. Both
  share the failure mode "the oracle can't be trusted at the boundary it exists
  to measure." When an `/auto-optimize` loop reports `halt-misfire`, suspect the
  oracle *script* before the domain.
- **Type-constrained PowerShell params are landmines for case collisions.** Any
  `[int]$Foo` / `[string]$Foo` parameter makes a later `$foo = <other-type>`
  throw at assignment, not at use. Grep new oracle scripts for
  `\$[a-z]\w+` locals that case-match a typed param.
- **The fail-closed gate did its job.** The loop never faked progress — it
  recorded `couldnt_run`, self-halted, and escalated. The bug cost zero bad
  commits; it only cost the loop's ability to *make* progress. That's the
  paranoia design (`feedback_auto_optimize_oracle_paranoia`) paying off.
