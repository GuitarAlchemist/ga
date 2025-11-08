# 3D Asset Downloads for BSP DOOM Explorer

This directory contains downloaded 3D assets (GLB files) ready for import into the Guitar Alchemist asset library.

## Directory Structure

```
Assets/Downloaded/
├── Decorative/     # Ankhs, scarabs, steles, hieroglyphics
├── Gems/           # Gems, crystals, precious stones
├── Lighting/       # Torches, braziers, lanterns
├── Architecture/   # Columns, pedestals, obelisks
└── Furniture/      # Jars, pottery, urns
```

## Quick Start

1. **Download assets** from the sources below
2. **Place GLB files** in the appropriate category folder
3. **Run import script**:
   ```powershell
   .\Scripts\import-assets.ps1 -Category Gems -License "CC0" -Source "Sketchfab"
   ```

## Recommended Free Assets

### Egyptian/Ancient Theme

#### Quaternius - Ultimate Low Poly Dungeon
- **URL**: https://quaternius.com/packs/ultimatelowpolydungeon.html
- **License**: CC0 (Public Domain)
- **Format**: GLB included
- **Assets**: Torches, braziers, columns, pedestals, pottery
- **Download**: Direct download from website
- **Category**: Multiple (Lighting, Architecture, Furniture)

#### Poly Pizza - Egyptian Collection
- **URL**: https://poly.pizza/search/egyptian
- **License**: CC-BY (varies by asset)
- **Format**: GLB
- **Assets**: Ankhs, scarabs, hieroglyphics, obelisks
- **Category**: Decorative

### Gems and Crystals

#### Sketchfab - Low Poly Gems
Search: "low poly gem" with filters:
- **Downloadable**: Yes
- **Format**: GLB
- **License**: CC0 or CC-BY
- **Recommended searches**:
  - "low poly ruby"
  - "low poly emerald"
  - "low poly crystal"
  - "low poly gemstone"

#### Quaternius - Ultimate Gems Pack
- **URL**: https://quaternius.com/ (check for gem packs)
- **License**: CC0
- **Format**: GLB
- **Category**: Gems

### Lighting

#### Quaternius - Ultimate Low Poly Dungeon (Torches)
- **URL**: https://quaternius.com/packs/ultimatelowpolydungeon.html
- **License**: CC0
- **Assets**: Wall torches, standing torches, braziers
- **Category**: Lighting

### Architecture

#### Quaternius - Ultimate Low Poly Dungeon (Architecture)
- **URL**: https://quaternius.com/packs/ultimatelowpolydungeon.html
- **License**: CC0
- **Assets**: Columns, pedestals, arches
- **Category**: Architecture

## Specific Asset Recommendations

### Priority 1: Essential Assets (5-10 assets)

1. **Torch (Wall-mounted)** - Quaternius Ultimate Low Poly Dungeon
2. **Torch (Standing)** - Quaternius Ultimate Low Poly Dungeon
3. **Brazier** - Quaternius Ultimate Low Poly Dungeon
4. **Ruby Gem** - Sketchfab (search "low poly ruby cc0")
5. **Emerald Gem** - Sketchfab (search "low poly emerald cc0")
6. **Sapphire Gem** - Sketchfab (search "low poly sapphire cc0")
7. **Pedestal** - Quaternius Ultimate Low Poly Dungeon
8. **Pottery Vase** - Quaternius Ultimate Low Poly Dungeon
9. **Column** - Quaternius Ultimate Low Poly Dungeon
10. **Ankh** - Poly Pizza or Sketchfab

### Priority 2: Enhanced Collection (10-15 additional assets)

11. **Amethyst Gem** - Sketchfab
12. **Diamond Gem** - Sketchfab
13. **Topaz Gem** - Sketchfab
14. **Scarab** - Poly Pizza or Sketchfab
15. **Stele (Hieroglyphics)** - Poly Pizza or Sketchfab
16. **Canopic Jar** - Poly Pizza or Sketchfab
17. **Obelisk (Small)** - Poly Pizza or Sketchfab
18. **Crystal Cluster** - Sketchfab
19. **Geode** - Sketchfab
20. **Lantern** - Quaternius or Sketchfab

## Download Instructions

### From Quaternius

1. Visit https://quaternius.com/
2. Find the pack (e.g., "Ultimate Low Poly Dungeon")
3. Click "Download" button
4. Extract ZIP file
5. Look for GLB files in the extracted folder
6. Copy GLB files to appropriate category folder

### From Sketchfab

