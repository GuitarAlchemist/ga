# Pre-registration — public progression-arpeggio tracer

Written: 2026-09-28T01:35:59Z (before any POST to the public service in this study)
Source revision read: origin/main `023fa941` (`Common/GA.Business.ML/Agents/Skills/ImprovisationSkill.cs`)
Study: [docs/research/2026-09-25-ga-chatbot-baseline.md](../../../docs/research/2026-09-25-ga-chatbot-baseline.md) §5, Wayfinder question GuitarAlchemist/.github#75.

Frozen inputs: `cases.json` sha256 `c7b60ea59555e550b8e7826538e248b87957262e1c599e171959c7fe6158d14d`; `oracle.py` sha256 `d71ba563fd0e6f73bcfbea9333ef2f304dd3909f942cd38cfc4887563aa7b7df`.
The oracle was self-tested offline on a synthetic answer and on a mutant (`Amm7`, `Fm`); it failed the mutant on quality and forbidden-content checks.

## Oracles (derived from interval arithmetic, not from the system's output)

1. Transport 200, a `routing` frame, a terminal `[DONE]`, no `{"error": ...}` frame, answer of at least 40 characters.
2. Route `skill.improvisation` for P1, P2, N1.
3. One per-chord line per input chord, in input order.
4. Each arpeggio's pitch classes equal the written chord's pitch classes (quality preserved; no `Amm`).
5. Each lead scale contains every chord tone.
6. **Key fit (stronger than §5 of the study):** for a fully diatonic progression in C major / A minor, each lead scale stays inside {C D E F G A B}. Not applied to the deliberately non-diatonic A in N1.
7. N1 secondary-dominant guard: the lead scale over A keeps C# and contains no natural C (no silent Aeolian / natural-C advice).
8. N2 invalid input: no fabricated per-chord arpeggio line; the answer declines or states uncertainty (keyword heuristic — flagged as such).

## Predictions from source reading

- P1 `which arpeggio fits Am F C G`: oracles 1–5 pass. Oracle 6 **fails for F** (lead `F Ionian` adds Bb) **and G** (lead `G Ionian` adds F#): the skill classifies each chord alone and records "no key inferred".
- P2 `Dm7 G7 Cmaj7`: all pass (D Dorian, G Mixolydian, C Ionian).
- N1 `C A Dm G`: guard 7 passes (A Ionian); oracle 6 fails for Dm (D Aeolian adds Bb) and G.
- N2 `Hm Q7`: route unknown (the improvisation gate needs a chord token); prediction: no fabricated line.

## Protocol

Seven POSTs in total: P1 three times, then P2, N1 and N2 once each, 2 s apart; then one browser submission of P1 through the public page. No deployment, restart, credential or paid API is used. Latency is recorded (routing frame, first text, total) but no threshold is set from it.

A falsified prediction is reported as observed; the oracles are not edited after the run.
