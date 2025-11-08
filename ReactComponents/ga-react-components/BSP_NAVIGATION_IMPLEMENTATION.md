# BSP Navigation Implementation - ThreeHarmonicNavigator

## Overview

This document describes the implementation of the **ThreeHarmonicNavigator** component, a 3D visualization for Binary Space Partitioning (BSP) navigation in the Guitar Alchemist fretboard visualization system.

## Implementation Summary

### What Was Implemented

1. **ThreeHarmonicNavigator Component** (`src/components/BSP/ThreeHarmonicNavigator.tsx`)
   - 3D visualization using Three.js with WebGL rendering
   - Interactive BSP room navigation with pyramid/frustum geometries
   - Chord nodes rendered as small pyramids in 3D space
   - Plücker line transitions between chords
   - Quaternion-based modulation for smooth rotations
   - Integration with existing BSP API data structures

2. **BSPInterface Integration** (`src/components/BSP/BSPInterface.tsx`)
   - Added visualization mode toggle (2D/3D)
   - Integrated ThreeHarmonicNavigator as an alternative to BSPSpatialVisualization
   - Seamless switching between 2D canvas and 3D WebGL rendering

3. **Component Exports** (`src/components/BSP/index.ts`)
   - Exported ThreeHarmonicNavigator for use throughout the application

## Technical Details

### Key Features

#### 1. **Pitch Class to 3D Mapping**
- Converts pitch classes (0-11) to 3D torus coordinates
- Uses circular mapping: `theta = (pc / 12) * 2π`
- Z-axis represents consonance/fret cost

#### 2. **Chord Centroid Calculation**
- Computes the geometric center of chord pitch classes in 3D space
- Normalizes and scales for visual separation
- Varies cost parameter for depth positioning

#### 3. **BSP Room Visualization**
- Pyramid/frustum geometries represent tonal regions
- Semi-transparent materials (opacity 0.14-0.18)
- Hierarchical structure with parent/child rooms

#### 4. **Interactive Features**
- OrbitControls for camera manipulation
- Raycasting for click detection
- Quaternion modulation triggered by room clicks
- Smooth SLERP interpolation for rotations

#### 5. **Data Integration**
- Accepts `BSPSpatialQueryResponse` from the API
- Converts `BSPElement` to `ChordPoint` for rendering
- Falls back to default chords (C, G, Am) when no data available

### Component Props

```typescript
interface ThreeHarmonicNavigatorProps {
  spatialResult?: BSPSpatialQueryResponse | null;
  width?: number;  // Default: 800
  height?: number; // Default: 600
}
```

### Architecture

```
BSPInterface (Main UI)
├── Visualization Mode Toggle (2D/3D)
├── BSPSpatialVisualization (2D Canvas)
└── ThreeHarmonicNavigator (3D WebGL)
    ├── Scene Setup
    │   ├── Camera (PerspectiveCamera)
    │   ├── Lights (Ambient + Directional)
    │   └── Renderer (WebGLRenderer)
    ├── BSP Rooms (Cone Geometries)
    │   ├── Tonic Room (Primary)
    │   └── Dominant Room (Secondary)
    ├── Chord Nodes (Small Pyramids)
    │   ├── Position from pitch class centroid
    │   └── Color: 0x66ffd2 (cyan-green)
    └── Transitions (Lines)
        └── Connect consecutive chords
```

## Usage

### Accessing the Component

1. **Via BSP Test Page**: Navigate to `http://localhost:5173/test/bsp`
2. **Perform a Spatial Query**: Enter pitch classes (e.g., "C,E,G") and click "Perform Spatial Query"
3. **Toggle Visualization Mode**: Click the "3D" chip to switch to ThreeHarmonicNavigator
4. **Interact**: 
   - Drag to rotate the camera
   - Scroll to zoom
   - Click on rooms to trigger quaternion modulation

### Integration Example

```typescript
import { ThreeHarmonicNavigator } from '../components/BSP';

// In your component
<ThreeHarmonicNavigator
  spatialResult={spatialQueryResult}
  width={800}
  height={600}
/>
```

