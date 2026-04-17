# Fix Voicing Analysis Quality — Remediation Plan

**Status:** Active
**Date:** 2026-04-17
**Owner:** spareilleux

## Problem statement

The corpus audit (`state/audit/voicing-audit-2026-04-17.json`) revealed that while the structural layer of voicing analysis is production-quality (100% Forte coverage, 0 null ChordName), the *labeling* layer has serious quality defects:

| Metric | Value |
|---|---:|
| Total voicings | 313,047 |
| `ChordName == "Unknown"` | 31.55% |
| Cross-instrument label consistency (same PC-set → same name) | **29.38%** |
| 2-note voicings mislabeled as triads | 1,655 (~29% of 2-note voicings) |
| Forte catalog coverage | 100% ✓ |
| Null HarmonicFunction | 0% ✓ |

This matters because labels flow into OPTIC-K embedding via:
- `SemanticTags` → SYMBOLIC partition (0.10 weight)
- `ChordName` string → used by `TheoryVectorService` indirectly
- `HarmonicFunction` → CONTEXT partition (0.20 weight)

Wrong labels → noisy embeddings → wrong search results.

## Root causes

### Cause A — degenerate chord templates

`ChordTemplateFactory.BuildChordTemplate` (`ChordTemplateFactory.cs` ~line 420) composes template names from `(parentMode, degree, extension, stackingSuffix)` — e.g. `"Augmented Degree2 Triad (4ths)"`. When the extension is `Triad` (3 notes) but the stacking collapses two of those notes to the same pitch class (common under Quartal/Quintal stacking in certain modes), the *declared* name still says "Triad" but the actual PC-set has cardinality 2.

The matcher in `VoicingHarmonicAnalyzer.IdentifyChord` finds these degenerate templates when looking up a 2-note voicing's PC-set ID and assigns them as the chord name.

**Example**: voicing `x-1-0-x-x-x` (C+G perfect fifth) → PC-set `{0, 7}` → matches template "Augmented Degree2 Triad (4ths)" that also has PC-set `{0, 7}` because its 3 declared intervals collapsed during stacking.

### Cause B — register-dependent matcher tie-breaking

`IdentifyChord` ranks candidate templates by complexity, then by `bassNote.Value == match.Root.Value` (root-position preferred). When the same PC-set appears with different bass notes across instruments (guitar low E vs ukulele high A), different roots win, yielding different degree labels.

**Example**: PC-set `{0, 7}` on guitar with bass=G → matched as "Aug Deg2 (C root)". On ukulele with bass=C → matched as "Aug Deg1 (G root)". Same PC-set, different labels.

### Cause C — narrow ChordName catalog

