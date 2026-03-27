---
date: 2026-03-26
topic: algedonic-visual-signals
---

# Algedonic Visual Signals in Prime Radiant

## Problem Frame

Prime Radiant shows governance health via node glow/size/pulse, but there's no visual feedback for discrete algedonic events — when something breaks, recovers, or compounds. The AlgedonicPanel exists with mock data but isn't connected to real governance signals or visible on the graph. Users can't see pain/pleasure propagate through the governance network in real time.

## Requirements

- R1. **Node ripple effect**: When an algedonic signal fires, the source node emits an expanding ripple — red for pain, gold for pleasure. Ripple fades over ~2 seconds.
- R2. **Edge propagation**: After the node ripple, the signal visually travels along connected edges to neighboring nodes. Color-coded: red=pain, gold=pleasure. Creates a visible wave through the graph.
- R3. **Compounding surge**: When 3+ pleasure signals fire within a short window (e.g., 10 seconds), a bright gold wave surges through the entire connected cluster. Visually distinct from single-signal ripples — bigger, brighter, more dramatic.
- R4. **11 algedonic channels (core set)**:
  - Pain: `belief_collapse`, `policy_violation`, `lolli_inflation`, `schema_drift`, `test_failure`, `cascade_harm`
  - Pleasure: `domain_convergence`, `resilience_recovery`, `knowledge_harvest`, `test_milestone`, `compounding_surge`
- R5. **Real-time signal delivery**: Algedonic signals pushed via SignalR (`GovernanceHub`) when governance state changes. Backend emits signals by mapping belief/health changes to the channel types in R4.
- R6. **AlgedonicPanel in icon rail**: Add an algedonic icon (heartbeat/wave) to the IconRail. Panel shows real-time signal timeline in the side panel area. Replace mock data with live SignalR-pushed signals.
- R7. **Signal persistence**: Signals written to `governance/demerzel/state/signals/` as JSON files. AlgedonicPanel loads recent history on connect, then receives new signals via push.

## Success Criteria

- Pain signals produce a visible red ripple + edge wave on the graph within 1 second of the event.
- Pleasure signals produce gold ripples. Compounding surge is visually distinct and dramatic.
- AlgedonicPanel in the rail shows a live, chronological feed of real signals (not mock data).
- Signals persist across page reloads (loaded from backend on reconnect).

## Scope Boundaries

- No changes to the governance health calculation itself — signals are derived from health/belief state changes, not a new health model
- No new SignalR hub — extend existing `GovernanceHub` with an `AlgedonicSignal` event
- No audio/haptic feedback — visual only
- Compounding detection is frontend-only (count recent pleasure signals in a sliding window)
- No changes to harm taxonomy or constitutional documents

## Key Decisions

- **Node ripple + edge propagation**: Both layers for maximum visual impact. Ripple shows source, edge propagation shows blast radius.
- **Compounding surge**: Frontend detects compound events (3+ pleasure signals in 10s window) and triggers a cluster-wide gold wave. This makes positive momentum visually rewarding.
- **Core channel set (11)**: Balanced between coverage and simplicity. Maps directly to existing harm taxonomy tiers.
- **AlgedonicPanel in rail**: Consistent with the new icon rail layout. One more panel slot.

## Dependencies / Assumptions

- Icon rail from PR #28 is merged (it is)
- SignalR `GovernanceHub` infrastructure is working
- `GovernanceWatcherService` detects file changes and triggers broadcasts

## Outstanding Questions

### Deferred to Planning
- [Affects R1][Technical] How to render expanding ring geometry in 3d-force-graph (shader vs sprite vs Three.js RingGeometry)
- [Affects R2][Technical] Performance impact of animating edge color propagation across many links simultaneously
- [Affects R5][Technical] Exact mapping logic: which belief state transitions produce which algedonic channel signals
- [Affects R7][Technical] Signal file naming convention and retention policy (how many signals to keep)

## Next Steps

→ `/ce:plan` for structured implementation planning
