# Demo Triage Report — 2026-03-26

## Summary
- **20 standalone demos** (work without backend)
- **10 demos needing backend** (3 have graceful fallback)
- **2 partial demos** (marked partial in TestIndex)
- **1 basic demo** (legacy, limited features)
- **1 missing page file** (GA Chat defined inline in main.tsx, not a standalone page)

## Standalone (Pure Frontend)

| # | Demo | Tech | Status | Notes |
|---|------|------|--------|-------|
| 1 | MinimalThreeInstrument | Three.js + WebGPU | complete | Universal 3D instrument renderer, YAML database, no API |
| 2 | ThreeFretboard | Three.js + WebGPU | complete | 3D fretboard, PBR materials, orbit controls |
| 3 | ThreeHeadstock | Three.js + WebGPU | complete | 3D headstock with multiple styles and tuning pegs |
| 4 | RealisticFretboard | Pixi.js v7 | complete | 2D realistic fretboard with wood grain |
| 5 | HarmonicNavigator3D | Three.js + WebGL + BSP | complete | Pure frontend BSP tetrahedral cells, quaternion modulation |
| 6 | Sunburst3D | Three.js + WebGL | complete | 3D sunburst visualization, LOD system |
| 7 | ImmersiveMusicalWorld | Three.js + WebGL + FPS | complete | First-person world, floating platforms, skybox |
| 8 | FluffyGrass | Three.js + Custom Shaders | complete | Billboard grass, instancing, wind animation |
| 9 | Ocean Shader | Three.js + GLSL | complete | Gerstner waves, Fresnel, specular highlights |
| 10 | ChordProgressionViz | Three.js + WebGL | complete | Circle of fifths, auto-progression, stacked notes |
| 11 | SandDunes | Three.js + GLSL | complete | Procedural desert, ridged multifractal noise |
| 12 | Guitar3DViewer | Three.js + GLTF | complete | GLTF/GLB loading, PBR materials, IBL |
| 13 | HandAnimation | Three.js + GLTF + Skeletal | complete | Rigged hand model, finger control, WebGPU/WebGL |
| 14 | 3DModelsGallery | Three.js + GLTF + WebGL | complete | Blender models gallery, orbit controls |
| 15 | CapoTest | Multiple Renderers | complete | Capo comparison across renderers |
| 16 | 3DCapoModel | Three.js + GLTF + Sketchfab | complete | Sketchfab model with geometric fallback |
| 17 | EcosystemRoadmap | Three.js WebGPU + D3 + Jotai | complete | Icicle, Poincare disk/ball — no API calls |
| 18 | IntelligentAIDemo | React + MUI | complete | All logic is client-side, no fetch calls |
| 19 | MusicTheoryTest | Three.js + WebGPU | **see note** | Component accepts `apiBaseUrl` prop but renders 3D without it |
| 20 | PrimeRadiant | Three.js + WebGL + Bloom | complete | Has `liveDataUrl` prop but **falls back to static sampleData** |

## Needs Backend

| # | Demo | API Endpoints Used | Fallback Exists? | Status |
|---|------|--------------------|------------------|--------|
| 1 | BSP Musical Analysis | `https://localhost:7184/api/bsp/*` | **Yes** — demo mode on API failure | complete |
| 2 | BSP DOOM Explorer | `https://localhost:7184/api/bsp/*` | **Yes** — demo mode on API failure | complete |
| 3 | Floor0 Navigation | `https://localhost:7001/api/music-rooms/floor/*` | **No** — shows error state | complete |
| 4 | InstrumentIcons | `https://localhost:7001/Instruments` | **No** — shows error message | complete |
| 5 | InverseKinematics | `http://localhost:5232/api/biomechanics/analyze-chord` | **No** — API call on user action | complete |
| 6 | MusicHierarchyDemo | `fetchHierarchyItems/fetchHierarchyLevels` (GraphQL) | **No** — fails to load data | complete |
| 7 | GrothendieckDSL | `https://localhost:7001/api/dsl/parse-grothendieck` | **No** — parse button fails | complete |
| 8 | ChordProgressionDSL | `https://localhost:7001/api/dsl/parse-chord-progression` | **No** — parse button fails | complete |
| 9 | FretboardNavDSL | `https://localhost:7001/api/dsl/parse-fretboard-navigation` | **No** — parse button fails | complete |
| 10 | TabConverter | `https://localhost:7003/api/TabConversion/*` | **No** — convert/formats fail | complete |

### GA Chat (special case)
- **GA Chat (AG-UI)** — defined inline in `main.tsx` (not a separate page file), requires `GA_API_URL/api/chatbot/agui/stream` (SSE streaming). **No fallback.** Fully backend-dependent.

## Partial / Broken

| # | Demo | Status | Issue | Fix Needed |
|---|------|--------|-------|-----------|
| 1 | WebGPUFretboard | **partial** | Pixi.js v8 WebGPU — marked partial in TestIndex | Needs feature parity with RealisticFretboard (Pixi v7) |
| 2 | FretboardWithHand | **partial** | Requires `apiBaseUrl` for hand pose data | Needs fallback hand poses when API unavailable |
| 3 | GuitarFretboard | **basic** | Legacy SVG port from Delphi — minimal features | Low priority; superseded by ThreeFretboard and RealisticFretboard |

## Recommendations

### Priority 1: Quick wins — add fallback states to API-dependent demos
- **3 DSL demos** (Grothendieck, ChordProgression, FretboardNav): Add client-side example parse results so the UI is explorable without backend. The examples are already in the UI — just pre-populate the result panel.
- **InstrumentIcons**: Ship a static JSON snapshot of instrument data as fallback.
- **MusicHierarchyDemo**: Add a static dataset fallback for demo purposes.
- **TabConverter**: Include sample conversion output to show the UI working.

### Priority 2: Upgrade partial demos
- **WebGPUFretboard**: Bring Pixi.js v8 version to feature parity with v7 RealisticFretboard.
- **FretboardWithHand**: Add hardcoded hand poses for common chords (C, G, Am, etc.) so the demo works standalone.

### Priority 3: Polish standalone demos
- All 20 standalone demos work without backend — these are the best candidates for public demo deployment.
- **MusicTheoryTest** and **PrimeRadiant** are "soft" standalone — they work but show backend-related UI hints. Consider hiding backend UI when no API is configured.

### Priority 4: GA Chat
- Extract from `main.tsx` into its own page file (`GAChatTest.tsx`).
- Add a mock/demo mode that simulates SSE responses for offline demo.

### Architecture note
- **BSP demos** are the gold standard for graceful degradation — they silently fall back to demo mode when the API is unavailable. This pattern should be replicated across all API-dependent demos.
