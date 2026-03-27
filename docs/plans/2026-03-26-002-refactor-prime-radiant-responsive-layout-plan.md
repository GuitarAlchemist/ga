---
title: "refactor: Prime Radiant icon rail layout and responsive breakpoints"
type: refactor
status: active
date: 2026-03-26
origin: docs/brainstorms/2026-03-26-prime-radiant-responsive-declutter-requirements.md
---

# refactor: Prime Radiant icon rail layout and responsive breakpoints

## Overview

Replace Prime Radiant's 12+ absolutely-positioned overlapping panels with a unified icon rail on the right edge. Secondary panels (Activity, Backlog, Agent, Seldon, LLM Status, Detail) open one-at-a-time in a side panel area. Add responsive breakpoints for tablet (bottom sheet overlay) and phone (bottom tab bar + full-screen overlays).

## Problem Frame

Prime Radiant renders all panels simultaneously with absolute positioning. On desktop it's visually noisy with overlapping glassmorphic panels in every corner. On mobile/tablet it's unusable. The layout needs to consolidate panels into a single access pattern while keeping all functionality accessible. (see origin: docs/brainstorms/2026-03-26-prime-radiant-responsive-declutter-requirements.md)

## Requirements Trace

- R1. Icon rail on right edge — one panel open at a time
- R2. Detail panel opens in rail area on node click
- R3. Canvas maximized — full width minus rail/panel when open
- R4. Bottom bar preserved — Chat, IxQL, planet nav stay at canvas bottom
- R5. Tablet (640–1024px) — icons-only rail, 280px overlay panel, 44px touch targets
- R6. Phone (<640px) — bottom tab bar, full-screen panel overlays
- R7. GST Clock and Health stay as small floating overlays
- R8. Tooltip and Tutorial unchanged
- R9. Smooth slide transitions, active icon state
- R10. Touch interactions — pinch/zoom/pan on canvas, node tap opens detail

## Scope Boundaries

- No new panel content or features — layout only
- No changes to 3D graph logic (ForceRadiant engine, SolarSystem)
- No changes to panel internals (ActivityPanel, DetailPanel, etc.)
- No changes to ChatWidget behavior — only positioning/sizing on mobile
- AlgedonicPanel and BeliefHeatmap excluded (not yet displayed)

## Context & Research

### Relevant Code and Patterns

- **ForceRadiant.tsx** (lines 1407–1553): Main render — all panels composed as siblings inside `.prime-radiant` container
- **styles.css** (1848 lines): All panel positioning via `position: absolute` with corner-based zones
- **Panel visibility state**: `selectedNode` drives DetailPanel, `seldonOpen` drives SeldonDashboard, other panels always rendered
- **Existing responsive**: Media queries at 1024px and 640px already exist (detail panel width reduction, activity panel hidden, bottom sheet transforms)
- **Animation pattern**: CSS `transform: translateX(100%)` + `.--open` class toggle, `0.3s ease` or `cubic-bezier(0.4, 0, 0.2, 1)`
- **Glassmorphism**: `backdrop-filter: blur(6–12px)`, `rgba(13, 17, 23, 0.85–0.95)` backgrounds, `#30363d` borders

### Panel Inventory (what moves into rail)

| Panel | Current Position | CSS Class | Rail Icon |
|---|---|---|---|
| ActivityPanel | top-left | `.prime-radiant__activity` | Activity/pulse icon |
| BacklogPanel | bottom-left | `.prime-radiant__backlog` | List/clipboard icon |
| AgentPanel | bottom-right | `.prime-radiant__agents` | Bot/cpu icon |
| SeldonDashboard | right slide-in | `.seldon-dashboard` | Chart/analytics icon |
| LLMStatus | top-right | `.prime-radiant__llm-status` | Brain/AI icon |
| DetailPanel | right slide-in | `.prime-radiant__detail` | Info/file icon |

### Elements that stay in place

| Element | Position | Reason |
|---|---|---|
| GST Clock | top-left floating | Compact, always visible (R7) |
| Health indicator | top-right floating | Compact, always visible (R7) |
| ChatWidget trigger + panel | bottom-left | Primary interaction (R4) |
| IxQL input | bottom-center | Primary interaction (R4) |
| Planet nav bar | bottom-center | Primary interaction (R4) |
| Tooltip | hover overlay | No change needed (R8) |
| Tutorial overlay | modal | No change needed (R8) |
| Search bar | top-left | HUD element, stays |
| Legend | bottom-left | HUD element, stays |
| Timeline slider | bottom-center | HUD element, stays |

