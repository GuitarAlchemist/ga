# Contract: Demerzel `ga-loop-driver` pipeline (Tier 3)

> **Status:** v0.1 draft — 2026-05-10. **Not frozen.** Tier 3 of the
> GA ↔ Demerzel integration ladder. Tier 1 (status emission) and
> Tier 2 (directives) shipped 2026-05-10 in GA. Tier 3 requires
> matching work on the Demerzel side.

## What this contract describes

A Demerzel pipeline (`Demerzel/pipelines/ga-loop-driver.ixql`, to be
written by the Demerzel team) that orchestrates GA's
`/chatbot-iterate` from Demerzel's scheduler instead of from a Claude
Code session. The result: the loop runs from Demerzel's clock, not
from a human invoking `/chatbot-iterate` interactively.

Mirrors the shape of `Demerzel/pipelines/qa-architect-cycle.ixql`
that already exists for the QA Architect Tribunal (Phase 1 fires
`trig_01WdRGSqgxah5PD46wg8u4Qq` on 2026-05-18 per memory
`project_qa_architect_tribunal`).

## Pre-requisites (already shipped on the GA side)

| Component | Status | File |
|---|---|---|
| Loop status emitter (Tier 1) | ✅ | `Scripts/project-sync.ps1` |
| Loop status schema | ✅ | `docs/schemas/ga-loop-status.schema.json` |
| Governance directives reader (Tier 2) | ✅ | `Scripts/check-governance-directives.ps1` |
| Governance directives schema | ✅ | `docs/schemas/governance-directives.schema.json` |
| `/chatbot-iterate` Step 0 reads directives | ✅ | `.claude/skills/chatbot-iterate/SKILL.md` |
| Gate liveness check | ✅ | `Scripts/octo-gate-liveness.ps1` |
| Auto-merge decision | ✅ | `Scripts/octo-auto-merge-decision.ps1` |
| Gate ledger writer | ✅ | `Scripts/gate-ledger-write.ps1` |
| Loop killswitch | ✅ | `Scripts/loop-killswitch.ps1` |

## Pre-requisites (Demerzel-side, NOT yet implemented)

The Demerzel team is responsible for:

1. **Polling `state/governance/ga-loop-status.json`** at a configurable
   cadence (recommend: 60–300 s). The repo is at `../ga/` from the
   Demerzel repo per the cross-repo convention in `CLAUDE.md`.
2. **Writing `state/governance/directives.json`** to control GA's loop
   in response to the status it observes (e.g. pause the track when
   the QA Tribunal verdict comes back negative).
3. **Triggering `/chatbot-iterate` execution** when a Track item is
   ready and Demerzel has approved spending the budget. Mechanism
   options below.

## Trigger mechanism options (Demerzel-side decision)

Three patterns, ordered by coupling:

### Option 1: GitHub Actions dispatch (loosest coupling)

Demerzel calls `gh workflow run chatbot-iterate.yml -f slug=<slug>`
against the GA repo. GA hosts a workflow that runs the loop in a
runner. Pros: zero GA-process modifications. Cons: runner costs;
runner must have Anthropic + OpenAI auth.

### Option 2: ACP RPC to a local GA daemon

A long-running GA-side daemon (extension of `GaApi`) exposes an
ACP endpoint at `port: 8201` accepting `{ "command":
"chatbot-iterate", "slug": "<slug>", "verdictBinding": "<id>" }`.
Demerzel calls it via ACP from `qa-architect-cycle.ixql`. The daemon
invokes the same Claude Code skill machinery as the interactive
session. Pros: matches the existing Demerzel-ACP pattern at
port 8200. Cons: requires a new GaApi endpoint.

### Option 3: File-watch + spawn

Demerzel writes a `state/governance/iterations/<verdictId>.request.json`
file. A GA-side watcher (extending the existing
`GovernanceWatcherService`) picks it up, spawns
`pwsh -c 'claude code ... /chatbot-iterate <slug>'` in headless mode,
and writes the result back to `<verdictId>.result.json`. Pros: zero
new endpoints; reuses existing watcher infra. Cons: headless Claude
Code needs explicit budget + auth flags.

**Recommendation:** Option 2 (ACP) once the Demerzel team's QA
Tribunal Phase 1 stabilises. Until then, Option 3 (file-watch) gives
us a working integration with no new ports.

## State machine the pipeline owns

