---
name: "GA Chatbot AFK Harness"
description: "End-to-end autonomous (AFK) harness that develops AND QAs the GA chatbot. Orchestrates the existing Level-3 /auto-optimize dev loop + the semantic /ga-chatbot-qa-panel judge workflow, instruments every step to a dashboard the human can watch, and enforces branch-only / never-merge safety. Use to run unattended chatbot-quality improvement with full human visibility. Refuses to run if the backend is down, a killswitch/halt marker is present, or scope_boundary is violated."
allowed-tools: Read, Write, Edit, Glob, Grep, Bash, Agent, AskUserQuestion, Skill
last_verified: 2026-05-29
---

# /ga-chatbot-afk-harness

The **orchestration layer** for fully-AFK GA chatbot development + QA. It does
NOT reimplement the improvement loop — it wires together pieces that already
exist and adds the three things they lacked: a semantic QA signal, live
instrumentation for a human dashboard, and one safe "arm and walk away" entry.

```
            ┌──────────────────────── this harness ────────────────────────┐
preflight → │  QA snapshot           dev cycles            QA snapshot      │ → exit
            │  (det oracle +         (/auto-optimize,      (re-measure)     │
            │   semantic panel)       Iron Laws + rollback)                 │
            └───────────────── status → dashboard at every step ───────────┘
```

## What it composes (do NOT duplicate these)

| Piece | Role | Owned by |
|---|---|---|
| `Scripts/run-prompt-corpus.ps1` | deterministic oracle (`pass_pct`) | existing |
| `/ga-chatbot-qa-panel` (workflow) | semantic judge panel → `state/quality/chatbot-qa-semantic/` | this session |
| `/auto-optimize` | Level-3 dev loop: pick worst → fix → roundtrip → commit/revert | existing |
| `/chatbot-qa-roundtrip-validate` | per-commit rollback gate | existing |
| `state/quality/chatbot-qa/baseline.json` | metric + scope_boundary + caps | existing |
| `Scripts/afk-harness-status.ps1` | instrumentation writer → dashboard | this session |
| `Tools/afk-dashboard/index.html` | human UI | this session |

## Inputs (skill args, `key=value`)

| Input | Default | Meaning |
|---|---|---|
| `target_metric` | `0.97` | exit when deterministic `pass_pct` ≥ this |
| `max_iterations` | `10` | hard cap on dev cycles (also bounded by baseline caps) |
| `batch_size` | `1` | `/auto-optimize` cycles per status-emit batch (smaller = livelier dashboard) |
| `branch` | `afk/chatbot-qa-<date>` | the harness commits here; it NEVER pushes/merges |
| `semantic` | `true` | run `/ga-chatbot-qa-panel` for the richer signal each snapshot |

## Iron Laws (inherited + added)

Inherits ALL of `/auto-optimize`'s Iron Laws (no commit without roundtrip pass;
no editing protected paths; caps `max_commits_per_session=50`,
`max_wall_clock_minutes=480`; always release `.lock`; honor
`state/.loop-halted`, `state/quality/chatbot-qa/.STOP`, and
`~/.demerzel/HALT-ALL`). The harness adds:

```
A. NEVER push, open/merge a PR, deploy, or apply a review-bypass label.
   The harness produces COMMITS ON A BRANCH only. A human arms it and a
   human (or a separate reviewed flow) lands it. This includes DELEGATED
   flows: /auto-optimize's Step 4 normally runs `gh pr create` after a batch
   — when the harness invokes it you MUST suppress that (see Step 2.0), so an
   unattended run never opens a PR behind your back.
B. NEVER fabricate a metric. If the backend is unreachable, emit a
   `blocked` status with the real reason and STOP. A degraded run is
   recorded honestly, never as pass_pct=100%.
C. EMIT status via Scripts/afk-harness-status.ps1 at every phase boundary
   and after every accepted/reverted cycle — code owns the status, so a
   crashed loop still leaves an honest last state on the dashboard.
```

## Procedure

### Step 0 — Preflight (refuse-to-run gates)
1. Pick `run_id = <UTCyyyyMMddTHHmmZ>-afk`. Emit `state=preflight phase=preflight`.
2. Halt/killswitch checks (reuse `/auto-optimize` Step 0 exactly): if
   `state/.loop-halted`, `state/quality/chatbot-qa/.STOP`, or a live
   `~/.demerzel/HALT-ALL` marker is present → emit `state=killed`, explain, STOP.
3. **Backend preflight**: `POST http://localhost:5252/api/chatbot/chat` with
   `{"message":"What is a major triad"}`. If not a real non-empty
   `naturalLanguageAnswer` → emit `state=blocked`,
   `-Blocker "backend_unavailable: GaChatbot.Api :5252"`, STOP. (Bring it up
   per `docs/runbooks/chatbot-deploy.md`, then re-arm.)
4. Create/checkout `branch`. Emit `state=running phase=qa-snapshot branch=<branch>`.

> **SCALE — read this first.** Both oracles report `pass_pct` on a **0..100**
> scale (e.g. `94`, `100`). `target_metric`, `afk-harness-status.ps1 -DetPct/-SemPct`,
> and the dashboard are all **0..1 fractions**. So you MUST divide every
> `pass_pct` by 100 before comparing it to `target_metric` or passing it to the
> status writer. Skipping this makes `94 >= 0.97` true and the harness exits
> "done" on a 94% run (and the dashboard shows 9400%). Always normalize.

