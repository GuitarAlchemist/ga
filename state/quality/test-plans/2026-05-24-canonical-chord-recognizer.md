---
title: CanonicalChordRecognizer High-Value Test Plan
target: Common/GA.Domain.Services/Chords/CanonicalChordRecognizer.cs
status: draft
generated_at: 2026-05-24T19:30:00Z
generator: claude-opus-4-7
business_value_confidence: 0.95
effort_tshirt: M
---

# CanonicalChordRecognizer High-Value Test Plan

`CanonicalChordRecognizer` owns **voicing-to-name resolution** for the
entire system — every diagram in the OPTIC-K index, every chatbot answer,
every Prime Radiant chord label flows through `Identify(PitchClassSet, bass?)`.
Annotated `@ai:business-value conf=0.95`.

## Coverage gap summary

`Tests/Common/GA.Business.Core.Tests/Voicings/ChordRecognitionRoundTripTests.cs`
is excellent at the **catalog level** (round-trip every pattern → PC-set → name)
but does not pin the **structural branches** of `Identify`:

- **Unison branch** (`pcs.Length == 1`) — `"C (unison)"` shape.
- **Dyad branch** (`pcs.Length == 2`):
  - Power-chord detection (interval 7 vs 5 — comment block warns about
    swap-before-check bug, but no test pins it).
  - Canonical-orientation logic (smaller interval wins so `{C,E}` → "Major 3rd"
    not "Minor 6th").
  - All 13 interval names round-tripped.
  - `IsNaturallyOccurring` flag for IC3/IC4 only.
- **Empty PC-set** (`pcs.Length == 0`) — `"(empty)"` defensive fallback.
- **Bass-note ranking invariance** (Invariant #33, called out in code:
  "the bass note must NOT influence which pattern wins"). The round-trip
  tests assert PC-set → name; they do **not** assert PC-set + varying-bass →
  same-name. The current memory note
  `reference/feedback_recognizer_bass_not_in_ranking.md` is the only guard.
- **`maxMissing=1` / `maxExtra=1` slack** — at least one test per direction
  (no-5 voicing matches; add-chord doesn't drift into a sibling pattern).
- **`FallbackFromForte`** for sets that match no pattern.

## Test cases (10 proposed)

| # | Name | Type | What it covers | Fixtures / mocks | Overlaps |
|---|---|---|---|---|---|
| 1 | `Identify_EmptyPcSet_ReturnsEmptySentinel` | unit | `pcs.Length == 0` → `CanonicalName == "(empty)"`, `MatchDistance == -1`, `IsNaturallyOccurring == false`. | `PitchClassSet.Empty`. | none. |
| 2 | `Identify_Unison_ReturnsNoteUnison` | unit | Single PC `{C}` → `"C (unison)"`, Root `"C"`, Quality `"unison"`. | parametric over all 12 PCs. | none. |
| 3 | `Identify_PowerChord_P5Direction_OrientsCorrectly` | unit | `{C, G}` (interval 7) → `"C5"`; `{F, C}` (interval 5 from F) → `"C5"` (root is the upper note). Pins the swap-before-check ordering bug the code comments warn about. | parametric. | none. |
| 4 | `Identify_Dyad_PicksSmallerIntervalCanonical` | unit | `{C, E}` (any direction) → "Major 3rd"; `{C, A}` → "Minor 3rd" (not "Major 6th"). | parametric over all 13 intervals. | none. |
| 5 | `Identify_Dyad_IsNaturallyOccurring_OnlyForThirds` | unit | `IsNaturallyOccurring == true` for IC3/IC4 only; false for IC1/2/5/6/7. | parametric. | partial: catalog tests don't reach dyads. |
| 6 | `Identify_BassNote_NeverChangesPatternRanking` | unit | For a curated set of voicings, varying `bassNote` across every PC in the set produces the same `PatternName` and `CanonicalName`. Pin Invariant #33. | curated list of 12 ambiguous voicings (Am7 vs C6, etc.). | indirect: round-trip tests don't pass a bass. |
| 7 | `Identify_BassNote_SetsSlashSuffix` | unit | `{C, E, G}` with `bassNote=E` → `SlashSuffix == "/E"`; with `bassNote=C` (root) → `SlashSuffix == null`. | parametric. | none. |
| 8 | `Identify_NoFifth_StillMatches_Within_MaxMissing` | unit | `{C, E, B}` matches `Cmaj7` (no-5 voicing) with `MatchDistance == 1`. | parametric over common no-5 voicings. | none. |
| 9 | `Identify_OneExtraNote_StillMatches_Within_MaxExtra` | unit | `{C, E, G, D}` resolves to `Cadd9` (or equivalent named pattern) rather than falling through to Forte fallback. | parametric. | partial: catalog tests don't probe `maxExtra=1`. |
| 10 | `Identify_UnknownPcSet_ReturnsFortFallback` | unit | A pathological set with no matching pattern (e.g. `{0,1,2}` cluster) returns a non-null `CanonicalChordResult` from `FallbackFromForte` (catalog name with Forte prefix). | parametric over 3 cluster sets. | none. |

## Suggested file locations

- `Tests/Common/GA.Business.Core.Tests/Voicings/CanonicalChordRecognizerBranchTests.cs`
  (all 10 cases — sits alongside the existing round-trip suite, focused on the
  branch-coverage gap rather than catalog coverage).

## Effort estimate

**M** (medium). Pure domain code, no DI, no IO, fast tests. The fixture data
(curated voicings for case #6, cluster sets for #10) is the only setup cost.
Estimate 1.5–2 dev-days.

## Rubric

Cases #3 and #6 pin invariants the code comments explicitly call out as
fragile — they are the single highest-priority additions. The round-trip
suite already proves catalog correctness; this plan proves the **dispatch
logic around the catalog** stays correct under refactor.
