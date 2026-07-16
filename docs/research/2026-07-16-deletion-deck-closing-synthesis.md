---
title: "Deletion-deck reconstruction — closing synthesis of the 2026-07-12→16 campaign"
date: 2026-07-16
type: research campaign closing synthesis (freeze document)
status: campaign CLOSED at this state; every claim below carries its exact status
provenance: >
  Conjecture originated by GPT-5.6 Sol in the 2026-07-12 discovery-engine duel
  (SOL-R2-4); developed by Claude (Fable) and Sol in alternating
  prove/adversarial-review roles, 2026-07-12 → 2026-07-16.
---

# Deletion-deck reconstruction: closing synthesis

**The question.** In Z_N under the dihedral (Tn/TnI) action, does the bare
*set* D(S) of one-note-deletion classes determine the class of S? And the
multiset version M(S)?

## Final status board

| Statement | Status |
|---|---|
| n = 3: set-deck reconstruction ⟺ 5 ∤ N; at 5 \| N exactly one collision {0,d,2d}/{0,d,3d}, d = N/5 | **THEOREM** (all N) |
| n = 4: D(S) ⟹ M(S) (multiplicity recovery) | **THEOREM** (all N; dual proofs: T4 strong form + Sol's Kelly-only form, both adversarially reviewed) |
| n = 4: M(S) ⟹ SC(S) (multiset completeness) | **THEOREM** (all N; Sol's extraction + second-moment proof, reviewed; machine-checked ≤ Z80) |
| **n = 4: D(S) ⟹ SC(S) — full set-deck reconstruction** | **THEOREM** (all N, by chaining the two above) |
| Infrastructure: L0 (deck invariance), L1 (Kelly parity), L2 (ICV identity), L3 (n = 3 vacuity), L4 (repeated-card dichotomy), L5 (necklace dictionary), L6 (part survival), L7 (P,F ⟹ 4-necklace, dual proofs), Theorem R (reduction) | **THEOREMS** |
| n = 5: coincidence lemmas L5.A / L5.D (with the g₅ = g₁+g₂ clause), twisted pairing P1 | **THEOREMS** (all N) |
| n = 5: C5.1 (three equal cards ⟹ five), C5.2 (2+1+1+1 ⟺ (a,b,a,b,a+b), N = 3(a+b)), T-N5 (support size ⟹ profile; r = 2 impossible; families exact) | **THEOREMS** (all N) |
| n = 5: multiplicity *assignment* (which support card is doubled, r ∈ {3,4}) | **OPEN**, precisely bounded; empirically pinned by parity + ICV, 7 ≤ N ≤ 24 |
| n = 5: multiset-deck completeness (O2-n=5) | **OPEN** — assigned to Sol 2026-07-16; empirically true ≤ Z30 |
| n ≥ 6: multiplicity recovery (O1) and multiset completeness (O2) | **OPEN**; empirically true 6 ≤ N ≤ 30 (~2.1·10⁹ subsets); the (P,F) invariant is provably insufficient from n = 6 (minimal pair in Z8) |
| ICV-integrality alone at n ≥ 5 (O4) | **OPEN**; at n = 5 fails exactly on the family d·(1,1,2,4,2), N = 10d (≤ 24), rescued by parity |
| General conjecture: set-deck reconstruction for all n ≥ 4, all N | **OPEN** — reduced by Theorem R to O1 + O2 |

**Sharp empirical boundary (all exhaustive, reproducible):** set-deck
reconstruction holds for every cardinality ≥ 4 on every 6 ≤ N ≤ 30; every
failure on that range is the proved mod-5 trichord pair; the multiset deck
is complete on every modulus tested (≤ Z30 all cardinalities; ≤ Z80 at
n = 4). **For Z12 — the musical universe — reconstruction is total:** every
set class of cardinality ≥ 3 is determined by the bare set of its
one-note-deletion classes (5 ∤ 12; exhaustive; the n = 3, 4 strata also
proved for all N).

## What made the campaign work (method notes)

- **Adversarial division of labour**: conjecture and proofs alternated
  between two models, with the non-author always running a hostile
  clause-by-clause review plus machine transcription of every checkable
  claim on a range wider than the author used. Three proofs were accepted
  only after repairs (all cosmetic); one reviewer bench bug (early-stop
  truncation that could have falsely certified uniqueness) was caught by a
  self-assertion before any number was reported.
- **Relation-mining before conjecturing**: the L5.D branch-2 clause
  g₅ = g₁+g₂ is invisible to multiset intuition and was caught only because
  the empirical families were mined *before* the lemma was stated.
- **Everything reproducible**: all sweeps and benches live in
  [code/2026-07-13-deletion-deck/](code/2026-07-13-deletion-deck/) with
  captured outputs; scripts assert their own sanity conditions.
- **No extrapolation**: every empirical claim in the campaign carries its
  exact range; the general conjecture is stated as open, not as "verified".

## Document map

1. [2026-07-13-deletion-deck-reconstruction-theorem.md](2026-07-13-deletion-deck-reconstruction-theorem.md) — main theorem write-up (trichord theorem, T4, Sol-O3, Sol-O2a, corollary closing n = 4)
2. [2026-07-13-deletion-deck-hostile-referee-report.md](2026-07-13-deletion-deck-hostile-referee-report.md) — the audit that reduced the program to O1/O2 (L0–L6, Theorem R, counterexamples)
3. [2026-07-15-sol-o3-proof-adversarial-review.md](2026-07-15-sol-o3-proof-adversarial-review.md) / [2026-07-15-sol-o2a-proof-adversarial-review.md](2026-07-15-sol-o2a-proof-adversarial-review.md) — cross-model reviews
4. [2026-07-15-n5-scout-and-o2a-bench.md](2026-07-15-n5-scout-and-o2a-bench.md) — the n = 5 scout + O2a bench
5. [2026-07-15-pentachord-coincidence-lemmas.md](2026-07-15-pentachord-coincidence-lemmas.md) — L5.A, L5.D, P1, C5.1, C5.2, T-N5
6. [2026-07-12-discovery-tracer-round1-claude-vs-sol.md](2026-07-12-discovery-tracer-round1-claude-vs-sol.md) — the duel that produced the conjecture

## Product handoff

Implementation ideas captured in `BACKLOG.md` → **« Deletion-deck →
produit »** (2026-07-16): `DeletionDeckAnalyzer` (layer 3) with the
precomputed Z12 deck table and reverse lookup, a chatbot "deck" skill, the
two invariant property-tests (ICV identity, Kelly parity), and
deck-similarity as an OPTIC-K-adjacent neighborhood. Tracer bullet defined
there; to be scheduled via `/feature` in queue order.

## If the campaign reopens

Priority order: (1) O2-n=5 (Sol's pending assignment — if it lands and
survives review, pentachords close like tetrachords modulo the assignment
gap); (2) the r ∈ {3,4} assignment gap at n = 5 (direct family inversion is
likely easier than a parity/ICV uniqueness proof); (3) n = 6, where the
(P,F) collapse means genuinely new invariants are required — the known
minimal (P,F)-collision pair (1,1,1,2,1,2)/(1,1,2,1,1,2) in Z8 is the first
test any candidate invariant must pass.
