# Implementation Plan: Blender Models & Grothendieck Monoid Integration

## Overview

This document outlines the implementation plan for two major enhancements to the Guitar Alchemist project:

1. **3D Asset Integration** - Free Blender models for BSP DOOM Explorer (Egyptian pyramid theme)
2. **Advanced Music Theory** - Grothendieck monoid and Markov chains for fretboard navigation

## Part 1: 3D Asset Integration for BSP DOOM Explorer

### Objective
Enhance the BSP DOOM Explorer with high-quality, free 3D models that fit the Egyptian pyramid + alchemy theme.

### Asset Categories

#### 1. Core Architecture
- **Pyramid models** - Main structure
- **Pillars with hieroglyphs** - Structural elements
- **Obelisks** - Landmarks

#### 2. Alchemy Props
- **Ankh (Key of Life)** - Symbol of eternal life
- **Eye of Horus** - Protection and power symbol
- **Alchemy flasks/jars** - Potion containers
- **Ancient scrolls/tablets** - Knowledge artifacts
- **Mysterious devices** - Arcane machinery

#### 3. Decorative Elements
- **Gems** (21 different shapes pack)
  - Emerald, ruby, sapphire cuts
  - Use for alchemy ingredients
  - Emissive materials for magical glow
- **Jars/Vessels**
  - Canopic jars
  - Clay vessels
  - Glass flasks with glowing contents
- **Torches**
  - Wall-mounted torches
  - Free-standing braziers
  - Flickering flame effects

#### 4. Egyptian Artifacts
- **Scarab artifacts**
- **Sarcophagi**
- **Statues** (Sekhmet, Anubis)
- **Mummy masks**
- **Amulets with gemstones**

### Asset Sources (Direct Links)

#### Sketchfab (CC Attribution)
```
https://sketchfab.com/3d-models/ankh-egyptian-cross-of-life-f77a365d70a4415998fa96007bc9a888
https://sketchfab.com/3d-models/eye-of-horus-072d6ad03cd740b9a46bf413e5c3ae15
https://sketchfab.com/3d-models/gems-21-different-shapes-1fb35b1aefae4d819dde9de90162602a
https://sketchfab.com/3d-models/ancient-egyptian-water-jar-53f768e11ed642ec8007cbadb0431f49
https://sketchfab.com/3d-models/low-poly-egyptian-jar-c4d68970fc984fc6ac56890bfac34565
https://sketchfab.com/3d-models/sci-fi-egyptian-canopic-jar-with-neon-lights-d6fcfce67bd54a169def0e2f63e2d403
```

#### CGTrader (Free Models)
```
https://www.cgtrader.com/3d-models/ankh
https://www.cgtrader.com/free-3d-models/eyeofhorus
https://www.cgtrader.com/3d-models/gem
https://www.cgtrader.com/3d-models/gemstone
https://www.cgtrader.com/3d-models/torch
```

#### Free3D
```
https://free3d.com/3d-models/pyramid
https://free3d.com/3d-models/obj-ankh
https://free3d.com/3d-models/jars
https://free3d.com/3d-models/torch
https://free3d.com/premium-3d-models/gemstones
```

### Integration Workflow

#### Backend (C# / F#)

1. **Asset Management Service** (`GA.Business.Core.Assets`)
   ```csharp
   public class AssetLibraryService
   {
       Task<AssetMetadata> ImportBlenderModel(string path);
       Task<byte[]> ConvertToGLB(string blendPath);
       Task<AssetMetadata> OptimizeForWebGPU(string glbPath);
       Task<List<AssetMetadata>> GetAssetsByCategory(AssetCategory category);
   }
   ```

2. **Asset Categories**
   ```csharp
   public enum AssetCategory
   {
       Architecture,    // Pyramids, pillars, obelisks
       AlchemyProps,    // Ankh, Eye of Horus, flasks
       Gems,            // Various gem cuts
       Jars,            // Canopic jars, vessels
       Torches,         // Light sources
       Artifacts,       // Scarabs, statues, masks
       Decorative       // General decoration
   }
   ```

3. **Asset Metadata**
   ```csharp
   public class AssetMetadata
   {
       public string Id { get; set; }
       public string Name { get; set; }
       public AssetCategory Category { get; set; }
       public string GlbPath { get; set; }
       public int PolyCount { get; set; }
       public string License { get; set; }
       public string Source { get; set; }
       public Dictionary<string, string> Tags { get; set; }
       public BoundingBox Bounds { get; set; }
   }
   ```