## File Structure

```
ReactComponents/ga-react-components/
├── src/
│   ├── components/
│   │   └── BSP/
│   │       ├── ThreeHarmonicNavigator.tsx  (NEW)
│   │       ├── BSPInterface.tsx            (MODIFIED)
│   │       ├── BSPApiService.ts
│   │       ├── BSPSpatialVisualization.tsx
│   │       └── index.ts                    (MODIFIED)
│   └── pages/
│       └── BSPTest.tsx                     (Existing test page)
└── tests/
    └── bsp.spec.ts                         (Existing Playwright tests)
```

## Dependencies

- **three** (v0.180.0): Core 3D rendering library
- **@types/three** (v0.180.0): TypeScript type definitions
- **OrbitControls**: Camera control from three/examples/jsm

## Future Enhancements

### Recommended Improvements

1. **WebGPU Support**
   - Upgrade to WebGPURenderer when browser support improves
   - Use NodeMaterial for shader-based effects

2. **Data Wiring**
   - Map tunings from Tunings.toml to pitch axes
   - Compute left-hand and right-hand costs from chord templates
   - Implement BSP splits based on scalar features:
     - `tonalEnergy`: Distance to tonic triad on Tonnetz
     - `chromaticLoad`: Number of non-diatonic pitch classes
     - `ergonomicCost`: Combined left/right hand cost

3. **Quaternion Assignment**
   - Assign quaternions per key/mode (e.g., `q_modeKey`)
   - Implement SLERP for mode changes (Ionian→Lydian, C→G)
   - Derive axes from circle-of-fifths (yaw) and modal brightness (pitch)

4. **Plücker Analytics**
   - Store (L, M) for each transition
   - Score transitions by:
     - Angle between L and consonance axis
     - Length (fret movement)
     - BSP plane crossing count

5. **Visual Enhancements**
   - Room portals with transparent planes
   - Mini-map with 2D Tonnetz inset
   - Haptic rhythm with beat grid synchronization
   - InstancedMesh for performance optimization

6. **User Experience**
   - Tooltips showing chord names on hover
   - Animation easing (expoOut) to prevent motion sickness
   - Customizable color schemes
   - Export 3D scene as image/video

## Testing

### Manual Testing
1. Start the dev server: `npm run dev`
2. Navigate to `http://localhost:5173/test/bsp`
3. Verify:
   - 2D/3D toggle works
   - 3D scene renders correctly
   - Camera controls respond
   - Chord data from API displays properly

### Automated Testing
- Existing Playwright tests in `tests/bsp.spec.ts` cover the BSP interface
- Tests verify visualization rendering and mode switching
- Run tests: `npm run test`

## Performance Considerations

- **WebGL Rendering**: Hardware-accelerated, suitable for real-time interaction
- **Geometry Batching**: Consider InstancedMesh for large chord sets
- **Memory Management**: Proper cleanup in useEffect to prevent leaks
- **Animation Loop**: Uses requestAnimationFrame for smooth 60fps rendering

## Known Issues

1. **Linting Warnings**: Some pre-existing ESLint warnings in other BSP components (not related to ThreeHarmonicNavigator)
2. **WebGPU**: Currently uses WebGL; WebGPU support is experimental in Three.js
3. **Mobile Performance**: May require optimization for lower-end devices

## References

- [Three.js Documentation](https://threejs.org/docs/)
- [BSP Algorithm Overview](../../../Apps/BSPDemo/README.md)
- [Guitar Alchemist API Documentation](../../../Apps/ga-server/GaApi/README.md)
- [ChatGPT Conversation](https://chatgpt.com/g/g-p-67c68e5430248191845c1493f1894d1b-guitar-alchemist/shared/c/68faf6a2-98c0-8329-8e33-daa5920b6fd0)

## Contributors

- Implementation based on design from ChatGPT conversation
- Integrated with existing Guitar Alchemist BSP infrastructure
- Uses established patterns from BSPSpatialVisualization component

---

**Status**: ✅ Complete and functional
**Last Updated**: 2025-10-24
**Version**: 1.0.0

