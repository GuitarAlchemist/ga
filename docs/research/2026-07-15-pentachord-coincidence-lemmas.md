---
title: "Pentachord coincidence lemmas — L5.A, L5.D, twisted pairing, and theorems C5.1 / C5.2"
date: 2026-07-15
type: research write-up (proofs; opens the n = 5 campaign)
status: L5.A, L5.D, P1, C5.1, C5.2 PROVED for all N; remaining for O1-at-n=5 = the 2+2+1 configuration analysis
relates: 2026-07-15-n5-scout-and-o2a-bench.md (conjectures now proved), 2026-07-13-deletion-deck-reconstruction-theorem.md
code: code/2026-07-13-deletion-deck/{n5_coincidence_families.py, n5_lemma_candidates.py, n5_structure_check.py}
---

# Pentachord coincidence lemmas

Setting: a pentachord class is a 5-necklace (g₁, …, g₅), gᵢ ≥ 1, Σgᵢ = N.
Its five cards are the adjacent fusions, **as 4-necklaces** — cyclic words up
to rotation/reflection, *not* multisets (the structural jump at n = 5):

    Δᵢ = (gᵢ + gᵢ₊₁, gᵢ₊₂, gᵢ₊₃, gᵢ₊₄)   (indices mod 5).

Method note: equality of two 4-necklaces means componentwise equality with
one of the 8 dihedral images of the second word. Each alignment yields four
linear equations; positivity of the gaps (and of the fused sum, strictly
larger than each of its two parts) eliminates most alignments. The lemmas
below were first *discovered* by relation-mining all coincidence instances
for 7 ≤ N ≤ 40 (`n5_coincidence_families.py`) — which caught a condition a
naive analogy would have missed (the g₅ = g₁+g₂ clause of L5.D branch 2) —
then proved by full alignment enumeration, then re-verified as exact
biconditionals over 669 420 (necklace, position, type) triples
(`n5_lemma_candidates.py`).

## Lemma L5.A (adjacent coincidence)

    Δᵢ = Δᵢ₊₁  ⟺  gᵢ = gᵢ₊₂  and  gᵢ₊₃ = gᵢ₊₄.

*Proof (i = 1).* Write s = g₁+g₂, t = g₂+g₃; Δ₁ = (s, g₃, g₄, g₅),
Δ₂ = (t, g₄, g₅, g₁). The eight alignments of Δ₂ against Δ₁:

| image of Δ₂ | equations | outcome |
|---|---|---|
| (t, g₄, g₅, g₁) | s=t, g₃=g₄, g₄=g₅, g₅=g₁ | g₁=g₃ (from s=t) and g₃=g₄=g₅=g₁ ⟹ conclusion |
| (g₄, g₅, g₁, t) | s=g₄, g₃=g₅, g₄=g₁, g₅=t | s = g₄ = g₁ ⟹ g₂ = 0, impossible |
| (g₅, g₁, t, g₄) | s=g₅, g₃=g₁, g₄=t, g₅=g₄ | g₁=g₃ and g₄=g₅ ⟹ conclusion (family (a,b,a,a+b,a+b)) |
| (g₁, t, g₄, g₅) | s=g₁ | g₂ = 0, impossible |
| (g₁, g₅, g₄, t) | s=g₁ | impossible |
| (g₅, g₄, t, g₁) | s=g₅, …, g₅=g₁ | s = g₁ ⟹ g₂ = 0, impossible |
| (g₄, t, g₁, g₅) | s=g₄, g₃=t | t = g₂+g₃ = g₃ ⟹ g₂ = 0, impossible |
| (t, g₁, g₅, g₄) | s=t, g₃=g₁, g₄=g₅ | conclusion (generic alignment) |

Every feasible alignment implies g₁ = g₃ ∧ g₄ = g₅. Conversely, for
(a, b, a, c, c): Δ₂ = (a+b, c, c, a), whose reversal (a, c, c, a+b) rotates
to (a+b, a, c, c) = Δ₁. ∎

## Lemma L5.D (distance-2 coincidence)

    Δᵢ = Δᵢ₊₂  ⟺  (gᵢ = gᵢ₊₃ and gᵢ₊₁ = gᵢ₊₂)                       [branch 1]
               or (gᵢ = gᵢ₊₂ and gᵢ₊₁ = gᵢ₊₃ and gᵢ₊₄ = gᵢ + gᵢ₊₁)  [branch 2]