### Step 1 — Baseline QA snapshot
1. Run the deterministic oracle: `pwsh Scripts/run-prompt-corpus.ps1 -Snapshot`.
   Read `pass_pct` (0..100) from the snapshot and compute `det = pass_pct / 100`
   → pass as `-DetPct det`.
2. If `semantic=true`: run the `/ga-chatbot-qa-panel` workflow; read its
   `state/quality/chatbot-qa-semantic/<date>.json` `pass_pct` (also 0..100),
   compute `sem = pass_pct / 100` → `-SemPct sem`.
3. Emit `phase=qa-snapshot` with both fractions + `target_metric`. If
   `det >= target_metric` already → skip to Step 3 (done).

**Step 2.0 — suppress delegated PR creation (Law A).** `/auto-optimize`'s Step 4
opens a PR with `gh pr create` once commits land. The harness must NOT let that
happen. When you invoke it, append an explicit instruction: *"Do NOT run Step 4 /
`gh pr create` / any push or PR — commit on the current branch only; the AFK
harness owns landing."* If a given `/auto-optimize` version cannot be told to skip
its PR step, do NOT delegate — drive the cycle inline instead (pick worst prompt →
fix → `/chatbot-qa-roundtrip-validate` → `git commit` on branch / revert).

### Step 2 — Improvement cycles (the AFK loop)
A stable id for THIS harness run so the AFK outer-loop rows (sub-step 4) cluster
together and stay distinct from the inner `/auto-optimize` per-cycle rows:
`$afk_loop_id = "chatbot-qa-afk-$((Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ'))"`.

Repeat until `det >= target_metric` (fractions, per the SCALE note),
`max_iterations` reached, plateau (baseline `plateau_threshold`), caps hit, or a
killswitch appears:
1. Run `/auto-optimize domain=chatbot-qa oracle_script_path=Scripts/run-prompt-corpus.ps1 baseline_path=state/quality/chatbot-qa/baseline.json max_iterations=<batch_size>` **with the no-PR instruction from Step 2.0**.
   This does the real work: picks the worst prompt, proposes a fix, runs
   `/chatbot-qa-roundtrip-validate`, and commits on the branch or reverts.
   *(The delegated `/auto-optimize` already appends inner per-cycle rows to
   `state/quality/loops/chatbot-qa.iterations.jsonl` via its Step 3.8 — those
   carry the deterministic `pass_pct` trajectory; do not duplicate them here.)*
2. After each batch, re-read `pass_pct` and **normalize `det = pass_pct / 100`**; emit one status update per outcome (always pass fractions to `-DetPct`):
   - accepted → `-Kind commit -Iteration <n> -Commits <c> -DetPct <x> -Event "accepted fix for <prompt-id> (<+Δpp>)"`
   - reverted → `-Kind revert -Iteration <n> -Event "reverted <prompt-id>: <reject reason>"`
3. Re-check killswitch/halt each iteration (Law C of auto-optimize). If set → emit `state=killed`, STOP.
4. **Append the AFK outer-loop row to the loop ledger** — the harness's
   value-add over the inner loop is the **semantic** signal, so record THAT
   (when `semantic=true`) so the ix-duck loop lens can see semantic vs
   deterministic divergence across batches. One row per batch:

   ```powershell
   # $sem_before/$sem_after are the semantic fractions from /ga-chatbot-qa-panel
   # this batch vs last; $worst is the worst-scoring prompt the panel flagged.
   # $verdict ∈ improved|regressed|plateau ; oracle couldn't run → couldnt_run.
   pwsh Scripts/loop-record.ps1 -LoopId $afk_loop_id -Domain chatbot-qa `
       -Iteration $n -MetricName semantic_pass_pct `
       -MetricBefore $sem_before -MetricAfter $sem_after `
       -Verdict $verdict -OracleStatus $oracle_status `
       -WorstItem $worst -CommitSha $sha
   ```
   Skip this row when `semantic=false` (the deterministic trajectory is already
   covered by the inner `/auto-optimize` rows — don't duplicate it).

### Step 3 — Final snapshot + exit
1. Re-run the deterministic oracle and (if enabled) `/ga-chatbot-qa-panel`.
2. Emit `state=done phase=exit` with final `DetPct`/`SemPct`, `commits`, and a
   `-Event` summarizing outcome vs target.
3. Drop a `state/handoffs/<ts>-claude-code.md` note: branch name, commits made,
   final metrics, and the explicit next human action (review + land the branch).
   **Do not land it yourself.**

## Arming modes (how the human runs it AFK)

- **Bounded, in-session:** `/goal deterministic pass_pct >= 0.97 for chatbot corpus` then `/ga-chatbot-afk-harness target_metric=0.97`. Claude keeps working across turns until the goal evaluator confirms.
- **Interval:** `/loop /ga-chatbot-afk-harness` (only if repo preflight reports `LOOP_READY=true`).
- **Headless / true AFK:** `claude -p "/ga-chatbot-afk-harness target_metric=0.97 max_iterations=20"` — runs unattended; tool prompts follow the configured allowlist.

## Watching it (UI)

Serve the repo root and open the dashboard:
```bash
python -m http.server 8099    # from repo root
# → http://localhost:8099/Tools/afk-dashboard/
```
It auto-refreshes every 5s from `state/quality/chatbot-qa/afk-runs/latest-status.json`
+ the run's `.jsonl` event log. Full details in
`docs/runbooks/ga-chatbot-afk-harness.md`.

## Stopping
Create `state/quality/chatbot-qa/.STOP` (this domain) or `state/.loop-halted`
(all loops). The harness checks both every iteration and exits gracefully,
leaving completed commits intact.
