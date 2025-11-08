# BSP DOOM Explorer 🎮

A DOOM-like first-person explorer for navigating Binary Space Partitioning (BSP) tree structures in 3D space. Experience the BSP tree as an interactive 3D environment where partition planes become walls and tonal regions become rooms.

## Overview

The BSP DOOM Explorer transforms the abstract concept of Binary Space Partitioning into a tangible, navigable 3D world inspired by classic DOOM gameplay. Walk through the BSP tree structure, cross partition planes, and explore different tonal regions in first-person view.

## Features

### 🎮 First-Person Navigation
- **WASD Movement** - Classic FPS controls for forward/backward/strafe
- **Mouse Look** - Full 360° camera rotation with pointer lock
- **Vertical Movement** - Space to ascend, Shift to descend
- **Smooth Controls** - Configurable movement and look sensitivity

### 🌳 BSP Visualization
- **Partition Planes as Walls** - BSP split planes rendered as semi-transparent walls
- **Tonal Regions as Rooms** - Different tonality types shown as colored spatial regions
- **Real-time Traversal** - Navigate through the tree structure dynamically
- **Visual Hierarchy** - Depth and structure visible through spatial layout

### 🎨 Visual Design
- **DOOM-Inspired Aesthetic** - Retro green HUD on black background
- **Color-Coded Regions** - Each tonality type has a distinct color:
  - Major: Green (stable/safe)
  - Minor: Blue (melancholic)
  - Modal: Magenta (exotic)
  - Atonal: Red (chaotic)
  - Chromatic: Yellow (transitional)
  - And more...
- **Distance Fog** - Atmospheric depth rendering
- **Emissive Materials** - Glowing partition planes and region boundaries

### 🖥️ HUD & Interface
- **Real-time Stats** - FPS counter, renderer type (WebGPU/WebGL)
- **Current Region Display** - Shows active tonal region information
- **BSP Tree Info** - Total regions, tree depth, partition strategies
- **Controls Guide** - On-screen control reference
- **Minimap** - Top-down view showing player position

### ⚡ Performance
- **WebGPU Rendering** - Hardware-accelerated graphics (with WebGL fallback)
- **Efficient Collision** - BSP-based spatial queries for collision detection
- **Optimized Geometry** - Minimal draw calls for smooth performance
- **60+ FPS Target** - Smooth gameplay experience

## Usage

### Basic Usage

```tsx
import { BSPDoomExplorer } from './components/BSP';

function App() {
  return (
    <BSPDoomExplorer
      width={1200}
      height={800}
      showHUD={true}
      showMinimap={true}
    />
  );
}
```

### Advanced Configuration

```tsx
import { BSPDoomExplorer } from './components/BSP';
import type { BSPRegion } from './components/BSP/BSPApiService';

function App() {
  const handleRegionChange = (region: BSPRegion) => {
    console.log('Entered region:', region.name);
    console.log('Tonality:', region.tonalityType);
  };

  return (
    <BSPDoomExplorer
      width={1920}
      height={1080}
      moveSpeed={8.0}        // Faster movement
      lookSpeed={0.003}      // More sensitive mouse
      showHUD={true}
      showMinimap={true}
      onRegionChange={handleRegionChange}
    />
  );
}
```

## Props

| Prop | Type | Default | Description |
|------|------|---------|-------------|
| `width` | `number` | `1200` | Canvas width in pixels |
| `height` | `number` | `800` | Canvas height in pixels |
| `moveSpeed` | `number` | `5.0` | Movement speed (units per second) |
| `lookSpeed` | `number` | `0.002` | Mouse sensitivity for camera rotation |
| `showHUD` | `boolean` | `true` | Show heads-up display |
| `showMinimap` | `boolean` | `true` | Show minimap overlay |
| `onRegionChange` | `(region: BSPRegion) => void` | - | Callback when entering new region |

## Controls

### Keyboard
- **W** - Move forward
- **S** - Move backward
- **A** - Strafe left
- **D** - Strafe right
- **Space** - Move up
- **Shift** - Move down
- **ESC** - Release pointer lock

### Mouse
- **Click** - Lock pointer for mouse look
- **Move** - Look around (when pointer locked)

