---
date: 2026-03-25
topic: living-cosmos-prime-radiant
---

# Living Cosmos — Prime Radiant Mega-Feature

## Problem Frame

The Prime Radiant governance visualization currently displays static-sized nodes with uniform animations. It has a solar system HUD, but planets lack cinematics and interactivity. The existing IXql language, Seldon Plan psychohistory framework, and Markov predictions are powerful backend capabilities with no visual expression. The visualization should feel **alive** — nodes breathing with activity, predictions painting ghostly futures, a space station growing with the project, and IXql queries sculpting the view in real-time.

## Pillar 1: Living Graph

### R1. Adaptive Node Sizing by Importance
- Nodes dynamically resize based on a composite importance score: edge count (connectivity), `health.ergolCount` (live bindings), `health.resilienceScore`, inverse staleness
- Size animates smoothly (lerp over ~0.5s) when underlying data changes
- Within each type tier, nodes with more connections/activity are visually bigger
- Importance score formula: `importance = 0.4 * normalizedEdgeCount + 0.3 * normalizedErgol + 0.2 * resilience + 0.1 * (1 - staleness)`

### R2. Activity-Based Node Effects
- Recent git commits: pulse/glow brighter (commit recency maps to pulse intensity)
- High `ergolCount`: faster particle orbit speed around the node
- Stale nodes (`staleness > 0.5`): dim opacity, slow animations, desaturated color
- `lolliCount > 0`: visible red decay particles drifting away from node (count proportional)

### R3. Snake-Like Edge Undulation
- `pipeline-flow` edges: slow sinusoidal wave (amplitude ~0.3, frequency ~0.5Hz) traveling source-to-target
- `constitutional-hierarchy` edges: stately, slower golden wave (amplitude ~0.15, frequency ~0.2Hz)
- `cross-repo` edges: shimmer effect (rapid low-amplitude oscillation, 2Hz)
- `lolli` edges: erratic jitter (random displacement, visualizing instability)
- Containment sphere: slow breathing (scale oscillation 0.98-1.02, period ~8s)

### R4. IXql Visualization Control
- Command input (palette or inline editor) accepts IXql-subset queries
- Queries filter, highlight, resize, and animate nodes based on predicates
- Example: `SELECT nodes WHERE health.staleness > 0.5 SET glow=red, pulse=fast`
- Example: `SELECT edges WHERE type='lolli' SET visible=true, color=red`
- Results apply as a visual overlay — original state restorable via `RESET`
- IXql is already a node type in the graph — queries that target IXql nodes are meta-circular

### R5. IXql Client-Side Runtime
- The existing F# parser uses FParsec (not Fable-compatible), so: **build a TypeScript IXql subset parser** targeting visualization commands
- Subset grammar: `SELECT nodes|edges WHERE <predicate> SET <visual-property>=<value>` + `RESET`
- Predicates: field comparisons (`health.staleness > 0.5`), type checks (`type='policy'`), name patterns
- Visual properties: `glow`, `pulse`, `size`, `color`, `visible`, `opacity`, `speed`
- Parser runs client-side — no server roundtrip, works offline and in demo mode
- Future: if full IXql is needed client-side, use the existing tree-sitter grammar compiled to WASM

## Pillar 2: Cosmos & Cinematics

### R6. Planet Cinematics
- Click a planet in the nav bar or directly in the scene: camera swoops in over 1.5s
- Time accelerates 10x while focused on a planet (orbit speed multiplied)
- Orbit trails become visible arcs (opacity increases from 0.08 to 0.4)
- Moon systems become visible when zoomed into a planet
- Trajectory prediction: dotted line showing future orbital path (next 90 degrees)
- Click again or press Escape to return to overview

### R7. Planet/Moon Hover Names
- Hover over any planet or moon: tooltip shows name
- Small info card with real astronomical data: diameter, distance from sun (AU), orbital period
- Card appears near cursor, disappears on mouseout
- No clutter — card is minimal, semi-transparent, monospace font matching Prime Radiant aesthetic

### R8. Sun Realism Enhancement
- Animated corona with procedural turbulence
- Solar flare particles ejecting periodically
- Proper emissive glow with HDR bloom interaction
- Surface granulation texture animation

### R9. Scrollbar & Panel Overlap Fixes
- Full-screen visualization: no browser scrollbars (overflow: hidden on container)
- Detail panel, activity panel, chat widget, LLM status, planet bar — none overlap
- Responsive: panels reflow or collapse on smaller viewports
- Z-index hierarchy: tooltips > panels > nav bar > 3D scene

