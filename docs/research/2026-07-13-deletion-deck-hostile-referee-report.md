---
title: "Hostile-referee report — deletion-deck reconstruction: what is proved, what is empirical, what is open"
date: 2026-07-13
type: adversarial re-examination (rapporteur hostile)
status: >
  Lemmas L0–L5 and the Reduction Theorem PROVED. The two empirical links
  (multiplicity-recovery uniqueness for n ≥ 4; multiset-deck completeness)
  verified on bounded ranges ONLY (≤ Z20 resp. ≤ Z30) — no claim beyond.
  The n = 3 stratum is closed for ALL N by the trichord theorem.
relates: 2026-07-13-deletion-deck-reconstruction-theorem.md
code: code/2026-07-13-deletion-deck/
---

# Hostile-referee report: deletion-deck reconstruction in Z_N

## 0. Mandate and rules of engagement

This report re-examines the deletion-deck program **without assuming the
general conjecture**. Ground rules enforced throughout:

- Abstract solvability of a multiplicity system is **never** conflated with
  geometric realizability (§5 shows they differ massively).
- No result verified on 6 ≤ N ≤ 20 (or ≤ 30) is stated for general N.
  Empirical claims carry their exact range.
- Every lemma below is either **proved in full** here, or explicitly tagged
  *empirical (range)*.
- All exhaustive computations are reproducible:
  [`code/2026-07-13-deletion-deck/`](code/2026-07-13-deletion-deck/). The
  scripts assert their own sanity conditions (the true multiplicity vector
  satisfies parity; sub-case (ii) of L4 never fires); outputs are committed.

Notation. D_N is the dihedral group acting on Z_N (transpositions
u ↦ t + u and inversions u ↦ t − u); SC(T) the D_N-class of T ⊆ Z_N.
For |S| = n ≥ 3, the **set-deck** is D(S) = { SC(S∖{x}) : x ∈ S } (support
only), the **multiset-deck** M(S) keeps multiplicities. ICV(T) is the
interval-class vector, a class invariant.

## 1. The gluing system, formalized (Point A)

Four kinds of object, kept distinct:

1. **Real subsets**: S ⊆ Z_N and its real cards S∖{x}, x ∈ S.
2. **Card classes**: the distinct elements C_1, …, C_r of D(S).
3. **External multiplicities** m_i = #{x ∈ S : SC(S∖{x}) = C_i}
   (so m_i ≥ 1, Σ m_i = n; the vector m is exactly the datum lost when
   passing from M(S) to D(S)).
4. **Internal deletion profiles** a_{iR} = number of one-element deletions
   of a card of class C_i landing in the (n−2)-class R.

**Lemma L0 (deck invariance — makes a_{iR} well-defined).**
For any T ⊆ Z_N and g ∈ D_N, the map t ↦ g(t) is a bijection T → g(T) with
g(T)∖{g(t)} = g(T∖{t}). Hence the multiset { SC(T∖{t}) : t ∈ T } depends
only on SC(T). In particular, for **any real card** T with SC(T) = C_i and
any class R: #{t ∈ T : SC(T∖{t}) = R} = a_{iR}. ∎

This is the step that licenses gluing class-level data (a_{iR}) to
subset-level counts; without L0 the parity system below would be ill-posed.

**Lemma L1 (Kelly double-deletion parity).** For every (n−2)-class R:

    Σ_i  m_i · a_{iR}  ≡  0   (mod 2).

*Proof.* Count X_R = #{ ordered pairs (x, y), x ≠ y in S, with
SC(S∖{x,y}) = R }. Grouping by first coordinate and applying L0 to the card
S∖{x}: X_R = Σ_{x∈S} #{ y ∈ S∖{x} : SC((S∖{x})∖{y}) = R }
= Σ_i m_i a_{iR}. Grouping instead by the unordered pair {x, y}: each pair
contributes 0 or exactly 2 ordered pairs (both orders yield the same set
S∖{x,y}). Hence X_R is even. ∎

**Lemma L2 (ICV identity and integrality).**

    Σ_{x∈S} ICV(S∖{x}) = (n−2) · ICV(S),

hence, using L0-style class invariance of ICV,
Σ_i m_i · ICV(C_i) ≡ 0 (mod n−2) **componentwise** — a necessary condition
on m that references only the support {C_i}, not the unknown S.

