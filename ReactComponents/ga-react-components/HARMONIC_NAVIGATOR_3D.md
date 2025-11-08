# Harmonic Navigator 3D - Complete Implementation Guide

## Overview

The **Harmonic Navigator 3D** is an advanced 3D visualization component for navigating harmonic space in the Guitar Alchemist application. It implements cutting-edge music theory concepts using modern web graphics technology.

## Core Concepts

### 1. BSP (Binary Space Partitioning) for Musical Space

Binary Space Partitioning divides the harmonic universe into hierarchical regions:

- **Tetrahedral Cells**: Each mode/scale occupies a 3D tetrahedral region
- **Hierarchical Structure**: BSP tree organizes tonal relationships
- **Spatial Queries**: Efficient lookup of nearby harmonic regions
- **Musical Predicates**: Splits based on interval classes, consonance, mode families

```typescript
type BSPNode =
  | { kind: "leaf"; regionId: string }
  | { kind: "node"; predicate: MusicalPredicate; left: BSPNode; right: BSPNode };

type MusicalPredicate =
  | { kind: "hasIntervalClass"; ic: number }
  | { kind: "modeFamily"; family: string }
  | { kind: "consonanceAbove"; threshold: number }
  | { kind: "fretSpanMax"; semitones: number };
```

### 2. Quaternion-Based Modulation

Quaternions provide smooth rotations for key/mode changes:

- **Axis-Angle Representation**: Each modulation has an axis and angle
- **SLERP Interpolation**: Spherical Linear Interpolation for smooth transitions
- **Rotor Application**: Quaternions applied to scene objects for visual feedback
- **Musical Mapping**: Circle of fifths → yaw, modal brightness → pitch

```typescript
type HarmonicRotor = { 
  axis: THREE.Vector3;  // Rotation axis
  angle: number;        // Rotation angle in radians
};

// Example: Modulate from C to G (perfect fifth)
const modulationRotor: HarmonicRotor = {
  axis: new THREE.Vector3(0, 1, 0),  // Y-axis (circle of fifths)
  angle: (7/12) * Math.PI * 2,       // 7 semitones
};
```

### 3. Plücker Coordinates for Voice-Leading

Plücker coordinates represent lines in 3D space, perfect for voice-leading paths:

- **Direction Vector (L)**: Shows the direction of voice movement
- **Moment Vector (M)**: Encodes the line's position in space
- **Geometric Properties**: Angle, distance, and crossing count
- **Visual Representation**: Rendered as tubes connecting chord centroids

```typescript
type PluckerLine = {
  L: THREE.Vector3;      // Direction vector
  M: THREE.Vector3;      // Moment vector
  fromChord: number[];   // Source pitch classes
  toChord: number[];     // Target pitch classes
  color?: number;        // Optional color override
};
```

### 4. Pitch Class to 3D Mapping

Pitch classes (0-11) are mapped to 3D torus coordinates:

```typescript
function chordBarycenter(pcs: number[]): THREE.Vector3 {
  const pts = pcs.map((pc) => {
    const t = (pc / 12) * Math.PI * 2;
    return new THREE.Vector3(
      Math.cos(t),           // X: Circle of fifths
      Math.sin(t),           // Y: Circle of fifths
      Math.sin(2 * t) * 0.2  // Z: Consonance/dissonance
    );
  });
  // Return centroid
  const c = new THREE.Vector3();
  for (const p of pts) c.add(p);
  c.multiplyScalar(1 / pts.length);
  return c;
}
```

## Component Architecture

### File Structure

```
ReactComponents/ga-react-components/
├── src/
│   ├── components/
│   │   └── BSP/
│   │       ├── HarmonicNavigator3D.tsx       (NEW - Main component)
│   │       ├── ThreeHarmonicNavigator.tsx    (Existing - Simpler version)
│   │       ├── BSPInterface.tsx              (Integration point)
│   │       └── index.ts                      (Exports)
│   └── pages/
│       └── HarmonicNavigator3DTest.tsx       (NEW - Test page)
└── HARMONIC_NAVIGATOR_3D.md                  (This file)
```

