---
title: "Adversarial review — Sol's proof of O2a (multiset-deck completeness for tetrachords)"
date: 2026-07-15
type: cross-model adversarial review (Claude reviewing GPT-5.6 Sol)
verdict: SOUND — two minor presentational gaps, no FALSE clause; O2a closed, tetrachord set-deck reconstruction COMPLETE
relates: 2026-07-13-deletion-deck-reconstruction-theorem.md, 2026-07-15-n5-scout-and-o2a-bench.md
code: code/2026-07-13-deletion-deck/sol_o2a_review_check.py
---

# Review of Sol's O2a proof

Reviewed statement: for all N ≥ 4, the multiset deck M(S) of a tetrachord
determines SC(S). Sol's architecture avoids per-card fused-element
identification entirely: aggregate multiset arithmetic on U = 2P ⊎ F
(extraction lemma), then a second-moment argument on opposite-pair matchings
(ordering lemma). Machine transcription of every claim, swept over **all**
4-necklaces for 4 ≤ N ≤ 80 — beyond Sol's cited control range (≤ 60) — in
`sol_o2a_review_check.py`: all assertions pass over 208 070 decks.

## Verdict table

| Clause | Content | Verdict |
|---|---|---|
| §1 dictionary | classes ⟺ necklaces; trichord class ⟺ gap multiset; cards Γᵢ | **SOUND** |
| §2 aggregation | U = 2P ⊎ F; F = two complementary pairs; **P contains no pair summing to N** (≤ 1 copy of N/2) | **SOUND** — the positivity argument is one line and airtight; this is the load-bearing structural fact |
| §3 extraction, framing | any valid candidate F′ satisfies Odd(F′) = Odd(U) | **GAP (minor)** — used implicitly; follows in one line from U = 2P′ ⊎ F′ |
| §3 case 1 (|Odd| = 4) | F = Odd(U), once each | **SOUND** |
| §3 case 2 (|Odd| = 2) | Odd(U) = {x, N−x}; F = {x, N−x, N/2, N/2} forced | **SOUND** — complementation-stability of F (from (3)) forces the Odd-set to be a complementary pair; the remainder argument ({v,v} stable ⟹ v = N/2) is exact |
| §3 case 3 (Odd = ∅) | F ∈ {{x,x,N−x,N−x}, {N/2⁴}}; alternatives killed by "P has no complementary pair" | **SOUND** — the flagged priority case survives: a rival F′ needs both y and N−y (or two copies of N/2) sourced from 2P, impossible. One implicit step: distinct complementary pairs are **value-disjoint** ({y,N−y} ≠ {x,N−x} ⟹ y ∉ {x,N−x}), needed for "le vrai F ne contient aucune de ces valeurs" — true (y = x or y = N−x would equate the pairs), deserves one line |
| §4 matchings ⟺ cycles | 4-necklace up to dihedral ⟺ perfect matching of labeled occurrences into opposite pairs; edges = cross pairs | **SOUND** (3 matchings ⟺ 3 unoriented labeled cycles) |
| §4 second moment | Q(F_Π) = 2Σp² + 2q(N−q); same F ⟹ q′ ∈ {q, N−q}; block swap | **SOUND** — including the q = N/2 edge |
| §4 distinct matchings | "two blocks from distinct matchings share exactly one label" | **GAP (minor)** — asserted; two-line proof needed: disjoint blocks ⟹ A′ = complement of A ⟹ Π′ = Π, contradiction |
| §4 conclusion | shared label + equal sums ⟹ p₂ = p₃ ⟹ same necklace | **SOUND** |
| §5–6 chain | M(S) ⟹ SC(S); with O3: D(S) ⟹ SC(S) for all tetrachords, all N | **SOUND** |
| §7 dependencies | uses neither Kelly parity, nor ICV, nor any tetrachord classification | **ACCURATE** — fully independent of the O3 machinery |

No FALSE clause. Machine checks E0–E5 (aggregation identity; no complementary
pair in P; the |Odd(U)| ∈ {0,2,4} trichotomy with exact case shapes;
**full candidate enumeration** — every two-complementary-pairs sub-multiset
F′ ⊆ U with even remainder equals the true F; matching-level ordering;
per-modulus deck injectivity) all pass, 4 ≤ N ≤ 80.

## Requested repairs (both cosmetic)

1. §3, before the case analysis: "any candidate F′ with U = 2P′ ⊎ F′
   satisfies Odd(F′) = Odd(U), since Odd(2P′) = ∅"; and in case 3: "two
   distinct complementary pairs share no value (y = x or y = N−x would make
   the pairs equal)".
2. §4: justify |A ∩ A′| = 1: if A ∩ A′ = ∅ then A′ = {1..4}∖A = B, whence
   Π′ = {A′, B′} = {B, A} = Π, contradicting Π ≠ Π′.

## Relation to prior artifacts

- Sol's **extraction lemma** proves exactly the uniqueness this side's O2a
  bench (2026-07-15) had isolated empirically as roadmap O2a′ — but lifted
  from per-card assignments to the aggregate U, which is why no case
  analysis over decks is needed. The bench finding (zero spurious
  c1+c2-assignments, N ≤ 30) is explained and superseded.
- Sol's **ordering lemma** is an independent second proof of Lemma L7
  (second moment vs. this side's pairwise-sum multiset subtraction). Both
  are retained; L7 now also has dual proofs.
- The review bench extends empirical multiset-deck injectivity at n = 4
  from N ≤ 30 to N ≤ 80.

## Consequence

**Tetrachord set-deck reconstruction is completely closed, for all N:**
D(S) ⟹ M(S) (O3, dual proofs T4 + Sol) and M(S) ⟹ SC(S) (this theorem).
The program's second full floor above the trichord classification. Remaining
open strata: n ≥ 5 (O1, O2, O4; conjectures C5.1, C5.2).
