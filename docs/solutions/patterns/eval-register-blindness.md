---
module: cross-cutting
tags: [quality, evals, corpus, routing-eval, false-green, measurement]
problem_type: learning
---

# Pattern: eval-register blindness

**A quality gate that has been green for months is not evidence the behaviour is
correct. It is evidence the bug lives in a *register the gate does not sample*.**

Every instance below shares one shape: a harness with real coverage of the
*subject* (the skill, the intent, the pipeline) but no coverage of the
*phrasing, environment, or failure mode* the bug actually occupied. Each gate
was working exactly as designed. None of them were careless.

## Instances

| date | gate | what it covered | what it missed | how the bug surfaced |
|---|---|---|---|---|
| 2026-05-30 | prompt corpus (100%) | deterministic skills | no LLM-bound or voicing-search prompts | user report; a null-deref in `CpuVoicingSearchStrategy` |
| 2026-07-19 | prompt corpus (98.08%) | answer *content* | could not distinguish "correct" from "degraded environment" — 50/52 passed against a **bogus chat model** | manual model swap |
| 2026-07-20 | prompt corpus | chord + scale skills | no `"what notes are in X"` prompt at all | LLM-judge probe |
| 2026-07-20 | `routing-eval` (174 prompts) | 10 chordinfo prompts, all passing | all **chord-symbol-shaped** (`Cmaj7`, `Dm7`, `Eb7#9`) — never the spelled-out register (`"a C major triad"`) | passed the *fix* at **Δ 0.0%** |

The last row is the sharpest: a 174-prompt routing evaluation ratcheted green
for months while the product's most basic question returned a 7-note scale for
a 3-note triad request — and then certified the fix as a no-op.

## Why it recurs

Eval sets are written by sampling the *space the author was thinking about*. The
author of a chord-info eval reaches for chord symbols, because that is the
canonical form. Users reach for prose. The gap between those two is invisible
from inside the eval.

It compounds: a green gate is read as "this area is covered", which suppresses
the instinct to add cases there.

## Countermeasures

1. **Ask which register, not which subject.** "Is chord-info covered?" is the
   wrong question — it was, ten times over. Ask: *which phrasings of this
   question are anchored?*
2. **Assert structure, not just content.** During one regression the substring
   invariants passed throughout, because the LLM fallback still produced a
   correct-looking answer. Pinning `routes_to` caught what `contains` could not.
   A content-only corpus is structurally blind to routing regressions.
3. **A no-op delta on a real fix is a finding, not a pass.** If a gate does not
   move when you fix a real bug, the gate cannot see that bug class. Investigate
   the Δ 0.0%, do not bank it.
4. **Distinguish "ran and saw no failures" from "could not run".** See
   [[feedback-auto-optimize-oracle-paranoia]] — a gate reporting success because
   it could not measure is the same disease.
5. **When a gate catches nothing for a long time, suspect the gate.** Long green
   streaks on an evolving surface are a smell, not a trophy.

## Related

- `../architecture/2026-05-30-chatbot-routing-is-embedding-first.md`
- `../architecture/2026-07-20-router-anchor-shape-misroute-chord-vs-scale.md`
- Issue #560 — routing-eval register gap (open)
- PR #556 — LLM-judge gate, added specifically because substring invariants
  cannot distinguish a 3B model from a frontier one

## The self-application

The same failure appeared three times in my own measurement during the
2026-07-20 session, which is why this note exists rather than a tidier one:

- A **9/9** routing score where 3 test queries *were* the anchors being tested
  (an anchor scores cosine 1.000 against itself).
- A **17/17** normalizer score where the two hardest cases had been written into
  the harness as "expect unchanged".
- A **"zero chordinfo coverage"** finding produced by querying lowercase JSON
  keys against a PascalCase schema — the filter matched nothing and the empty
  result was read as a result.

All three looked like clean passes. **An empty or zero result deserves the same
scrutiny as a surprising positive one.**
