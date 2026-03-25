---
title: "feat: Voicing by Hand Comfort (My hand hurts playing barre chords)"
type: feat
status: active
date: 2026-03-08
---

# feat: Voicing by Hand Comfort

## Overview

Given any chord name, suggest open-position or partial-barre voicings ranked by fret-hand stretch — so a guitarist with hand discomfort or limited reach can find a playable alternative to a full barre chord. The feature is surfaced via a new `VoicingComfortService` in GaApi, a ROP-compliant fix to `VoicingFilterService`, and wiring to the existing `VexTabViewer` frontend component.

## Problem Statement

A guitarist says: "My hand hurts playing barre chords — is there an easier way to play Bm?" or clicks a chord in the frontend and wants the least-stretch voicing displayed.

Currently:
- `GET /api/contextual-chords/voicings/{chordName}` returns all voicings for a chord but returns them in an unordered list with no stretch metric.
- `VoicingFilterService` throws `ArgumentException` on invalid input instead of returning `Result<T>` — violating the ROP policy and making it unreliable at the API boundary.
- The frontend `VexTabViewer` component exists and can render chord diagrams, but there is no endpoint or service that selects the easiest voicing for a given chord.
- There is no barre-chord detection filter — "open or partial-barre only" is not enforced.

## Proposed Solution

1. **`VoicingComfortService`** (new, in `Apps/ga-server/GaApi/Services/`) — wraps `ContextualChordService`, adds a stretch scorer and barre filter, returns voicings sorted ascending by hand stretch.
2. **Fix `VoicingFilterService`** to return `Result<IReadOnlyList<Voicing>, VoicingFilterError>` instead of throwing `ArgumentException` — full ROP compliance.
3. **`GET /api/contextual-chords/voicings/{chordName}/comfort`** — new endpoint on `ContextualChordsController` that delegates to `VoicingComfortService` and returns sorted voicings with stretch scores.
4. **Frontend wiring** — `VexTabViewer` shows the easiest voicing for any clicked chord using the new endpoint.

## Technical Approach

### New Files

| File | Purpose |
|---|---|
| `Apps/ga-server/GaApi/Services/VoicingComfortService.cs` | Stretch scoring + barre filter |

### Files to Change

| File | Change |
|---|---|
| `Apps/ga-server/GaApi/Services/VoicingFilterService.cs` | Replace `ArgumentException` throws with `Result<T, VoicingFilterError>` returns |
| `Apps/ga-server/GaApi/Controllers/ContextualChordsController.cs` | Add `GET voicings/{chordName}/comfort` action |
| `ReactComponents/ga-react-components/src/components/Fretboard/VexTabViewer.tsx` | Wire chord click → comfort endpoint → render easiest voicing |
| `Tests/Apps/GaApi.Tests/Services/VoicingFilterServiceTests.cs` | Update tests for ROP signature |

### Stretch Metric

The stretch score for a voicing is computed as:

```csharp
// Fretted strings only — exclude open strings (fret == 0) and muted strings (fret == -1)
var frettedFrets = voicing.Strings
    .Where(s => s.Fret > 0)
    .Select(s => s.Fret)
    .ToList();

var stretch = frettedFrets.Count == 0
    ? 0
    : frettedFrets.Max() - frettedFrets.Min();
```

Lower stretch = easier to play. Open-position chords that use only open strings score 0.

### Barre Chord Detection

A voicing is classified as a full barre chord (and filtered out) when 4 or more fretted strings share the same fret value at the lowest fretted position:

```csharp
private static bool IsFullBarre(Voicing voicing)
{
    var frettedFrets = voicing.Strings
        .Where(s => s.Fret > 0)
        .Select(s => s.Fret)
        .ToList();

    if (frettedFrets.Count < 4) return false;

    var minFret = frettedFrets.Min();
    return frettedFrets.Count(f => f == minFret) >= 4;
}
```

### VoicingComfortService

```csharp
public sealed class VoicingComfortService(
    ContextualChordService chordService,
    ILogger<VoicingComfortService> logger)
{
    public async Task<Result<IReadOnlyList<ComfortRankedVoicing>, VoicingError>> GetComfortRankedAsync(
        string chordName,
        bool excludeFullBarre = true,
        CancellationToken ct = default)
    {
        var voicingsResult = await chordService.GetVoicingsAsync(chordName, ct);
        if (voicingsResult.IsError) return voicingsResult.Error;

        var voicings = voicingsResult.Value;
        if (excludeFullBarre)
            voicings = voicings.Where(v => !IsFullBarre(v)).ToList();

        return voicings
            .Select(v => new ComfortRankedVoicing(v, ComputeStretch(v)))
            .OrderBy(r => r.Stretch)
            .ToList();
    }
}

public sealed record ComfortRankedVoicing(Voicing Voicing, int Stretch);
```

### VoicingFilterService ROP Fix

Current (throws):
```csharp
public IReadOnlyList<Voicing> Filter(VoicingFilterCriteria criteria)
{
    if (criteria is null) throw new ArgumentException("Criteria cannot be null");
    // ...
}
```

Fixed (ROP):
```csharp
public Result<IReadOnlyList<Voicing>, VoicingFilterError> Filter(VoicingFilterCriteria criteria)
{
    if (criteria is null)
        return VoicingFilterError.NullCriteria;
    // ...
    return filteredVoicings;
}

public enum VoicingFilterError { NullCriteria, InvalidRange, NoVoicingsFound }
```

Controllers convert `VoicingFilterError` to HTTP status codes at the boundary.

### New API Endpoint

