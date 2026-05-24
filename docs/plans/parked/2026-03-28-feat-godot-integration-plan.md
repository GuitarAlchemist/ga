# Godot 4.6 Integration Plan

**Issue**: GuitarAlchemist/ga#37
**Status**: active
**Created**: 2026-03-28

## Summary

Integrate Godot 4.6.1 as the 3D rendering engine for Prime Radiant, augmenting the current Three.js force graph with physics-based governance visualization.

## Architecture

React keeps all UI panels. Godot owns the 3D viewport for immersive mode. Hybrid bridge enables both WASM web preview and desktop Godot.

```
React (panels, controls) <--postMessage/WebSocket--> Godot (3D viewport)
                              Bridge Schema v1
```

## Phases

### Phase 1: Bridge Protocol (S — 1-2 days) ✅
- [x] Define typed bridge event schema (`BridgeEvent<T>` union type) — 17 event types
- [x] Events: `navigate-to-planet`, `highlight-node`, `update-belief-weather`, `camera-sync`
- [x] A2A agent events: `agent-connect`, `agent-disconnect`, `agent-status`, `agent-invoke`, `agent-result`
- [x] WebSocket transport (port 6505, auto-reconnect)
- [x] React `useGodotBridge()` hook wrapping the WS connection
- [x] Fallback: postMessage for WASM embed, WS for desktop
- [x] `GodotScene.tsx` — iframe embed with panel/fullscreen modes, Demerzel face controls
- [x] `AgentPresence.ts` — A2A tracker polling ACP (Demerzel/Seldon), GA health, MCP agents (ix/TARS)
- [x] `PresencePanel.tsx` — viewers + A2A agents + Discord + session timeline + connection log
- [x] `ConnectionLog.ts` — persistent admin connection log with rotation/scavenge
- **Files**: `GodotBridge.ts`, `useGodotBridge.ts`, `GodotScene.tsx`, `AgentPresence.ts`, `PresencePanel.tsx`, `ConnectionLog.ts`

### Phase 2: Constitutional Gravity Engine (L — 3-5 days)
- [ ] Godot scene: central gravity well = Asimov constitution
- [ ] Policy nodes orbit based on citation count (more citations = closer)
- [ ] Unanchored artifacts drift to edge (visual LOLLI detection)
- [ ] Confidence = mass, staleness = entropy, ERGOL = energy/light
- [ ] Import governance graph topology from `state/beliefs/*.belief.json`
- **Files**: Godot project in `Apps/ga-godot/`

### Phase 3: Tetravalent Weather System (M — 2-3 days)
- [ ] Map T/F/U/C truth values to weather:
  - True = clear skies
  - False = night/darkness
  - Unknown = fog (navigation blocked)
  - Contradictory = lightning storms (damages nodes over time)
- [ ] Weather particles via Godot GPU particles
- [ ] Belief heatmap becomes a weather forecast overlay
- **Depends on**: Phase 2

### Phase 4: WASM Web Export (M — 2-3 days)
- [ ] Godot WASM export embedded in PrimeRadiant via iframe/canvas
- [ ] Same bridge protocol (postMessage instead of WebSocket)
- [ ] Toggle: Three.js quick view vs Godot immersive
- [ ] Progressive: load WASM only when user clicks "3D Engine" button
- **Depends on**: Phase 1

### Phase 5: MCP Autonomous Loop (L — 3-5 days)
- [ ] Claude reads `state/` → MCP commands → Godot renders → screenshot → critic
- [ ] Algedonic channel integration (prevents LOLLI inflation)
- [ ] Use existing 163 MCP Pro tools for Godot automation
- [ ] Visual critic scoring pipeline
- **Depends on**: Phases 2, 3

## Key Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Engine | Godot 4.6.1 | GDExtension/Rust for ix ML, MIT license, MCP Pro tools |
| Bridge | WebSocket + postMessage | WS for desktop, postMessage for WASM |
| Deployment | Hybrid (WASM preview + desktop immersive) | Low barrier for web, full power for dev |
| Physics | Godot built-in | No need for external physics lib |

## Existing Infrastructure

- PlanetNav `onLaunchGodot` button (commit `94435c52`) — already wires to WS port 6505
- Godot MCP server (`mcp-servers/` potential, MCP Pro tools available)
- Governance state files in `governance/state/beliefs/`
- Three.js force graph in ForceRadiant.tsx (coexists, not replaced)

## Out of Scope (This Plan)

- VR/XR support (future phase)
- Replacing Three.js entirely (augmentation, not replacement)
- Spatial audio (future phase)
- GDExtension/Rust bindings for ix ML (separate project)
