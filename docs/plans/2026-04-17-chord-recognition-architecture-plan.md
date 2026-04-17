# Chord Recognition Architecture — Refactor Plan

**Status:** Active — supersedes 2026-04-17-fix-voicing-analysis-quality-plan.md Sprint 1
**Date:** 2026-04-17
**Owner:** spareilleux

## Why this supersedes the previous Sprint 1

The earlier Sprint 1 plan proposed tactical fixes: reject degenerate templates, strip slash notation, dyad fallback. These would improve the 29% cross-instrument consistency → ~85%, but they patch a broken architecture rather than fix it.

Deep analysis reveals **two conflations** in the current code:

### Conflation 1 — template generation mixes enumeration with naming

`ChordTemplateFactory.GenerateAllPossibleChords` enumerates via `(11 parent modes) × (7 degrees) × (5 extensions) × (4 stackings) ≈ 1500 templates`, then matches by PC-set. Multiple derivation patterns that produce the same PC-set compete for recognition; "Augmented Degree2 Triad (4ths)" exists as a template even when its 3 declared intervals collapse to 2 distinct pitch classes.

### Conflation 2 — `ChordIdentification` fuses invariant and variant properties

Today `ChordName = "C Major/E"` combines:
- *Invariant*: the chord identity (`C Major` — same PC-set on any instrument, any voicing)
- *Variant*: the inversion label (`/E` — depends on which note is bass)

When this string flows into SYMBOLIC embedding dims, the register-dependent `/E` leaks and destroys cross-instrument consistency.

## First-principles architecture

A chord identity is a function of `(root, quality, extension, alterations)`. Not of bass. Not of instrument.

Recognition should be:
1. Compute PC-set (already correct).
2. **Canonical root by minimal-alteration principle** — for each candidate root, compute intervals-from-root; root requiring fewest alterations to match a canonical pattern wins.
3. **Match against a curated pattern catalog** (~80 entries, not 1500).
4. Return structured `ChordIdentification` with separate `CanonicalName` (invariant) and `SlashSuffix` (voicing-specific) fields.
5. Voicing-level properties (bass, inversion, drop-N) are computed *on top of* the chord identity, not baked into it.

## Phased implementation

### Phase A — curated chord pattern catalog

Location: `Common/GA.Domain.Core/Theory/Harmony/CanonicalChordPatternCatalog.cs` (new).

~80 entries covering:

| Category | Count | Examples |
|---|---:|---|
| Triads | 5 | major, minor, dim, aug, sus2, sus4 |
| Seventh chords | 8 | maj7, 7, m7, m(maj7), m7b5, dim7, sus4-7, aug-maj7 |
| Extended chords (9/11/13) | ~20 | maj9, 9, m9, 11, 13, m11, maj13, etc. |
| Altered dominants | ~12 | 7#9, 7b9, 7#11, 7b13, alt, 7#9#11, etc. |
| Add chords | 4 | add9, madd9, add11, add4 |
| Quartal/quintal | 6 | quartal-3, quartal-4, quintal-3, etc. |
| Polychord primitives | ~10 | slash triads, hybrid chords |

Each entry:
```csharp
public readonly record struct ChordIntervalPattern(
    string Name,            // "dominant-7-sharp-9"
    string Quality,         // "altered-dominant"
    string? Extension,      // "7th"
    string[] Alterations,   // ["#9"]
    int[] Intervals,        // [0, 3, 4, 7, 10] — MUST be sorted ascending, root-relative
    int Priority);          // lower = preferred when multiple patterns match
```

Legal note: these are standard music-theory terms, not copyrightable.

### Phase B — constraint-satisfaction recognizer

Location: `Common/GA.Domain.Services/Chords/CanonicalChordRecognizer.cs` (new; replaces `VoicingHarmonicAnalyzer.IdentifyChord` for the chord-identity path).

