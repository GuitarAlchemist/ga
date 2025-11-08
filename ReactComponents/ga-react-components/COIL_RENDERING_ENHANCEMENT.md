# Coil Rendering Enhancement for Wound Guitar Strings

## Overview

Enhanced the wound string coil rendering in the `ThreeFretboard` component to create a more realistic metallic appearance matching professional reference images.

## Changes Made

### File Modified
- **`ReactComponents/ga-react-components/src/components/ThreeFretboard.tsx`**
  - Enhanced `createWindingTexture()` function (lines 578-680) - realistic coil rendering
  - Fixed string appearance (lines 674-680, 1146-1158):
    - Reduced texture repeat from 150 to 80 for more visible coils
    - Reduced normalScale from 8.0 to 0.5 to prevent wavy/zigzag appearance
    - Reduced bumpScale from 1.2 to 0.3 for subtle texture
  - **Improved fret geometry** (lines 1021-1084):
    - Reduced fret width from 105% to 92% of nut width (stays within neck bounds)
    - Added meplat (flat top surface) for realistic crowned fret profile
    - Custom extruded shape: flat bottom, curved sides, flat top
    - More realistic metallic appearance with higher metalness (0.9)
  - Fixed THREE.TSL deprecation: replaced `atan2` with `atan` (line 14, 721)
  - Improved WebGPU initialization with longer delay (100ms) to prevent device conflicts
  - Enhanced error handling in renderer disposal
  - **Fixed shader material compatibility** (lines 433-448, 1090):
    - Disabled shader winding toggle when not in WebGPU mode
    - Only use shader winding with WebGPU (prevents ShaderMaterial error)
    - Updated tooltip to indicate WebGPU requirement
  - **Fixed neck geometry** (lines 888-961):
    - Changed from half-cylinder to extruded half-ellipse for realistic guitar neck profile
    - Cross-section (tranche) is now elliptical - wider than thick
    - Corrected orientation (curved part faces down, flat part connects to fretboard)
    - Proper elliptical proportions: width = 95% of nut width, thickness = 35% of nut width
    - Fixed alignment with fretboard (proper X-axis positioning after rotation)
    - Seamless connection to fretboard bottom
    - Uses THREE.Shape and THREE.ExtrudeGeometry for accurate elliptical profile
  - Added separate neck back material with lighter maple color

## Key Improvements

### 1. **Higher Resolution Texture**
- Increased canvas size from 512x128 to 1024x256 pixels
- Provides better detail and smoother gradients

### 2. **Tightly Wound Coils (No Gaps)**
- **Continuous wrapping**: Coil width 8.0 pixels - each wrap touches the next
- **No visible core**: Steel core completely covered by tight winding (like real wound strings)
- **Contact shadows only**: Subtle darkening where wraps meet (not gaps or grooves)
- **Smooth rounded profile**: Using sine wave (`Math.cos(angle)`) for natural cylindrical wire cross-section

### 3. **Advanced Lighting Model**
- **Directional lighting**: Light source from top-right (0.3, 0.95) for realistic highlights
- **Specular highlights**: Very bright reflections (brightness > 0.95) on coil peaks
- **Graduated shading**: 
  - Bright specular (lightDot > 0.7): 95-110% brightness
  - Diffuse reflection (0.3-0.7): 65-95% brightness
  - Mid-tone (0-0.3): 35-65% brightness
  - Shadow transition (-0.3-0): 20-35% brightness
  - Deep shadow (< -0.3): 8-20% brightness
- **Ambient occlusion**: Subtle darkening in grooves using `Math.pow(height, 0.6)`

### 4. **Golden/Brass Metallic Color**
- **Warmer color palette**: RGB(235, 200, 130) base color
- **Realistic bronze/brass tones**: Matches wound string appearance
- **High contrast**: Strong highlights and deep shadows for metallic look

### 5. **Contact Shadow Rendering**
- **Wrap boundaries**: Subtle ambient occlusion where wraps touch
- **No gaps**: Wraps are continuous with no visible steel core between them
- **Edge darkening**: Slight shadow at wrap edges (15% AO factor) for depth perception

### 6. **Subtle Surface Texture**
- **Metallic variation**: 60 random micro-variations for realistic surface
- **Wire imperfections**: Subtle color variations (3-8% alpha)
- **Natural imperfections**: Small spots (0.3-1.3 pixels) for authenticity

