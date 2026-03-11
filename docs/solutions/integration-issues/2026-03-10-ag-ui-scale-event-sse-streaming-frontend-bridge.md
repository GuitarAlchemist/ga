---
title: "AG-UI ga:scale custom event — streaming scale notes from C# to React via SSE"
date: 2026-03-10
problem_type: "integration-issues"
component: "AgUiChatController / useGAAgent / GAChatPanel"
symptoms:
  - "No way to push scale degree data to React frontend after key identification"
  - "Fretboard scale overlay had no live data source"
  - "GaAgentState had no scaleNotes field"
tags:
  - "ag-ui"
  - "sse"
  - "c-sharp"
  - "react"
  - "typescript"
  - "streaming"
  - "chatbot"
related_patterns:
  - "ag-ui-custom-events"
  - "sse-domain-push"
severity: "medium"
related_docs:
  - "docs/solutions/architecture/orchestration-library-extraction-gachatbot.md"
---

# AG-UI `ga:scale` custom event — streaming scale notes from C# to React via SSE

## Problem

After the chatbot agent identified a musical key (e.g. "G major"), the backend could emit a `ga:diatonic` event with the 7 diatonic chords. However, there was no way to push the individual scale notes (with pitch class and degree) to the React frontend. The `GaAgentState` had no field for scale data, so the fretboard scale overlay component had no live data source.

## Root Cause

The AG-UI SSE stream already supports arbitrary typed payloads via the `CUSTOM` event type. The gap was:
1. No `ScaleNoteService` to compute scale notes server-side
2. No `ga:scale` emission in `AgUiChatController`
3. No `scaleNotes` field in `GaAgentState`
4. No `ga:scale` handler in `useGAAgent`

## Solution

### The AG-UI Custom Event Pattern

Any typed domain payload can be pushed from the C# pipeline to React over the existing SSE channel using `WriteCustomAsync`. No new endpoints, no WebSockets, no polling.

**Backend** — emit after processing:
```csharp
await writer.WriteCustomAsync("ga:<event-name>", payload, cancellationToken);
```

**Frontend** — intercept by name:
```typescript
onCustomEvent({ event }) {
  if (event.name === "ga:<event-name>") {
    dispatch({ type: "SET_<DATA>", payload: event.value as MyType });
  }
}
```

### 1. Backend: `ScaleNoteService`

Pure static calculator — no dependencies, no DI needed.

```csharp
// Apps/ga-server/GaApi/Services/ScaleNoteService.cs
public static class ScaleNoteService
{
    public record ScaleNote(int Degree, string Note, int PitchClass);

    private static readonly int[] MajorIntervals = [0, 2, 4, 5, 7, 9, 11];
    private static readonly int[] MinorIntervals = [0, 2, 3, 5, 7, 8, 10];

    private static readonly string[] NoteNames =
        ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    public static IReadOnlyList<ScaleNote>? GetNotes(string keyString)
    {
        // Parses "G major" → G, A, B, C, D, E, F#
        // Parses "A minor" → A, B, C, D, E, F, G
        var parts = keyString.Trim().Split(' ', 2);
        if (parts.Length < 2) return null;

        var root = parts[0];
        var quality = parts[1].ToLowerInvariant();
        var rootIndex = Array.IndexOf(NoteNames, root);
        if (rootIndex < 0) return null;

        var intervals = quality.StartsWith("minor") ? MinorIntervals : MajorIntervals;
        return intervals
            .Select((interval, i) =>
            {
                var pc = (rootIndex + interval) % 12;
                return new ScaleNote(i + 1, NoteNames[pc], pc);
            })
            .ToList();
    }
}
```

### 2. Backend: Emit `ga:scale` in `AgUiChatController`

```csharp
// After the existing ga:diatonic emission:
if (finalKey is not null)
{
    var chordsResult = await contextualChordService.GetChordsForKeyAsync(finalKey);
    if (chordsResult.IsSuccess)
        await writer.WriteCustomAsync("ga:diatonic", chordsResult.GetValueOrThrow(), cancellationToken);

    // NEW: scale notes for the live fretboard overlay
    var scaleNotes = ScaleNoteService.GetNotes(finalKey);
    if (scaleNotes is not null)
        await writer.WriteCustomAsync("ga:scale", scaleNotes, cancellationToken);
}
```

### 3. Frontend: Extend `agent-state.ts`

```typescript
// ReactComponents/ga-react-components/src/types/agent-state.ts
export interface ScaleNote {
  readonly degree: number;
  readonly note: string;
  readonly pitchClass: number;
}

export interface GaAgentState {
  // ... existing fields ...
  readonly scaleNotes: readonly ScaleNote[];
}

export const EMPTY_GA_STATE: GaAgentState = {
  // ... existing fields ...
  scaleNotes: [],
};
```

### 4. Frontend: Handle in `useGAAgent.ts`

```typescript
// Add to GaAction union type:
| { type: 'SET_SCALE'; notes: readonly ScaleNote[] }

// Add to gaReducer:
case 'SET_SCALE': return { ...state, scaleNotes: action.notes };

// Add to onCustomEvent handler:
onCustomEvent({ event }) {
  const name  = (event as { name?: string }).name;
  const value = (event as { value?: unknown }).value;
  if (name === 'ga:diatonic' && Array.isArray(value)) {
    dispatch({ type: 'SET_DIATONIC', chords: value as ChordInContext[] });
  } else if (name === 'ga:scale' && Array.isArray(value)) {
    dispatch({ type: 'SET_SCALE', notes: value as ScaleNote[] });
  }
},
```

### 5. Frontend: Render scale degree badges in `GAChatPanel.tsx`

```tsx
{state.scaleNotes.length > 0 && (
  <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
    {state.scaleNotes.map(sn => (
      <Box
        key={sn.degree}
        sx={{
          px: 0.8, py: 0.2,
          borderRadius: 1,
          bgcolor: sn.degree === 1 ? '#1565c0' : '#e3f2fd',
          color:   sn.degree === 1 ? '#fff'    : '#1565c0',
          fontFamily: 'monospace',
          fontSize:   '0.68rem',
          fontWeight: sn.degree === 1 ? 700 : 400,
        }}
        title={`Scale degree ${sn.degree}`}
      >
        {sn.note}
      </Box>
    ))}
  </Box>
)}
```

## Extending the Pattern

To add a new domain push event (e.g. `ga:fingering`, `ga:chord-tones`):

1. **Backend**: Add a static service to compute the payload, call `WriteCustomAsync("ga:<name>", payload, ct)` in `AgUiChatController`
2. **Frontend**: Add interface to `agent-state.ts`, add field to `GaAgentState` + `EMPTY_GA_STATE`
3. **Hook**: Add action type + reducer case + `onCustomEvent` branch in `useGAAgent.ts`
4. **UI**: Read `state.<field>` in any component

The SSE infrastructure handles everything else — ordering, framing, error handling.

## Key Properties

- **Ordering**: Events emit in SSE sequence order; `ga:scale` always follows `ga:diatonic` within the same response
- **Non-blocking**: Custom events don't interrupt the streaming text message
- **Type safety**: C# record → JSON → TypeScript interface; kept in sync manually (or code-generated)
- **Stateless server**: `ScaleNoteService` is a pure static class — no DI, no state, easy to test
