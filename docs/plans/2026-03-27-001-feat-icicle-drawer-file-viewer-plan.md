---
title: "feat: Bottom drawer with icicle navigator and file content viewer"
type: feat
status: active
date: 2026-03-27
origin: docs/brainstorms/2026-03-27-icicle-drawer-requirements.md
---

# feat: Bottom drawer with icicle navigator and file content viewer

## Overview

Add a resizable bottom drawer to Prime Radiant with a drag handle. The drawer contains a split pane: an icicle (D3 partition) view of the governance file hierarchy on the left, and a rendered markdown/syntax-highlighted file content viewer on the right. Reuses the existing IcicleView from EcosystemRoadmap. Requires a new file-content endpoint and adding FilePath to GovernanceNode.

## Problem Frame

Prime Radiant's 3D graph shows governance nodes but provides no spatial overview of the file hierarchy or inline file viewing. The DetailPanel has a collapsible file tree for a single node, but you can't see the full governance structure at a glance or read file contents without leaving the app. IcicleView.ts exists but is orphaned. (see origin: docs/brainstorms/2026-03-27-icicle-drawer-requirements.md)

## Requirements Trace

- R1. Bottom drawer with drag handle — pull up to reveal, independent of rail
- R2. Icicle navigator — governance file hierarchy, color-coded by type, zoomable
- R3. File content viewer — rendered markdown, syntax-highlighted code, split pane right
- R4. Governance data source — derived from existing graph data + FilePath on nodes
- R5. Drawer resize — drag handle, min 150px, max 60vh, double-click to toggle
- R6. Mobile — full-screen overlay on phone (<640px)

## Scope Boundaries

- Read-only file viewer — no editing
- No changes to IcicleView rendering logic — reuse as-is with new data
- No changes to EcosystemRoadmap — IcicleView is shared
- No audio, no new SignalR events

## Context & Research

### Relevant Code and Patterns

- **IcicleView.ts**: `createIcicleView(scene, camera, root, callbacks)` — expects `RoadmapNode` tree with `{id, name, color, description, children[]}`. Returns `{update, handleClick, handleHover, dispose}`.
- **GovernanceController.cs**: `ScanDirectory()` scans `governance/demerzel/` for files but doesn't persist file paths in `GovernanceNode`.
- **GovernanceNode**: Record with `Id, Name, Type, Description, Color, Health, Children[]` — needs `FilePath` added.
- **react-markdown** + **react-syntax-highlighter** + **remark-gfm**: Already installed. `ChatMessage.tsx` has the rendering pattern with VSCode Dark+ theme.
- **MarkdownCard.tsx**: Simpler markdown renderer in IxqlViewer — alternative pattern.
- **Existing drawer patterns**: No bottom drawer exists yet. The side panel (`prime-radiant__side-panel`) uses CSS width transition — drawer will use height transition.

### File Content Endpoint

GovernanceController already reads files (`File.ReadAllText`) for predictions and backlog. A new `GET /api/governance/file-content?filePath=...` endpoint follows the same pattern with path-traversal validation (resolved path must stay under demerzel root).

## Key Technical Decisions

- **Add `FilePath` to GovernanceNode**: Captured during `ScanDirectory()`, stored as path relative to demerzel root (e.g., `constitutions/asimov.constitution.md`). Enables file content fetching without guessing paths.
- **New file-content endpoint**: `GET /api/governance/file-content?filePath=...` with path validation. Cached 5 minutes. Returns `{content, filePath, mediaType}`.
- **Transform graph nodes to RoadmapNode tree**: Frontend builds a hierarchy from flat governance nodes by grouping on `Type` (constitutions, policies, personas, schemas, etc.), then nesting files under type groups. This produces the `RoadmapNode` tree IcicleView expects.
- **Split pane in drawer**: CSS flexbox — icicle canvas left (60%), content viewer right (40%). Content viewer uses `react-markdown` + `react-syntax-highlighter` for rendering.
- **Drawer as new component**: `IcicleDrawer.tsx` — self-contained, receives `graphData` prop. Manages its own Three.js scene for the icicle view (separate from the main 3D graph scene).

## Open Questions

### Resolved During Planning

- **File content availability**: New `GET /api/governance/file-content` endpoint with path validation. Follows existing `File.ReadAllText` pattern in GovernanceController.
- **IcicleView data adaptation**: Transform flat governance nodes into `RoadmapNode` tree by grouping on `Type` field. Each type becomes a parent node, files become children.
- **Markdown renderer**: Use `react-markdown` + `remarkGfm` + `react-syntax-highlighter` (Prism, vscDarkPlus theme) — already installed, pattern in `ChatMessage.tsx`.

### Deferred to Implementation

- **Exact color mapping per governance type**: Tune visually during implementation. Start with existing `GOVERNANCE_NODE_COLORS` from types.ts.
- **Icicle canvas sizing**: May need aspect ratio adjustments when embedded in the drawer vs full-screen EcosystemRoadmap.

