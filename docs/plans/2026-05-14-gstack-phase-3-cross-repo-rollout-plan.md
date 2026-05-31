---
title: gstack Phase 3 cross-repo rollout plan
status: staged (not yet approved)
date: 2026-05-14
related:
  - docs/runbooks/gstack-methodology-borrows.md
  - docs/plans/2026-05-07-chatbot-roadmap.md
provenance: Authored 2026-05-14 after Phase 2 single-PR validation (PR #210 / bc3e4592). NOT a commitment — decision criteria live here so future-us can act with data, not vibes.
reversibility: two-way door (methodology copy-paste, no schema lock-in)
revisit_trigger: 3-5 Phase 2 PRs OR Demerzel QA-verdict contract bump (2026-05-19+)
---

# gstack Phase 3 — cross-repo rollout plan

## Current state (2026-05-14)

- **Phase 1 (GA)**: methodology runbook on disk (`docs/runbooks/gstack-methodology-borrows.md`). ✅ shipped.
- **Phase 2 (GA)**: validated on ONE PR (bc3e4592 — chord-from-notes). Findings: `/office-hours` produced useful scope discipline; `/qa` caught the SSE multi-line writer bug and the DiatonicChords routing bug that the 50-prompt YAML corpus missed; `/cso` correctly self-deferred (no new attack surface). Sample size: 1. Not yet enough to separate signal from confirmation bias.
- **Phase 1 (Demerzel, ix, tars)**: not yet started.

## What "Phase 3" actually means

Full `./setup` install of the gstack skill bundle into each repo's `.claude/skills/` directory + CLAUDE.md updates + pinned SHA in `.git/HEAD` of the local clone. That brings:

- All 23 gstack skills (vs. the 3 we've borrowed manually)
- Auto-update mechanism (we'd pin to SHA per memory `feedback_check_ci_before_next_chunk`-style discipline)
- Cross-repo skill consistency

It also brings:

- Cognitive surface bloat (23 skills × 4 repos = 92 skill cards to keep mental model of, on top of existing compound-engineering + octo)
- Convention collisions (`/review`, `/commit`, `/debug` exist in both gstack and compound-engineering)
- Maintenance burden if Garry's upstream evolves fast (he ships daily as of 2026-05-13)

## Decision criteria — do NOT execute until ALL pass

1. **Phase 2 data sufficiency**: ≥3 PRs where the methodology runbook caught something the existing stack (compound-engineering + octo + sub-agent reviewers) missed. Today's PR #210 was 1. Need 2+ more.
2. **Demerzel contract version**: Phase 1 of the QA Architect tribunal trigger `trig_01WdRGSqgxah5PD46wg8u4Qq` fires 2026-05-18. The QA-verdict contract is currently v0.1 draft. Phase 3 forces a version bump to integrate `/cso`-style named lenses. Wait until AFTER 2026-05-18 so we don't rush the contract.
3. **Convention collision plan**: a written-down decision per skill where gstack and compound-engineering overlap (`/review`, `/debug`, `/commit-push-pr`, `/plan`). "Pick one, retire the other" needs to be answered per skill, not deferred.
4. **ix and tars applicability assessment**: Rust ML algorithms and F# grammar work — what fraction of the 23 gstack skills actually applies? If <40%, don't install — borrow the applicable subset instead.

## Sequenced rollout (when criteria pass)

| Step | Repo | What | Reversibility |
|---|---|---|---|
| 1 | GA | Pin gstack SHA, install full bundle, retire `compound-engineering:ce-test-browser` if `/qa` proves superior | two-way (revert .claude/) |
| 2 | tars | Install subset (research/synthesis skills only — skip QA/web/security) | two-way |
| 3 | ix | Install subset (security/perf skills only — skip UI QA) | two-way |
| 4 | Demerzel | Install full + bump QA-verdict contract to v0.2 with named-lens slots | **one-way** (contract version) |

Step 4 is the only one-way door. Steps 1–3 can be reverted by `git rm -r .claude/skills/gstack/`.

## Estimated effort

- Step 1 (GA full install): ~2 hours including collision retirement decisions
- Step 2 (tars subset): ~1 hour
- Step 3 (ix subset): ~1 hour
- Step 4 (Demerzel + contract bump): ~4 hours (cross-repo coordination + schema work)

**Total: ~8 hours of focused work, can be done in one session.**

## Triggers to revisit this plan

- Phase 2 hits 5 PRs without methodology surfacing anything new → demote, archive runbook as "tried but didn't pay"
- Phase 2 hits 5 PRs with strong signal → execute Phase 3 in one session
- Garry ships a breaking change that invalidates the 3 borrowed skills → re-evaluate Phase 1 contents first

## What NOT to do

- Don't auto-update. Pin SHA.
- Don't install full bundle in ix/tars without subset filtering — most gstack skills assume a TypeScript/Python/web stack.
- Don't bump Demerzel contract version before 2026-05-18 (Phase 1 trigger fires then; rushing the schema before it stabilizes is reckless).
- Don't run gstack `./setup` script blindly — read it first. It mutates global Claude Code config.
