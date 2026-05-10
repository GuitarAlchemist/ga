---
name: "Chatbot Iterate"
description: "L2 chatbot-development loop. Picks the next chatbot-shaped item from BACKLOG.md, runs feature → plan → work → review → PR while enforcing the Demerzel tribunal gate on any change touching GA.Business.ML/Agents, MCP tooling, DSL parser, or DI composition. Use when iterating on chatbot capability, not for general feature work."
allowed-tools: Read, Write, Edit, Glob, Grep, Bash, Agent, AskUserQuestion, Skill
last_verified: 2026-05-10
---

# /chatbot-iterate

Glue skill that ties the existing pieces (`/feature`, `/work`,
`/octo:review`, the Demerzel QA tribunal, `/learnings`,
`/ce-compound`) into a single chatbot-scoped iteration loop. Emits a
clear gate plan up front so the human knows what review the change
will need before merging.

This is the **Level 2** automation in our chatbot-development ladder
(per the discussion in docs/automation/chatbot-loop.md): assistant
drives end-to-end on one item, human reviews and merges. Not Level 3
(auto-merge on multi-LLM green) or Level 4 (failure-fed dark factory).

## When to Run

- "Pick up the next chatbot thing" — moving down the chatbot backlog
  systematically.
- After shipping a chatbot feature, when the next item is queued and
  the assistant should keep the cadence.
- *Not* for general feature work — use `/feature` directly. This skill
  exists to enforce the chatbot-specific gates.

## Iron Law

```
ANY PR touching the following paths REQUIRES Demerzel tribunal verdict
+ multi-LLM review (octopus or codex) BEFORE merge:

  Common/GA.Business.ML/Agents/**
  Common/GA.Business.ML/**/Mcp/**
  Apps/ga-server/GaApi/Mcp/**
  Common/GA.Business.DSL/**
  **/IChatClientFactory*
  **/AddGuitarAlchemistAi*

The "feedback_multi_llm_review_pays_off" memory documents that
multi-LLM review caught 9 real bugs in PR #151 alone that local
tests missed. This gate is load-bearing, not bureaucratic.

NEVER mark a PR ready-for-merge on a chatbot path until both gates
have been satisfied.
```

## Process

### Step 0: Killswitch check

Before doing ANYTHING, check the loop killswitch:

```powershell
if (Test-Path state/.loop-halted) {
    Write-Host "Loop halted — sentinel present. Aborting." -ForegroundColor Red
    Get-Content state/.loop-halted
    exit 0
}
```

If `state/.loop-halted` exists, exit cleanly with the sentinel's
reason. Do NOT pick a new item. Do NOT touch the working tree.
This is the L4 kill switch — when set, every fresh iteration
refuses to start. To resume:

```powershell
pwsh Scripts/loop-killswitch.ps1 -Reset
```

Status check (read-only):

```powershell
pwsh Scripts/loop-killswitch.ps1 -Status
```

### Step 1: Identify the next chatbot item

Read `BACKLOG.md` → **Chatbot Track (curated YYYY-MM-DD)** section.
Items there are slug-tagged (e.g. `memory-session-scope`,
`router-quality`) and labelled with **priority** (P0/P1/P2) and
**status** (ready / blocked / scheduled / parked).

Selection rule:

1. Filter to **status: ready**.
2. Pick the highest-priority item (P0 before P1 before P2).
3. Within a priority tier, prefer the smallest scope.
4. If the user named a slug (e.g. `/chatbot-iterate router-quality`),
   honour that and skip the pick step — but still verify status is
   ready and warn if not.

Secondary sources only when the Chatbot Track is exhausted:

- Active `docs/plans/*-chatbot-*.md` — any plan in flight with a
  "Next" section.
- Open GitHub issues tagged `chatbot`:
  `gh issue list --label chatbot --state open`.
- Recent `docs/solutions/*chatbot*` learnings that surfaced a
  concrete fix worth promoting into the track.