*Proof (i = 1).* Δ₃ = (u, g₅, g₁, g₂), u = g₃+g₄. Alignments:

| image of Δ₃ | equations | outcome |
|---|---|---|
| (u, g₅, g₁, g₂) | s=u, g₃=g₅, g₄=g₁, g₅=g₂ | branch 1 (g₁=g₄; g₂=g₅=g₃) |
| (g₅, g₁, g₂, u) | s=g₅, g₃=g₁, g₄=g₂, g₅=u | **branch 2** (g₅ = s = g₁+g₂) |
| (g₁, g₂, u, g₅) | s=g₁ | impossible |
| (g₂, u, g₅, g₁) | s=g₂ | impossible |
| (g₂, g₁, g₅, u) | s=g₂ | impossible |
| (g₁, g₅, u, g₂) | s=g₁ | impossible |
| (g₅, u, g₂, g₁) | s=g₅, g₃=u | u = g₃+g₄ = g₃ ⟹ g₄ = 0, impossible |
| (u, g₂, g₁, g₅) | s=u, g₃=g₂, g₄=g₁ | branch 1 |

Converses: (a, b, b, a, e) matches the last alignment; (a, b, a, b, a+b)
matches the second. ∎

**Both branches imply g₁+g₂ = g₃+g₄, but — unlike T4.2 at n = 4 — no N/2
condition appears: the fifth gap absorbs the complementarity.** Branch 2 is
the clause the multiset intuition misses: {g₁,g₂} = {g₃,g₄} alone is *not*
sufficient — the alternating pairing needs g₅ = g₁+g₂ exactly.

## Proposition P1 (twisted pairing)

    Δᵢ = Δᵢ₊₁  ⟹  Δᵢ₊₂ = Δᵢ₊₄.

*Proof.* By L5.A the necklace is (a, b, a, c, c) (at i = 1). Then
Δ₃ = (a+c, c, a, b) and Δ₅ = (a+c, b, a, c); the reversal of Δ₅ is
(c, a, b, a+c), which rotates to Δ₃. ∎
(The T4.1 phenomenon "adjacent coincidences come in pairs" survives at
n = 5 in twisted form: an adjacent pair forces a *distance-2* pair.)

## Theorem C5.1 (three equal cards force five)

If three of the five cards of a pentachord coincide, all five coincide —
equivalently the multiplicity profiles 4+1, 3+2, 3+1+1 are impossible, and
profile 5 occurs exactly for the regular necklace (d,d,d,d,d), N = 5d.

*Proof.* The independence number of the 5-cycle is 2, so any three of the
five fusion positions contain an adjacent pair; WLOG Δ₁ = Δ₂, so the
necklace is (a, b, a, c, c) by L5.A. The third equal card is Δ₃, Δ₄ or Δ₅:

- Δ₅ = Δ₁ is adjacent at i = 5: L5.A gives g₅ = g₂ and g₃ = g₄, i.e.
  c = b and a = c.
- Δ₃ = Δ₁ is distance-2 at i = 1: branch 1 gives a = c, b = a; branch 2
  needs b = c and c = a + b, forcing a = 0 — impossible.
- Δ₄ = Δ₁ is distance-2 at i = 4: branch 1 gives c = b, c = a; branch 2
  needs c = a, c = b and a = 2c, forcing c = 0 — impossible.

In every feasible case a = b = c: the necklace is regular, N = 5a, and all
five cards equal (2a, a, a, a). ∎

## Theorem C5.2 (classification of profile 2+1+1+1)

The profile 2+1+1+1 occurs **iff** the necklace is, up to dihedral action,

    (a, b, a, b, a+b),  a ≠ b       (hence N = 3(a+b): 3 | N is necessary),

and the number of such classes in Z_N is ⌊(N/3 − 1)/2⌋ when 3 | N, else 0.

