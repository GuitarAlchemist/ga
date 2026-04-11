---
date: 2026-04-11
topic: planet-ocean-zoom-v3
status: production-spec
supersedes: v1 (2026-04-11-planet-ocean-zoom-brainstorm.md), v2 (2026-04-11-planet-ocean-zoom-brainstorm-v2-review-response.md)
ruling: octopus-multi-ai-vote (3-way Team mode, tallied 2026-04-11)
---

# Planet Ocean Zoom v3 — Production Spec

## Status

This is the implementation spec. v1 was the exploratory design, v2 was the multi-AI review response, v3 is the ship-ready plan derived from the octopus decision vote. Everything below is build-it-as-written.

## Octopus ruling (for audit)

Three AI reviewers — Codex CLI, Gemini CLI, a Claude subagent — voted on three decisions from v2. Aggregate results:

| Decision | Ruling | Vote | Rationale |
|---|---|---|---|
| **D-v2-1** Architecture | **Hybrid: Path B + 1h feasibility spike, fallback to Path A if spike reveals blocker** | 3-0 Path B direction (2 Hybrid, 1 straight B) | Path B's projected-grid raycasting structurally resolves six of seven v1 review critiques (C1/C3/C4/C5/C6); the 1h spike is cheap insurance against the two unvalidated unknowns (TSL fragment-shader ray-sphere + depth-buffer compat with the atmosphere Fresnel rim) |
| **D-v2-2** GEBCO asset delivery | **git-LFS** | 2-1 LFS | Base clones stay slim, no runtime S3 dependency, asset versioned + reproducible — but this is **the first LFS asset in GA** and requires one-time repo initialization (see Pre-flight) |
| **D-v2-3** Chord-reactive feature | **Phase 2 opt-in, default-off** | 3-0 | Brand-defining, ~4h cost on existing `AnalyserNode` pipeline, zero risk to base ocean when opt-in |

Aggregate confidence: Medium-High. The Medium came from Claude subagent flagging the depth-buffer interaction risk with the atmosphere shader — explicitly covered by the Phase 0 spike.

## Problem frame (compressed)

When the Prime Radiant camera zooms below ~50-80 km altitude above Earth (the Nyquist limit of the existing 2 k specular map at ~19.6 km/texel), the ocean becomes a visibly flat blue texture with specular shimmer. The goal is to render a believable animated ocean at close zoom without touching the existing `PlanetSurfaceTSL.ts` shader for far-field rendering. See v1 for full failure-mode analysis.

## Architecture

### Projected-grid raycasting (Path B, Johanson 2004)

Instead of mounting a mesh patch onto Earth's sphere in world space, render a grid of vertices in **post-projection (NDC) space**. Each vertex corresponds to a camera ray; the fragment shader ray-traces against an **ocean sphere** (analytic, radius = `earthRadius + seaLevel`) and samples a **heightfield texture** at the hit point to compute the displaced ocean surface.

Industry precedent: Sea of Thieves, Frostbite ocean system, Unity's Crest Ocean. The pattern is well-understood outside Three.js and transfers cleanly via a `MeshBasicNodeMaterial` with a custom fragment node.

### Why this beats v1's mounted-patch approach

- **No sliding origin, no pole singularity, no TBN fallback at the patch level** (C6 disappears)
- **FFT becomes an actual drop-in kernel** — the consumer is a heightfield texture regardless of whether the source is Gerstner or IFFT (C1 disappears)
- **Normal computation lives in heightfield texture space** — no flat-domain derivatives warping after sphere bend (C3 disappears)
- **Horizon coverage is automatic** — the projected grid covers exactly the visible frustum at any zoom level
- **No `u_oceanPatchOpacity` fade** — the projected grid draws over `PlanetSurfaceTSL` where the ray hits ocean; land pixels don't render at all (C5 disappears)

### What's still open after the structural win

- **Shore mask quality** (C4) — the 2 k specular map is inadequate regardless of architecture. Resolved by adopting GEBCO 2023 bathymetry (see Data section).
- **Perf credibility** (C7) — Path B shifts work from vertex to fragment; net cost depends on patch-to-screen coverage ratio. Measured empirically in the Phase 0 spike.

## Data

### GEBCO 2023 bathymetry

**Source**: General Bathymetric Chart of the Oceans, BODC/NOAA, public domain. Native resolution 15 arc-seconds (~450 m/texel at the equator).

