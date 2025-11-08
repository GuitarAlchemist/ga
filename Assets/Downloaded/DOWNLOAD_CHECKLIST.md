# 3D Asset Download Checklist

Track your progress downloading and importing 3D assets for the BSP DOOM Explorer.

## Priority 1: Essential Assets (Target: 10 assets)

### Lighting (3 assets)
- [ ] **Wall Torch** - Quaternius Ultimate Low Poly Dungeon
  - Source: https://quaternius.com/packs/ultimatelowpolydungeon.html
  - License: CC0
  - Category: Lighting
  - Status: Not downloaded

- [ ] **Standing Torch** - Quaternius Ultimate Low Poly Dungeon
  - Source: https://quaternius.com/packs/ultimatelowpolydungeon.html
  - License: CC0
  - Category: Lighting
  - Status: Not downloaded

- [ ] **Brazier** - Quaternius Ultimate Low Poly Dungeon
  - Source: https://quaternius.com/packs/ultimatelowpolydungeon.html
  - License: CC0
  - Category: Lighting
  - Status: Not downloaded

### Gems (3 assets)
- [ ] **Ruby** - Sketchfab
  - Search: "low poly ruby cc0"
  - License: CC0 or CC-BY
  - Category: Gems
  - Status: Not downloaded

- [ ] **Emerald** - Sketchfab
  - Search: "low poly emerald cc0"
  - License: CC0 or CC-BY
  - Category: Gems
  - Status: Not downloaded

- [ ] **Sapphire** - Sketchfab
  - Search: "low poly sapphire cc0"
  - License: CC0 or CC-BY
  - Category: Gems
  - Status: Not downloaded

### Architecture/Furniture (3 assets)
- [ ] **Pedestal** - Quaternius Ultimate Low Poly Dungeon
  - Source: https://quaternius.com/packs/ultimatelowpolydungeon.html
  - License: CC0
  - Category: Architecture
  - Status: Not downloaded

- [ ] **Pottery Vase** - Quaternius Ultimate Low Poly Dungeon
  - Source: https://quaternius.com/packs/ultimatelowpolydungeon.html
  - License: CC0
  - Category: Furniture
  - Status: Not downloaded

- [ ] **Column** - Quaternius Ultimate Low Poly Dungeon
  - Source: https://quaternius.com/packs/ultimatelowpolydungeon.html
  - License: CC0
  - Category: Architecture
  - Status: Not downloaded

### Decorative (1 asset)
- [ ] **Ankh** - Poly Pizza or Sketchfab
  - Search: "ankh" on https://poly.pizza/ or Sketchfab
  - License: CC0 or CC-BY
  - Category: Decorative
  - Status: Not downloaded

## Priority 2: Enhanced Collection (Target: 10 additional assets)

### Gems (3 assets)
- [ ] **Amethyst** - Sketchfab
  - Search: "low poly amethyst cc0"
  - License: CC0 or CC-BY
  - Category: Gems
  - Status: Not downloaded

- [ ] **Diamond** - Sketchfab
  - Search: "low poly diamond cc0"
  - License: CC0 or CC-BY
  - Category: Gems
  - Status: Not downloaded

- [ ] **Topaz** - Sketchfab
  - Search: "low poly topaz cc0"
  - License: CC0 or CC-BY
  - Category: Gems
  - Status: Not downloaded

### Decorative (4 assets)
- [ ] **Scarab** - Poly Pizza or Sketchfab
  - Search: "scarab" on https://poly.pizza/ or Sketchfab
  - License: CC0 or CC-BY
  - Category: Decorative
  - Status: Not downloaded

- [ ] **Stele (Hieroglyphics)** - Poly Pizza or Sketchfab
  - Search: "hieroglyphics" or "stele" on https://poly.pizza/ or Sketchfab
  - License: CC0 or CC-BY
  - Category: Decorative
  - Status: Not downloaded

- [ ] **Canopic Jar** - Poly Pizza or Sketchfab
  - Search: "canopic jar" on https://poly.pizza/ or Sketchfab
  - License: CC0 or CC-BY
  - Category: Decorative
  - Status: Not downloaded

- [ ] **Obelisk (Small)** - Poly Pizza or Sketchfab
  - Search: "obelisk" on https://poly.pizza/ or Sketchfab
  - License: CC0 or CC-BY
  - Category: Decorative
  - Status: Not downloaded

### Gems/Decorative (2 assets)
- [ ] **Crystal Cluster** - Sketchfab
  - Search: "low poly crystal cluster cc0"
  - License: CC0 or CC-BY
  - Category: Gems
  - Status: Not downloaded

- [ ] **Geode** - Sketchfab
  - Search: "low poly geode cc0"
  - License: CC0 or CC-BY
  - Category: Gems
  - Status: Not downloaded