4. **MongoDB Storage**
   ```csharp
   public class AssetDocument
   {
       [BsonId]
       public ObjectId Id { get; set; }
       public AssetMetadata Metadata { get; set; }
       public byte[] GlbData { get; set; }  // Or GridFS reference
       public DateTime ImportedAt { get; set; }
       public string ImportedBy { get; set; }
   }
   ```

#### Frontend (React + Three.js + WebGPU)

1. **Asset Loader** (`Apps/ga-client/src/services/AssetLoader.ts`)
   ```typescript
   export class AssetLoader {
       async loadGLB(url: string): Promise<GLTF>;
       async loadAssetsByCategory(category: AssetCategory): Promise<Asset[]>;
       async preloadAssets(assetIds: string[]): Promise<void>;
       getCachedAsset(id: string): Asset | null;
   }
   ```

2. **BSP Scene Integration** (`Apps/ga-client/src/components/BSPDoomExplorer/`)
   ```typescript
   // Place assets in BSP rooms based on floor level
   export class AssetPlacer {
       placeInRoom(room: Room, asset: Asset, position: Vector3): void;
       distributeByFloor(floor: number, assets: Asset[]): void;
       createAlchemyLab(room: Room): void;  // Specialized room setup
   }
   ```

3. **Material Enhancement**
   ```typescript
   // Add emissive materials for gems and magical items
   export class MaterialEnhancer {
       addGlow(mesh: Mesh, color: Color, intensity: number): void;
       addReflection(mesh: Mesh, envMap: Texture): void;
       animateEmission(mesh: Mesh, frequency: number): void;
   }
   ```

### Optimization Guidelines

1. **Geometry Optimization**
   - Target: < 10k triangles per asset
   - Use decimation in Blender if needed
   - Merge static meshes where possible
   - Remove hidden faces

2. **Texture Optimization**
   - Use texture atlases to reduce draw calls
   - Max texture size: 2048x2048
   - Use compressed formats (KTX2, Basis)
   - PBR materials: albedo, normal, metallic, roughness

3. **Performance Targets**
   - 60 FPS on mid-range hardware
   - < 100 draw calls per frame
   - LOD (Level of Detail) for distant objects
   - Frustum culling via BSP tree

### Implementation Tasks

- [ ] Create `AssetLibraryService` in `GA.Business.Core`
- [ ] Add MongoDB collections for assets
- [ ] Create asset import CLI tool (`GaCLI asset import`)
- [ ] Download and process 15-20 core assets
- [ ] Create React `AssetLoader` service
- [ ] Integrate with BSP DOOM Explorer
- [ ] Add material enhancement shaders
- [ ] Implement LOD system
- [ ] Create asset browser UI component
- [ ] Add asset placement tools for level design

---

## Part 2: Grothendieck Monoid & Markov Chains for Fretboard Navigation

### Objective
Implement advanced music theory using Grothendieck monoids and Markov chains to:
- Organize the 4-level atonal hierarchy
- Discover common chord shapes and arpeggios
- Generate intelligent fretboard navigation
- Create heat maps for next position suggestions

### Theoretical Foundation

#### 1. Grothendieck Monoid Structure

**Monoid Elements**: Pitch-class multisets (mod 12)
- Operation: Multiset union (⊎)
- Identity: Empty set (∅)

**Grothendieck Group**: K₀(M)
- Allows subtraction: "What notes to add/remove to transform A → B?"
- Signed interval-content deltas

**Canonical Invariants**:
- ICV (Interval-Class Vector): φ: M → ℕ⁶
- Extended to G: φ: G → ℤ⁶
- Result: φ(B) - φ(A) = signed interval-content change

#### 2. Four-Level Hierarchy

1. **Cardinality Layer** - n-note families (dyads, triads, tetrads, etc.)
2. **Set-Class / ICV Layer** - Objects with same ICV/TI-class
3. **Mode/Scale Family Layer** - Orbits under rotations
4. **Fretboard Shapes Layer** - Playable grips per tuning/position

#### 3. Markov Chain on Hierarchy

**State Choices**:
- S₁: ICV classes (atonal, compact)
- S₂: (ICV, cardinality) pairs
- S₃: Scale/mode IDs
- S₄: Fretboard shape classes

**Transition Weights**:
- Harmonic proximity: ‖φ(Δ)‖₁
- Physical ease: finger travel on tuning
- Style priors: scale-family biases
- User feedback: reinforcement learning

### Backend Implementation (F# / C#)

#### 1. Core Types (`GA.Business.Core.Atonal`)