**Asset**: `public/textures/earth/bathymetry_8k.png` — 8192 × 4096, 16-bit grayscale, equirectangular projection. Downsampled from native GEBCO 2023 NetCDF via GDAL. Approximate size: 30 MB.

**Encoding**: grayscale value = clamp((depth_meters / 11000) * 65535, 0, 65535). A value of 0 means land or sea level; 65535 means Challenger Deep. The shader reconstructs depth via `depth = sample * 11000`.

**Storage**: git-LFS (see Pre-flight).

**Consumer**: sampled in the projected-grid fragment shader for (a) wave amplitude attenuation, (b) water color gradient, (c) physical dispersion in Phase 1.

### Physical dispersion (Phase 1 only)

The classical ocean-wave dispersion relation ω² = gk is only valid for deep water. At finite depth h, the correct form is ω² = gk·tanh(kh). This makes shallow waves slow down and bunch up near coastlines — exactly what real oceans do. Phase 1's spectrum time-evolution pass samples GEBCO at the patch center to pick a representative depth and applies the `tanh(kh)` correction per frequency bin.

## Shading (unchanged from v1/v2)

- Water color gradient: `#2ea5c8` shallow (5 m) → `#0a2845` deep (200 m+), depth-driven via GEBCO sample
- Fresnel sky reflection: Schlick approximation, F0 = 0.02
- Sun specular: reuse the existing `uSunPosWorld` uniform from `PlanetSurfaceTSL.ts`
- Sky color for Fresnel: hardcoded constant matching atmospheric glow in Phase 0; skybox sample in Phase 2
- Night-side handling: when `dot(N, sun) < 0`, the Fresnel term darkens toward `#050510` to prevent visible reflection against black space

## Requirements

### Lifecycle (L1–L6)

- **L1**. Ocean rendering activates when the camera altitude above Earth's surface is below an altitude threshold derived from the **Nyquist limit of the shore mask** — specifically, when one shore-mask texel subtends > 0.5 screen pixels at screen center. With GEBCO at ~5 km/texel (downsampled), this is approximately 120–180 km altitude. The threshold is computed each frame, not hardcoded.
- **L2**. Lifecycle uses hysteresis: instantiate at threshold × 1.0, dispose at threshold × 1.15. Prevents churn at the boundary.
- **L3**. The projected-grid mesh and material are instantiated once per mount and disposed on unmount. Instantiation cost includes uploading the GEBCO texture (one-time per browser session, cached thereafter).
- **L4**. The existing `PlanetSurfaceTSL.ts` shader is NOT modified. The projected-grid draws on top with `depthTest: true`, `depthWrite: true`. Where the ray misses the ocean sphere OR hits a land pixel (GEBCO depth == 0), the fragment discards and the underlying `PlanetSurfaceTSL` output remains visible.
- **L5**. AsteroidLOD already in `SolarSystem.ts:7` is the lifecycle pattern template — mount/dispose/hysteresis vocabulary MUST match. Implementation lives in `Ocean/OceanLODHandle.ts` with the same shape.
- **L6**. Mounting happens when Earth is the focused body (not when orbiting Mars or Jupiter) — gated on the existing focus-tracking state in `SolarSystem.ts`.

### Projected-grid geometry (G1–G5)

- **G1**. Grid geometry is a 256 × 256 `PlaneGeometry` in NDC space (x, y ∈ [-1, 1], z = 0). Total: 65 536 vertices, 130 050 triangles.
- **G2**. Vertex shader passes through NDC position and computes the per-vertex camera ray in world space: `ray_origin = cameraPosition`, `ray_dir = normalize(inverse(viewProjection) * vec4(ndc, 1, 1).xyz - cameraPosition)`.
- **G3**. Grid density is uniform in screen space; the `frustumCulled = false` flag is set because the grid always covers the full viewport.
- **G4**. On resize, the grid mesh is NOT regenerated — NDC-space geometry is resolution-independent by construction.
- **G5**. Grid resolution is tunable at construction via `oceanQuality: 'low' | 'medium' | 'high'` → 128² / 256² / 512² respectively.

### Ray-sphere intersection (I1–I5)

- **I1**. Fragment shader analytically intersects the camera ray with a sphere at Earth's world-space position and radius `earthRadius + seaLevel` (sea level offset is zero for Phase 0; configurable for Phase 2 tide tests).
- **I2**. Two intersection roots may exist (entry + exit). Phase 0 uses the entry root only; the camera-inside-sphere branch is deferred to Phase 2.
- **I3**. Ray miss discards the fragment. No ocean rendered outside the horizon.
- **I4**. The hit point is converted to spherical coordinates (lat, lon) in Earth-local space, accounting for Earth's current rotation matrix. These coordinates drive the heightfield UV, the GEBCO shore mask UV, and the day/night sun-dot product.
- **I5**. Earth-local transform uses `earthGroup.matrixWorld.invert()` — not raw world coordinates — so the reprojection formula works correctly when Earth translates in the solar-system scene graph (v1 review finding from Codex).