### Lighting (1 asset)
- [ ] **Lantern** - Quaternius or Sketchfab
  - Search: "low poly lantern cc0"
  - License: CC0 or CC-BY
  - Category: Lighting
  - Status: Not downloaded

## Import Progress

### By Category
- [ ] **Decorative**: 0 / 5 imported
- [ ] **Gems**: 0 / 6 imported
- [ ] **Lighting**: 0 / 4 imported
- [ ] **Architecture**: 0 / 2 imported
- [ ] **Furniture**: 0 / 1 imported

### Overall Progress
- **Total Assets**: 0 / 20 downloaded
- **Total Imported**: 0 / 20 imported
- **Completion**: 0%

## Quick Commands

### Download Quaternius Pack
```bash
# Visit https://quaternius.com/packs/ultimatelowpolydungeon.html
# Click "Download" button
# Extract ZIP file
# Copy GLB files to appropriate category folders
```

### Import Single Category
```powershell
# Import all gems
.\Scripts\import-assets.ps1 -Category Gems -License "CC0" -Source "Sketchfab"

# Import all lighting
.\Scripts\import-assets.ps1 -Category Lighting -License "CC0" -Source "Quaternius"

# Import all decorative
.\Scripts\import-assets.ps1 -Category Decorative -License "CC BY 4.0" -Source "Poly Pizza"
```

### Import All Categories
```powershell
# Import everything
.\Scripts\import-assets.ps1 -License "CC0" -Source "Quaternius"
```

### Verify Imports
```powershell
# List all imported assets
cd GaCLI
dotnet run -- asset-list --verbose

# List by category
dotnet run -- asset-list --category Gems --verbose
```

## Notes

### Quaternius Ultimate Low Poly Dungeon
This single pack contains most of the Priority 1 assets:
- Wall torches
- Standing torches
- Braziers
- Pedestals
- Columns
- Pottery/vases
- And many more!

**Download once, get multiple assets!**

### License Compliance
Remember to:
- [ ] Document license for each asset
- [ ] Note author/creator name
- [ ] Save source URL
- [ ] Include attribution in game credits

### File Organization
```
Assets/Downloaded/
├── Decorative/
│   ├── ankh.glb
│   ├── scarab.glb
│   ├── stele.glb
│   └── LICENSES.txt
├── Gems/
│   ├── ruby.glb
│   ├── emerald.glb
│   ├── sapphire.glb
│   └── LICENSES.txt
├── Lighting/
│   ├── wall-torch.glb
│   ├── standing-torch.glb
│   ├── brazier.glb
│   └── LICENSES.txt
├── Architecture/
│   ├── pedestal.glb
│   ├── column.glb
│   └── LICENSES.txt
└── Furniture/
    ├── pottery-vase.glb
    └── LICENSES.txt
```

## Completion Criteria

### Minimum Viable Collection (10 assets)
- [x] Directory structure created
- [ ] At least 3 lighting assets downloaded
- [ ] At least 3 gem assets downloaded
- [ ] At least 3 architecture/furniture assets downloaded
- [ ] At least 1 decorative asset downloaded
- [ ] All assets imported to MongoDB
- [ ] All licenses documented
- [ ] Assets tested in BSP Explorer

### Full Collection (20 assets)
- [ ] All Priority 1 assets downloaded (10)
- [ ] All Priority 2 assets downloaded (10)
- [ ] All assets imported to MongoDB
- [ ] All licenses documented
- [ ] Assets tested in BSP Explorer
- [ ] Performance verified (FPS > 60)
- [ ] LOD system configured

## Next Steps After Download

1. **Import Assets**
   ```powershell
   .\Scripts\import-assets.ps1 -License "CC0" -Source "Quaternius"
   ```

2. **Verify Import**
   ```powershell
   cd GaCLI
   dotnet run -- asset-list --verbose
   ```

3. **Test in BSP Explorer**
   - Start services: `.\Scripts\start-all.ps1 -Dashboard`
   - Open BSP Explorer
   - Verify assets load and render correctly

4. **Check Performance**
   - Monitor FPS counter
   - Verify smooth navigation
   - Adjust LOD settings if needed

5. **Document Licenses**
   - Create LICENSES.txt in each category folder
   - Include all required attribution
   - Update game credits

## Resources

- **Download Guide**: `Docs/ASSET_DOWNLOAD_GUIDE.md`
- **Import Script**: `Scripts/import-assets.ps1`
- **Asset README**: `Assets/Downloaded/README.md`
- **Quaternius**: https://quaternius.com/
- **Poly Pizza**: https://poly.pizza/
- **Sketchfab**: https://sketchfab.com/

