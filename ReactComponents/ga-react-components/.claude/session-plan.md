# Session Plan: TSL Migration — ShaderMaterial → NodeMaterial + WebGPU

**Created:** 2026-04-03
**Intent Contract:** See .claude/session-intent.md

## What You'll End Up With

All 25 ShaderMaterials migrated to TSL NodeMaterials, running on WebGLRenderer first (renderer-agnostic), with a clear path to flip the WebGPU switch. Zero visual regressions. Each shader testable independently.

## Key Insight

TSL is **renderer-agnostic** — NodeMaterials auto-compile to GLSL on WebGL2 and WGSL on WebGPU. We do NOT need to solve the 3d-force-graph renderer problem before migrating materials. Phase 1 (materials) and Phase 2 (renderer) are fully decoupled.

## How We'll Get There

### Phase 1: Material Migration (can run on current WebGLRenderer)

Migrate each ShaderMaterial → TSL NodeMaterial. Order by dependency (shared patterns first) and risk (simpler shaders first, flagship last).

#### Wave 1 — Simple Shaders (low risk, establish patterns)
| # | Shader | File | Lines | Complexity | Notes |
|---|--------|------|-------|------------|-------|
| 1 | Orbit trail | SolarSystem.ts | 779 | Minimal | Color + alpha fade. Good first migration. |
| 2 | Star field points | LunarLanderEngine.ts | 1224 | Minimal | Per-vertex color, gl_PointCoord disc. |
| 3 | Sun corona glow | SolarSystem.ts | 1054 | Minimal | Single Fresnel shader. |
| 4 | Atmosphere Fresnel | SolarSystem.ts | 1118 | Low | Fresnel glow, reused on multiple planets. |
| 5 | Titan atmosphere | SolarSystem.ts | 1369 | Low | Same pattern as #4. |
| 6 | Ring glow | SolarSystem.ts | 1705 | Low | Additive glow layer. |
| 7 | Marker pins | SolarSystem.ts | 2440 | Low | Pulsing geometry effect. |

#### Wave 2 — Animated Shaders (medium complexity)
| # | Shader | File | Lines | Complexity | Notes |
|---|--------|------|-------|------------|-------|
| 8 | God ray orange | ForceRadiant.tsx | 2610 | Low-Med | Sine interference. |
| 9 | God ray cyan | ForceRadiant.tsx | 2636 | Low-Med | Clone of #8, different params. |
| 10 | Aurora borealis | SolarSystem.ts | 1608 | Medium | Animated waves on polar sphere. |
| 11 | Storm vortex (GRS) | SolarSystem.ts | 1786 | Medium | Procedural swirl + time animation. |
| 12 | Engine exhaust | LunarLanderEngine.ts | 2055 | Medium | Particle system with age fade. |
| 13 | Earth atmosphere | LunarLanderEngine.ts | 1324 | Medium | Fresnel + per-frame viewVector. |
| 14 | Skybox nebula | ForceRadiant.tsx | 2328 | Medium | Perlin FBM — reuse TSLNoiseLib. |
| 15 | Milky Way band | MilkyWay.ts | 166 | Medium | Galactic dust lanes + starfield. |

#### Wave 3 — Complex Shaders (high complexity, flagship visuals)
| # | Shader | File | Lines | Complexity | Notes |
|---|--------|------|-------|------------|-------|
| 16 | Volumetric node core | ForceRadiant.tsx | 413 | High | Raymarched governance node glow. |
| 17 | Sun plasma (swap in) | SolarSystem.ts | 915 | High | **Already done** — SunMaterialTSL.ts exists. Just wire it in. |
| 18 | Saturn rings | SolarSystem.ts | 1252 | High | Cassini gap, density, backlit rendering. |
| 19 | Procedural planet | SolarSystem.ts | 709 | High | Texture + displacement + clouds. |
| 20 | Moon/asteroid procedural | SolarSystem.ts | 720/747 | High | Procedural noise generation. |

#### Wave 4 — Holographic Shaders (aesthetic-critical)
| # | Shader | File | Lines | Complexity | Notes |
|---|--------|------|-------|------------|-------|
| 21 | Holo face (rigged) | DemerzelRiggedFace.ts | 106 | Medium | Gold wireframe + Fresnel scanlines. |
| 22 | Holo face (procedural) | DemerzelFace.ts | 92 | Medium | Same aesthetic, different geometry. |
| 23 | TARS slab holo | TarsRobot.ts | 71 | Medium | Gold holographic monolith. |