## Key Technical Decisions

- **New IconRail component**: Single new component manages rail icons, active panel state, and responsive transformation to bottom tab bar. Keeps ForceRadiant.tsx changes minimal.
- **`activePanel` state in ForceRadiant**: Replace individual panel toggles (`seldonOpen`, implicit `selectedNode`-drives-detail) with a single `activePanel: string | null` state. Panel components receive an `open` boolean prop.
- **CSS-first responsive**: Use media queries and CSS custom properties for breakpoint transitions. No JS resize listeners needed — CSS handles rail → bottom tab bar transformation.
- **Rail width**: 48px icons-only rail. Panel area: 360px on desktop, 280px on tablet, full-screen on phone.
- **Icon choices**: Simple SVG stroke icons matching existing codebase style (24x24, stroke="currentColor", strokeWidth=2). Activity=pulse, Backlog=clipboard, Agent=cpu, Seldon=bar-chart, LLM=brain, Detail=file-text.

## Open Questions

### Resolved During Planning

- **Icon style**: SVG stroke icons matching the existing icon style in ChatWidget, TutorialOverlay (24x24, stroke-based). Consistent with the codebase.
- **3d-force-graph touch conflicts**: The library handles its own touch events on the canvas element. Rail/tab bar are outside the canvas DOM, so no conflict. Node tap already works via the library's `onNodeClick` callback.
- **Rail-to-tab-bar transition at 640px**: CSS media query flips the rail from `position: fixed; right: 0; flex-direction: column` to `position: fixed; bottom: 0; flex-direction: row`. The icons and click handlers stay the same — only the CSS layout changes.

### Deferred to Implementation

- **Exact animation timing**: May need tuning after seeing the transitions in-browser.
- **Legend/search repositioning**: May need minor nudges to avoid overlapping the rail. Resolve visually during implementation.

## High-Level Technical Design

> *This illustrates the intended approach and is directional guidance for review, not implementation specification. The implementing agent should treat it as context, not code to reproduce.*

```
Desktop (>1024px):
┌──────────────────────────────────┬────┬──────────┐
│                                  │Rail│  Panel    │
│  [Clock]              [Health]   │ A  │ (360px)   │
│  [Search]                        │ B  │ Content   │
│                                  │ S  │ of active │
│         3D Canvas                │ L  │ panel     │
│    (fills remaining space)       │ D  │           │
│                                  │    │           │
│  [Legend]                        │    │           │
│  [Chat] [IxQL] [Planets] [Time] │    │           │
└──────────────────────────────────┴────┴──────────┘
         Rail: 48px  Panel: 360px (0px when closed)

Tablet (640–1024px):
┌──────────────────────────────────┬────┐
│                                  │Rail│ ← Panel overlays
│         3D Canvas                │ A  │   canvas edge
│    (full width minus rail)       │ B  │   (280px)
│                                  │ S  │
│  [Chat] [IxQL] [Planets]        │ L  │
└──────────────────────────────────┴────┘

Phone (<640px):
┌──────────────────────────────────┐
│                                  │
│         3D Canvas                │
│       (full screen)              │
│                                  │
│  [Chat] [IxQL] [Planets]        │
├──────────────────────────────────┤
│  [A] [B] [S] [L] [D]  (tab bar) │
└──────────────────────────────────┘
   Panels open as full-screen overlays
```

## Implementation Units

- [ ] **Unit 1: IconRail component**

  **Goal:** Create the icon rail component with panel toggle logic.

  **Requirements:** R1, R9

  **Dependencies:** None

  **Files:**
  - Create: `ReactComponents/ga-react-components/src/components/PrimeRadiant/IconRail.tsx`

  **Approach:**
  - Props: `activePanel: string | null`, `onPanelToggle: (panelId: string) => void`
  - Renders a vertical strip of icon buttons, each with a `data-panel` id
  - Active panel icon gets a highlight class (gold accent border or background)
  - Icons: SVG inline, matching existing 24x24 stroke style
  - Panel IDs: `'activity' | 'backlog' | 'agent' | 'seldon' | 'llm' | 'detail'`

  **Patterns to follow:**
  - ChatWidget's SVG icon buttons for icon style
  - Planet bar button layout for horizontal variant

  **Test scenarios:**
  - Clicking an icon calls `onPanelToggle` with correct panel ID
  - Active icon shows highlight state
  - Clicking active icon again calls toggle (to close)

  **Verification:**
  - Component renders, icons are visible, click handlers fire.