### Heightfield + normals (H1–H6)

- **H1**. The displacement heightfield is a `StorageTexture` (Phase 1) or a procedural fragment computation (Phase 0). Either way, the sampling contract at the consumer is identical: sample by `(lat, lon)` → returns `vec4(dx, dy, dz, foam)`.
- **H2**. Phase 0 synthesizes the heightfield via Gerstner sums **directly inside the ray-sphere fragment shader** — no texture pass, no compute shader. N = 6 Gerstner waves with preset parameters. Analytic normal from the Gerstner derivative, evaluated in the local tangent frame at the hit point.
- **H3**. Phase 1 replaces the in-fragment Gerstner sum with a texture lookup of the FFT-generated `displacementTexture`. The consumer (ray-sphere + shading) is unchanged. The feature flag `enableFFTOcean` gates the swap at runtime.
- **H4**. Normals in Phase 1 come from the FFT-generated `normalTexture`, NOT from finite differencing the displacement. The IFFT pass outputs both textures from the same spectrum.
- **H5**. The local tangent frame at the hit point uses the **stable east/north basis**: tangent = normalize(axis × normal), bitangent = normalize(normal × tangent), where axis = Earth's rotation axis. When `abs(dot(normal, axis)) > 0.99` (polar region), fall back to tangent = [1, 0, 0] to avoid the singularity.
- **H6**. Phase 1's `normalTexture` is computed per-texel from the Jacobian of the displacement field; `jxx * jyy - jxy * jyx` is stored alongside as the foam mask.

### Shore mask + GEBCO integration (S1–S6)

- **S1**. GEBCO texture is loaded once per browser session via Three.js `TextureLoader` and cached on the window. The URL is `/textures/earth/bathymetry_8k.png`. 16-bit PNG decoded as `THREE.HalfFloatType` texture.
- **S2**. Sampling uses `THREE.LinearFilter` for displacement attenuation, `THREE.NearestFilter` for the land/ocean binary mask to prevent bleed at coastlines.
- **S3**. Wave amplitude multiplier: `smoothstep(0.0, 50.0, depth_meters)`. Zero amplitude at the shoreline, full amplitude beyond 50 m depth, smooth transition in between.
- **S4**. Water color: `mix(color_shallow, color_deep, smoothstep(0.0, 200.0, depth_meters))`. `color_shallow = vec3(0.18, 0.65, 0.78)`, `color_deep = vec3(0.04, 0.16, 0.27)`.
- **S5**. Land pixel detection: `depth_meters == 0` → `discard`. The fragment is transparent; `PlanetSurfaceTSL.ts` output shows through.
- **S6**. Inland water handling (lakes, rivers) is not in Phase 0 scope. GEBCO represents only ocean bathymetry; inland water pixels have depth 0 and discard. The specular shimmer from `PlanetSurfaceTSL.ts` remains authoritative for inland water appearance. A future composite (GEBCO ∪ existing specular map at ocean boundaries) is a Phase 3 consideration.

### Rendering + compositing (R1–R5)

- **R1**. The projected-grid material renders with `renderOrder = 10` to draw after `PlanetSurfaceTSL` (default 0) and before atmosphere.
- **R2**. The projected grid uses `depthTest: true`, `depthWrite: true`. Fragment depth is computed from the displaced hit point, so overlapping geometry (e.g., an aircraft mesh near the surface) composes correctly.
- **R3**. The atmosphere Fresnel rim in `PlanetSurfaceTSL.ts` renders on Earth's sphere, radius = `earthRadius`. The ocean sphere is at `earthRadius + seaLevel`. The minuscule gap (< 1 m at 1 scene unit scale) does not produce visible z-fighting, but this MUST be validated in the Phase 0 feasibility spike.
- **R4**. WebGL2 fallback: Path B's ray-sphere intersection is WebGL-portable; only the Phase 1 FFT compute pass requires WebGPU. Phase 0 Gerstner runs on both. The `enableFFTOcean` flag only enables when WebGPU compute is available.
- **R5**. Postprocess compatibility: the ocean grid is opaque where it renders (no alpha blending); bloom, tone mapping, and AA passes compose normally.