*Proof.* (⟸) Branch 2 of L5.D gives Δ₁ = Δ₃. Checking all ten coincidence
conditions (five L5.A, five L5.D both branches) on (a, b, a, b, a+b): each
fails unless a = b or a = 0 — e.g. L5.A at i = 1 needs g₄ = g₅, i.e.
b = a+b; branch 1 at i = 1 needs g₂ = g₃, i.e. b = a. So exactly one
coincident pair: profile 2+1+1+1.
(⟹) Exactly one coincident pair. It cannot be adjacent: P1 would force a
second pair (distinct from the first unless three cards coincide, which
C5.1 sends to the regular necklace, profile 5). It cannot be branch 1 of
L5.D: the necklace (a, b, b, a, e) satisfies L5.A at i = 4 (g₄ = g₁,
g₂ = g₃), again a second pair. So it is branch 2: (a, b, a, b, a+b), and
a ≠ b (a = b satisfies branch 1 too, hence a second pair). The count is the
number of unordered {a, b}, a ≠ b, a + b = N/3. ∎

Both theorems, plus the count formula and P1, machine-verified for
7 ≤ N ≤ 40 (`n5_structure_check.py`; C5.1/C5.2 were the scout's conjectures
— both are now theorems, and the scout's "2+1+1+1 only at 3 | N" and
"profile 5 only at 5 | N" observations are explained exactly).

## Theorem T-N5 (profile classification — added 2026-07-16)

At n = 5 the support size r = |D(S)| takes only the values {1, 3, 4, 5}
(**r = 2 is impossible**), and **r determines the multiplicity profile**,
with exact necklace families:

| r | profile | necklace (up to dihedral) |
|---|---|---|
| 5 | 1+1+1+1+1 | generic |
| 4 | 2+1+1+1 | (a, b, a, b, a+b), a ≠ b — so N = 3(a+b) |
| 3 | 2+2+1 | (a, b, a, c, c), (a, b, c) not all equal |
| 2 | — | impossible |
| 1 | 5 | (d, d, d, d, d) — so N = 5d |

*Proof.* By C5.1 no profile has a part 3 or 4 except the full 5, so the
possible profiles are exactly 1⁵ (r = 5), 2+1+1+1 (r = 4), 2+2+1 (r = 3)
and 5 (r = 1); r = 2 would require profile 4+1 or 3+2, both impossible.
The r = 4 family is Theorem C5.2; the r = 1 family is part of C5.1. It
remains to classify r = 3, i.e. two disjoint coincident pairs.

*Case A — some pair is adjacent.* By L5.A the necklace is (a, b, a, c, c)
(rotate so the pair is (Δ₁, Δ₂)); P1 supplies the second pair (Δ₃, Δ₅);
the fifth card Δ₄ is single unless a further coincidence occurs, and every
remaining coincidence condition on (a, b, a, c, c) — the three other
adjacent conditions and the five distance-2 conditions in both branches —
forces either a = b = c (regular, profile 5) or a zero gap (checked one by
one; e.g. Δ₂ = Δ₄ branch 2 needs g₁ = g₂ + g₃, i.e. a = b + a, so b = 0).

*Case B — no pair is adjacent.* A branch-1 distance-2 pair at position i
has conditions gᵢ = gᵢ₊₃ ∧ gᵢ₊₁ = gᵢ₊₂, which are **exactly** L5.A's
conditions at position i+3 — it forces the adjacent pair
Δᵢ₊₃ = Δᵢ₊₄, contradicting Case B. So every pair is branch-2; but a
branch-2 pair forces the necklace (a, b, a, b, a+b), which by C5.2 has
exactly **one** coincident pair — profile 2+1+1+1, not 2+2+1.
Contradiction; Case B is empty.

Conversely (a, b, a, c, c), not all equal, has exactly the two pairs
(Δ₁, Δ₂) and (Δ₃, Δ₅) by L5.A + P1 and the exclusion list above. ∎

Machine verification of all biconditionals: 7 ≤ N ≤ 40
(`n5_profile_theorem_check.py`).

**Consequence for O1-n=5.** The set-deck determines the multiplicity
*profile* outright (it is a function of the support size alone). What
remains of multiplicity recovery at n = 5 is only the *assignment*
question — which support member carries multiplicity 2 when r ∈ {3, 4} —
empirically pinned by parity + ICV on every class, 7 ≤ N ≤ 24 (scout), and
in principle recoverable instead by direct inversion of the classified
families; left open as a precisely-bounded gap.
