# Fender Telecaster 3D Model Setup

## Model Information

**Source**: Sketchfab - Fender Telecaster by Sladegeorg  
**URL**: https://sketchfab.com/3d-models/fender-telecaster-f2b583f97def4b1d954bda871f65eaf2  
**Triangles**: 25.4k  
**Vertices**: 20.5k  
**Published**: August 29th, 2017  
**Views**: 25,167  
**Likes**: 276  

## Quick Setup

### Option 1: Automated Script (Recommended)

```bash
# Navigate to the React components directory
cd ReactComponents/ga-react-components

# Run the download helper script
.\scripts\download-telecaster-model.ps1

# Follow the prompts to open Sketchfab and download the model
```

### Option 2: Manual Download

1. **Visit Sketchfab**: https://sketchfab.com/3d-models/fender-telecaster-f2b583f97def4b1d954bda871f65eaf2
2. **Create Account**: Sign up for a free Sketchfab account if you don't have one
3. **Download Model**: 
   - Click "Download 3D Model" button
   - Select "glTF" format
   - Choose .glb (binary) for single file
4. **Extract & Place**:
   - Extract the downloaded ZIP file
   - Find the .glb file
   - Copy to: `ReactComponents/ga-react-components/public/models/guitar2.glb`

## File Structure

```
ReactComponents/ga-react-components/public/models/
├── guitar.glb              # Slot 1 - Classical Guitar
├── guitar2.glb             # Slot 2 - Fender Telecaster (NEW)
├── guitar3.glb             # Slot 3 - TBD Electric Guitar
├── guitar-capo.glb         # 3D Capo Model
├── README.md
├── TELECASTER_SETUP.md     # This file
└── CAPO_MODEL_SETUP.md
```

## Testing the Model

### 1. Start Development Server

```bash
cd ReactComponents/ga-react-components
npm run dev
```

### 2. Test Pages

- **Guitar 3D Viewer**: http://localhost:5173/test/guitar-3d
- **Three Fretboard**: http://localhost:5173/test/three-fretboard
- **Capo Model Test**: http://localhost:5173/test/capo-model

### 3. Verify Loading

1. Go to http://localhost:5173/test/guitar-3d
2. Look for "Guitar Model 2 - Fender Telecaster"
3. The model should load and display a Telecaster guitar
4. Check the model info shows correct triangle/vertex count

## Model Specifications

### Technical Details
- **Format**: GLB (binary glTF)
- **Size**: ~1-3MB estimated
- **Polygons**: 25.4k triangles (optimized for real-time)
- **Textures**: Embedded in GLB file
- **Materials**: PBR (Physically Based Rendering)

### Guitar Specifications
- **Type**: Electric Guitar
- **Brand**: Fender
- **Model**: Telecaster
- **Scale Length**: 25.5" (648mm)
- **Nut Width**: 1.685" (42.8mm)
- **Frets**: 22 frets
- **Strings**: 6 strings
- **Tuning**: Standard (E-A-D-G-B-E)

## Integration Details

### Guitar Models Configuration

The Telecaster has been added to the guitar models configuration:

```typescript
'electric_fender_telecaster': {
  name: 'Fender Telecaster',
  category: 'electric',
  brand: 'Fender',
  model: 'Telecaster',
  woodColor: 0x8B4513, // Saddle brown (maple neck)
  stringColor: 0xc0c0c0, // Silver
  fretColor: 0x696969, // Dim gray
  nutColor: 0xf5f5dc, // Bone
  markerColor: 0x000000, // Black dots
  fretCount: 22,
  stringCount: 6,
  tuning: ['E', 'B', 'G', 'D', 'A', 'E'],
  scaleLength: 648,
  nutWidth: 42.8,
  bridgeWidth: 52,
  neckProfileId: 'vintage-c',
  headstockStyle: 'electric',
  inlayStyle: 'dots',
  inlayColor: 0x000000,
}
```

### Usage in Components

```typescript
// Use in ThreeFretboard
<ThreeFretboard
  config={{
    guitarModel: 'electric_fender_telecaster',
    capoFret: 0,
    // ... other config
  }}
/>

// Use in Guitar3D viewer
<Guitar3D
  modelPath="/models/guitar2.glb"
  width={400}
  height={500}
  autoRotate={true}
/>
```

## Troubleshooting

### Model Not Loading

1. **Check File Path**: Ensure file is exactly at `public/models/guitar2.glb`
2. **Check File Size**: Should be 1-3MB for the Telecaster model
3. **Check Console**: Look for loading errors in browser console
4. **Verify Format**: Ensure it's a .glb file, not .gltf with separate textures

### Performance Issues

1. **Model Quality**: 25.4k triangles is reasonable for real-time display
2. **Browser Support**: Ensure WebGL is enabled in your browser
3. **Memory Usage**: The model should load quickly due to optimized polygon count

### Visual Issues

1. **Scaling**: The Guitar3D component auto-scales models to fit
2. **Materials**: PBR materials should render correctly with proper lighting
3. **Textures**: All textures are embedded in the GLB file

## Model Comparison

| Slot | Model | Triangles | Type | Status |
|------|-------|-----------|------|--------|
| 1 | Classical Guitar | 10.4k | Classical/Acoustic | ✅ Working |
| 2 | Fender Telecaster | 25.4k | Electric | 🎸 Ready to Download |
| 3 | TBD Electric | - | Electric | ⚠️ Needs Model |

## Next Steps

1. **Download the Model**: Use the script or manual method above
2. **Test Integration**: Verify the model loads in the Guitar 3D viewer
3. **Explore Features**: Try the model with different camera angles and lighting
4. **Add Slot 3**: Consider adding another electric guitar model for slot 3

## Attribution

Model created by Sladegeorg on Sketchfab. No specific license information was provided on the model page, but it's available for download from Sketchfab.

## Support

If you encounter issues:

1. **Check Scripts**: Run `.\scripts\download-telecaster-model.ps1 -Info` for model information
2. **Verify Setup**: Run `.\scripts\download-telecaster-model.ps1` to check all model slots
3. **Console Logs**: Check browser console for detailed error messages
4. **File Permissions**: Ensure the web server can access the models directory
