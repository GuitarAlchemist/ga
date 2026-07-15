---
title: "Adversarial review — Sol's independent proof of O3 (tetrachord multiplicity recovery)"
date: 2026-07-15
type: cross-model adversarial review (Claude reviewing GPT-5.6 Sol)
verdict: SOUND — one minor presentational gap (§2), no FALSE clause, no counterexample found
relates: 2026-07-13-deletion-deck-reconstruction-theorem.md (Theorem T4), 2026-07-13-deletion-deck-hostile-referee-report.md
code: code/2026-07-13-deletion-deck/sol_o3_review_check.py
---

# Review of Sol's O3 proof

Reviewed statement: for all N ≥ 4, |S| = 4 ⟹ (D(S), N) determines M(S),
Kelly parity only. Review protocol: clause-by-clause audit on the proof's own
merits (Theorem T4 used only for the final comparison, not as authority);
machine transcription of every checkable claim, swept over **all** 4-part gap
necklaces for 4 ≤ N ≤ 40 (`sol_o3_review_check.py`, all assertions pass).

## Verdict table

| Clause | Content | Verdict |
|---|---|---|
| Lemme 1 | a_{C,R} well-defined on dihedral classes | **SOUND** — correct L0-instance; bijection x ↦ g(x), g(T∖{x}) = T′∖{g(x)} |
| §2 Kelly | Σ mᵢ a_{Cᵢ,R} ≡ 0 (mod 2); reduction to (K) over F₂ | **GAP (minor, repairable)** — the step converting per-card deletion counts into class coefficients a_{Cᵢ,R} silently uses Lemme 1 applied to the real cards S∖{x}; one explicit sentence fixes it. The mod-2 reduction (mᵢ mod 2)·p(Cᵢ) is exact. |
| §3 dictionary | trichord class ⟺ unoriented gap multiset | **SOUND** — rotations generate A₃, reversal adds a transposition, together S₃; converse immediate |
| Lemme 2 | Γ₁=Γ₂ ⟺ g₁=g₃, and then Γ₃=Γ₄ | **SOUND** — the common-occurrence cancellation is valid unconditionally (free commutative monoid), *including* with repeated values; the 2-element multiset matching is exhaustive |
| Lemme 3 | Γ₁=Γ₃ ⟺ fused sums = N/2 ∧ {g₁,g₂}={g₃,g₄} | **SOUND** — g₁+g₂ strictly exceeds g₁, g₂, so it must match g₃+g₄; N-even is forced, not assumed; no gap can equal N/2 in this configuration (it would force a zero gap), so the boundary case is vacuous |
| (7) alternating | (a,b,a,b) ⟹ all four cards equal | **SOUND** — direct substitution |
| Lemme 4 | 3+1 impossible | **SOUND** — any 3 of 4 cyclic positions contain an adjacent pair; tighter than T4.3's case list |
| Lemme 5 | r=3 ⟹ (a,b,b,a), a≠b, N=2(a+b); U doubled, V, W single | **SOUND** — distinctness of U, V, W is hypothesis (r=3), not needed as conclusion |
| §8 signatures | p(V)=p(W)=e_{λ(2a)} via λ(2a)=λ(2b); p(U) has exactly 3 nonzero coords; (K) kills (1,2,1) and (1,1,2) | **SOUND** — edge cases checked: 2a=N/2 or 2b=N/2 ⟺ a=b (excluded); λ(2a)=N/2 ⟺ a=b (excluded); λ(2b)=a possible (V={a,a,a}) and the formulas remain consistent (odd multiplicity 3); machine-verified to N=40 |
| §9 cases | r=4/3/2/1 → (1,1,1,1)/(2,1,1)/(2,2)/(4) | **SOUND** — honest note that r=2 is settled geometrically (3+1 excluded), not by parity; parity indeed can never separate (3,1) from (1,3) |
| §10 dependencies | only L0-instance + L1; not L2/L3/L4/L6 | **ACCURATE** — §8 uses parity signatures only, no ICV identity; no hidden exhaustive appeal anywhere |
| §11 scope | realizable version only; abstract uniqueness not claimed | **ACCURATE and honest** |

No FALSE clause. No counterexample exists on 4 ≤ N ≤ 40 for any checkable
claim (S1–S6 in the script: adjacent/opposite equivalences, no 3+1, the
palindromic classification, λ(2a)=λ(2b), signature shapes, Kelly selection
of the doubled class, r=2 ⟹ (2,2)).

## Comparison with Theorem T4 (only after the audit)

The two proofs are **independent in authorship, equivalent in skeleton,
genuinely different in the r=3 endgame, and different in scope**:

- Same coincidence infrastructure (Lemme 2 ≈ T4.1, Lemme 3 ≈ T4.2,
  Lemme 4 ≈ T4.3) — expected: it is the combinatorial core of the problem.
  Sol's proofs are cleaner at two points: multiset *cancellation* instead of
  element matching in Lemme 2 (T4.1's extra sub-case g₁+g₂ = g₄ is
  superfluous), and "any 3 of 4 cyclic positions contain an adjacent pair"
  instead of T4.3's enumeration.
- **r=3 endgame differs**: T4 uses the coarse signature-size argument
  (|sig(U)| = 3 vs |sig(single)| = 1); Sol proves the sharper identity
  p(V) = p(W) from the complementarity 2a + 2b = N and then runs the three
  Kelly tests explicitly. Sol's identity is a genuine refinement: the two
  single classes are parity-indistinguishable *from each other*, yet the
  doubled class is still pinned.
- **Scope differs**: T4 additionally proves *abstract* uniqueness of the
  parity system (requiring the r=2 signature-distinctness analysis, which
  Sol neither needs nor claims). Sol proves the realizable version only —
  and states so. Both versions suffice for Theorem R at n = 4.

Status: **O3 now has two independent proofs** (double demonstration,
cross-model). Canonical freeze remains 7a059048; T4 (ac6a3078) unchanged;
L0–L6 and Theorem R untouched — no demonstrable error found in either.

## Requested repair (cosmetic)

§2 should add: "for each x ∈ S, #{y ∈ S∖{x} : SC((S∖{x})∖{y}) = R} =
a_{SC(S∖{x}),R} by Lemme 1 applied to the card S∖{x}; summing over x and
grouping by card class gives Σᵢ mᵢ a_{Cᵢ,R}." Nothing else.

**Repair status: APPLIED (2026-07-15, confirmed by Sol).** §2 now makes the
real-deletion → class-coefficient step explicit before the multiplicity
grouping. The proof is final; no remaining reservations. O3 is closed by two
independent, fully reviewed proofs. Sol's next assignment: O2a (multiset deck
⟹ (P, F) at n = 4), which with L7 + O3 would close tetrachord set-deck
reconstruction entirely.