```fsharp
// F# types for algebraic structures
type PC = int  // 0..11
type PCSet = Set<PC>
type ICV = int * int * int * int * int * int  // Interval-class vector
type MonoidElement = {
    pcs: PCSet
    icv: ICV
    n: int
    id: string
}
type GrothendieckDelta = int * int * int * int * int * int  // Signed ICV delta

type Shape = {
    tuningId: string
    stringMask: int
    fretMask: int64
    fingers: int list
    span: int
    diagness: float  // 0 = box, 1 = diagonal
    ergonomics: float
    hash: string
}

type Transition = {
    fromId: string
    toId: string
    delta: GrothendieckDelta
    harmCost: float
    physCost: float
    score: float
}
```

#### 2. Grothendieck Operations

```fsharp
module Grothendieck =
    let computeICV (pcs: PCSet) : ICV =
        // Compute interval-class vector
        // ic1 = minor 2nd, ic2 = major 2nd, etc.
        ...

    let delta (a: ICV) (b: ICV) : GrothendieckDelta =
        // Signed difference
        let (a1,a2,a3,a4,a5,a6) = a
        let (b1,b2,b3,b4,b5,b6) = b
        (b1-a1, b2-a2, b3-a3, b4-a4, b5-a5, b6-a6)

    let l1Norm (d: GrothendieckDelta) : int =
        let (a,b,c,d,e,f) = d
        abs a + abs b + abs c + abs d + abs e + abs f

    let harmCost (d: GrothendieckDelta) : float =
        0.6 * float (l1Norm d)
```

#### 3. Shape Graph Builder

```fsharp
module ShapeGraph =
    type ShapeGraph = {
        nodes: Map<string, Shape>
        adj: Map<string, Transition list>
    }

    let buildGraph (tuning: Tuning) (scales: Scale list) : ShapeGraph =
        // 1. Generate all possible shapes for each scale
        // 2. Compute Grothendieck deltas between shapes
        // 3. Build adjacency list with costs
        ...

    let neighbors (g: ShapeGraph) (s: Shape) (k: int) : Transition list =
        g.adj.[s.hash]
        |> List.sortBy (fun t -> t.score)
        |> List.truncate k
```

#### 4. Markov Walker

```fsharp
module MarkovWalker =
    type WalkOptions = {
        steps: int
        boxPreference: bool option  // Some true = box, Some false = diagonal
        maxSpan: int
        maxShift: int
        temperature: float  // Exploration parameter
    }

    let softmax (tau: float) (transitions: Transition list) : (Transition * float) list =
        let exps = transitions |> List.map (fun t -> t, exp (t.score / tau))
        let z = exps |> List.sumBy snd
        exps |> List.map (fun (t, e) -> t, e / z)

    let sampleWalk (g: ShapeGraph) (start: Shape) (opts: WalkOptions) : Transition list =
        // Probabilistic walk through shape graph
        ...

    let generateHeatMap (g: ShapeGraph) (current: Shape) (opts: WalkOptions) : float[,] =
        // Generate probability heat map for next positions
        // Returns 6 strings × 24 frets grid
        ...
```

### Frontend Implementation (TypeScript + React)

#### 1. Grothendieck Service (`Apps/ga-client/src/services/GrothendieckService.ts`)

```typescript
export interface ICV {
    ic1: number;  // minor 2nd
    ic2: number;  // major 2nd
    ic3: number;  // minor 3rd
    ic4: number;  // major 3rd
    ic5: number;  // perfect 4th
    ic6: number;  // tritone
}

export interface GrothendieckDelta {
    delta: ICV;
    l1Norm: number;
    explanation: string;  // e.g., "+1 ic2, -1 ic5 → brighter color"
}

export class GrothendieckService {
    async computeICV(pitchClasses: number[]): Promise<ICV>;
    async computeDelta(from: ICV, to: ICV): Promise<GrothendieckDelta>;
    explainDelta(delta: GrothendieckDelta): string;
}
```

#### 2. Shape Navigator (`Apps/ga-client/src/services/ShapeNavigator.ts`)

```typescript
export interface Shape {
    id: string;
    tuningId: string;
    frets: number[];  // Per string
    pitchClasses: number[];
    icv: ICV;
    span: number;
    diagness: number;  // 0-1, box to diagonal
}

export interface NavigationOptions {
    boxPreference?: boolean;
    maxSpan?: number;
    maxShift?: number;
    temperature?: number;
}

export class ShapeNavigator {
    async getNextShapes(current: Shape, options: NavigationOptions): Promise<Shape[]>;
    async generateHeatMap(current: Shape, options: NavigationOptions): Promise<number[][]>;
    async generatePracticePath(start: Shape, steps: number, options: NavigationOptions): Promise<Shape[]>;
}
```