### Chord-reactive hook (Phase 2, C1–C4)

- **C1**. The ocean material accepts an optional `audioModulationTexture: THREE.DataTexture` and a `reactive: boolean` flag. When `reactive === false`, the texture is ignored.
- **C2**. When reactive is on, Phase 1's FFT spectrum pass multiplies each frequency bin's amplitude by the corresponding audio-bin multiplier sampled from `audioModulationTexture`.
- **C3**. The audio bin → ocean bin mapping is log-linear: audio 80 Hz → ocean wavelength 100 m, audio 5000 Hz → 10 cm. Evaluated as a preset LUT, not recomputed per frame.
- **C4**. Audio pipeline hookup lives in `SolarSystem.ts` or a sibling controller, NOT inside the ocean material. The material receives a pre-computed texture. Keeps the ocean code audio-agnostic.

## Phase 0 — projected grid + Gerstner + GEBCO (target: 14–18 hours)

### Phase 0 deliverables

1. `ReactComponents/ga-react-components/src/components/PrimeRadiant/shaders/Ocean/OceanProjectedGrid.ts` — material factory
2. `ReactComponents/ga-react-components/src/components/PrimeRadiant/shaders/Ocean/OceanLODHandle.ts` — lifecycle handle mirroring `AsteroidLODHandle`
3. `ReactComponents/ga-react-components/src/components/PrimeRadiant/shaders/Ocean/shoreMask.ts` — GEBCO sampling + shore attenuation helpers
4. `public/textures/earth/bathymetry_8k.png` — committed via git-LFS (see Pre-flight)
5. Small hook in `SolarSystem.ts::updateSolarSystem` — compute camera altitude, trigger mount/dispose via the LOD handle
6. One new test file `PlanetNav.ocean.test.tsx` — mount/dispose assertions at threshold crossings + the AC5 frame-diff test

### Phase 0 feasibility spike — Phase 0 step 0 (MUST PASS BEFORE PROCEEDING)

Before committing to the architecture, validate the two unknowns flagged by the Claude subagent review. Time budget: **1 hour**. Exit criteria:

- **Spike check 1**: `MeshBasicNodeMaterial` with a custom fragment node can analytically ray-sphere intersect and write depth at the displaced position, rendering a static offset sphere that visibly appears AROUND the existing Earth sphere without z-fighting
- **Spike check 2**: the existing atmosphere Fresnel rim shader in `PlanetSurfaceTSL.ts` continues to render correctly with the ocean sphere in the scene graph, without visible Z-order artifacts at the terminator or limb

If both pass: commit to Path B, proceed with Phase 0. If either fails: abort Path B, fall back to Path A with v2 fixes (re-read `2026-04-11-planet-ocean-zoom-brainstorm-v2-review-response.md` section Path A).

### Phase 0 acceptance criteria

- **AC0-1**. Zooming below the computed threshold triggers mount; zooming above × 1.15 triggers dispose. Verified by assertion in `PlanetNav.ocean.test.tsx`.
- **AC0-2**. Waves are visibly animated, curved around Earth (ray-sphere intersection is working), and plausibly scaled for the current camera altitude.
- **AC0-3**. Continents (GEBCO depth == 0) show no ocean rendering; coastlines fade smoothly via `smoothstep(0, 50, depth)`.
- **AC0-4**. At the mount/dispose threshold, the crossfade into `PlanetSurfaceTSL` far-field shimmer is visually continuous. **Falsifiable test**: frame-diff luminance delta over the ocean mask, ± 5% of threshold, 3 sun angles (noon / terminator / midnight) × 3 crossing directions (approach / retreat / tangential). Max delta < 2%, mean delta < 0.3%. Nine assertions in the test file.
- **AC0-5**. No frame drops at 256² grid resolution on a reference RTX 3060 / M2 MacBook during a 10-second guided zoom from orbit to surface and back.
- **AC0-6**. Reverting by deleting the `Ocean/` folder and the `SolarSystem.ts` hook restores previous behavior exactly — zero modifications to `PlanetSurfaceTSL.ts`.
- **AC0-7**. Polar zoom test: camera orbits from equator to the north pole at ocean-visible altitude. No fragment corruption, no visible tangent flips.

## Phase 1 — Tessendorf FFT heightfield (target: 10–12 hours)

### Phase 1 deliverables