*Proof.* An unordered pair {u, v} ⊆ S contributes its interval class to
ICV(S∖{x}) iff x ∉ {u, v}, i.e. in exactly n−2 of the n summands. The
integrality condition follows because the left side is computable from
(m, support) alone. ∎

**Definition (the multiplicity system).** Given only a support
{C_1, …, C_r} of (n−1)-classes (n is readable off the deck: cards have
n−1 elements), the system is

    m ∈ Z^r,  m_i ≥ 1,  Σ m_i = n,
    Σ_i m_i a_{iR} ≡ 0 (mod 2)          for every R        (L1)
    Σ_i m_i ICV(C_i) ≡ 0 (mod n−2)      componentwise      (L2)

Any real S with set-deck {C_i} has its true m* as a solution. If the system
has a **unique** solution, the set-deck determines the multiset-deck.

## 2. The n = 3 degeneracy, proved (and the explicit counterexample — Point 1)

**Lemma L3 (both systems are vacuous at n = 3).**
(i) Every card is a dyad; both its deletions are singletons, and all
singletons form one class R₀, so a_{i,R₀} = 2 for every i and the parity
system reads 2·Σm_i ≡ 0 (mod 2) — no constraint.
(ii) n − 2 = 1, so ICV-integrality is trivially satisfied. ∎

So at n = 3 the only constraints are m_i ≥ 1, Σ m_i = 3: every 2-class
support carries the abstract ambiguity {(1,2), (2,1)}. This is exactly the
stratum where the real collisions live — the obstruction is not an accident
but the degeneracy of both counting systems.

**Explicit counterexample (machine-verified, `hostile_referee.py`).**
Z10, support {SC({0,2}), SC({0,4})}:

- A = {0,2,4}: cards {0,2}, {0,2}, {0,4} → m = (2,1);
  ICV check: 2·ICV({0,2}) + 1·ICV({0,4}) = (0,2,0,1,0) = ICV(A). ✓
- B = {0,2,6}: cards {0,4}, {0,4}, {0,2} → m = (1,2);
  ICV check: 1·ICV({0,2}) + 2·ICV({0,4}) = (0,1,0,2,0) = ICV(B). ✓

Both multiplicity vectors are integral solutions **and both are
geometrically realizable** — this is the proved mod-5 collision seen from
the system side. Any hoped-for "ICV pins m" claim dies at n = 3.

## 3. The Reduction Theorem (proved, conditional)

**Theorem R.** Fix N and n ≥ 3. Suppose:
(a) the multiplicity system of §1 has a unique solution for every support
    arising as the set-deck of some n-class in Z_N; and
(b) multiset-deck reconstruction holds at (N, n) (M(S) determines SC(S)).
Then set-deck reconstruction holds at (N, n).

*Proof.* Let SC(S₁) ≠ SC(S₂) share a set-deck. The set-deck **is** the
support, so both classes yield the same system; their true multiplicity
vectors are solutions (L1, L2); by (a) they coincide, so M(S₁) = M(S₂);
by (b) SC(S₁) = SC(S₂), a contradiction. ∎

Theorem R is unconditional as an implication. The two hypotheses have this
status:

| Link | Status | Range |
|---|---|---|
| (a) uniqueness, n ≥ 4 | **empirical** | all 6 ≤ N ≤ 20, every class (§4) |
| (a) uniqueness, n = 3 | **false** (L3 + §2) | all N |
| (b) multiset completeness | **empirical** | all 6 ≤ N ≤ 30 (C sweep) |
| n = 3 endgame | **proved** for all N | trichord theorem (mod-5 pairs only) |

## 4. Exhaustive uniqueness results (Point B)

Full enumeration of the system (no early stopping — an earlier draft of the
bench truncated the solution list at 3 and could have **falsely certified
uniqueness after the ICV filter**; found and fixed before any run was
reported). Composition counts are ≤ C(19, 9) on this range, so complete
enumeration is cheap. Scripts: `hostile_referee.py` (6–16),
`referee_ext.py` (17–20); committed outputs `referee_*.txt`.

