---
title: "feat: Algedonic visual signals — ripple effects, edge propagation, compounding surge"
type: feat
status: active
date: 2026-03-26
origin: docs/brainstorms/2026-03-26-algedonic-visual-signals-requirements.md
---

# feat: Algedonic visual signals — ripple effects, edge propagation, compounding surge

## Overview

Add visual algedonic feedback to the Prime Radiant 3D graph: node ripples (red=pain, gold=pleasure), signal propagation along graph edges, and dramatic compounding surges when multiple positive signals fire together. Wire AlgedonicPanel to real SignalR-pushed signals with 11 named channels. Add AlgedonicPanel to the icon rail.

## Problem Frame

Prime Radiant shows static health status on nodes but has no visual feedback for discrete governance events. When a belief collapses or resilience recovers, there's no visual signal. The AlgedonicPanel exists with mock data but isn't connected to real events. Users can't see pain/pleasure propagate through the governance network. (see origin: docs/brainstorms/2026-03-26-algedonic-visual-signals-requirements.md)

## Requirements Trace

- R1. Node ripple effect — expanding ring, red=pain, gold=pleasure, ~2s fade
- R2. Edge propagation — signal travels along connected edges after ripple
- R3. Compounding surge — 3+ pleasure signals in 10s → cluster-wide gold wave
- R4. 11 algedonic channels (6 pain, 5 pleasure including `compounding_surge`)
- R5. Real-time SignalR delivery via `GovernanceHub.AlgedonicSignal` event
- R6. AlgedonicPanel in icon rail with live signal timeline
- R7. Signal persistence in `governance/demerzel/state/signals/`

## Scope Boundaries

- No changes to governance health calculation
- No new SignalR hub — extend existing `GovernanceHub`
- No audio/haptic feedback
- Compounding detection is frontend-only (sliding window)
- No changes to harm taxonomy or constitutional documents

## Context & Research

### Relevant Code and Patterns

- **ForceRadiant.tsx**: Node rendering with HEALTH_PROMINENCE — size, glow, particles, pulse speed all driven by health status. Three-layer rendering: core mesh + nebula sprite + orbiting particles
- **ForceRadiant animation loop**: `fg.onEngineTick()` callback runs per frame — existing pattern for injecting visual effects
- **GovernanceHub.cs**: `BroadcastGraphUpdate`, `BroadcastNodeChanged`, `BroadcastBeliefUpdate` — pattern for adding `BroadcastAlgedonicSignal`
- **GovernanceWatcherService.cs**: Watches governance directory, debounces, broadcasts — pattern for triggering algedonic signals
- **BeliefStateService.cs**: Reads/writes belief JSON files — can detect state transitions that map to signals
- **DataLoader.ts**: SignalR client with `on('GraphUpdate')`, `on('BeliefUpdate')` — pattern for adding `on('AlgedonicSignal')`
- **AlgedonicPanel.tsx**: Full UI component with signal timeline, filters, color coding — needs to swap mock data for SignalR events
- **IconRail.tsx**: `PanelId` union type and `RAIL_ITEMS` array — add `'algedonic'` entry
- **3d-force-graph API**: `fg.linkColor()` accepts accessor function called per frame — supports dynamic per-link coloring for edge propagation

### Existing Signal Infrastructure

- Signal files exist in `governance/demerzel/state/conscience/signals/` with format: `sig-{type}-{date}-{seq}.signal.json`
- `AlgedonicSignal` TypeScript interface already defined in `AlgedonicPanel.tsx` with `id`, `timestamp`, `signal`, `type`, `source`, `severity`, `status`, `description`
- C# `BeliefState` record has `TruthValue` (T/F/U/C) and `Confidence` — transitions between these map to algedonic channels

## Key Technical Decisions

- **Ripple via RingGeometry**: Add expanding `THREE.RingGeometry` meshes as children of the graph scene. Animate scale up + opacity down over 2 seconds, then dispose. Lightweight — no custom shaders, consistent with existing Three.js approach.
- **Edge propagation via linkColor**: Use `fg.linkColor()` accessor to check if a link is currently carrying a signal. Store active signal propagations in a Map keyed by link ID with a start time. On each frame, links within the propagation window get colored red/gold; expired entries are removed. No geometry changes needed.
- **Compounding surge — frontend detection**: Keep a sliding window array of recent pleasure signals (last 10 seconds). When length >= 3, trigger a surge by creating a larger, brighter ripple on all nodes in the connected cluster. Detected in ForceRadiant, no backend coordination needed.
- **Backend signal emission**: `AlgedonicSignalService` compares belief state before/after update. Mapping: confidence drop > 0.2 → `belief_collapse`, truth value T→C → `policy_violation`, LOLLI increase → `lolli_inflation`, confidence rise > 0.2 → `resilience_recovery`, etc. Emits via `GovernanceHub.BroadcastAlgedonicSignal`.
- **Signal persistence**: Write to `governance/demerzel/state/signals/`. Retain last 100 files. Load recent signals via REST endpoint on initial connect.

