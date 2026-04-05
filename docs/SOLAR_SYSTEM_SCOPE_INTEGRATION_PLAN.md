# Solar System Scope Integration Plan

Date: 2026-04-05
Audience: Claude Code, Octopus, and human maintainers

## Goal

Use Solar System Scope planetary textures to improve the visual quality of Guitar Alchemist’s existing solar-system and Prime Radiant scenes without creating a licensing or architecture mess.

This plan assumes integration into the current GA visualization stack first, then optional reuse in `ga-godot`.

## What the Site Actually Provides

As of 2026-04-05, the Solar System Scope textures page provides:

- equirectangular planetary textures,
- high-resolution and, for many bodies, ultra-resolution downloads,
- separate Earth maps including day, night, clouds, normal, and specular maps,
- Sun, Moon, stars, and Saturn ring textures,
- and an explicit Creative Commons Attribution 4.0 license notice.

Important nuance:

- The page clearly provides textures.
- It does **not** clearly present downloadable production-ready 3D planet meshes on the textures page.
- There is a separate “Paper Models” section on the site, but that is not the same thing as shipping reusable GLTF/GLB orbital assets.

Conclusion:

**Treat Solar System Scope as a texture source first, not a model source.**

## Licensing Position

The site states the textures are distributed under **Creative Commons Attribution 4.0 International** and may be used, adapted, and shared, including commercially.

Practical implication:

- You can bundle and transform the textures if you preserve attribution requirements.
- You should add attribution in repo docs and, ideally, in-app credits where visually appropriate.

Minimum attribution package:

- source name: Solar System Scope
- source URL: `https://www.solarsystemscope.com/textures/`
- license: CC BY 4.0
- date retrieved

## Recommended Strategy

Do **not** ingest the site as if it were a full asset pack of meshes + materials + scene graphs.

Instead:

1. Use your own procedural sphere/ring geometry or existing Three.js/Godot primitives.
2. Apply Solar System Scope textures as the surface source.
3. Normalize naming, storage, compression, and attribution inside GA.
4. Treat the textures as one curated asset family, not a random collection of downloaded JPG/PNG files.

This avoids a future where the repo contains a pile of manually downloaded planet images with no metadata and no lifecycle.

## Best Target Surfaces in GA

From the existing repo surface, the best first integration targets appear to be:

- Prime Radiant solar-system/orrery scenes in `ReactComponents/ga-react-components`
- `Apps/ga-client` demo pages using Three.js
- any existing Earth/cloud visual systems already planned in docs
- optional later reuse in `ga-godot`

This is a good fit because the repo already has:

- Three.js-heavy visualization work,
- Prime Radiant planet navigation concepts,
- weather/cloud texture design work,
- and multiple 3D demo surfaces.

## Architecture Recommendation

Create a first-class asset pipeline, not ad hoc file drops.

Recommended structure in GA:

```text
ReactComponents/ga-react-components/public/textures/space/solarsystemscope/
  mercury/
  venus/
  earth/
  mars/
  jupiter/
  saturn/
  uranus/
  neptune/
  moon/
  sun/
  stars/
  ATTRIBUTION.md
  manifest.json
```

Alternative if shared across multiple runtimes:

```text
Assets/Textures/SolarSystemScope/
  ...
```

Then expose those through a typed asset manifest in code instead of hard-coded file paths spread across components.

## Asset Manifest Design

Use a manifest entry per body:

```json
{
  "id": "earth",
  "displayName": "Earth",
  "albedo": "/textures/space/solarsystemscope/earth/day_8k.jpg",
  "night": "/textures/space/solarsystemscope/earth/night_8k.jpg",
  "clouds": "/textures/space/solarsystemscope/earth/clouds_8k.png",
  "normal": "/textures/space/solarsystemscope/earth/normal_8k.jpg",
  "specular": "/textures/space/solarsystemscope/earth/specular_8k.jpg",
  "license": "CC BY 4.0",
  "source": "https://www.solarsystemscope.com/textures/"
}
```