## Architecture

### Component Structure

```
BSPDoomExplorer
├── Three.js Scene
│   ├── WebGPU/WebGL Renderer
│   ├── First-Person Camera
│   └── BSP World Group
│       ├── Ground Plane
│       ├── Partition Planes (walls)
│       └── Region Volumes (rooms)
├── Input System
│   ├── Keyboard Handler (WASD)
│   ├── Mouse Handler (Pointer Lock)
│   └── Movement Controller
├── HUD Overlay
│   ├── Stats Display
│   ├── Region Info
│   └── Controls Guide
└── Minimap
    └── Top-down View
```

### BSP Integration

The explorer integrates with the existing BSP API:

1. **Fetches BSP Tree Info** - Gets tree structure from backend
2. **Builds 3D Geometry** - Converts BSP nodes to 3D meshes
3. **Spatial Queries** - Uses BSP for collision detection
4. **Region Detection** - Raycasting to determine current region

## Technical Details

### Rendering
- **Engine**: Three.js r163+
- **Renderer**: WebGPU (with WebGL fallback)
- **Materials**: MeshStandardMaterial with PBR
- **Lighting**: Ambient + Directional (DOOM-style sector lighting)
- **Post-processing**: Distance fog for atmosphere

### Physics
- **Movement**: Velocity-based with delta time
- **Collision**: AABB bounds checking
- **Gravity**: Optional (disabled by default for free flight)

### Performance Optimizations
- **Frustum Culling**: Automatic via Three.js
- **LOD**: Future enhancement for large BSP trees
- **Instancing**: Potential for repeated geometry
- **Occlusion**: BSP tree naturally provides occlusion culling

## Demo Page

Access the test page at: `/test/bsp-doom-explorer`

The demo page includes:
- Full-screen explorer view
- Side panel with settings
- Real-time region tracking
- Region history log
- Adjustable movement/look speeds
- Toggle HUD and minimap

## Future Enhancements

### Planned Features
- [ ] **Sound Integration** - Play chords/scales when entering regions
- [ ] **Procedural BSP Generation** - Generate random BSP structures
- [ ] **Multiplayer** - Navigate BSP trees with other users
- [ ] **VR Support** - WebXR integration for immersive exploration
- [ ] **Advanced Collision** - Precise BSP-based collision detection
- [ ] **Particle Effects** - Visual feedback for region transitions
- [ ] **Texture Mapping** - Detailed textures on partition planes
- [ ] **Dynamic Lighting** - Per-region lighting schemes

### Potential Improvements
- **Performance**: Implement LOD for large trees
- **Visuals**: Add post-processing effects (bloom, chromatic aberration)
- **Interaction**: Click on regions to query musical information
- **Navigation**: Breadcrumb trail showing path through tree
- **Analytics**: Track exploration patterns and statistics

## Integration with BSP System

The DOOM Explorer complements the existing BSP components:

- **BSPInterface** - High-level API interaction
- **BSPTreeVisualization** - 2D tree diagram
- **BSPSpatialVisualization** - 3D scatter plot
- **HarmonicNavigator3D** - Tetrahedral cell navigation
- **BSPDoomExplorer** - First-person immersive exploration ⭐

Each component provides a different perspective on the same BSP data structure.

## Browser Compatibility

- **WebGPU**: Chrome 113+, Edge 113+ (recommended)
- **WebGL**: All modern browsers (fallback)
- **Pointer Lock**: All modern browsers
- **Performance**: Best on desktop with dedicated GPU

## Development

### Running Locally

```bash
cd ReactComponents/ga-react-components
npm install
npm run dev
```

Navigate to: `http://localhost:5173/test/bsp-doom-explorer`

### Building

```bash
npm run build
```

## Credits

Inspired by:
- **DOOM** (id Software, 1993) - BSP rendering and FPS gameplay
- **Quake** (id Software, 1996) - 3D BSP tree traversal
- **Three.js** - 3D rendering library
- **Guitar Alchemist** - Musical BSP tree implementation

## License

Part of the Guitar Alchemist project.

---

**Ready to explore the BSP tree like never before? 🎮🌳**