## Open Questions

### Resolved During Planning

- **Ripple rendering approach**: `THREE.RingGeometry` with animated scale/opacity. Cheaper than shaders, matches existing Three.js patterns. Disposed after animation completes.
- **Edge propagation performance**: `linkColor` accessor is already called per frame by 3d-force-graph. Adding a Map lookup per link is O(1) per link — negligible overhead.
- **Signal mapping logic**: Compare old vs new `TruthValue` and `Confidence` in `BeliefStateService.UpdateBelief`. Each transition maps to exactly one channel.
- **Signal file naming**: `sig-{channel}-{ISO-date}-{seq}.signal.json`. Retention: 100 most recent files.

### Deferred to Implementation

- **Exact ripple animation curve**: ease-out timing may need visual tuning
- **Compounding surge visual intensity**: number of particles, bloom boost — tune in-browser
- **Edge propagation speed**: how fast the color wave travels along links (try 500ms per hop)

## High-Level Technical Design

> *This illustrates the intended approach and is directional guidance for review, not implementation specification. The implementing agent should treat it as context, not code to reproduce.*

```
Signal Flow:

  governance/ file change
       ↓
  GovernanceWatcherService
       ↓
  BeliefStateService.UpdateBelief(old, new)
       ↓
  AlgedonicSignalService.Evaluate(old, new) → AlgedonicSignal?
       ↓
  GovernanceHub.BroadcastAlgedonicSignal(signal)
       ↓
  SignalR → DataLoader.ts on('AlgedonicSignal')
       ↓
  ┌─────────────────────────┐
  │ ForceRadiant            │
  │  • Add ripple ring mesh │
  │  • Queue edge propagation│
  │  • Check compounding    │
  │    window → surge?      │
  └─────────────────────────┘
       ↓
  AlgedonicPanel (in rail) — append to timeline
```

## Implementation Units

### Phase 1: Backend — Signal Service + SignalR

