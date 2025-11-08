# 3D Guitar Models

This directory is for storing 3D guitar models in GLTF/GLB format.

## 📁 Model Slots

The Guitar 3D Viewer supports **3 guitar model slots**:

1. **`guitar.glb`** - ✅ **Classical Guitar** (Currently working: 9,125 vertices, 10,420 triangles)
2. **`guitar2.glb`** - 🎸 **Fender Telecaster** (Electric Guitar - Download from Sketchfab)
3. **`guitar3.glb`** - ⚠️ **Electric Guitar** (Needs model - add your own electric guitar GLB file)

**Goal:** Display three different guitar types side-by-side for comparison.

### 🎸 Slot 2: Fender Telecaster Setup

**Model**: Fender Telecaster by Sladegeorg
**Source**: https://sketchfab.com/3d-models/fender-telecaster-f2b583f97def4b1d954bda871f65eaf2
**Quality**: 25.4k triangles, 20.5k vertices (optimized for real-time display)
**Published**: August 29th, 2017

**Quick Setup**:
```bash
# Run the download helper script
.\scripts\download-telecaster-model.ps1

# Or manually download and place at:
# public/models/guitar2.glb
```

---

## 🎸 **Recommended Models to Download (When You Have Computer Access)**

### **For Slot 2 - Acoustic Guitar:**
- **Model**: "Acoustic guitar" by pezcurrel
- **URL**: https://sketchfab.com/3d-models/acoustic-guitar-770b851ca34343a2825180ec23800402
- **License**: CC Attribution
- **Stats**: 729.1k triangles, 362.3k vertices
- **Download**: Click "Download 3D Model" → Select "glTF" format → Save as `guitar2.glb`

### **For Slot 3 - Electric Guitar:**
- **Model**: "Fender Stratocaster Guitar" by Ryan_Nein
- **URL**: https://sketchfab.com/3d-models/fender-stratocaster-guitar-15a37147641b4c1b963bb494b234593f
- **License**: CC Attribution
- **Stats**: 373.3k triangles, 201.5k vertices
- **Download**: Click "Download 3D Model" → Select "glTF" format → Save as `guitar3.glb`

**Note**: You'll need to create a free Sketchfab account to download these models.

## ⚠️ Note About BlenderKit

The BlenderKit zip file (`blenderkit-v3.17.0.251008.zip`) is a **Blender addon**, not a 3D model. To use BlenderKit:

1. Install the addon in Blender (Edit → Preferences → Add-ons → Install)
2. Search for guitar models within Blender
3. Download models through the BlenderKit interface
4. Export as GLB: File → Export → glTF 2.0 (.glb)
5. Place the exported file in this directory

## Where to Find Free Guitar Models

### 1. Sketchfab (Recommended)
- **URL**: https://sketchfab.com
- **Search**: "guitar" with filters for "Downloadable" and "CC Attribution" or "CC0" license
- **Format**: Download as GLTF (.glb)
- **Examples**:
  - Acoustic Guitar: https://sketchfab.com/3d-models/acoustic-guitar-ce63135788664884adb41ec0e16aac0b
  - Electric Guitar Explorer: https://sketchfab.com/3d-models/electric-guitar-explorer-a7ffc570d3fe41c89b9dde195ab0faea

### 2. BlenderKit
- **URL**: https://www.blenderkit.com
- **Search**: "guitar"
- **Format**: Download and export from Blender as GLTF (.glb)
- **License**: Various (check individual models)
- **Example**: https://www.blenderkit.com/get-blenderkit/0a3e4f50-dd07-4b7a-9dd0-59219450aad1/

### 3. Poly Haven
- **URL**: https://polyhaven.com/models
- **Search**: Limited guitar selection, but all CC0
- **Format**: Download and convert to GLTF if needed
- **License**: CC0 (Public Domain)

### 4. Free3D
- **URL**: https://free3d.com
- **Search**: "guitar"
- **Format**: Various (may need conversion to GLTF)
- **License**: Check individual models

### 5. TurboSquid Free Models
- **URL**: https://www.turbosquid.com/Search/3D-Models/free/guitar
- **Format**: Various (may need conversion)
- **License**: Check individual models