**Headline result.** For every 6 ≤ N ≤ 20 and **every class of cardinality
n ≥ 4**, the system (parity + ICV-integrality) has exactly one solution —
the true multiplicity vector. Ambiguity survives **only at n = 3**
(consistently with L3). In particular, for **all tetrachords** on this
range, multiplicity recovery holds — and at n = 4 the **parity system
alone** already pins m for every class of every N ≤ 20 (empirical; we do
not claim it in general).

Neither constraint family is dispensable for n ≥ 5:

- Parity alone fails at many (N, n): minimal example Z6, n = 5 (its unique
  5-class has 2 parity solutions); worst observed case Z20, n = 19 with
  **536** parity solutions — all killed by ICV-integrality except the true
  one.
- ICV-integrality alone is not tested as a standalone system here; at
  n = 3 it is vacuous and at n ≥ 4 the bench applies it only after parity.
  (Open: whether ICV alone suffices for n ≥ 4 — not claimed.)

Summary of the ambiguity landscape (rows with a non-uniqueness only):

| range | ambiguous (N, n) rows | all at n = | worst #parity-solutions |
|---|---|---|---|
| 6 ≤ N ≤ 16 | 11 (one per N) | 3 | 121 (Z16, n = 15) |
| 17 ≤ N ≤ 20 | 4 (one per N) | 3 | 536 (Z20, n = 19) |

Per-(N, n) tables with class counts, parity-unique counts, parity+ICV
counts and max solution counts: committed outputs
[`referee_6_16.txt`](code/2026-07-13-deletion-deck/referee_6_16.txt),
[`referee_17_20.txt`](code/2026-07-13-deletion-deck/referee_17_20.txt).

**Sanity assertions passed on every class**: the true m* satisfies L1
(machine check of the Kelly lemma itself), and m* belongs to the enumerated
solution set.

## 5. Abstract uniqueness vs realizability (Point C)

These are different questions, and the data keeps them apart:

- On 6 ≤ N ≤ 16 there are **48** abstractly ambiguous n = 3 cases
  (2-class supports, both (1,2) and (2,1) admissible).
- Requiring the hypothetical multiset-deck to be realized by an actual
  class kills the wrong vector in **all but the mod-5 pairs**. Exhaustive
  over 6 ≤ N ≤ 20 (`realizability_n3.py`): the only doubly-realizable
  supports are

      Z10: {0,2,4} / {0,2,6};   Z15: {0,3,6} / {0,3,9};   Z20: {0,4,8} / {0,4,12}

  i.e. exactly the proved trichord collisions — and by the trichord theorem
  this classification holds for **all** N, not just the tested range.