## High-Level Technical Design

> *This illustrates the intended approach and is directional guidance for review, not implementation specification. The implementing agent should treat it as context, not code to reproduce.*

```
┌──────────────────────────────────────────────────┬────┐
│             3D Canvas (ForceRadiant)              │Rail│
│                                                   │    │
│                                                   │    │
├─── drag handle ──────────────────────────────────┤    │
│ ┌──────────────────────┬────────────────────────┐│    │
│ │   Icicle View        │   File Content Viewer  ││    │
│ │   (D3 partition)     │   (react-markdown)     ││    │
│ │                      │                        ││    │
│ │   [constitutions]    │   # Asimov Constitution││    │
│ │    [asimov] [default]│   ## Article 0         ││    │
│ │   [policies]         │   The Zeroth Law...    ││    │
│ │    [alignment]...    │                        ││    │
│ └──────────────────────┴────────────────────────┘│    │
└──────────────────────────────────────────────────┴────┘
```

## Implementation Units

- [ ] **Unit 1: Add FilePath to GovernanceNode + file-content endpoint**

  **Goal:** Backend changes to support file content fetching.

  **Requirements:** R4, R3

  **Dependencies:** None

  **Files:**
  - Modify: `Apps/ga-server/GaApi/Controllers/GovernanceController.cs`

  **Approach:**
  - Add `public string? FilePath { get; init; }` to the `GovernanceNode` record
  - In `ScanDirectory()`, capture the relative file path (relative to demerzel root) and set it on each node
  - Add `[HttpGet("file-content")]` endpoint: accepts `filePath` query param, validates path stays under demerzel root (`Path.GetFullPath` comparison), returns `{content, filePath, mediaType}`
  - Media type detection: `.md` → `text/markdown`, `.yaml/.yml` → `text/yaml`, `.json` → `application/json`, `.ixql` → `text/plain`

  **Patterns to follow:**
  - Existing `File.ReadAllText` usage in GovernanceController (lines 86, 185, 439)
  - Path validation pattern: resolve both paths with `Path.GetFullPath`, check `StartsWith`

  **Test scenarios:**
  - Valid file path returns content with correct media type
  - Path traversal attempt (`../../etc/passwd`) returns 400
  - Missing file returns 404
  - GovernanceNode now includes FilePath in graph response

  **Verification:**
  - Backend builds. `/api/governance/file-content?filePath=constitutions/asimov.constitution.md` returns markdown content.

- [ ] **Unit 2: IcicleDrawer component + drag handle**

  **Goal:** Create the drawer shell with drag-to-resize handle and split pane layout.

  **Requirements:** R1, R5, R6

  **Dependencies:** None (can be built in parallel with Unit 1)

  **Files:**
  - Create: `ReactComponents/ga-react-components/src/components/PrimeRadiant/IcicleDrawer.tsx`
  - Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/styles.css`
  - Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/ForceRadiant.tsx`

  **Approach:**
  - New `IcicleDrawer` component with props: `graphData`, `open`, `onToggle`, `height`
  - Drag handle: thin bar (8px) with a centered grip indicator. `onMouseDown` starts drag, `onMouseMove` updates height, `onMouseUp` ends. Double-click toggles open/close.
  - Split pane: flexbox row inside the drawer. Left (icicle placeholder div), right (content viewer placeholder div).
  - Height state managed in ForceRadiant, passed down. Min 150px, max 60vh.
  - Add drawer to ForceRadiant render tree below the canvas-area div.
  - CSS: `.icicle-drawer` with `height` transition, glassmorphism background, border-top.
  - Mobile: `@media (max-width: 640px)` — drawer becomes `position: fixed; inset: 0` with close button.

  **Patterns to follow:**
  - Side panel slide animation pattern in styles.css
  - Chat widget's mobile full-screen overlay pattern

  **Test scenarios:**
  - Drag handle reveals drawer on drag up
  - Double-click toggles open/close
  - Drawer coexists with rail side panel without overlap
  - Mobile: drawer opens as full-screen overlay
  - Resize stays within min/max bounds

  **Verification:**
  - Drawer opens/closes smoothly. Drag resize works. Mobile overlay works. Build and lint pass.

