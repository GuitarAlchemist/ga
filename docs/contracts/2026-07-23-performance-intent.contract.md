# Performance Intent (Arpeggio Suggestions) — Contract

**Version:** 0.1.0 (draft, tracer)
**Schema version:** 1
**Status:** Draft — tracer-bullet slice for [ga#589](https://github.com/GuitarAlchemist/ga/issues/589)
**Producer:** `GA.Business.Core.Orchestration.PerformanceIntents.ArpeggioIntentService` (LLM via Ollama structured outputs)
**Validator / consumer:** `GA.Business.Core.Orchestration.PerformanceIntents.PerformanceIntentValidator` (deterministic theory engine)
**Schema:** [`performance-intent.schema.json`](performance-intent.schema.json)

---

## 1. Why This Contract Exists

[#567](https://github.com/GuitarAlchemist/ga/issues/567) documented the chatbot giving structurally
wrong music advice: an `Amm7` chord symbol produced by string concatenation, and key-blind degree
mapping that put a natural note against a borrowed chord's accidental. That bug class recurs as long
as free-form LLM text *is* the theory.

This contract makes the LLM a **probabilistic sampler proposing a typed intent**, and lets the
deterministic theory engine own the truth. The model emits a `PerformanceIntent`; the validator
parses every symbol with the strict `Chord.TryFromSymbol` and checks every chord's diatonicity
against the key. Anything that fails is refused deterministically — never rendered as a
plausible-looking answer.

## 2. Shape

A `PerformanceIntent` has four fields:

| Field | Type | Notes |
|-------|------|-------|
| `chord` | string | The chord the advice is primarily about (e.g. `Am`). |
| `key` | string | Tonal centre, `<root> major` or `<root> minor` (e.g. `C major`). |
| `degrees[]` | array | `{ chord, roman }` — scale-degree analysis per chord. |
| `suggested_arpeggios[]` | array | `{ chord, arpeggio, mode }` — the arpeggio symbol to play and the fitting mode. |

`mode` is constrained to the repo's real diatonic + common-jazz mode vocabulary (see the schema
`enum`). Chord/arpeggio symbols are only pattern-checked for a valid root by the schema — a regex
cannot know that `Amm7` is not a real chord, so the theory engine validates the full symbol.

## 3. Validation Rules (the ground truth)

1. **Well-formed symbols.** Every `chord` and `arpeggio` must parse via `Chord.TryFromSymbol`
   (strict — throws on unrecognised suffixes). Rejects `Amm7`.
2. **Arpeggio rooted on the chord.** `arpeggio` root pitch class must equal `chord` root pitch class.
3. **Key-aware degrees.** Every pitch class of `chord` must belong to `key`'s diatonic scale.
   A borrowed/secondary chord (e.g. an A major chord in C major) is flagged, not forced onto a
   diatonic mode. Rejects the key-blind #567 case.

Failure → deterministic "cannot answer" listing the problems. Never a free-text fallback.

## 4. Scope

Tracer only: one answer type (arpeggio suggestions), one producer/validator pair, wired end-to-end.
Schema coverage for other answer types is deliberately out of scope until this tracer proves out
(per #589: over-constraining everything costs reasoning quality — constrain the final artifact only).