If no candidate is unblocked, **stop**. Don't manufacture work and
don't pick from the Parked or Scheduled subsections — both exist
specifically because they aren't ready. Tell the user and ask whether
to promote a parked item or surface a stale one.

### Step 2: Classify the gate requirements

For the chosen item, walk the implementation areas and decide which
gates are required. Cross-reference the path hints in the BACKLOG
entry against `Scripts/check-chatbot-tribunal-gate.ps1` — that
script is the authoritative classifier. Output a short upfront
plan, e.g.:

```
Item: alternate-tuning-voicings  (P1, ready)
Goal: Add ga_chord_voicings(chord, tuning) MCP tool

Touches (per BACKLOG entry):
  - Common/GA.Business.ML/Mcp/Tools/             → tribunal: REQUIRED
  - Common/GA.Business.Core/Fretboard/Voicings/  → tribunal: review only
  - Apps/ga-server/GaApi/Program.cs (DI wiring)  → tribunal: review only

Gates before merge:
  - octo:review (correctness + security)
  - Demerzel QA Architect Tribunal (music-theory verdict)
  - dotnet test AllProjects.slnx green
  - ga-react-components build + lint green
  - Manual smoke against /api/chatbot endpoint

Pre-flight (verify BEFORE starting work):
  - git status clean
  - On a fresh branch off main (not stacked)
  - dotnet build AllProjects.slnx -c Debug currently green
  - Vite (5176) + GaApi (5232) up — see SessionStart hook output

Estimated PR size: medium (3-6 files, +200/-50 LOC)
```

This step is **explicit so the user can veto**. If they push back on
scope, restart with a smaller item.

### Step 3: Run the feature pipeline

Once gates are agreed, hand off to existing skills:

1. **Plan**: invoke `compound-engineering:ce-plan` if the item needs
   design thinking. For most well-shaped items, skip directly to
   step 2.
2. **Work**: invoke `compound-engineering:ce-work` with the scope
   limited to the touched paths. Or run `/feature` if the item
   requires the full BACKLOG → plan → implement flow.
3. **Verify**: per CLAUDE.md, `dotnet build AllProjects.slnx -c Debug`
   AND `dotnet test AllProjects.slnx` AND
   `npm run build && npm run lint` in
   `ReactComponents/ga-react-components`. Don't claim success until
   all three are green.
4. **Multi-LLM review**: spawn the two reviewer subagents in parallel
   via the Agent tool. This is the **proven-effective mechanism** —
   per `feedback_multi_llm_review_pays_off`, this path caught 9 real
   bugs across the 2026-05-03 chatbot migration that local tests
   missed. **Do not substitute `/octo:review`** without first running
   the liveness check below; the orchestrator has been silently dark
   on Windows since ≥2026-04-29 per
   `reference_octo_plugin_corruption_2026_05_10`.

   ```
   Agent({
     description: "Code review PR #N",
     subagent_type: "octo:droids:octo-code-reviewer",
     prompt: "Review the diff at branch <branch>. Concerns: <specific
              concerns from the gate plan in Step 2 — what could break,
              not generic 'review for issues'>"
   })
   Agent({
     description: "Security review PR #N",
     subagent_type: "octo:droids:octo-security-auditor",
     prompt: "Security audit <branch>. Focus: <trust boundaries the
              diff touches — auth, DI surface, public input>"
   })
   ```

   Send both in a single message so they run in parallel. Resolve
   every BLOCKING and high-confidence Medium finding before
   requesting tribunal. APPROVE-WITH-NITS is the typical verdict;
   defer Lows.

   **If using `/octo:review` instead (slash command, orchestrated path):**
   it requires a liveness check after the run, because empty findings
   are indistinguishable from a broken gate:

   ```
   pwsh Scripts/octo-gate-liveness.ps1
   ```

   Exit 2 (`state: dark`) = every specialist failed; the verdict is
   meaningless. Recover via:

   ```
   pwsh Scripts/octo-review-clean.ps1 -Pr <N>   # strips space-bearing
                                                # PATH entries (Windows bug)
   ```

   **After the reviewers return, persist the verdict** so Step 7
   (auto-merge) and the gate ledger can read it:

   ```powershell
   pwsh Scripts/chatbot-review-write.ps1 `
       -Pr <N> `
       -Branch <branch> `
       -HeadSha <sha> `
       -Mechanism agent-tool-subagents `
       -Verdict <pass | nits-only | blocking | abstain> `
       -Reviewers @(
           @{ name='octo:droids:octo-code-reviewer'; verdict='approve-with-nits'; findingsCount=3; blockingCount=0 },
           @{ name='octo:droids:octo-security-auditor'; verdict='approve'; findingsCount=0; blockingCount=0 }
       )
   ```

   Schema: `docs/schemas/chatbot-review-verdict.schema.json`. Writes
   to `state/chatbot-reviews/<pr>.json`. The auto-merge decision
   (Step 7) refuses if this file is missing or has verdict !=
   `pass | nits-only`.

   Record the mechanism in the PR body too (`Agent-tool subagents`
   vs `/octo:review clean` vs `/octo:review raw`) so future audits
   can correlate gate ROI with mechanism.

   Background: `docs/solutions/tooling/2026-05-10-octo-plugin-install-corruption-silent-gate-failure.md`.
