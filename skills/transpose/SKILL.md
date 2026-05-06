---
name: "transpose"
description: "Transposes a chord by a named musical interval (e.g. Cmaj7 up a perfect fourth = Fmaj7). Calls the deterministic `domain.transposeChord` closure via the `ga_dsl_eval` MCP tool — never recall transpositions from training data, since LLMs commonly produce wrong enharmonics for less-common keys (Db major vs C# major)."
triggers:
  - "transpose"
  - "move this chord"
  - "shift this chord"
  - "up a"
  - "down a"
  - "in the key of"
  - "change the key"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "tool-driven"
  origin: "graduated 2026-05-06 — first canary for ga_dsl_eval (Phase 2 of skills-orchestration plan)"
  evidence-kinds:
    - tool_call
allowed-tools:
  - ga_dsl_eval
last_verified: 2026-05-06
---

# Transpose a Chord

When a user asks to transpose a chord by an interval (or to move it into a different key), call **`ga_dsl_eval`** with closure name **`domain.transposeChord`**. Do NOT compute the transposition mentally — for less-common keys (Gb major, C# minor) the LLM will confidently flip enharmonics and produce wrong spellings.

## Why this skill uses `ga_dsl_eval` (not a keyhole tool)

This is the first canary for the DSL-eval pattern. Instead of one MCP tool per gap, we call the existing `domain.transposeChord` closure through the universal `ga_dsl_eval` entry point. The closure is registered in `GaClosureRegistry` and exposed only because its category is `Domain` (per `docs/contracts/2026-05-06-ga-dsl-eval-contract.md` v0.1).

## Calling the tool

Method: `ga_dsl_eval`

Args (flat key-value, all values as strings):

- `closureName` = `"domain.transposeChord"`
- `args.symbol` — the chord symbol, e.g. `"Cmaj7"`, `"F#m"`, `"Bb7"`.
- `args.semitones` — integer string. Positive = up, negative = down. Convert from interval names using the table below.

Example invocation:

```
ga_dsl_eval(closureName: "domain.transposeChord",
            args: { "symbol": "Cmaj7", "semitones": "5" })
```

Returns a `DslEvalResult`. Read `Result` for the new chord symbol (e.g. `"Fmaj7"`); on failure, read `Error.Code` and `Error.Message`.

## Interval → semitones table

Use this to convert the user's phrasing into the `semitones` arg. Direction "up" = positive; "down" = negative.

| Interval | Semitones | Common phrasings |
|---|---:|---|
| unison / P1 | 0 | "no change", "same root" |
| minor 2nd / m2 / half step | 1 | "up a half step", "by a semitone" |
| major 2nd / M2 / whole step | 2 | "up a whole step", "by two semitones" |
| minor 3rd / m3 | 3 | "up a minor third", "by three semitones" |
| major 3rd / M3 | 4 | "up a major third" |
| perfect 4th / P4 | 5 | "up a perfect fourth", "by a fourth" |
| tritone / TT / dim 5 / aug 4 | 6 | "by a tritone" |
| perfect 5th / P5 | 7 | "up a perfect fifth", "by a fifth" |
| minor 6th / m6 | 8 | "up a minor sixth" |
| major 6th / M6 | 9 | "up a major sixth" |
| minor 7th / m7 | 10 | "up a minor seventh" |
| major 7th / M7 | 11 | "up a major seventh" |
| octave / P8 | 12 | "up an octave" |

Direction `"down"` means negative semitones. *"Down a minor third"* → `semitones = -3`.

## Mapping user phrasings

- *"Transpose Cmaj7 up a perfect fourth"* → `args = { "symbol": "Cmaj7", "semitones": "5" }`
- *"Move this F chord down a minor third"* → `args = { "symbol": "F", "semitones": "-3" }`
- *"What's Dm7 up a whole step?"* → `args = { "symbol": "Dm7", "semitones": "2" }`
- *"Cmaj7 in the key of G"* — G is up a perfect fifth from C, so `args = { "symbol": "Cmaj7", "semitones": "7" }`. If ambiguous (could go down), surface the assumption.

## Phrasing the answer

Lead with the result. Cite the interval and direction so the user can verify:

> **Cmaj7 up a perfect fourth = Fmaj7** (interval = +5 semitones).

If the closure returns an error (`Error` non-null), surface the message verbatim and ask the user to clarify the chord name. Common cases:

- `closure-runtime-error` with a `ParseError` — chord symbol couldn't be parsed (typo, unsupported quality, etc.)
- `arg-coerce-failed` on `semitones` — should not happen if you wrote a valid integer string

## When to refuse / clarify

- *"Transpose this whole progression"* — call this skill once per chord, OR ask the user to list the chords. Bulk-transposition is roadmap.
- Keys named ambiguously (*"in C"* — major or minor?) — pick the same mode as the source chord, OR ask.
- Slash chords (e.g. `C/E` up a fifth) — the closure may not preserve the bass note correctly; warn the user that the bass note in the answer may need verification.

## Out of scope

- **Whole-progression transposition** — separate call per chord; the closure is single-chord.
- **Capo translation** — separate skill (BACKLOG #139 gap, not yet shipped).
- **Tuning-aware transposition** (DADGAD, drop-D shape adjustments) — separate skill.

## Cross-reference

- MCP tool surface: `ga_dsl_eval` in `Common/GA.Business.ML/Agents/Mcp/DslEvalMcpTools.cs`
- Closure: `domain.transposeChord` in `Common/GA.Business.DSL/Closures/BuiltinClosures/DomainClosures.fs`
- Contract: `docs/contracts/2026-05-06-ga-dsl-eval-contract.md` (v0.1)
- Plan: `docs/plans/2026-05-06-skills-orchestration-architecture.md` (Phase 2 canary)
