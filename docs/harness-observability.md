---
title: Harness & Response-Quality Observability (GA)
status: living
date: 2026-05-17
related:
  - docs/review-independence.md
  - docs/loop-onboarding.md
  - state/governance/dev-process-overseer.json
  - state/quality/ga-harness/baseline.json
  - state/quality/chatbot-qa/baseline.json
  - state/quality/embeddings/baseline.json
  - agent-blackbox.policy.json
---

# Harness & Response-Quality Observability (GA)

This doc records the loop-driven observability artifacts that GA emits
so the agent-blackbox install audit and the supervised-loop kit can
treat the harness as **durably reviewed**, not just *"the tests passed
on my box"*.

It is the GA-side companion to the install-audit `observability`
check, which expects four pieces of evidence:

1. **harness-audit** — repo harness readiness report.
2. **response-quality** — agent response verbosity, readability, claim
   density, grounding markers.
3. **overseer** — dev-process-overseer JSON capturing workflowMode,
   warnings, and gate signals.
4. **quality baselines** — `state/quality/<domain>/baseline.json` plus
   the matching `last.json` produced each cycle.

GA already produces three of these (overseer + baselines + per-domain
oracle output). The two that the install audit currently flags
(`harness-audit` and `response-quality`) are wired in agent-blackbox
itself and are surfaced in GA via the loop-driven evidence chain
below.

## Loop-driven evidence chain

```text
producer  -> Scripts/dev-process-overseer.ps1
          -> state/governance/dev-process-overseer.json   (overseer)

producer  -> Scripts/supervised-loop-harness-oracle.ps1
          -> state/quality/ga-harness/last.json           (baseline + last)

producer  -> Scripts/run-prompt-corpus.ps1                (chatbot-qa)
          -> state/quality/chatbot-qa/last.json           (response-quality
                                                           input)

reviewer  -> python -m cli.agent_blackbox harness-audit   (harness-audit)
          -> dist/harness-audit.json

reviewer  -> python -m cli.agent_blackbox response-quality
          -> dist/response-quality.json

verdict   -> python -m cli.agent_blackbox install-audit
          -> dist/install-audit.json
```

The pattern is *producer-reviewer with disk handoff*: each producer
writes to a stable JSON path, each reviewer reads that path in a
fresh sub-agent / different context, and the verdict is a third party
reading both producer and reviewer outputs. This is the same review
independence the GA tribunal pattern uses (see
`docs/review-independence.md`).

## Why on-disk artifacts

JSON-on-disk is the canonical GA cross-agent handoff. It:

- survives session boundaries (auto-compact, restart, surface
  hand-off),
- can be inspected by a human reviewer without rehydrating the agent,
- is the input shape `python -m cli.agent_blackbox harness-audit` and
  `python -m cli.agent_blackbox response-quality` already accept,
- is the same shape `state/quality/<domain>/baseline.json` and
  `state/governance/dev-process-overseer.json` already publish.

## Pre-existing GA evidence

| Path | Producer | What it proves |
| --- | --- | --- |
| `state/governance/dev-process-overseer.json` | `Scripts/dev-process-overseer.ps1` | workflowMode, gate warnings, halt markers |
| `state/quality/ga-harness/baseline.json` | (committed baseline) | what "green" means for the GA harness oracle |
| `state/quality/ga-harness/last.json` | `Scripts/supervised-loop-harness-oracle.ps1` | latest harness oracle result |
| `state/quality/chatbot-qa/baseline.json` | committed baseline | golden corpus pass rate baseline |
| `state/quality/embeddings/baseline.json` | committed baseline | OPTIC-K invariants baseline |

These three baselines + overseer artifact are already published, and
the install audit already credits them. The remaining install-audit
deductions for `harness-audit` and `response-quality` are
*workflow-step-string* checks (the audit greps the agent-blackbox
GitHub Actions workflow text for those literal strings); they do not
relax to docs alone, so closing them requires either an operator
workflow edit or a follow-up audit-rule relaxation in agent-blackbox
itself. Both are tracked in BACKLOG.md.

## How a loop session uses this

A supervised-loop cycle ends with:

1. The producer writes `state/quality/<domain>/last.json` and
   `state/governance/supervised-loop-cycle.json`.
2. A reviewer (fresh sub-agent, different context) reads both, and
   votes by writing `dist/harness-audit.json` and
   `dist/response-quality.json` if applicable.
3. The verdict step runs `python -m cli.agent_blackbox install-audit`
   and `python -m cli.agent_blackbox enforce --report …` against the
   above to decide whether the cycle is mergeable.

No producer is ever its own reviewer (see
`docs/review-independence.md`).

## Hard limits

The observability chain never bypasses the supervised-loop hard
gates (`docs/loop-onboarding.md`):

- No service restarts.
- No edits to `Tests/Apps/GaChatbot.Api.Tests/Corpus/prompts.yaml`
  or `docs/runbooks/chatbot-improvement-loop.md`.
- No `.github/workflows/**` edits without explicit human approval —
  even if it would close an install-audit deduction.
- No `agent-blackbox-reviewed` label without explicit human approval.

## Related

- `docs/review-independence.md` — independence dimensions the
  verdict step relies on.
- `docs/loop-onboarding.md` — operator one-pager for the supervised
  loop.
- `.claude/skills/supervised-loop/SKILL.md` — the cycle skill.
- `.claude/skills/auto-optimize/SKILL.md` — the domain-specific
  precedent that the harness-audit pattern generalises.