Benefits:

- central attribution,
- deterministic loading,
- easier compression/transcoding,
- consistent names across React, Three.js, and Godot.

## Model Strategy

Because the source is texture-centric, your model strategy should be your own.

Recommended default models:

- planets and moon: procedural sphere geometry
- Saturn ring: custom ring geometry with alpha texture
- Sun: sphere geometry plus emissive/bloom treatment
- sky: skydome or large inward-facing sphere using stars/Milky Way textures

This is preferable to hunting for third-party planet models unless you need:

- topographical displacement,
- cinematic close-ups,
- crater silhouettes,
- or landing-scale terrain.

For current Prime Radiant and educational visualization purposes, procedural spheres plus high-quality maps are the correct default.

## Rendering Recommendations

### Earth

Use a layered material stack:

- albedo/day map
- night lights blended on dark hemisphere
- separate cloud sphere slightly above the surface
- normal map for relief
- specular map for oceans
- optional atmosphere shell

This gives the best return on the available texture set.

### Gas Giants

Use:

- albedo map
- gentle normal or fake detail if needed
- atmospheric rim lighting
- slow band motion only if it supports the art direction

Do not overcomplicate these initially. Their maps already carry most of the visual character.

### Saturn

Use:

- sphere map for the planet
- ring geometry with alpha-enabled ring texture
- careful sorting and transparency handling

### Sun

Use the texture as a base, but rely on shader treatment, bloom, emissive intensity, and subtle animated distortion for life.

### Stars / Milky Way

Use the provided stars or stars+Milky Way texture on a skydome. Keep it as a background layer, not the primary visual element.

## Performance Guidance

Do not blindly ship ultra-resolution textures to all clients.

Use tiers:

- mobile: 1K to 2K
- standard desktop: 2K to 4K
- premium desktop / cinematic mode: 4K to 8K

Recommended pipeline:

1. Download originals.
2. Store canonical originals outside the runtime bundle if needed.
3. Generate optimized delivery versions:
   JPG for opaque albedo maps, PNG where alpha matters, consider KTX2/Basis for runtime compression later.
4. Expose quality tiers via the manifest.

This matters because Prime Radiant and demo pages already have heavy visual ambitions. Huge textures plus dynamic effects plus graph rendering can become expensive quickly.

## Integration Phases

## Phase 1: Asset Ingestion

1. Curate the bodies you actually need first:
   Earth, Moon, Sun, Mars, Jupiter, Saturn, ring, stars.
2. Download high-resolution versions first, not ultra.
3. Normalize filenames.
4. Add `ATTRIBUTION.md`.
5. Add `manifest.json`.

Deliverable:

- one coherent asset pack in-repo with explicit licensing.

## Phase 2: Prime Radiant / GA React Integration

1. Create a typed asset loader in `ga-react-components`.
2. Replace current placeholder/static planet materials with manifest-driven textures.
3. Add Earth layered rendering first as the quality benchmark.
4. Add Saturn ring implementation.
5. Add stars skydome.

Deliverable:

- one high-quality, production-grade solar-system visual stack in the main React visualization surface.

## Phase 3: Quality and Controls

1. Add texture quality tiers.
2. Add material toggles for:
   clouds, night lights, atmosphere, cinematic mode.
3. Add an attribution UI location:
   about dialog, credits panel, or asset credits drawer.

Deliverable:

- shippable user-facing integration without licensing ambiguity.

## Phase 4: Optional Godot Reuse

1. Reuse the same canonical texture pack.
2. Mirror the same manifest semantics if practical.
3. Avoid creating a separate, divergent copy of the asset set.

Deliverable:

- one texture source used across React and Godot surfaces.

## What Not To Do

