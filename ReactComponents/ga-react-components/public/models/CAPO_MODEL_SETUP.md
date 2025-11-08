# Guitar Capo 3D Model Setup

## Model Information

**Source**: Sketchfab - Guitar Capo by Chad (@cpenfold)
**URL**: https://sketchfab.com/3d-models/guitar-capo-255712235bcd49d6b8bd7fd056868de7
**License**: CC Attribution (Creative Commons Attribution)
**Triangles**: 389.5k
**Vertices**: 195.9k
**Published**: June 20th, 2020

## Download Instructions

### Step 1: Create Sketchfab Account (Free)
1. Go to https://sketchfab.com
2. Click "Sign Up" in the top right
3. Create a free account

### Step 2: Download the Model
1. Visit the model page: https://sketchfab.com/3d-models/guitar-capo-255712235bcd49d6b8bd7fd056868de7
2. Click the "Download 3D Model" button
3. Select format: **"glTF"** (choose .glb for single file)
4. Click "Download"
5. Extract the downloaded ZIP file

### Step 3: Place the Model
1. Find the `.glb` file in the extracted folder
2. Copy it to this directory: `ReactComponents/ga-react-components/public/models/`
3. Rename it to: `guitar-capo.glb`

### Step 4: Test the Model
1. Start the development server: `npm run dev`
2. Go to: http://localhost:5173/test/capo
3. The 3D capo should load automatically!

## File Structure After Setup

```
ReactComponents/ga-react-components/public/models/
├── README.md
├── CAPO_MODEL_SETUP.md (this file)
├── guitar.glb
├── guitar2.glb
└── guitar-capo.glb (new file)
```

## Model Specifications

- **Format**: GLB (binary glTF)
- **Size**: ~2-5MB (estimated)
- **Materials**: PBR (Physically Based Rendering)
- **Textures**: Embedded in GLB file
- **Scale**: Real-world scale (may need adjustment)

## Usage in Code

```typescript
import { loadCapoModel } from '../utils/capoModelLoader';

// Load the capo model
const capoModel = await loadCapoModel({
  modelPath: '/models/guitar-capo.glb',
  scale: 0.1, // Adjust scale as needed
  position: new THREE.Vector3(capoX, 0.8, 0),
  color: 0x1a1a1a, // Optional color override
  metalness: 0.8,
  roughness: 0.3
});

scene.add(capoModel);
```

## License Attribution

As required by CC Attribution license, please include this attribution:

```
"Guitar Capo" by Chad (@cpenfold) is licensed under CC Attribution
https://sketchfab.com/3d-models/guitar-capo-255712235bcd49d6b8bd7fd056868de7
```

## Troubleshooting

### Model Not Loading
1. Check that the file is named exactly `guitar-capo.glb`
2. Verify the file is in the correct directory
3. Check browser console for error messages
4. Ensure the file isn't corrupted (try re-downloading)

### Model Too Large/Small
- Adjust the `scale` parameter in the `loadCapoModel` function
- Typical values: 0.05 - 0.2 depending on the original model scale

### Performance Issues
- The model has 389.5k triangles, which is quite detailed
- Consider using a lower-poly version for better performance
- Enable LOD (Level of Detail) if needed

### Material Issues
- If materials look wrong, try adjusting `metalness` and `roughness` parameters
- The model should have PBR materials that work well with Three.js

## Alternative Models

If this model doesn't work or you need a different style:

1. **Sketchfab Search**: https://sketchfab.com/search?q=guitar+capo&type=models&downloadable=true
2. **Free3D**: https://free3d.com/3d-models/guitar-capo
3. **TurboSquid Free**: https://www.turbosquid.com/Search/3D-Models/free/guitar-capo

## Model Optimization

For better performance, you can optimize the model:

1. **Blender Optimization**:
   - Import the GLB file into Blender
   - Use "Decimate" modifier to reduce polygon count
   - Re-export as GLB

2. **Online Tools**:
   - https://gltf.report/ - Analyze and optimize GLTF files
   - https://products.aspose.app/3d/compress - Compress 3D models

## Development Notes

- The capo model loader includes caching to avoid reloading
- Fallback geometric capo is used if 3D model fails to load
- Model supports material customization (color, metalness, roughness)
- Shadows are automatically enabled for realistic rendering
