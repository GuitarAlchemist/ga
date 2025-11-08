# Guitar Alchemist 3D Logo Components

This directory contains interactive 3D logo components built with React Three Fiber (R3F) and Three.js.

## Components

### GuitarAlchemistLogo3D (Complex Version)
**File:** `GuitarAlchemistLogo3D.tsx`

Features:
- **Guitar Body Shape**: Custom extruded guitar body with distortion material
- **Alchemist Symbol**: Golden ratio spiral using tube geometry
- **Floating Elements**: Geometric shapes representing musical notes
- **Torus Rings**: Musical staff line effect
- **Auto-rotation**: Smooth automatic rotation with orbit controls
- **Interactive**: Drag to rotate, scroll to zoom

Technical Details:
- Uses `MeshDistortMaterial` for organic movement
- Golden ratio spiral (φ = 1.618) for alchemist symbolism
- Custom shape extrusion for guitar body
- Multiple point lights and spotlights
- Sparkles particle effect

### GuitarAlchemistLogoSimple (Simple Version)
**File:** `GuitarAlchemistLogoSimple.tsx`

Features:
- **Guitar Pick**: Rotating triangular cone shape
- **String Spheres**: 6 orbiting colored spheres (representing guitar strings)
- **Alchemy Core**: Wireframe octahedron at center
- **Float Animation**: Smooth floating motion using `@react-three/drei`
- **Color Spectrum**: HSL rainbow colors for each string
- **Auto-rotation**: Continuous rotation with orbit controls

Technical Details:
- Simplified geometry for better performance
- Uses `Float` component from drei for animations
- Color-coded spheres (HSL spectrum)
- Wireframe golden octahedron core
- Sparkles effect with custom parameters

## Technology Stack

- **React Three Fiber**: React renderer for Three.js
- **@react-three/drei**: Helper components (OrbitControls, Float, Sparkles, etc.)
- **Three.js**: 3D graphics library
- **TypeScript**: Type-safe development

## WebGPU Support

While React Three Fiber primarily uses WebGL, it's built on Three.js which has experimental WebGPU support. The components are designed with modern rendering in mind:

- High-performance rendering settings
- `powerPreference: 'high-performance'` for GPU selection
- Antialiasing enabled
- Alpha blending support

## Usage

```tsx
import GuitarAlchemistLogo3D from './components/GuitarAlchemistLogo3D';
import GuitarAlchemistLogoSimple from './components/GuitarAlchemistLogoSimple';

// Complex version
<GuitarAlchemistLogo3D />

// Simple version (better performance)
<GuitarAlchemistLogoSimple />
```

## Performance Considerations

The components are optimized for performance:
- Uses `useMemo` for expensive geometry calculations
- Efficient animation loops with `useFrame`
- `powerPreference: 'high-performance'` for GPU utilization
- Reasonable particle counts for sparkles

## Customization

Both components can be customized by modifying:
- Colors (material color, emissive, etc.)
- Rotation speeds (in `useFrame` callbacks)
- Geometry parameters (sizes, segments)
- Lighting (intensity, position, color)
- Animation speeds (Float component props)

## Browser Compatibility

- Chrome/Edge: Full support
- Firefox: Full support
- Safari: Full support (WebGL 2.0)
- Mobile: Supported (with reduced performance)

## Future Enhancements

Potential improvements:
- Add WebGPU renderer when stable
- Post-processing effects (bloom, etc.)
- More complex guitar geometry
- Animated chord progressions
- Sound-reactive animations
- VR/AR support with WebXR

## Credits

Created for Guitar Alchemist - A music theory and guitar visualization tool.
