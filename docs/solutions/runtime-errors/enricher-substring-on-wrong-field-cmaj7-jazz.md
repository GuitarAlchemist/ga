---
title: "VoicingTagEnricher never tagged Cmaj7 as jazz — substring match on wrong field"
category: runtime-errors
date: 2026-04-19
component: GA.Domain.Services/Fretboard/Voicings/Analysis
tags: [enricher, symbolic-tag, optic-k, voicing-search, structured-fields, chord-recognition]
severity: silent-correctness-bug
symptom-phrases:
  - "tag bit never fires"
  - "jazz tag query ties bare chord query"
  - "Cmaj7 jazz scores same as Cmaj7"
  - "style tag has no effect on ranking"
  - "enricher tests pass but production wrong"
---

# `VoicingTagEnricher` never tagged Cmaj7 as jazz — substring match on wrong field

## Symptom

Live MCP probe showed `"Cmaj7 jazz"` and bare `"Cmaj7"` both scored `0.5333` — identical. The `jazz` tag query-side component was live (telemetry confirmed `tags: ["jazz"]`), but it produced zero score boost. Bit math said a successful tag landing should add ~0.1. The tag bit on top-ranked corpus voicings was zero.

Tests? All 18 `VoicingTagEnricherTests` green.

## Root cause

`CanonicalChordPatternCatalog` splits chord identity across **two structured fields**:

```csharp
new("major-7",    "major",    "7th", [], [0, 4, 7, 11],  5)
//   Name         Quality     Extension           ^ pattern-catalog columns
```

So for a `Cmaj7` voicing, `ChordIdentification` gets populated with:

```csharp
Quality   = "major"
Extension = "7th"
```

The enricher was matching on `Quality` alone:

```csharp
var q = (c.ChordId.Quality ?? "").ToLowerInvariant();
var isExtended = q.Contains("7") || q.Contains("maj7") || q.Contains("m7") || …;
```

`"major".Contains("7")` → **false**. The 7 information wasn't in `Quality` — it lived in the sibling `Extension` field.

**Net**: every block-chord Cmaj7 voicing in the 313k corpus was encoded with a zero jazz-bit, regardless of how jazzy it actually was.

### Why tests lied

`MakeCharacteristics(quality: "maj7")` — the test helper accepted a compound string for the `Quality` field. That input never occurs in the real pipeline (`CanonicalChordRecognizer` only ever emits family names like `"major"` / `"dominant"` / `"diminished"`). The tests were validating a predicate against data shapes the production pipeline never produces.

## Fix

Two parts — the predicate logic, and the test inputs.

### 1. Structured predicate property on `ChordIdentification`

Added to `Common/GA.Business.Core/Analysis/Voicings/ChordIdentification.cs`:

```csharp
public bool HasSeventhOrBeyond =>
    Extension is ChordExtensions.Seventh
              or ChordExtensions.Ninth
              or ChordExtensions.Eleventh
              or ChordExtensions.Thirteenth;
```

With a named constants class co-located with the field it describes:

```csharp
public static class ChordExtensions
{
    public const string Triad      = "triad";
    public const string Sixth      = "6th";
    public const string Seventh    = "7th";
    public const string Ninth      = "9th";
    public const string Eleventh   = "11th";
    public const string Thirteenth = "13th";
    public const string Add        = "add";
}
```

### 2. Enricher collapses to a single property read

`Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingTagEnricher.cs`:

```csharp
// Before — substring OR-chain that never matched real data:
var isExtended =
    q.Contains("maj7") || q.Contains("maj9") || q.Contains("m7") ||
    q.Contains("7")    || q.Contains("9")    || q.Contains("11") || …;

// After:
var isExtended = c.ChordId.HasSeventhOrBeyond;
```

Legacy fallback deleted — it existed only to keep the lying tests green.

### 3. Tests pin the real data shape

`MakeCharacteristics` gained an `extension:` parameter; every existing test that used compound strings got rewritten:

```csharp
// Before:
MakeCharacteristics(quality: "maj7", …)   // Quality field is lying

// After:
MakeCharacteristics(quality: "major", extension: "7th", …)   // Matches CanonicalChordRecognizer output
```

Plus a new regression test pinned to production output shapes:

```csharp
[Test]
public void Jazz_FiresForCanonicalRecognizerOutput_Maj7()
{
    var canonicalMaj7 = VoicingTagEnricher.Enrich(
        MakeCharacteristics(quality: "major", extension: "7th", consonance: 0.65),
        [60, 64, 67, 71]).ToList();
    Assert.That(canonicalMaj7, Does.Contain("jazz"),
        "Quality='major' + Extension='7th' (real recognizer output for Cmaj7) should tag jazz.");
}
```

### 4. Corpus rebuild

Fixing the enricher only affects NEW embeddings. The 313k on-disk voicings were baked with the broken predicate. Full `FretboardVoicingsCLI --export-embeddings` rerun — ~140 s for 313k voicings on a dev machine. Same schema hash / count; only SYMBOLIC bit density changed.

## Verification

Post-rebuild live probe:

| Query | Before | After |
|---|---|---|
| `Cmaj7` | 0.5333 | 0.5333 (baseline — no style bits in query) |
| `Cmaj7 jazz` | 0.5333 (tied bare) | **0.578** (+0.045) |

The "jazz query > bare query" ordering is now correct.

## Prevention

**Pattern to carry forward**: *When a domain model deliberately splits a concept across structured fields, downstream code reads the fields — not a sibling string.*

- Add a named helper property (`HasX`, `IsY`) on the owning record so consumers don't duplicate the predicate at every call site.
- When the predicate is backed by a small vocabulary (like `Extension ∈ {triad, 7th, 9th, …}`), introduce named constants co-located with the field. No magic strings.
- Tests must mirror the REAL producer's output shape. Run the producer on one representative input, inspect the output, build the test helper to match. Convenience-string test inputs that never occur in production are the highest-risk anti-pattern — they camouflage bugs the production pipeline exposes.
- When a bug surfaces despite green tests, the first question is: *do the test inputs look like anything the production pipeline actually emits?* If not, that's the bug.

## Cross-reference

- Memory pattern: `feedback_structured_fields_over_substring.md` — the reusable rule.
- Memory pattern: `feedback_tests_must_use_real_data_shapes.md` — the reusable QA rule.
- Ship commit: ga `a830dea4` (fix + tests), ga `296b754` (prior corpus state this assumed), FretboardVoicingsCLI rebuild log at `/tmp/optick-rebuild-2026-04-19-jazz-fix.log`.
- Related runbook: `ga/.claude/skills/optic-k-rebuild/SKILL.md` — required after any enricher / encoder / schema change.