1. `ReactComponents/ga-react-components/src/components/PrimeRadiant/shaders/Ocean/spectrumInit.wgsl` — Phillips spectrum compute (runs once at mount, writes to `h0StorageTexture`)
2. `shaders/Ocean/spectrumEvolve.wgsl` — per-frame time evolution with GEBCO-sampled depth → `tanh(kh)` dispersion (writes `htStorageTexture`)
3. `shaders/Ocean/ifft.wgsl` — Stockham-ordering radix-2 IFFT, 9 stages × 2 dimensions, workgroup-memory implementation. Takes `htStorageTexture` → `displacementStorageTexture`. **This is a real IFFT kernel, not "two passes" — 18 dispatches at minimum, each pass has its own stride/twiddle stage. Estimated 300-500 lines of WGSL.**
4. `shaders/Ocean/jacobian.wgsl` — per-texel Jacobian determinant → foam mask, written into `normalStorageTexture.a`
5. `OceanProjectedGrid.ts` updated to conditionally swap in-fragment Gerstner for texture sample of `displacementStorageTexture` based on the `enableFFTOcean` flag
6. Runtime detection of WebGPU compute availability; flag auto-disables on WebGL2 fallback

### Phase 1 acceptance criteria

- **AC1-1**. With `enableFFTOcean: false`, Phase 0 Gerstner waves remain the default and all Phase 0 acceptance criteria still pass.
- **AC1-2**. With `enableFFTOcean: true`, wave patterns are visibly distinct from Gerstner — Phillips spectrum produces irregular, multi-scale wavelets versus Gerstner's obvious sinusoids.
- **AC1-3**. Foam appears in wave crests/troughs, driven by the Jacobian mask — no hand-authored foam textures.
- **AC1-4**. Shallow-water dispersion visibly manifests: waves near coastlines have shorter wavelength and slower propagation than waves in deep ocean.
- **AC1-5**. 60 FPS at 256² FFT resolution on an M2 MacBook Pro during the same 10-second guided zoom from AC0-5. The 512² target moves to Phase 2 as optional perf polish.

## Phase 2 — polish + chord-reactive (target: 10–14 hours)

### Phase 2 deliverables

1. Cascaded FFT: two spectrum grids at different wavelength bands (swell + chop), blended by weight in the heightfield lookup
2. Skybox sampling for Fresnel reflection — reuse existing star/milky way textures if their orientation is compatible
3. Chord-reactive pipeline: `AnalyserNode` in `SolarSystem.ts` → modulation `DataTexture` → `audioModulationTexture` uniform on the ocean material. Gated on `reactive: boolean` flag, default false.
4. Wind direction runtime control exposed via `setOceanWind(direction: Vector2, speed: number)` on the Prime Radiant controller

### Phase 2 acceptance criteria

- **AC2-1**. Cascaded patches blend seamlessly — no visible seam between the swell and chop bands at any viewing angle.
- **AC2-2**. Skybox-sampled Fresnel produces plausible reflections of the actual sky, not a constant color.
- **AC2-3**. With `reactive: true` and a guitar input, playing a power chord visibly modulates wave amplitude. Measured by frame-average heightfield variance during silence vs during sustained playing — variance ratio > 1.5×.
- **AC2-4**. Audio pipeline works with or without mic permission; denied permission is silent-fail to the non-reactive fallback.

## Pre-flight checklist (MUST COMPLETE BEFORE STARTING PHASE 0)

1. **git-LFS is not currently initialized in GA.** Adopting it for the GEBCO asset is a repo-level change. Required actions:
   - Create `.gitattributes` at repo root with `public/textures/earth/bathymetry_*.png filter=lfs diff=lfs merge=lfs -text`
   - Run `git lfs install` (user-level, one-time per developer)
   - Document the LFS adoption in the GA README and `CLAUDE.md` so downstream consumers know they need `git-lfs` installed for full clone
   - GitHub LFS bandwidth quota: 1 GB/month free. A 30 MB asset fetched by ~30 developers/month = ~900 MB, within free tier. Monitor usage.
2. **Validate the 1-hour Phase 0 spike.** Both spike checks must pass before committing to Path B. If either fails, abandon v3 and implement Path A from v2 instead.
3. **Confirm Three.js version.** v3 assumes Three.js r168+ with TSL `compute()` and `StorageTexture` support. Phase 1 FFT compute requires this. If GA is on an older release, upgrade or defer Phase 1.
4. **Confirm WebGPU feature detection.** The `enableFFTOcean` flag auto-disables on non-WebGPU-compute browsers. Verify that GA's existing Prime Radiant entry point already checks for WebGPU, or add a guard.