- Consequently: an ambiguous abstract system is **not** evidence of a
  collision, and (contrapositive, the referee's warning) a uniqueness proof
  for the abstract system is **stronger than needed** — it may fail while
  reconstruction still holds via realizability. Any future proof attempt
  should be stated against the abstract system first (harder, cleaner), and
  fall back to realizability arguments only explicitly.

## 6. Repeated cards: the structural lemma (Point D)

**Lemma L4 (repeated-card dichotomy).** Let x ≠ y ∈ S with
SC(S∖{x}) = SC(S∖{y}), and let g ∈ D_N be any witness,
g(S∖{x}) = S∖{y}. Then exactly one of:

- **(i) symmetry**: g(x) = y, and then g(S) = S — g is a nontrivial
  automorphism of S carrying x to y;
- **(iii) controlled exchange**: g(x) ∉ S, and then
  g(S) = (S∖{y}) ∪ {g(x)} — so S is D_N-equivalent to the set obtained by
  exchanging y for the outside point g(x).

The remaining case **(ii)** g(x) ∈ S∖{y} is **impossible**.

*Proof.* g(S) = g(S∖{x}) ∪ {g(x)} = (S∖{y}) ∪ {g(x)}.
If g(x) = y then g(S) = S (case i). If g(x) ∈ S∖{y} then
g(S) = S∖{y}, so n = |g(S)| = n − 1, absurd (case ii impossible). Otherwise
g(x) ∉ S and the union is disjoint, giving case (iii). ∎

Heeding the mission's warning that "g(x) is not necessarily y", the
sub-cases were also verified **exhaustively** for 6 ≤ N ≤ 14
(`point_d_lemma.py`, all assertions passed over every repeated-card pair of
every class):

| N | repeated-card pairs | some symmetry witness, no exchange | exchange only | both kinds |
|---|---|---|---|---|
| 6–14 | 30 … 798 per N | vast majority | rare: 1 (Z9), 4 (Z12), else 0 | minority |

The exchange-only case is **real**, not hypothetical — minimal witness:
Z9, S = {0,1,3,4,6}, x = 1, y = 4: the unique-up-to-symmetry witness is
u ↦ u + 6 with g(1) = 7 ∉ S, exhibiting S ≅ {0,1,3,6,7}; **no** symmetry
of S carries 1 to 4. Any structural proof of multiplicity recovery must
therefore handle genuine exchanges, not just symmetric sets.

## 7. Cyclic-composition (necklace) reformulation (Point E)

**Lemma L5 (dictionary).** For n ≥ 3 there is a bijection between
(a) D_N-classes of n-subsets of Z_N and (b) length-n cyclic compositions of
N (parts ≥ 1) up to rotation and reflection ("gap necklaces"): send S to
its word of circular gaps. Rotation of Z_N rotates the word's basepoint;
inversion reflects the word. Under this dictionary, **deleting an element
fuses its two incident gaps**, so the multiset-deck of S is the multiset of
the n necklaces obtained by fusing one adjacent pair.
*Proof sketch (complete at this level).* Gaps determine the set up to
rotation; the D_N action on subsets corresponds exactly to
rotation/reflection of gap words; deletion of the vertex between gaps
g_{j−1}, g_j replaces them by g_{j−1} + g_j and leaves other gaps
untouched. ∎

**Lemma L6 (part survival — the composition-level ICV germ).** In the n
fusions of a length-n necklace, the part g_j is consumed in exactly 2 (the
fusions of (g_{j−1}, g_j) and (g_j, g_{j+1})) and survives untouched in the
other n − 2. Hence the multiset union of the parts of all n fused necklaces
equals (n−2)·{parts of S} ⊎ {g_{j} + g_{j+1} : j}. ∎
(L2 is the ic-projection of this identity; L6 is finer — it also constrains
the fused sums, unused so far: a genuine lever for a future proof.)

**Abstract collision search.** By L5, searching for deck collisions among
necklaces **is** the sweep already performed on subsets: complete for
6 ≤ N ≤ 30 (C implementation, ~2.1·10⁹ subsets): no multiset-deck
collision anywhere; set-deck collisions exactly the mod-5 trichord pairs.
No new search was needed — the referee confirms the two formulations are
equivalent, so composition-level intuition can be trusted on the tested
range.

### 7.1 The L6 invariant tested against O2 (addendum, same day)

Write P for the multiset of parts of the necklace and
F = {g_j + g_{j+1}} for the multiset of adjacent sums — the pair (P, F) is
exactly what L6 extracts from the deck-level union
U = (n−2)·P ⊎ F. Exhaustive injectivity test on necklaces
(`l6_invariant_test.py`):

- **(P, F) is NOT a complete invariant for n ≥ 6.** Minimal counterexample
  at N = 8: necklaces (1,1,1,2,1,2) and (1,1,2,1,1,2) share P **and** F
  (and U), yet their decks differ — e.g. the first deck contains the card
  (1,2,1,2,2), the second (1,1,2,2,2), same parts in different cyclic
  order. Collisions grow rapidly (Z24, n = 7: 2604 pairs). The obstruction
  is precise: (P, F) forgets the **cyclic arrangement**, which the deck's
  individual cards retain. Any proof of O2 must therefore use how parts
  co-occur *within* cards, not just the global part/sum counts — the naive
  L6 route is dead for n ≥ 6.
- **(P, F) has no collision at n = 4 or n = 5 for any N ≤ 40** (empirical).
- **Lemma L7 (proved): at n = 4, (P, F) determines the necklace.**
  *Proof.* The six pairwise sums of P split as F ⊎ O with
  O = {g₁+g₃, g₂+g₄} the opposite-pair sums, so O = (pairwise sums) ∖ F is
  computable from (P, F) by multiset subtraction. A 4-necklace up to
  rotation/reflection is exactly a partition of P into two opposite pairs.
  Two distinct partitions with equal sum-multisets — say
  {a+b, c+d} = {a+c, b+d} — force b = c (or a = d), and then the two
  partitions coincide as partitions of the *multiset* P, hence give the
  same necklace. ∎

This refines O2 at the bottom of the ladder: **(O2a)** show the multiset
deck determines (P, F) at n = 4 (then L7 finishes: multiset-deck
completeness for tetrachords, all N — which with Theorem T4 would close
set-deck reconstruction for tetrachords entirely). Empirical support: no
U-collision at n = 4 for N ≤ 24 and no (P, F) collision for N ≤ 40; what
is missing is the deck → (P, F) extraction, nontrivial because which part
of a card is the fused one is not marked.

## 8. What remains open (no extrapolation)

- **(O1)** Uniqueness of the parity+ICV system for n ≥ 4, general N.
  Verified only for 6 ≤ N ≤ 20. The proof must survive genuine exchange
  configurations (§6) and cannot use realizability implicitly (§5). L6's
  fused-sum constraint is unexploited and is the referee's best candidate
  lever.
- **(O2)** Multiset-deck completeness, general N. Verified only for
  6 ≤ N ≤ 30. Note (O1) + (O2) + Theorem R ⟹ the general n ≥ 4
  conjecture; but neither is proved, and (O1) might fail at some larger N
  while reconstruction still holds (§5's realizability gap) — failure of
  (O1) would weaken the strategy, not refute the conjecture. §7.1 sharpens
  the route: the coarse L6 invariant (P, F) is provably insufficient for
  n ≥ 6, sufficient-if-extractable at n = 4 (Lemma L7); the tetrachord
  instance is isolated as **(O2a)**: deck → (P, F) extraction at n = 4.
  **(O2a) CLOSED (2026-07-15): Sol's extraction + ordering proof, reviewed
  and accepted** ([review](2026-07-15-sol-o2a-proof-adversarial-review.md))
  — with O3 this closes tetrachord set-deck reconstruction entirely; O2
  remains open only for n ≥ 5.
- **(O3)** ~~Whether parity alone suffices at n = 4~~ **CLOSED (2026-07-13,
  same day): Theorem T4 in the main write-up** — parity has a unique
  admissible solution for every tetrachord support, **all N**, via the exact
  coincidence classification of tetrachord decks (adjacent coincidences come
  in pairs; (3,1) impossible; r = 3 forces the necklace (a,b,b,a) whose
  doubled card has signature size 3 vs size 1 for the singles; r = 2 forces
  (2,2) with distinct signatures). Note: at n = 4, Kelly parity **is**
  ICV-integrality (a trichord's deletions are its ICV), so T4 is exactly the
  n = 4 instance of O1 — the first inductive floor of the program.
- **(O4)** Whether ICV-integrality alone (without parity) suffices at
  n ≥ 5. (At n = 4 the two systems coincide, so O4 starts at n = 5.)
- **Closed** (for all N, by proof): the n = 3 stratum — collisions exist
  iff 5 | N, and are exactly {0,d,2d}/{0,d,3d}, d = N/5 (trichord theorem;
  re-derived and re-checked in this pass, including the boundary sub-cases
  v_i = N/2 in the k-flip count).

## 9. Referee verdict on the program's prior claims

| Prior claim | Verdict |
|---|---|
| Trichord theorem (mod-5 classification, all N) | **SOUND** — re-derived; k ≥ 2 flips impossible including boundary v = N/2 cases |
| "Multiset-deck complete" | Correctly stated as empirical ≤ Z30; **keep the range qualifier** |
| "Set-deck holds for n ≥ 4" | Empirical ≤ Z30; now **explained** by (O1)+(O2)+R but still unproved in general |
| "Multiplicity-recovery lemma" (was open problem #1) | **Upgraded**: formal system stated (L1+L2), reduction proved (R), uniqueness verified exhaustively n ≥ 4, N ≤ 20; remains open as (O1) |
| "Three cards give too few cross-constraints" (heuristic) | **Now a theorem** (L3): both systems are provably vacuous at n = 3 |

The best honest statement of the program's status: *set-deck reconstruction
for n ≥ 5 is reduced, by a proved theorem, to two independent empirical
regularities (O1, O2), each exhaustively verified on a bounded range; the
proved general-N statements are the trichord classification (n = 3), the
tetrachord multiplicity-recovery theorem T4 (n = 4, closing O3), and the
lemmas L0–L6.*
