# 3D Capo Model Integration

This document describes the integration of a realistic 3D capo model from Sketchfab into the Guitar Alchemist fretboard components.

## Overview

The 3D capo integration replaces the basic geometric capo representation with a detailed 3D model while maintaining backward compatibility through automatic fallback to geometric shapes if the 3D model fails to load.

## Model Information

**Source**: [Guitar Capo by Chad (@cpenfold)](https://sketchfab.com/3d-models/guitar-capo-255712235bcd49d6b8bd7fd056868de7)
- **License**: CC Attribution
- **Format**: GLB (binary glTF)
- **Triangles**: 389.5k
- **Vertices**: 195.9k
- **Published**: June 20th, 2020

## Quick Start

### 1. Download the Model

```bash
# Run the download helper script
.\scripts\download-capo-model.ps1

# Or manually:
# 1. Visit https://sketchfab.com/3d-models/guitar-capo-255712235bcd49d6b8bd7fd056868de7
# 2. Download as GLB format
# 3. Place at public/models/guitar-capo.glb
```

### 2. Test the Integration

```bash
npm run dev
# Visit http://localhost:5173/test/capo-model
```

## Architecture

### Components Updated

1. **ThreeFretboard** (`src/components/ThreeFretboard.tsx`)
   - Uses `loadCapoModel()` for 3D model loading
   - Falls back to `createFallbackCapo()` if model fails

2. **MinimalThreeInstrument** (`src/components/MinimalThree/MinimalThreeInstrument.tsx`)
   - Integrated 3D capo loading in `createCapo()` function
   - Maintains original geometric fallback

### New Utilities

1. **capoModelLoader** (`src/utils/capoModelLoader.ts`)
   - `loadCapoModel()` - Async 3D model loading with caching
   - `createFallbackCapo()` - Geometric capo fallback
   - `clearCapoModelCache()` - Cache management
   - `getCapoModelCacheStats()` - Cache statistics

### Test Pages

1. **CapoModelTest** (`src/pages/CapoModelTest.tsx`)
   - Comprehensive testing of 3D capo integration
   - Interactive controls for capo position and guitar model
   - Technical documentation and attribution

## Usage Examples

### Basic Usage (ThreeFretboard)

```typescript
import { ThreeFretboard } from './components/ThreeFretboard';

<ThreeFretboard
  positions={chordPositions}
  config={{
    capoFret: 2, // Capo at 2nd fret
    guitarModel: 'electric_fender_strat',
    enableOrbitControls: true,
  }}
/>
```

### Advanced Usage (Direct Model Loading)

```typescript
import { loadCapoModel } from './utils/capoModelLoader';

const capoModel = await loadCapoModel({
  modelPath: '/models/guitar-capo.glb',
  scale: 0.1,
  position: new THREE.Vector3(x, y, z),
  rotation: new THREE.Euler(0, 0, 0),
  color: 0x1a1a1a,
  metalness: 0.8,
  roughness: 0.3
});

scene.add(capoModel);
```

## Configuration Options

### CapoModelConfig Interface

```typescript
interface CapoModelConfig {
  modelPath: string;        // Path to GLB/GLTF file
  scale?: number;           // Model scale (default: 1)
  position?: THREE.Vector3; // World position
  rotation?: THREE.Euler;   // World rotation
  color?: number;           // Material color override
  metalness?: number;       // Material metalness
  roughness?: number;       // Material roughness
}
```

### Recommended Settings

| Component | Scale | Position | Notes |
|-----------|-------|----------|-------|
| ThreeFretboard | 0.1 | Dynamic based on fret | Full-size fretboard |
| MinimalThreeInstrument | 0.05 | Dynamic based on fret | Compact instrument view |

## Performance Considerations

### Model Optimization

- **Triangles**: 389.5k (high detail)
- **File Size**: ~2-5MB estimated
- **Loading Time**: 1-3 seconds on first load
- **Memory Usage**: Cached after first load

### Optimization Strategies

1. **Model Caching**: Models are cached to avoid reloading
2. **Fallback System**: Instant geometric fallback if model fails
3. **Async Loading**: Non-blocking model loading
4. **LOD Consideration**: Consider lower-poly version for performance

### Performance Monitoring

```typescript
import { getCapoModelCacheStats } from './utils/capoModelLoader';

const stats = getCapoModelCacheStats();
console.log('Cache size:', stats.size);
console.log('Cached models:', stats.keys);
```

## Fallback System

The integration includes a robust fallback system:

1. **Primary**: Load 3D model from Sketchfab
2. **Fallback**: Create geometric capo using Three.js primitives
3. **Error Handling**: Graceful degradation with console warnings

### Fallback Triggers

- Model file not found (404 error)
- Invalid GLB/GLTF format
- WebGL/WebGPU loading errors
- Network connectivity issues

## Attribution Requirements

As per CC Attribution license, include this attribution:

```html
"Guitar Capo" by Chad (@cpenfold) is licensed under CC Attribution
https://sketchfab.com/3d-models/guitar-capo-255712235bcd49d6b8bd7fd056868de7
```

## Troubleshooting

### Model Not Loading

1. **Check File Path**: Ensure `guitar-capo.glb` is in `public/models/`
2. **Check File Size**: Should be 2-5MB for the Sketchfab model
3. **Check Console**: Look for loading errors in browser console
4. **Check Network**: Verify file is accessible via HTTP

### Performance Issues

1. **Reduce Model Quality**: Use a lower-poly version
2. **Implement LOD**: Add Level of Detail system
3. **Optimize Materials**: Reduce texture resolution
4. **Cache Management**: Clear cache if memory issues occur

### Visual Issues

1. **Scale Problems**: Adjust `scale` parameter (0.05-0.2 range)
2. **Position Problems**: Verify fret position calculations
3. **Material Issues**: Adjust `metalness` and `roughness` values
4. **Lighting Issues**: Ensure proper scene lighting setup

## Development Notes

### File Structure

```
ReactComponents/ga-react-components/
├── public/models/
│   ├── guitar-capo.glb          # 3D capo model
│   └── CAPO_MODEL_SETUP.md      # Setup instructions
├── src/utils/
│   └── capoModelLoader.ts       # Model loading utilities
├── src/components/
│   ├── ThreeFretboard.tsx       # Updated with 3D capo
│   └── MinimalThree/
│       └── MinimalThreeInstrument.tsx  # Updated with 3D capo
├── src/pages/
│   └── CapoModelTest.tsx        # Test page
└── scripts/
    └── download-capo-model.ps1  # Download helper
```

### Testing

```bash
# Run all tests
npm test

# Test specific capo functionality
npm run test:capo

# Visual testing
npm run dev
# Visit http://localhost:5173/test/capo-model
```

### Future Enhancements

1. **Multiple Capo Styles**: Support different capo designs
2. **Animation**: Capo placement/removal animations
3. **Physics**: Realistic capo pressure on strings
4. **Customization**: User-selectable capo colors/materials
5. **LOD System**: Automatic quality adjustment based on distance

## License and Attribution

This integration respects the original model's CC Attribution license. The model must be attributed to Chad (@cpenfold) from Sketchfab in any public use of this code.