```csharp
public ChordIdentification IdentifyChord(PitchClassSet pcSet)
{
    if (pcSet.Count == 2)
        return IdentifyDyad(pcSet);  // interval-based name, bypass pattern matching

    var candidates = new List<Candidate>();
    foreach (var root in pcSet)
    {
        var intervals = PcSetToIntervalsFromRoot(pcSet, root);
        foreach (var pattern in CanonicalChordPatternCatalog.All)
        {
            var match = pattern.TryMatch(intervals);
            if (match != null)
                candidates.Add(new Candidate(root, pattern, match));
        }
    }

    if (candidates.Count == 0)
        return IdentifyByForteNumber(pcSet);  // "6-Z50 hexachord" not "Unknown"

    return candidates
        .OrderBy(c => c.AlterationCount)     // fewest alterations wins
        .ThenBy(c => c.Pattern.Priority)     // catalog priority
        .ThenBy(c => RootCommonness(c.Root))  // C/F/G over C#/Ab (prior)
        .First()
        .ToChordIdentification();
}
```

**No more bass-dependent tie-breaking.** Same PC-set → same chord identity, on every instrument.

### Phase C — split `ChordIdentification`

Location: `Common/Business.Core/Analysis/Voicings/ChordIdentification.cs`.

```csharp
public record ChordIdentification(
    string CanonicalName,       // "C Major 7"              — INVARIANT across voicings
    string? SlashSuffix,        // "/E"                     — voicing-specific, can be null
    string Root,                // "C"
    string Quality,             // "major"
    string? Extension,          // "7th"
    string[] Alterations,       // ["#9"]
    PitchClassSet PitchClassSet,
    bool IsNaturallyOccurring,
    string? ClosestKey,
    string FunctionalDescription
);

public string DisplayName => SlashSuffix is null ? CanonicalName : $"{CanonicalName}{SlashSuffix}";
```

- `VoicingDocumentFactory` reads `CanonicalName` into SYMBOLIC embedding dims → cross-instrument invariant.
- Chatbot display reads `DisplayName` → user sees slash notation when meaningful.

### Phase D — fallbacks

**Dyad fallback** in `CanonicalChordRecognizer.IdentifyDyad`:
```csharp
var interval = (pc2 - pc1 + 12) % 12;
var intervalName = interval switch {
    1 => "Minor 2nd", 2 => "Major 2nd", 3 => "Minor 3rd", 4 => "Major 3rd",
    5 => "Perfect 4th", 6 => "Tritone", 7 => "Perfect 5th",
    8 => "Minor 6th", 9 => "Major 6th", 10 => "Minor 7th", 11 => "Major 7th",
    _ => "Unison"
};
return new ChordIdentification(
    CanonicalName: $"{GetNoteName(pc1)}+{GetNoteName(pc2)}",
    Quality: "dyad",
    ...);
```

**Forte-number fallback** for unmatched set classes:
```csharp
var forte = ProgrammaticForteCatalog.GetForteNumber(pcSet);
return new ChordIdentification(
    CanonicalName: $"Forte {forte} ({CardinalityName(pcSet.Count)})",
    Quality: "set-class",
    ...);
```

## IX diagnostics as acceptance gates

Before we refactor, **measure the current state** so we can prove improvement. After each phase, **remeasure**.

### Diagnostic 1 — cross-instrument invariance score (already built)

Re-run `VoicingAnalysisAudit`:

| Metric | Baseline (current) | Target (post-refactor) |
|---|---:|---:|
| `ChordName == "Unknown"` | 31.55% | <5% |
| Cross-instrument consistency | 29.38% | >98% |
| 2-note voicings as triads | 1,655 | 0 |

### Diagnostic 2 — `ix_supervised` leak detection

Train a classifier on `(embedding → instrument)`. Current hypothesis: STRUCTURE partition *shouldn't* predict instrument but does, because mislabeled ChordNames leak through the SymbolicVectorService.

```
BEFORE refactor:
  Classifier on STRUCTURE alone:   ~65% accuracy  (LEAK — should be ~33% baseline)
  Classifier on MORPHOLOGY alone:  ~95% accuracy  (expected)

AFTER refactor:
  Classifier on STRUCTURE alone:   ~35% accuracy  (no leak)
  Classifier on MORPHOLOGY alone:  ~95% accuracy  (unchanged)
```

Uses `ix_supervised` for training, `ix_random_forest` for feature importance ranking.

### Diagnostic 3 — `ix_kmeans` stability via Adjusted Rand Index

Cluster voicings BEFORE refactor and AFTER refactor using same k. Compare cluster assignments via ARI.

