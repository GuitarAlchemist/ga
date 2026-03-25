---
module: GA.Domain.Services
date: 2026-03-06
problem_type: best_practice
component: service_object
symptoms:
  - "Voicing accepts physically impossible notes on the same guitar string"
  - "No mechanism to detect parallel perfect fifths or octaves across voicings"
root_cause: missing_validation
resolution_type: code_fix
severity: medium
tags: [voice-leading, voicing-validator, parallel-motion, music-theory, domain-invariant]
---

# Best Practice: Voicing Invariants — Same-String Guard & Parallel Motion Detection

## Problem

Two domain invariants were absent from the voicing analysis layer:

1. The `Voicing` record could contain multiple `Position.Played` entries on the same guitar string — physically impossible but structurally unchecked.
2. There was no code to detect parallel perfect fifths or octaves between consecutive voicings, a fundamental voice-leading rule in classical and jazz harmony.

`FretboardPositionMapper.IsPhysicallyPossible()` guards the generation pipeline against same-string duplicates, but nothing protected voicings created outside that path (e.g., tests, API payloads, manually constructed chords).

## Environment

- Module: `GA.Domain.Services` → `Fretboard/Voicings/Analysis/`
- .NET Version: .NET 10 / C# 14
- Affected Component: `Voicing` analysis layer
- Date: 2026-03-06

## Symptoms

- `Voicing` type allows two `Position.Played` entries with the same `Str.Value`, which is physically impossible on a single-neck guitar
- No type or service enforces the constraint outside the chord-generation pipeline
- Chord progression sequences have no validator for parallel fifths or octaves (critical voice-leading issues in most styles)

## What Didn't Work

**Direct solution:** The problem was identified up-front via domain gap analysis. No failing tests or runtime errors — this was a proactive invariant addition.

The implementation choice was: do **not** modify `Voicing.cs` or `VoicingGenerator.cs`. Keeping the domain record pure and adding validation at the service/analysis layer (same pattern as `VoicingPhysicalAnalyzer`) was the correct approach.

## Solution

**Three new files in `Common/GA.Domain.Services/Fretboard/Voicings/Analysis/`:**

### 1. `ProgressionVoiceLeadingReport.cs` — result types

```csharp
public enum ParallelMotionType { Fifths, Octaves }

public record ParallelMotionIssue(
    int StringA, int StringB,
    ParallelMotionType Type,
    int FromMidiA, int ToMidiA,
    int FromMidiB, int ToMidiB);

public record ProgressionVoiceLeadingReport(IReadOnlyList<ParallelMotionIssue> Issues)
{
    public bool HasParallelFifths  => Issues.Any(i => i.Type == ParallelMotionType.Fifths);
    public bool HasParallelOctaves => Issues.Any(i => i.Type == ParallelMotionType.Octaves);
    public bool IsClean            => Issues.Count == 0;
    public static ProgressionVoiceLeadingReport Clean { get; } = new([]);
}
```

### 2. `VoicingValidator.cs` — same-string guard

```csharp
public static class VoicingValidator
{
    public static bool HasDuplicateStrings(Voicing voicing)
    {
        var strings = voicing.Positions
            .OfType<Position.Played>()
            .Select(p => p.Location.Str.Value)
            .ToList();
        return strings.Count != strings.Distinct().Count();
    }

    public static bool IsPhysicallyValid(Voicing voicing) => !HasDuplicateStrings(voicing);

    public static void ThrowIfInvalid(Voicing voicing)
    {
        if (HasDuplicateStrings(voicing))
            throw new InvalidOperationException(
                $"Invalid voicing: multiple notes on the same string. " +
                $"Diagram: {string.Join("-", voicing.Positions.Select(p =>
                    p is Position.Played pl ? pl.Location.Fret.Value.ToString() : "x"))}");
    }
}
```

### 3. `ProgressionVoiceLeadingAnalyzer.cs` — parallel motion detection

