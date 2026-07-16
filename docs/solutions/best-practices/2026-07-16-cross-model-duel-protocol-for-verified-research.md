---
module: docs/research (deletion-deck campaign) / discovery-engine methodology
tags: [research, duel, cross-model, adversarial-review, sol, gpt-5.6, exhaustive-verification, eval-gaming, discovery-engine]
problem_type: best_practice
decision: "Run mathematical research as an alternating cross-model duel: one model proves, the other performs hostile clause-by-clause review with machine transcription of every checkable claim on a wider range than the author used; repair; freeze."
rejected:
  - "Single-model research with self-review (no independent adversary; plausible-but-wrong survives)."
  - "Trusting claimed exhaustive verifications in a submitted proof (the reviewer must re-derive AND re-sweep independently)."
  - "Continuing a successful campaign by inertia once marginal returns drop (parked instead, with explicit re-engagement rule: new bounded target, different verifiable universe)."
reason: "The 2026-07-12→16 deletion-deck campaign produced five cross-reviewed theorems in four days. The two load-bearing ingredients: exhaustive verifiers on finite universes neutralize eval-gaming (a false claim dies mechanically), and role alternation keeps the reviewer genuinely hostile (reviewing on the proof's own merits, own proof used only for post-audit comparison)."
date_decided: 2026-07-16
---

# Cross-model duel protocol for verified mathematical research

## The protocol (reusable)

1. **Conjecture** comes out of an adversarial generation round (discovery-engine
   duel) and must survive an exhaustive verifier before anyone invests in proofs.
2. **Prove** on one side (Claude or Sol). The proof must be computation-free or
   flag its computational controls explicitly.
3. **Hostile review** on the other side: clause-by-clause verdict table
   (SOUND / GAP / FALSE), every checkable claim transcribed into machine
   assertions and swept on a range **wider than the author cited**, edge cases
   hunted explicitly (boundary values, repeated elements, degenerate multiset
   cancellations).
4. **Repair** — even cosmetic gaps get a written one-line fix, confirmed applied.
5. **Freeze** with a status board separating THEOREM / empirical-with-range /
   OPEN. No extrapolation ever.

## Evidence

Deletion-deck campaign: trichord mod-5 theorem, T4 + Sol-O3 (dual proofs of the
same statement — kept both: one strong-form, one minimal-hypothesis),
Sol-O2a (reviewed to N=80 vs author's N=60), pentachord lemmas + T-N5.
Full map: `docs/research/2026-07-16-deletion-deck-closing-synthesis.md`.

## How to reuse

The duel needs: a finite verifiable universe, a mechanical verifier (sweep),
and two models with alternating roles. Candidates already identified: Z12
voice-leading invariants, Spectral Track questions with mechanical evaluators.
Pause rule from the discovery-engine plan applies (3 runs without a
non-trivial survivor → park).
