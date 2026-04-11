---
date: 2026-04-11
topic: planet-ocean-zoom
---

# Planet Ocean Zoom — Realistic Water Surface for Close Earth Approach

## Problem Frame

When the Prime Radiant camera zooms close to Earth, the current ocean rendering breaks down visually. `PlanetSurfaceTSL.ts` treats the ocean as a flat specular highlight via the `2k_earth_specular.jpg` mask with Blinn-Phong shading — effectively "pixels with a shiny flag." This is fine from orbit but unmistakably a flat texture once the camera drops below ~1000 km altitude, where the eye expects displacement, parallax, animated waves, and foam.

The reference for what "good" looks like is [WebTide](https://barthpaleologue.github.io/WebTide/) — a WebGL demo implementing Tessendorf's 2001 "Simulating Ocean Water" FFT-based surface synthesis. It produces physically-plausible ocean displacement, normals, and whitecaps at interactive frame rates by running an IFFT pass on a GPU-computed wave spectrum.

The goal is to add an altitude-triggered ocean overlay that replaces the flat specular shading when the camera is close enough to Earth to notice. The existing far-field shader stays untouched — this is additive, not a rewrite.

## Scope

**In scope:**
- Ocean surface rendering that activates when camera altitude above Earth's surface crosses a threshold
- Displacement + normal-driven shading of the water surface
- Tangent-plane mounting of a flat ocean patch onto Earth's curved sphere
- Shore masking so waves don't appear over continents
- Crossfade between the existing far-field shader and the new near-field ocean patch
- Phased delivery: Gerstner waves first (visible progress fast), Tessendorf FFT as a drop-in upgrade

**Out of scope (explicit non-goals):**
- Replacing the existing `PlanetSurfaceTSL.ts` shader for far-field rendering
- Ocean on other planets (the mount is Earth-specific; generalization comes later)
- Buoyancy / physics interactions with ships or objects
- Breaking waves at the shoreline (needs depth data we don't have)
- Caustics, underwater rendering, subsurface scattering
- Realtime weather-driven wind speed changes (MVP uses a static wind vector)

## Requirements

**Mount & Lifecycle**

- R1. A new module `ReactComponents/ga-react-components/src/components/PrimeRadiant/shaders/Ocean/` shall contain the ocean rendering code, split across at least `OceanPatch.ts` (geometry + mount) and `OceanMaterial.ts` (shading)
- R2. `SolarSystem.ts::updateSolarSystem` shall detect when the camera is within a configurable altitude threshold (default: surface distance less than 25% of Earth's scene-scale radius — roughly 1000 km equivalent) and instantiate the ocean patch as a child of Earth's group
- R3. When the camera retreats beyond the threshold, the patch shall be disposed (geometry, material, textures) — no always-on cost
- R4. Mount/unmount shall hysteresis-debounce (instantiate at threshold × 1.0, dispose at threshold × 1.15) to prevent churn when the camera oscillates near the boundary
- R5. The existing `PlanetSurfaceTSL.ts` Earth material shall not be modified; its specular-map shimmer stays authoritative for far-field rendering
- R6. A crossfade factor `u_oceanPatchOpacity` in `[0,1]` shall be computed from camera altitude and drive a linear blend between the patch and the underlying specular shimmer in a narrow transition band (±10% around the threshold)

**Tangent-Frame & Sphere Binding**

- R7. The patch shall be a flat `PlaneGeometry` of configurable size (default: 4.0 scene units ≈ 2000 km equivalent, resolved into 256×256 vertices for Phase 0 and up to 512×512 for Phase 1)
- R8. The patch's rest position shall be the camera's ground projection: raycast from camera to Earth center, take the surface intersection, use that as the patch origin
- R9. The patch's orientation shall be the TBN frame at the origin: surface normal = outward radial, tangent = east (from Earth's rotation axis), bitangent = north
- R10. Before wave displacement is applied in the vertex shader, each patch vertex shall be re-projected onto Earth's sphere — `vertex_world_position = normalize(patch_local_to_world * vertex) * earthRadius`
- R11. The camera's ground projection shall be recomputed each frame and the patch origin shall slide smoothly; texture coordinates on the patch shall wrap so wave content is continuous across origin slides

**Wave Synthesis — Phase 0 (Gerstner)**

- R12. Phase 0 shall implement Gerstner waves: sum of N sinusoidal displacements (default N=6) with direction, wavelength, amplitude, and steepness parameters
- R13. Gerstner displacement shall be computed in the vertex shader via TSL (`OceanGerstnerTSL.ts`), matching the convention of existing TSL materials in GA
- R14. Wave parameters shall be presets: `{wavelength, amplitude, direction, steepness}[]` selected at material construction time (no runtime tuning in MVP)
- R15. Normal vectors shall be computed analytically from the Gerstner derivative formulas, not via finite differencing — cheaper and exactly correct

**Wave Synthesis — Phase 1 (Tessendorf FFT, upgrade path)**

- R16. Phase 1 shall implement Tessendorf-spectrum FFT-based wave synthesis as a drop-in replacement for `OceanGerstnerTSL.ts`, preserving the mount/LOD/shore-masking interface
- R17. Phase 1 shall use raw WGSL compute shaders for (a) initial Phillips spectrum generation, (b) per-frame time evolution, (c) horizontal and vertical IFFT passes, mirroring the existing WGSL conventions in `WebGPUFretboard/filters.ts`
- R18. Phase 1's output shall be a pair of render targets (displacement, normal) sampled by the same `OceanMaterial.ts` that Phase 0 wrote, so the material code swaps cleanly
- R19. Phase 1 shall compute a Jacobian determinant per texel and use `max(0, -J)` as a foam mask in the fragment shader
- R20. Phase 1 shall be gated behind a runtime feature flag (`enableFFTOcean`) so the Gerstner path remains the fallback when WebGPU compute is unavailable

**Shore Masking**

- R21. The ocean material shall sample Earth's `2k_earth_specular.jpg` specular map at the patch vertex's lat/lon (reconstructed from the spherical surface position)
- R22. Vertex wave displacement shall be attenuated by `smoothstep(0.15, 0.45, specular.r)` — land pixels (low specular) get zero displacement, deep ocean gets full displacement, coastlines fade smoothly
- R23. The shore mask shall also drive a color blend in the fragment shader between Earth's daymap albedo (at the shoreline) and the ocean shading color (deep water), so the transition is visually seamless

**Shading**

- R24. The ocean material shall use a physically-motivated water color: shallow `#1e5a8a`, deep `#0a2845`, driven by altitude-from-surface and a simple depth approximation (inverse of shore mask)
- R25. Sky reflection shall use a Fresnel term (Schlick approximation, F0 = 0.02) modulated by the surface normal
- R26. For Phase 0, sky color shall be a constant per-frame value matching the atmospheric glow of the existing `PlanetSurfaceTSL.ts`. Phase 2 may upgrade to a sampled skybox
- R27. Sun specular shall reuse the sun position uniform (`uSunPosWorld`) already piped through the existing planet material

**Performance**

- R28. Phase 0 target: 60 FPS on discrete GPUs at 256×256 patch resolution; Phase 1 target: 60 FPS on integrated GPUs at 512×512
- R29. Patch geometry shall be created once and cached; only the patch's transform updates per frame when the camera's ground projection moves
- R30. The compute shaders in Phase 1 shall run at most once per frame; if the render loop runs faster than 60 FPS the ocean shall frame-rate-limit its simulation step

## Acceptance Criteria

**Phase 0 (Gerstner) — ships when:**

- AC1. Zooming the camera toward Earth below the altitude threshold causes a visible ocean patch to appear; zooming back out causes it to disappear
- AC2. The ocean patch follows the camera's ground projection — panning the camera slides the patch smoothly along the surface
- AC3. Waves are clearly visible and animated (not static geometry); their scale looks plausible for the camera altitude
- AC4. Continents show no wave displacement; coastlines fade between land and water without visible seams
- AC5. The transition between far-field shader and near-field patch at the altitude threshold is visually continuous — no pop, no tear
- AC6. No frame drops on a reference RTX 3060 / M2 Mac at 256×256 patch resolution
- AC7. The existing `PlanetSurfaceTSL.ts` shader is unmodified; reverting just the new `Ocean/` folder plus the small hook in `SolarSystem.ts` restores previous behavior exactly

**Phase 1 (FFT upgrade) — ships when:**

- AC8. The FFT path is behind a runtime flag and defaults off until validated
- AC9. With the flag on, wave patterns are visibly distinct from Gerstner (Phillips spectrum produces irregular, multi-scale wavelets versus Gerstner's obvious sinusoids)
- AC10. Foam appears in wave crests and troughs driven by the Jacobian-determinant mask, not hand-authored
- AC11. Performance matches R28

**Phase 2 (Polish) — optional:**

- AC12. Cascaded patches at two scales blend seamlessly (long-wavelength swell + short-wavelength chop)
- AC13. Sky reflection samples a skybox instead of a constant
- AC14. Wind direction can be animated at runtime to drive directional bias

## Key Design Decisions

**D1. Gerstner-first instead of FFT-first.** FFT ocean is visually impressive but carries a lot of infrastructure risk: compute-shader setup, render-target management, WebGPU fallback, radix-2 IFFT debugging. None of that risk overlaps with the "can I mount a patch on a sphere and crossfade with the existing shader" risk. Gerstner waves — pure TSL, vertex-shader-only, analytically differentiable — let us ship Phase 0 in half a day and validate all the mounting/LOD/shore-masking infrastructure. FFT becomes a drop-in kernel replacement once the scaffolding is proven.

**D2. Mixed TSL + raw WGSL, not TSL-only.** TSL's compute shader support is immature; Phase 1's IFFT passes need raw WGSL. GA already has raw WGSL in `WebGPUFretboard/filters.ts`, so this is not a new convention. Materials stay in TSL (consistent with `PlanetSurfaceTSL.ts`, `AmbientDustTSL.ts`), compute passes go raw WGSL.

**D3. Flat patch rebased onto the sphere, not a spherical mesh.** A proper spherical ocean mesh would be millions of triangles at the density needed for visible waves near the camera. A flat tangent-plane patch with sphere-bending in the vertex shader gives the right appearance at 256²–512² triangles. The patch is a flat 2D simulation domain; only the final position is projected onto Earth's curvature.

**D4. Shore masking via existing specular map, not a new SDF.** The `2k_earth_specular.jpg` asset already encodes "where water is" with values ~0 on land and ~1 on ocean. A proper signed distance field to coastlines would give cleaner transitions but is a much larger asset-pipeline task. The specular map is good enough for MVP and the transition quality is bounded by its 2k resolution — not a limitation the ocean code itself introduces.

**D5. Hysteresis debounce on LOD mount.** Without it, a camera that hovers exactly at the threshold would instantiate and dispose the patch every frame. A ×1.0 / ×1.15 dead band is cheap and prevents thrashing.

**D6. No changes to `PlanetSurfaceTSL.ts`.** The far-field shader is well-tested and serves a different purpose. Crossfade happens at the scene-graph level, not inside either shader. Reverting the feature means deleting one folder and one function call.

**D7. Ix contributes nothing in MVP.** The FFT spectrum math in Phase 1 is ~50 lines of WGSL; piping it through `ix-signal` via MCP would add orders of magnitude more latency than running it locally in a compute shader. Cross-repo integration can revisit after Phase 2 if we want deterministic offline spectrum pre-generation for regression testing.

## Implementation Plan

**Phase 0 — Gerstner waves (~4-6 hours)**

1. Create `ReactComponents/ga-react-components/src/components/PrimeRadiant/shaders/Ocean/OceanPatch.ts`:
   - Export `createOceanPatch(earthGroup, material): THREE.Mesh`
   - Builds a 256×256 `PlaneGeometry`, attaches `material`
   - Exports `updateOceanPatchOrigin(patch, camera, earthPosition, earthRadius)` — recomputes patch transform from camera ground projection
2. Create `ReactComponents/ga-react-components/src/components/PrimeRadiant/shaders/Ocean/OceanGerstnerTSL.ts`:
   - `createOceanGerstnerMaterial(specularMap, sunPosUniform, earthRadiusUniform): MeshBasicNodeMaterial`
   - TSL vertex shader: sum 6 Gerstner waves, compute analytic normal, sphere-bend, shore-mask
   - TSL fragment shader: water color + Fresnel sky reflection + sun specular
   - Preset wave table in a separate const — direction/wavelength/amplitude/steepness
3. Hook into `SolarSystem.ts::updateSolarSystem`:
   - Detect Earth's target group, compute `cameraAltitude = distanceToEarthSurface`
   - If `altitude < threshold` and no patch exists, create it via `createOceanPatch` and add to Earth's group
   - If `altitude > threshold * 1.15` and a patch exists, dispose and clear
   - Each frame while mounted: call `updateOceanPatchOrigin` and update the crossfade uniform on `PlanetSurfaceTSL` (one new uniform: `u_oceanPatchOpacity`)
4. Add the `u_oceanPatchOpacity` uniform to `PlanetSurfaceTSL.ts` and multiply the specular term by `(1.0 - u_oceanPatchOpacity)` — this is the only change to the existing shader
5. Tests:
   - Manual: zoom to Earth in the Prime Radiant dashboard, verify waves appear, continents are flat, transition is smooth
   - Automated: extend `PlanetNav.test.tsx` with a mount/unmount assertion at threshold crossings

**Phase 1 — Tessendorf FFT (~16 hours after Phase 0 ships)**

1. `Ocean/spectrum.wgsl` — Phillips spectrum: compute shader, runs once at init, writes to `h0Texture` RGBA16F
2. `Ocean/timeEvolve.wgsl` — per-frame: reads `h0Texture`, writes frequency-domain `ht` at current time
3. `Ocean/ifft.wgsl` — Stockham-ordering radix-2 IFFT, two passes (horizontal then vertical), writes to `displacementTexture`
4. `Ocean/jacobian.wgsl` — per-texel Jacobian determinant for foam mask
5. `OceanFFTMaterial.ts` — replaces `OceanGerstnerTSL.ts`'s role; samples `displacementTexture` in the vertex shader, `foamTexture` in the fragment shader
6. Feature flag `enableFFTOcean` in SolarSystem options
7. Tests: visual regression screenshots at two altitudes with the flag on vs off

**Phase 2 — Polish (~8 hours, optional)**

1. Cascaded patches: two OceanPatches at different scales, blend weights based on camera altitude
2. Skybox sampling for reflection (reuse existing star/milky way textures if suitable)
3. Runtime wind control: expose `setOceanWind(direction, speed)` on the Prime Radiant controller

## Open Questions

- Q1. What is the exact altitude threshold in scene units? The proposed 25% of `EARTH_RADIUS` (0.03 scene units) is a starting point; it may need tuning after Phase 0 lands so the patch activates at a visually correct distance
- Q2. Is there an existing sky color uniform on the solar system side that the ocean fragment shader should read for Fresnel reflection, or should Phase 0 use a hardcoded color?
- Q3. Are there any tests in `PlanetNav.test.tsx` that would conflict with adding a scene-graph child to Earth? A quick scan did not find any but a full regression run is required before Phase 0 merges
- Q4. Should the patch also account for Earth's rotation so waves don't "drag" relative to the surface as Earth spins? Phase 0 answer: yes, patch is parented to Earth's rotating group, so rotation is free

## Risks

**R1 — WebGPU compute shader availability in production.** Phase 1 requires WebGPU compute support. Safari is still rolling this out; older Chrome on Windows may fall back to ANGLE. Mitigation: Gerstner fallback is already the Phase 0 default — the feature flag doesn't break anything if WebGPU compute isn't available.

**R2 — Patch seams at the edges.** A flat patch has hard edges where it ends. If the transition band is too narrow, those edges will be visible. Mitigation: the crossfade uniform fades the patch opacity to zero at its geometric boundary AND at altitude thresholds, so edges always blend into the underlying shimmer.

**R3 — Performance regression on older hardware.** Integrated GPUs may struggle with 512×512 FFT at 60 FPS. Mitigation: runtime quality setting — `oceanQuality: 'low' | 'medium' | 'high'` selects 128²/256²/512² patch resolution.

**R4 — Shore mask resolution.** The 2k specular map has ~10 km per texel at Earth's equator, which is coarser than the patch at close zoom. Near the shoreline, individual texels will be visible. Mitigation: accept the limitation for MVP; document it; consider an upscaled shore mask or SDF in a follow-up.

## References

- [WebTide](https://barthpaleologue.github.io/WebTide/) — the visual reference, MIT-licensed, pure WebGL
- Tessendorf, J. (2001). "Simulating Ocean Water" — the seminal paper on FFT-based wave synthesis
- Gerstner waves: any graphics textbook; the classic [GPU Gems 1, Chapter 1](https://developer.nvidia.com/gpugems/gpugems/part-i-natural-effects/chapter-1-effective-water-simulation-physical-models) is the canonical intro
- Existing GA conventions:
  - `ReactComponents/ga-react-components/src/components/PrimeRadiant/shaders/PlanetSurfaceTSL.ts` — the TSL material the ocean must crossfade with
  - `ReactComponents/ga-react-components/src/components/WebGPUFretboard/filters.ts` — raw-WGSL precedent for Phase 1
  - `ReactComponents/ga-react-components/src/components/PrimeRadiant/SolarSystem.ts` — the solar system scene graph and update loop
