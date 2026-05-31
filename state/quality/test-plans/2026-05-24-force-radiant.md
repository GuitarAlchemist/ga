---
title: ForceRadiant 3D Viz High-Value Test Plan
target: ReactComponents/ga-react-components/src/components/PrimeRadiant/ForceRadiant.tsx
status: draft
generated_at: 2026-05-24T19:30:00Z
generator: claude-opus-4-7
business_value_confidence: 0.90
effort_tshirt: L
---

# ForceRadiant 3D Viz High-Value Test Plan

`ForceRadiant.tsx` is the **headline 3D viz** for the whole demo
(`demos.guitaralchemist.com/`) — a 4 590-line React component that
mounts `3d-force-graph` + Three.js + ~40 lazy-loaded panels, the
governance graph, GIS overlays, the Demerzel face, every shader pass,
and the IxQL command surface. Annotated `@ai:business-value conf=0.90`.

## Coverage gap summary

The Prime Radiant tree has a healthy unit suite for *adjacent* primitives
— `PlanetNav.test.tsx`, `IxqlControlParser.test.ts`, `IxqlPipeEngine.test.ts`,
`shaders/Ocean/shoreMask.test.ts` — but **nothing tests `ForceRadiant.tsx`
itself**. The component is too big to render end-to-end in jsdom
(WebGL, Web Workers, postprocessing). Strategy: extract & test the
**pure logic islands** that already live in this file (admin gate,
mobile detection, fractal-texture cache key, ripple/propagation
bookkeeping, edge-color lookup) without mounting the canvas.

Gaps:

- **`_checkIsAdmin()`** — token-membership check + localhost auto-admit
  + SSR safety. Comment block calls it a "UI-only gate, not a security
  boundary"; pin both behaviors (localhost true, token-match true,
  token-mismatch false, SSR false).
- **Mobile / low-end detection** (`_isMobileDevice`, `_isLowEndDevice`) —
  driven by `navigator.userAgent` + `hardwareConcurrency`; no test.
- **`generateFractalTexture` cache key** — `${baseColor.getHexString()}-${complexity}`
  must collide deterministically (same color+complexity returns cached
  texture). Easy to break with a future "include time-jitter" tweak.
- **`TYPE_SIZE` / `TYPE_PARTICLES` / `EDGE_COLORS` / `EDGE_WIDTH` tables**
  — drift here changes the visual hierarchy belief documented at the top
  of the file. Pin membership + values.
- **`PR_ADMIN_TOKENS` set non-empty** — a future `.replace(...)` typo that
  empties the set would silently break local-dev admin mode for everyone.
- **`MAX_CONCURRENT_RIPPLES`, `RIPPLE_DURATION`, `COMPOUNDING_THRESHOLD`,
  `SURGE_BLOOM_*`** — algedonic-ripple constants the README documents.
  Pin them as "if you change these, also update docs/plans/...".
- **Mount smoke** — `render(<ForceRadiant />)` does not throw in jsdom
  with WebGL/canvas stubbed. (Tests the import graph + lazy-load
  references, nothing visual.)

## Test cases (8 proposed)

| # | Name | Type | What it covers | Fixtures / mocks | Overlaps |
|---|---|---|---|---|---|
| 1 | `_checkIsAdmin_LocalhostHostnames_ReturnsTrue` | unit | Stub `window.location.hostname` to `'localhost'`, `'127.0.0.1'`, `'::1'` → returns `true` (no token needed). | `vi.stubGlobal('window', ...)`. Requires extracting helper into its own file or testing via export-for-tests. | none. |
| 2 | `_checkIsAdmin_DeployedHost_TokenMatch_ReturnsTrue_MismatchReturnsFalse` | unit | Hostname `'demos.guitaralchemist.com'`; localStorage has the known SHA256 token → true; wrong token → false; no token → false. | `vi.stubGlobal('window', ...)` + `vi.stubGlobal('localStorage', ...)`. | none. |
| 3 | `_checkIsAdmin_SsrSafety_ReturnsFalse` | unit | `typeof window === 'undefined'` → returns false (no throw). | use `vi.stubGlobal('window', undefined)`. | none. |
| 4 | `generateFractalTexture_CacheKey_DeterministicReuse` | unit | Calling twice with the same `(baseColor, complexity)` returns the **same** texture instance (`===`); different complexity returns different. | jsdom canvas stub via `vi.mock('three', ...)` or rely on jsdom 2D canvas. | none. |
| 5 | `TypeSize_TypeParticles_EdgeColors_TablesAreStable` | unit | Snapshot each table: `Object.keys(TYPE_SIZE).sort()` matches the 8 documented `GovernanceNodeType` values; `TYPE_SIZE.constitution === 30`; `EDGE_COLORS['lolli'] === '#FF444466'`. | export tables for tests via `// @internal` re-export. | none. |
| 6 | `PR_ADMIN_TOKENS_NotEmpty_AndKnownLocalToken_Present` | unit | The set has size ≥ 1 and contains the documented SHA256 prefix `'8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918'`. | direct. | none. |
| 7 | `AlgedonicConstants_Stable_LockedToDocs` | unit | `MAX_CONCURRENT_RIPPLES===10`, `RIPPLE_DURATION===2.0`, `COMPOUNDING_THRESHOLD===3`, `SURGE_BLOOM_STRENGTH===1.2`. Comment in test references the doc the value is mirrored from. | direct. | none. |
| 8 | `ForceRadiant_MountSmoke_DoesNotThrow` | unit (smoke) | `render(<ForceRadiant />)` in jsdom with WebGL/canvas mocked completes without error; component returns at least a root `<div>`. | mock `3d-force-graph` to a no-op factory; mock `three/webgpu`; mock all `React.lazy` panel imports. | none. |

## Suggested file locations

- `ReactComponents/ga-react-components/src/components/PrimeRadiant/ForceRadiant.test.tsx`
  (co-located, matches existing convention — cases #4, #5, #6, #7, #8).
- `ReactComponents/ga-react-components/src/components/PrimeRadiant/AdminGate.test.ts`
  **after** extracting `_checkIsAdmin` to `AdminGate.ts` (cases #1–#3 —
  extraction is one-line surgical change, not a refactor).

## Effort estimate

**L** (large). Reasons:
1. **Extract-for-test step** for `_checkIsAdmin` and the constants (case #5
   needs them re-exported) — small surgical edit, but it's source touch on a
   load-bearing file; review-worthy.
2. **Smoke mount (#8)** needs a non-trivial `vi.mock` setup for
   `3d-force-graph` + `three/webgpu` + every lazy panel; ≈30–50 LOC of
   mocks but reusable for future Prime Radiant component tests.
3. The 4 590-line file size makes review of any source-touching change
   slow.

Estimate 3–5 dev-days.

## Rubric

The strategy is **deliberate** — we are not testing the visual output (a
Playwright + canvas-diff job already does that on the live deploy via the
`/dashboard` QA suite). We are testing the **safety-rail logic** that
governs which UI paths fire (admin mode, mobile downgrade, ripple budgets,
edge palette). That logic is the part most likely to silently regress
during a refactor.