5. **Tribunal verdict**: invoke
   `compound-engineering:ce-doc-review` against the change list, or
   for chatbot paths specifically use the Demerzel governance
   pipeline `Demerzel/pipelines/qa-architect-cycle.ixql` (the
   actual filename — verified 2026-05-10; emits verdicts to
   `state/quality/verdicts/<repo>/<ref>/<verdict_id>.json` per the
   `project_qa_architect_tribunal` memory). Phase 1 of the tribunal
   contract fires `trig_01WdRGSqgxah5PD46wg8u4Qq` 2026-05-18 — the
   schema is still v0.1 draft; do not assume frozen fields.

### Step 4: Open the PR

Use `compound-engineering:ce-commit-push-pr` for the PR with body
including:

- The backlog item link
- Summary of approach
- Test output snippets (build + tests + lint)
- Multi-LLM review summary (passed gates)
- Tribunal verdict ID
- A short demo paragraph if the change is observable (chatbot
  question + new response example)

### Step 5: Post-merge — capture learnings + write ledger row

After the PR merges:

1. Run `/learnings` to capture any non-obvious thing from the
   session.
2. Run `/ce-compound` if the change unlocks a new abstraction that
   should be promoted into the F# DSL or a new MCP tool.
3. Update `BACKLOG.md` to mark the item shipped and surface any
   follow-ups.
4. **Tally cost** (L3 promotion criterion — cost-per-auto-merge budget):

   ```powershell
   pwsh Scripts/octo-cost-tally.ps1 `
       -Pr <N> `
       -RunId <orchestrator-timestamp-or-iso> `
       -AgentToolEstimateUsd <approximate-agent-cost> `
       -BudgetUsd 5.00
   ```

   Writes to `state/quality/cost-ledger.jsonl`. Exits 2 if cumulative
   ledger cost exceeds the budget — caller should refuse further
   `/chatbot-iterate` runs until the budget resets.

   Summary view: `pwsh Scripts/octo-cost-tally.ps1 -Summary`.

5. **Append a gate-ledger row** so future cohort analysis can prove
   the gate is pulling its weight (L3 promotion criterion):

   ```powershell
   pwsh Scripts/gate-ledger-write.ps1 `
       -Pr <N> `
       -Branch <branch> `
       -MergedAt (Get-Date -Format o) `
       -Decision <merged-clean | merged-with-followup | merged-with-revert> `
       -Tests @{ ran=$true; passed=<n>; failed=<n>; allowlistedFailures=@() } `
       -AgentToolReview @{
           verdict='nits-only'
           mechanism='agent-tool-subagents'
           findingsCount=<n>
           blockingCount=0
           uniqueFindingsCount=<n>  # findings this gate caught that
                                    # no other gate did — drives ROI
       }
   ```

   Schema: `docs/schemas/gate-ledger.schema.json`. Writes to
   `state/quality/gate-ledger.jsonl`. After 10 chatbot PRs this
   ledger answers "is the multi-LLM gate worth its cost?" — if
   `uniqueFindingsCount` is consistently 0, the gate is duplicate
   coverage and we can downgrade it.

