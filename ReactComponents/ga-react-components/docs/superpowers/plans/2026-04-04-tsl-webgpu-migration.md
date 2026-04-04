# TSL + WebGPURenderer Migration Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate Prime Radiant from GLSL ShaderMaterial + WebGLRenderer to TSL NodeMaterial + WebGPURenderer, activating all 22 existing TSL shader files.

**Status (2026-04-04 update):** Partial execution done via `/octo:embrace`. Foundation fix (TSL imports) committed at `8a4627d4`. ProceduralMoonTSL.ts library committed at `7b116ad3`. See **Discovery Findings** and **CRITICAL: Sequencing Correction** sections below before resuming execution.

**Tech Stack:** Three.js r180, TSL (`three/tsl`), WebGPURenderer (`three/webgpu`), 3d-force-graph v1.79.1, `PostProcessing` + `bloom()` + `chromaticAberration()`

---

## CRITICAL: Sequencing Correction (from debate gate)

**The original plan had a fatal ordering bug.** It said to flip `useWebGPU: true` FIRST then swap materials. This is wrong because:

> **WebGPURenderer silently replaces any `ShaderMaterial` with a blank NodeMaterial** (grey/white). No exception — just a console error and wrong visuals. Source: `three/webgpu` renders through `library.fromMaterial()` which is not registered for `ShaderMaterial`.

**Correct sequence:**
1. Fix TSL imports (DONE — commit `8a4627d4`)
2. Swap ALL `ShaderMaterial` instances → `NodeMaterial` **WHILE STILL ON WebGLRenderer** — TSL auto-compiles to GLSL on WebGL2, so each swap is visually verifiable immediately
3. Extend `PlanetSurfaceTSL.ts` to support `nightMap` + `specularMap` + `atmosphereType` (otherwise Earth loses city lights and ocean shimmer)
4. Only after ALL ShaderMaterials are gone: flip `useWebGPU: true`
5. Then tackle PostProcessing via `patch-package`

---

## Discovery Findings

### Bug 1: 5 TSL files had broken imports (FIXED in `8a4627d4`)
`THREE.MeshBasicNodeMaterial` does not exist on the `three` namespace — it's exported from `three/webgpu`. Fixed in: SunMaterialTSL, CymaticsTSL, VoronoiShellTSL, ReactionDiffusionTSL, FlowFieldParticleTSL. Also corrected SaturnRingsTSL.ts return type annotation.

### Bug 2: `fg.postProcessingComposer()` is getter-only
Kapsule method registered at `three-render-objects.js` line 102228, not a prop. `fg.postProcessingComposer(newValue)` silently returns the old composer, ignoring the argument. The internal `state.postProcessingComposer` slot cannot be swapped via public API.

**Solution path:** `patch-package` on `three-render-objects` to add (a) a setter prop for `postProcessingComposer`, (b) null-guard in tick loop (`state.postProcessingComposer?.render()`), (c) null-guard in resize filter. ~3 lines total.

### Bug 3: PlanetSurfaceTSL.ts is INCOMPLETE
Current interface only accepts `planetTexture`, `displacementMap`, `displacementScale`, `isEarth`. The old GLSL shader supported:
- `nightMap` — Earth city lights (night side)
- `specularMap` — ocean shimmer
- `atmosphereType` ('blue' | 'orange' | 'none') — sunrise glow tint
- `uSunPosView` — manual sun direction uniform

Without these, Earth will render without city lights and ocean highlights. **Must extend PlanetSurfaceTSL before swapping `createPlanetMesh`.**

### Bug 4: Procedural moons (15 variants) not covered in original plan
SolarSystem.ts has 15 inline GLSL moon fragments (Io, Europa, Ganymede, Callisto, Titan, Enceladus, Mimas, Iapetus, Triton, Miranda + rocky/icy base variants). These are now written as TSL factories in `shaders/ProceduralMoonTSL.ts` (commit `7b116ad3`). Use `MOON_TSL_FACTORIES[key]()` to look up by legacy fragment constant name, or match `def.fragment === IO_FRAG` by reference identity.

