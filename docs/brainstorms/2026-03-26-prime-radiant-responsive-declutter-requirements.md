---
date: 2026-03-26
topic: prime-radiant-responsive-declutter
---

# Prime Radiant: Declutter & Responsive Layout

## Problem Frame

Prime Radiant currently has 12+ absolutely-positioned, overlapping panels competing for screen space. On desktop it's visually noisy; on tablet/phone it's unusable. All panels serve a purpose and must remain accessible, but the layout needs to consolidate them into a coherent, responsive system that works across devices.

## Requirements

- R1. **Icon rail**: Add a vertical icon strip on the right edge of the screen. Each icon represents a panel: Activity, Backlog, Agent, Seldon Dashboard, LLM Status, and Detail (node info). Clicking an icon opens that panel in a fixed-width side area. Only one panel open at a time — clicking another icon swaps it, clicking the active icon closes it.
- R2. **Detail panel in rail**: When a user clicks a graph node, the Detail Panel opens in the rail's panel area (not a separate overlay). If another panel is already open, it gets replaced by Detail.
- R3. **Graph canvas maximized**: The 3D force graph/solar system canvas takes all remaining horizontal space (full width minus the rail when a panel is open). No panels overlap the canvas on desktop.
- R4. **Bottom bar preserved**: Chat widget trigger, IxQL command input, and planet navigation bar stay at the bottom of the canvas. These are primary interactions and should not move into the rail.
- R5. **Tablet layout (640–1024px)**: Rail collapses to icons only (no labels). Panel opens as a narrower overlay (280px) over the canvas edge. Touch targets minimum 44px.
- R6. **Phone layout (<640px)**: Rail becomes a bottom tab bar (horizontal icons). Panels open as full-screen overlays with a close/back button. Graph is the default view. Chat opens as a full-screen overlay from the bottom.
- R7. **GST Clock and Health indicator**: Remain as small floating overlays on the canvas (top-left, top-right). They're compact and don't need rail slots.
- R8. **Tooltip and Tutorial**: Tooltip stays as a hover element. Tutorial overlay stays as a modal. No changes needed.
- R9. **Smooth transitions**: Panel open/close uses slide animations consistent with the current glassmorphism style. Rail icons show active state for the currently open panel.
- R10. **Touch interactions**: Pinch-to-zoom and pan on the 3D canvas work on touch devices. Node tap opens detail. Long-press or double-tap reserved for future use.

## Success Criteria

- Desktop: only the 3D canvas + small rail icons visible by default. Clicking a rail icon smoothly opens one panel. No overlapping panels.
- Tablet: usable with touch. Panels open/close cleanly. Canvas is interactive.
- Phone: full-screen panel overlays. Bottom tab bar for navigation. Graph fills the screen by default.
- All existing panel functionality (Activity, Backlog, Agent, Seldon, LLM Status, Detail, Chat) remains accessible.

## Scope Boundaries

- No new panel content or features — layout and responsiveness only
- No changes to the 3D graph logic (ForceRadiant, RadiantEngine, SolarSystem)
- No changes to panel internal content (ActivityPanel, DetailPanel, etc. stay as-is internally)
- No changes to ChatWidget behavior — only its positioning/sizing changes on mobile
- AlgedonicPanel and BeliefHeatmap not included (not yet displayed)

## Key Decisions

- **Icon rail pattern**: Consolidates all secondary panels into one consistent access point, eliminating scattered absolute-positioned panels.
- **Detail panel in rail**: Node detail shares the same panel area as other rail panels, keeping the right side unified.
- **Bottom bar stays separate**: Chat, IxQL, and planet nav are primary actions — they stay at the bottom of the canvas, not in the rail.
- **Phone uses bottom tab bar**: Horizontal icon bar at the bottom is the standard mobile pattern for app navigation.

## Outstanding Questions

### Deferred to Planning
- [Affects R1][Technical] Exact icon choices for each panel in the rail (SVG icons or emoji)
- [Affects R5][Needs research] Whether 3d-force-graph's touch handling conflicts with panel gestures on tablet
- [Affects R6][Technical] How to handle the transition from right rail to bottom tab bar at the 640px breakpoint

## Next Steps

→ `/ce:plan` for structured implementation planning