1. Do not scatter downloaded textures across random folders.
2. Do not mix source resolutions and names inconsistently.
3. Do not assume the site gives you reusable 3D planet meshes.
4. Do not integrate ultra-resolution assets first.
5. Do not omit attribution because the assets are “free.”
6. Do not create separate untracked texture conventions in `ga`, `ga-godot`, and `guitaralchemist.github.io`.

## Recommended Immediate Tasks for Claude Code / Octopus

1. Create the texture asset directory and attribution file.
2. Define the manifest schema.
3. Implement a small typed asset loader in `ga-react-components`.
4. Integrate Earth, Moon, Sun, and Saturn first.
5. Add a quality-tier switch and a default desktop-safe preset.
6. Add a visible or documented attribution location in the UI.

## Bottom Line

The right integration model is:

**Solar System Scope textures as a licensed, curated, manifest-driven texture pack applied to your own geometry and rendering stack.**

That keeps the system:

- legally clean,
- visually strong,
- technically controllable,
- and reusable across React and Godot.

## BlenderKit: Where It Fits

BlenderKit should be treated differently from Solar System Scope.

Solar System Scope is best used as a **canonical texture source** for real planets.
BlenderKit is best used as a **selective model and scene source** for:

- hero assets,
- stylized or fictional planets,
- moon/terrain closeups,
- atmospheric set dressing,
- and cinematic one-off renders or demos.

It should not be your canonical source of scientific planet surfaces.

## What BlenderKit Actually Offers

As of 2026-04-05:

- BlenderKit exposes a large planet-model category with both free and paid/full-plan assets.
- The category clearly includes Earth, Moon, Sun, Saturn, gas giants, fictional planets, and terrain-style assets.
- BlenderKit states that downloaded assets are available for commercial use under either:
  - Royalty Free
  - CC0

Important licensing nuance:

- Royalty Free allows commercial use, but does **not** allow re-selling the asset itself in the same form.
- BlenderKit’s FAQ says games and higher-level derivative works are allowed, as long as the assets are not easily extractable by users.

This makes BlenderKit suitable for inclusion in:

- rendered videos,
- product visuals,
- packaged games/apps,
- and baked GLB-driven experiences,

but a worse fit for:

- redistributing raw marketplace-style asset packs,
- or treating downloaded assets as first-party canonical library items without metadata.

## Recommended BlenderKit Use Cases in GA

### Good use case 1: Hero Objects

Use BlenderKit when you need a visually richer object than a procedural sphere:

- close-up Moon terrain,
- a cinematic Sun,
- stylized exoplanets,
- ringed planets with custom geometry,
- science-fiction planetary set pieces.

These are most appropriate in:

- promo demos,
- splash scenes,
- Prime Radiant cinematic mode,
- `ga-godot` immersive scenes,
- or the public showcase site.

### Good use case 2: Terrain or Landmark Inserts

If you want:

- a moonbase shot,
- orbital station scene,
- crater flyover,
- planetary surface diorama,

BlenderKit is better than trying to force all of that from Solar System Scope textures alone.

### Good use case 3: Blender Authored Intermediate Assets

The strongest workflow is:

1. pull a model into Blender via BlenderKit,
2. clean topology/materials if needed,
3. swap or augment materials with your canonical texture policy,
4. export a controlled GLB/GLTF from your own pipeline,
5. ship only the exported artifact and metadata you intend to own operationally.

That gives you a stable runtime asset without requiring the app to depend directly on BlenderKit at runtime.

## Bad BlenderKit Use Cases

### Bad use case 1: Canonical planet library

Do not build your scientific solar-system foundation on mixed third-party planet meshes from BlenderKit. You will get:

- inconsistent art direction,
- inconsistent scale conventions,
- inconsistent topology,
- inconsistent material setups,
- and inconsistent authorship/licensing metadata.

For canonical planets, your default should still be:

- your own sphere/ring geometry,
- Solar System Scope textures,
- your own material/shader stack.

### Bad use case 2: Runtime dependency on BlenderKit

Do not architect the app so it depends on fetching BlenderKit assets dynamically at runtime. That creates:

- auth/subscription coupling,
- asset availability risk,
- licensing ambiguity in deployment,
- and nondeterministic builds.

BlenderKit belongs in the **content production pipeline**, not the production runtime.

### Bad use case 3: Mixing asset provenance casually

If a Sun mesh is from BlenderKit, Earth textures are from Solar System Scope, and ring shaders are custom, that is fine only if the provenance is tracked explicitly.

Without metadata, the asset library will become impossible to audit.

## Recommended Asset Policy

Use this rule:

### Tier A: Canonical Scientific Bodies

Use:

- custom geometry,
- Solar System Scope textures,
- your own renderer/material pipeline.

Applies to:

- Mercury
- Venus
- Earth
- Moon
- Mars
- Jupiter
- Saturn
- Uranus
- Neptune
- Sun
- star domes

### Tier B: Cinematic or Stylized Variants

Use BlenderKit selectively for:

- alternate art-direction scenes,
- educational flyovers,
- stylized exoplanets,
- close-range terrain shots,
- promotional or demo-only sequences.

### Tier C: Production Intermediates

If a BlenderKit asset is adopted, convert it into a controlled internal artifact:

- export to GLB/GLTF,
- normalize scale/orientation,
- rebake or standardize materials,
- store metadata and source attribution,
- then consume the exported result in GA.

## Proposed Directory Convention

If BlenderKit assets are adopted, do **not** mix them into the same directory as Solar System Scope source textures.

Use a separate structure such as:

```text
Assets/Models/BlenderKit/
  planets/
  terrain/
  stations/
  props/
  manifest.json
  ATTRIBUTION.md
```

And keep Solar System Scope under a separate texture namespace.

This matters because these are different asset classes with different provenance and different lifecycle expectations.

## Recommended Metadata Fields for BlenderKit Assets

Track at least:

```json
{
  "id": "moon-terrain-closeup-01",
  "source": "BlenderKit",
  "sourceUrl": "https://www.blenderkit.com/?query=category_subtree:planets",
  "license": "Royalty Free",
  "author": "TBD from asset page",
  "isRuntimeApproved": true,
  "runtimeFormat": "glb",
  "usedIn": ["ga-client-demo", "ga-godot"],
  "notes": "Used for close-up cinematic terrain, not canonical moon representation"
}
```

## Suggested Combined Strategy

The clean combined strategy is:

1. **Solar System Scope** for the canonical solar-system texture library.
2. **BlenderKit** for exceptional meshes and cinematic set pieces.
3. **Your own export pipeline** as the gate between source assets and runtime assets.

That gives you:

- scientific consistency where it matters,
- artistic flexibility where it helps,
- and operational control in the shipped app.

## Best Initial BlenderKit Targets

If I were choosing high-value first targets, I would look for:

1. Moon terrain or crater closeup assets for cinematic inserts.
2. A higher-fidelity Sun asset or effect-ready star object for hero scenes.
3. One or two stylized fictional/exoplanet assets for demos that are explicitly non-canonical.
4. Space station/orbital props if Prime Radiant wants stronger environmental storytelling.

I would **not** start by replacing Earth, Mars, or Jupiter canonical bodies with random BlenderKit models.

## Handoff Guidance for Claude Code and Octopus

Ask them to produce:

1. A curated shortlist of 10-15 candidate BlenderKit assets by role:
   canonical-adjacent, cinematic, stylized, terrain, prop.
2. A provenance schema and manifest.
3. An export pipeline spec:
   BlenderKit asset → Blender normalization → GLB export → GA runtime import.
4. A recommendation of which assets should remain demo-only versus production-approved.

## Bottom Line on BlenderKit

BlenderKit is useful, but not as the base truth for your solar-system asset library.

Use it as a **content-production source for special meshes and scenes**, while keeping:

- canonical planets on your own geometry,
- canonical maps from Solar System Scope,
- and runtime assets under your own manifest and export control.