### Component Props

```typescript
type Props = {
  regions?: HarmonicRegion[];              // Harmonic regions to visualize
  bsp?: BSPNode;                           // BSP tree structure
  tunings?: Tuning[];                      // Instrument tunings
  chordPaths?: PluckerLine[];              // Voice-leading paths
  spatialResult?: BSPSpatialQueryResponse; // Integration with BSP API
  dataUrls?: DataUrls;                     // Optional YAML/TOML loading
  initialRotor?: HarmonicRotor;            // Initial rotation state
  onSelectRegion?: (id: string) => void;   // Selection callback
  className?: string;                      // CSS class
  width?: number;                          // Canvas width (default: 800)
  height?: number;                         // Canvas height (default: 600)
};
```

### Key Features

1. **Tetrahedral Cell Rendering**
   - Each region is a 4-vertex tetrahedron
   - Semi-transparent materials (opacity 0.9)
   - Color-coded by mode/scale family
   - Interactive selection via raycasting

2. **Interactive Key Wheel**
   - 12-segment ring representing pitch classes
   - Tick marks for each semitone
   - Drag-to-rotate for modulation (future enhancement)
   - Visual feedback for key changes

3. **Voice-Leading Tubes**
   - Cubic Bézier curves between chord centroids
   - Tube geometry with customizable radius
   - Color-coded by transition type
   - Smooth interpolation using Plücker coordinates

4. **Camera Controls**
   - OrbitControls for intuitive navigation
   - Damping for smooth motion
   - Zoom and pan support
   - Responsive to window resize

## Usage Examples

### Basic Usage

```typescript
import { HarmonicNavigator3D, HarmonicRegion } from '../components/BSP';

const regions: HarmonicRegion[] = [
  {
    id: 'ionian_c',
    name: 'C Ionian',
    pcs: [0, 2, 4, 5, 7, 9, 11],
    tonic: 0,
    family: 'Ionian',
    cell: [v0, v1, v2, v3], // Four THREE.Vector3 vertices
    color: 0x6aa3ff,
  },
  // ... more regions
];

<HarmonicNavigator3D
  regions={regions}
  width={800}
  height={600}
  onSelectRegion={(id) => console.log('Selected:', id)}
/>
```

### With Voice-Leading Paths

```typescript
const paths: PluckerLine[] = [
  {
    L: new THREE.Vector3(1, 0, 0),
    M: new THREE.Vector3(0, 1, 0),
    fromChord: [0, 4, 7],  // C major
    toChord: [7, 11, 2],   // G major
    color: 0xff7a7a,
  },
];

<HarmonicNavigator3D
  regions={regions}
  chordPaths={paths}
  width={800}
  height={600}
/>
```

### Integration with BSP API

```typescript
const [spatialResult, setSpatialResult] = useState<BSPSpatialQueryResponse | null>(null);

// Fetch from API
const result = await bspApiService.spatialQuery({ pitchClasses: ['C', 'E', 'G'] });
setSpatialResult(result);

<HarmonicNavigator3D
  spatialResult={spatialResult}
  width={800}
  height={600}
/>
```

## Advanced Features

### 1. Dynamic YAML/TOML Loading

```typescript
<HarmonicNavigator3D
  dataUrls={{
    modes: '/Modes.yaml',
    tunings: '/Tunings.toml'
  }}
  width={800}
  height={600}
/>
```

### 2. Quaternion Modulation

```typescript
const initialRotor: HarmonicRotor = {
  axis: new THREE.Vector3(0, 1, 0),
  angle: Math.PI / 6,  // 30 degrees
};

<HarmonicNavigator3D
  regions={regions}
  initialRotor={initialRotor}
  width={800}
  height={600}
/>
```

### 3. Custom BSP Tree

