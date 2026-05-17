---
title: Supervised Loop Onboarding (GA)
status: living
date: 2026-05-17
related:
  - .claude/skills/supervised-loop/SKILL.md
  - Scripts/supervised-loop-preflight.ps1
  - Scripts/dev-process-overseer.ps1
  - ga.loop-policy.json
  - agent-blackbox.policy.json
---

# Supervised Loop Onboarding (GA)

This is the one-page operator guide for the supervised autonomous loop kit
on the Guitar Alchemist repo. The kit is intentionally small: a preflight,
a skill, a producer, and an evidence file. It was propagated from
agent-blackbox as the **adjacent-repo rollout** (Phase 5.5 of the
agent-blackbox autonomous-development-operating-model).

## The 5-step rollout principle

The kit is shipped under the user-approved rollout principle from
agent-blackbox:

> 1. **Install loop governance on 1-2 repos first.** Start with
>    agent-blackbox and one adjacent repo where the blast radius is low.
>    Each repo should have: `state/quality/<domain>/baseline.json`,
>    `Scripts/dev-process-overseer.ps1` (or equivalent),
>    `state/governance/dev-process-overseer.json`, explicit `allow_edit` /
>    `protected_paths`, `.STOP` and global halt support,
>    `pwsh Scripts/<verify>.ps1` or equivalent oracle.
> 2. **Use `/goal` before `/loop`.** For the first runs, use bounded
>    goals like *"Run the repo overseer, verify loop eligibility, make
>    one scoped improvement inside allow_edit, run the oracle, emit
>    evidence, and stop."* Avoid long unattended `/loop` until the repo
>    has multiple clean runs.
> 3. **Do not install generic "autonomous dev" loops everywhere.** Each
>    repo needs its own domain boundaries.
> 4. **Require Agent Blackbox evidence before merge-driving.** A loop
>    can open branches/PRs, but merging requires: local oracle passes,
>    overseer loop-eligible, risk report pass/warn or explicit human
>    override, PR preflight pass, no service restarts or service-manager
>    changes unless explicitly approved.
> 5. **Keep service restarts human-only.** Loops can edit code/config
>    inside scope, but cannot restart services (GaApi, GaChatbot.Api,
>    cloudflared, Ollama, Redis), mutate service managers, rotate
>    secrets, deploy production, or change billing/legal/customer
>    commitments.

## Why GA is a good second target

GA already had the precursors — `state/quality/<domain>/baseline.json`
schema (chatbot-qa, embeddings, voicing-analysis, dsl-eval, optick-sae,
readme-drift, invariants, memory-curator, gate-ledger), the
`Scripts/dev-process-overseer.ps1` producer, the
`state/governance/dev-process-overseer.json` evidence file, and the
`.claude/skills/auto-optimize/` domain-specific loop. The supervised-loop
skill **generalises** the auto-optimize precedent: it lets the same
preflight + evidence shape govern slices that aren't already wired into a
domain-specific oracle.

The existing `chatbot-qa` `/auto-optimize` flow is the canonical
domain-specific precedent on GA. Supervised-loop is the lightweight
sibling for slices that don't have a domain oracle yet.

## `/goal` before `/loop`

For the first several runs on this repo, prefer Claude Code `/goal` over
`/loop`. A `/goal` invocation has a verifiable end state and one stop
condition. Multi-hour `/loop` is for after the repo has produced a
string of clean cycle evidence files.

Canonical first-run goal:

```text
/goal Run one supervised-loop cycle on GA: run Scripts/dev-process-overseer.ps1, then Scripts/supervised-loop-preflight.ps1; if LOOP_READY=true, invoke .claude/skills/supervised-loop; emit state/governance/supervised-loop-cycle.json and stop. Prove this with the cycle evidence file, the verify command exit code, and a one-paragraph summary. Stop after 4 turns if blocked.
```

## When to use the supervised-loop skill

- The user asks for one bounded autonomous improvement.
- A scheduled `/loop` is running and you need to advance one slice
  before yielding.
- You are dogfooding the supervised pattern that other repos will copy.

The skill itself enforces its own hard refusals — it will abort if the
preflight is not green, if the overseer is stale, or if a hard gate
(below) is triggered.

## Hard gates (always require a human)

The supervised loop never bypasses any of these:

- Restarting services (`Restart-Service`, `taskkill` against GaApi /
  GaChatbot.Api / cloudflared / Ollama / Redis / any service-manager).
