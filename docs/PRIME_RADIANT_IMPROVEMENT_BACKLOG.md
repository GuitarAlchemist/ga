# Prime Radiant Improvement Backlog

**Date:** 2026-04-06
**Sources:** 3 parallel deep investigations (UX Research, Cross-Repo Integration, Panel Audit)
**Total findings:** 76 across 9 repos

## CRITICAL (6 findings — ship-blocking)

### C1. Zero focus-visible styles anywhere
**File:** `styles.css` (entire 11,485-line file)
No keyboard focus indicator on any element. Fails WCAG 2.1 AA.
**Fix:** `.prime-radiant *:focus-visible { outline: 2px solid #FFD700; outline-offset: 2px; }`

### C2. styles.css is 11,485 lines — monolithic
One file for 30+ components. 270KB downloaded on every page load.
**Fix:** Extract per-component CSS modules.

### C3. Triplicate CSS blocks at lines 5100, 9539, 9996
Same `@media (max-width: 768px)` rules pasted 3 times.
**Fix:** Delete duplicates at lines 9539 and 9996.

### C4. Node tooltip fixed at top-center, not near the hovered node
`PrimeRadiant.tsx:407-422` — tooltip at `left: 50%, top: 16` regardless of node position.
**Fix:** Position at raycast screen coordinates.

### C5. Demerzel face thumbnail invisible
`styles.css:1217` — `background: rgba(13, 17, 23, 0.6)` nearly matches page background.
**Fix:** Increase contrast, add gold border, pulse animation on first visit.

### C6. No loading/empty states — mock data shown silently
`AgentPanel.tsx:178`, `ActivityPanel.tsx:425` — hardcoded fallback data with no indicator.
**Fix:** Add `[DEMO]` badge when fallback data is active. Article 1 (Truthfulness).

## IMPORTANT — UX (14 findings)

### I1. Search dropdown inline styles, no keyboard navigation
`PrimeRadiant.tsx:296-347` — 15+ CSS properties in JSX. Only Enter/Escape handled.
**Fix:** CSS classes + ArrowUp/Down + aria-activedescendant.

### I2. DetailPanel close button is literal "x" character
`DetailPanel.tsx:290` — inconsistent with other panels using `&times;`.
**Fix:** Replace with `&times;` or SVG icon.

### I3. ActivityPanel fires 35 GitHub API calls per refresh
7 repos × 5 endpoints = 35 calls every 60s. Hits rate limit in 2 minutes.
**Fix:** GraphQL batching or server-side aggregation.

### I4. TutorialOverlay has placeholder Discord link
`TutorialOverlay.tsx:7` — `https://discord.gg/YOUR_INVITE` — broken link.
**Fix:** Replace with real invite URL.

### I5. AlgedonicPanel is extremely dense — 7 interactive zones
`AlgedonicPanel.tsx:292-737` — filter, graph, form, fix-all, timeline, gaps, create-all.
**Fix:** Split into tabs: Active Signals / Create Signal / Gaps.

### I6. Legend text fails WCAG AA contrast
`styles.css:837` — `#8b949e` on `rgba(22,27,34,0.85)` = 3.2:1 (need 4.5:1).
**Fix:** Lighten to `#b1b8c0`.

### I7. Health bar overlaps search bar at 768px
Both at `top: 16px` — health bar centered, search bar left-aligned, they collide.
**Fix:** Adjust health bar position in 768px media query.

### I8. Inconsistent toggle arrow characters across panels
ASCII `>` in Backlog/Agent, Unicode `▶` in Activity/Algedonic.
**Fix:** Shared SVG chevron component.

### I9. Time slider has no functional effect
`PrimeRadiant.tsx:389-404` — slider stores value but nothing reads it.
**Fix:** Wire to node filtering by date, or remove until implemented.

### I10. ForceRadiant.tsx imports 80+ modules eagerly
All panels + shaders loaded on initial render.
**Fix:** `React.lazy()` for panels not visible by default.

### I11. Inconsistent responsive breakpoints (640/768/1024)
IconRail uses 640px, health bar 768px, tablet 1024px.
**Fix:** Shared breakpoint constants.

### I12. Mobile: 9+ features hidden via display:none with no alternative
`styles.css:3712-3919` — backend status, tutorial, brainstorm, triage all hidden.
**Fix:** Mobile-friendly alternatives for critical features.

### I13. No `prefers-reduced-motion` support
15+ `@keyframes` with no reduced-motion media query.
**Fix:** `@media (prefers-reduced-motion: reduce) { * { animation-duration: 0.01ms !important; } }`

### I14. "Fix All" button has no confirmation dialog
`AlgedonicPanel.tsx:346-355` — immediately triggers auto-remediation. Article 3 (Reversibility).
**Fix:** Confirmation dialog.

## IMPORTANT — Integration Gaps (10 findings)

### G1. 5 parked .NET controllers with full implementations
`AdaptiveAI`, `AdvancedAI`, `VectorSearch`, `SemanticSearch`, `EnhancedPersonalization` in `_Parked/`.
**Fix:** Unpark progressively; wire to panels.

### G2. ix MCP governance tools not visualized
`ix_governance_belief`, `ix_governance_check`, `ix_governance_persona` — no panel.
**Fix:** GovernanceCompliancePanel.tsx consuming ix MCP.

