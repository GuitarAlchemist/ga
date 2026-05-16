---
name: "Auto-Optimize"
description: "Generalized Cherny-style autonomous improvement loop. Reads a domain-scoped baseline.json + oracle script, runs the oracle, picks the worst-scoring item, proposes a fix, validates rollback safety, commits if metric improved AND roundtrip passes. Use for chatbot-qa, embeddings, voicing-analysis, or any other domain that has a baseline + oracle. Refuses to run if killswitch present, governance halt active, or scope_boundary violated."
allowed-tools: Read, Write, Edit, Glob, Grep, Bash, Agent, AskUserQuestion, Skill
last_verified: 2026-05-16
---

# /auto-optimize

The generalized version of the `chatbot-improvement-loop` runbook
(`docs/runbooks/chatbot-improvement-loop.md`). Where `/chatbot-iterate`
is hard-wired to the chatbot corpus, this skill is parameterized by
`domain` so the same Cherny pattern applies to any quality-metric loop
in the workspace (chatbot-qa, embeddings, voicing-analysis, optick-sae,
chatbot golden-trace signatures, etc.).

This is the **Level 3** automation in the chatbot-development ladder:
assistant runs cycle-by-cycle without per-cycle human approval, with
hard caps + killswitch + Harness rollback validator gating every
commit. Level 4 (auto-merge on multi-LLM green) is still gated behind
Phase 2's Ollama-vs-cloud signal measurement (see `docs/plans/
2026-05-14-arch-cherny-adoption-and-tribunal-ci-plan-v2.md` §Phase 2,
item 6).

## Inputs

Pass via skill args as `key=value` pairs:

| Input               | Required | Example                                                |
|---------------------|----------|--------------------------------------------------------|
| `domain`            | yes      | `chatbot-qa`, `embeddings`, `voicing-analysis`, `optick-sae` |
| `oracle_script_path`| yes      | `Scripts/run-prompt-corpus.ps1`                        |
| `baseline_path`     | yes      | `state/quality/chatbot-qa/baseline.json`               |
| `max_iterations`    | no (10)  | hard cap on cycles per invocation                      |
| `plateau_window`    | no (5)   | how many consecutive sub-threshold improvements before exit |
| `plateau_threshold` | no (0.005)| minimum relative metric delta to count as progress     |

The `domain` value drives every path computed by the skill — lock,
artifact dir, protected paths, etc. **Picking a `domain` value that
doesn't already have a `state/quality/<domain>/baseline.json` is an
error**; see Step 0.

## Iron Laws

```
1. NEVER commit without the roundtrip validator passing.
2. NEVER edit a protected file (declared in baseline._protected_paths
   or matching the global protected-paths-hook pattern).
3. NEVER exceed max_commits_per_session=50 or max_wall_clock_minutes=480.
4. ALWAYS release the .lock on exit (success, failure, or kill).
5. ALWAYS check state/.loop-halted and the per-domain .STOP marker
   BEFORE every iteration, not just at start.
6. AUTO-EXIT when the last `plateau_window` cycles all improved the
   metric by less than `plateau_threshold` (rel. to prior). Plateau
   exit is not failure — it's the loop's job to know when to stop.
```

Memory notes worth honoring (read at session start):
- `feedback_multi_llm_review_pays_off` — chatbot/MCP/DI/parser PRs
  require multi-LLM review before merge. Loop-opened PRs get the
  `auto-loop` label so the workflow auto-invokes `/octo:review`.
- `feedback_shared_infra_auth` — auto mode does NOT cover service
  restarts. The loop never `Restart-Service`s anything.
- `feedback_ui_click_through_before_done` — shape-only assertions are
  not enough for showcase/demo paths. Use the actual oracle the
  domain ships with.

## Process

### Step 0: Domain validity + killswitch + governance

Before any iteration, validate that the domain is real and not halted.

```powershell
# 1. Domain validity
$baseline = Get-Content $baseline_path -Raw | ConvertFrom-Json
if (-not $baseline.schema_version) {
    Write-Error "baseline at $baseline_path has no schema_version — refusing"
    exit 2
}

# 2. Local killswitch
if (Test-Path state/.loop-halted) {
    Write-Host "Loop halted globally — sentinel state/.loop-halted present" -ForegroundColor Red
    Get-Content state/.loop-halted
    exit 0
}
if (Test-Path "state/quality/$domain/.STOP") {
    Write-Host "Domain killswitch — state/quality/$domain/.STOP present" -ForegroundColor Red
    Get-Content "state/quality/$domain/.STOP"
    exit 0
}

