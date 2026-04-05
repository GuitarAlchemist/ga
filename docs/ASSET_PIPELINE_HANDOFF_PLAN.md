# Asset Pipeline Handoff Plan

Date: 2026-04-05
Audience: Claude Code, Octopus, and human maintainers

## Purpose

This document converts the asset strategy into a concrete implementation plan for:

- Solar System Scope textures
- BlenderKit-derived hero assets
- GA React / Three.js integration
- optional `ga-godot` reuse

It is designed as a handoff pack for parallel execution.

## High-Level Decision

Use this split:

- **Solar System Scope** = canonical texture source for real solar-system bodies
- **BlenderKit** = selective source for special meshes, terrain, props, and cinematic scenes
- **GA runtime** = consumes only curated, attributed, manifest-driven assets

Do not make either external source a runtime dependency.

## Primary Deliverables

1. A canonical texture pack with attribution and manifest.
2. A model manifest for approved BlenderKit-derived runtime assets.
3. A typed asset loader for React/Three.js.
4. A first integrated visual stack for:
   Earth, Moon, Sun, Saturn, rings, stars.
5. A documented export pipeline for Blender-authored GLB assets.

## Recommended File Layout

### Canonical textures

```text
ReactComponents/ga-react-components/public/textures/space/solarsystemscope/
  earth/
  moon/
  sun/
  mercury/
  venus/
  mars/
  jupiter/
  saturn/
  uranus/
  neptune/
  stars/
  ATTRIBUTION.md
  manifest.json
```

### Curated models

```text
Assets/Models/BlenderKit/
  planets/
  terrain/
  stations/
  props/
  ATTRIBUTION.md
  manifest.json
```

### Runtime code

```text
ReactComponents/ga-react-components/src/
  assets/
    space/
      assetTypes.ts
      solarSystemManifest.ts
      blenderKitManifest.ts
      loadTextureSet.ts
      loadModelAsset.ts
```

## Workstream Split

Use two parallel workstreams plus one integration lane.

## Workstream A: Canonical Texture Library

Owner: Claude Code

### Tasks

1. Create the canonical texture directory structure.
2. Create `ATTRIBUTION.md` for Solar System Scope.
3. Define `manifest.json` shape for texture assets.
4. Normalize filenames and naming conventions.
5. Define quality tiers:
   `1k`, `2k`, `4k`, `8k` where available.
6. Prepare initial body set:
   Earth, Moon, Sun, Mars, Jupiter, Saturn, stars.
7. Document fallback behavior when optional maps are absent.

### Output

- curated texture hierarchy
- attribution file
- manifest schema
- initial approved texture set

### Acceptance Criteria

- every asset has source, license, and URL metadata
- every runtime file path is deterministic
- no loose ad hoc filenames
- Earth supports multi-map material composition

## Workstream B: BlenderKit Curation and Export Policy

Owner: Octopus

### Tasks

1. Curate 10-15 candidate BlenderKit assets by role:
   planet hero, terrain closeup, Sun variant, station/prop, stylized exoplanet.
2. Record asset metadata:
   asset name, author, source URL, license, intended role.
3. Separate assets into:
   `production-approved`, `demo-only`, `rejected`.
4. Define Blender normalization steps:
   scale, orientation, material cleanup, texture rebasing, mesh cleanup.
5. Define export rules:
   GLB preferred, embedded vs external textures, max triangle budgets, naming conventions.
6. Produce `manifest.json` schema for BlenderKit-derived assets.

### Output

- curated shortlist
- provenance manifest
- export checklist
- runtime approval policy

### Acceptance Criteria

- no adopted asset lacks provenance
- no adopted asset enters runtime without normalization rules
- canonical planets are not replaced casually by marketplace meshes

## Workstream C: GA Runtime Integration

Owner: Claude Code with review from Octopus

### Tasks

1. Create TypeScript asset types for textures and models.
2. Implement manifest-backed loaders.
3. Implement quality-tier selection logic.
4. Integrate the first canonical texture set into the current solar-system/Prime Radiant rendering path.
5. Add Earth layered rendering:
   day, night, clouds, normal, specular, atmosphere.
6. Add Saturn ring support.
7. Add stars skydome/background.
8. Add a single switch for “canonical” versus “cinematic” asset mode if needed later.

### Output

- typed runtime asset layer
- manifest-driven material loading
- upgraded planet visuals in the main React/Three.js path

### Acceptance Criteria

- runtime loads assets only from curated local manifests
- Earth, Moon, Sun, Saturn render correctly
- attribution is documented
- no hard-coded scattered asset paths in rendering components

## Sequencing

Recommended order:

1. Workstream A starts immediately.
2. Workstream B starts immediately.
3. Workstream C begins after A defines the manifest contract, but can stub the loaders in parallel.

## Detailed Task List

## Phase 1: Manifest and Provenance

### Task 1

Create `solarSystemManifest.ts` and JSON backing file.

Fields:

- `id`
- `displayName`
- `source`
- `sourceUrl`
- `license`
- `quality`
- `albedo`
- `night`
- `clouds`
- `normal`
- `specular`
- `emissive`
- `alpha`
- `notes`

### Task 2

Create `blenderKitManifest.ts` and JSON backing file.

Fields:

- `id`
- `displayName`
- `source`
- `sourceUrl`
- `license`
- `author`
- `runtimeFormat`
- `category`
- `approvedForRuntime`
- `usedIn`
- `polyBudget`
- `notes`

### Task 3

Create `ATTRIBUTION.md` files for both asset families.

### Acceptance Criteria

- manifests are human-readable
- manifests are parseable and type-checked
- provenance is attached before rendering code is merged

## Phase 2: Runtime Asset Layer

### Task 4

Implement `assetTypes.ts` with interfaces:

- `TextureAssetSet`
- `ModelAsset`
- `AssetQualityTier`

### Task 5

Implement `loadTextureSet.ts`.

Responsibilities:

- resolve tiered URLs
- validate required maps
- return a typed object for material builders

### Task 6

Implement `loadModelAsset.ts`.

Responsibilities:

- resolve approved model assets only
- reject non-approved IDs in development
- expose metadata for debug/credits panels

### Acceptance Criteria

- asset loading is centralized
- texture and model provenance can be surfaced to UI/debug panels
- no renderer component needs to know raw source folder layout

## Phase 3: Material Builders

### Task 7

Create material-builder utilities for:

- rocky planet
- gas giant
- Earth layered planet
- Sun/emissive body
- ring material
- star dome

Suggested module:

```text
ReactComponents/ga-react-components/src/assets/space/materialBuilders.ts
```

### Earth Builder Requirements

- albedo base
- optional night map blended by light direction
- optional cloud layer on second sphere
- optional normal map
- optional specular map
- optional atmosphere shell

### Ring Builder Requirements

- alpha-enabled texture
- double-sided rendering if needed
- configurable opacity and inner/outer radii

### Acceptance Criteria

- material creation is reusable
- canonical asset use is separated from scene composition
- renderer components stay smaller and easier to test

## Phase 4: Scene Integration

### Task 8

Identify the primary planet-rendering entry points in:

- `ReactComponents/ga-react-components`
- `Apps/ga-client`

Likely targets include:

- Prime Radiant scene code
- orrery/planet navigation surfaces
- demo pages with solar-system rendering

### Task 9

Replace placeholder or ad hoc textures in those surfaces with manifest-backed canonical assets.

### Task 10

Add one debug panel or dev overlay showing:

- current asset id
- source family
- quality tier
- map availability

### Acceptance Criteria

- a reviewer can confirm which assets are in use at runtime
- swapping tier or asset set does not require scene-specific hacks

## Phase 5: Blender Export Pipeline

### Task 11

Write `docs/BLENDER_ASSET_EXPORT_STANDARD.md` or equivalent.

It should define:

- import from BlenderKit
- normalization
- naming convention
- material cleanup
- GLB export settings
- texture rebasing policy
- polygon budget expectations

### Task 12

Create one golden-path example export:

- one approved BlenderKit asset
- exported to runtime-ready GLB
- entered into manifest
- rendered in a GA demo scene

### Acceptance Criteria

- there is a repeatable path from BlenderKit to runtime
- the first approved model proves the pipeline

## Suggested Acceptance Tests

### Test 1: Manifest Validation

- manifests parse successfully
- required fields present
- no duplicate IDs

### Test 2: Texture Loading

- Earth canonical set loads in supported tier
- fallback behavior works if optional maps are missing

### Test 3: Scene Render Smoke Test

- Earth, Moon, Sun, Saturn render without broken paths
- ring alpha works
- star dome renders

### Test 4: Provenance Visibility

- debug/credits surface can show source and license for loaded assets

### Test 5: Blender Asset Gate

- non-approved BlenderKit asset IDs are blocked from production runtime path

## Risks

### Risk 1: Asset sprawl

Mitigation:

- manifests are mandatory
- attribution files are mandatory
- no raw asset drop-ins without metadata

### Risk 2: Performance regressions from oversized textures

Mitigation:

- tiered delivery
- default desktop-safe preset
- optional later KTX2 compression

### Risk 3: Inconsistent art direction

Mitigation:

- Solar System Scope remains canonical for real planets
- BlenderKit remains selective and secondary

### Risk 4: Runtime/legal ambiguity

Mitigation:

- provenance tracked per asset
- attribution documented
- no runtime fetching from third-party marketplaces

## Handoff Notes for Claude Code

Priority for Claude Code:

1. manifest schema
2. typed asset loaders
3. canonical texture ingestion
4. material builders
5. React/Three.js integration

Claude Code should bias toward:

- deterministic file layout
- typed manifest access
- small reusable material utilities
- no hard-coded one-off asset paths

## Handoff Notes for Octopus

Priority for Octopus:

1. BlenderKit curation
2. provenance collection
3. export checklist
4. runtime approval gate
5. first hero asset pipeline

Octopus should bias toward:

- asset governance
- art-direction consistency
- mesh quality and runtime appropriateness
- avoiding marketplace dependency leakage into runtime

## Final Recommendation

The first concrete sprint should deliver:

1. canonical texture manifest,
2. attribution files,
3. Earth layered rendering,
4. Saturn ring rendering,
5. star dome,
6. one approved BlenderKit hero asset exported to GLB,
7. one debug/provenance panel.

That is enough to prove the pipeline end to end without overcommitting to a giant asset migration.