- [ ] **Unit 1: AlgedonicSignalService (C#)**

  **Goal:** Create a service that evaluates belief state transitions and emits algedonic signals.

  **Requirements:** R4, R5

  **Dependencies:** None

  **Files:**
  - Create: `Apps/ga-server/GaApi/Services/AlgedonicSignalService.cs`
  - Create: `Apps/ga-server/GaApi/Models/AlgedonicSignalDto.cs`
  - Modify: `Apps/ga-server/GaApi/Extensions/AIServiceExtensions.cs` (register service)

  **Approach:**
  - `AlgedonicSignalDto` record: `Id`, `Timestamp`, `Signal` (channel name), `Type` (pain/pleasure), `Source`, `Severity`, `Status`, `Description`, `NodeId` (affected governance node)
  - `AlgedonicSignalService.EvaluateTransition(BeliefState oldState, BeliefState newState)` → returns `AlgedonicSignalDto?`
  - Mapping rules: confidence drop > 0.2 → `belief_collapse` (pain/warning), T→C → `policy_violation` (pain/emergency), LOLLI increase → `lolli_inflation` (pain/warning), confidence rise > 0.2 → `resilience_recovery` (pleasure/info), T→T with new evidence → `domain_convergence` (pleasure/info), etc.
  - `PersistSignal(signal)` → writes JSON to `governance/demerzel/state/signals/`, prunes to 100 files

  **Patterns to follow:**
  - `BeliefStateService.cs` for file I/O pattern
  - Existing signal files in `governance/demerzel/state/conscience/signals/` for naming convention

  **Test scenarios:**
  - Confidence drop 0.8→0.5 produces `belief_collapse` signal
  - T→C produces `policy_violation` signal
  - No change produces no signal
  - Signal file is persisted to disk
  - Oldest signals pruned when > 100

  **Verification:**
  - Service compiles, tests pass, signal files appear in signals directory.

- [ ] **Unit 2: GovernanceHub AlgedonicSignal broadcast + REST endpoint**

  **Goal:** Add `AlgedonicSignal` event to SignalR hub and a REST endpoint for loading recent signals.

  **Requirements:** R5, R7

  **Dependencies:** Unit 1

  **Files:**
  - Modify: `Apps/ga-server/GaApi/Hubs/GovernanceHub.cs`
  - Modify: `Apps/ga-server/GaApi/Services/GovernanceWatcherService.cs`
  - Modify: `Apps/ga-server/GaApi/Services/BeliefStateService.cs`
  - Create: `Apps/ga-server/GaApi/Controllers/AlgedonicController.cs`

  **Approach:**
  - Add `BroadcastAlgedonicSignal(IHubContext, AlgedonicSignalDto)` static method to GovernanceHub
  - In `BeliefStateService.UpdateBelief`: after updating belief, call `AlgedonicSignalService.EvaluateTransition` → if signal, persist + broadcast
  - In `GovernanceWatcherService`: after detecting governance file changes, evaluate if any algedonic signals should fire (e.g., schema drift detected, test file changes)
  - `AlgedonicController`: `GET /api/algedonic/recent` returns last 50 signals from persisted files
  - Frontend subscribes to `AlgedonicSignal` event via existing DataLoader SignalR connection

  **Patterns to follow:**
  - `GovernanceHub.BroadcastBeliefUpdate` for broadcast pattern
  - `ChatbotController` for REST endpoint pattern

  **Test scenarios:**
  - Updating a belief triggers broadcast to SignalR clients
  - GET /api/algedonic/recent returns persisted signals in reverse chronological order
  - Empty signals directory returns empty array

  **Verification:**
  - Backend compiles. REST endpoint returns signal data. SignalR event fires on belief change.

### Phase 2: Frontend — Visual Effects

- [ ] **Unit 3: DataLoader SignalR + AlgedonicPanel wiring**

  **Goal:** Connect AlgedonicPanel to real SignalR events. Add algedonic icon to IconRail.

  **Requirements:** R5, R6

  **Dependencies:** Unit 2

  **Files:**
  - Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/DataLoader.ts`
  - Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/AlgedonicPanel.tsx`
  - Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/IconRail.tsx`
  - Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/ForceRadiant.tsx`

  **Approach:**
  - DataLoader: add `on('AlgedonicSignal', callback)` handler in SignalR setup. Add `onAlgedonicSignal` to `LiveDataConfig`.
  - DataLoader: on initial connect, fetch `/api/algedonic/recent` and call callback with each
  - AlgedonicPanel: replace mock data with a `signals` prop (array of `AlgedonicSignal`). Parent manages state, pushes new signals from DataLoader callback.
  - IconRail: add `'algedonic'` to `PanelId` union. Add heartbeat/wave SVG icon to `RAIL_ITEMS`.
  - ForceRadiant: add `algedonicSignals` state array. Wire DataLoader callback to push new signals. Pass to AlgedonicPanel. Render AlgedonicPanel in side-panel area when `activePanel === 'algedonic'`.

  **Patterns to follow:**
  - DataLoader's existing `on('BeliefUpdate')` pattern
  - ForceRadiant's existing panel wiring for Activity, Backlog, etc.

  **Test scenarios:**
  - AlgedonicPanel renders live signals from backend
  - New signal appears in panel within 1 second of push
  - Recent signals load on initial page load
  - Algedonic icon appears in rail and opens panel

  **Verification:**
  - Frontend builds. AlgedonicPanel shows real signals. Rail icon works.

- [ ] **Unit 4: Node ripple effect**

  **Goal:** When an algedonic signal arrives, render an expanding ripple ring on the affected graph node.

  **Requirements:** R1

  **Dependencies:** Unit 3

  **Files:**
  - Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/ForceRadiant.tsx`

  **Approach:**
  - When a new algedonic signal arrives, find the 3D node object by `nodeId` in the graph scene
  - Create a `THREE.RingGeometry` mesh at the node's world position. Color: red (`#FF4444`) for pain, gold (`#FFD700`) for pleasure. Material: `MeshBasicMaterial` with `transparent: true`, `side: DoubleSide`, additive blending.
  - Animate in the `onEngineTick` callback: scale ring from 1→8 over 2 seconds, opacity from 0.8→0 (ease-out). Remove from scene + dispose when done.
  - Store active ripples in a `Map<string, { mesh, startTime, color }>`.

  **Patterns to follow:**
  - Existing particle/sprite creation in ForceRadiant's node rendering
  - `onEngineTick` animation loop for health-driven pulsing

  **Test scenarios:**
  - Pain signal → red ripple expands from node
  - Pleasure signal → gold ripple
  - Ripple cleans up after 2 seconds (no memory leak)
  - Multiple simultaneous ripples on different nodes
  - Signal for unknown nodeId → no crash, silently skip

  **Verification:**
  - Visual: ripple ring visible and fades. No console errors. No memory growth after repeated signals.

- [ ] **Unit 5: Edge propagation effect**

  **Goal:** After ripple, signal color propagates along connected edges from the source node outward.

  **Requirements:** R2

  **Dependencies:** Unit 4

  **Files:**
  - Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/ForceRadiant.tsx`

  **Approach:**
  - When a ripple starts, find all edges connected to the source node. Record them in a propagation queue with `{ linkId, startTime, color, hops }`.
  - For each hop, after a delay (e.g., 500ms per hop), add the next ring of connected edges to the queue. Max depth: 3 hops.
  - In `fg.linkColor()` accessor: check if link ID is in the active propagation map. If yes and within the animation window (1 second per link), return the signal color. Otherwise return normal color.
  - Expired propagation entries cleaned up per frame.

  **Patterns to follow:**
  - `fg.linkColor()` dynamic accessor — already used for health-based edge coloring

  **Test scenarios:**
  - Signal propagates to immediate neighbors first, then 2nd hop, then 3rd
  - Edge color returns to normal after propagation passes
  - Multiple concurrent propagations don't interfere
  - Dense clusters don't cause frame drops (test with full governance graph)

  **Verification:**
  - Visual: wave of color travels outward from signal source along edges. Performance stays above 30 FPS.

- [ ] **Unit 6: Compounding surge**

  **Goal:** Detect compounding and trigger a dramatic cluster-wide gold wave.

  **Requirements:** R3

  **Dependencies:** Unit 5

  **Files:**
  - Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/ForceRadiant.tsx`

  **Approach:**
  - Maintain a sliding window array of pleasure signal timestamps (last 10 seconds).
  - On each new pleasure signal, push timestamp and filter out entries > 10s old.
  - If window length >= 3, trigger compounding surge:
    - Emit a `compounding_surge` algedonic signal (frontend-generated, pleasure/info)
    - Create oversized ripple rings on all nodes in the connected cluster of the most recent signal source
    - Temporarily boost bloom pass strength (0.6 → 1.2 for 3 seconds, then ease back)
    - Edge propagation with max depth (all reachable edges)
  - Surge is visually distinct: larger rings, brighter color (#FFFFFF gold-white), longer duration (3s vs 2s)

  **Patterns to follow:**
  - Existing bloom pass adjustment in ForceRadiant (adaptive quality system)
  - Ripple and propagation from Units 4-5

  **Test scenarios:**
  - 3 pleasure signals in 8 seconds → surge triggers
  - 2 pleasure signals → no surge
  - Surge creates larger, brighter visual than individual ripples
  - Bloom returns to normal after surge completes
  - Pain signals don't count toward compounding window

  **Verification:**
  - Visual: compounding surge is dramatically different from single ripples. Bloom recovers. No performance regression.

## System-Wide Impact

- **Interaction graph:** `BeliefStateService.UpdateBelief` gains a side-effect (algedonic evaluation + broadcast). `GovernanceWatcherService` file-change handler gains signal evaluation.
- **Error propagation:** Signal evaluation failures should log and continue — never block belief updates or governance watcher. `AlgedonicSignalService.EvaluateTransition` returns null on error.
- **State lifecycle risks:** Signal file writes are append-only with pruning. If write fails, signal is still broadcast via SignalR (ephemeral is acceptable). No transactional requirement.
- **API surface parity:** New REST endpoint `GET /api/algedonic/recent`. New SignalR event `AlgedonicSignal`. Both are additive — no breaking changes.
- **Integration coverage:** End-to-end: governance file change → signal broadcast → ripple on graph. Manual visual testing is primary. Backend unit tests cover signal mapping logic.

## Risks & Dependencies

- **3d-force-graph scene access**: Adding RingGeometry meshes to the scene requires `fg.scene()` access, which exists and is used for solar system objects. Should work.
- **Frame rate with many concurrent animations**: Ripples, edge propagation, and surge all animate per frame. Cap concurrent ripples at 10 to prevent frame drops. Edge propagation limited to 3 hops.
- **SignalR reconnection**: If client disconnects and reconnects, it fetches recent signals from REST endpoint — no signals lost.
- **Icon rail capacity**: Adding a 7th icon to the rail. Current rail has 6 icons — 7 should fit within the vertical space.

## Sources & References

- **Origin document:** [docs/brainstorms/2026-03-26-algedonic-visual-signals-requirements.md](docs/brainstorms/2026-03-26-algedonic-visual-signals-requirements.md)
- Related code: `ForceRadiant.tsx`, `GovernanceHub.cs`, `BeliefStateService.cs`, `AlgedonicPanel.tsx`, `DataLoader.ts`, `IconRail.tsx`
- Related backlog: "Algedonic channels" item in BACKLOG.md
- Governance harm taxonomy: `governance/demerzel/constitutions/harm-taxonomy.md`