## Risks

**R-v3-1 — Phase 0 spike reveals a blocker.** Mitigation: time budget is 1 hour; if the spike fails, immediately fall back to Path A. The v2 doc has the full Path A plan ready. Total cost overrun: ~1 hour of wasted spike time.

**R-v3-2 — GEBCO 8K × 4K downsampling quality insufficient for close zoom.** At 5 km/texel, extremely narrow channels or small islands may disappear. Mitigation: Phase 2 adds a second GEBCO-16K tile-on-demand for the ultra-close-zoom band below ~20 km altitude. Not in Phase 0 scope.

**R-v3-3 — IFFT compute shader fails on older integrated GPUs.** Mitigation: the `enableFFTOcean` flag defaults off until AC1-5 passes. Gerstner Phase 0 remains the production fallback for at-risk hardware.

**R-v3-4 — GitHub LFS quota exceeded if the repo sees unexpected fork activity.** Mitigation: monitor quota via `gh lfs-migrate info` quarterly; if exceeded, move bathymetry to S3/CDN with a Phase 3 tile-on-demand implementation.

**R-v3-5 — Atmosphere Fresnel rim interaction with ocean sphere at the limb.** The atmosphere renders on a sphere of radius `earthRadius`; ocean is at `earthRadius + seaLevel`. At normal camera distances the ~1 m offset is invisible, but at grazing limb views it may produce subtle Z-order artifacts. **This is the unresolved risk from the Claude subagent review that the Phase 0 spike MUST exercise specifically.**

## Non-goals (explicit, unchanged from v1)

- No buoyancy or physics interactions with meshes (ships, planes)
- No breaking waves at the shoreline (would need local depth gradient data)
- No caustics or underwater rendering
- No runtime weather-driven wind changes in Phase 0/1 (static preset wind vector; runtime control is Phase 2)
- No ocean on other planets — the GEBCO asset is Earth-specific. Generalizing to gas giants with liquid-helium atmospheres or Europa's subsurface ocean is far-future work.

## Implementation sequencing

```
Pre-flight (2-3 hours, one-time)
  ├── git-LFS initialization
  ├── GEBCO 2023 download + GDAL downsample → bathymetry_8k.png
  ├── Three.js version audit
  └── WebGPU feature detection audit

Phase 0 — 14-18 hours
  ├── Step 0 — 1h feasibility spike (ABORT IF FAILS)
  ├── Step 1 — OceanProjectedGrid.ts (material + fragment shader + Gerstner)
  ├── Step 2 — OceanLODHandle.ts (mount/dispose/hysteresis, mirrors AsteroidLODHandle)
  ├── Step 3 — shoreMask.ts (GEBCO sampling + attenuation)
  ├── Step 4 — SolarSystem.ts hook
  ├── Step 5 — PlanetNav.ocean.test.tsx (AC0-1 through AC0-7)
  └── Step 6 — Manual visual check at 3 altitudes, 3 sun angles

Phase 1 — 10-12 hours (optional, gated on WebGPU compute availability)
  ├── Step 1 — spectrumInit.wgsl + spectrumEvolve.wgsl
  ├── Step 2 — ifft.wgsl (the real 9-stage Stockham kernel)
  ├── Step 3 — jacobian.wgsl
  ├── Step 4 — OceanProjectedGrid.ts FFT swap path
  ├── Step 5 — enableFFTOcean flag wiring + WebGPU detection
  └── Step 6 — AC1-1 through AC1-5 verification

Phase 2 — 10-14 hours (optional)
  ├── Step 1 — Cascaded FFT
  ├── Step 2 — Skybox Fresnel
  ├── Step 3 — Chord-reactive audio pipeline
  ├── Step 4 — Wind runtime control
  └── Step 5 — AC2-1 through AC2-4 verification
```

Total cost if all three phases complete: 36–47 hours. Minimum shippable (Phase 0 only): 14–18 hours.

## Audit trail

- v1: `2026-04-11-planet-ocean-zoom-brainstorm.md` — exploratory design
- Multi-AI critique round 1: 3 providers (Codex, Gemini, Claude subagent), 7 convergent issues surfaced
- v2: `2026-04-11-planet-ocean-zoom-brainstorm-v2-review-response.md` — explored Paths A/B, GEBCO, chord-reactive
- Multi-AI decision vote round 2: 3 providers, outcome tallied above
- v3: this document — production spec derived from the vote

Each stage is preserved in the `ga/docs/brainstorms/` directory for audit.
