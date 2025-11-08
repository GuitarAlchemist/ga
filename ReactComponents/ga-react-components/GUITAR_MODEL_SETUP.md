# Guitar 3D Model Setup Guide

## Quick Start - Download a Free Guitar Model

### Option 1: Acoustic Guitar from Sketchfab (Recommended)

1. **Visit the model page**:
   - URL: https://sketchfab.com/3d-models/acoustic-guitar-ce63135788664884adb41ec0e16aac0b

2. **Download the model**:
   - Click the "Download 3D Model" button (requires free Sketchfab account)
   - Select format: **"glTF"** (choose either .gltf or .glb - .glb is preferred)
   - Click "Download"

3. **Place the model**:
   - Extract the downloaded ZIP file
   - Find the `.glb` file (or `.gltf` + textures)
   - Copy it to: `ReactComponents/ga-react-components/public/models/`
   - Rename it to: `guitar.glb`

4. **Test it**:
   - Go to: http://localhost:5178/test/guitar-3d
   - The model should load automatically!

### Option 2: Electric Guitar Explorer from Sketchfab

1. **Visit the model page**:
   - URL: https://sketchfab.com/3d-models/electric-guitar-explorer-a7ffc570d3fe41c89b9dde195ab0faea

2. **Follow the same download steps as above**

3. **Place the model**:
   - Copy to: `ReactComponents/ga-react-components/public/models/electric-guitar.glb`

4. **Load it**:
   - Go to: http://localhost:5178/test/guitar-3d
   - Enter path: `/models/electric-guitar.glb`
   - Click "Load"

### Option 3: Use a Direct URL (No Download Required)

Some models can be loaded directly from URLs. Try these:

1. **Go to**: http://localhost:5178/test/guitar-3d

2. **Enter one of these URLs** in the "Model Path or URL" field:
   ```
   https://raw.githubusercontent.com/KhronosGroup/glTF-Sample-Models/master/2.0/DamagedHelmet/glTF-Binary/DamagedHelmet.glb
   ```
   (This is a test model - not a guitar, but demonstrates the loading)

3. **Click "Load"**

## Detailed Instructions

### Creating a Sketchfab Account (Free)

1. Go to: https://sketchfab.com/signup
2. Sign up with email or Google/Facebook
3. Verify your email
4. You can now download models!

### Finding More Guitar Models

#### Sketchfab Search
1. Go to: https://sketchfab.com/search?q=guitar&type=models
2. Filter by:
   - ✓ **Downloadable**
   - ✓ **Animated** (optional - for animated guitars)
   - License: **CC Attribution** or **CC0** (free to use)
3. Click on a model you like
4. Click "Download 3D Model"
5. Select "glTF" format

#### BlenderKit
1. Download BlenderKit addon: https://www.blenderkit.com/get-blenderkit/
2. Install in Blender
3. Search for "guitar" in BlenderKit panel
4. Download a model
5. Export from Blender: File → Export → glTF 2.0 (.glb)

#### Other Sources
- **Poly Haven**: https://polyhaven.com/models (limited guitar selection)
- **Free3D**: https://free3d.com/3d-models/guitar
- **TurboSquid Free**: https://www.turbosquid.com/Search/3D-Models/free/guitar
- **CGTrader Free**: https://www.cgtrader.com/free-3d-models/guitar

## File Structure

After downloading, your file structure should look like:

```
ReactComponents/ga-react-components/
├── public/
│   └── models/
│       ├── README.md (already exists)
│       ├── guitar.glb (your downloaded model)
│       ├── electric-guitar.glb (optional)
│       └── acoustic-guitar.glb (optional)
└── src/
    └── components/
        └── Guitar3D/
            ├── Guitar3D.tsx
            └── index.ts
```

## Troubleshooting

### "Model loading error: SyntaxError"
- This means the file doesn't exist or the path is wrong
- Check the file is in `public/models/`
- Check the filename matches what you entered
- Try using the full path: `/models/guitar.glb`

### "Model is too big/small"
- The component automatically scales models
- If it's still wrong, the model might have incorrect units
- Try a different model

### "Textures are missing"
- Use `.glb` format (single file with embedded textures)
- If using `.gltf`, ensure all texture files are in the same directory

### "Model doesn't rotate"
- Turn on "Auto Rotate" switch
- Or use mouse to drag and rotate manually

## Converting Other Formats to GLTF

If you have a guitar model in another format (FBX, OBJ, 3DS, etc.):

### Using Blender (Free)

1. **Download Blender**: https://www.blender.org/download/

2. **Import your model**:
   - Open Blender
   - File → Import → [Your Format] (FBX, OBJ, etc.)
   - Select your guitar model file

3. **Export as GLTF**:
   - File → Export → glTF 2.0 (.glb)
   - Settings:
     - ✓ Apply Modifiers
     - ✓ UVs
     - ✓ Normals
     - ✓ Materials
     - Format: **glTF Binary (.glb)**
   - Save to: `ReactComponents/ga-react-components/public/models/guitar.glb`

### Using Online Converters

- **Aspose 3D Converter**: https://products.aspose.app/3d/conversion
- **AnyConv**: https://anyconv.com/fbx-to-gltf-converter/
- **ImageToSTL**: https://imagetostl.com/convert/file/obj/to/gltf

## Example Models to Download

### 1. Acoustic Guitar (iam3d_ar)
- **URL**: https://sketchfab.com/3d-models/acoustic-guitar-ce63135788664884adb41ec0e16aac0b
- **License**: CC Attribution
- **Quality**: High
- **File Size**: ~10MB
- **Textures**: Included

### 2. Electric Guitar Explorer (Skabl)
- **URL**: https://sketchfab.com/3d-models/electric-guitar-explorer-a7ffc570d3fe41c89b9dde195ab0faea
- **License**: CC Attribution
- **Quality**: High
- **File Size**: ~15MB
- **Textures**: Included

### 3. Classical Guitar
- Search Sketchfab for "classical guitar" with CC license
- Many free options available

## License Attribution

When using models with CC Attribution license, please credit the author:

Example:
```
"Acoustic Guitar" by iam3d_ar is licensed under CC Attribution 4.0
https://sketchfab.com/3d-models/acoustic-guitar-ce63135788664884adb41ec0e16aac0b
```

You can add this to your project documentation or credits page.

## Advanced: Creating Your Own Guitar Model

If you want to create your own guitar model in Blender:

1. **Follow tutorials**:
   - YouTube: "Blender guitar modeling tutorial"
   - Blender Guru, CG Geek, Grant Abbitt have great tutorials

2. **Use PBR materials**:
   - Use Principled BSDF shader
   - Add wood textures from Poly Haven or Texture Haven
   - Set up proper metallic/roughness values

3. **Export settings**:
   - Format: glTF Binary (.glb)
   - ✓ Apply Modifiers
   - ✓ UVs, Normals, Materials
   - ✓ Compress (optional, reduces file size)

4. **Optimize**:
   - Keep polygon count reasonable (< 100k triangles)
   - Use texture sizes: 1024x1024 or 2048x2048
   - Compress textures if needed

## Support

For issues or questions:
- Check the browser console for errors (F12)
- Verify the file path is correct
- Try a different model to isolate the issue
- Check the model works in other GLTF viewers: https://gltf-viewer.donmccurdy.com/

## Next Steps

Once you have a model loaded:
1. Try the different camera controls
2. Toggle auto-rotate on/off
3. Show/hide the grid
4. Try loading multiple different guitar models
5. Experiment with different camera positions

Enjoy your 3D guitar viewer! 🎸

