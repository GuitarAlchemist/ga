---
module: docs/research (deletion-deck campaign) / discovery-engine methodology
tags: [research, conjecture, relation-mining, empirical-first, necklaces, pentachords, false-conjecture-prevention]
problem_type: best_practice
decision: "Before stating a structural lemma, mine ALL empirical instances with a relation grid and inspect raw examples — the correct statement may contain a clause invisible to the intuitive abstraction."
rejected:
  - "Conjecturing the n=5 distance-2 coincidence lemma by analogy with the n=4 case ({g1,g2}={g3,g4} multiset equality) — the analogy is FALSE."
reason: "The true pentachord lemma L5.D has an alternating branch requiring the extra clause g5 = g1+g2, which no multiset-level intuition produces. It was caught only because the empirical coincidence families were extracted first and the (1,2,1,2,3)-type instances showed the fifth gap always equals the fused sum — a relation absent from the initial relation grid, visible only in the raw instances. A proof attempt against the naive statement would have failed mysteriously; worse, a careless 'verification' could have shipped a false lemma."
date_decided: 2026-07-16
---

# Relation-mine before conjecturing

## The failure mode

Structural intuition transfers laws across levels (n=4 → n=5, multisets →
cyclic words) and silently drops clauses. At n=4, opposite-fusion coincidence
is `{g1,g2} = {g3,g4}` plus a complementarity condition; at n=5 the same
question splits into two branches, one of which needs `g5 = g1 + g2` — the
fifth gap must equal the fused sum exactly. Nothing in the multiset picture
suggests this.

## The working procedure

1. Enumerate every instance of the phenomenon on a decent range (here: all
   card coincidences, all pentachord necklaces, 7 ≤ N ≤ 40).
2. Normalize (rotate the pattern to a canonical position) and classify by a
   grid of candidate linear relations.
3. **Inspect raw instances of every family** — a family whose signature looks
   complete may satisfy an extra relation missing from the grid (that is
   exactly how g5 = g1+g2 was found: the grid said only "g1=g3, g2=g4", the
   instances all had the extra property).
4. Only then state the biconditional, test it as an exact ⟺ over the full
   range, and go prove it (alignment enumeration).

## Where this lives

`docs/research/code/2026-07-13-deletion-deck/n5_coincidence_families.py`
(the miner), `n5_lemma_candidates.py` (the biconditional test),
`2026-07-15-pentachord-coincidence-lemmas.md` (the proved lemmas).
