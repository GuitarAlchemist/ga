---
title: "Discovery-engine tracer, round 1 — Claude vs GPT-5.6 Sol under exhaustive Z12 verification"
date: 2026-07-12
type: research evidence (generator-verifier duel)
status: round 1 complete — GO verdict for the generator half reinforced
---

# Discovery tracer round 1 — two frontier generators vs the incorruptible sweep

First live run of the math-discovery-engine tracer shape (plan:
`2026-07-04-research-math-discovery-engine-plan.md`): two frontier LLMs each
propose 5 candidate laws about Z12 pitch-class sets; a session-side Python
verifier sweeps **all 4096 subsets exhaustively** (no sampling, no judge to
game — the METR eval-gaming concern about GPT-5.6 Sol has no purchase here).
Generators: Claude (session model, inline) and GPT-5.6 Sol (operator-run via
the subscription ChatGPT channel, structured prompt with predicate format).

## Scoreboard

| Generator | Survived | Killed | Declared-original survivors |
|---|---:|---:|---:|
| GPT-5.6 Sol | **5/5** | 0 | 4 |
| Claude | 4/5 | 1 | 1 |

**Claude's kill (the arena working as designed):** L3 claimed the diatonic
collection is the *unique* 7-note class with all-distinct ICV entries — the
sweep produced the chromatic heptachord {0,1,2,3,4,5,6} (icv 654321) as a
second one. No rhetoric survives 4096 cases.

## Surviving laws worth keeping

- **Complement-ICV affine formula** (both generators, known — Babbitt/Lewin):
  `icv(comp)[i] − icv(s)[i] = 12−2n` (ic1–5), `= 6−n` (ic6), all 4096 sets.
- **Z-relation cardinality window** (Claude floor + Sol ceiling): distinct
  Tn/TnI classes sharing an ICV exist **only** at cardinalities 4–8.
- **SOL-3 periodic ICV bounds** (most original of the round):
  `ic_d(s) ≤ |s| − [L∤|s|]` with L = 12/gcd(d,12) for d=1..5, and
  `ic6 ≤ ⌊|s|/2⌋`.
- **SOL-4 saturation ⇔ invariance**: `ic_d(s)=|s| ⇔ T_d fixes s` (d=1..5);
  `2·ic6=|s| ⇔ T_6 fixes s`.
- **SOL-5 Z-hexachords are mutual complement classes** — strictly stronger
  than the hexachord theorem (also rules out ICV triples among hexachords).
- **Claude L4 flat-ICV census**: exactly two classes have a flat ICV — the
  all-interval tetrachords {0,1,4,6}, {0,1,3,7}. **Claude L5**: any hexachord
  with no ic1 and no ic5 is the whole-tone class.

## Honest caveats

Single round, n=5 per side; generation conditions not controlled (Sol got the
full structured prompt); **survival ≠ novelty** — several survivors are
known-adjacent to set theorists, and novelty judgment belongs to the tribunal
per the plan. This round measures only: *can frontier generators produce
exhaustively-true, non-tautological laws?* Answer: yes, both.

## Implications

1. **The GO tracer verdict is now evidence-backed**: the generator half of the
   FunSearch-style loop works on our verifiable universe.
2. **The game-proof arena neutralizes the METR concern**: Sol's documented
   eval-gaming cannot touch an exhaustive mechanical verifier — this is the
   safe way to use it as a generator.
3. Remaining pieces are the ones already planned: the product evaluator
   (ga#519) and tribunal novelty judgment.

Verifier + both law sets live in the session scratchpad (`z12_sweep.py`,
`claude_laws.py`, `sol_laws.py`); pair-laws verified by ICV-group exhaustion
(equivalent to the full 16.7M-pair sweep). Raw verdict output reproduced in
the session log.