### Phase 2: Post-Processing Migration (requires renderer consideration)

| # | Pass | File | Notes |
|---|------|------|-------|
| 24 | CausticsPass | shaders/CausticsPass.ts | ShaderPass → TSL post node |
| 25 | DispersionPass | shaders/DispersionPass.ts | Hexavalent 6-channel mapping |
| 26 | MoebiusPassTSL | shaders/MoebiusPassTSL.ts | Already named TSL but still ShaderPass |
| 27 | Chromatic aberration | ForceRadiant.tsx ~1698 | Inline ShaderPass |

Post-processing migration uses TSL's `pass()`, `mrt()`, `bloom()`, `gaussianBlur()` etc. This replaces EffectComposer entirely with WebGPU's native PostProcessing pipeline.

### Phase 3: Renderer Swap (final step)

1. Replace `import * as THREE from 'three'` with `import * as THREE from 'three/webgpu'` in entry points
2. Handle async renderer init (`await renderer.init()`)
3. Replace EffectComposer with `PostProcessing` class
4. Add WebGL2 fallback detection for older browsers
5. Test 3d-force-graph compatibility (may need custom renderer injection)

## Shared Infrastructure (already exists)

- `TSLNoiseLib.ts` — noise3, fbm3, fbm6 (used by all procedural shaders)
- `TSLUniforms.ts` — QualityTier type definitions
- `SunMaterialTSL.ts` — Reference implementation for migration pattern
- `CymaticsTSL.ts`, `VoronoiShellTSL.ts`, `FlowFieldParticleTSL.ts`, `ReactionDiffusionTSL.ts` — Additional TSL examples

## Migration Pattern (per shader)

```typescript
// 1. Create new file: shaders/<Name>TSL.ts
// 2. Import from 'three/tsl' (renderer-agnostic)
// 3. Convert GLSL uniforms → uniform() / time / uv() etc.
// 4. Convert GLSL functions → Fn() blocks
// 5. Convert vertex shader → positionLocal manipulation or geometry nodes
// 6. Convert fragment shader → colorNode / emissiveNode assignment
// 7. Export factory function matching existing signature
// 8. Swap in the calling code (one line change)
// 9. Visual regression test
```

## Phase Weights

```
DISCOVER ██ 5%       — TSL docs fetched, inventory complete
DEFINE   ████ 10%    — Migration order and patterns established
DEVELOP  ████████████████████ 55%   — Main shader conversion work
DELIVER  ██████████████ 30%  — Testing, visual validation, perf checks
```

## Provider Availability
🔴 Codex CLI: Available ✓
🟡 Gemini CLI: Available ✓
🟤 OpenCode: Available ✓
🟠 Ollama: Available ✓
🔵 Claude: Available ✓

## 🐙 DEBATE CHECKPOINTS IN THIS PLAN

🔸 After Wave 1 complete: "Does the TSL pattern work reliably on WebGLRenderer?"
   Triggers: validation that renderer-agnostic approach holds

🔸 After Wave 3 complete: "Are flagship visuals (sun, rings, planets) regression-free?"
   Triggers: 1-round visual quality debate before proceeding to holographic shaders

🔸 Before Phase 3 renderer swap: "Is the codebase ready for WebGPU?"
   Triggers: full audit of remaining WebGL dependencies

## YOUR INVOLVEMENT: Checkpoint after each wave

## Execution Commands

To execute this plan:
```
/octo:embrace "TSL Migration — ShaderMaterial to NodeMaterial"
```

Or execute waves individually:
```
Wave 1: /octo:develop "Migrate simple shaders (orbit, stars, fresnel) to TSL"
Wave 2: /octo:develop "Migrate animated shaders (god rays, aurora, nebula) to TSL"
Wave 3: /octo:develop "Migrate complex shaders (volumetric, sun, rings, planets) to TSL"
Wave 4: /octo:develop "Migrate holographic shaders to TSL"
Phase 2: /octo:develop "Migrate post-processing to TSL pipeline"
Phase 3: /octo:develop "Swap renderer to WebGPU"
```

## Success Criteria
- All 25 ShaderMaterials replaced with TSL NodeMaterials
- Zero visual regressions (screenshot comparison per shader)
- Performance maintained or improved (FPS benchmarks)
- Works on current WebGLRenderer (Phase 1)
- WebGPU renderer enabled with WebGL2 fallback (Phase 3)
- Each wave independently testable and shippable

## Next Steps
1. Review this plan
2. Adjust if needed (re-run /octo:plan)
3. Execute with /octo:embrace when ready