```csharp
public static class ProgressionVoiceLeadingAnalyzer
{
    private const int PerfectFifth  = 7;   // mod 12 catches compound intervals
    private const int PerfectOctave = 0;   // mod 12

    public static ProgressionVoiceLeadingReport Analyze(IReadOnlyList<Voicing> progression)
    {
        if (progression.Count < 2) return ProgressionVoiceLeadingReport.Clean;
        var issues = new List<ParallelMotionIssue>();
        for (var i = 0; i < progression.Count - 1; i++)
            issues.AddRange(DetectParallelMotion(progression[i], progression[i + 1]));
        return new(issues);
    }

    public static IReadOnlyList<ParallelMotionIssue> DetectParallelMotion(Voicing from, Voicing to)
    {
        var issues = new List<ParallelMotionIssue>();
        var fromMap = BuildVoiceMap(from);
        var toMap   = BuildVoiceMap(to);
        var shared  = fromMap.Keys.Intersect(toMap.Keys).OrderBy(s => s).ToList();
        if (shared.Count < 2) return issues;

        for (var a = 0; a < shared.Count - 1; a++)
        for (var b = a + 1; b < shared.Count; b++)
        {
            var fromA = fromMap[shared[a]]; var toA = toMap[shared[a]];
            var fromB = fromMap[shared[b]]; var toB = toMap[shared[b]];
            if (fromA == toA && fromB == toB) continue;          // both stationary
            var dirA = Math.Sign(toA - fromA);
            var dirB = Math.Sign(toB - fromB);
            if (dirA == 0 || dirB == 0 || dirA != dirB) continue; // not parallel motion
            var icFrom = Math.Abs(fromB - fromA) % 12;
            var icTo   = Math.Abs(toB   - toA)   % 12;
            if (icFrom == PerfectFifth  && icTo == PerfectFifth)
                issues.Add(new(shared[a], shared[b], ParallelMotionType.Fifths,  fromA, toA, fromB, toB));
            else if (icFrom == PerfectOctave && icTo == PerfectOctave)
                issues.Add(new(shared[a], shared[b], ParallelMotionType.Octaves, fromA, toA, fromB, toB));
        }
        return issues;
    }

    private static Dictionary<int, int> BuildVoiceMap(Voicing v) =>
        v.Positions
         .OfType<Position.Played>()
         .ToDictionary(p => p.Location.Str.Value, p => p.MidiNote.Value);
}
```

**Commands run:**

```powershell
# Build succeeded with 0 warnings
dotnet build Common/GA.Domain.Services/ -c Debug

# Build test project (GaApi locked by running process — use --no-dependencies)
dotnet build Tests/Common/GA.Business.Core.Tests/GA.Business.Core.Tests.csproj -c Debug --no-dependencies

# All 13 new tests pass
dotnet test ... --no-build --filter "VoicingValidatorTests|ProgressionVoiceLeadingAnalyzerTests"
# → Passed: 13, Failed: 0

# Full suite still green
dotnet test ... --no-build
# → Passed: 603, Failed: 0, Skipped: 5
```

## Why This Works

### Same-string guard

`Voicing.Positions` is a flat `Position[]` array with no structural enforcement on string uniqueness. `VoicingValidator` operates on the array post-construction, checking only `Position.Played` entries (muted strings have no MIDI note and can't conflict). Using `Distinct()` on the `Str.Value` int is O(n) and allocation-minimal for typical 6-string voicings.

### Parallel motion detection

The algorithm:
1. Builds a `Dictionary<int, int>` (string → MIDI) for each voicing — only played strings.
2. Finds strings played in **both** consecutive voicings (the "shared voices").
3. For each pair of shared voices, checks:
   - Skip if both stationary (`fromA == toA && fromB == toB`)
   - Skip if either voice is stationary or voices move in opposite directions (oblique / contrary motion)
   - Compute interval class `Math.Abs(midiB - midiA) % 12` — `% 12` collapses compound intervals (a 19-semitone 12th is the same interval class as a 7-semitone fifth)
   - Flag as parallel fifths if both intervals = 7; parallel octaves if both = 0

The `% 12` trick is critical: without it, a C4→G4 (7 semitones) paired with C3→G3 (19 semitones) would not be detected as parallel fifths.

## Prevention

- When constructing a `Voicing` programmatically (e.g., in tests or API handlers), always call `VoicingValidator.ThrowIfInvalid()` or `IsPhysicallyValid()` at the boundary.
- When the chord generation pipeline changes, verify `FretboardPositionMapper.IsPhysicallyPossible()` is still in the path — it's the upstream guard; `VoicingValidator` is the downstream safety net.
- Add `ProgressionVoiceLeadingAnalyzer.Analyze()` to any voice-leading report service before presenting suggestions to the user.
- The `PerfectOctave = 0` constant is counter-intuitive — it's zero because unisons and octaves share interval class 0 mod 12. Document this at usage sites.

## Related Issues

No related issues documented yet.
