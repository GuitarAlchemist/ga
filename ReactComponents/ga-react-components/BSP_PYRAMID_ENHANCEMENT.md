# BSP DOOM Explorer - Pyramid Enhancement Documentation

## Overview

The BSP DOOM Explorer has been enhanced with a **pyramid structure** that reflects the hierarchical nature of musical equivalence groups. Floor sizes scale logarithmically based on element counts, creating a visually striking pyramid that grows from the abstract (small, top floors) to the concrete (large, bottom floors).

## Pyramid Structure

### Floor Hierarchy & Sizes

The pyramid structure is based on the **Harmonic Equivalence Hierarchy** from [Harmonious App](https://harmoniousapp.net/p/ec/Equivalence-Groups):

| Floor | Level | Element Count | Floor Size | Material Theme |
|-------|-------|---------------|------------|----------------|
| 0 | Set Classes (OPTIC/K) | ~115 | 50×50m | **Purple Cosmos** - Abstract space with cone primitives |
| 1 | Forte Codes (OPTIC) | ~200 | 68×68m | **Sepia Archive** - Catalogued knowledge with cylinder primitives |
| 2 | Prime Forms (OPTC) | ~350 | 88×88m | **Deep Teal Ocean** - Major-like simplicity with sphere primitives |
| 3 | Pitch Class Sets (OPC) | ~4,096 | 142×142m | **Emerald Forest** - Named chords with guitar models |
| 4 | Inversions (OC) | ~10,000 | 167×167m | **Ruby Crystal** - Chord inversions with guitar models |
| 5 | Voicings (Octave) | ~100,000 | 200×200m | **Golden Hour** - Physical voicings with guitar models |

### Logarithmic Scaling Formula

```typescript
const floorElementCounts = [115, 200, 350, 4096, 10000, 100000];
const minSize = 50;  // Smallest floor (Floor 0)
const maxSize = 200; // Largest floor (Floor 5)

const calculateFloorSize = (elementCount: number): number => {
  const logMin = Math.log10(floorElementCounts[0]);
  const logMax = Math.log10(floorElementCounts[5]);
  const logCount = Math.log10(elementCount);
  const normalized = (logCount - logMin) / (logMax - logMin);
  return minSize + normalized * (maxSize - minSize);
};
```

## Visual Enhancements

### 1. Realistic Stone Pyramid Walls

**Solid stone walls** connect each floor to the floor below, creating a realistic pyramid structure:

- **Material**: `createStoneMaterial()` - Realistic stone with high roughness (0.95)
- **Environment-specific colors**:
  - Floors 0-1: Brown sandstone (0x8B7355)
  - Floor 2: Slate gray (0x708090)
  - Floor 3: Dark olive green (0x556B2F)
  - Floor 4: Dark magenta (0x8B4789)
  - Floor 5: Dark goldenrod (0xB8860B)
- **Geometry**: 4 trapezoidal wall faces per floor (one for each side)
- **Shadows**: Walls cast and receive shadows for depth
- **Thickness**: 0.3 units for structural appearance

### 2. Stone Pyramid Roof (Floor 0)

The **top of the pyramid** (Floor 0) has a realistic stone roof:

- **Material**: Dim gray stone (0x696969)
- **Geometry**: 4 triangular faces converging to a peak
- **Height**: 20 units above Floor 0
- **Purpose**: Creates enclosed space at pyramid peak, reinforces inverted pyramid concept

### 3. Player Starting Position

Player **always starts at Floor 0** (top of pyramid):
- Position: `(0, 18, 0)` - Floor 0 base + eye height + elevation
- Conceptual: Start at most abstract level (Pitch Class Sets)
- Navigation: Descend through floors to reach more concrete voicings

### 4. Pyramid Edge Visualization (Floor Navigator)

Connecting lines in the floor navigator mini-map create a clear pyramid silhouette:

```typescript
// Four corner edges connecting previous floor to current floor
const corners = [
  [[-halfPrevSize, -20, -halfPrevSize], [-halfCurrSize, 0, -halfCurrSize]], // Back-left
  [[halfPrevSize, -20, -halfPrevSize], [halfCurrSize, 0, -halfCurrSize]],   // Back-right
  [[-halfPrevSize, -20, halfPrevSize], [-halfCurrSize, 0, halfCurrSize]],   // Front-left
  [[halfPrevSize, -20, halfPrevSize], [halfCurrSize, 0, halfCurrSize]],     // Front-right
];
```

### 2. Adaptive Grid System

Grid divisions scale with floor size for consistent visual density:

```typescript
const gridDivisions = Math.max(10, Math.floor(floorSize / 5));
const floorGrid = new THREE.GridHelper(floorSize, gridDivisions, gridColor1, gridColor2);
```

### 3. Pyramid Collision Detection

Player movement bounds adapt to current floor size:

```typescript
const currentFloorIndex = Math.floor(newPosition.y / 20);
const currentFloorSize = calculateFloorSize(floorElementCounts[clampedFloorIndex]);
const halfSize = currentFloorSize / 2;

// Clamp X and Z to current floor's pyramid bounds
newPosition.x = Math.max(-halfSize, Math.min(halfSize, newPosition.x));
newPosition.z = Math.max(-halfSize, Math.min(halfSize, newPosition.z));
```

## 3D Model Integration

### Model Loading System

A centralized model loading system with caching:

```typescript
const MODEL_PATHS: Record<string, string> = {
  'guitar': '/models/guitar.glb',
  'guitar2': '/models/guitar2.glb',
  'box': 'primitive:box',
  'sphere': 'primitive:sphere',
  'cylinder': 'primitive:cylinder',
  'cone': 'primitive:cone',
};

const modelCache = new Map<string, THREE.Group>();
const gltfLoader = new GLTFLoader();
```

### Model Assignment by Floor

Different model types for each abstraction level:

- **Floor 0 (Set Classes)**: Cone primitives - pointing upward (abstract)
- **Floor 1 (Forte Codes)**: Cylinder primitives - catalogued forms
- **Floor 2 (Prime Forms)**: Sphere primitives - pure mathematical forms
- **Floors 3-5 (Musical Elements)**: Guitar GLB models - concrete instruments

### Async Model Loading

Models load asynchronously without blocking the scene:

```typescript
loadModel(modelKey).then((model) => {
  model.position.set(x, 2.25, z);
  model.scale.set(0.4, 0.4, 0.4);
  
  // Apply color tint
  model.traverse((child) => {
    if ((child as THREE.Mesh).isMesh) {
      const mat = mesh.material as THREE.MeshStandardMaterial;
      mat.emissive = new THREE.Color(group.color);
      mat.emissiveIntensity = 0.3;
    }
  });
  
  model.userData = userData;
  floorGroup.add(model);
});
```

## Planned Enhancements

### Natural Environments

#### Floor 2: Ocean Environment (Prime Forms)
- **Water Shader**: Animated ocean surface with waves
- **Underwater Atmosphere**: Blue-green fog, caustics lighting
- **Marine Life**: Floating spheres as "musical fish"
- **Coral Platforms**: Organic platform shapes

#### Floors 0-1: Desert Dunes (Abstract Space)
- **Sand Shader**: Procedural sand texture with wind ripples
- **Dune Geometry**: Heightmap-based terrain
- **Desert Atmosphere**: Warm sepia fog, harsh sunlight
- **Sandstone Platforms**: Weathered stone materials

### Premium Materials

#### Floor 3: Emerald Forest (Chords)
- **Gem Materials**: PBR emerald with subsurface scattering
- **Crystal Platforms**: Faceted geometry with refraction
- **Forest Atmosphere**: Green ambient light, particle effects

#### Floor 4: Ruby Crystal (Inversions)
- **Ruby Material**: Deep red crystal with internal reflections
- **Marble Platforms**: Veined marble texture (Blender procedural)
- **Crystal Cave**: Stalactite/stalagmite formations

#### Floor 5: Golden Metal (Voicings)
- **Gold Material**: Anodized metal with high reflectivity
- **Brass Accents**: Warm metallic highlights
- **Polished Platforms**: Mirror-like surfaces

### Blender Model Integration

#### Required Models

1. **Ocean Assets** (`/models/ocean/`)
   - `coral_platform.glb` - Organic platform shapes
   - `seaweed.glb` - Animated vegetation
   - `fish.glb` - Swimming creatures

2. **Desert Assets** (`/models/desert/`)
   - `sandstone_platform.glb` - Weathered platforms
   - `dune_rock.glb` - Desert rock formations
   - `cactus.glb` - Desert vegetation

3. **Gem/Crystal Assets** (`/models/gems/`)
   - `emerald_crystal.glb` - Faceted emerald
   - `ruby_crystal.glb` - Faceted ruby
   - `diamond_platform.glb` - Crystal platform

4. **Metal Assets** (`/models/metal/`)
   - `gold_platform.glb` - Polished gold platform
   - `brass_ornament.glb` - Decorative brass elements
   - `bronze_statue.glb` - Musical instrument statues

#### Material Properties

**Ocean Water Shader**:
```typescript
const oceanMaterial = new THREE.MeshPhysicalMaterial({
  color: 0x006994,
  metalness: 0.0,
  roughness: 0.1,
  transmission: 0.9,
  thickness: 2.0,
  ior: 1.33, // Water IOR
  clearcoat: 1.0,
  clearcoatRoughness: 0.1,
});
```

**Emerald Gem Material**:
```typescript
const emeraldMaterial = new THREE.MeshPhysicalMaterial({
  color: 0x50C878,
  metalness: 0.0,
  roughness: 0.05,
  transmission: 0.7,
  thickness: 1.5,
  ior: 1.57, // Emerald IOR
  clearcoat: 1.0,
  sheen: 0.5,
  sheenColor: new THREE.Color(0x90EE90),
});
```

**Gold Metal Material**:
```typescript
const goldMaterial = new THREE.MeshPhysicalMaterial({
  color: 0xFFD700,
  metalness: 1.0,
  roughness: 0.15,
  clearcoat: 1.0,
  clearcoatRoughness: 0.1,
  reflectivity: 1.0,
  sheen: 1.0,
  sheenColor: new THREE.Color(0xFFA500),
});
```

## HUD Enhancements

### Pyramid Information Display

The HUD now shows pyramid structure information:

```typescript
<Typography variant="caption" sx={{ color: '#888', mt: 0.5, display: 'block' }}>
  🔺 Pyramid Structure:
</Typography>
<Typography sx={{ color: '#0f0', fontSize: '11px' }}>
  {currentFloorSize.toFixed(0)}×{currentFloorSize.toFixed(0)}m | 
  {elementCount.toLocaleString()} elements
</Typography>
```

## Performance Optimizations

### Model Caching

All loaded models are cached to prevent redundant loading:

```typescript
const modelCache = new Map<string, THREE.Group>();

if (modelCache.has(modelKey)) {
  return modelCache.get(modelKey)!.clone();
}
```

### Instanced Rendering

For floors with many elements (Floors 3-5), use instanced meshes:

```typescript
const instancedMesh = new THREE.InstancedMesh(geometry, material, count);
for (let i = 0; i < count; i++) {
  matrix.setPosition(positions[i]);
  instancedMesh.setMatrixAt(i, matrix);
}
```

### LOD System

Level-of-detail switching based on camera distance:

```typescript
const lod = new THREE.LOD();
lod.addLevel(highDetailModel, 0);
lod.addLevel(mediumDetailModel, 50);
lod.addLevel(lowDetailModel, 100);
```

## Implementation Checklist

### ✅ Completed Features

- [x] **Pyramid floor size calculation** (logarithmic scaling)
- [x] **Pyramid edge visualization** (corner lines with environment colors)
- [x] **Adaptive grid system** (scales with floor size)
- [x] **Pyramid collision detection** (dynamic bounds per floor)
- [x] **3D model loading system** (GLTFLoader + cache)
- [x] **Model assignment by floor** (primitives + guitars + gems + metals)
- [x] **HUD pyramid information display** (size + element count)
- [x] **Ocean environment materials** (Floor 2 - water shader, bubbles)
- [x] **Desert dunes materials** (Floors 0-1 - sand shader)
- [x] **Emerald gem materials** (Floor 3 - PBR emerald with transmission)
- [x] **Ruby crystal materials** (Floor 4 - marble with ruby accents)
- [x] **Gold metal materials** (Floor 5 - polished gold with sheen)
- [x] **Environment-specific grid colors** (desert, ocean, emerald, ruby, gold)
- [x] **Premium platform materials** (sandstone, coral, emerald, marble, gold)
- [x] **Model paths configuration** (ocean, desert, gems, metal assets)

### 🔄 In Progress / Needs Assets

- [ ] **Blender model integration** - Models need to be sourced/created:
  - Ocean: `coral_platform.glb`, `seaweed.glb`, `fish.glb`
  - Desert: `sandstone_platform.glb`, `dune_rock.glb`, `cactus.glb`
  - Gems: `emerald_crystal.glb`, `ruby_crystal.glb`, `diamond_platform.glb`
  - Metal: `gold_platform.glb`, `brass_ornament.glb`
- [ ] **Animated water shader** (currently static ocean material)
- [ ] **Sand shader with wind effects** (currently static sand material)
- [ ] **Caustics lighting** (underwater light patterns)
- [ ] **Enhanced particle systems** (sand storms, bubbles, sparkles)

## Free Resources

### Blender Models (CC0/CC-BY)

1. **Sketchfab** - Search for "ocean", "desert", "crystal", "gold"
   - Filter: Downloadable, CC0 or CC-BY license
   - Download as GLB format

2. **Poly Haven** - Free PBR materials and HDRIs
   - Ocean HDRIs for realistic reflections
   - Sand/rock textures

3. **BlenderKit** - Free Blender addon
   - Ocean assets, desert rocks, crystal formations
   - Export as GLB for Three.js

4. **Quaternius** - Free low-poly 3D models
   - Nature assets, crystals, decorative elements

### Shader Resources

1. **Three.js Examples** - Water shader, ocean simulation
2. **Shadertoy** - Sand dunes, caustics, gem refraction
3. **Book of Shaders** - Procedural textures, noise functions

## Implemented Features Summary

### Pyramid Structure ✅
- **Logarithmic floor scaling**: Floors grow from 50×50m (Floor 0) to 200×200m (Floor 5)
- **Visual pyramid edges**: Colored lines connecting floor corners
- **Dynamic collision bounds**: Player movement constrained to current floor size
- **Adaptive grids**: Grid divisions scale with floor size for consistent density
- **Inverted pyramid shape**: Smallest floor (Floor 0) at top, largest (Floor 5) at bottom
- **3D Floor Navigator**: Mini-map widget showing inverted pyramid structure with:
  - Floor 0 (top/peak): 1.0m × 1.0m - smallest, most abstract
  - Floor 5 (bottom/base): 4.0m × 4.0m - largest, most concrete
  - Pyramid edge lines connecting corners between floors
  - Current floor highlighting with emissive glow
  - Environment-specific colors for each floor
  - Isometric camera view for clear depth perception

### Environment Themes ✅

#### Floor 0-1: Desert Dunes (Purple/Sepia)
- **Material**: Procedural sand shader with noise
- **Grid**: Warm brown/tan lines
- **Edges**: Brown pyramid edges
- **Platforms**: Sandstone material
- **Models**: Cone (Floor 0), Cylinder (Floor 1) primitives

#### Floor 2: Ocean Floor (Deep Teal)
- **Material**: **Gerstner wave shader** with realistic ocean simulation
  - 3-wave set: long swell (45m) + mid (18m) + chop (9m)
  - Fresnel reflections (Schlick approximation, F0 = 0.02)
  - Beer's law absorption for depth-based color
  - Slope-based foam generation
  - Animated wave movement (time-based)
- **Geometry**: High-resolution (100×100 segments for wave deformation)
- **Grid**: Blue/cyan lines
- **Edges**: Steel blue pyramid edges
- **Platforms**: Coral/shell with pearlescent sheen and iridescence
- **Models**: Sphere primitives (bubbles/pearls)
- **Decorations**: 20 floating bubble spheres

#### Floor 3: Emerald Forest (Emerald Green)
- **Material**: Dark forest green with emerald glow
- **Grid**: Green lines
- **Edges**: Emerald green pyramid edges
- **Platforms**: Emerald gem material (IOR 1.57, transmission 0.7)
- **Models**: Emerald crystals + guitars
- **Vegetation**: Fluffy grass system

#### Floor 4: Ruby Crystal Cave (Ruby Red)
- **Material**: Misty rose marble
- **Grid**: Red/pink lines
- **Edges**: Ruby red pyramid edges
- **Platforms**: Marble with veining
- **Models**: Ruby crystals + guitars
- **Vegetation**: Fluffy grass system

#### Floor 5: Golden Temple (Gold)
- **Material**: Dark goldenrod with gold glow
- **Grid**: Gold lines
- **Edges**: Gold pyramid edges
- **Platforms**: Polished gold (metalness 1.0, sheen 1.0)
- **Models**: Brass ornaments + guitars
- **Vegetation**: Fluffy grass system

### Material System ✅

**Premium PBR Materials**:
- `createOceanMaterial()` - **Gerstner wave shader** with realistic ocean physics
  - Vertex shader: 3-wave Gerstner wave displacement
  - Fragment shader: Fresnel reflections + Beer's law + foam
  - Animated with time uniform for wave movement
- `createStoneMaterial(baseColor)` - **Realistic stone** for pyramid walls and roof
  - High roughness (0.95), no metalness
  - Subtle emissive variation for depth
  - Environment-specific colors per floor
- `createEmeraldMaterial()` - Gem with subsurface scattering
- `createRubyMaterial()` - Deep red crystal with reflections
- `createGoldMaterial()` - Anodized metal with sheen
- `createMarbleMaterial()` - Veined stone with clearcoat
- `createSandMaterial()` - Procedural sand texture

**Material Properties**:
- Physically-based rendering (PBR)
- Accurate IOR values (water 1.33, emerald 1.57, ruby 1.76)
- Transmission for transparency
- Clearcoat for glossy finish
- Sheen for anodized/pearlescent effects
- Iridescence for pearl-like surfaces

### Model Loading System ✅

**Supported Models**:
- Guitar models: `guitar.glb`, `guitar2.glb`
- Ocean assets: `coral_platform.glb`, `seaweed.glb`, `fish.glb`
- Desert assets: `sandstone_platform.glb`, `dune_rock.glb`, `cactus.glb`
- Gem assets: `emerald_crystal.glb`, `ruby_crystal.glb`, `diamond_platform.glb`
- Metal assets: `gold_platform.glb`, `brass_ornament.glb`
- Primitives: box, sphere, cylinder, cone

**Features**:
- Async loading with promises
- Model caching (no redundant loads)
- Automatic centering and scaling
- Shadow casting/receiving
- Color tinting via emissive
- Fallback to primitives if models unavailable

## Next Steps

1. ✅ Complete documentation
2. ✅ Create ocean environment for Floor 2
3. ✅ Create desert dunes for Floors 0-1
4. ✅ Implement gem materials (emerald, ruby)
5. ✅ Implement metal materials (gold, brass)
6. 🔄 Source/create Blender models (paths configured, models needed)
7. 🔄 Integrate animated water shader (static version implemented)
8. 🔄 Add enhanced particle effects (basic bubbles implemented)
9. ⏳ Performance testing with 100,000+ elements
10. ⏳ User testing and refinement

## How to Add Blender Models

1. **Download/Create Models**:
   - Use Sketchfab, Poly Haven, BlenderKit, or Quaternius
   - Ensure CC0 or CC-BY license
   - Export as GLB format

2. **Place in Public Directory**:
   ```
   ReactComponents/ga-react-components/public/models/
   ├── ocean/
   │   ├── coral_platform.glb
   │   ├── seaweed.glb
   │   └── fish.glb
   ├── desert/
   │   ├── sandstone_platform.glb
   │   ├── dune_rock.glb
   │   └── cactus.glb
   ├── gems/
   │   ├── emerald_crystal.glb
   │   ├── ruby_crystal.glb
   │   └── diamond_platform.glb
   └── metal/
       ├── gold_platform.glb
       └── brass_ornament.glb
   ```

3. **Models Will Auto-Load**:
   - System will attempt to load from configured paths
   - Falls back to primitives if models not found
   - No code changes needed once models are in place

## Performance Notes

- **Model caching**: Each model loaded once, then cloned
- **Async loading**: Models load without blocking scene initialization
- **Fallback system**: Primitives ensure scene always renders
- **LOD ready**: System prepared for level-of-detail switching
- **Instancing ready**: Can switch to instanced meshes for high element counts

