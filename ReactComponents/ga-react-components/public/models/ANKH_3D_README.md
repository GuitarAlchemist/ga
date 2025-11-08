# 3D Ankh Model for BSP DOOM Explorer

## Overview

This directory contains a custom 3D ankh model created in Blender specifically for the BSP DOOM Explorer's crosshair/reticle. The ankh (☥) is an ancient Egyptian hieroglyphic symbol representing "life" and serves as a thematic crosshair for navigating the musical harmonic space.

## Files

- **`ankh.glb`** - The 3D ankh model in GLB format (114.93 KB)
- **`create_ankh.py`** - Blender Python script used to generate the model
- **`ANKH_3D_README.md`** - This documentation file

## Model Specifications

### Geometry
- **Format:** GLB (GL Transmission Format Binary)
- **Vertices:** Optimized mesh with smooth shading
- **Components:**
  - Top loop (torus) - The circular/oval head of the ankh
  - Vertical staff (cylinder) - The main vertical shaft
  - Horizontal arms (cylinder) - The crossbar
  - Decorative spheres - End caps for visual polish

### Material
- **Type:** PBR (Physically Based Rendering)
- **Base Color:** Gold (#F0C040)
- **Metallic:** 1.0 (fully metallic)
- **Roughness:** 0.2 (shiny/polished)
- **Emission:** Gold glow with adjustable intensity
  - Normal state: 0.4 intensity
  - Hovered state: 1.0 intensity (brighter glow)

### Dimensions
- **Height:** ~2 units (scaled to fit reticle)
- **Width:** ~1.5 units
- **Centered:** Origin at geometric center

## Integration

### Component: `AnkhReticle3D.tsx`

The 3D ankh is integrated into the BSP DOOM Explorer through a dedicated React component:

```typescript
import { AnkhReticle3D } from './components/BSP/AnkhReticle3D';

// Usage
<AnkhReticle3D 
  hovered={!!hoveredElement} 
  size={60} 
/>
```

### Features

1. **Dynamic Glow Effect**
   - Normal state: Subtle gold glow
   - Hovered state: Bright gold glow when targeting interactive elements

2. **Smooth Animation**
   - Gentle rotation (0.01 rad/frame)
   - Smooth transitions between states

3. **Fallback Support**
   - If 3D model fails to load, falls back to SVG version
   - Ensures crosshair is always visible

4. **Performance Optimized**
   - Separate Three.js scene for reticle
   - Minimal geometry for fast rendering
   - Efficient material updates

## Creating the Model

The model was created using Blender 4.5.3 LTS with the following process:

### 1. Run the Blender Script

```bash
blender --background --python create_ankh.py
```

### 2. Script Process

The `create_ankh.py` script:
1. Clears the default Blender scene
2. Creates geometric primitives:
   - Torus for the top loop
   - Cylinders for staff and arms
   - Spheres for decorative ends
3. Applies beveling for smooth edges
4. Joins all parts into a single mesh
5. Applies gold PBR material with emission
6. Exports as GLB format

### 3. Manual Adjustments (Optional)

If you want to modify the model:
1. Open Blender
2. Import `ankh.glb`
3. Make your changes
4. Export as GLB with these settings:
   - Format: GLB
   - Include: Selected Objects
   - Materials: Export

## Usage in Three.js

### Loading the Model

```typescript
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader';

const loader = new GLTFLoader();
loader.load('/models/ankh.glb', (gltf) => {
  const ankh = gltf.scene;
  
  // Center the model
  const box = new THREE.Box3().setFromObject(ankh);
  const center = box.getCenter(new THREE.Vector3());
  ankh.position.sub(center);
  
  // Scale to desired size
  const size = box.getSize(new THREE.Vector3());
  const maxDim = Math.max(size.x, size.y, size.z);
  const scale = 2.5 / maxDim;
  ankh.scale.setScalar(scale);
  
  scene.add(ankh);
});
```

### Updating Material Properties

```typescript
ankh.traverse((child) => {
  if (child instanceof THREE.Mesh) {
    const material = child.material as THREE.MeshStandardMaterial;
    material.emissiveIntensity = hovered ? 1.0 : 0.4;
    material.needsUpdate = true;
  }
});
```

## Design Rationale

### Why an Ankh?

1. **Symbolic Meaning:** The ankh represents "life" in ancient Egyptian culture, fitting for a tool that helps navigate and explore musical harmony (the "life" of music)

2. **Visual Clarity:** The distinctive cross-with-loop shape is instantly recognizable and works well as a crosshair

3. **Thematic Consistency:** Matches the Egyptian/ancient aesthetic of the BSP DOOM Explorer with its pyramid structures and hieroglyphic-inspired UI

4. **Functional Design:** The vertical staff naturally points to targets, while the loop provides a clear center point

### Why 3D Instead of SVG?

1. **Visual Appeal:** 3D model with PBR materials looks more polished and professional
2. **Dynamic Lighting:** Responds to scene lighting and can glow dynamically
3. **Animation:** Can rotate and animate smoothly in 3D space
4. **Depth:** Adds visual depth to the UI, making it feel more immersive
5. **Consistency:** Matches the 3D nature of the BSP DOOM Explorer environment

## Performance Considerations

- **File Size:** 114.93 KB - reasonable for a detailed 3D model
- **Render Cost:** Minimal - separate small scene with simple geometry
- **Load Time:** Async loading with fallback ensures no blocking
- **Memory:** Single shared model instance, minimal overhead

## Future Enhancements

Potential improvements for the 3D ankh:

1. **Particle Effects:** Add particle trails when moving
2. **Color Variations:** Change color based on current floor/region
3. **Hit Feedback:** Pulse or flash when successfully targeting
4. **Texture Maps:** Add normal/roughness maps for more detail
5. **LOD Versions:** Create lower-poly versions for performance
6. **Animation States:** Different animations for different interactions

## Troubleshooting

### Model Not Loading

1. Check browser console for errors
2. Verify `ankh.glb` exists in `public/models/`
3. Check network tab for 404 errors
4. Ensure GLTFLoader is properly imported

### Material Issues

1. Verify Three.js version compatibility
2. Check that PBR materials are supported
3. Ensure proper lighting in the scene

### Performance Issues

1. Reduce reticle size
2. Disable rotation animation
3. Use lower-poly version
4. Check for memory leaks in animation loop

## Credits

- **Model Creation:** Blender 4.5.3 LTS
- **Script:** Python 3.x with Blender Python API
- **Integration:** React + Three.js + TypeScript
- **Format:** GLB (glTF 2.0 Binary)

## License

This 3D model is part of the Guitar Alchemist project and follows the same license as the main repository.