- ARI > 0.95 = structural meaning preserved (refactor is semantically neutral where it should be)
- ARI < 0.85 = refactor shifted cluster boundaries too much (investigation needed)

### Diagnostic 4 — `ix_topo` persistent homology

Per-instrument 112-dim point clouds → Betti numbers. Compare before and after:

- β₀ (connected components) convergence across instruments → good
- β₁ (loops / cycles) convergence → good
- β₂ (voids) typically 0 or low

Before refactor: Betti numbers differ across instruments (symptom of MORPHOLOGY + buggy SYMBOLIC mixing).
After refactor: Betti numbers for STRUCTURE-only projection should match across instruments (proving cross-instrument content invariance).

## Acceptance criteria (hard gates)

Refactor is accepted iff ALL of these pass:

1. ✅ Cross-instrument consistency > 98% (measured on 313k corpus)
2. ✅ `ChordName == "Unknown"` < 5%
3. ✅ 2-note voicings labeled as triads: 0
4. ✅ Forte coverage still 100%
5. ✅ All NUnit tests pass
6. ✅ Full GA solution builds with 0 errors
7. ✅ `ix_supervised` STRUCTURE-only classifier accuracy < 40% for instrument prediction (no leak)
8. ✅ `ix_kmeans` ARI between before/after ≥ 0.85

## Risk assessment

- **Phase A** (curated catalog): additive, zero risk to existing code.
- **Phase B** (recognizer): new service alongside old. Can run both in parallel, compare outputs, roll over only when Phase B matches or beats Phase A.
- **Phase C** (split ChordIdentification): adds `SlashSuffix` as nullable field. Existing consumers keep using `ChordName` as `DisplayName`. Zero breaking change if migrated carefully.
- **Phase D** (fallbacks): pure addition in the fallback path. Zero risk.

## Rollback

Each phase is a separate commit. `git revert` sufficient. No schema changes to the binary index, no database migrations.

## Effort estimate

| Phase | Effort | Parallelizable? |
|---|---:|---|
| A — Curated catalog | 1-2 days | Yes (one person writes, reviewer checks) |
| B — Recognizer | 2-3 days | Depends on A |
| C — Split struct | 0.5 day | Depends on B |
| D — Fallbacks | 0.5 day | Depends on A + B |
| IX diagnostics | 1 day | Parallel throughout |

Total: ~5-6 days of focused work.

## Why the original tactical Sprint 1 is subsumed

Every one of Sprint 1's fixes is a sub-case of this architectural refactor:

- Sprint 1 Fix 1 (reject degenerate templates) → Phase A deprecates those templates entirely.
- Sprint 1 Fix 2 (strip slash notation from PC-set-level label) → Phase C structurally separates them.
- Sprint 1 Fix 3 (dyad fallback naming) → Phase D.
- Sprint 1 Fix 4 (delete duplicate Modes.yaml family) → Still useful; do this immediately regardless.

Doing the architectural refactor takes ~5 days instead of Sprint 1's ~2 days, but permanently fixes the foundation.

## Files touched

### New files
- `Common/GA.Domain.Core/Theory/Harmony/CanonicalChordPatternCatalog.cs`
- `Common/GA.Domain.Core/Theory/Harmony/ChordIntervalPattern.cs`
- `Common/GA.Domain.Services/Chords/CanonicalChordRecognizer.cs`

### Modified files
- `Common/Business.Core/Analysis/Voicings/ChordIdentification.cs` — add CanonicalName, SlashSuffix, structured fields
- `Common/GA.Domain.Services/Fretboard/Voicings/Analysis/VoicingHarmonicAnalyzer.cs` — delegate to CanonicalChordRecognizer for chord-identity path
- `Common/GA.Business.ML/Rag/VoicingDocumentFactory.cs` — read CanonicalName (invariant) for SYMBOLIC embedding
- `Common/GA.Business.Config/Modes.yaml` — delete redundant "Diminished (Octatonic) Family"

### Factory stays but for different job
- `ChordTemplateFactory` keeps its role for *Roman-numeral modal context naming* (called by `ChordTemplateNamingService.GenerateModalChordName`). It does NOT participate in PC-set recognition after refactor.
