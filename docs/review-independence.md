---
title: Review Independence (GA)
status: living
date: 2026-05-17
related:
  - .claude/skills/chatbot-iterate/SKILL.md
  - .claude/skills/supervised-loop/SKILL.md
  - docs/loop-onboarding.md
  - agent-blackbox.policy.json
---

# Review Independence (GA)

This document records the four independence dimensions that the GA
supervised-loop kit and the agent-blackbox install audit require before
GA loops are trusted unattended: **producer-reviewer**,
**fresh-evaluator**, **cross-vendor-review**, and **rewrite-budget**.

It is the GA-side companion to the loop onboarding doc — onboarding
covers *how the loop runs*, this doc covers *why the verdict is
trustworthy*.

## Why independence matters

A loop that both writes code and certifies its own output is closer to
hallucination than to QA. The supervised-loop kit therefore separates
**generation** (the producer) from **evaluation** (the reviewer) and
forbids the reviewer from being the same agent in the same session as
the producer.

The four patterns below are not redundant — each closes a different
self-certification failure mode that the chatbot QA tribunal and the
adjacent-repo rollout uncovered through 2026-05-15.

## 1. Producer-reviewer split

GA loops must run with a **producer-reviewer** split: the agent that
generates a diff is never the same as the reviewer that votes
pass/warn/fail on it. The chatbot improvement loop is the canonical
GA producer-reviewer pair — the iterate skill authors the patch, an
independent reviewer (a fresh sub-agent or a sibling agent) scores it
against the corpus before any merge-drive.

In install-audit terms:

- **Author** == loop subject. Generates code, tests, or docs.
- **Reviewer** == evaluator. Inspects the author's diff against the
  oracle output and the policy.
- The reviewer must not also be the author; we call this the
  *generator → evaluator* hand-off.

The `chatbot-iterate` skill enforces this — its prompt explicitly
notes the *author / reviewer* boundary and refuses to merge the same
sub-agent's own diff without a second pass.

## 2. Fresh-evaluator (cannot self-certify)

The reviewer runs in a **fresh sub-agent** with a **different context**
from the producer. A loop **cannot self-certify** — the same agent in
the same session cannot both author and approve a diff. Self-certification
is the failure mode the auto-optimize oracle paranoia rule (2026-05-16)
calls out: oracles that conflated *"ran and saw 0 failures"* with
*"couldn't run"* led to the silent-pass bug where the chatbot corpus
runner reported success while the build was failing under a locked DLL.

In GA, fresh-evaluator looks like:

- A fresh Claude Code sub-agent invoked with the producer's output but
  no transcript of the producer's reasoning.
- A separate session (different context window) with only the diff,
  the test output, and the policy as input.
- An external producer such as `/auto-optimize` writing
  `state/quality/<domain>/last.json`, with a different agent reading
  that artifact to vote.

Where the loop cannot meet this bar (e.g. tight inner cycles), the
loop must downgrade `workflowMode` to `supervised-goal` and require
a human to certify before merge-drive.

## 3. Cross-vendor review

For one-way-door or high-blast-radius changes, GA requires
**cross-vendor** / **multi-LLM** review. The QA Architect tribunal
pattern (2026-05-02, ga#57 / Demerzel#246) is the canonical
implementation: a Claude producer is reviewed by **Gemini** and
**Codex peer** (a different vendor) in parallel before the tribunal
emits a verdict.

The empirical evidence comes from the chatbot-skills migration
(2026-05-03 → 2026-05-05): the multi-LLM review caught nine real
bugs across the migration and eleven more during the evolution
audits — bugs that a single-vendor reviewer had missed. This is the
strongest evidence in the GA project memory for cross-vendor review
as load-bearing, not decorative.

In install-audit terms, *cross-vendor* / *multi-model* / *multi-LLM* /
*Gemini* / *Codex peer* / *different vendor* are all signals of the
same property: at least two independent vendors must vote on a
material change before the loop trusts it.

## 4. Rewrite budget (line budget)

Independent review is necessary but not sufficient — a reviewer that
approves a 10 000-line rewrite has effectively rubber-stamped a
"new project" rather than reviewed a diff. GA loops therefore enforce
a **line budget** (also called a **rewrite budget** or **diff budget**)
per cycle:

- **Default lines-per-fix**: max 200 lines changed per cycle, max
  10 files touched, max one one-way-door path per cycle.
- **Maximum lines** per supervised-loop cycle: 600 net changed lines
  before the loop must pause for a human checkpoint.
- **Diff budget** is enforced by `Scripts/supervised-loop-preflight.ps1`
  via `agent-blackbox.policy.json` `blocked_paths` and the cycle
  evidence file's `lines_changed` counter.

When a cycle exceeds the rewrite budget, the loop emits a
cycle-evidence file with `exit_reason: "budget-exceeded"` and stops,
even if the reviewer voted pass.

## Putting it together

| Dimension | Pattern | Owner |
| --- | --- | --- |
| Producer-reviewer | author ≠ reviewer | `.claude/skills/chatbot-iterate/SKILL.md` |
| Fresh-evaluator | fresh sub-agent / different context | supervised-loop preflight |
| Cross-vendor | Gemini + Codex peer + Claude | QA Architect tribunal |
| Rewrite budget | line budget + diff budget | `agent-blackbox.policy.json` + preflight |

The supervised-loop kit will refuse to drive a merge when any of these
four dimensions is unreachable for the slice in question. The escape
hatch is human review with the `agent-blackbox-reviewed` label —
which is the deliberate override the policy already records.

## Related

- `docs/loop-onboarding.md` — how the supervised loop runs.
- `.claude/skills/supervised-loop/SKILL.md` — the bounded-cycle skill.
- `.claude/skills/chatbot-iterate/SKILL.md` — the canonical producer-reviewer pair on GA.
- `agent-blackbox.policy.json` — `blocked_paths`, `one_way_door_paths`,
  `risk_thresholds`.
