---
date: 2026-04-11
topic: planet-ocean-zoom-v2
supersedes-sections: v1 D1, R5, R7-R11, R15, R16, R18, R21-R24, R28, AC5, AC7
---

# Planet Ocean Zoom v2 — Response to Multi-AI Review

## Purpose

v1 of this brainstorm (`2026-04-11-planet-ocean-zoom-brainstorm.md`) received a multi-AI Team-mode review from Codex, Gemini, and a Claude subagent. Three-way convergence on seven issues and several non-obvious alternatives raised by only one reviewer each. v2 explores the three follow-up paths the review surfaced:

- **Path A** — patch the v1 approach, keep the mounted-patch architecture
- **Path B** — structural rewrite, switch to projected-grid raycasting (Sea of Thieves / Frostbite pattern)
- **Data upgrade** — replace the 2k specular-map shore mask with GEBCO bathymetry + physical dispersion
- **Ecosystem hook** — chord-reactive ocean spectrum driven by live guitar audio

v1 is not discarded; its review findings are the input. Whichever path is chosen becomes v3 with the concrete requirements.

## Convergent review findings (from v1 → v2)

The review identified seven issues where two or three reviewers independently converged. These drive the rewrite:

- **C1** — "Phase 1 is a drop-in kernel replacement" (v1 D1, R16, R18) is false. Gerstner validates mount/shore/crossfade but **not** the FFT pipeline risk surface. The "drop-in" framing is sunk-cost. (Codex #1, Claude P1, Gemini implied)
- **C2** — v1's 2000 km patch at 1000 km altitude is under-scoped. Horizon math: 1000 km altitude → ~3700 km horizon radius → 2000 km patch covers only the center. **Patch edge is visible inside the viewport.** (Codex #2, Claude sagitta)
- **C3** — v1 R10 (sphere reprojection) + R15 (analytic Gerstner normals) are mutually exclusive. Flat-domain derivatives are wrong after non-linear bend. (Codex #3, Claude P2)
- **C4** — v1 R21-R23's 2k specular mask is below the realism floor at the zoom range the feature targets. Actual pixel size is **19.6 km/texel**, not the ~10 km v1 quoted (Codex correction). `smoothstep(0.15, 0.45)` manufactures a coastline band tens of km wide. Bays, islands, estuaries smear. (Codex #4, Claude, Gemini)
- **C5** — v1 R5/AC7 ("no changes to `PlanetSurfaceTSL.ts`") self-contradicts Phase 0 step 4 which adds `u_oceanPatchOpacity` and multiplies specular by it. (Codex #6, Claude)
- **C6** — v1 R9's "east-from-axis" TBN frame is singular at the poles. (Codex #7, Claude)
- **C7** — v1 R28's perf target for "integrated GPUs as a class" is not credible for a full 512² FFT pipeline stacked on top of Earth/atmosphere. (Codex #5)

Plus a factual correction: **TSL now ships `compute()` and `StorageTexture`** as of recent Three.js releases. v1's "TSL is too immature, therefore raw WGSL" framing is obsolete. Raw WGSL may still win on FFT control/perf but the justification must change.

## Path A — patch the mounted-patch approach

### What changes vs v1

- **Shrink the patch** from 2000 km to **500 km** (R7 rewrite). Sagitta drops from 78.7 km to ~4.9 km; TBN edge error drops from 9° to ~2.2°. Fits inside the horizon at 1000 km altitude with 3× margin.
- **Cascades move from Phase 2 to Phase 1** (or even Phase 0). A single patch cannot cover the horizon at the proposed altitudes. Minimum two cascades: a small near-field patch (500 km, 256² sim) for the camera's ground projection, and a large far-field patch (3000 km, 128² sim, lower wave amplitude) for horizon coverage.
- **Earth-relative reprojection** (R10 rewrite). `vertex_world = earthCenter + normalize(vertex_world - earthCenter) * earthRadius`. Works under scene-graph translation.
- **Spherical-corrected normals** (R15 rewrite). After sphere bend, re-rotate the analytic Gerstner normal by the TBN frame at the re-projected position. Alternative: switch to finite-difference normals from the sphere-bent positions (simpler, marginally slower, correct by construction).
- **Pole fallback frame** (R9 rewrite). When surface normal's dot product with Earth's axis exceeds 0.99, use "tangent = +X, bitangent = +Z" instead of the axis-cross. Eliminates the singularity.
- **Threshold derived from Nyquist, not altitude** (R2 rewrite). Replace "25% of Earth radius" with "angular size of one specular-map texel exceeds 0.5 screen pixels." At 19.6 km/texel, this triggers around **50-80 km altitude**, not 1000 km. The entire activation envelope is 20× smaller than v1 planned.
- **Rename "opacity fade" to "patch takeover"** (R6 rewrite). The v1 `u_oceanPatchOpacity` approach is wrong — fading specular doesn't hide the daymap's blue pixels under the patch. Replace with a binary hand-off: when the patch is mounted, its geometry occludes the daymap entirely (depth-test enabled, vertex displacement only near shorelines attenuated by shore mask). Eliminates the three-line `PlanetSurfaceTSL.ts` edit AND the daymap-bleed issue.
- **AC5 falsifiable test** — frame-diff luminance delta < 2% max, < 0.3% mean, at threshold ± 5%, × 3 sun angles (noon/terminator/midnight) × 3 crossing directions (approach/retreat/tangential). Nine automated visual-regression assertions.
- **Name the pattern**: the sliding tangent patch becomes the **"camera-anchored tangent patch"** (CATP). Borrows mount/dispose/hysteresis vocabulary from `AsteroidLODHandle` already in `SolarSystem.ts:7`.

### What Path A still gets wrong

- **Patch seams at cascade boundaries.** Two cascaded patches mean two edges. Hand-tuned blend weights will still produce visible seams under grazing sun angles.
- **FFT compute scaffolding in Phase 1 is still a separate risk surface.** Gerstner validates mount + shore + crossfade + LOD — NOT ping-pong RT management, compute-pass scheduling, IFFT debugging, or RGBA16F support detection.
- **Shore mask is still 2k.** Path A doesn't fix it unless we also adopt the data upgrade below.
- **Horizon coverage at zoom-out** — even with two cascades, rapid camera pull-back during the unmount transition will show artifacts.

### Cost estimate (Path A)

- Phase 0 (Gerstner + single near-patch + correct mount): 6-8 hours (was 4-6)
- Phase 0.5 (add far-patch cascade): 3-4 hours
- Phase 1 (FFT, with honest risk): 16-20 hours
- Phase 2 (polish + wind + sky): 8 hours
- **Total**: ~33-40 hours

## Path B — structural rewrite, projected-grid raycasting

### The idea

Claes Johanson's 2004 thesis "Real-time Water Rendering" introduces the projected-grid approach: instead of a mesh tessellation in world space, render a grid of vertices in **post-projection space** (the camera's view frustum). Each vertex is a ray from the camera; the ray is intersected with a displaced water plane (or sphere). The mesh automatically covers exactly the visible horizon — no patch seams, no LOD, no sliding origin.

Adapted to planet scale:

1. **Ocean-sphere primitive.** A mathematical sphere at Earth's position with radius `earthRadius + seaLevel`. Not a mesh; just a parameter.
2. **Projected grid.** A 128×128 or 256×256 mesh in **post-projection (NDC) space**, transformed into rays in the fragment shader.
3. **Ray-sphere intersection.** Each fragment ray is intersected analytically with the ocean sphere. Hit point is on the undisplaced surface.
4. **Heightfield displacement.** The undisplaced hit point's lat/lon is used as UV into the FFT-generated displacement texture. The displaced hit point is the final ocean surface position.
5. **Fragment shading.** Normal from the heightfield derivative; Fresnel, sun specular, shore mask, sky reflection — same math as Path A, but in the fragment shader, per-pixel, not per-vertex.

### What Path B fixes vs Path A

- **No patches.** R7-R11 go away. No 2000 km, no 500 km, no cascades, no sliding origin, no pole singularity, no TBN frame at all.
- **No mount/unmount lifecycle.** The projected grid always exists when the camera is within the ocean altitude threshold. There's no geometry to dispose. The altitude threshold gates **rendering**, not **instantiation**.
- **Horizon coverage is automatic.** The projected grid covers exactly the visible frustum. Zoom out → grid stretches; zoom in → grid compresses. Always pixel-accurate density.
- **C1 (drop-in FFT) becomes true.** The Path B pipeline takes a heightfield texture as input. Gerstner-in-fragment-shader → FFT-generated-displacement-texture is now an **actual kernel replacement**, because the consumer (ray-sphere + heightfield lookup) is identical for both. Codex's C1 critique evaporates because the v1 architecture was wrong, not because FFT is uniquely hard.
- **C3 (normal warping after sphere bend) goes away.** There's no sphere bend. The sphere is analytic; the heightfield is a 2D texture; normals are computed in texture space, then expressed in the local TBN at the hit point's lat/lon using a stable east/north basis (with pole fallback).
- **C6 (pole singularity) becomes local-only.** The projected-grid pipeline still needs a TBN for normal expression, but only at the per-pixel hit point, not at a patch's origin. Singular fragments can be allowed (one pixel wide) and they won't break the frame.
- **C4 (shore mask)** — unchanged, still needs the data upgrade below.
- **C5 (R5/AC7 contradiction)** — goes away because there's no `u_oceanPatchOpacity`. The projected grid simply has a rendering order that draws over the planet surface where the ray hits ocean.

### What Path B introduces

- **Z-order vs the planet sphere.** The ocean sphere is slightly larger than Earth's solid-geometry sphere. When the ray hits ocean before hitting land, the ocean wins. When the ray hits land first, land wins. This is naturally handled by ray-sphere precedence + shore mask, but requires careful depth-buffer management: the projected grid must write depth at the displaced surface position, not the undisplaced one, so the existing atmosphere shader composes correctly.
- **Ray miss = fragment discard.** Fragments where the ray doesn't hit the ocean sphere must discard. Wastes some fragments in a screen quad (the corners), but at 256×256 grid resolution the overhead is negligible.
- **Near-camera degeneracy.** When the camera is below sea level or inside the ocean sphere, the ray direction flips and intersections happen behind the camera. Handled by a "camera inside water" branch that renders a full-screen quad with underwater shading instead of the projected grid.
- **Learning curve.** The team needs to understand projected grids. Johanson's thesis (Chapter 4) is the canonical reference, ~40 pages. Implementation is ~300 lines of WGSL for the grid pass, ~150 for ray-sphere intersection.
- **Depth buffer interaction with existing planet.** Need to confirm the current `PlanetSurfaceTSL.ts` writes depth at the sphere's surface (it should). Projected-grid depth writes at the *displaced* position must play nicely with the atmosphere's Fresnel rim (R-v1 of `PlanetSurfaceTSL.ts`).

### Cost estimate (Path B)

- Phase 0 (projected grid + ray-sphere + Gerstner-in-fragment-shader + shore mask via existing specular): 10-14 hours (initial learning curve, then faster)
- Phase 1 (FFT heightfield as drop-in): 10-12 hours (compute pass for spectrum + IFFT + Jacobian, no interface rewrite)
- Phase 2 (polish): 6-8 hours
- **Total**: ~26-34 hours

Path B is actually *cheaper* than Path A despite the structural rewrite because Phase 1 becomes half as expensive (no interface redesign, no cascade tuning, no mount plumbing).

### Why projected-grid is the right call if viable

- **Uses the correct abstraction** — the visible ocean is a function of the camera, so the geometry should be a function of the camera too. A mounted patch in world space is fighting the problem.
- **Converges with the real industry pattern** — Sea of Thieves (via `SimulOcean`), Frostbite's ocean system, and Unity's Crest Ocean all use projected grids as their base. Three.js doesn't ship one, but the pattern is well-understood.
- **Unifies Phase 0 and Phase 1** — the heightfield texture contract is the shared interface. Phase 0 writes Gerstner sums into it from a compute/fragment pass; Phase 1 writes FFT IFFT output into it. Same consumer.
- **Makes Gemini's alternatives viable** — bathymetry-driven dispersion and chord-reactive wave amplitudes both feed the same heightfield pipeline with no architectural change.

### What could kill Path B

- **Confirmation that TSL/Three.js r168+ supports custom ray-sphere intersection in the fragment shader** without fighting the Three.js camera/projection pipeline. 98% confident this works via `MeshBasicNodeMaterial` with a custom fragment node, but not validated in GA's codebase yet. Needs a 1-hour spike before committing.
- **Depth-buffer compatibility** with the existing atmosphere/Fresnel-rim shader. The atmosphere is rendered as a Fresnel glow on the Earth sphere; adding a larger ocean sphere might push atmosphere rendering to a deferred pass.

## Data upgrade — GEBCO bathymetry + physical dispersion

### GEBCO as the shore mask replacement

GEBCO (General Bathymetric Chart of the Oceans) is a global seafloor depth grid, publicly released by BODC/NOAA. Key facts:

- **Resolution**: GEBCO 2023 grid is **15 arc-seconds** globally — that's about 450 m per texel at the equator, vs. the specular map's 19.6 km. **44× resolution improvement** for free.
- **Format**: NetCDF, GeoTIFF, or JPEG. For the web viewer, a downsampled 8K×4K equirectangular 16-bit grayscale PNG is ~30 MB and gives ~5 km/texel — still 4× better than the specular map.
- **License**: Public domain / open data.
- **Content**: signed depth in meters. Positive values = ocean depth, negative/zero = land.

This replaces the binary specular mask with a **graded depth field**. Immediate wins:

1. **Natural shore mask.** `waveAmplitude *= smoothstep(0.0, 50.0, depth)` fades waves from zero at the coastline to full at 50 m depth. No hand-tuned thresholds.
2. **Color gradient.** Water color shifts from `#2ea5c8` (shallow, turquoise) at 5 m to `#0a2845` (deep navy) at 200 m. No more constant-color hack.
3. **Physical dispersion** (Gemini's insight). The dispersion relation ω² = gk·tanh(kh) makes shallow waves slow down and bunch up. Real-time: for each FFT frequency bin, modulate the time-evolution phase by `sqrt(tanh(k * h_avg))` where `h_avg` is the local depth sampled at the patch origin (or per-fragment in Path B). Produces physically-correct wave bunching near coastlines without faking it.

### Integration cost

- **Asset pipeline**: download GEBCO 2023, downsample in QGIS or GDAL, export as 8K×4K PNG, commit as `public/textures/earth/bathymetry_8k.png` (~30 MB — check GA's git-LFS policy first).
- **Shader sampling**: one extra texture binding. Sampling is a 2-line change per shader point.
- **Physical dispersion**: ~20 lines of WGSL in the spectrum time-evolution pass.
- **Development cost**: 3-4 hours, fits into Phase 1 naturally.

### Why this is low-risk and high-value

- **Public data, stable source, no runtime dependency.** Download once, commit to the repo, ship.
- **Zero change to the rendering architecture.** Works in Path A (sampled per-vertex) or Path B (sampled per-fragment) identically.
- **Visually transformative.** The difference between "waves ∈ {0, 1}" and "waves ∈ [0, 1] graded by real bathymetry" is enormous at close zoom.
- **The chord-reactive idea needs depth anyway** — reactive wave amplitudes look broken without spatial shape.

### Caveats

- **30 MB is a big asset** for a web viewer. Tile-serving with lazy load per region (WMTS style, similar to the MODIS snow cover tiles already loaded in `SolarSystem.ts:1343`) is the right move for production. MVP can ship the monolithic 8K PNG.
- **GEBCO doesn't include lakes, rivers, or glaciers.** The inland-water features of the specular map are lost. Solution: composite GEBCO with the specular map — GEBCO for ocean depths, specular map for inland water.
- **Antarctic coastline** is ambiguous in GEBCO (ice shelves) and will need a visual check.

## Ecosystem hook — chord-reactive ocean spectrum

### The idea

GA is a guitar app. The Prime Radiant is a 3D viewer for a musical domain. A planet Earth ocean that responds to what the user is playing is brand-native — it's the kind of insight that only emerges when the visualization stack and the domain are in the same repo. Gemini surfaced this; it's worth serious consideration.

**The mechanism**: every audio frame (typically 1024 samples at 44.1 kHz = ~23 ms), compute an FFT of the guitar signal. This produces a ~512-bin spectrum. Map audio frequency bins to ocean wave frequency bins:

- **Audio ~80 Hz (low E)** → **ocean wavelength ~100 m** (long swell, deep bass)
- **Audio ~330 Hz (high E open)** → **ocean wavelength ~10 m** (chop)
- **Audio ~1000 Hz (12th fret high E)** → **ocean wavelength ~1 m** (ripples)
- **Audio ~5000 Hz (fret noise, pick attack)** → **ocean wavelength ~10 cm** (foam texture)

The log-log mapping from audio frequency to ocean wavelength is a constant curve; each audio-bin amplitude becomes a multiplier on the Phillips spectrum amplitude at the corresponding ocean-bin.

**Visual result**: playing a big open E chord generates rolling swell; shredding on the high frets generates spectral chop; palm-muted bass riffs pulse the surface rhythmically; a clean arpeggio generates delicate ripples that propagate outward.

### Technical feasibility in GA

GA already has the audio input wire in `DevicesPanel.tsx:41` via `navigator.mediaDevices.getUserMedia`. A browser-side FFT via `AnalyserNode.getFloatFrequencyData` is free (Web Audio API built-in, no custom math). The missing piece is piping `AnalyserNode` output into the ocean's spectrum uniform.

```typescript
// In the Ocean render loop (Path A or B):
const audioSpectrum = new Float32Array(512);
analyser.getFloatFrequencyData(audioSpectrum);  // dB
// Remap dB to [0,1] multipliers
const multipliers = audioSpectrum.map(db =>
  Math.max(0, (db + 100) / 60)  // -100 dB = 0, -40 dB = 1
);
// Upload to oceanMaterial's audioModulation uniform
oceanMaterial.userData.audioModulationUniform.value = new THREE.DataTexture(
  multipliers, 512, 1, THREE.RedFormat, THREE.FloatType,
);
```

### Scope implications

- **MVP**: a `reactive: boolean` flag on the ocean material. Default off. When on, read from a global `AnalyserNode` set up by the existing `DevicesPanel` wiring.
- **Demo impact**: huge. This is the kind of thing that ships to the front of the GA README and the social media clip.
- **Accessibility**: users without mic access (or who deny permission) get the non-reactive fallback.
- **Cost**: ~4 hours on top of Path A or Path B — the audio pipeline exists, only the mapping and uniform upload are new.

### Why this is worth including

- **Unique to GA.** No other ocean shader anywhere has this because no other ocean shader lives in a guitar app.
- **Technical feasibility is trivial.** The audio pipeline, the FFT, the spectrum modulation uniform — each piece is standard.
- **Demo-worthy.** A GitHub README clip of power chords making the planet's oceans swell is marketing material.
- **Non-blocking.** The feature flag means the non-reactive default ships first; reactive is a Phase 2 polish item.

### Caveats

- **Audio latency vs visual latency.** Web Audio → AnalyserNode → JS read → GPU upload → shader → frame output is ~2-3 frames (~50 ms). Noticeable for staccato riffs. Mitigation: pre-emptively push modulation forward by one frame based on moving average.
- **Silence vs ambient noise.** Mic picks up room noise even when the user isn't playing. Gate by RMS threshold.
- **Stereo guitars.** GA might process stereo signal (L/R pickups); the ocean wants one mono spectrum. Fold to mono pre-FFT.

## Recommendation

**Primary**: **Path B + Data upgrade (GEBCO)** for Phase 0 and Phase 1. **Chord-reactive as opt-in Phase 2 polish.**

Rationale:

1. **Path B is cheaper in total cost** (~26-34h vs ~33-40h for Path A) because Phase 1 is half as expensive when FFT becomes an actual drop-in kernel.
2. **Path B doesn't fight the reviewers.** Six of the seven convergent critiques (C1-C6) either disappear or become trivial in the projected-grid architecture. C7 (perf) is still an open question but at least a principled one.
3. **GEBCO costs 3-4 hours and replaces the single biggest shore-mask problem** that all three reviewers independently flagged. Zero architectural risk.
4. **Chord-reactive fits both paths** but is most compelling as a brand-defining polish item. Ship it in Phase 2 once the base is stable.

**Secondary**: if Path B's 1-hour feasibility spike (custom ray-sphere intersection in a Three.js fragment shader + depth buffer compatibility with the atmosphere shader) reveals a blocker, fall back to Path A with the v2 fixes. Path A is worse but still an improvement over v1.

**Avoid**: Path A + v1 shore mask. This combination ships all the issues the reviewers flagged without fixing any of them.

## Phased plan (under the primary recommendation)

**Phase 0 — projected grid + Gerstner + GEBCO shore** (~14-18 hours)

1. 1-hour spike: validate that a `MeshBasicNodeMaterial` with custom fragment-shader ray-sphere intersection renders correctly on top of the existing Earth without fighting depth writes or the atmosphere shader
2. Download, downsample, and commit `public/textures/earth/bathymetry_8k.png` (GEBCO 2023, 8K × 4K, 16-bit grayscale)
3. `ReactComponents/ga-react-components/src/components/PrimeRadiant/shaders/Ocean/OceanProjectedGrid.ts`:
   - Projected-grid geometry: 256×256 NDC quad
   - Vertex shader: pass through NDC; compute per-vertex camera ray
   - Fragment shader: ray-sphere intersection with ocean sphere; Gerstner displacement in fragment (6 waves summed); normal via analytic derivative at the (undisplaced) surface; sphere-local TBN; Fresnel + sun specular + sky constant
4. `shaders/Ocean/shoreMask.ts`: sample GEBCO, smoothstep depth 0→50m for wave attenuation, depth 0→200m for color blend
5. `SolarSystem.ts` hook: mount the projected-grid material when camera altitude is below the Nyquist-derived threshold (~50-80 km), unmount above ×1.15 hysteresis. No changes to `PlanetSurfaceTSL.ts` — the projected grid draws over it where the ray hits ocean.
6. Phase 0 acceptance: AC1-AC7 from v1 + the concrete frame-diff AC5 test + a visual regression at pole crossing (fail-safe for C6).

**Phase 1 — FFT heightfield as drop-in** (~10-12 hours)

1. `shaders/Ocean/spectrumInit.wgsl`: Phillips spectrum compute, runs once
2. `shaders/Ocean/spectrumEvolve.wgsl`: per-frame time evolution with GEBCO-depth-aware tanh(kh) dispersion
3. `shaders/Ocean/ifft.wgsl`: Stockham-order radix-2 IFFT, 9 stages × 2 dims, workgroup-memory implementation (real work, not "two passes")
4. `shaders/Ocean/jacobian.wgsl`: per-texel Jacobian → foam mask
5. `OceanProjectedGrid.ts` swap: replace in-fragment Gerstner with a texture sample of the FFT heightfield. The ray-sphere intersection and shading code are unchanged.
6. Feature flag `enableFFTOcean` defaulting off until the frame-diff AC5 test passes with both paths at the threshold.

**Phase 2 — polish + chord-reactive** (~10-14 hours)

1. Cascaded FFT (two wavelength bands for swell + chop)
2. Skybox sampling for Fresnel (replace constant)
3. Chord-reactive: `AnalyserNode` → modulation uniform → Phillips spectrum multiplication (behind `reactive: boolean` flag, default off)
4. Wind direction runtime control

## Decisions needed before v3

Three questions to nail down before writing v3 as the production spec:

**D-v2-1.** Path B vs Path A. This doc recommends B. Need confirmation.

**D-v2-2.** GEBCO asset size. 30 MB commit, 30 MB git-LFS, or tile-on-demand from an S3-hosted bucket? The asset committed to the repo is simplest; git-LFS is cleanest; tile-on-demand is most scalable but adds network dependency.

**D-v2-3.** Chord-reactive in Phase 2 or not at all? Adds 4 hours and brand value. Could also be deferred indefinitely.

## What v1 got right (worth preserving)

Despite the structural issues, v1 identified the core framing correctly and a lot of the detail survives:

- **The visible failure mode at close zoom is real** — the specular-shimmer approach genuinely breaks down
- **Non-goals list** (no buoyancy, no caustics, no breaking waves) — still exactly right
- **WebGPU compute shader availability risk** — still real, still mitigated by the fallback path (though the fallback is now "projected-grid with Gerstner in fragment," not "patch with Gerstner in vertex")
- **No-changes-to-atmosphere discipline** — still honored in Path B because the projected grid draws *over* the existing shaders, not modifies them
- **Shore masking via a single global data source** — right instinct; just the wrong data source (2k specular instead of 15-arc-second bathymetry)
- **Gerstner-first as a visible-progress strategy** — still right, now inside the projected grid instead of inside a patch

## Referenced review sources

- Codex CLI review — 7 findings, strongest on technical feasibility and perf reality
- Gemini CLI review — 4 lateral ideas including projected-grid raycasting (Path B) and GEBCO bathymetry (data upgrade) and chord-reactive ecosystem hook
- Claude subagent review — 9 findings including the paradox in D1, the sagitta math, the AsteroidLOD pattern recognition, and the falsifiable AC5 test framing

All three reviews preserved in the session log as of 2026-04-11 for audit.