- [ ] **Unit 2: activePanel state in ForceRadiant**

  **Goal:** Replace scattered panel visibility state with a single `activePanel` state. Wire IconRail into the render tree.

  **Requirements:** R1, R2

  **Dependencies:** Unit 1

  **Files:**
  - Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/ForceRadiant.tsx`

  **Approach:**
  - Add `const [activePanel, setActivePanel] = useState<string | null>(null)`
  - Replace `seldonOpen` with `activePanel === 'seldon'`
  - When `selectedNode` changes (node click), set `activePanel` to `'detail'`
  - Pass `open={activePanel === 'activity'}` etc. to each panel component
  - Panels that don't already have an `open` prop: wrap their render in a conditional or add the prop
  - Remove direct renders of panels from their old positions — they now render inside a panel container next to the rail
  - Add `<IconRail activePanel={activePanel} onPanelToggle={setActivePanel} />` to JSX

  **Patterns to follow:**
  - Existing `seldonOpen` toggle pattern
  - `selectedNode` → `DetailPanel` prop passing

  **Test scenarios:**
  - Clicking a rail icon opens corresponding panel
  - Clicking graph node sets activePanel to 'detail'
  - Only one panel visible at a time
  - Clicking active icon closes panel (activePanel → null)

  **Verification:**
  - All 6 panels accessible via rail icons. Node click opens detail. Only one panel at a time.

- [ ] **Unit 3: Desktop CSS layout — rail + panel + canvas**

  **Goal:** Restructure the CSS so the canvas, rail, and panel share the screen without overlapping.

  **Requirements:** R1, R3, R7, R9

  **Dependencies:** Unit 2

  **Files:**
  - Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/styles.css`

  **Approach:**
  - `.prime-radiant` container: `display: flex` (canvas area + rail + panel)
  - Canvas area: `flex: 1` (fills remaining space)
  - Rail: `width: 48px`, fixed right edge, `flex-shrink: 0`
  - Panel area: `width: 360px` when open, `width: 0` when closed, `overflow: hidden`, slide transition
  - Remove old absolute positioning for Activity, Backlog, Agent, Seldon, LLM Status panels
  - Keep absolute positioning for: GST Clock, Health, Search, Legend, Timeline, Chat, IxQL, Planet bar, Tooltip — these float over the canvas
  - Rail icons: glassmorphism background matching existing panel style
  - Active icon: gold left-border accent or background highlight
  - Panel container: existing glassmorphism style, `border-left: 1px solid #30363d`

  **Patterns to follow:**
  - Existing glassmorphism: `backdrop-filter: blur(12px)`, `background: rgba(13, 17, 23, 0.95)`, `border: 1px solid #30363d`
  - Existing slide animation: `transition: width 0.3s ease` or `transform: translateX`

  **Test scenarios:**
  - Canvas fills full width when no panel open
  - Canvas shrinks when panel opens (no overlap)
  - Rail always visible on right edge
  - Floating HUD elements (clock, health, search) remain correctly positioned
  - Smooth slide animation on panel open/close

  **Verification:**
  - Desktop layout matches the design sketch. No overlapping panels. Canvas resizes correctly.