### G3. 7 Demerzel schemas have no live dashboard
`governance-evolution`, `blackboard-state`, `conscience-weekly-report`, `compounding-report`, `capability-registry`.
**Fix:** GovernanceMetricsDashboard.tsx polling /api/governance/metrics.

### G4. Discord bot data doesn't flow to Prime Radiant
Voice analysis, domain classification, visual critic, governance tools — all one-way.
**Fix:** WebSocket bridge → DiscordActivityPanel.tsx.

### G5. Spectral agent topology endpoint uncalled
`/api/spectral/agent-loop` computes agent interaction graphs.
**Fix:** AgentSpectralPanel.tsx.

### G6. TARS v2 knowledge graph not synced
MCP server exposes temporal knowledge graph.
**Fix:** KnowledgeGraphPanel.tsx.

### G7. Godot scene hierarchy not introspectable
24+ MCP command categories available.
**Fix:** GodotSceneInspectorPanel.tsx.

### G8. governance_graph (ix) is static, not live-updated
`prime_radiant.rs:67` — scanned at startup, never refreshed.
**Fix:** Poll or watch for governance artifact changes.

### G9. 6 API endpoints defined in client but no backend
`AIApiService.ts` has methods for endpoints that don't exist.
**Fix:** Either implement or remove dead client code.

### G10. AdminInbox has no data source
254 LOC defined, no API connected.
**Fix:** Pull governance decisions from Demerzel state.

## NICE-TO-HAVE (20 findings)

| # | Finding | File | Fix |
|---|---------|------|-----|
| N1 | Node labels invisible at default zoom | NodeRenderer.ts:136 | Min opacity 0.3 |
| N2 | Triplicate `timeAgo()` implementations | Activity/Algedonic/Agent panels | Shared utility |
| N3 | Sparkline trajectory is simulated | SeldonDashboard.tsx:162 | Label as "projected" |
| N4 | BacklogPanel "Start /feature" only console.logs | BacklogPanel.tsx:300 | Wire or disable |
| N5 | Node particles update every frame (perf) | NodeRenderer.ts:153 | Frustum cull, skip frames |
| N6 | Edge particles don't scale with perf tier | EdgeRenderer.ts:20 | Halve on low tier |
| N7 | DetailPanel file tree files not clickable | DetailPanel.tsx:162 | Add onFileClick callback |
| N8 | Sidebar group labels cryptic (GOV, KNOW, VIZ) | IconRail.tsx:69 | Full labels on desktop |
| N9 | Health metrics show jargon without explanation | PrimeRadiant.tsx:352 | Add "i" info icon |
| N10 | Multiple panels poll independently | Activity/Agent/LLM | Shared polling manager |
| N11 | Chat panel positioned with magic pixels | styles.css:1282 | Relative positioning |
| N12 | LLMStatus uses emoji for provider icons | LLMStatus.tsx:115 | Consistent SVG icons |
| N13 | BacklogPanel localStorage btoa fails on Unicode | BacklogPanel.tsx:97 | Proper hash + TTL |
| N14 | AlgedonicPanel uses regex for slug | AlgedonicPanel.tsx:175 | String-based slugify |
| N15 | Error boundary retry doesn't re-init engine | PrimeRadiant.tsx:63 | Add retryKey to deps |
| N16 | No skip-to-content link | PrimeRadiant.tsx | Add sr-only skip link |
| N17 | DeviceContext.tsx has no consumers | DeviceContext.tsx | Wire to responsive layout |
| N18 | GalacticClock is decorative only | GalacticClock.tsx | Connect to governance timing |
| N19 | PanelRegistry vs actual components mismatched | 5 IXQL panels unregistered | Add to registry |
| N20 | YouTube PiP video IDs may be wrong | PlanetPiP.tsx | Verify all 11 IDs |

## PANEL QUALITY SCORECARD

| Quality | Count | Panels |
|---------|:---:|--------|
| 5/5 Production | 3 | IxqlGrid, IxqlViz, QAPanel |
| 4/5 Good | 10 | Activity, CICD, Detail, Library, Brainstorm, GIS, IxqlForm, IxqlTruthLattice, AssetProvenance, Devices |
| 3/5 Adequate | 7 | Backlog, Agent, ClaudeCode, CourseViewer, Presence, SeldonFaculty, Tutorial |
| Unaudited | 4 | Godot, LunarLander, LiveNotebook, ChatWidget |

## CROSS-REPO INTEGRATION MATRIX

| Source | Target | Gap | Priority |
|--------|--------|-----|:---:|
| ix MCP governance | PR panels | No panel | P1 |
| Demerzel schemas | Live dashboard | No viz | P1 |
| .NET parked controllers | PR features | Commented out | P2 |
| Discord bot streams | PR activity | No bridge | P2 |
| TARS knowledge graph | PR viz | No connector | P3 |
| Godot scene hierarchy | PR inspector | No panel | P3 |
| Spectral analytics | PR topology | Endpoint unused | P3 |

## EFFORT ESTIMATE

| Severity | Count | Est. Hours |
|----------|:---:|:---:|
| Critical | 6 | 8-12 |
| Important UX | 14 | 20-30 |
| Important Integration | 10 | 30-40 |
| Nice-to-have | 20 | 15-20 |
| **Total** | **50** | **73-102** |