## Pillar 3: Hari Seldon Analytics

### R10. Ghost Trail Predictions
- Each node with `health.markovPrediction` shows fading translucent copies along predicted trajectory
- Trail shows 3-5 future time steps as ghost copies decreasing in opacity (0.5 -> 0.1)
- Ghost copies colored by predicted health: green -> amber -> red
- Scrubable timeline slider controls which prediction horizon is shown
- Default: show 1-step prediction; slider extends to 5-step

### R11. Psychohistory Dashboard
- "Seldon" nav button opens a prediction overlay panel
- Shows aggregate governance health trajectory (sparkline of predicted states)
- Highlights nodes at highest risk of health degradation
- Displays Markov transition probabilities for selected node
- Ties into existing `governance/demerzel/state/markov/` data

### R12. Jarvis Space Station
- Modular space station assembled procedurally in 3D background
- **Dual representation:** modules map to BOTH project layers (Core, Domain, Analysis, ML, Orchestration) AND governance categories (constitutions, policies, personas, pipelines)
- Construction progress reflects actual status: build passing = module complete, tests green = module lit, failing = module dark/flickering
- Jarvis-style HUD text labels on modules showing component names and status
- Assembly animation: new modules dock and connect when milestones are reached
- Visual spectacle: lights, particle welding effects, docking bay, rotating sections

## Pillar 4: Backend Connectivity

### R13. Live Backend Integration
- Demerzel chat widget sends messages to the real chatbot backend (GaApi SignalR hub), not just local display
- Governance health data refreshes from the backend health endpoint (`/health` or GraphQL)
- Node activity data (git commits, ergol counts) sourced from the API, not hardcoded
- IXql queries can optionally execute server-side via the API for full pipeline execution
- Graceful degradation: if backend is unavailable, fall back to static demo data

## Success Criteria

- Prime Radiant nodes visibly differ in size based on importance — a new contributor can identify the most important governance artifacts at a glance
- Typing an IXql query in the browser highlights matching nodes within 200ms (WASM execution)
- Ghost trails correctly predict the direction of health changes when compared against actual future data
- Space station modules accurately reflect project build/test status
- Planet cinematics feel cinematic — smooth camera swoops, time dilation, visible trajectories
- 60fps maintained with all features active (~100 nodes, bloom, particles, station, solar system)

## Scope Boundaries

- **Not building:** Full IXql IDE (syntax highlighting, autocomplete, error recovery) — just a command input
- **Not building:** Multi-user collaborative viewing
- **Not building:** Saving/loading IXql visualization presets (can add later)
- **Not building:** Audio/sound effects for animations
- **Not building:** VR/AR mode
- Station modules are procedural geometry, not Blender imports (for now)

## Key Decisions

- **IXql runtime = TS subset parser + tree-sitter WASM future:** FParsec blocks Fable compilation. Build a focused TS parser for visualization commands now; tree-sitter WASM grammar available for full IXql later.
- **Predictions = Ghost trails:** Fading translucent node copies along predicted trajectory with health-colored gradient. Scrubable timeline.
- **Station = Dual representation:** Maps to both project layers AND governance categories. Data-driven AND visually spectacular.
- **All three pillars = equal priority:** No phasing — design the architecture to support all three from the start.

## Dependencies / Assumptions

- IXql F# parser (`governance/demerzel/tools/ixql-parser/`) is compilable to JS via Fable
- Markov prediction data exists in `governance/demerzel/state/markov/`
- Build/test status is accessible at runtime (health endpoint or static data for demo)
- Three.js instanced rendering can handle station + solar system + graph at 60fps

## Outstanding Questions

### Resolve Before Planning
- (Resolved) F# IXql parser uses FParsec — not Fable-compatible. Decision: TS subset parser for visualization, tree-sitter WASM for full IXql later.

### Deferred to Planning
- [Affects R12][Needs research] What procedural geometry approach for the station? (BoxGeometry modules vs. more detailed shapes)
- [Affects R10][Technical] How to source Markov prediction data in demo mode — static JSON snapshots or live computation?
- [Affects R4][Technical] IXql query subset — which productions from the full grammar are needed for visualization control?
- [Affects R6][Technical] Camera animation library — use existing OrbitControls tweening or add gsap/tween.js?

## Next Steps

`-> /ce:plan` for structured implementation planning (all blocking questions resolved)