- [ ] **Unit 4: Tablet responsive (640–1024px)**

  **Goal:** Adapt the rail layout for tablet screens.

  **Requirements:** R5, R10

  **Dependencies:** Unit 3

  **Files:**
  - Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/styles.css`

  **Approach:**
  - Media query `@media (max-width: 1024px)`: panel area overlays canvas edge (280px, `position: absolute` over canvas) instead of pushing it
  - Rail stays as vertical strip (48px) but icons-only (no labels — already the case)
  - Touch targets: ensure all rail icons are minimum 44px
  - Panel gets a subtle shadow to distinguish from canvas

  **Patterns to follow:**
  - Existing `@media (max-width: 1024px)` rules in styles.css
  - Existing `pointer: coarse` media query for touch targets

  **Test scenarios:**
  - Panel overlays canvas edge at tablet width
  - Touch targets meet 44px minimum
  - Canvas remains interactive under the panel overlay area
  - Pinch-to-zoom works on canvas

  **Verification:**
  - Tablet layout works in browser dev tools responsive mode. Panels overlay cleanly.

- [ ] **Unit 5: Phone responsive (<640px)**

  **Goal:** Transform rail into bottom tab bar, panels into full-screen overlays.

  **Requirements:** R6

  **Dependencies:** Unit 4

  **Files:**
  - Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/styles.css`
  - Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/IconRail.tsx` (add close button for overlays)

  **Approach:**
  - Media query `@media (max-width: 640px)`:
    - Rail: `position: fixed; bottom: 0; left: 0; right: 0; flex-direction: row; height: 48px; width: 100%`
    - Panel: `position: fixed; inset: 0; z-index: 30` (full-screen overlay with close button)
    - Canvas: full screen, bottom padding for tab bar
    - Chat widget: full-width bottom sheet (already partially handled in existing CSS)
  - Add a close/back button to the panel overlay header on mobile
  - Bottom bar items (Chat trigger, IxQL, planet nav) need to coexist with tab bar — stack them above the tab bar

  **Patterns to follow:**
  - Existing `@media (max-width: 640px)` bottom-sheet patterns for detail/chat
  - Existing `pointer: coarse` touch target enlargement

  **Test scenarios:**
  - Tab bar appears at bottom on phone width
  - Tapping a tab opens full-screen panel overlay
  - Close button dismisses overlay
  - Graph fills screen by default
  - Chat, IxQL, planet nav visible above tab bar
  - Back button / gesture closes panel

  **Verification:**
  - Phone layout works in browser dev tools. Tab bar navigates between panels. Full-screen overlays open/close.

- [ ] **Unit 6: Clean up removed panel positioning**

  **Goal:** Remove orphaned CSS rules and old panel positioning code.

  **Requirements:** R1, R3

  **Dependencies:** Unit 5

  **Files:**
  - Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/styles.css`
  - Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/ForceRadiant.tsx`

  **Approach:**
  - Remove old absolute positioning rules for: `.prime-radiant__activity`, `.prime-radiant__backlog`, `.prime-radiant__agents`, `.seldon-dashboard`, `.prime-radiant__llm-status`, `.prime-radiant__detail`
  - Remove `seldonOpen` state and its toggle logic from ForceRadiant
  - Remove the old Seldon/Demerzel toggle buttons from planet bar (replaced by rail icons)
  - Clean up any unused imports or dead code
  - Verify `npm run lint` and `npm run build` pass

  **Patterns to follow:**
  - Existing code conventions in ForceRadiant.tsx

  **Test scenarios:**
  - No orphaned CSS rules referencing old panel positions
  - No console warnings about missing styles
  - All panels still accessible via rail

  **Verification:**
  - `npm run build` passes. `npm run lint` passes. No visual regressions.

## System-Wide Impact

- **Interaction graph:** ForceRadiant's `onNodeClick` callback now sets `activePanel` instead of just `selectedNode`. SeldonDashboard's toggle moves from planet bar button to rail icon.
- **Error propagation:** No new error paths. Panel components receive the same props as before.
- **State lifecycle risks:** The `activePanel` state replaces `seldonOpen` — must ensure no other component reads `seldonOpen` directly.
- **API surface parity:** `index.ts` exports unchanged. Panel components' public APIs unchanged — only positioning CSS changes.
- **Integration coverage:** Manual testing in browser dev tools at 3 breakpoints (desktop, tablet, phone) is the primary verification method for layout work.

## Risks & Dependencies

- **3D canvas resize**: When the panel opens/closes, the canvas width changes. `3d-force-graph` may need a resize signal (check if it handles `ResizeObserver` or needs a manual `.width()` call).
- **Existing media queries**: The current `@media (max-width: 640px)` rules hide ActivityPanel and transform DetailPanel into a bottom sheet. These need to be replaced, not layered on top of.
- **z-index conflicts**: The new full-screen phone overlay (z-index: 30) must not conflict with Tutorial overlay (z-index: 40) or Chat widget (z-index: 25).

## Sources & References

- **Origin document:** [docs/brainstorms/2026-03-26-prime-radiant-responsive-declutter-requirements.md](docs/brainstorms/2026-03-26-prime-radiant-responsive-declutter-requirements.md)
- Related code: `ForceRadiant.tsx`, `styles.css`, `DetailPanel.tsx`, `SeldonDashboard.tsx`, `ActivityPanel.tsx`
- Related issues: #21 (icicle navigator), #20 (breadcrumb navigator) — these may build on the new rail/panel layout
