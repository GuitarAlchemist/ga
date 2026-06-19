---
title: Chord Domain Model High-Value Test Plan
target: Common/GA.Domain.Core/Theory/Harmony/Chord.cs
status: draft
generated_at: 2026-05-24T19:30:00Z
generator: claude-opus-4-7
business_value_confidence: 0.90
effort_tshirt: S
---

# Chord Domain Model High-Value Test Plan

`Chord` is the **foundational domain entity** for harmony — `Root`,
`Notes`, `Formula`, `Symbol`, `Quality`, `Extension`, `PitchClassSet`,
`Equals`, `GetInversion`, `ToInversion`. Every higher layer (recognizers,
voicing services, MCP tools, F# scale solver, React renderers) consumes
this type. Annotated `@ai:business-value conf=0.90`.

## Coverage gap summary

`Tests/GA.Domain.Core.Tests/Theory/Harmony/ChordTests.cs` covers some
constructor cases (rooted + formula, notes-based analysis) but is shallow
on:

- **`GenerateSymbol`** — only triad cases tested; not exercised for
  `add9`, `sus2`, `sus4`, `6`, `dim`, `aug`, or accidentals on the root
  (`F#m7`, `Bbmaj9`). Symbol generation has a known scope (no slash, no
  alterations like `b9`/`#11`); pin what *is* and *is not* produced.
- **`DetermineExtension` decision order** — `Thirteenth > Eleventh >
  (Ninth+Seventh) > Add9 > Seventh > Triad`. Decision order matters; no
  test asserts a 7-chord with a 13 reports `Thirteenth` (not `Seventh`).
- **`DetermineQuality`** edge cases — `{Min3 + Aug5}`, `{Maj3 + Dim5}`,
  power chord (no third) → `ChordQuality.Other` (Triad falls through).
- **`Equals` / `GetHashCode` contract** — `(PitchClassSet, Root)` is the
  identity. Two `Cmaj7` instances built from different overloads must be
  equal *and* have equal hashes.
- **`IsInverted` + `Bass`** — `Notes[0] != Root` only fires after a
  `ToInversion` call; no test pins this.
- **`GetInversion` round-trip** — `chord.ToInversion(2).GetInversion() == 2`
  for every chord size 2..7.
- **`ToInversion` argument validation** — negative + out-of-range throw.
- **Constructor invariant** — `notes.Count < 2` throws `ArgumentException`.

## Test cases (9 proposed)

| # | Name | Type | What it covers | Fixtures / mocks | Overlaps |
|---|---|---|---|---|---|
| 1 | `GenerateSymbol_CoversAllExtensionEnums` | unit | One test per `ChordExtension` value (`Triad`, `Seventh`, `Ninth`, `Eleventh`, `Thirteenth`, `Add9`, `Sus2`, `Sus4`, `Sixth`) → symbol contains the expected suffix. | parametric. | partial: `ChordTests` covers `Triad` only. |
| 2 | `GenerateSymbol_HandlesAccidentalRoots` | unit | `F#m7` and `Bbmaj9` symbols generated correctly from `Accidented` roots. | parametric. | none. |
| 3 | `DetermineExtension_DecisionOrder_PrefersHigherExtension` | unit | A formula with `7 + 9 + 11 + 13` reports `Thirteenth`, not lower; explicitly pins the if-ladder order. | parametric over 4 nested cases. | none. |
| 4 | `DetermineQuality_EdgeCases` | unit | `{min3,aug5}` → `Other`; `{maj3,dim5}` → `Other`; power chord (`{P5}`) → `Other`. | parametric. | none. |
| 5 | `Equals_TwoCmaj7_FromDifferentConstructors_AreEqual` | unit | `new Chord(C, Cmaj7Formula)` and `new Chord(noteCollection)` with same PCs+root are `.Equals` and share `GetHashCode`. | direct construction. | none. |
| 6 | `IsInverted_FalseAtRootPosition_TrueAfterToInversion` | unit | Fresh chord has `IsInverted == false` and `Bass == Root`; after `ToInversion(1)`, `IsInverted == true` and `Bass == Notes[0]`. | parametric over a 4-note chord. | none. |
| 7 | `GetInversion_RoundTrips_ForEachInversionIndex` | unit | For chord sizes 3, 4, 5: `chord.ToInversion(i).GetInversion() == i` for `i in 0..size-1`. | parametric. | none. |
| 8 | `ToInversion_OutOfRange_Throws` | unit | `inversion = -1` and `inversion = Notes.Count` throw `ArgumentOutOfRangeException`. | parametric. | none. |
| 9 | `Ctor_NotesLessThan2_Throws` | unit | `new Chord(new AccidentedNoteCollection([C]))` throws `ArgumentException`. | direct. | none. |

## Suggested file locations

- Extend `Tests/GA.Domain.Core.Tests/Theory/Harmony/ChordTests.cs` directly
  — all 9 cases follow the existing AAA pattern.

## Effort estimate

**S** (small). Pure value-object tests, no infrastructure, fast. The fixture
table for case #1 is the longest part. Estimate 0.5–1 dev-day.

## Rubric

These tests are **cheap insurance** — `Chord` is consumed by ~50 downstream
files. A symbol-generator regression that silently emits `"Cmaj7"` as `"C7"`
would break every chatbot answer with no localized test failure. Worth a half-day
for permanent confidence.