# 3. Demerzel governance check (only if mcp__demerzel server is wired)
# Skip silently if MCP not available — local-only loops still run.
```

To resume:
```powershell
pwsh Scripts/loop-killswitch.ps1 -Reset       # global
Remove-Item "state/quality/$domain/.STOP"      # per-domain
```

### Step 1: Acquire domain lock

Exclusive lock prevents two concurrent loop runs in the same domain
from racing each other's commits.

```powershell
$lock = "state/quality/$domain/.lock"
if (Test-Path $lock) {
    $age = (Get-Date) - (Get-Item $lock).LastWriteTime
    if ($age.TotalMinutes -lt $max_wall_clock_minutes) {
        Write-Error "Domain $domain is locked by another loop (started $($age.TotalMinutes.ToString('0'))m ago); exiting"
        exit 1
    } else {
        Write-Warning "Stale lock for $domain (older than max wall-clock); reclaiming"
        Remove-Item $lock
    }
}
$env:PID | Set-Content $lock
try {
    # ... main loop body ...
} finally {
    Remove-Item $lock -ErrorAction SilentlyContinue
}
```

### Step 2: Read scope boundary

The baseline declares which paths the loop is allowed to edit and
which are protected. Honor both.

```json
{
  "schema_version": 1,
  "domain": "chatbot-qa",
  "metric": "pass_pct",
  "primary_baseline": 0.94,
  "_harness": {
    "rollback_metadata": {
      "roundtrip_validator": ".claude/skills/chatbot-qa-roundtrip-validate/SKILL.md",
      "reject_on_loss": true
    }
  },
  "scope_boundary": {
    "allow_edit": [
      "Common/GA.Business.ML/Agents/Skills/*.cs",
      "Common/GA.Business.ML/Agents/Mcp/*.cs",
      "Common/GA.Domain.Services/**/*.cs"
    ],
    "protected": [
      "Tests/Apps/GaChatbot.Api.Tests/Corpus/prompts.yaml",
      "docs/runbooks/chatbot-improvement-loop.md",
      "Scripts/run-prompt-corpus.ps1",
      ".github/workflows/*"
    ]
  }
}
```

If the baseline doesn't declare `scope_boundary`, **abort with a clear
error**. Loops without a declared scope are dangerous.

### Step 3: Iteration body

For each cycle (up to `max_iterations`):

1. **Check halt signals again** (Step 0 markers may have appeared mid-run).
2. **Run oracle**: `pwsh $oracle_script_path -Worst 1 -Json state/quality/$domain/last.json`.
   The oracle MUST produce a JSON-shaped `state/quality/$domain/last.json`
   with fields `{metric_value, worst_item, worst_item_diagnostic,
   oracle_status}`. **Fail-closed contract**: if `metric_value` is
   `null` OR `oracle_status` is anything other than `"ok"`, the loop
   MUST refuse to use the file as a baseline and exit
   `aborted-oracle-unreliable`. A green report from an oracle that
   never ran is the worst possible failure mode (see
   `docs/solutions/tooling/2026-05-16-auto-optimize-oracle-silent-success-build-failure.md`
   for the chatbot-qa incident that motivated this contract).
3. **Compare metric** against `baseline.primary_baseline`. If the new
   value is at-or-above baseline AND there are no failing items, the
   loop has run out of work — exit with status `converged`.
4. **Propose a fix** for `worst_item`. Read the diagnostic, find the
   responsible file under `scope_boundary.allow_edit`, apply a minimal
   change. **Refuse if any file outside `allow_edit` would change.**
5. **Run roundtrip validator** — `Skill` tool with the
   `_harness.rollback_metadata.roundtrip_validator` skill name. Pass
   `{before_metric: <baseline>, after_metric: <new>}`. If the
   validator rejects, revert the edit + record the rejection reason
   in the cycle log + continue to next iteration (don't commit).
6. **Commit** with format:
   ```
   loop(<domain>): <one-line summary of fix>

   Cycle <n>/<max>; metric <metric> moved from <before> to <after>;
   roundtrip-validate passed.

   Co-Authored-By: <repo>-auto-loop <noreply@guitar-alchemist.io>
   ```
7. **Re-run oracle** to confirm. If the metric *regressed* (the fix
   broke something else), `git revert HEAD --no-edit` + record the
   regression + continue.
8. **Plateau check** — track last `plateau_window` cycles' rel. delta.
   If all are below `plateau_threshold`, exit with status `plateau`.

### Step 4: Open PR (NOT merge)

After the loop exits (converged, plateau, or max-iterations), if any
commits landed, open a single PR with all loop commits squashed-by-
GitHub-on-merge. Label: `auto-loop`.

**The loop NEVER merges its own PRs.** Branch protection enforces the
`auto-loop` label triggers `/octo:review` and the protected-files
gate; merge is human-only until Phase 2's signal-measurement gate
(item 6 of v2 plan) clears.

```powershell
gh pr create `
    --title "loop($domain): <summary>" `
    --body "Auto-loop cycle output. Metric moved $before → $after. Roundtrip-validate green on every commit. Multi-LLM review required by branch protection." `
    --label auto-loop
```

