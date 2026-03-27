---
title: "feat: Living Cosmos — Prime Radiant mega-feature"
type: feat
status: active
date: 2026-03-25
origin: docs/brainstorms/2026-03-25-living-cosmos-requirements.md
---

# Living Cosmos — Prime Radiant Mega-Feature

## Overview

Transform the Prime Radiant from a static governance graph viewer into a living, data-driven cosmos. Four pillars deliver simultaneously: (1) **Living Graph** — adaptive node sizing, activity effects, edge undulation, IXql-driven visualization control; (2) **Cosmos & Cinematics** — planet fly-to animations, hover tooltips, realistic sun, UI polish; (3) **Hari Seldon Analytics** — ghost trail predictions, psychohistory dashboard, Jarvis space station; (4) **Backend Connectivity** — real Demerzel chat via SSE, live health polling, IXql server execution.

Key decisions carried from origin document (see origin: `docs/brainstorms/2026-03-25-living-cosmos-requirements.md`):
- IXql runtime: TS subset parser (FParsec blocks Fable), tree-sitter WASM for full IXql later
- Predictions: Ghost trails with scrubable timeline
- Station: Dual representation (project layers + governance categories)
- All four pillars equal priority

## Problem Statement

- Nodes are statically sized by type — no visual signal for importance, activity, or staleness
- Edges are static curves — no visual signal for data flow or broken references
- IXql exists as a complete language with parser, grammar, and examples, but has zero integration with the 3D visualization
- Markov prediction data model exists (`HealthMetrics.markovPrediction`) but is unpopulated and unvisualized
- ChatWidget uses hardcoded mock responses — not connected to the real chatbot backend
- Solar system lacks interactivity — no cinematics, no hover tooltips, unrealistic sun
- Panel overlaps and scrollbar issues on various viewports

## Proposed Solution

### Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    ForceRadiant.tsx                       │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌─────────┐ │
│  │ Living   │  │ Cosmos   │  │ Seldon   │  │Backend  │ │
│  │ Graph    │  │ Cinema   │  │ Analytics│  │Connect  │ │
│  │          │  │          │  │          │  │         │ │
│  │ NodeSizer│  │ PlanetCam│  │ GhostTrail│ │SSE Chat │ │
│  │ FxEngine │  │ Tooltip  │  │ Dashboard│  │HealthPoll│ │
│  │ EdgeWave │  │ SunShader│  │ Station  │  │IxqlAPI  │ │
│  │ IxqlCtrl │  │ UIFix    │  │          │  │         │ │
│  └──────────┘  └──────────┘  └──────────┘  └─────────┘ │
│                                                          │
│  ┌──────────────────────────────────────────────────┐    │
│  │            Shared: NodeLookupMap, QualityGate     │    │
│  └──────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────┘
```

### Implementation Phases

#### Phase 1: Foundation & Performance (Critical Path)

Must ship first — fixes performance bottlenecks and builds shared infrastructure all other phases depend on.

**1.1 Node Lookup Map** — `ForceRadiant.tsx`
Replace all O(n) `nodes.find()` calls in link callbacks (linkColor, linkWidth, linkDirectionalParticle*) and edge undulation tick with a pre-built `Map<string, GraphNode>`. Currently ~1,200 O(n) searches per frame = 8M comparisons/sec. With a Map: O(1).

```typescript
// Build once after toForceData()
const nodeMap = new Map<string, GraphNode>();
forceData.nodes.forEach(n => nodeMap.set(n.id, n));
```

**1.2 Quality Gate for Solar System** — `ForceRadiant.tsx:805`
Skip `updateSolarSystem()` at `low` quality. At `medium`, update every 3rd frame.

```typescript
if (qualityLevel !== 'low') {
  if (qualityLevel === 'high' || frameCount % 3 === 0) {
    updateSolarSystem(solarSystem, t);
  }
}
```

**1.3 Fix Hover Scale Override** — `ForceRadiant.tsx`
The tick handler's breathing animation (`group.scale.setScalar(breathe)`) overrides hover scale every frame. Store `userData.isHovered` and skip breathing when hovered.

**1.4 Cancel Auto-Zoom on User Interaction** — `ForceRadiant.tsx`
The 2-second `setTimeout` for auto-zoom on load fires even if the user has already clicked. Store the timeout ID and clear it on any `onNodeClick`, `onBackgroundClick`, or `onNodeDrag`.

**1.5 UI Panel Z-Index & Overflow Fix (R9)** — `styles.css`
- Set `overflow: hidden` on `.prime-radiant__container`
- Establish z-index hierarchy: tooltips (1000) > panels (100) > nav bar (50) > canvas (0)
- Add `max-height` + `overflow-y: auto` to ActivityPanel and DetailPanel
- Test responsive breakpoints: panels stack or collapse below 768px

**Files:** `ForceRadiant.tsx`, `styles.css`

---

#### Phase 2: Living Graph (R1–R3)

**2.1 Adaptive Node Sizing (R1)** — `ForceRadiant.tsx:createNodeObject`

Add importance score computation to the tick handler. Store mutable `userData.targetScale` on each node group. Lerp toward target each frame.

```typescript
// Importance formula (see origin: R1)
// importance = 0.4 * normEdgeCount + 0.3 * normErgol + 0.2 * resilience + 0.1 * (1 - staleness)
const edgeCounts = new Map<string, number>();
forceData.links.forEach(l => {
  edgeCounts.set(l.source, (edgeCounts.get(l.source) ?? 0) + 1);
  edgeCounts.set(l.target, (edgeCounts.get(l.target) ?? 0) + 1);
});
const maxEdges = Math.max(...edgeCounts.values(), 1);
```

In tick: compute importance, derive `targetScale = baseScale * (0.7 + importance * 0.6)`, lerp `currentScale` toward it at rate 0.05/frame.

**2.2 Activity-Based Node Effects (R2)** — `ForceRadiant.tsx` tick handler

- `ergolCount > 0`: multiply particle orbit speed by `1 + log2(ergolCount) * 0.3`
- `staleness > 0.5`: multiply opacity by `1 - (staleness - 0.5)`, slow spin by 0.5x
- `lolliCount > 0`: spawn `min(lolliCount, 5)` red decay particles drifting outward (reuse `THREE.Points` pattern from existing orbital dust)
- Recent commits: pulse brightness proportional to recency (needs `metadata.lastCommitAge` field — default to staleness inverse for now)

**2.3 Snake-Like Edge Undulation (R3)** — `ForceRadiant.tsx` tick handler

Currently `link._curvature` is set via sinusoidal modulation gated by `srcActive && tgtActive`. Extend to all edge types with type-specific parameters:

| Edge Type | Amplitude | Frequency | Style |
|---|---|---|---|
| `pipeline-flow` | 0.3 | 0.5 Hz | Smooth sine wave, source→target |
| `constitutional-hierarchy` | 0.15 | 0.2 Hz | Stately golden wave |
| `cross-repo` | 0.05 | 2.0 Hz | Shimmer (rapid oscillation) |
| `lolli` | 0.2 | random | Erratic jitter |
| `policy-persona` | 0.1 | 0.4 Hz | Gentle pulse |

Containment sphere breathing: scale oscillation 0.98–1.02, period 8s (already partially implemented in `HealthOverlay.ts:animateContainmentSphere`).

**Files:** `ForceRadiant.tsx`, `types.ts` (add `lastCommitAge` to metadata)

---

#### Phase 3: IXql Control (R4–R5)

**3.1 IXql Subset Parser (R5)** — New file: `IxqlControlParser.ts`

Minimal parser for visualization control commands. Grammar:

```
command    := SELECT target WHERE predicate SET assignments | RESET
target     := "nodes" | "edges"
predicate  := field comparator value (AND predicate)?
field      := dotted.path (e.g., "health.staleness", "type", "name")
comparator := ">" | "<" | "=" | ">=" | "<=" | "!=" | "~" (regex match)
value      := number | quoted_string
assignments := assignment ("," assignment)*
assignment  := visual_prop "=" value
visual_prop := "glow" | "pulse" | "size" | "color" | "visible" | "opacity" | "speed"
```

Returns `{ target, predicate, assignments }` or parse error. ~150 lines of TS.

**3.2 IXql Command Input (R4)** — New component: `IxqlCommandInput.tsx`

Minimal command palette at bottom of Prime Radiant (above planet bar). Monospace input, gold border, semi-transparent black background. Toggled via keyboard shortcut (`` ` `` backtick or `Ctrl+I`).