When no template matches (which happens for dyads and unusual sets not in the factory's enumeration), `IdentifyChord` returns `AnalysisConstants.Unknown`. The factory currently enumerates templates only for Major / Harmonic Minor / Melodic Minor / Natural Minor / Whole Tone / Diminished / Augmented / Major Pentatonic / Hirajoshi / InSen — anything outside these parent modes falls through.

### Cause D — data duplicate in Modes.yaml

`Modes.yaml` has both "Diminished Family" and "Diminished (Octatonic) Family" pointing to the same PC-sets. The atonal-modal-families bridge surfaced this as an orphan family with no matching atonal counterpart.

## Sprint 1 — high-ROI surgical fixes (target: 1-2 days)

### Fix 1: Reject degenerate template matches

Location: `Common/GA.Domain.Services/Chords/ChordTemplateFactory.cs` — `BuildChordTemplate` (around line 420).

After computing `intervals`, check:
```csharp
var distinctPcCount = intervals.Select(i => i.Interval.Semitones.Value % 12).Distinct().Count();
var expectedCount = extension switch {
  ChordExtension.Triad => 3,
  ChordExtension.Seventh => 4,
  ChordExtension.Ninth => 5,
  _ => intervals.Count
};
if (distinctPcCount < expectedCount) {
  return null; // skip degenerate template, will be filtered by caller
}
```

Update `GenerateAllPossibleChords` to filter `null` results. This prevents degenerate templates from entering the matcher cache.

Expected impact: the 1,655 "2-note-as-triad" labels collapse to `Unknown` (they'll be caught in Fix 3).

### Fix 2: Strip register-dependent slash notation from PC-set-level label

Location: `Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingHarmonicAnalyzer.cs` — `IdentifyChord` (lines 120-126).

Problem: currently appends `/{bassNote}` to the chord name, making PC-set-level label register-dependent.

Fix: keep the slash notation but export TWO fields on `ChordIdentification`:
- `PitchClassSetName`: the root-position name ("C Major") — invariant across voicings
- `ChordName`: the full slash-chord name ("C Major/G") — voicing-specific

Then:
- OPTIC-K SYMBOLIC partition reads `PitchClassSetName` (invariant → clusters correctly across instruments)
- Display layer reads `ChordName` (register-specific, shown to user)

Alternative simpler fix: Make the degree-tie-breaker deterministic by ordering by PC value (lowest-numbered root wins), not by whether it matches bass. This eliminates the cross-instrument disagreement at the cost of some slash-chord accuracy.

### Fix 3: Dyad fallback naming

Location: `VoicingHarmonicAnalyzer.IdentifyChord` (around line 90 "Fallback" branch).

Before returning `AnalysisConstants.Unknown`, if the PC-set has cardinality 2, compute the interval between the two pitch classes and return:
```csharp
var interval = (pc2 - pc1 + 12) % 12;
var intervalName = interval switch {
  1 => "Minor 2nd", 2 => "Major 2nd", 3 => "Minor 3rd", 4 => "Major 3rd",
  5 => "Perfect 4th", 6 => "Tritone", 7 => "Perfect 5th",
  8 => "Minor 6th", 9 => "Major 6th", 10 => "Minor 7th", 11 => "Major 7th",
  _ => "Unison"
};
chordName = $"{GetNoteName(pc1)}+{GetNoteName(pc2)} ({intervalName})";
```

Expected impact: the 31.55% "Unknown" rate drops significantly; 2-note voicings get musically meaningful labels.

### Fix 4: Delete duplicate "Diminished (Octatonic) Family" from Modes.yaml

Location: `Common/GA.Business.Config/Modes.yaml` — remove the redundant family entry. Its PC-sets are already covered by "Diminished Family".

One-line YAML delete.

## Sprint 1 acceptance criteria

After all four fixes land, re-run `Demos/VoicingAnalysisAudit`:

- [ ] `ChordName == "Unknown"` rate **< 15%** (down from 31.55%)
- [ ] Cross-instrument consistency **> 85%** (up from 29.38%)
- [ ] `2-note voicings labeled as Triad` count = **0** (down from 1,655)
- [ ] Forte coverage still 100% (regression check)
- [ ] No null HarmonicFunction introduced
- [ ] All existing NUnit tests still pass
- [ ] `dotnet build AllProjects.slnx` → 0 errors

## Sprint 2 — medium-ROI enhancements (target: 2-3 days, only if Sprint 1 is insufficient)

1. **Extend ChordName catalog for non-diatonic voicings** — add Byzantine, Double Harmonic, Hungarian Minor, etc. to the parent-mode enumeration in `ChordTemplateFactory.GenerateAllPossibleChords`.
2. **Add `HarmonicFunctionContext` string field** on `ChordVoicingRagDocument` — bypasses enum extension; lets `ContextVectorService` consume "modal-interchange" / "secondary-dominant" tags directly.
3. **Fine-grained SemanticTags** — emit "altered-dominant", "tritone-sub", "quartal-voicing" as explicit tags when detected, feeding SYMBOLIC partition.

## Sprint 3 — structural follow-ups (unblock later)

4. **Enum extensions** (`ChordQuality` altered-dominant family, `HarmonicFunction` modal-interchange markers) — larger codebase changes; defer until Sprints 1-2 prove insufficient.
5. **Enharmonic spelling pass** — use key context to choose sharp vs flat chord spelling.

## Ranking acceptance tests (cross-cutting, add after Sprint 1)

Create `Tests/GA.Business.Core.Tests/Voicings/VoicingRetrievalAcceptanceTests.cs` with 20 golden queries:

```csharp
[Test]
public void Query_Cmaj7_Drop2_Guitar_ReturnsRealDrop2Voicings()
{
    var query = embeddingOf("Cmaj7 drop-2 guitar");
    var results = optickIndex.Search(query, "guitar", topK: 20);

    Assert.That(results.Count(r => hasPitchClasses(r, [0,4,7,11])), Is.GreaterThan(15));
    Assert.That(results.Count(r => isDrop2(r.Diagram)), Is.GreaterThan(10));
    Assert.That(results[0].Score, Is.GreaterThan(0.8));
}
```

These validate the end-to-end pipeline (analyzer → embedding → mmap index → search), not just unit-level correctness.

## Files changed (Sprint 1)

| File | Change |
|---|---|
| `Common/GA.Domain.Services/Chords/ChordTemplateFactory.cs` | Fix 1: reject degenerate templates |
| `Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingHarmonicAnalyzer.cs` | Fix 2: separate PitchClassSetName from ChordName; Fix 3: dyad fallback |
| `Common/Business.Core/Analysis/Voicings/ChordIdentification.cs` | Add `PitchClassSetName` field |
| `Common/GA.Business.ML/Rag/VoicingDocumentFactory.cs` | Consume new `PitchClassSetName` field in document |
| `Common/GA.Business.Config/Modes.yaml` | Fix 4: remove duplicate family |

## Risk assessment

- **Fix 1** could cause regression in corpus count if template generation relied on degenerate templates → verify corpus dedup still produces 313k voicings.
- **Fix 2** is the most invasive change (touches `ChordIdentification` struct) — requires downstream consumers updating. But the new field is purely additive.
- **Fix 3** is self-contained — only affects the Unknown fallback path.
- **Fix 4** is a data-only change, zero code risk.

## Rollback procedure

All changes are in code + one YAML file. `git revert` on the commit suffices. No database migrations, no schema changes to the binary index format.