### Step 5: Release lock + write digest

```powershell
Remove-Item $lock
# Append to state/quality/$domain/loop-history.jsonl
@{
    timestamp        = (Get-Date -AsUTC -Format o)
    domain           = $domain
    cycles_ran       = $n
    metric_before    = $before
    metric_after     = $after
    exit_status      = "converged|plateau|max-iter|killed"
    pr_url           = $prUrl
} | ConvertTo-Json -Compress | Add-Content "state/quality/$domain/loop-history.jsonl"
```

## Domains shipped today

| Domain                | Baseline                                          | Oracle                                          | Roundtrip validator skill                          |
|-----------------------|---------------------------------------------------|-------------------------------------------------|----------------------------------------------------|
| `chatbot-qa`          | `state/quality/chatbot-qa/baseline.json`          | `Scripts/run-prompt-corpus.ps1 -Worst 1 -Json…` | `.claude/skills/chatbot-qa-roundtrip-validate/`    |
| `embeddings`          | `state/quality/embeddings/baseline.json`          | `Scripts/run-embedding-diagnostics.ps1 -Json…`  | _(pending; see #182)_                              |
| `voicing-analysis`    | `state/quality/voicing-analysis/baseline.json`    | _(pending recorder)_                            | _(pending)_                                        |
| `chatbot-signatures`  | _(implicit — uses committed _signature.json)_     | `pwsh Scripts/compare-trace-to-canonical.ps1 -Sweep` | _(reuses chatbot-qa roundtrip)_                |
| `optick-sae` (ix)     | `state/quality/optick-sae/baseline.json`          | _(in ix sibling; cross-repo dispatch)_          | _(in ix sibling)_                                  |

## Anti-patterns

- **Skipping the roundtrip validator on "trivial" fixes.** Every commit
  goes through. The reason cycles fail is not that the proposed fix
  was wrong — it's that the loop didn't know it broke something else.
- **Hand-editing the lock file.** If a stale lock prevents resumption,
  use `pwsh Scripts/loop-killswitch.ps1 -Reset -Domain $domain`.
- **Running the loop on a feature branch.** It commits unprompted —
  put it on a dedicated `loop/<domain>/<date>` branch. The PR opens
  against main from there.
- **Treating plateau as failure.** Plateau means the metric stopped
  responding to fixes from the loop's current toolbox. That's signal,
  not regression — file a sub-task for human-driven next-layer work
  rather than retry.
- **Editing prompts.yaml or any baseline.json from inside the loop.**
  Per memory `feedback_loop_protected_paths`, those are operator-owned
  artifacts. The loop changes _implementations_, not _oracles_.

## Companion skills + scripts

- `/chatbot-iterate` — the chatbot-specific Level 2 sibling. Use this
  when you want one-item-at-a-time human-in-the-loop iteration on the
  chatbot specifically. `/auto-optimize` is the Level 3 generalization.
- `/digest` — write the current cursor + success criteria. The loop
  invokes /digest at exit so the next session inherits state cleanly.
- `/learnings` — record surprises during the loop (a regression that
  the roundtrip validator caught is a learning, not just noise).
- `Scripts/run-prompt-corpus.ps1` — the canonical oracle for the
  `chatbot-qa` domain.
- `Scripts/compare-trace-to-canonical.ps1 -Sweep` — fast oracle for
  the `chatbot-signatures` domain (no LLM required, ~seconds).
- `Scripts/loop-killswitch.ps1` — operator interface to the halt
  sentinels.

## Related

- `docs/runbooks/chatbot-improvement-loop.md` — the original chatbot-
  specific runbook this skill generalizes. Stays as the canonical
  reference for the chatbot domain.
- `docs/plans/2026-05-14-arch-cherny-adoption-and-tribunal-ci-plan-v2.md`
  — the v2 plan that specified this skill. §Phase 2 item 4 is the
  authoritative input/behavior spec.
- `state/quality/*/baseline.json` — every domain's contract.
- `[[reference-cherny-learnings-ritual]]` — the larger pattern this
  loop participates in.