#### 3. Heat Map Visualizer (`Apps/ga-client/src/components/FretboardHeatMap.tsx`)

```typescript
interface FretboardHeatMapProps {
    heatMap: number[][];  // 6 strings × 24 frets
    currentShape?: Shape;
    onCellClick?: (string: number, fret: number) => void;
}

export const FretboardHeatMap: React.FC<FretboardHeatMapProps> = ({ heatMap, currentShape, onCellClick }) => {
    // Render fretboard with color-coded heat map
    // Hot zones = likely next positions
    // Cool zones = discouraged positions
    return (
        <div className="fretboard-heatmap">
            {/* Render with Three.js or SVG */}
        </div>
    );
};
```

### Implementation Tasks

#### Phase 1: Core Algebra (Backend)
- [ ] Implement `Grothendieck` module in F#
- [ ] Create `ShapeGraph` builder
- [ ] Add `MarkovWalker` with basic transitions
- [ ] Create unit tests for ICV computation
- [ ] Add MongoDB storage for shape graphs

#### Phase 2: Shape Discovery (Backend)
- [ ] Generate shapes for Standard tuning
- [ ] Generate shapes for Drop-D, DADGAD
- [ ] Implement shape canonicalization
- [ ] Mine common chord shapes (high centrality)
- [ ] Extract arpeggio patterns (k-shortest paths)
- [ ] Classify box vs diagonal shapes

#### Phase 3: Frontend Integration
- [ ] Create `GrothendieckService` TypeScript wrapper
- [ ] Implement `ShapeNavigator` service
- [ ] Build `FretboardHeatMap` component
- [ ] Add "Next Shapes" recommendation panel
- [ ] Create practice path generator UI
- [ ] Add shape library browser

#### Phase 4: Advanced Features
- [ ] Implement higher-order Markov (2-3 step memory)
- [ ] Add Hidden Markov Model for hand position
- [ ] Implement semi-Markov for phrasing
- [ ] Add personalization via bandit learning
- [ ] Create chaos-modulated exploration
- [ ] Build hierarchical/factorial chains

### API Endpoints

```csharp
// GaApi Controllers
[Route("api/grothendieck")]
public class GrothendieckController : ControllerBase
{
    [HttpPost("compute-icv")]
    Task<ICV> ComputeICV([FromBody] int[] pitchClasses);

    [HttpPost("compute-delta")]
    Task<GrothendieckDelta> ComputeDelta([FromBody] ICVPair pair);

    [HttpGet("shapes/{tuningId}")]
    Task<List<Shape>> GetShapes(string tuningId, [FromQuery] ShapeFilter filter);

    [HttpPost("next-shapes")]
    Task<List<Shape>> GetNextShapes([FromBody] NavigationRequest request);

    [HttpPost("heat-map")]
    Task<float[,]> GenerateHeatMap([FromBody] HeatMapRequest request);

    [HttpPost("practice-path")]
    Task<List<Shape>> GeneratePracticePath([FromBody] PracticePathRequest request);
}
```

### Success Metrics

1. **Shape Discovery**
   - Extract 20+ common chord shapes per quality
   - Identify 12+ arpeggio patterns per quality
   - Classify 10+ box and diagonal scale shapes

2. **Navigation Quality**
   - Average harmonic cost < 2.0 (small ICV changes)
   - Average physical cost < 3.0 (comfortable moves)
   - User acceptance rate > 70%

3. **Performance**
   - Heat map generation < 100ms
   - Next shapes recommendation < 50ms
   - Practice path generation < 200ms

### Documentation

- [ ] Write theory guide (Grothendieck monoids for musicians)
- [ ] Create API documentation
- [ ] Add UI tutorials
- [ ] Document shape classification system
- [ ] Create video demonstrations

---

## Integration Timeline

### Week 1-2: Asset Integration
- Download and process 3D models
- Create asset management service
- Integrate with BSP DOOM Explorer

### Week 3-4: Grothendieck Core
- Implement algebraic structures
- Build shape graph
- Create basic Markov walker

### Week 5-6: Shape Discovery
- Mine common shapes
- Extract patterns
- Build shape library

### Week 7-8: Frontend Integration
- Create TypeScript services
- Build UI components
- Add heat map visualization

### Week 9-10: Advanced Features
- Higher-order Markov
- Personalization
- Polish and optimization

## References

- ChatGPT conversation: "Free Blender models for BSP"
- ChatGPT conversation: "Grothendieck monoid guitar project"
- Harmonious App: https://harmoniousapp.net/p/ec/Equivalence-Groups
- Ian Ring's Scale Website: https://ianring.com/musictheory/scales/

