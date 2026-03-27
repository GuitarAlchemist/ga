---
date: 2026-03-27
topic: icicle-drawer
---

# Prime Radiant: Bottom Drawer with Icicle Navigator + File Content Viewer

## Problem Frame

Prime Radiant shows the governance graph as a 3D force-directed network, but there's no way to browse the governance file hierarchy spatially or view file contents inline. The DetailPanel shows a collapsible file tree for a single node, but you can't see the full governance structure at a glance. IcicleView.ts (D3 partition layout, zoomable tiles) already exists in EcosystemRoadmap but is orphaned — not integrated into Prime Radiant.

## Requirements

- R1. **Bottom drawer with drag handle**: A thin handle below the 3D canvas that the user can drag upward to reveal a resizable bottom drawer. Default collapsed (handle only visible). Can be open alongside a rail side panel.
- R2. **Icicle navigator in drawer**: The left portion of the drawer shows an icicle/partition view of the governance file hierarchy (`governance/demerzel/`). Tiles represent directories and files, color-coded by type (constitutions, policies, schemas, beliefs, signals). Click a tile to zoom into that subtree. Click again to zoom out.
- R3. **File content viewer**: The right portion of the drawer shows the content of the selected file. Markdown files rendered with formatting, YAML/JSON/code files with syntax highlighting. Updates when a new tile is selected.
- R4. **Governance data source**: The icicle view is populated from the governance graph data already loaded by ForceRadiant (via DataLoader). Files map to governance nodes. No additional API calls needed.
- R5. **Drawer resize**: User can drag the handle to resize the drawer height (min 150px, max 60vh). Double-click the handle to toggle open/close.
- R6. **Mobile behavior**: On phone (<640px), the drawer opens as a full-screen overlay with a close button (same pattern as rail panels). Drag handle becomes a tap-to-toggle bar.

## Success Criteria

- Dragging the handle up reveals the icicle view with governance file tiles
- Clicking a tile shows its file content with proper formatting/highlighting
- Drawer coexists with 3D canvas and rail panels without overlap
- Works on tablet and phone

## Scope Boundaries

- No editing of file contents — read-only viewer
- No new API endpoints — use existing governance graph data + fetch file content from governance directory
- No changes to the existing IcicleView rendering logic — reuse as-is, just wire new data
- No changes to EcosystemRoadmap — IcicleView stays there too (shared code)

## Key Decisions

- **Drag handle (independent of rail)**: The drawer is a different interaction axis (bottom vs right). Keeping it independent avoids overloading the rail and allows both to be open simultaneously.
- **Split pane (icicle left, content right)**: Standard file explorer pattern. Icicle gives spatial overview, content pane gives detail.
- **Rendered markdown + syntax highlighting**: Worth the small effort — governance files are mostly markdown and YAML, both benefit greatly from formatting.

## Dependencies / Assumptions

- IcicleView.ts from EcosystemRoadmap is reusable without modification
- Governance graph data from DataLoader includes file hierarchy information
- File content can be fetched from the governance directory (may need a simple file-read endpoint or can use the existing graph data if content is included)

## Outstanding Questions

### Deferred to Planning
- [Affects R3][Needs research] Whether governance file content is already available in the graph data or needs a new fetch endpoint
- [Affects R2][Technical] How to adapt IcicleView's data format (currently EcosystemRoadmap hierarchy) to governance file hierarchy
- [Affects R3][Technical] Which markdown renderer and syntax highlighter to use (check what's already in the project)

## Next Steps

→ `/ce:plan` for structured implementation planning