### 7. **Optimized Texture Mapping**
- **Realistic repeat count**: 80 wraps along string length
- **Tight winding density**: Matches real wound string appearance
- **Seamless wrapping**: Proper THREE.RepeatWrapping for continuous pattern
- **Subtle normal/bump mapping**: Reduced scales (0.5, 0.3) prevent wavy distortion

## Visual Characteristics

The enhanced coil rendering now features:

✅ **Smooth rounded coils** - Like tightly packed cylinders  
✅ **Bright metallic highlights** - Specular reflections on coil peaks  
✅ **Deep shadows** - Strong contrast between lit and shadowed areas  
✅ **Golden/brass coloring** - Warm metallic tones  
✅ **Realistic depth** - Proper 3D appearance with ambient occlusion  
✅ **Fine detail** - High-resolution texture with subtle variations  

## Technical Details

### Lighting Calculation
```typescript
// Surface normal based on coil angle
const normalX = Math.sin(angle);
const normalY = Math.cos(angle);

// Light direction (from top-right)
const lightDirX = 0.3;
const lightDirY = 0.95;

// Dot product for lighting intensity
const lightDot = normalX * lightDirX + normalY * lightDirY;
```

### Color Calculation
```typescript
// Golden/brass base color
const r = Math.floor(235 * brightness);
const g = Math.floor(200 * brightness);
const b = Math.floor(130 * brightness);
```

### Ambient Occlusion
```typescript
// Darken grooves between coils
const grooveFactor = Math.pow(height, 0.6);
brightness *= (0.7 + grooveFactor * 0.3);
```

## Usage

The enhanced coil rendering is automatically applied to wound strings (strings 2-5: G, D, A, low E) in the `ThreeFretboard` component when using texture-based winding (default mode).

### Viewing the Enhancement

1. **Start the development server**:
   ```bash
   cd ReactComponents/ga-react-components
   npm run dev
   ```

2. **Open the demo page**: http://localhost:5173

3. **View the 3D fretboard**: The "C Major Chord - 3D (Three.js)" section shows the enhanced wound strings

4. **Rotate the view**: Use mouse to orbit and zoom to see the coil detail

### Configuration

The wound string rendering can be toggled between texture-based (enhanced) and shader-based modes:

```typescript
<ThreeFretboard
  config={{
    guitarModel: 'electric_fender_strat',
    // Texture-based winding (default) - uses enhanced coil rendering
    // Shader-based winding (experimental) - uses procedural shader
  }}
/>
```

## Performance

- **Texture generation**: One-time cost during component initialization
- **GPU-accelerated**: Texture mapping handled by WebGL/WebGPU
- **Efficient**: No per-frame calculations for coil pattern
- **Scalable**: Works well with multiple wound strings

## Troubleshooting

### WebGPU Device Conflicts

If you see warnings like "TextureView is associated with [Device], and cannot be used with [Device]":

**Cause**: Multiple WebGPU renderers initializing simultaneously (e.g., both `RealisticFretboard` and `ThreeFretboard` on the same page)

**Solutions**:
1. **Stagger component mounting**: Add a small delay between rendering multiple WebGPU components
2. **Use only one WebGPU component at a time**: Consider tabs or conditional rendering
3. **Increase initialization delay**: The component now uses a 100ms delay to prevent conflicts

### THREE.TSL Warnings

The `atan2` deprecation warning has been fixed by replacing it with `atan(y/x)` in the shader code.

### Performance Issues

If the 3D fretboard is slow:
1. **Reduce pixel ratio**: Lower the `devicePixelRatio` multiplier in the renderer setup
2. **Disable orbit controls**: Set `enableOrbitControls: false` in config
3. **Use texture-based winding**: Avoid shader-based winding for better performance
4. **Reduce texture resolution**: Lower the canvas size in `createWindingTexture()`

## Future Enhancements

Potential improvements:
- [ ] Varying coil density based on string gauge
- [ ] Oxidation/tarnish effects for aged strings
- [ ] Different winding materials (nickel, bronze, phosphor bronze)
- [ ] Animated coil vibration during string playback
- [ ] Normal mapping for even more realistic depth
- [ ] Shared WebGPU context for multiple components

## References

- Enhanced to match professional wound string reference images
- Based on real guitar string construction (core wire + outer winding)
- Lighting model inspired by PBR (Physically Based Rendering) techniques

