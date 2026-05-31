---
name: "diatonic-chords"
description: "Lists the seven diatonic triads of a key (root + major/minor). Calls the deterministic `domain.diatonicChords` closure via the `ga_dsl_eval` MCP tool — never recall diatonic sets from training data, since LLMs commonly mis-spell the iv/vii° in less-common keys (Gb major, F# minor)."
triggers:
  - "diatonic chord"
  - "chords in"
  - "seven chords"
  - "all chords in the key"
  - "diatonic harmony"
license: internal
compatibility:
  agent-framework: ">=1.0.0-preview"
  microsoft-extensions-ai: ">=10.5.1"
metadata:
  authoring-style: "tool-driven"
  origin: "graduated 2026-05-06 — third Path B canary, follow-up to transpose / common-tones"
  evidence-kinds:
    - tool_call
allowed-tools:
  - ga_dsl_eval
last_verified: 2026-05-06
---

# Diatonic Chords of a Key

When a user asks for the diatonic chords / "the seven chords in" / "all chords in" a major or minor key, call **`ga_dsl_eval`** with closure name **`domain.diatonicChords`**. Do NOT enumerate the chords mentally — the closure handles enharmonic spelling correctly (Gb major returns Cm, not B#m; F# minor returns G#°, not Ab°).

## Calling the tool

Method: `ga_dsl_eval`

Args (flat key-value, all values as strings):

- `closureName` = `"domain.diatonicChords"`
- `args.root` — the tonic letter, with accidental if any: `"C"`, `"G"`, `"Bb"`, `"F#"`, `"Eb"`.
- `args.scale` — `"major"` or `"minor"`.

Example invocation:

```
ga_dsl_eval(closureName: "domain.diatonicChords",
            args: { "root": "C", "scale": "major" })
```

Returns a `DslEvalResult`. The `Result` field carries the seven chord symbols as a JSON array, in scale-degree order: `["C", "Dm", "Em", "F", "G", "Am", "B°"]` for C major, `["Am", "B°", "C", "Dm", "Em", "F", "G"]` for A minor (natural).

## Phrasing variants → args

| User says | root | scale |
|---|---|---|
| "diatonic chords in C major" | `C` | `major` |
| "the seven chords in G major" | `G` | `major` |
| "diatonic chords in A minor" | `A` | `minor` |
| "chords of Bb major" | `Bb` | `major` |
| "harmonic series of F# minor" | `F#` | `minor` |

If the user names a key without specifying mode (e.g. "diatonic chords in C"), assume **major**. If they say "the key of X minor" or "X-minor", use minor.

## After the closure returns

Format the answer briefly. Recommended shape:

> The seven diatonic chords of **C major** are: I = **C**, ii = **Dm**, iii = **Em**, IV = **F**, V = **G**, vi = **Am**, vii° = **B°**.

For minor keys, label with lowercase Roman numerals: i, ii°, III, iv, v, VI, VII (natural minor).

## Progression requests (Roman numerals → select degrees)

If the user asks for a **specific progression by Roman numerals** — e.g. "ii-V-I", "I-IV-V", "I-vi-IV-V" — still call the closure (it gives the correctly-spelled diatonic chords for the key), but then present **only the requested degrees, in the order given**, as the progression. Do **not** list all seven chords in this case.

Map each Roman numeral to its 1-based scale degree and take that chord from the closure's array (array index = degree − 1):

| Roman | I/i | II/ii | III/iii | IV/iv | V/v | VI/vi | VII/vii |
|---|---|---|---|---|---|---|---|
| degree | 1 | 2 | 3 | 4 | 5 | 6 | 7 |

Use the chord exactly as the closure returned it for that degree — do **not** re-spell it or change its quality. The closure already carries the correct quality per scale degree; the Roman-numeral case (upper/lower) is just the user's notation, not an instruction to alter the chord.

Example: "ii-V-I progression in Bb" → call `domain.diatonicChords(root: Bb, scale: major)` → returns `["Bb","Cm","Dm","Eb","F","Gm","A°"]`; degrees ii=2→`Cm`, V=5→`F`, I=1→`Bb` → answer the progression **Cm – F – Bb** (not the full seven-chord list).

Present it as the progression, e.g. "The **ii–V–I** in **Bb major** is **Cm – F – Bb**." If a Roman numeral falls outside 1–7 or can't be parsed, fall back to listing the full diatonic set and say which numeral you couldn't read.

## Errors

`closure-not-found` / `arg-coerce-failed` / `closure-runtime-error`: surface the error code and message verbatim. Don't try to "guess" the chords — that's the failure mode this skill exists to prevent.