## How to Download from Sketchfab

1. Go to https://sketchfab.com
2. Search for "guitar"
3. Filter by:
   - ✓ Downloadable
   - ✓ CC Attribution or CC0 license
4. Click on a model you like
5. Click "Download 3D Model" button
6. Select "glTF" format (either .gltf or .glb)
7. Download the file
8. Place it in this directory (`public/models/`)
9. Rename it to something simple like `guitar.glb`

## Example: Downloading Acoustic Guitar from Sketchfab

```bash
# Direct download link (example - may require authentication)
# https://sketchfab.com/3d-models/acoustic-guitar-ce63135788664884adb41ec0e16aac0b

# After downloading, place in this directory:
# public/models/acoustic-guitar.glb
```

## Converting Other Formats to GLTF

If you have a model in another format (FBX, OBJ, etc.), you can convert it using:

### Option 1: Blender (Free)
1. Download Blender: https://www.blender.org/download/
2. Open Blender
3. File → Import → [Your Format] (FBX, OBJ, etc.)
4. File → Export → glTF 2.0 (.glb)
5. Save to this directory

### Option 2: Online Converters
- https://products.aspose.app/3d/conversion
- https://imagetostl.com/convert/file/obj/to/gltf
- https://anyconv.com/fbx-to-gltf-converter/

## Using Models in the App

Once you have a model in this directory:

1. Go to http://localhost:5178/test/guitar-3d
2. Enter the path in the "Model Path or URL" field:
   - Example: `/models/guitar.glb`
   - Or: `/models/acoustic-guitar.glb`
3. Click "Load"

## Model Requirements

- **Format**: GLTF (.gltf) or GLB (.glb) - GLB is preferred (single file)
- **Size**: Recommended < 50MB for good performance
- **Textures**: Embedded in GLB or in same directory for GLTF
- **Materials**: PBR materials work best (Metallic-Roughness workflow)

## License Attribution

If you use models with CC Attribution license, please credit the original author:

Example:
```
"Acoustic Guitar" by [Author Name] is licensed under CC Attribution
https://sketchfab.com/3d-models/[model-id]
```

## Placeholder Model

If you don't have a model yet, the app will show a loading error. You can:

1. Download a free model from Sketchfab (see above)
2. Use a direct URL to a model hosted online
3. Create a simple guitar model in Blender

## Example Models to Try

### Acoustic Guitar (Sketchfab)
- **Author**: iam3d_ar
- **License**: CC Attribution
- **URL**: https://sketchfab.com/3d-models/acoustic-guitar-ce63135788664884adb41ec0e16aac0b
- **Download**: Click "Download 3D Model" → Select "glTF"

### Electric Guitar Explorer (Sketchfab)
- **Author**: Skabl
- **License**: CC Attribution
- **URL**: https://sketchfab.com/3d-models/electric-guitar-explorer-a7ffc570d3fe41c89b9dde195ab0faea
- **Download**: Click "Download 3D Model" → Select "glTF"

## Troubleshooting

### Model doesn't load
- Check the file path is correct
- Ensure the file is in GLB or GLTF format
- Check browser console for errors
- Try a different model

### Model is too big/small
- The component automatically scales models to fit
- If scaling is wrong, check the model's units in Blender

### Textures missing
- For GLTF files, ensure textures are in the same directory
- Use GLB format for single-file models with embedded textures

### Performance issues
- Reduce model polygon count in Blender
- Compress textures
- Use smaller texture sizes (1024x1024 or 2048x2048)

## Creating Your Own Guitar Model

If you want to create your own guitar model:

1. Use Blender (free): https://www.blender.org
2. Follow guitar modeling tutorials on YouTube
3. Use PBR materials (Principled BSDF shader)
4. Export as GLB with:
   - ✓ Apply Modifiers
   - ✓ UVs
   - ✓ Normals
   - ✓ Materials
   - ✓ Compress (optional)

## Support

For issues or questions, check:
- Three.js GLTF documentation: https://threejs.org/docs/#examples/en/loaders/GLTFLoader
- Sketchfab help: https://help.sketchfab.com
- Blender GLTF export: https://docs.blender.org/manual/en/latest/addons/import_export/scene_gltf2.html