On submit: parse → evaluate predicate against graph nodes/edges → apply visual overrides via `userData.ixqlOverrides` on matching Three.js objects → tick handler reads overrides and applies them (glow color, pulse speed, size multiplier, opacity).

`RESET` clears all overrides.

**3.3 IXql Node Detail Integration** — `DetailPanel` modification

When a node of type `ixql` is selected and has `metadata.ixqlSource`, show a "View Pipeline" button that opens a slide-over panel with `IxqlViewer` rendering the source.

**Files:** `IxqlControlParser.ts` (new), `IxqlCommandInput.tsx` (new), `ForceRadiant.tsx`, `DetailPanel.tsx`, `styles.css`

---

#### Phase 4: Cosmos & Cinematics (R6–R8)

**4.1 Planet Cinematics (R6)** — `ForceRadiant.tsx` + `SolarSystem.ts`

On planet click (nav bar or direct raycast):
1. Camera swoops to planet over 1.5s using `fg.cameraPosition()` with target = planet's current world position
2. Time multiplier increases 10x (pass `time * 10` to `updateSolarSystem` during cinematic mode)
3. Orbit trail opacity lerps from 0.08 to 0.4
4. Dotted trajectory line appears (next 90 degrees of orbit as dashed `THREE.Line`)
5. ESC or second click returns to overview (lerp all values back)

Track planet world positions each tick via `planet.getWorldPosition(vec3)` so fly-to targets the planet's actual position at animation end.

**4.2 Planet/Moon Hover Tooltips (R7)** — `ForceRadiant.tsx`

Add a `Raycaster` on `mousemove` that tests against solar system planet/moon meshes. On hit, show a CSS tooltip near cursor:

```html
<div class="prime-radiant__planet-tooltip">
  <strong>Saturn</strong>
  <span>9.54 AU | 29.5 yr orbit | 116,460 km</span>
</div>
```

Minimal, semi-transparent, monospace. Store astronomical data in `SolarSystem.ts` planet definitions.

**4.3 Sun Realism (R8)** — `SolarSystem.ts`

Replace the current `MeshBasicMaterial` sun with a custom shader:
- Animated granulation (FBM noise at surface scale)
- Limb darkening (darken toward edges)
- Corona rays (additive blended outer sphere with ray-march shader)
- Periodic flare particles (instanced billboards ejecting from random surface points)
- Proper bloom interaction (emissive intensity tuned for UnrealBloomPass)

**Files:** `SolarSystem.ts`, `ForceRadiant.tsx`, `styles.css`

---

#### Phase 5: Hari Seldon Analytics (R10–R12)

**5.1 Ghost Trail Predictions (R10)** — New file: `GhostTrail.ts`