- [ ] **Unit 3: Icicle view integration in drawer**

  **Goal:** Wire IcicleView into the drawer's left pane with governance data.

  **Requirements:** R2, R4

  **Dependencies:** Unit 1, Unit 2

  **Files:**
  - Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/IcicleDrawer.tsx`

  **Approach:**
  - Transform flat `GovernanceNode[]` from graph data into a `RoadmapNode` tree: group nodes by `Type` (constitutions, policies, personas, schemas, pipelines, tests, etc.). Each type group becomes a parent `RoadmapNode`, individual files become children.
  - Color mapping: use existing governance type colors from `types.ts` or define a mapping (constitutions=gold, policies=blue, schemas=green, etc.)
  - Create a dedicated Three.js scene + camera for the icicle canvas (separate from the main 3D graph). Use a `<div ref>` in the left pane, attach a `WebGLRenderer`.
  - Call `createIcicleView(scene, camera, rootNode, { onNodeClick, onNodeHover })`.
  - On node click: if it has a `filePath`, fetch content and display in right pane. If it's a directory/group, zoom into that subtree (IcicleView handles this internally).
  - Resize observer on the icicle container to update renderer size.

  **Patterns to follow:**
  - `createIcicleView` API from `EcosystemRoadmap/IcicleView.ts`
  - ForceRadiant's Three.js setup pattern (renderer creation, resize observer)

  **Test scenarios:**
  - Governance graph data renders as icicle tiles
  - Clicking a file tile fetches and displays content
  - Clicking a group tile zooms into that subtree
  - Icicle view resizes correctly when drawer height changes
  - Empty graph data shows placeholder message

  **Verification:**
  - Icicle tiles visible and interactive. Click navigation works. No console errors.

- [ ] **Unit 4: File content viewer with markdown rendering**

  **Goal:** Display file content in the right pane with markdown rendering and syntax highlighting.

  **Requirements:** R3

  **Dependencies:** Unit 1, Unit 2

  **Files:**
  - Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/IcicleDrawer.tsx`
  - Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/styles.css`

  **Approach:**
  - Right pane shows selected file content. State: `selectedFilePath`, `fileContent`, `mediaType`, `loading`.
  - On file selection from icicle: fetch `GET /api/governance/file-content?filePath={path}`, set content.
  - Render based on mediaType:
    - `text/markdown`: `<ReactMarkdown remarkPlugins={[remarkGfm]}>{content}</ReactMarkdown>` with custom components for headings, code blocks (syntax-highlighted via `react-syntax-highlighter`).
    - `application/json` / `text/yaml` / other: wrap in a code block with appropriate language for syntax highlighting.
  - Loading state: spinner or skeleton.
  - No file selected: show placeholder text ("Select a file from the icicle view").
  - Style the content pane with monospace font, dark background, scrollable, padding.

  **Patterns to follow:**
  - `ChatMessage.tsx` in ga-client for `ReactMarkdown` + `react-syntax-highlighter` integration
  - `MarkdownCard.tsx` in IxqlViewer for simpler markdown pattern

  **Test scenarios:**
  - Markdown file renders with headings, lists, links, code blocks
  - YAML file renders with syntax highlighting
  - JSON file renders with syntax highlighting
  - Loading spinner shown while fetching
  - Error state shows "File not found" message
  - Switching files updates content immediately

  **Verification:**
  - File content displays with proper formatting. Markdown rendered, code highlighted. Build and lint pass.

- [ ] **Unit 5: Export and wire up**

  **Goal:** Export IcicleDrawer, add to index.ts, close issue #21.

  **Requirements:** R1-R6

  **Dependencies:** Units 1-4

  **Files:**
  - Modify: `ReactComponents/ga-react-components/src/components/PrimeRadiant/index.ts`

  **Approach:**
  - Export `IcicleDrawer` from index.ts
  - Verify full integration: drawer opens, icicle shows governance hierarchy, clicking file shows content, mobile overlay works
  - Final build + lint check

  **Verification:**
  - `npm run build` passes. `npm run lint` 0 errors. `dotnet build` passes. Full end-to-end: drag handle → icicle → click file → rendered content.

## System-Wide Impact

- **Interaction graph:** New `GET /api/governance/file-content` endpoint — leaf endpoint, no callbacks or middleware beyond existing CORS/rate limiting. GovernanceNode gains `FilePath` property — additive, no breaking change to existing consumers.
- **Error propagation:** File not found → 404. Path traversal → 400. Frontend handles both gracefully with error messages.
- **State lifecycle risks:** Drawer height stored in React state (not persisted). File content fetched on demand, not cached in frontend state. No orphaned state risk.
- **API surface parity:** GovernanceNode change is additive — existing clients ignore the new `FilePath` field.
- **Integration coverage:** Manual visual testing is primary. Backend file-content endpoint can be unit tested.

## Risks & Dependencies

- **IcicleView canvas in a drawer**: Creating a second Three.js renderer instance. Must not conflict with the main ForceRadiant renderer. Use a separate `<canvas>` element with its own scene.
- **File content security**: Path validation is critical — must prevent directory traversal. Use `Path.GetFullPath` comparison.
- **react-syntax-highlighter bundle size**: Already in the dependency tree via ga-client. If not tree-shaken into ga-react-components, the import may increase bundle size. Check if it's already bundled.

## Sources & References

- **Origin document:** [docs/brainstorms/2026-03-27-icicle-drawer-requirements.md](docs/brainstorms/2026-03-27-icicle-drawer-requirements.md)
- Related code: `IcicleView.ts`, `GovernanceController.cs`, `ForceRadiant.tsx`, `ChatMessage.tsx`, `MarkdownCard.tsx`
- Related issues: #21