- Schema or contract freezes (`docs/contracts/**`, ix schemas, Demerzel
  governance schemas, IXQL grammar bumps).
- OPTIC-K embedding dimension changes (240-dim one-way door across the
  GA / ix / Demerzel fleet — see `Common/GA.Business.ML/Embeddings/`).
- Lowest-layer fanout (`Common/GA.Business.Core/**`).
- Cross-repo schema bumps (GA contracts, ix schemas, Demerzel governance
  schemas).
- Production deploys or secret rotation.
- Editing the chatbot improvement runbook
  (`docs/runbooks/chatbot-improvement-loop.md`) or the golden prompt
  corpus (`Tests/Apps/GaChatbot.Api.Tests/Corpus/prompts.yaml`).
- Applying any review-bypass label.
- Any file matching `protected_paths` in
  [ga.loop-policy.json](../ga.loop-policy.json).

When any of these is needed, the loop emits a cycle-evidence file with
`exit_reason` set to the appropriate code and stops.

## Files this kit adds

| Path | Role |
| ---- | ---- |
| `.claude/skills/supervised-loop/SKILL.md` | Claude Code skill (one bounded cycle). |
| `Scripts/supervised-loop-preflight.ps1` | Deterministic readiness gate. |
| `Scripts/dev-process-overseer.ps1` | (Pre-existing) Producer of `state/governance/dev-process-overseer.json`. |
| `ga.loop-policy.json` | Edit scope (`allow_edit`, `protected_paths`). |
| `state/quality/ga-harness/baseline.json` | (Pre-existing) Loop baseline. |
| `docs/loop-onboarding.md` | This document. |

`agent-blackbox.policy.json` (risk-scoring) stays untouched; the loop
scope is a sibling artifact so the risk-policy stays pristine.

## Daily-use recipe

```powershell
# 1. Run the harness oracle (emits state/quality/ga-harness/last.json)
pwsh Scripts/supervised-loop-harness-oracle.ps1

# 2. Refresh the overseer evidence (defaults to scanning all domains;
#    scope to one to avoid pre-existing missing-oracle warnings)
pwsh Scripts/dev-process-overseer.ps1 -Domain ga-harness

# 3. Run the preflight (exit 0 == LOOP_READY=true)
pwsh Scripts/supervised-loop-preflight.ps1

# 4. If ready, dispatch the skill from a Claude Code session
#    "run one supervised loop cycle on this repo"

# 5. Inspect the cycle evidence
Get-Content state/governance/supervised-loop-cycle.json
```

### Why `-Domain ga-harness`?

GA already has several `state/quality/<domain>/baseline.json` files
(chatbot-qa, embeddings, voicing-analysis, dsl-eval, readme-drift,
optick-sae, invariants, memory-curator, gate-ledger) — but not all of
them have a current `last.json`. The unscoped overseer enumerates every
domain and warns on the missing oracle outputs, which downgrades
`workflowMode` from `loop-eligible` to `supervised-goal`.

For the supervised-loop kit's own readiness gate, scoping to
`ga-harness` is the canonical pattern. When you run a real domain loop
(e.g. chatbot-qa via `/auto-optimize`), pass `-Domain chatbot-qa`
instead — that domain's existing `last.json` is up-to-date and the
overseer reports `loop-eligible` for it.

## Pre-existing GA loops eligible for supervision

Each of these already has a `state/quality/<domain>/baseline.json` and
emits a `last.json` shape compatible with the overseer:

- **chatbot-qa** — `/auto-optimize` + `Scripts/run-prompt-corpus.ps1`
  (the existing precedent; supervised-loop is a generalisation)
- **embeddings** — `Scripts/embeddings-roundtrip-validate` skill
- **voicing-analysis** — voicing-search RAG telemetry
- **dsl-eval** — F# DSL eval soak
- **readme-drift** — `Scripts/readme-drift-survey.ps1`
- **optick-sae** — Phase 1 scheduled (cross-repo with ix + Demerzel)
- **invariants** — invariant test #25 family
- **memory-curator** — ChatTranscriptStore offline summarisation
- **gate-ledger** — chatbot tribunal gate history

## Related docs

- `CLAUDE.md` — top-level GA conventions and 5-layer architecture.
- `docs/methodology/ai-surfaces.md` — Antigravity / Claude Code /
  Augment split.
- `Scripts/dev-process-overseer.ps1` — producer.
- `.claude/skills/auto-optimize/` — domain-specific precedent.
