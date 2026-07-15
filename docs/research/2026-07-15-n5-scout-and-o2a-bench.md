---
title: "Scout n = 5 + O2a adversarial bench — preparatory results"
date: 2026-07-15
type: research scout (empirical, review-preparation; no theorem claimed)
status: all findings empirical on stated ranges; conjectures labeled C5.x / roadmap for O2a
relates: 2026-07-13-deletion-deck-reconstruction-theorem.md, 2026-07-13-deletion-deck-hostile-referee-report.md
code: code/2026-07-13-deletion-deck/{n5_scout.py, o2a_adversarial_bench.py}
---

# Scout n = 5 and the O2a adversarial bench

Two preparatory workstreams while Sol attempts O2a: (a) an adversarial bench
that any O2a proof will be tested against; (b) the structural scout of the
n = 5 stratum, next proof target after the tetrachords. **Everything here is
empirical on the stated ranges — nothing is claimed beyond them.**

## A. O2a bench: zero spurious de-fusings (4 ≤ N ≤ 30)

O2a = "the multiset deck of a tetrachord determines (P, F)". The natural
attack picks a *fused element* in each card. Define the two cheap necessary
constraints on an assignment (e₁, …, e₄), one pick per card:

- **(c1)** the 8 surviving elements form 2·P for some 4-multiset P (each
  part survives in exactly 2 cards);
- **(c2)** {e₁, …, e₄} splits into two pairs, each summing to N.

Bench result over **every** tetrachord deck, 4 ≤ N ≤ 30: **no deck admits
any assignment satisfying (c1) + (c2) other than the true one** (and zero
decks with two realizable (P, F) — consistent with multiset-deck
injectivity). Consequence: O2a has a precise proof roadmap —

> **(O2a′)** prove that a (c1)+(c2)-consistent assignment is unique.
> Then survivors give P, picks give F, and Lemma L7 finishes.

Any submitted O2a proof will be reviewed against this bench; a proof
strategy that needs more than (c1)+(c2) is admissible but suspect of
overcomplication; one that needs less is suspect of a gap.

## B. Scout of the n = 5 stratum (7 ≤ N ≤ 24, all pentachord classes)

At n = 5 the cards are **4-necklaces (cyclic order matters up to dihedral),
no longer multisets** — the first structural jump, and the reason the
(P, F)-style coarsening that dies at n = 6 must be handled carefully from
here up.

Findings (all empirical on the range):

1. **Parity + ICV-integrality pin m uniquely for every pentachord class**,
   7 ≤ N ≤ 24 — the O1 empirics extend to n = 5, where the two systems
   genuinely differ for the first time (n − 2 = 3).
2. **Genuine complementarity.** Parity-only fails on scattered classes
   (mostly 2+2+1 profiles); ICV-only fails on exactly **one scaling
   family**: the necklace d·(1, 1, 2, 4, 2), N = 10d (observed d = 1, 2,
   i.e. Z10 and Z20 — mod-5 moduli again), where ICV admits the false
   profile (1,1,3) beside the true (2,2,1); parity kills the impostor
   (δ = (1,1,−2) is odd on two coordinates). Neither system alone
   suffices at n = 5; together they did on every class tested.
3. **Multiplicity profiles observed: only 1⁵, 2+1+1+1, 2+2+1, and 5.**
   Never 4+1, never 3+2, never 3+1+1.
   - **C5.1 (conjecture):** at n = 5, three equal cards force five equal
     cards (the T4.3 phenomenon persists).
   - Profile 5 occurs exactly at 5 | N via the regular necklace
     (d,d,d,d,d).
   - Profile 2+1+1+1 occurs only at 3 | N on this range (counts 1, 1, 2,
     2, 3, 3 at N = 9, 12, 15, 18, 21, 24) — unexplained; worth a
     structural look. **C5.2 (conjecture):** 2+1+1+1 at n = 5 requires
     3 | N.
4. Adjacent (|i−j| = 1) and distance-2 card coincidences occur in roughly
   equal numbers; both types are realized — the n = 4 luxury that adjacent
   coincidences pair up (T4.1) does **not** visibly survive as-is.

## What this sets up

- The O2a review can start the moment Sol's proposal lands: catalog ready,
  roadmap (O2a′) identified independently.
- The n = 5 proof campaign has its shape: first prove C5.1 (no 3+2 / 4+1 /
  3+1+1), then classify the 2+2+1 and 2+1+1+1 configurations the way T4.4
  classified the tetrachords, then run the two-system uniqueness per
  configuration. The d·(1,1,2,4,2) family is the canonical hard instance
  to keep on the bench.