```http
GET /api/contextual-chords/voicings/{chordName}/comfort?excludeBarre=true
```

Response:
```json
{
  "chordName": "Bm",
  "key": null,
  "voicings": [
    {
      "shape":   "x-2-4-4-3-2",
      "stretch": 2,
      "isBarre": false,
      "displayName": "Bm (open partial)"
    },
    {
      "shape":   "x-x-4-4-3-2",
      "stretch": 2,
      "isBarre": false,
      "displayName": "Bm (4-string)"
    }
  ]
}
```

### Frontend Wiring

`VexTabViewer` already exists at `ReactComponents/ga-react-components/src/components/Fretboard/VexTabViewer.tsx`. Add a `onChordClick` handler that:

1. Calls `GET /api/contextual-chords/voicings/{chordName}/comfort`.
2. Takes the first (lowest-stretch) voicing from the response.
3. Renders it inline using the existing `VexTabViewer` renderer.

State and fetch logic lives in a new hook `src/hooks/useComfortVoicing.ts`.

## Acceptance Criteria

- [ ] `GET /api/contextual-chords/voicings/Bm/comfort` returns voicings sorted by stretch ascending.
- [ ] Full barre voicings (4+ strings on the same fret) are excluded by default (`excludeBarre=true`).
- [ ] Passing `excludeBarre=false` returns all voicings including barre chords, still sorted by stretch.
- [ ] Open strings (fret 0) are excluded from the stretch calculation.
- [ ] `VoicingFilterService.Filter()` returns `Result<IReadOnlyList<Voicing>, VoicingFilterError>` — no `ArgumentException` thrown.
- [ ] All existing `VoicingFilterService` tests pass after the ROP fix (signatures updated).
- [ ] Frontend: clicking a chord in `VexTabViewer` fetches and renders the lowest-stretch voicing.
- [ ] `dotnet build AllProjects.slnx -c Debug` passes with zero warnings in touched files.
- [ ] `npm run build` and `npm run lint` pass in `ReactComponents/ga-react-components`.
- [ ] Unit tests added in `Tests/Apps/GaApi.Tests/Services/VoicingComfortServiceTests.cs` covering:
  - [ ] Stretch calculation excludes open strings.
  - [ ] Barre detection flags voicings with 4+ strings on the same lowest fret.
  - [ ] Results are sorted by stretch ascending.
  - [ ] `excludeBarre=false` includes barre chords.

## Dependencies & Prerequisites

| Dependency | Status |
|---|---|
| `ContextualChordService.GetVoicingsAsync()` | Exists — `Apps/ga-server/GaApi/Services/` |
| `ContextualChordsController` | Exists — `Apps/ga-server/GaApi/Controllers/ContextualChordsController.cs` |
| `VoicingFilterService` | Exists — needs ROP fix |
| `VexTabViewer` component | Exists — `ReactComponents/ga-react-components/src/components/Fretboard/VexTabViewer.tsx` |
| `GA.Core.Functional` (`Result<T, TError>`) | Exists — Layer 1 |

## Implementation Tasks

- [ ] Fix `VoicingFilterService.Filter()` to return `Result<IReadOnlyList<Voicing>, VoicingFilterError>` (ROP).
  - [ ] Add `VoicingFilterError` enum.
  - [ ] Update `ContextualChordsController` to convert `VoicingFilterError` to HTTP responses at the boundary.
  - [ ] Update `VoicingFilterServiceTests.cs` for the new signature.
- [ ] Create `VoicingComfortService.cs` in `Apps/ga-server/GaApi/Services/`.
  - [ ] Implement `ComputeStretch(Voicing)` — excludes open and muted strings.
  - [ ] Implement `IsFullBarre(Voicing)` — 4+ strings share the same lowest fret.
  - [ ] Implement `GetComfortRankedAsync(chordName, excludeFullBarre, ct)` returning `Result<T, VoicingError>`.
- [ ] Register `VoicingComfortService` in GaApi DI container.
- [ ] Add `GET voicings/{chordName}/comfort` action to `ContextualChordsController`.
- [ ] Create `useComfortVoicing.ts` hook in `ReactComponents/ga-react-components/src/hooks/`.
- [ ] Wire `VexTabViewer` `onChordClick` to call `useComfortVoicing` and render the result.
- [ ] Write unit tests in `Tests/Apps/GaApi.Tests/Services/VoicingComfortServiceTests.cs`.
- [ ] Run `dotnet build AllProjects.slnx -c Debug`, `dotnet test AllProjects.slnx`, `npm run build`, `npm run lint` — all must pass.

## Sources & References

- `ContextualChordsController.cs`: `Apps/ga-server/GaApi/Controllers/ContextualChordsController.cs` — existing voicing endpoint pattern.
- `VoicingFilterService.cs`: `Apps/ga-server/GaApi/Services/VoicingFilterService.cs` — target for ROP fix.
- `VoicingFilterCriteria.cs`: `Common/GA.Domain.Services/Fretboard/Voicings/Filtering/VoicingFilterCriteria.cs` — filter input model.
- `VexTabViewer.tsx`: `ReactComponents/ga-react-components/src/components/Fretboard/VexTabViewer.tsx` — frontend render target.
- `GA.Core.Functional`: Layer 1 ROP types (`Result<T, TError>`, `Option<T>`).
- ROP Patterns skill: `.agent/skills/rop-patterns/SKILL.md` — decision tree and code patterns for service-layer error handling.
- Fast Voicing ILGPU plan: `docs/plans/2026-03-05-feat-fast-voicing-ilgpu-batch-pipeline-plan.md` — related voicing pipeline work.