### Step 6: Loop

If the user said "keep going" or this is part of a `/loop`, return to
Step 1 with the next item. Otherwise stop.

### Step 7: Auto-merge (opt-in, gated, L3 mechanism)

Default OFF. Only fires when the PR was explicitly opted into auto-merge
AND every gate from Steps 3–5 has produced an affirmative verdict.

**Preconditions** (all must hold — script enforces them):

1. PR has the `auto-merge-eligible` label.
2. PR lacks `do-not-merge` / `wip` labels and is not a draft.
3. PR diff is path-restricted to chatbot-safe paths only (see
   `SafePathPatterns` in `Scripts/octo-auto-merge-decision.ps1`).
   Excludes public API surface, DI registration, GraphQL schema,
   `.env`, migrations. Anything outside that list requires human eyes.
4. All CI checks finished AND none failed (except allowlisted
   pre-existing env failures like the Anthropic key gap).
5. Agent-tool multi-LLM review verdict at
   `state/chatbot-reviews/<pr>.json` is `pass` or `nits-only`.
6. If `/octo:review` was used as a secondary check, its liveness was
   `live` or `mixed` — not `dark`.
7. Tribunal verdict, if present, is PASS / APPROVE / approve-with-nits.

**How to invoke:**

```
pwsh Scripts/octo-auto-merge-decision.ps1 -Pr <N> -Json
```

Exit codes:
- `0` + `decision: merge` — all gates pass; caller should run
  `gh pr merge <N> --squash --delete-branch`
- `1` + `decision: wait` — checks still pending; sleep + retry
- `1` + `decision: refuse` — at least one gate failed; surface the
  reason and STOP. Do not retry without human triage.

**Hard rules:**

- This script never merges. It only decides. The skill is responsible
  for acting on the decision.
- Refusal is the default. If any condition is ambiguous, refuse.
- Adding a path to `SafePathPatterns` is a one-way door for safety —
  the path-restriction list should grow slowly and only after a path
  has demonstrated it's safe to auto-merge (e.g., 5 successful
  human-merged PRs on the same path with no rollbacks).

**Why this is L3-mechanism but L2-default:**

The mechanism is here so the loop CAN auto-merge — but the
`auto-merge-eligible` label is the deliberate human-in-the-loop step
that L3 hasn't reached yet. L3 promotion ships when:

- 5+ chatbot PRs have merged via this mechanism cleanly (no rollbacks,
  no post-merge bug reports).
- A production canary auto-rolls back a bad merge.
- The label gate is replaced with a default-on policy.

See `docs/automation/chatbot-loop.md` for the full L2/L3/L4 ladder.

## Anti-Patterns

- **Skipping the gate analysis** because "this is a small change".
  The rule is path-based, not size-based. A one-line change in
  `IChatClientFactory.cs` requires the same gates as a 200-line
  change.
- **Running `/feature` against a chatbot item without invoking this
  skill first** — you lose the gate enforcement. Use this as the
  entrypoint.
- **Auto-merging on a green octopus review without the tribunal
  verdict** — the tribunal catches different things (music-theory
  correctness, cross-LLM consensus on contested decisions). Both
  gates exist for different reasons.
- **Counting `dotnet test` as the only quality gate**. PR #151
  evidence: tests passed locally; multi-LLM review caught 9 bugs;
  tribunal would have caught more. Three gates = three signals.

## Output

Each invocation should produce, in order:

1. The selected item (or a clean "no work to do" message).
2. The gate plan (paths + required gates).
3. The hand-off to the next skill (with the prompt that will be sent).
4. After the work completes: gate-by-gate pass/fail summary.
5. After the PR opens: PR URL + waiting-for state.

If the user invokes this skill in `auto` or `/loop` mode, run the
gates serially and only require human input if a gate fails or the
plan changes.