```
   ┌─────────────────────────────────────────────────┐
   │  Demerzel poll loop (ga-loop-driver.ixql)        │
   │                                                  │
   │  every <interval>:                              │
   │    1. read GA's ga-loop-status.json             │
   │    2. apply governance rules:                   │
   │       - pause if rolling regression rate > 0    │
   │       - pause if cost-ledger > budget           │
   │       - pause if Track is empty                 │
   │    3. write directives.json if any pause fires  │
   │    4. if no pause + at least one ready P0/P1:   │
   │         pick highest-priority ready slug        │
   │         trigger /chatbot-iterate (Option 1/2/3) │
   │    5. when iteration completes:                 │
   │         match returned verdict to GA's          │
   │         state/quality/verdicts/                 │
   │         emit Demerzel tribunal verdict          │
   │         update its own state                    │
   └─────────────────────────────────────────────────┘
```

## Governance rules the pipeline should enforce

These are the rules Demerzel's IXQL evaluates against
`ga-loop-status.json`. Numbers are recommendations; tune as data
accumulates.

| Signal | Threshold | Directive issued |
|---|---|---|
| `ledger.decisionCounts."merged-with-revert"` over last 5 PRs | ≥ 2 | `track-pause`, reason: "regression rate exceeded" |
| Slug's last 3 ledger rows show ≥ 1 revert | | `item-pause` on that slug |
| Cost ledger 30-day total | > budget | `track-pause`, reason: "monthly budget exhausted" |
| `verdicts.pendingTribunal[].ageDays` | > 7 | `advisory` per PR, reason: "tribunal stuck — manual escalation" |
| Demerzel's own QA Architect verdict on a PR | = REJECT | `rollback-ordered` on that PR |
| `loop.halted` | = true | No action — operator already paused locally; just observe |

## Identity / authority boundaries

- **GA writes** `ga-loop-status.json` only. Read-only on `directives.json`.
- **Demerzel writes** `directives.json` only. Read-only on `ga-loop-status.json`.
- **Operator overrides Demerzel** via `pwsh Scripts/loop-killswitch.ps1`
  — sets the local sentinel which composes with directives. The operator's
  kill is ALWAYS sufficient.
- **Demerzel cannot override the operator** — if both halt signals are
  set, both must lift before iteration resumes. This is by design.

## Verdict-binding contract

When Demerzel-driven iteration produces a PR, the iteration result
includes the Demerzel verdict ID that authorised it. That verdict ID
goes into:

- The PR body (so reviewers see "authorised by Demerzel
  verdict `<id>`")
- The gate-ledger row (`tribunal.verdictId`)
- The Agent-tool review verdict file's `notes` field

Closes the audit loop: every Demerzel-driven merge can be traced back
to the verdict that authorised it. Operator-driven (interactive
`/chatbot-iterate`) merges have no `verdictId` and that's the audit
signal that distinguishes the two modes.

## Out of scope for this contract

- **Cross-repo coordination.** Demerzel itself orchestrates GA + ix +
  tars + Seldon. This contract is the GA <-> Demerzel slice only.
- **Multi-host loops.** If GA's loop runs on multiple machines, the
  loop-status emitter would need to namespace by host. Current shape
  assumes one canonical GA instance.
- **Real-time push.** Both directions are file-polling; latency floor
  is the poll interval. A SignalR push channel would cut latency but
  adds a connection lifetime concern. Defer until polling latency is
  a real bottleneck.

## Schedule

Tier 3 is gated on:

1. QA Architect Tribunal Phase 1 firing successfully (2026-05-18 per
   memory `project_qa_architect_tribunal`)
2. Demerzel team confirming this contract shape via their own review
   (their pipeline owns implementation; GA owns the contract)
3. At least one Track item shipped via `/chatbot-iterate` with a
   verdict in the ledger (PR #155 or #157 — the first two canaries)

Once those three line up, the Demerzel team writes
`Demerzel/pipelines/ga-loop-driver.ixql` against this contract, and
the loop transitions from operator-driven (L2-with-label) toward
governance-driven (L3+).

## Cross-references

- GA-side schemas: `docs/schemas/ga-loop-status.schema.json`,
  `docs/schemas/governance-directives.schema.json`
- GA-side scripts: `Scripts/project-sync.ps1`,
  `Scripts/check-governance-directives.ps1`
- Skill that consumes both: `.claude/skills/chatbot-iterate/SKILL.md`
- Memory: `project_qa_architect_tribunal` (Phase 1 trigger),
  `project_acp_agents_2026_03_29` (ACP pattern for Option 2),
  `feedback_multi_llm_review_pays_off` (gate ROI evidence)
- Demerzel-side reference shape:
  `Demerzel/pipelines/qa-architect-cycle.ixql` (existing)
- The 4-level ladder this Tier 3 sits at the top of:
  `docs/automation/chatbot-loop.md`