### Bug 5: Async render collision risk with Object.assign duck-type
TSL `PostProcessing.render()` returns a Promise (WebGPU command submission). The three-render-objects tick loop calls it fire-and-forget, which causes command buffer races. This rules out Option A (monkey-patching EffectComposer's `.render()`).

### Finding: BloomNode uniforms differ from UnrealBloomPass
Old: `bloomPass.strength = x` (direct property)
TSL: `bloomNode.strength.value = x` (UniformNode)
The surge bloom code at `ForceRadiant.tsx:2248-2257` needs updating when the swap happens.

### Finding: `useWebGPU: true` IS a supported init option
`three-render-objects` v1.40.5 supports it at line 102251. When set, it creates `WebGPURenderer` internally. But the library still creates EffectComposer unconditionally (line 102363) — that's the blocker requiring `patch-package`.

---

## Execution Status

| Task | Status | Commit |
|------|--------|--------|
| Fix 5 TSL import bugs | DONE | `8a4627d4` |
| Create ProceduralMoonTSL.ts (15 variants) | DONE | `7b116ad3` |
| Extend PlanetSurfaceTSL (night/spec/atmosphere) | DONE | `00f73a83` |
| Swap 13 ShaderMaterials in SolarSystem.ts | DONE | `7cb30f44` |
| Rewrite createPlanetMesh + createMoonMesh for TSL | DONE | `7cb30f44` |
| Update uniform setters in updateSolarSystem | DONE | `7cb30f44` |
| Swap 3 ShaderMaterials in ForceRadiant.tsx | DONE | `bdfe34a2` |
| Remove dead GLSL constants | DONE | `6a49a6f9` |
| patch-package three-render-objects | DEFERRED | — |
| Flip useWebGPU: true | DEFERRED | — |
| Wire TSL PostProcessing | DEFERRED | — |
| Visual verification | NEEDS HUMAN | `npm run dev` |

**Current state (post-migration, still on WebGLRenderer):**
All 16 `ShaderMaterial` instances (13 in SolarSystem.ts + 3 in
ForceRadiant.tsx) have been replaced with TSL `NodeMaterial`. TSL
materials auto-compile to GLSL on WebGL2, so the app should render
visually on the existing WebGLRenderer with no visible regression.
`npx tsc --noEmit` passes clean.

**What's deferred (require visual verification + patch-package):**
- Flipping `useWebGPU: true` still needs `patch-package` on
  three-render-objects to make `postProcessingComposer` nullable.
- EffectComposer/UnrealBloomPass/ShaderPass are still wired but will
  break on WebGPURenderer. Need TSL PostProcessing swap.
- Procedural moon fragment constants (IO_FRAG, etc.) remain as
  reference identifiers for the TSL factory lookup; they can be
  migrated to enum keys if desired.

**Known regressions on TSL-on-WebGL2 (pending user verify):**
- IXQL color/opacity/glow overrides on volumetric node materials
  silently no-op (`.uniforms` access on MeshBasicNodeMaterial).
- Saturn ring sun position now uses world-space (was view-space
  in GLSL); uniform update was adjusted accordingly.

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `ForceRadiant.tsx` | Modify | Enable `useWebGPU`, replace postprocessing, swap 3 ShaderMaterials |
| `SolarSystem.ts` | Modify | Replace 13 GLSL ShaderMaterials with TSL imports, update uniform setters |
| `shaders/SunMaterialTSL.ts` | Activate (exists) | Sun plasma material |
| `shaders/PlanetSurfaceTSL.ts` | Activate (exists) | Planet day/night/bump/specular |
| `shaders/FresnelGlowTSL.ts` | Activate (exists) | Atmosphere, corona, Titan, marker |
| `shaders/SaturnRingsTSL.ts` | Activate (exists) | Saturn rings with Cassini division |
| `shaders/OrbitTrailTSL.ts` | Activate (exists) | Orbit trail fading lines |
| `shaders/AuroraTSL.ts` | Activate (exists) | Aurora borealis curtains |
| `shaders/RingGlowTSL.ts` | Activate (exists) | Saturn ring glow overlay |
| `shaders/StormVortexTSL.ts` | Activate (exists) | Jupiter Great Red Spot |
| `shaders/VolumetricCoreTSL.ts` | Activate (exists) | Raymarched node cores |
| `shaders/SkyboxNebulaTSL.ts` | Activate (exists) | Skybox nebula background |

---

## Critical Context

### How `three-render-objects` works (the render pipeline we're patching)

```
Line 534: state.renderer = new (useWebGPU ? WebGPURenderer : WebGLRenderer)(config)
Line 552: state.postProcessingComposer = new EffectComposer(state.renderer)  // CRASHES with WebGPURenderer
Line 234: state.postProcessingComposer ? composer.render() : renderer.render() // duck-type opportunity
Line 582: [renderer, composer].forEach(r => r.setSize(w, h))  // PostProcessing has no setSize
```

### The duck-type strategy

Replace `postProcessingComposer` with a wrapper around TSL `PostProcessing` that:
1. Has `.render()` (calls `PostProcessing.render()`)
2. Has `.setSize()` (no-op — PostProcessing uses renderer's size)
3. Has `.addPass()` (no-op — we configure via `outputNode`)

### Uniform update pattern change

GLSL: `material.uniforms.uTime.value = t`
TSL: `material.userData.timeUniform.value = t` (each TSL file stores uniforms in `userData`)

---

### Task 1: Enable WebGPURenderer + duck-type PostProcessing replacement

**Files:**
- Modify: `src/components/PrimeRadiant/ForceRadiant.tsx:1-15` (imports)
- Modify: `src/components/PrimeRadiant/ForceRadiant.tsx:1448-1454` (useWebGPU flag)
- Modify: `src/components/PrimeRadiant/ForceRadiant.tsx:1618-1699` (postprocessing setup)

- [ ] **Step 1: Add TSL/WebGPU imports to ForceRadiant.tsx**

Replace old postprocessing imports with TSL equivalents. At the top of the file, replace:

```typescript
import { UnrealBloomPass } from 'three/examples/jsm/postprocessing/UnrealBloomPass.js';
import { ShaderPass } from 'three/examples/jsm/postprocessing/ShaderPass.js';
```

With:

```typescript
import { PostProcessing } from 'three/webgpu';
import { pass } from 'three/tsl';
import { bloom } from 'three/examples/jsm/tsl/display/BloomNode.js';
import { chromaticAberration } from 'three/examples/jsm/tsl/display/ChromaticAberrationNode.js';
```

- [ ] **Step 2: Enable useWebGPU on ForceGraph3D**

At line ~1448, change:

```typescript
const fg = ForceGraph3D({
  controlType: 'orbit',
  rendererConfig: { preserveDrawingBuffer: true, antialias: true },
  // useWebGPU: true — DISABLED...
})(container)
```

To:

```typescript
const fg = ForceGraph3D({
  controlType: 'orbit',
  rendererConfig: { preserveDrawingBuffer: true, antialias: true },
  useWebGPU: true,
})(container)
```

- [ ] **Step 3: Replace EffectComposer with duck-typed TSL PostProcessing**

The old `EffectComposer` (created internally by `three-render-objects` at init) will crash or be non-functional. Replace it immediately after force-graph initialization. Find the section after `fg` is created (around line 1618) and replace the entire postprocessing setup:

Replace:

```typescript
const bloomPass = new UnrealBloomPass(
  bloomSize,
  isLowEnd ? 0.3 : 0.6,   // strength
  isLowEnd ? 0.3 : 0.6,   // radius
  isLowEnd ? 0.8 : 0.5,   // threshold
);
fg.postProcessingComposer().addPass(bloomPass);
bloomPassRef.current = bloomPass;
```

And all subsequent `addSafePass` / `ShaderPass` / chromatic aberration code through line ~1699 with:

```typescript
// ── TSL PostProcessing (replaces old EffectComposer) ──
const renderer = fg.renderer();
const tslPostProcessing = new PostProcessing(renderer);

const scenePass = pass(fg.scene(), fg.camera());
const scenePassColor = scenePass.getTextureNode('output');

// Bloom — TSL native (replaces UnrealBloomPass)
const bloomNode = bloom(scenePassColor, {
  strength: isLowEnd ? 0.3 : 0.6,
  radius: isLowEnd ? 0.3 : 0.6,
  threshold: isLowEnd ? 0.8 : 0.5,
});

// Chromatic aberration — TSL native (replaces ShaderPass)
let outputNode = scenePassColor.add(bloomNode);
if (!isLowEnd) {
  outputNode = chromaticAberration(outputNode, {
    offset: new THREE.Vector2(0.0008, 0.0008),
  });
}

tslPostProcessing.outputNode = outputNode;

// Duck-type: replace three-render-objects' postProcessingComposer
// It calls .render(), .setSize() on this object
const duckComposer = {
  render: () => tslPostProcessing.render(),
  setSize: () => {}, // PostProcessing uses renderer's size automatically
  addPass: () => {}, // no-op — TSL uses outputNode chain
};
// Monkey-patch: three-render-objects stores composer in internal state
// accessed via fg.postProcessingComposer(). We replace the internal ref.
(fg as any)._regl.postProcessingComposer = duckComposer;
```

Note: The exact internal state access pattern may vary. Check how `fg.postProcessingComposer()` accesses state — it may be `fg.__kapsuleInstance` or similar. We'll verify in step 4.

- [ ] **Step 4: Find the correct internal state path for duck-type injection**

Run in browser console or add a temporary log:

```typescript
console.log('composer ref:', fg.postProcessingComposer());
console.log('fg keys:', Object.keys(fg));
```

The `postProcessingComposer()` getter in `three-render-objects` returns `state.postProcessingComposer`. We need to find how to SET it. Check if `fg.postProcessingComposer(duckComposer)` works as a setter (kapsule pattern often uses the same name for get/set).

- [ ] **Step 5: Remove old postprocessing refs**

Remove or comment out these refs since they reference old `ShaderPass`/`UnrealBloomPass` types:

```typescript
// Remove these refs:
const bloomPassRef = useRef<UnrealBloomPass | null>(null);
const moebiusPassRef = useRef<ShaderPass | null>(null);
const causticsPassRef = useRef<ShaderPass | null>(null);
const dispersionPassRef = useRef<ShaderPass | null>(null);
```

Replace with TSL-compatible refs if needed for runtime toggling:

```typescript
const tslPostProcessingRef = useRef<PostProcessing | null>(null);
```

- [ ] **Step 6: Remove the `addSafePass` helper and all ShaderPass usage**

Delete the `addSafePass` function (lines ~1636-1649) and all calls to it. The caustics, dispersion, and Moebius passes are disabled by default — skip them for now. They can be re-added as TSL Fn() nodes later.

- [ ] **Step 7: Verify build compiles**

Run: `npx tsc --noEmit --pretty`
Expected: Clean compile (or only pre-existing warnings)

- [ ] **Step 8: Commit**

```bash
git add src/components/PrimeRadiant/ForceRadiant.tsx
git commit -m "feat: enable WebGPURenderer + TSL PostProcessing (replaces EffectComposer)"
```

---

### Task 2: Replace SolarSystem.ts GLSL ShaderMaterials with TSL

**Files:**
- Modify: `src/components/PrimeRadiant/SolarSystem.ts:1-30` (imports)
- Modify: `src/components/PrimeRadiant/SolarSystem.ts:475-680` (remove PLANET_VERT/PLANET_FRAG constants)
- Modify: `src/components/PrimeRadiant/SolarSystem.ts:680-730` (createPlanetMesh)
- Modify: `src/components/PrimeRadiant/SolarSystem.ts:730-800` (createMoonMesh, orbit trail, planet label)
- Modify: `src/components/PrimeRadiant/SolarSystem.ts:887-1400` (createSolarSystem — sun, corona, atmosphere, rings, aurora, etc.)
- Modify: `src/components/PrimeRadiant/SolarSystem.ts:1860-1990` (updateSolarSystem — uniform updates)

- [ ] **Step 1: Add TSL imports to SolarSystem.ts**

At the top of `SolarSystem.ts`, add:

```typescript
import { createSunMaterialTSL } from './shaders/SunMaterialTSL';
import { createPlanetSurfaceMaterialTSL } from './shaders/PlanetSurfaceTSL';
import { createCoronaMaterialTSL, createAtmosphereMaterialTSL, createTitanAtmosphereMaterialTSL, createMarkerMaterialTSL } from './shaders/FresnelGlowTSL';
import { createSaturnRingsMaterialTSL } from './shaders/SaturnRingsTSL';
import { createOrbitTrailMaterialTSL } from './shaders/OrbitTrailTSL';
import { createAuroraMaterialTSL } from './shaders/AuroraTSL';
import { createRingGlowMaterialTSL } from './shaders/RingGlowTSL';
import { createStormVortexMaterialTSL } from './shaders/StormVortexTSL';
```

- [ ] **Step 2: Remove GLSL shader string constants**

Delete the `PLANET_VERT` and `PLANET_FRAG` GLSL string constants (lines ~475-680). These are replaced by `PlanetSurfaceTSL.ts`.

Also delete the smaller inline vertex/fragment shader strings:
- `VERT` constant (simple position pass-through)
- Any inline `vertexShader`/`fragmentShader` string literals in `createPlanetMesh` and `createMoonMesh`

- [ ] **Step 3: Replace `createPlanetMesh` to use TSL**

Replace the function body. The GLSL version creates `THREE.ShaderMaterial` with manual uniforms. The TSL version:

```typescript
function createPlanetMesh(def: PlanetDef, scale: number): THREE.Mesh {
  const baseSegments = def.radius > 0.5 ? 64 : def.radius > 0.1 ? 48 : 32;
  const segments = def.textureDisplacement ? Math.max(baseSegments, 64) : baseSegments;
  const geo = new THREE.SphereGeometry(def.radius * scale, segments, segments);
  geo.computeBoundingSphere();

  if (def.texture) {
    const map = loadTex(def.texture);
    const mat = createPlanetSurfaceMaterialTSL({
      map,
      nightMap: def.textureNight ? loadTex(def.textureNight) : undefined,
      specularMap: def.textureSpecular ? loadTex(def.textureSpecular) : undefined,
      displacementMap: def.textureDisplacement ? loadTex(def.textureDisplacement) : undefined,
      displacementScale: def.textureDisplacement ? def.radius * scale * (def.name === 'mars' ? 0.12 : 0.05) : 0,
      isEarth: def.name === 'earth',
      atmosphereType: def.name === 'earth' ? 'blue' : def.name === 'venus' ? 'orange' : 'none',
    });
    const mesh = new THREE.Mesh(geo, mat);
    mesh.userData.isPlanetShader = true;
    return mesh;
  }

  // Procedural fallback — use basic material with color
  const mat = new THREE.MeshBasicNodeMaterial();
  mat.colorNode = vec3(0.5, 0.5, 0.5); // placeholder
  return new THREE.Mesh(geo, mat);
}
```

Note: Check `PlanetSurfaceTSL.ts` for the exact options interface. The options above are based on the exploration results — verify parameter names match the actual exports.

- [ ] **Step 4: Replace sun material in createSolarSystem**

Find the sun `ShaderMaterial` creation (line ~919) and replace:

```typescript
const sunMat = createSunMaterialTSL({
  sunTexture: sunTex,
  quality: 'high',
});
const sunMesh = new THREE.Mesh(sunGeo, sunMat);
sunMesh.name = 'sun';
```

- [ ] **Step 5: Replace corona material**

Find the corona `ShaderMaterial` (line ~1058) and replace:

```typescript
const coronaMat = createCoronaMaterialTSL();
```

- [ ] **Step 6: Replace atmosphere shell material**

Find the atmosphere `ShaderMaterial` inside the planet loop (line ~1122) and replace:

```typescript
const atmoMat = createAtmosphereMaterialTSL({
  color: new THREE.Vector3(...def.atmosphere.color.split(',').map(c => parseFloat(c.trim())) as [number, number, number]),
  intensity: def.atmosphere.intensity,
  power: def.atmosphere.power,
});
```

- [ ] **Step 7: Replace Saturn rings material**

Find the Saturn ring `ShaderMaterial` (line ~1256) and replace:

```typescript
const ringMat = createSaturnRingsMaterialTSL({
  ringTexture: ringTex,
  innerRadius: ringInner,
  outerRadius: ringOuter,
  scale,
});
```

- [ ] **Step 8: Replace Titan atmosphere material**

Find the Titan `ShaderMaterial` (line ~1373) and replace:

```typescript
const titanAtmoMat = createTitanAtmosphereMaterialTSL();
```

- [ ] **Step 9: Replace orbit trail material**

Find the orbit trail `LineBasicMaterial` / `ShaderMaterial` in `createOrbitTrail` (line ~783) and replace:

```typescript
const mat = createOrbitTrailMaterialTSL(color);
```

- [ ] **Step 10: Replace aurora material**

Find the aurora `ShaderMaterial` (line ~1612) and replace:

```typescript
const auroraMat = createAuroraMaterialTSL();
```

- [ ] **Step 11: Replace ring glow material**

Find the ring glow `ShaderMaterial` (line ~1709) and replace:

```typescript
const glowMat = createRingGlowMaterialTSL();
```

- [ ] **Step 12: Replace storm vortex material**

Find the storm `ShaderMaterial` (line ~1790) and replace:

```typescript
const stormMat = createStormVortexMaterialTSL();
```

- [ ] **Step 13: Replace location marker material**

Find the marker `ShaderMaterial` (line ~2457) and replace:

```typescript
const markerMat = createMarkerMaterialTSL();
```

- [ ] **Step 14: Verify build compiles**

Run: `npx tsc --noEmit --pretty`
Expected: Clean compile

- [ ] **Step 15: Commit**

```bash
git add src/components/PrimeRadiant/SolarSystem.ts
git commit -m "feat: replace all GLSL ShaderMaterials with TSL NodeMaterials in SolarSystem"
```

---

### Task 3: Update uniform setters in updateSolarSystem

**Files:**
- Modify: `src/components/PrimeRadiant/SolarSystem.ts:1860-1990` (updateSolarSystem)

TSL materials store uniforms differently. Each TSL file exposes uniforms via `material.userData` (e.g. `userData.timeUniform`, `userData.sunPosUniform`). The update loop needs to change from:

```typescript
// Old GLSL pattern:
if (u.uSunPosView) u.uSunPosView.value.copy(_sunViewPos);
if (u.uTime) u.uTime.value = time;
```

To the TSL pattern (varies per material — check each TSL file's `userData` exports).

- [ ] **Step 1: Read each TSL file's userData uniform pattern**

Check each TSL file for how it exposes updatable uniforms. Common patterns:
- `material.userData.timeUniform` — a `uniform(float)` node
- `material.userData.sunPosUniform` — a `uniform(vec3)` node
- Update via: `material.userData.timeUniform.value = t`

Read each TSL file's source to confirm the exact property names.

- [ ] **Step 2: Update the planet shader uniform loop**

In `updateSolarSystem`, replace the planet uniform update block:

```typescript
// Update planet shader uniforms
if (mesh.material.userData?.sunPosUniform) {
  mesh.material.userData.sunPosUniform.value.copy(_sunViewPos);
}
if (mesh.material.userData?.timeUniform) {
  mesh.material.userData.timeUniform.value = time;
}
```

- [ ] **Step 3: Update Saturn ring uniform**

```typescript
if (def.name === 'saturn') {
  const satRing = mesh.getObjectByName('saturn-ring') as THREE.Mesh | undefined;
  if (satRing?.material.userData?.sunPosUniform) {
    satRing.material.userData.sunPosUniform.value.copy(_sunViewPos);
  }
}
```

- [ ] **Step 4: Update sun shader time uniform**

```typescript
const sunMesh = group.getObjectByName('sun') as THREE.Mesh | undefined;
if (sunMesh?.material.userData?.timeUniform) {
  sunMesh.material.userData.timeUniform.value = time;
}
```

- [ ] **Step 5: Remove old _sunViewPos computation if TSL handles it differently**

Check if `PlanetSurfaceTSL.ts` uses `positionWorld` and a sun position uniform, or if it relies on Three.js PBR lighting (which uses `PointLight` automatically). If PBR lighting handles the sun, remove the manual sun position computation entirely — the `PointLight` at group origin already provides correct lighting.

- [ ] **Step 6: Verify build compiles**

Run: `npx tsc --noEmit --pretty`

- [ ] **Step 7: Commit**

```bash
git add src/components/PrimeRadiant/SolarSystem.ts
git commit -m "feat: update uniform setters for TSL materials"
```

---

### Task 4: Replace ForceRadiant.tsx ShaderMaterials (nodes, skybox, god rays)

**Files:**
- Modify: `src/components/PrimeRadiant/ForceRadiant.tsx:415-436` (volumetric core material)
- Modify: `src/components/PrimeRadiant/ForceRadiant.tsx:2330` (skybox nebula)
- Modify: `src/components/PrimeRadiant/ForceRadiant.tsx:2612` (god rays)

- [ ] **Step 1: Replace volumetric core material**

Replace `createVolumetricMaterial` (line 415) with VolumetricCoreTSL.ts. Read the TSL file first to confirm its interface:

```typescript
import { createVolumetricCoreMaterialTSL } from './shaders/VolumetricCoreTSL';

function createVolumetricMaterial(color: THREE.Color, complexity: number, intensity: number) {
  const key = `${color.getHexString()}-${complexity}-${intensity}`;
  if (shaderMaterialCache.has(key)) return shaderMaterialCache.get(key)!.clone();

  const mat = createVolumetricCoreMaterialTSL({ color, complexity, intensity });
  shaderMaterialCache.set(key, mat);
  return mat.clone();
}
```

Note: Check if `MeshBasicNodeMaterial.clone()` works correctly. If not, create fresh instances.

- [ ] **Step 2: Replace skybox nebula material**

Replace the skybox `ShaderMaterial` (line ~2330) with `SkyboxNebulaTSL.ts`:

```typescript
import { createSkyboxNebulaMaterialTSL } from './shaders/SkyboxNebulaTSL';

// Replace: const skyMat = new THREE.ShaderMaterial({...})
const skyMat = createSkyboxNebulaMaterialTSL();
```

- [ ] **Step 3: Replace god ray material**

Replace the god ray `ShaderMaterial` (line ~2612) with `GodRayTSL.ts` or `VolumetricCoreTSL.ts`:

```typescript
import { createGodRayMaterialTSL } from './shaders/GodRayTSL';

// Replace: const godRayMat = new THREE.ShaderMaterial({...})
const godRayMat = createGodRayMaterialTSL();
```

- [ ] **Step 4: Remove all GLSL shader string constants from ForceRadiant.tsx**

Delete `volumetricVertexShader`, `volumetricFragmentShader`, and any other inline GLSL strings that are now replaced by TSL.

- [ ] **Step 5: Verify build compiles**

Run: `npx tsc --noEmit --pretty`

- [ ] **Step 6: Commit**

```bash
git add src/components/PrimeRadiant/ForceRadiant.tsx
git commit -m "feat: replace ForceRadiant GLSL materials with TSL (volumetric, skybox, god rays)"
```

---

### Task 5: Clean up and remove dead GLSL code

**Files:**
- Modify: `src/components/PrimeRadiant/SolarSystem.ts` (remove unused constants/functions)
- Modify: `src/components/PrimeRadiant/ForceRadiant.tsx` (remove unused imports)

- [ ] **Step 1: Remove unused GLSL imports and constants**

In SolarSystem.ts, remove:
- `PLANET_VERT` constant (if not already deleted)
- `PLANET_FRAG` constant
- `VERT` constant
- Any per-planet fragment shader strings
- The old `_sunViewPos` / `_viewMatrix` computation if PBR lighting handles sun direction

In ForceRadiant.tsx, remove:
- `import { UnrealBloomPass }` (already done in Task 1)
- `import { ShaderPass }` (already done in Task 1)
- `import { MoebiusShader }` — keep if planning TSL port, remove if deferring
- `import { CausticsShader }` — keep if planning TSL port, remove if deferring
- `import { DispersionShader }` — keep if planning TSL port, remove if deferring
- Old `bloomPassRef`, `moebiusPassRef`, `causticsPassRef`, `dispersionPassRef` refs
- The `addSafePass` helper function
- Any remaining GLSL shader string literals

- [ ] **Step 2: Remove old postprocessing comments**

Remove the old comments about TSL being incompatible with WebGLRenderer:
- SolarSystem.ts line ~916: "TSL MeshBasicNodeMaterial does NOT work with 3d-force-graph's WebGLRenderer"
- ForceRadiant.tsx line ~1451: "useWebGPU: true — DISABLED"
- ForceRadiant.tsx line ~2891: "TSL materials require WebGPURenderer — skip on WebGLRenderer"

- [ ] **Step 3: Verify no remaining `ShaderMaterial` references**

Run: `grep -n "ShaderMaterial" SolarSystem.ts ForceRadiant.tsx`
Expected: No matches (or only in comments explaining what was replaced)

- [ ] **Step 4: Verify build compiles**

Run: `npx tsc --noEmit --pretty`

- [ ] **Step 5: Commit**

```bash
git add src/components/PrimeRadiant/SolarSystem.ts src/components/PrimeRadiant/ForceRadiant.tsx
git commit -m "chore: remove dead GLSL code and old postprocessing imports"
```

---

### Task 6: Visual verification

**Files:** None (testing only)

- [ ] **Step 1: Start dev server**

Run: `npm run dev`
Open: `http://localhost:5173/test/prime-radiant`

- [ ] **Step 2: Check console for WebGPU/WebGL backend**

Open browser DevTools console. Look for:
- `THREE.WebGPURenderer` initialization message
- Any "WebGPU not available, falling back to WebGL2" messages
- Any shader compile errors

- [ ] **Step 3: Visual checklist**

Verify each element renders correctly:
- [ ] Sun: animated plasma, warm colors, corona glow
- [ ] Earth: day/night terminator facing sun, city lights on night side, blue atmosphere rim
- [ ] Moon: visible near Earth
- [ ] Mars: reddish, slight displacement
- [ ] Saturn: rings with Cassini division, backlit glow
- [ ] Jupiter: storm vortex visible
- [ ] Orbit trails: fading alpha
- [ ] Bloom: soft glow on bright objects (sun, city lights)
- [ ] Force graph nodes: still render with volumetric cores
- [ ] Skybox: nebula visible in background
- [ ] FPS: maintaining 30+ FPS

- [ ] **Step 4: Screenshot and compare**

Take screenshots and compare to the GLSL version. Key checks:
- Day/night terminator position matches UTC time
- Atmosphere doesn't appear as "double Earth"
- Bloom intensity matches previous
- No black screen, no missing objects

- [ ] **Step 5: Test on mobile/tablet (low-end path)**

Verify `isLowEnd` codepath works — reduced bloom, no chromatic aberration, lower segment counts.

---

### Task 7 (Deferred): Port remaining post-processing effects to TSL

This task is deferred — the effects start disabled and can be ported later.

**Effects to port:**
- Caustics (`CausticsPass.ts` → TSL `Fn()`)
- Dispersion (`DispersionPass.ts` → TSL `Fn()`)
- Moebius (`MoebiusPassTSL.ts` — already has TSL, just need to wire as post-processing node)

Each becomes a TSL `Fn()` node chained into the `postProcessing.outputNode` pipeline.