1. Visit https://sketchfab.com/
2. Search for asset (e.g., "low poly ruby")
3. Apply filters:
   - **Downloadable**: Yes
   - **Format**: GLB
   - **License**: CC0 or CC-BY
4. Click on asset
5. Click "Download 3D Model" button
6. Select "glTF Binary (.glb)" format
7. Download and save to appropriate category folder
8. **Important**: Note the license and author for attribution

### From Poly Pizza

1. Visit https://poly.pizza/
2. Search for asset (e.g., "ankh")
3. Click on asset
4. Click "Download" button
5. Select GLB format if available
6. Save to appropriate category folder
7. **Important**: Note the license and author

## Import Process

### Single Category Import

```powershell
# Import all gems with CC0 license from Sketchfab
.\Scripts\import-assets.ps1 -Category Gems -License "CC0" -Source "Sketchfab"

# Import decorative items with CC-BY license
.\Scripts\import-assets.ps1 -Category Decorative -License "CC BY 4.0" -Source "Poly Pizza" -Author "ArtistName"
```

### Dry Run (Preview)

```powershell
# See what would be imported without actually importing
.\Scripts\import-assets.ps1 -DryRun
```

### Import All Categories

```powershell
# Import all assets from all categories
.\Scripts\import-assets.ps1 -License "CC0" -Source "Quaternius"
```

## Asset Optimization

If assets are too large or have too many polygons:

### Using Blender

1. Open GLB in Blender
2. Select mesh
3. Add Modifier → Decimate
4. Set Ratio to 0.5 (reduces poly count by 50%)
5. Apply modifier
6. File → Export → glTF 2.0 (.glb)

### Using gltf-transform (Command Line)

```bash
# Install (requires Node.js)
npm install -g @gltf-transform/cli

# Optimize GLB
gltf-transform optimize input.glb output.glb

# Compress textures
gltf-transform etc1s input.glb output.glb --quality 128
```

## License Tracking

Create a `LICENSES.txt` file in each category folder to track licenses:

```
# Gems/LICENSES.txt

ruby.glb
- Source: Sketchfab
- Author: ArtistName
- License: CC0
- URL: https://sketchfab.com/3d-models/...
- Downloaded: 2025-01-15

emerald.glb
- Source: Sketchfab
- Author: AnotherArtist
- License: CC BY 4.0
- URL: https://sketchfab.com/3d-models/...
- Downloaded: 2025-01-15
```

## Verification

After importing, verify assets are in MongoDB:

```powershell
cd GaCLI
dotnet run -- asset-list --verbose
```

Or check via API:
```
GET http://localhost:7001/api/assets
GET http://localhost:7001/api/assets?category=Gems
```

## Troubleshooting

### "No GLB files found"
- Ensure files are in the correct directory
- Check file extension is `.glb` (lowercase)
- Verify directory structure matches expected layout

### "Import failed"
- Check MongoDB is running
- Verify file is valid GLB format
- Check file size (very large files may timeout)
- Try optimizing the asset first

### "Asset not appearing in BSP Explorer"
- Verify import was successful: `dotnet run -- asset-list`
- Check browser console for loading errors
- Verify API is serving assets: `GET /api/assets/{id}/download`

## Next Steps

After importing assets:

1. **Test in BSP Explorer**: Start the app and verify assets load
2. **Check Performance**: Monitor FPS with assets loaded
3. **Adjust LOD**: Configure Level of Detail settings if needed
4. **Document**: Update LICENSES.md with all asset attributions

## Resources

- **Asset Download Guide**: `Docs/ASSET_DOWNLOAD_GUIDE.md`
- **Import Script**: `Scripts/import-assets.ps1`
- **CLI Documentation**: `GaCLI/README.md`
- **API Documentation**: `http://localhost:7001/swagger`

## Quick Reference

### Best Free Sources
1. **Quaternius** - https://quaternius.com/ (CC0, high quality)
2. **Poly Pizza** - https://poly.pizza/ (CC-BY, Google Poly archive)
3. **Sketchfab** - https://sketchfab.com/ (Various licenses, filter for free)

### Recommended Search Terms
- "low poly ankh"
- "low poly gem"
- "low poly torch"
- "low poly egyptian"
- "low poly crystal"
- "low poly dungeon"

### File Requirements
- **Format**: GLB (glTF Binary)
- **Poly Count**: < 10K triangles (preferred)
- **Texture Size**: 512x512 or 1024x1024 (preferred)
- **License**: CC0, CC-BY, or similar permissive license