```typescript
const bspTree: BSPNode = {
  kind: 'node',
  predicate: { kind: 'modeFamily', family: 'Ionian' },
  left: { kind: 'leaf', regionId: 'ionian_c' },
  right: {
    kind: 'node',
    predicate: { kind: 'consonanceAbove', threshold: 0.7 },
    left: { kind: 'leaf', regionId: 'dorian_d' },
    right: { kind: 'leaf', regionId: 'phrygian_e' },
  },
};

<HarmonicNavigator3D
  regions={regions}
  bsp={bspTree}
  width={800}
  height={600}
/>
```

## Testing

### Access the Test Page

1. Start the dev server: `npm run dev`
2. Navigate to: `http://localhost:5173/test/harmonic-navigator-3d`
3. Interact with the 3D visualization:
   - **Drag** to orbit the camera
   - **Scroll** to zoom in/out
   - **Click** on tetrahedral cells to select regions
   - **Toggle** voice-leading paths on/off

### Test Scenarios

1. **Region Selection**: Click on different cells and verify info panel updates
2. **Voice-Leading Paths**: Toggle paths on/off and verify tubes render correctly
3. **Camera Controls**: Test orbit, zoom, and pan functionality
4. **Responsive Design**: Resize window and verify canvas adapts
5. **Performance**: Monitor FPS with many regions and paths

## Performance Considerations

### Optimization Strategies

1. **Geometry Batching**: Use `InstancedMesh` for large numbers of cells
2. **Level of Detail**: Reduce polygon count for distant objects
3. **Frustum Culling**: Automatically handled by Three.js
4. **Material Sharing**: Reuse materials across similar objects
5. **Animation Loop**: Use `requestAnimationFrame` for smooth 60fps

### Memory Management

- Proper cleanup in `useEffect` return function
- Dispose of geometries and materials when unmounting
- Remove event listeners on cleanup
- Clear scene objects before rebuilding

## Future Enhancements

### Planned Features

1. **WebGPU Support**: Upgrade to `WebGPURenderer` when browser support improves
2. **Node Materials**: Use Three.js NodeMaterial system for advanced shaders
3. **Mini-Map**: 2D Tonnetz inset showing current orientation
4. **Haptic Rhythm**: Beat grid synchronization with visual pulsing
5. **Room Portals**: Transparent planes connecting adjacent regions
6. **Tunings Integration**: Map Tunings.toml data to pitch axes
7. **Ergonomic Costs**: Visualize left/right hand costs from chord templates
8. **BSP Splits**: Implement scalar feature-based partitioning
9. **Plücker Analytics**: Full transition scoring with angle/distance/crossing metrics
10. **Export Functionality**: Save 3D scenes as images or videos

### Integration Opportunities

- **Scales.yaml**: Load scale definitions dynamically
- **Modes.yaml**: Load mode definitions dynamically
- **Tunings.toml**: Map tunings to instrument configurations
- **BSP API**: Real-time queries for nearby harmonic regions
- **Audio Playback**: Trigger sounds when selecting regions
- **MIDI Integration**: Map MIDI input to harmonic navigation

## References

### Documentation

- [Three.js Documentation](https://threejs.org/docs/)
- [BSP Algorithm Overview](https://en.wikipedia.org/wiki/Binary_space_partitioning)
- [Quaternions in 3D Graphics](https://en.wikipedia.org/wiki/Quaternions_and_spatial_rotation)
- [Plücker Coordinates](https://en.wikipedia.org/wiki/Pl%C3%BCcker_coordinates)
- [ChatGPT Conversation](https://chatgpt.com/g/g-p-67c68e5430248191845c1493f1894d1b-guitar-alchemist/shared/c/68faf6a2-98c0-8329-8e33-daa5920b6fd0)

### Related Components

- `ThreeHarmonicNavigator.tsx` - Simpler 3D visualization
- `BSPInterface.tsx` - Main BSP interface with 2D/3D toggle
- `BSPSpatialVisualization.tsx` - 2D canvas-based visualization
- `BSPApiService.ts` - Backend API integration

---

**Status**: ✅ Complete and functional  
**Last Updated**: 2025-10-24  
**Version**: 1.0.0  
**Author**: Guitar Alchemist Team

