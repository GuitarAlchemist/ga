# 3D Asset Download Guide for BSP DOOM Explorer

This guide provides instructions for downloading and importing priority 3D assets for the BSP DOOM Explorer.

## Overview

The BSP DOOM Explorer requires 15-20 core 3D assets in GLB format to populate the game world. These assets should be:
- **Low-poly** (< 10K triangles preferred)
- **Optimized for real-time rendering**
- **Properly licensed** (CC0, CC BY, or similar permissive licenses)
- **GLB format** (GLTF binary)

## Asset Categories and Priorities

### 1. Egyptian/Ancient Theme (High Priority)
- **Ankh symbols** (2-3 variants)
- **Scarab beetles** (1-2 variants)
- **Hieroglyphic steles** (2-3 variants)
- **Canopic jars** (2-3 variants)
- **Obelisks** (1-2 variants, small scale)

### 2. Gems and Crystals (High Priority)
- **Colored gems** (5-6 variants: ruby, emerald, sapphire, amethyst, topaz, diamond)
- **Crystal clusters** (2-3 variants)
- **Geodes** (1-2 variants)

### 3. Lighting and Atmosphere (Medium Priority)
- **Torches** (2-3 variants: wall-mounted, standing)
- **Braziers** (1-2 variants)
- **Lanterns** (1-2 variants)

### 4. Decorative Elements (Medium Priority)
- **Pottery** (2-3 variants: vases, urns)
- **Pedestals** (1-2 variants)
- **Columns** (1-2 variants, small scale)

## Recommended Sources

### Sketchfab
- **URL**: https://sketchfab.com/
- **Search Tips**:
  - Use filters: "Downloadable", "GLB format", "Low-poly"
  - License filter: "CC0", "CC BY", "CC BY-SA"
  - Search terms: "low poly ankh", "low poly gem", "low poly torch", etc.
- **Download Process**:
  1. Find asset and click "Download 3D Model"
  2. Select "glTF Binary (.glb)" format
  3. Note the license and author information
  4. Save to a local directory (e.g., `Assets/Downloaded/`)

### CGTrader
- **URL**: https://www.cgtrader.com/
- **Search Tips**:
  - Filter by "Free" or "Low Price"
  - Filter by "Low-poly"
  - Check license terms carefully
- **Download Process**:
  1. Download the asset (may require account)
  2. Extract if zipped
  3. Convert to GLB if needed (use Blender or online converters)

### Poly Pizza (formerly Google Poly)
- **URL**: https://poly.pizza/
- **Notes**: Community-maintained archive of Google Poly assets
- **License**: Most assets are CC-BY

### Quaternius (Free Game Assets)
- **URL**: https://quaternius.com/
- **Notes**: High-quality, free game assets with CC0 license
- **Excellent for**: Generic game objects, nature elements

## Asset Preparation Checklist

Before importing, ensure each asset:
- [ ] Is in GLB format (not GLTF with separate files)
- [ ] Has reasonable poly count (< 10K triangles)
- [ ] Has proper scale (approximately 1 unit = 1 meter)
- [ ] Has textures embedded in GLB file
- [ ] License information is documented

## Importing Assets

### Single Asset Import

```bash
cd GaCLI
dotnet run -- asset-import path/to/model.glb \
  --name "Golden Ankh" \
  --category Decorative \
  --license "CC BY 4.0" \
  --source "Sketchfab" \
  --author "ArtistName"
```

### Batch Import from Directory

```bash
cd GaCLI
dotnet run -- asset-import path/to/assets/ \
  --directory \
  --category Gems \
  --license "CC0" \
  --source "Sketchfab" \
  --recursive
```

### Verify Import

```bash
cd GaCLI
dotnet run -- asset-list --verbose
```

## Asset Categories

The following categories are available:
- `Decorative` - General decorative objects (ankhs, scarabs, steles)
- `Gems` - Gems, crystals, precious stones
- `Lighting` - Torches, braziers, lanterns
- `Architecture` - Columns, pedestals, obelisks
- `Furniture` - Jars, pottery, urns
- `Nature` - Plants, rocks, natural elements
- `Interactive` - Objects that can be interacted with
- `Collectible` - Items that can be collected

## Storage and Serving

Assets are stored in MongoDB GridFS and served via the GaApi:
- **List all assets**: `GET /api/assets`
- **Get asset metadata**: `GET /api/assets/{id}`
- **Download GLB**: `GET /api/assets/{id}/download`
- **Filter by category**: `GET /api/assets?category=Gems`

## Optimization Tips

### If Assets Are Too Large
Use Blender to optimize:
1. Open GLB in Blender
2. Select mesh → Modifiers → Add Modifier → Decimate
3. Adjust ratio to reduce poly count
4. File → Export → glTF 2.0 (.glb)

### If Textures Are Too Large
Use texture compression:
1. Extract textures from GLB
2. Resize to 512x512 or 1024x1024
3. Re-embed in GLB using Blender or gltf-transform

### Command-line Tools
```bash
# Install gltf-transform (Node.js required)
npm install -g @gltf-transform/cli

# Optimize GLB
gltf-transform optimize input.glb output.glb

# Compress textures
gltf-transform etc1s input.glb output.glb --quality 128
```

## Example Asset List

Here's a suggested starter set (15 assets):

1. **Ankh** (gold) - Decorative
2. **Ankh** (silver) - Decorative
3. **Scarab** (blue) - Decorative
4. **Stele** (hieroglyphics) - Decorative
5. **Canopic Jar** (jackal head) - Furniture
6. **Ruby** - Gems
7. **Emerald** - Gems
8. **Sapphire** - Gems
9. **Amethyst** - Gems
10. **Diamond** - Gems
11. **Wall Torch** - Lighting
12. **Standing Torch** - Lighting
13. **Brazier** - Lighting
14. **Pottery Vase** - Furniture
15. **Small Pedestal** - Architecture

## License Tracking

Create a `LICENSES.md` file to track asset licenses:

```markdown
# Asset Licenses

## Ankh (Gold)
- **Source**: Sketchfab
- **Author**: ArtistName
- **License**: CC BY 4.0
- **URL**: https://sketchfab.com/3d-models/...
- **Date Downloaded**: 2025-01-15

## Ruby Gem
- **Source**: Quaternius
- **Author**: Quaternius
- **License**: CC0
- **URL**: https://quaternius.com/...
- **Date Downloaded**: 2025-01-15
```

## Next Steps

After downloading and importing assets:
1. Test asset loading in BSP DOOM Explorer
2. Verify rendering performance (FPS should remain > 60)
3. Adjust LOD settings if needed
4. Document any issues or optimization needs

## Troubleshooting

### Asset Not Appearing in Explorer
- Check MongoDB connection
- Verify asset was imported successfully: `dotnet run -- asset-list`
- Check browser console for loading errors

### Performance Issues
- Reduce poly count using Blender Decimate modifier
- Compress textures using gltf-transform
- Enable LOD system in BSP Explorer

### License Compliance
- Always document license and attribution
- Include attribution in game credits
- Respect license restrictions (e.g., non-commercial use)

## Resources

- **glTF Validator**: https://github.khronos.org/glTF-Validator/
- **Blender**: https://www.blender.org/
- **gltf-transform**: https://gltf-transform.donmccurdy.com/
- **Three.js GLB Viewer**: https://gltf-viewer.donmccurdy.com/

