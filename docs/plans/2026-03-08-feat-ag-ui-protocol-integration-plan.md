---
title: "feat: AG-UI Protocol Integration — Agent-Native UI for Guitar Alchemist"
type: feat
status: completed
date: 2026-03-08
---

# feat: AG-UI Protocol Integration — Agent-Native UI for Guitar Alchemist

## Overview

Integrate the [AG-UI protocol](https://github.com/ag-ui-protocol/ag-ui) into Guitar Alchemist so that chatbot agents can push structured music-domain events (diatonic chords, tab notation, key analysis) to composable React UI components in real time. The protocol sits between the existing `ProductionOrchestrator` SSE pipeline and the React frontend, replacing ad-hoc event shapes with a standardized, typed event stream.

The core idea: when the `TheoryAgent` identifies that the user asked about G major, it not only writes a text answer — it also emits a `CUSTOM { name: "ga:diatonic", value: ChordInContext[] }` event. The `DiatonicChordTable` React component renders it without any string parsing or hardcoded quality detection; all domain knowledge (roman numerals, chord function, quality) flows from the backend model.

---

## Problem Statement

**Current state:**
- The chatbot streams text only — `NaturalLanguageAnswer` split into sentences by `SseChunker`.
- `ChatResponse` already carries `Candidates` (RAG voicings), `Progression` (extracted chord sequence), and `Routing` (agent metadata) — all currently discarded before reaching the frontend.
- The frontend has two duplicate SSE parsers (`chatApi.ts`, `chatService.ts`) with no shared abstraction.
- `DiatonicChordTable.tsx` (partially written) hardcodes quality detection from chord symbol strings — a violation of domain ownership.

**The gap:** The agent knows what it found (chords in G major), but the UI only gets the prose description of that finding.

**With AG-UI:** The agent emits structured events alongside text. The UI renders domain-aware components that are _driven by_ agent state, not just displaying text about it.

---

## Proposed Solution

### Architecture

```
User message
    │
    ▼
POST /api/chatbot/chat/agui/stream
    │  (RunAgentInput: threadId, messages, state)
    ▼
ProductionOrchestrator.AnswerAsync()
    │  returns ChatResponse { NaturalLanguageAnswer, Candidates, Progression, Routing }
    ▼
AgUiEventWriter  (new — thin SSE helper, ~60 lines)
    │
    │── RUN_STARTED
    │── STEP_STARTED { stepName: routing.AgentId }
    │── STATE_SNAPSHOT { key, mode, chords: [], analysisPhase: "idle" }
    │── TEXT_MESSAGE_START / CONTENT chunks / TEXT_MESSAGE_END
    │── CUSTOM { name: "ga:diatonic",    value: ChordInContext[] }       ← if theory path
    │── CUSTOM { name: "ga:candidates",  value: CandidateVoicing[] }    ← if RAG path
    │── CUSTOM { name: "ga:progression", value: string[] }              ← if progression found
    │── CUSTOM { name: "ga:key",         value: { key, confidence } }   ← if key identified
    │── STATE_DELTA [JSON Patch]                                         ← state sync
    │── RUN_FINISHED
    ▼
@ag-ui/client HttpAgent (frontend)
    │
    ▼
useGAAgent hook  (new — ga-react-components/src/hooks/useGAAgent.ts)
    │  GaAgentState { diatonicChords, candidates, progression, analysisPhase }
    ▼
┌──────────────────────┬────────────────────┬────────────────────┐
│ DiatonicChordTable   │ VexTabViewer       │ FretboardHeatMap   │
│ (ChordInContext[])   │ (notation string)  │ (candidates[])     │
└──────────────────────┴────────────────────┴────────────────────┘
```

### Wire Format (AG-UI SSE)

No .NET SDK exists — implement directly. The protocol is three response headers plus `data: {json}\n\n` frames:

```
Content-Type: text/event-stream
Cache-Control: no-cache
X-Accel-Buffering: no          ← critical for nginx reverse proxy

data: {"type":"RUN_STARTED","threadId":"...","runId":"...","timestamp":1741449600000}\n\n
data: {"type":"TEXT_MESSAGE_CONTENT","messageId":"msg_1","delta":"The chords in G major..."}\n\n
data: {"type":"CUSTOM","name":"ga:diatonic","value":[...]}\n\n
data: {"type":"RUN_FINISHED","threadId":"...","runId":"..."}\n\n
```

All JSON field names must be **camelCase** (`messageId`, `threadId`, `runId`) — configure `JsonNamingPolicy.CamelCase` on the serializer options used for SSE emission.

---

## Technical Approach

### Phase 1 — C# AG-UI Infrastructure (GaApi)

#### 1.1 `AgUiEventTypes.cs`

```
Common/GA.Business.Core.Orchestration/AgUi/AgUiEventTypes.cs
```

String constants for all valid `type` values from the AG-UI spec:

```csharp
internal static class AgUiEventTypes
{
    public const string RunStarted          = "RUN_STARTED";
    public const string RunFinished         = "RUN_FINISHED";
    public const string RunError            = "RUN_ERROR";
    public const string StepStarted         = "STEP_STARTED";
    public const string StepFinished        = "STEP_FINISHED";
    public const string TextMessageStart    = "TEXT_MESSAGE_START";
    public const string TextMessageContent  = "TEXT_MESSAGE_CONTENT";
    public const string TextMessageEnd      = "TEXT_MESSAGE_END";
    public const string StateSnapshot       = "STATE_SNAPSHOT";
    public const string StateDelta          = "STATE_DELTA";
    public const string Custom              = "CUSTOM";
}
```

#### 1.2 `AgUiEvents.cs` — Typed event record types

```
Common/GA.Business.Core.Orchestration/AgUi/AgUiEvents.cs
```

Sealed records for each emitted event type. Use `[JsonPropertyName]` for camelCase where needed (or configure globally):

```csharp
// Examples — full file defines all types used
public sealed record AgUiRunStartedEvent(string Type, string ThreadId, string RunId, long Timestamp);
public sealed record AgUiTextMessageContentEvent(string Type, string MessageId, string Delta);
public sealed record AgUiCustomEvent(string Type, string Name, object Value, long Timestamp);
public sealed record AgUiStateDeltaEvent(string Type, IReadOnlyList<JsonPatchOperation> Delta);
```

#### 1.3 `AgUiEventWriter.cs`

```
Common/GA.Business.Core.Orchestration/AgUi/AgUiEventWriter.cs
```

Thin helper that writes SSE frames to `HttpResponse.Body`. Follows the existing `ChatbotController` pattern:

```csharp
public sealed class AgUiEventWriter(HttpResponse response, JsonSerializerOptions options)
{
    public async Task WriteEventAsync<T>(T payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, options);
        // Escape embedded newlines to keep the SSE frame on a single data: line
        json = json.Replace("\n", "\\n", StringComparison.Ordinal);
        await response.WriteAsync($"data: {json}\n\n", ct);
        await response.Body.FlushAsync(ct);
    }

    public Task WriteRunStartedAsync(string threadId, string runId, CancellationToken ct) => ...
    public Task WriteTextChunkAsync(string messageId, string delta, CancellationToken ct) => ...
    public Task WriteCustomAsync(string name, object value, CancellationToken ct) => ...
    public Task WriteStateDeltaAsync(IReadOnlyList<JsonPatchOperation> patch, CancellationToken ct) => ...
    public Task WriteRunFinishedAsync(string threadId, string runId, CancellationToken ct) => ...
    public Task WriteRunErrorAsync(string message, string code, CancellationToken ct) => ...
}
```

#### 1.4 `POST /api/chatbot/chat/agui/stream`

New action on `ChatbotController.cs` (or `AgUiChatController.cs` if separation is preferred). Mirrors the existing `ChatStream` action exactly, substituting AG-UI event envelopes:

```
Apps/ga-server/GaApi/Controllers/ChatbotController.cs  (or AgUiChatController.cs)
```

Event emission order:
1. `RUN_STARTED`
2. `STATE_SNAPSHOT { key: null, mode: null, chords: [], analysisPhase: "idle" }`
3. `STEP_STARTED { stepName: routing.AgentId }`  ← routing metadata first (institutional learning)
4. `TEXT_MESSAGE_START`
5. Per-chunk `TEXT_MESSAGE_CONTENT` via `SseChunker.SplitIntoChunks()`
6. `TEXT_MESSAGE_END`
7. `CUSTOM "ga:diatonic"` if `ChatResponse.Candidates` contains theory voicings
8. `CUSTOM "ga:candidates"` if RAG voicings found
9. `CUSTOM "ga:progression"` if `ChatResponse.Progression` is non-null
10. `STATE_DELTA` to sync final state
11. `RUN_FINISHED`
12. On error: `RUN_ERROR` (never change status code after headers committed)

**Concurrency gate**: same `ILlmConcurrencyGate` as existing endpoint — 3 parallel slots.

**Input model** (`RunAgentInput`):

```csharp
public sealed record RunAgentInput(
    string ThreadId,
    string RunId,
    IReadOnlyList<AgUiMessage> Messages,
    object? State = null
);
```

#### 1.5 CORS update

```
Apps/ga-server/GaApi/Program.cs (lines 119–126)
```

Add `http://localhost:5174` (Vite component library dev server) and Streamlit port (`http://localhost:8501`) to the `AllowAll` CORS policy.

---

### Phase 2 — React Hook + Component Library

#### 2.1 Install packages

```bash
cd ReactComponents/ga-react-components
npm install @ag-ui/core @ag-ui/client
```

#### 2.2 `src/types/agent-state.ts` — Typed domain state

```
ReactComponents/ga-react-components/src/types/agent-state.ts
```

```typescript
import { z } from "zod";   // already in project via @ag-ui/core dependency

// Domain types mirroring C# ChordInContext record
export interface ChordInContext {
  readonly templateName: string;
  readonly root: string;
  readonly contextualName: string;
  readonly scaleDegree: number | null;
  readonly function: string;           // "Tonic" | "Subdominant" | "Dominant" | ...
  readonly commonality: number;
  readonly isNaturallyOccurring: boolean;
  readonly alternateNames: readonly string[];
  readonly notes: readonly string[];
  readonly romanNumeral: string | null;  // "I" | "ii" | "iii" | ... — from backend, no heuristics
  readonly functionalDescription: string | null;
}

export interface GaAgentState {
  readonly key: string | null;
  readonly mode: string | null;
  readonly diatonicChords: readonly ChordInContext[];
  readonly candidates: readonly CandidateVoicing[];
  readonly progression: readonly string[];
  readonly analysisPhase: "idle" | "identifying" | "complete";
  readonly lastError: string | null;
}

export const EMPTY_GA_STATE: GaAgentState = {
  key: null, mode: null,
  diatonicChords: [], candidates: [], progression: [],
  analysisPhase: "idle", lastError: null,
};
```

#### 2.3 `src/hooks/useGAAgent.ts`

```
ReactComponents/ga-react-components/src/hooks/useGAAgent.ts
```

Wraps `@ag-ui/client` `HttpAgent`. No Jotai — uses `useState` + `useReducer`. Exposes:

```typescript
interface UseGAAgentReturn {
  state: GaAgentState;
  messages: AssistantMessage[];
  isStreaming: boolean;
  run: (userMessage: string, threadId?: string) => Promise<void>;
  abort: () => void;
}

export function useGAAgent(endpointUrl: string): UseGAAgentReturn
```

Internal subscriber handles:
- `onStateSnapshotEvent` → validate with Zod, `setState(snapshot)`
- `onStateDeltaEvent` → `state` arg already patched by SDK; `setState(state)`
- `onCustomEvent` → dispatch `ga:diatonic` → `setDiatonicChords`, etc.
- `onTextMessageStartEvent` / `onTextMessageContentEvent` / `onTextMessageEndEvent` → accumulate streaming message
- `onRunFinishedEvent` → `setIsStreaming(false)`
- `onRunFailed` → `setLastError(error.message)`, return `{ stopPropagation: true }`

**Cancellation**: `agent.abortRun()` wired to `abort()` return value. `AbortController` is managed by `HttpAgent` internally.

#### 2.4 `src/components/DiatonicChordTable.tsx` — rewrite with domain types

Replaces the partially-written version that used string-parsing heuristics. New props:

```typescript
export interface DiatonicChordTableProps {
  chords: readonly ChordInContext[];         // from backend — quality is ChordInContext.function
  onChordClick?: (chord: ChordInContext) => void;
}
```

Quality color mapping:
```typescript
// Driven by ChordInContext.function (backend value), not parsed from chord symbol
const FUNCTION_COLORS: Record<string, ChordColors> = {
  "Tonic":       { bg: "#fff8e1", text: "#e65100", border: "#ffb300" },
  "Subdominant": { bg: "#e3f2fd", text: "#1565c0", border: "#1976d2" },
  "Dominant":    { bg: "#fce4ec", text: "#880e4f", border: "#c2185b" },
};
```

Roman numeral display: `chord.romanNumeral` — directly from backend, null-safe with fallback to `chord.scaleDegree?.toString()`.

#### 2.5 `src/components/GAChatPanel.tsx`

New composable panel wiring `useGAAgent` + domain components:

```typescript
interface GAChatPanelProps {
  agentUrl: string;
}

// Renders:
//  - Chat input + streaming text (left column)
//  - DiatonicChordTable when state.diatonicChords.length > 0 (right column)
//  - VexTabViewer when selectedChord has tab notation (right column, below table)
//  - Routing badge (STEP_STARTED agent name + confidence)
```

---

### Phase 3 — ga-client Integration

#### 3.1 Consolidate duplicate SSE parsers

The existing `chatApi.ts` async-generator and `chatService.ts` callback-based parsers duplicate SSE parsing logic. Replace both with `useGAAgent` from the component library. This is the AG-UI integration payoff: one standard client, not three.

Files to retire (or reduce to thin wrappers calling `useGAAgent`):
- `Apps/ga-client/src/services/chatApi.ts`
- `Apps/ga-client/src/services/chatService.ts`

**Migration**: Update `chatAtoms.ts` `sendMessageAtom` to drive the `useGAAgent` hook instead of `chatApi.streamChat()`.

#### 3.2 Panel routes for Streamlit embedding

Add `/panels/diatonic` and `/panels/vextab` routes to `ga-react-components/src/main.tsx` — chrome-free pages that read `useGAAgent` state (or URL params as fallback). Enables Streamlit `st.components.v1.iframe()` embedding with no postMessage hacks.

---

### Phase 4 — Streamlit (Optional, Later)

The AG-UI Python SDK (`sdks/python`) exists in the protocol repo. A Streamlit app can:
1. Call `POST /api/chatbot/chat/agui/stream` directly (it's just SSE)
2. Parse AG-UI events in Python
3. Embed `/panels/diatonic?key=G&scale=major` iframes as visual output

No work needed now — the C# SSE endpoint is the foundation.

---

## Alternative Approaches Considered

| Approach | Why rejected |
|---|---|
| **Extend existing `/stream` endpoint** with AG-UI-shaped events | Would break existing ga-client consumers of the text-only SSE format; cleaner to add a new parallel endpoint |
| **SignalR as AG-UI transport** | AG-UI is SSE-based; `@ag-ui/client.HttpAgent` sends `Accept: text/event-stream`. SignalR hub stays for existing use cases; new AG-UI endpoint is HTTP/SSE. |
| **CopilotKit instead of raw `@ag-ui/client`** | CopilotKit is built on AG-UI but adds React-specific abstractions and a cloud proxy. Using raw `@ag-ui/client` keeps the implementation thin and GA-specific. |
| **iframe + URL params (Streamlit panels)** | Suitable for Streamlit only; not agent-native. AG-UI is the right long-term pattern for React. |
| **Hardcode quality from chord symbol strings** | Domain violation; quality must come from `GaParseChord` / `ChordInContext.function`. |

---

## System-Wide Impact

### Interaction Graph

```
User input
  → POST /api/chatbot/chat/agui/stream (new)
  → ILlmConcurrencyGate.TryEnterAsync (shared with existing /stream endpoint — same 3-slot pool)
  → ProductionOrchestrator.AnswerAsync
    → SemanticRouter (embedding → LLM → keyword fallback)
    → [TheoryAgent | TabAgent | TechniqueAgent | ...]
    → IChatClient.GetResponseAsync (LLM call, blocks until complete)
  → AgUiEventWriter
    → Response.WriteAsync (SSE frames)
    → Response.Body.FlushAsync (per event)
  → HttpAgent (client)
    → AgentSubscriber callbacks
    → React state updates (useGAAgent reducer)
    → Component re-renders (DiatonicChordTable, VexTabViewer)
```

### Error & Failure Propagation

| Failure point | Handling |
|---|---|
| Concurrency gate full | Emit `RUN_ERROR { code: "SERVICE_BUSY" }` — never 503 after headers committed |
| Orchestrator throws | Catch in controller; emit `RUN_ERROR`; log via ILogger |
| LLM timeout | `CancellationToken` propagated from HTTP request; orchestrator respects it |
| Client disconnects | `CancellationToken` fires; `FlushAsync` throws; controller disposes cleanly |
| JSON patch invalid | Frontend: `applyPatch` in try/catch; hold last-known-good state |
| `@ag-ui/client` can't connect | `onRunFailed` → surface error in `GAChatPanel`, reset `isStreaming` |

### State Lifecycle Risks

- `ProductionOrchestrator` is **Scoped** — lifetime is bounded to the HTTP request. No orphan state risk.
- `useGAAgent` maintains `GaAgentState` in React component tree. If `GAChatPanel` unmounts during streaming, the `AbortController` fires (via `useEffect` cleanup) and the subscription is torn down.
- `STATE_SNAPSHOT` is emitted at run start — frontend always has a known initial state before any `STATE_DELTA` arrives.

### API Surface Parity

- **Existing `/stream` endpoint**: Keep as-is for backward compatibility with current `ga-client` consumers. Do not modify its event format.
- **New `/agui/stream` endpoint**: AG-UI compliant; consumed by `useGAAgent` hook.
- **MCP tools** (`GaDiatonicChords`, `GaParseChord`): Unaffected — still used by Claude Code directly.
- **`GET /api/contextual-chords/keys/{key}`**: Still the source of truth for `ChordInContext[]`; the orchestrator calls this internally when theory analysis runs.

### Integration Test Scenarios

1. **Theory question → diatonic CUSTOM event**: Send "chords in G major" → assert `CUSTOM { name: "ga:diatonic" }` event arrives with 7 `ChordInContext` entries, `romanNumeral` non-null on all.
2. **Concurrency gate saturation**: With 3 concurrent AG-UI streams in flight, a 4th request must receive `RUN_ERROR { code: "SERVICE_BUSY" }` as the first event, not a connection error.
3. **Client disconnect mid-stream**: Disconnect TCP after `TEXT_MESSAGE_START` — assert no exception leaks in server logs, concurrency gate slot is released.
4. **STATE_DELTA round-trip**: Backend emits `STATE_DELTA [{ op: "replace", path: "/analysisPhase", value: "complete" }]` → frontend `GaAgentState.analysisPhase` becomes `"complete"` after event.
5. **Routing metadata before text**: Assert `STEP_STARTED` event arrives before first `TEXT_MESSAGE_CONTENT` event in all routing paths (semantic, LLM, keyword).

---

## Acceptance Criteria

### Functional

- [x] `POST /api/chatbot/chat/agui/stream` accepts `RunAgentInput` and emits a valid AG-UI event stream
- [x] Event sequence always begins with `RUN_STARTED` → `STATE_SNAPSHOT` → `STEP_STARTED` → text events → domain CUSTOM events → `RUN_FINISHED`
- [x] `CUSTOM "ga:diatonic"` is emitted when `TheoryAgent` handles the request and the response contains diatonic chord information
- [x] `ChordInContext.romanNumeral` and `ChordInContext.function` are non-null in all emitted `ga:diatonic` payloads
- [x] `useGAAgent` hook correctly applies `STATE_SNAPSHOT` and `STATE_DELTA` to `GaAgentState`
- [x] `DiatonicChordTable` renders with quality colors derived from `ChordInContext.function` (no string parsing)
- [x] Existing `/api/chatbot/chat/stream` endpoint is unmodified and all existing tests pass
- [x] `GAChatPanel` renders streaming text + `DiatonicChordTable` side by side when theory content is detected

### Non-Functional

- [x] All JSON field names in SSE events are camelCase (`messageId`, `threadId`, `runId`)
- [x] `X-Accel-Buffering: no` header is set on the new SSE endpoint
- [x] New C# types use `sealed record`, file-scoped namespaces, collection expressions
- [x] TypeScript types use `readonly` arrays and strict null safety — no `any`
- [x] Zero new compiler warnings in any touched file

### Quality Gates

- [ ] Integration test: theory question → `ga:diatonic` CUSTOM event with valid `ChordInContext[]`
- [ ] Integration test: concurrency gate saturation → `RUN_ERROR` as first event
- [ ] Integration test: `STATE_DELTA` round-trip from C# → React state
- [ ] `useGAAgent` unit test: subscriber callbacks fire in correct order
- [ ] `DiatonicChordTable` snapshot test: renders 7 chords from `ChordInContext[]` with correct roman numerals

---

## Critical Gaps Identified by SpecFlow Analysis

SpecFlow analysis surfaced 20 gaps. The five that block implementation are resolved here; the remainder are captured as acceptance criteria or future work.

### Resolved: New endpoint, not a replacement

**Decision**: New route `POST /api/chatbot/agui/stream`. Existing `/api/chatbot/chat/stream` is unchanged. Both share the same `ILlmConcurrencyGate` pool (3 slots combined).

### Resolved: Key extraction — extend `QueryFilters`

`ContextualChordService.GetChordsForKeyAsync(string keyName)` requires an explicit key string like `"G major"`. `QueryFilters` currently has no `Key` field. **Add `Key: string?` to `QueryFilters`** and update the `QueryUnderstandingService` extraction prompt to populate it. Write a unit test: `"show me chords in G major"` → `QueryFilters { Key = "G major" }`. This is a prerequisite for the AG-UI CUSTOM event emission and must be done before Phase 1.4.

### Resolved: ContextualChordService layering

`ContextualChordService` lives in `GaApi` — it cannot be injected into `GA.Business.Core.Orchestration` without a layer violation. **Inject `ContextualChordService` directly into the new AG-UI controller via constructor DI** (it's already registered in GaApi's DI container). The AG-UI emission logic lives in the controller, not in the orchestration library. This is consistent with how `ChatbotController` manages its own SSE emission today.

### Resolved: VexTabViewer data shape mismatch

`VexTabViewer.tsx` needs `str/fret` notation. `ChordInContext` has pitch names only. **On chord click, trigger a lazy fetch** to `GET /api/contextual-chords/voicings/{chordName}` which returns `VoicingWithAnalysis[]` with `Frets: int[]`. The first/easiest voicing (index 0, sorted by difficulty ascending) is converted to `str/fret` notation for `VexTabViewer`. Fix `VoicingFilterService`'s `ChordSymbolParser` to return `Result` instead of throwing `ArgumentException` (currently a 500 risk on unknown chord names).

### Resolved: TheoryAgent `Data` is always null

`TheoryAgent` instructs the LLM to return `"data": null`. The AG-UI CUSTOM event cannot be populated from `AgentResponse.Data`. **The AG-UI controller calls `ContextualChordService.GetChordsForKeyAsync(queryFilters.Key)` directly** when `QueryFilters.Key` is non-null, independent of the agent response. The agent provides the natural language answer; the controller provides the structured domain data.

### Resolved: runId source

Use `Activity.Current?.TraceId.ToString()` as `runId`, falling back to `Guid.NewGuid().ToString("N")`. This makes AG-UI sessions directly correlatable with Jaeger traces.

### Remaining gaps (acceptance criteria)

- **Fast-path skill path**: Emit `RUN_STARTED` / `RUN_FINISHED` with text events only; suppress CUSTOM event; hide `DiatonicChordTable` when `chords` is empty.
- **RxJS transitive dep**: `@ag-ui/client` brings RxJS. The `useGAAgent` hook subscribes to the internal Observable inside `useEffect` and converts to `useReducer` state — RxJS is never in the component's public API surface.
- **Null `RomanNumeral`**: Render em dash; hide null `FunctionalDescription`.
- **`GaAgentState` reset semantics**: Each `RUN_STARTED` resets all state fields. CUSTOM events within a run replace (not append) `chords`.
- **Loading states**: `DiatonicChordTable` hidden while `analysisPhase !== "complete"`; `VexTabViewer` shows spinner during lazy voicing fetch.

---

## Dependencies & Prerequisites

| Dependency | Status |
|---|---|
| `@ag-ui/core` npm package | Install (v0.0.47) |
| `@ag-ui/client` npm package | Install (v0.0.47) |
| `ContextualChordsController.GetChordsForKeyAsync` | Exists — `GET /api/contextual-chords/keys/{key}` |
| `ChordInContext` record with `RomanNumeral`, `Function` | Exists — `Apps/ga-server/GaApi/Models/ContextualChords.cs` |
| `ProductionOrchestrator.AnswerAsync` | Exists — `Common/GA.Business.Core.Orchestration/Services/ProductionOrchestrator.cs` |
| `SseChunker` | Exists — `Apps/ga-server/GaApi/Helpers/SseChunker.cs` |
| `ILlmConcurrencyGate` | Exists — shared with ChatbotController |
| No `fast-json-patch` C# lib needed | Backend builds JSON Patch as anonymous objects; frontend SDK applies via built-in `fast-json-patch` |

**Risk: `ChordInContext.Function` field on theory responses**: The `ProductionOrchestrator` currently doesn't call `ContextualChordsController` — it routes to `TheoryAgent` which uses a prompt. `ChatResponse.Candidates` are RAG voicings, not the same as `ChordInContext[]` from the contextual chord service. **The AG-UI endpoint may need to call `GET /api/contextual-chords/keys/{inferredKey}` post-hoc** when the agent response indicates a key analysis was performed. This requires key extraction from `ChatResponse.QueryFilters` or a post-hoc call. This is the highest-risk design decision — flag for resolution during implementation.

---

## Risk Analysis

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| No true token streaming — text feels chunky | High (by design) | Low | `SseChunker` already handles this; same UX as today |
| `ChordInContext[]` not directly available from orchestrator | Resolved | — | AG-UI controller calls `ContextualChordService` directly when `QueryFilters.Key` is non-null |
| Key not extracted from user intent | High | High | Add `Key: string?` to `QueryFilters`; update `QueryUnderstandingService` prompt — **prerequisite before Phase 1.4** |
| `VoicingFilterService` throws `ArgumentException` on bad chord name | High | Medium | Change to ROP (`Result<T>`) pattern before lazy voicing fetch is wired |
| CORS misconfiguration blocks Vite → GaApi | Medium | High | Add `localhost:5174` and `localhost:8501` to `AllowAll` CORS policy |
| `@ag-ui/client` brings RxJS as transitive dep | High (by design) | Low | Convert Observable → `useReducer` inside hook; RxJS never in public API |
| Two SSE parsers in ga-client create migration risk | Medium | Low | Keep old endpoint alive; migrate `chatAtoms.ts` incrementally |
| `ContextualChordService` not in orchestration layer | Resolved | — | Inject into AG-UI controller directly; stays in GaApi |
| Fast-path skill path yields no `ChordInContext` | High | Low | `DiatonicChordTable` hidden when `chords` is empty; valid UI state |

---

## Future Considerations

- **True token streaming**: Upgrade `GuitarAlchemistAgentBase.ChatAsync` to use `IChatClient.GetStreamingResponseAsync` (`IAsyncEnumerable<StreamingChatCompletionUpdate>`). Thread through `ProductionOrchestrator`. This would make `TEXT_MESSAGE_CONTENT` events token-by-token rather than sentence-by-sentence.
- **Streamlit Python consumer**: AG-UI Python SDK exists. A thin Streamlit harness can call the SSE endpoint and embed React panels via `st.components.v1.iframe()` for research/exploration.
- **`useCoAgent` migration**: If CopilotKit is adopted later, `useGAAgent` maps directly to `useCoAgent`. The state interface and CUSTOM event names remain unchanged.
- **`ga:optic-k` CUSTOM event**: Emit OPTIC-K embedding clusters as a CUSTOM event when the RAG path returns semantic neighbors; wire to `OpticKHierarchy` visualization component (future).

---

## Documentation Plan

- Update `Common/GA.Business.ML/Documentation/Architecture/Chatbot_Technical_Roadmap.md` — add AG-UI as the standard agent-to-UI transport
- Add `docs/solutions/architecture/ag-ui-sse-endpoint-pattern.md` to document the `AgUiEventWriter` + camelCase + `X-Accel-Buffering` pattern for future endpoints
- Update `CLAUDE.md` — add AG-UI endpoint to the Monitoring section with the new route

---

## Sources & References

### Internal References

- Existing SSE endpoint pattern: `Apps/ga-server/GaApi/Controllers/ChatbotController.cs`
- SSE chunk helper: `Apps/ga-server/GaApi/Helpers/SseChunker.cs`
- `ChordInContext` model: `Apps/ga-server/GaApi/Models/ContextualChords.cs`
- Frontend SSE parser (reference): `Apps/ga-client/src/services/chatApi.ts`
- Frontend state atoms: `Apps/ga-client/src/store/chatAtoms.ts`
- `ProductionOrchestrator`: `Common/GA.Business.Core.Orchestration/Services/ProductionOrchestrator.cs`
- Routing metadata first-event rule: `docs/solutions/architecture/orchestration-library-extraction-gachatbot.md`
- Security guards (dev-only endpoints): `docs/solutions/compound-reviews/2026-03-07-ce-review-security-arch-hygiene.md`

### External References

- AG-UI protocol repo: https://github.com/ag-ui-protocol/ag-ui
- AG-UI TypeScript SDK (`@ag-ui/core`, `@ag-ui/client`): `sdks/typescript/packages/`
- AG-UI Go SSE writer (wire format reference): `sdks/community/go/pkg/encoding/sse`
- JSON Patch RFC 6902: https://datatracker.ietf.org/doc/html/rfc6902
- Java Spring AG-UI reference: `sdks/community/java/servers/spring`