For each node with populated `health.markovPrediction`:
- Create 3–5 translucent ghost copies of the node mesh at progressively faded opacity (0.5 → 0.1)
- Position each ghost at a slight offset along a predicted trajectory vector (derived from the node's velocity or a random outward direction)
- Color each ghost by predicted health: interpolate `[healthy, watch, warning, freeze]` colors weighted by the Markov probability distribution
- Ghosts are children of the node's Three.js group, gated by a `showPredictions` toggle

Markov prediction semantics: `[p(Healthy), p(Watch), p(Warning), p(Freeze)]` — matches the states in `transition-matrix.json`.

Timeline slider: React component overlaid on the 3D view. Controls prediction horizon (1–5 steps). Default: 1 step.

**5.2 Psychohistory Dashboard (R11)** — New component: `SeldonDashboard.tsx`

Opens when "Seldon" nav button is clicked (replaces current governance-center zoom behavior). Slide-over panel from the right:
- Aggregate health trajectory sparkline (last 10 cycles)
- Top 5 nodes at highest degradation risk (sorted by `p(Warning) + p(Freeze)`)
- Selected node's Markov transition table (4x4 grid with probabilities)
- "Seldon Says" — one-line prediction summary generated from the highest-risk node

Data sourced from `governance/demerzel/state/markov/` files loaded via `DataLoader.ts`.

**5.3 Jarvis Space Station (R12)** — New file: `SpaceStation.ts`

Procedural modular station in 3D:
- 5 core modules (BoxGeometry) = project layers: Core, Domain, Analysis, ML, Orchestration
- 4 wing modules = governance categories: Constitutions, Policies, Personas, Pipelines
- Central hub connecting all modules (CylinderGeometry)
- Rotating ring section for visual dynamism
- Status: lit module = build passing, dark/flickering = failing, under-construction = animated docking

Assembly animation: modules start detached, dock one by one over 30 seconds on first load.

HUD labels: `THREE.Sprite` text labels on each module showing component name and status.

Positioned in scene as HUD element (like solar system) at offset `(-8, 8, -20)` from camera.

**Files:** `GhostTrail.ts` (new), `SeldonDashboard.tsx` (new), `SpaceStation.ts` (new), `ForceRadiant.tsx`, `DataLoader.ts`, `styles.css`

---

#### Phase 6: Backend Connectivity (R13)

**6.1 SSE Chat Integration** — `ChatWidget.tsx`

Replace `askDemerzel()` mock with real SSE streaming:
- Use `fetch()` to POST to `/api/chatbot/chat/stream` with message + conversation history
- Parse SSE chunks, append to last assistant message in state
- `AbortController` to cancel in-flight streams on new message send
- Feature flag: `VITE_DEMERZEL_BACKEND_URL` env var — when absent, fall back to mock
- Auth: `bearerToken?: string` prop on `ChatWidgetProps` — passed as `Authorization: Bearer ${token}` header

**6.2 Live Health Polling** — `DataLoader.ts`

- Poll `/api/health` every 30 seconds
- On response, update health metrics on existing graph nodes **in-place** (mutate `node.health`, `node.healthStatus`, `node.color`)
- Do NOT re-set `data` prop (which would trigger full graph teardown/rebuild)
- Emit a `healthUpdated` event that the tick handler can read to trigger size/color transitions

**6.3 IXql Server Execution** — `IxqlCommandInput.tsx`

When a query starts with `@server:`, POST the full IXql text to `/api/ixql/execute` (new endpoint). Stream results back and apply as visualization overlay. Falls back to client-side parser for `SELECT/WHERE/SET` commands without the `@server:` prefix.

**Files:** `ChatWidget.tsx`, `DataLoader.ts`, `IxqlCommandInput.tsx`, `ForceRadiant.tsx`

---

## Alternative Approaches Considered

| Alternative | Reason Rejected |
|---|---|
| Fable WASM for IXql parser | FParsec dependency is not Fable-compatible (see origin) |
| React Three Fiber for solar system | ForceRadiant uses imperative `3d-force-graph`, mixing R3F would create two render loops |
| SignalR for chat (instead of SSE) | Hub requires `[Authorize]` — SSE endpoint is simpler, works with bearer token header |
| Separate `PredictionView` route | Ghost trails lose context without the graph; inline overlay preferred (see origin: ghost trails decision) |
| Blender-imported station models | Procedural geometry ships faster and has zero asset pipeline dependency (see origin: scope boundaries) |

## System-Wide Impact

### Interaction Graph
User clicks planet → nav bar onClick → `fg.cameraPosition()` → tick handler detects cinematic mode → time multiplier applied → `updateSolarSystem(t * 10)` → orbit trail opacity transition → ESC listener resets.

User types IXql → `IxqlCommandInput.onSubmit` → `IxqlControlParser.parse()` → predicate evaluation against `forceData.nodes` → matched nodes get `userData.ixqlOverrides` set → tick handler reads overrides → visual properties applied.

Health poll → `DataLoader.pollHealth()` → in-place node update → tick handler detects changed health → importance recalculated → size/color/opacity lerp transitions.

### Error & Failure Propagation
- SSE chat failure: catch in fetch, show error in ChatWidget, fall back to mock
- Health poll failure: skip update, retry next interval, show stale indicator
- IXql parse error: show error message below input, do not apply
- Ghost trail with empty `markovPrediction`: skip node, no visual artifact
- Solar system texture load failure: Three.js shows magenta (default), non-blocking

### State Lifecycle Risks
- **Health poll must NOT trigger graph rebuild**: Mutate nodes in-place, not `setData()`
- **IXql overrides must be clearable**: `RESET` command clears all `userData.ixqlOverrides`
- **Cinematic mode must be cancellable**: ESC clears time multiplier, trail opacity, trajectory lines
- **Station assembly animation**: run once on first load, store completion flag in `userData`

## Acceptance Criteria

### Functional Requirements
- [ ] Nodes visibly differ in size based on importance score — constitution nodes largest, peripheral test nodes smallest, with continuous variation within tiers
- [ ] Stale nodes (staleness > 0.5) are visibly dimmer and slower than active nodes
- [ ] Pipeline-flow edges undulate with visible sinusoidal motion
- [ ] Typing `SELECT nodes WHERE type='policy' SET glow=red` highlights all policy nodes red
- [ ] `RESET` command restores all nodes to original appearance
- [ ] Clicking "Earth" in planet bar swoops camera to Earth and accelerates time
- [ ] Hovering over Saturn shows "Saturn — 9.54 AU | 29.5 yr orbit" tooltip
- [ ] Sun has visible corona, granulation, and limb darkening
- [ ] Nodes with markovPrediction show colored ghost trails at 3 future positions
- [ ] Seldon dashboard shows top 5 at-risk nodes with probability distributions
- [ ] Space station has 9 modules corresponding to project layers and governance categories
- [ ] ChatWidget sends real messages to backend when `VITE_DEMERZEL_BACKEND_URL` is set
- [ ] ChatWidget falls back to mock when backend is unavailable
- [ ] No scrollbars appear on the Prime Radiant full-screen view
- [ ] Panels do not overlap each other on any viewport width

### Non-Functional Requirements
- [ ] 60fps maintained with all features active (~100 nodes, bloom, particles, solar system, station)
- [ ] IXql query parse + apply completes within 200ms
- [ ] Health poll does not cause visual stutter or graph rebuild
- [ ] SSE chat stream renders tokens within 50ms of receipt
- [ ] Ghost trails render at < 1ms per node per frame

### Quality Gates
- [ ] `npm run build` passes with no new errors
- [ ] `npm run lint` passes with no new warnings from changed files
- [ ] All existing ForceRadiant interactions (click, hover, orbit, search) still work
- [ ] Quality auto-degradation still triggers correctly at 35fps and 20fps thresholds
- [ ] No GPU memory leaks: station/solar system meshes properly disposed on unmount

## Critical Gaps Identified by SpecFlow Analysis

### Gap: Hover scale immediately overridden by breathing animation
**Resolution:** Phase 1.3 — store `userData.isHovered` flag, skip breathing scale when true.

### Gap: Per-link O(n) node lookups (8M comparisons/sec)
**Resolution:** Phase 1.1 — pre-built `Map<string, GraphNode>` for O(1) lookups.

### Gap: 2-second auto-zoom fires even after user interaction
**Resolution:** Phase 1.4 — cancel timeout on any user interaction.

### Gap: Solar system not quality-gated
**Resolution:** Phase 1.2 — skip at `low`, throttle at `medium`.

### Gap: No path from ixql node click to IxqlViewer
**Resolution:** Phase 3.3 — "View Pipeline" button in DetailPanel for ixql nodes.

### Gap: markovPrediction field unpopulated in live data
**Resolution:** Phase 5.1 — populate for at least the `pipe-markov` node; load from `predictions.json` in DataLoader.

### Gap: ChatWidget mock backend with no real connection
**Resolution:** Phase 6.1 — SSE streaming with feature flag fallback.

### Gap: Health data never refreshes at runtime
**Resolution:** Phase 6.2 — 30-second polling with in-place node updates.

### Remaining as acceptance criteria:
- [ ] Color is not the sole health signal — add shape or label redundancy for accessibility
- [ ] Keyboard shortcut (backtick) toggles IXql command input without mouse
- [ ] Multiple simultaneous camera animations are queued, not conflicting

## Dependencies & Prerequisites

| Dependency | Status |
|---|---|
| `3d-force-graph` library (imperative API) | Installed, working |
| NASA 2K planet textures | Downloaded to `public/textures/planets/` |
| IxqlParser.ts (existing TS parser) | Working, in `components/IxqlViewer/` |
| Markov state data format | Defined in `governance/demerzel/state/markov/` |
| SSE chat endpoint | Exists at `/api/chatbot/chat/stream` |
| Health REST endpoint | Exists at `/api/health` |
| tree-sitter IXql grammar | Exists but not compiled to WASM |

## Risk Analysis

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| 60fps not achievable with all features | Medium | High | Quality auto-degradation already exists; extend to solar system and station |
| Ghost trails clutter the visualization | Medium | Medium | Toggle via Seldon dashboard; default off, enable per-node or globally |
| IXql subset parser grows complex | Low | Medium | Strict grammar subset; full IXql deferred to tree-sitter WASM |
| SSE auth token management | Medium | Medium | Feature flag fallback to mock; `bearerToken` prop keeps auth external |
| Space station dominates visual attention | Low | Medium | Positioned behind graph; semi-transparent at distance; quality-gated |

## Future Considerations

- **tree-sitter WASM**: Compile IXql grammar to WASM for full pipeline visualization client-side
- **VR/AR mode**: WebXR integration for immersive governance exploration
- **Multi-user**: Shared cursor / collaborative exploration via WebRTC
- **Audio**: Sonification of health status changes, IXql query sounds
- **Blender station models**: Replace procedural geometry with detailed GLTF models
- **Time-travel**: Scrub through historical governance snapshots

## Documentation Plan

- Update `CLAUDE.md` Frontend Standards section if new component patterns emerge
- Add `PrimeRadiant/README.md` documenting the IXql command syntax
- Update `NAVIGATION.md` with `/demos/prime-radiant` route

## Sources & References

### Origin
- **Origin document:** [docs/brainstorms/2026-03-25-living-cosmos-requirements.md](docs/brainstorms/2026-03-25-living-cosmos-requirements.md) — Key decisions: IXql TS subset parser (FParsec blocks Fable), ghost trail predictions with scrubable timeline, station dual representation, all pillars equal priority, backend connectivity via SSE.

### Internal References
- `ReactComponents/ga-react-components/src/components/PrimeRadiant/ForceRadiant.tsx` — main visualization
- `ReactComponents/ga-react-components/src/components/PrimeRadiant/SolarSystem.ts` — solar system with NASA textures
- `ReactComponents/ga-react-components/src/components/PrimeRadiant/types.ts` — GovernanceNode, HealthMetrics
- `ReactComponents/ga-react-components/src/components/IxqlViewer/IxqlParser.ts` — existing IXql parser
- `Apps/ga-server/GaApi/Hubs/ChatbotHub.cs` — SignalR hub (auth required)
- `Apps/ga-client/src/services/chatService.ts` — SSE streaming pattern
- `governance/demerzel/state/markov/` — Markov prediction data
- `governance/demerzel/tree-sitter-ixql/grammar.js` — IXql grammar
- `ReactComponents/ga-react-components/src/components/BSP/LODManager.ts` — LOD + instancing patterns

### External References
- Solar System Scope textures (CC BY 4.0): https://www.solarsystemscope.com/textures/
- 3d-force-graph API: https://github.com/vasturiano/3d-force-graph
