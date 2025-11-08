# Three.js BSP Loader

A pragmatic BSP-style scene loader for Three.js with cells/portals and BVH acceleration, designed for WebGPU (with WebGL fallback).

## Features

- **Portal-based visibility culling** - Only render cells visible through portals
- **BVH acceleration** - Fast raycasting and collision detection using three-mesh-bvh
- **Capsule collision system** - Smooth player movement with collision resolution
- **WebGPU support** - Modern rendering with automatic WebGL fallback
- **glTF compatibility** - Load levels from standard glTF files with metadata
- **Procedural sample level** - Built-in test level for immediate testing

## Architecture

### Core Components

1. **LevelLoader.ts** - Parses glTF files and extracts cells/portals from `extras` metadata
2. **PortalCulling.ts** - Implements frustum culling through portals for BSP-like visibility
3. **CapsuleCollision.ts** - Handles player movement and collision resolution using BVH
4. **SampleLevel.ts** - Generates a procedural test level (hall + kitchen + portal)

### Data Structure

```typescript
type Portal = {
  from: string;           // Source cell ID
  to: string;             // Target cell ID  
  quad: THREE.Vector3[];  // 4 world-space points defining portal
  plane: THREE.Plane;     // Portal plane for culling
};

type Cell = {
  id: string;             // Unique cell identifier
  meshes: THREE.Mesh[];   // Geometry in this cell
  portals: Portal[];      // Portals leading from this cell
  aabb: THREE.Box3;       // Bounding box for frustum culling
};
```

## Quick Start

1. **Install dependencies:**
   ```bash
   npm install
   ```

2. **Run development server:**
   ```bash
   npm run dev
   ```

3. **Open browser** and navigate to `http://localhost:3000`

## Controls

- **WASD / Arrow Keys** - Move around
- **Mouse** - Look around (click to lock pointer)
- **Space** - Jump
- **C** - Toggle cell debug visualization
- **P** - Toggle portal debug visualization

## Creating Levels in Blender

### For Cells (Rooms):
1. Create an Empty or parent object
2. Add Custom Properties:
   - `type = "cell"`
   - `cellId = "room_name"`
3. Parent all room geometry to this object

### For Portals (Doors/Windows):
1. Create a thin quad mesh
2. Orient normal towards the target cell
3. Add Custom Properties:
   - `type = "portal"`
   - `from = "source_room"`
   - `to = "target_room"`

### Export:
- Export as glTF 2.0 (.glb recommended)
- Custom Properties will be saved in `node.extras`

## Usage Example

```typescript
import { CellsPortalsLevelLoader } from './LevelLoader';
import { collectVisibleCells } from './PortalCulling';

// Load from glTF
const loader = new CellsPortalsLevelLoader();
const level = await loader.load('/path/to/level.glb');
scene.add(level.sceneRoot);

// In render loop
const visibleCells = collectVisibleCells(level, camera);
for (const [id, cell] of level.cells) {
  const show = visibleCells.has(cell);
  for (const mesh of cell.meshes) {
    mesh.visible = show;
  }
}
```

## Performance Features

- **BVH per mesh** - Accelerated raycasting and collision detection
- **Portal frustum clipping** - Tight culling reduces overdraw
- **Merged geometry** - Optional mesh merging per cell to reduce draw calls
- **WebGPU optimization** - Modern GPU pipeline with efficient batching

## Technical Details

### Portal Culling Algorithm
1. Locate camera's current cell using AABB tests
2. Start with camera frustum
3. For each portal in current cell:
   - Clip frustum to portal quad
   - Recursively traverse to connected cell
   - Continue until no more visible portals

### Collision System
- Uses `three-mesh-bvh` for fast triangle queries
- Capsule vs triangle resolution with penetration correction
- Supports gravity, ground detection, and sliding along surfaces

### WebGPU Integration
- Automatic detection and fallback to WebGL
- Optimized for modern GPU architectures
- Efficient pipeline compilation and caching

## File Structure

```
src/
├── LevelLoader.ts      # glTF parsing and cell/portal extraction
├── PortalCulling.ts    # Frustum culling through portals
├── CapsuleCollision.ts # Player physics and collision
├── SampleLevel.ts      # Procedural test level generator
└── main.ts            # Demo application with WebGPU renderer
```

## License

MIT License - See LICENSE file for details.

## Credits

Based on the pragmatic BSP approach outlined in the ChatGPT conversation, combining modern Three.js features with classic BSP visibility techniques.
