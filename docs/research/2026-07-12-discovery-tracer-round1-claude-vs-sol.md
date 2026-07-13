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

---

# Round 2 — original-conjectures-only, harder shapes (same day)

Rules hardened: textbook theorems disqualified; exact censuses,
characterizations, and substructure laws required; **pre-verification by code
explicitly allowed** (both sides; Sol declared `PREVERIFIE: oui` on all 5).

## Scoreboard (cumulative: Sol 10/10, Claude 7/10)

| Generator | R2 survived | Notes |
|---|---:|---|
| GPT-5.6 Sol | **5/5** | survivors-only submission (private generate→verify→filter loop) |
| Claude | 3/5 | kills published (R2 Z-tetrachord trichords; R3 mono-trichord uniqueness) |

## Surviving round-2 laws

- **Claude R1**: all-distinct-ICV classes exist only at cards 6 and 7, exactly
  two each ({0-5} + major hexachord {0,2,4,5,7,9}; {0-6} + diatonic).
- **Claude R4**: for d∈{1,5}, `ic_d(s)=|s|−1 ⟹ s is a d-generated chain`.
- **Claude R5**: exactly **20**/50 hexachord classes are self-complementary,
  and they are precisely the non-Z hexachords.
- **SOL-R2-1/2**: exactly **2** all-tetrachord octachords; exactly **5**
  all-pentachord nonachords (listed in the law set).
- **SOL-R2-3**: unique all-trichord hexachord {0,1,2,4,7,8} — *survives the
  sweep but is literature-known* (Carter/Morris all-trichord hexachord);
  novelty claim to be deflated by the tribunal.
- **SOL-R2-4** (most striking of the duel): the **set of deletion classes
  determines the Tn/TnI class** for card ≥ 3 — a reconstruction-type result;
  candidate genuine find pending literature check.
- **SOL-R2-5**: unique-deletion AND unique-addition neighbor classes are
  exactly {0369}, {014589}, whole-tone, octatonic — i.e. Messiaen's
  limited-transposition family; elegant, known-adjacent.

## Instructive kills (Claude, published)

- Z-tetrachords {0146}/{0137} **do** share trichord classes ((016), (026)).
- Mono-trichord classes of card ≥ 4 are **three**, not one: {0167},
  **{0268} (French sixth)**, {0369} — the refutation itself was a discovery.

## Round-2 reading (honest)

Pre-verification changes what is measured: round 2 demonstrates both
frontiers can run the **full FunSearch-style loop end-to-end** (generate →
exhaustively verify → filter), which is the load-bearing feasibility fact for
the discovery engine. Survival rate is no longer a proxy for raw mathematical
intuition once the loop is private. **Pause rule invoked**: two rounds settle
feasibility; further rounds add laws, not information. Next per plan: ga#519
product evaluator + tribunal novelty judgment (SOL-R2-4, Claude R5, and the
combined Z-window 4–8 are the priority candidates; SOL-R2-3/5 to be deflated
as literature-known).

---

# Tribunal input (2026-07-13) — literature check + Zn extension

## Novelty verdicts (Sol literature pass, citations spot-checked for authenticity)

| Law | Verdict | Anchor |
|---|---|---|
| **SOL-R2-4 deletion-deck reconstruction** | **adjacent — exact theorem not found → serious original candidate** (conf. 0.84) | Lewin EMB/COV inclusion functions (JMT 21/2 1977; PNM 18 1979-80) keep multiplicities; Harary set-reconstruction (1964) is unweighted simple graphs — neither yields this theorem |
| Z-window 4–8 | adjacent — immediate consequence of published census (Straus 1991; Jedrzejewski & Johnson arXiv:1304.6608) | no standalone "Z-window theorem" phrase located |
| 20 self-complementary = non-Z | **known** (Forte 1985 + Straus 1991, direct corollary) | deflated |
| Periodic ICV bounds | **known** — Buchler's saturation formula is practically identical (diss. 1997 ch.2; PNM 38/2 2000, 55-58) | deflated |
| ic_d=n−1 ⟹ chain (d∈{1,5}) | **known** — special case of Buchler's maximizer characterization | deflated |

Citation authenticity: all major anchors verified real (Lewin, Straus, Forte,
Buchler, Harary, Jedrzejewski/Johnson). Two flags: "McKay up to 13 vertices"
(11 is the well-established bound — re-verify) and Joksimović arXiv:2605.22948
(2026 preprint, unverifiable from this session).

## Zn extension of the reconstruction law (new, this session)

Exhaustive sweep of the deletion-deck reconstruction over Z6..Z16
(`zn_reconstruction.py`, canonical Tn/TnI orbits per modulus):

- **HOLDS**: Z6, Z7, Z8, Z9, Z11, Z12, Z13, Z14, Z16.
- **FAILS**: **Z10** ({0,2,4} vs {0,2,6}) and **Z15** ({0,3,6} vs {0,3,9}).

The failures are one structural family: in Z(5d), the trichords {0,d,2d} and
{0,d,3d} share a deletion deck (invert({0,3d}) = {0,2d} mod 5d) while being
Tn/TnI-inequivalent — an analytic counterexample for every modulus divisible
by 5. **The law therefore fails iff 5|N on the tested range**, making the Z12
theorem non-vacuous: it sits on the good side of a real obstruction.

## Write-up frame (per the tribunal input)

The defensible contribution: *a set-reconstruction theorem for the family of
cyclic metric pitch-class graphs under the dihedral action — exhaustively
verified for Z12 (and all N ≤ 16 with 5∤N), with the mod-5 counterexample
family delimiting it* — explicitly positioned against Lewin's inclusion
vectors (which keep multiplicities) and Harary's set-reconstruction
conjecture (unweighted graphs). Next: structural proof attempt; check the
McKay bound; optionally sweep Z17–Z20 to confirm the 5|N boundary (Z20
predicted to fail via d=4).
