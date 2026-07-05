---
title: "The \"frugal\" deep-research workflow blew ~10x its envelope on a dense multi-part question"
date: 2026-07-04
problem_type: "tooling"
component: ".claude/workflows/deep-research-frugal.js"
symptoms:
  - "A single run consumed ~4.74M subagent tokens across 79 agents in ~27 min, vs an announced envelope of ~200-500k — roughly 10x over"
  - "Model mix was 15 haiku + 63 sonnet + 1 synthesis — the sonnet count (verify) dominated, inverting the intended haiku-heavy tiering"
  - "Extract agents made 20-55 tool-calls each (fetch+search storms); Verify escalated 19 times, and each escalation adds 2 more sonnet agents"
tags:
  - "deep-research"
  - "cost-control"
  - "workflow"
  - "model-tiering"
---

## Root cause

The workflow's hard caps (`MAX_ANGLES`, `MAX_SOURCES`, `MAX_CLAIMS`) bound cost
**per claim**, not **per question**. A dense, multi-part question (this one had 4
distinct sub-questions in one prompt) multiplies the fan-out: the scope agent maxes
angles, every stage fans out per angle/source/claim, and Verify escalates on every
contested or shaky claim — on a question where "everything is load-bearing," that
escalation path fires constantly, and each escalation spawns 2 more sonnet agents.
So the loop stays sober *per claim* while the *aggregate* runs away.

## Fix (v0.4, tracked in ga#517)

Three guards, all overridable via `args`:
1. **Hard spend ceiling** — `budget.spent()` checked before Extract, each Verify
   item, and each escalation; degrade (log + `stats.dropped`) past `MAX_SPEND_TOKENS`
   (default 600k). Synthesize still runs (a truncated-but-honest report beats none).
2. **Escalation cap** — beyond `MAX_ESCALATIONS` (default 8), accept the single-vote
   verdict as-is (contested → `unverified`, never auto-confirm).
3. **Dense-question guard** — if the question is long AND the scope returns ≥3
   distinct sub-questions, `throw` and instruct splitting into separate narrower runs
   (escape hatch: `args.allowDense: true`).

## How to apply

**Do not relaunch the frugal workflow on a dense multi-part question without the
v0.4 guards** (session-learned rule, CLAUDE.md 2026-07-02: announce cost + prefer
the soberest config). Split a 4-part question into 4 narrow runs, or set an explicit
`maxSpendTokens`. The "frugal" label describes per-claim discipline, not a per-run
guarantee — the guarantee is what v0.4 adds.
