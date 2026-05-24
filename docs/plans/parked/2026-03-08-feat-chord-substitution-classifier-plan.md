---
title: "feat: Chord Substitution Classifier (two-chord relationship detection)"
type: feat
status: completed
date: 2026-03-08
---

# feat: Chord Substitution Classifier

## Overview

Extend `ChordSubstitutionSkill` to answer: **"Is G7 a tritone substitution for Db7?"** — given two chord symbols, classify the substitution relationship (tritone sub, secondary dominant, backdoor dominant, modal interchange, set-class equivalent, ICV neighbor) using pure pitch-class arithmetic.

Currently the skill takes a single chord and finds nearby chords in ICV space. This plan adds a two-chord comparison mode that names the relationship type with evidence.

## Problem Statement

A guitarist types: "Is G7 a tritone sub for Db7?" or "How are Am and C related?" or "Can I use Eb7 instead of A7?".

Currently:
- `ChordSubstitutionSkill.CanHandle` triggers only when it sees ONE chord + substitution keyword
- It parses only triads (major/minor/dim/aug) — no 7th chord quality support
- It never compares two named chords to classify their relationship type
- `GrothendieckService` computes structural proximity numerically but has no named-type classifier

## Proposed Solution

Add a two-chord comparison path to `ChordSubstitutionSkill`:

1. `CanHandle` also matches messages containing **two chord symbols** with a comparison framing
2. Parse 7th chord qualities (dom7, maj7, m7, m7b5, dim7) in addition to triads
3. Add `ClassifySubstitution(a, b)` → `SubstitutionType[]` covering:
   - **Tritone sub**: roots 6 semitones apart, both dominant 7ths — the classic bebop move
   - **Secondary dominant**: A is a major/dom7 whose root is a P5 above B's root (A = V of B)
   - **Backdoor dominant**: A is bVII7 resolving by step to B = I (e.g. Bb7 → C)
   - **Modal interchange**: A's root appears in the parallel minor/major of B's key
   - **Set-class equivalent**: same prime form under T/I equivalence (delegate to `GaSetClassSubs` logic)
   - **ICV neighbor**: `GrothendieckService.ComputeDelta` L1 ≤ 2 (harmonic proximity)
4. Return a named explanation with evidence strings, `Confidence = 1.0f`

## Technical Approach

### Files to Change

| File | Change |
|---|---|
| `Common/GA.Business.ML/Agents/Skills/ChordSubstitutionSkill.cs` | Primary edit — all logic here |
| No other files need changing | `GaPlugin` already registers it |

### CanHandle Extension

```csharp
// Existing single-chord trigger (keep)
private static readonly Regex SingleChordPattern = ...;

// New two-chord comparison trigger
private static readonly Regex TwoChordComparisonPattern = new(
    @"\b([A-G][b#]?(?:m|maj|dim|aug|7|maj7|m7|m7b5|dim7)?)\b.*\b([A-G][b#]?(?:m|maj|dim|aug|7|maj7|m7|m7b5|dim7)?)\b.*\b(?:sub|related|same|instead|replace|equivalent|tritone|swap|reharmoni)\b",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);
```

### Quality Map Extension

Add dominant-7th parsing to `QualityIntervals`:
```csharp
// Current: triads only
// Add:
{ "7",    [0, 4, 7, 10] },   // dominant 7th
{ "maj7", [0, 4, 7, 11] },   // major 7th
{ "m7",   [0, 3, 7, 10] },   // minor 7th
{ "m7b5", [0, 3, 6, 10] },   // half-diminished
{ "dim7", [0, 3, 6, 9]  },   // diminished 7th
```

### ClassifySubstitution Method

```csharp
private IReadOnlyList<SubstitutionRelationship> ClassifySubstitution(
    int rootA, int[] intervalsA,
    int rootB, int[] intervalsB)
{
    var results = new List<SubstitutionRelationship>();
    var rootInterval = Math.Abs((rootB - rootA + 12) % 12);

    // Tritone sub: roots 6 semitones apart + both dominant 7ths
    if (rootInterval == 6
        && intervalsA.SequenceEqual([0,4,7,10])
        && intervalsB.SequenceEqual([0,4,7,10]))
        results.Add(new("Tritone Substitution",
            $"Roots are a tritone ({rootInterval} semitones) apart; both are dominant 7ths sharing guide tones B/F."));

    // Secondary dominant: A is V of B (root of A = root of B + 7 semitones)
    if ((rootA - rootB + 12) % 12 == 7)
        results.Add(new("Secondary Dominant",
            $"Chord A (root {rootA}) is a P5 above Chord B (root {rootB}) — A functions as V of B."));

    // Backdoor dominant: A is bVII7, resolves by half-step to B = I
    // ... etc.

    return results;
}

public sealed record SubstitutionRelationship(string Type, string Explanation);
```

### Response Format

```
G7 → Db7 relationship analysis:

★★★ Tritone Substitution
  Both are dominant 7ths with roots 6 semitones apart (G and Db = tritone).
  They share the same guide tones: B/Cb (M3 of G7 = m7 of Db7) and F (m7 of G7 = M3 of Db7).
  Classic bebop move — Db7 resolves to C just as G7 resolves to C.

★★  ICV Neighbor (distance 2)
  G7 and Db7 are 2 steps apart in ICV space (GrothendieckDelta.L1 = 2).

Confidence: 100% (deterministic pitch-class arithmetic)
```

## Acceptance Criteria

- [x] `"Is G7 a tritone sub for Db7?"` → classified as "Tritone Substitution" with guide-tone evidence
- [x] `"Is Dm the same as F?"` → classified as "Set-Class Equivalent" (both major-triad prime form relatives)
- [x] `"Can I use C7 instead of G7?"` → classified as "ICV Neighbor" (not tritone sub — roots are P4 apart)
- [x] Existing single-chord substitution queries still work unchanged
- [x] Triads still parse correctly (no regression)
- [x] 7th chord quality parsing: G7, Dbmaj7, Am7, Bm7b5, Bdim7 all resolve correctly
- [x] `Confidence = 1.0f` (pure domain computation, no LLM)
- [x] Tests added for each substitution type in `Tests/Common/GA.Business.ML.Tests/`

## System-Wide Impact

- **Single file change**: `ChordSubstitutionSkill.cs` — no new dependencies
- **No DI registration change**: already registered as Singleton in `GaPlugin`
- **No LLM call**: pure pitch-class arithmetic, `Confidence = 1.0f`
- **CanHandle is additive**: existing single-chord path continues; new two-chord path is an OR condition
- `GrothendieckService` is already injected into the skill — use `ComputeDelta` for ICV distance

## Sources & References

- `ChordSubstitutionSkill.cs`: `Common/GA.Business.ML/Agents/Skills/ChordSubstitutionSkill.cs`
- `GrothendieckService.cs`: `Common/GA.Domain.Services/Atonal/Grothendieck/GrothendieckService.cs`
- `ModalInterchange.yaml`: `Common/GA.Business.Config/ModalInterchange.yaml` — borrowed chord catalog
- `KeyModulationTechniques.yaml`: `Common/GA.Business.Config/KeyModulationTechniques.yaml` — pivot chord patterns
- `FretSpanSkill.cs`: pattern reference for pure-domain Singleton skill
- Research finding: tritone sub detection is `(rootB - rootA + 12) % 12 == 6` + both dominant 7ths
