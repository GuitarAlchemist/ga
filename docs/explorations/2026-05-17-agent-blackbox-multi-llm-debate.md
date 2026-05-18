---
date: 2026-05-17
topic: agent-blackbox multi-LLM debate gate (Phase 2 → 3)
agents_involved: 4
duration: 6
outcome: kept
---
# Exploration: agent-blackbox multi-LLM debate gate

The `/octo:embrace` Phase 2 Define gate ran a structured debate between
Claude, Codex (GPT), and Gemini against the Phase 1 Discover synthesis
for the agent-blackbox commercial launch. The debate produced four
substantive corrections that the consensus would otherwise have shipped
wrong. This is the strongest empirical evidence yet for adopting
multi-LLM debate as a default gate on positioning / pricing / install
artifacts.

## What I was trying to figure out

Whether the agent-blackbox repo was ready to move from internal tooling
to private-beta launch surface. Specific questions:

- Is the positioning ("AI code review for agent-generated PRs") tight
  enough that design partners can self-identify?
- Does the pricing model survive contact with a buyer?
- Are the right design partners on the shortlist, or did we anchor on
  the obvious-but-wrong segment?
- Will a first-time installer actually get a healthy run on attempt 1?

## What I tried

1. Phase 1 Discover: parallel research across 5 repos (agent-blackbox,
   ga, ix, tars, Demerzel) producing `state/embrace/discover-synthesis.md`.
2. Phase 2 Define: ran the debate gate — Claude proposed the
   consensus, Codex and Gemini each got the synthesis and were asked
   to find the strongest counter-arguments. Output:
   `state/embrace/define-consensus.md`.
3. Phase 3 Develop: 8 parallel agents on 5 repos, install-audit closure
   PRs (#23-27 on agent-blackbox; #267 on ga; #43 on ix; #29 on tars;
   #282 on Demerzel).
4. Phase 4 Deliver: fresh install-audit against each post-merge `main`.

## What worked

Four debate corrections all shipped as artifacts and survived merge:

1. **Positioning reframed** to "control plane for AI-agent code
   changes" — explicit "do not evaluate against CodeRabbit / Greptile /
   Qodo" instruction to design partners. (PR #26 on agent-blackbox.)
2. **Pricing hypotheses** — three testable models drafted; primary
   candidate is per-agent-PR or per-active-agent-seat against the
   $3-5k/mo flat list. (PR #24.) Codex argued flat list anchors low
   and per-seat traps growth; Gemini argued per-PR is legible to
   buyers procuring "AI line-items" budget. Both correct.
3. **Design-partner shortlist** — 2-axis grid (autonomy × incident
   severity); three concrete profiles named (Northwind Robotics,
   Helios Health, Forge Data). (PR #25.) Claude's first pass anchored
   on FAANG-adjacent. Gemini argued for high-autonomy + high-blast-radius
   teams where current code review is already partly automated.
4. **Install-doctor preflight** — `INSTALL_READY=true/false` semantics
   matching the SKILL.md preflight pattern; healthy first-run docs
   section. (PR #27.) Codex flagged install fragility as the single
   biggest threat to design-partner conversion — was right.

Fleet score moved from ~429/550 to 451/550 (+22 net) post-merge. ga
hit 100/100, ix hit 110/110.

## What didn't work

- **Squash-merge fidelity regression** on `tars` and `Demerzel`: both
  PRs claimed full closure but post-merge audits show 56/110 and 81/110.
  Files force-added past `.gitignore` were dropped by the squash. The
  multi-LLM debate gate caught all four shipping risks; it did NOT
  catch this mechanical merge issue. Lesson: post-merge audit is a
  separate gate from debate, not a replacement.
- **Observability rubric conflict**: `score_install_observability`
  only credits literal `harness-audit` / `response-quality` strings
  inside `.github/workflows/agent-blackbox.yml`, but workflow files
  are in `protected_paths`. Loop-driven docs PRs cannot close
  observability. Both ga#267 and Demerzel#282 closure agents
  independently flagged this. Rubric needs to accept
  `docs/harness-observability.md` content as evidence.
- **`install-audit` CLI** lives in `C:/tmp/ab-wip/cli/agent_blackbox.py`
  (755 lines uncommitted). All Phase-3 and Phase-4 verifications ran
  against this WIP. Until the CLI is PR'd to agent-blackbox main, the
  audit is not reproducible.

## What I'd carry forward

1. **Default the debate gate** for any plan touching positioning,
   pricing, install path, or design-partner targeting. The pattern
   pays for itself when even one of the 4 corrections would have been
   a costly ship-and-walk-back.
2. **Pair debate with post-merge audit**. Debate catches conceptual
   shipping risk; post-merge audit catches mechanical merge fidelity.
   They are orthogonal gates.
3. **Treat "8 parallel agents" as the new baseline** for fleet rollout
   of cross-repo policy changes — 7 of 8 succeeded; the one failure
   retried clean with worktree isolation. The autonomy infrastructure
   works.
4. **The multi-LLM review pattern is now empirically validated** on:
   chatbot skills migration (9 bugs caught in PR #210 era), chatbot
   evolution (11 more in post-migration audits), and now agent-blackbox
   launch (4 substantive corrections). Three independent surfaces, same
   verdict. This is no longer experimental.

## What NOT to re-attempt (and why)

- **Do not** rely on PR claims of score deltas without a fresh audit
  on post-merge `main`. tars claimed 65→110 and was actually 56/110
  post-merge. Two repos out of five regressed silently.
- **Do not** force-add files past `.gitignore` for cross-repo policy
  rollouts. Squash-merge drops them. Either fix the gitignore in the
  same PR, or land the policy + gitignore in separate commits that
  the squash preserves.
- **Do not** assume the agent-blackbox CLI on `main` matches the
  audit numbers — until install-audit is PR'd, every audit is running
  against a directory that could be deleted at any time.
- **Do not** skip the debate gate to "save tokens" on positioning /
  pricing / GTM artifacts. The cost of running it is bounded; the
  cost of shipping wrong positioning publicly is unbounded.

## Provenance

- Source: `C:/Users/spare/source/repos/agent-blackbox/state/embrace/deliver-final.md`
- Phase 1: `state/embrace/discover-synthesis.md` (agent-blackbox)
- Phase 2 debate: `state/embrace/define-consensus.md` (agent-blackbox)
- Phase 4 fresh audit: `C:/tmp/final-audit/install-audit.{json,md}`
- Debate transcripts: `C:/tmp/ab-debate/{codex,gemini}.txt`
- PRs landed: agent-blackbox #23, #24, #25, #26, #27; ga #267;
  ix #43; tars #29; Demerzel #282
