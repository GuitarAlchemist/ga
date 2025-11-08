# 3D Asset Links for BSP DOOM Explorer

## Quick Reference: Free 3D Models for Egyptian Pyramid + Alchemy Theme

### Direct Download Links (Sketchfab - CC Attribution)

#### Symbols & Icons
1. **Ankh (Egyptian Cross of Life)**
   - https://sketchfab.com/3d-models/ankh-egyptian-cross-of-life-f77a365d70a4415998fa96007bc9a888
   - License: CC Attribution
   - Format: GLB, FBX, OBJ

2. **Eye of Horus**
   - https://sketchfab.com/3d-models/eye-of-horus-072d6ad03cd740b9a46bf413e5c3ae15
   - Triangles: ~15.3k
   - License: CC Attribution

#### Gems & Precious Stones
3. **Gems (21 different shapes)**
   - https://sketchfab.com/3d-models/gems-21-different-shapes-1fb35b1aefae4d819dde9de90162602a
   - Triangles: ~3k
   - License: Free download
   - Perfect for alchemy ingredients

4. **Gemstone Pack (11 different cuts)**
   - https://sketchfab.com/3d-models/gemstone-pack-68c4ec3dd23247188884243ad8bb2492
   - Format: Blender file
   - Various cuts: emerald, ruby, sapphire

#### Jars & Vessels
5. **Ancient Egyptian Water Jar**
   - https://sketchfab.com/3d-models/ancient-egyptian-water-jar-53f768e11ed642ec8007cbadb0431f49
   - License: CC Attribution

6. **Low Poly Egyptian Jar**
   - https://sketchfab.com/3d-models/low-poly-egyptian-jar-c4d68970fc984fc6ac56890bfac34565
   - Optimized for real-time rendering

7. **Sci-fi Egyptian Canopic Jar with Neon Lights**
   - https://sketchfab.com/3d-models/sci-fi-egyptian-canopic-jar-with-neon-lights-d6fcfce67bd54a169def0e2f63e2d403
   - Perfect for alchemy theme with glowing effects

8. **Canopic Jar (EC379 & EC380)**
   - https://sketchfab.com/3d-models/canopic-jar-ec379-ec380-715576942a5349d6a846ed4cf30c8925
   - Museum quality

9. **Cosmetic Jar (Ancient Egypt)**
   - https://sketchfab.com/3d-models/cosmetic-jar-94207f3e1cbe4d0bb2acd5c26cba0e66
   - Small decorative jar

10. **Egyptian Predynastic Painted Pottery Jar**
    - https://sketchfab.com/3d-models/egyptian-predynastic-painted-pottery-jar-ae7aa0eeb915405e803fe7444343701c
    - Historical artifact

### Collection Links (Browse & Download)

#### Ankh Models
- **Sketchfab Ankh Collection**: https://sketchfab.com/tags/ankh
- **CGTrader Ankh Models**: https://www.cgtrader.com/3d-models/ankh (361 free & premium)
- **Free3D Ankh OBJ**: https://free3d.com/3d-models/obj-ankh

#### Eye of Horus Models
- **CGTrader Eye of Horus**: https://www.cgtrader.com/free-3d-models/eyeofhorus

#### Gems & Gemstones
- **CGTrader Gem Models**: https://www.cgtrader.com/3d-models/gem
- **CGTrader Gemstone Models**: https://www.cgtrader.com/3d-models/gemstone
- **Free3D Gemstones**: https://free3d.com/premium-3d-models/gemstones
- **Free3D Blender Gemstones**: https://free3d.com/premium-3d-models/blender-gemstone-gemstone
- **CRAFTSMANSPACE Gemstone Cuts**: https://www.craftsmanspace.com/free-3d-models/3d-models-of-various-gemstone-cuts.html

#### Jars & Vessels
- **Free3D Jars**: https://free3d.com/3d-models/jars
- **Sketchfab Ancient Jar**: https://sketchfab.com/3d-models/ancient-jar-714b04236caa4cdca3405436db42ff1a

#### Torches & Lighting
- **CGTrader Torch Models**: https://www.cgtrader.com/3d-models/torch
- **Free3D Torch Models**: https://free3d.com/3d-models/torch (17+ models)
- **Free3D Torches Collection**: https://free3d.com/premium-3d-models/torches
- **Clara.io Torch Library**: https://clara.io/library?gameCheck=true&query=torch
- **RenderHub Torches**: https://www.renderhub.com/3d-models/lighting/torches

#### Pyramids & Architecture
- **Free3D Pyramid Models**: https://free3d.com/3d-models/pyramid (21+ models)
- **BlenderKit Pyramid**: https://www.blenderkit.com/asset-gallery-detail/8ea653fa-00c5-4607-9697-e5317eebeda7/
- **CGTrader Egyptian Pyramid**: https://www.cgtrader.com/3d-models/egyptian-pyramid

#### Egyptian Artifacts
- **Free3D Egyptian Models**: https://free3d.com/premium-3d-models/egyptian (1089+ models)
- **Free3D Hieroglyphs**: https://free3d.com/premium-3d-models/hieroglyph
- **Free3D Artifacts**: https://free3d.com/3d-models/artifacts
- **Sketchfab British Museum Egyptian Objects**: https://sketchfab.com/britishmuseum/collections/egyptian-objects-451a426bdae644029d99e46f5022cd87
- **CGTrader Egypt Models**: https://www.cgtrader.com/3d-models/egypt

#### General Resources
- **Free3D Main Site**: https://free3d.com/
- **RigModels Ankh Search**: https://rigmodels.com/?searchkeyword=ankh

---

## Download & Import Workflow

### 1. Download Assets
```bash
# Create assets directory
mkdir -p Assets/3D/Egyptian
cd Assets/3D/Egyptian

# Download from Sketchfab (requires account)
# 1. Click "Download 3D Model"
# 2. Select format: GLB (preferred) or FBX/OBJ
# 3. Save to Assets/3D/Egyptian/
```

### 2. Import to Blender
```bash
# Open Blender
# File → Import → glTF 2.0 (.glb/.gltf)
# Or: File → Import → Wavefront (.obj)
```

### 3. Optimize for WebGPU
```python
# Blender Python script for optimization
import bpy

# Decimate geometry if needed
bpy.ops.object.modifier_add(type='DECIMATE')
bpy.context.object.modifiers["Decimate"].ratio = 0.5

# Apply modifiers
bpy.ops.object.modifier_apply(modifier="Decimate")

# Export as GLB
bpy.ops.export_scene.gltf(
    filepath="optimized.glb",
    export_format='GLB',
    export_texcoords=True,
    export_normals=True,
    export_materials='EXPORT'
)
```

### 4. Import to Guitar Alchemist
```bash
# Use GaCLI to import assets
dotnet run --project GaCLI -- asset import \
    --path "Assets/3D/Egyptian/ankh.glb" \
    --category "AlchemyProps" \
    --tags "symbol,ankh,egyptian"
```

---

## Asset Categories & Tags

### Categories
- `Architecture` - Pyramids, pillars, obelisks
- `AlchemyProps` - Ankh, Eye of Horus, flasks, scrolls
- `Gems` - Various gem cuts and precious stones
- `Jars` - Canopic jars, vessels, containers
- `Torches` - Light sources, braziers
- `Artifacts` - Scarabs, statues, masks, sarcophagi
- `Decorative` - General decoration

### Recommended Tags
- **Symbols**: `ankh`, `eye-of-horus`, `scarab`, `hieroglyph`
- **Materials**: `gold`, `stone`, `ceramic`, `glass`, `metal`
- **Effects**: `emissive`, `glow`, `reflective`, `transparent`
- **Size**: `small`, `medium`, `large`, `monumental`
- **Quality**: `low-poly`, `mid-poly`, `high-poly`
- **Theme**: `egyptian`, `alchemy`, `mystical`, `ancient`

---

## Optimization Guidelines

### Polygon Count Targets
- **Small props** (gems, small jars): < 1,000 triangles
- **Medium props** (ankh, torches, jars): 1,000 - 5,000 triangles
- **Large props** (statues, pillars): 5,000 - 10,000 triangles
- **Architecture** (pyramids, obelisks): 10,000 - 20,000 triangles

### Texture Guidelines
- **Resolution**: 512x512 to 2048x2048
- **Format**: PNG (source), KTX2/Basis (runtime)
- **Maps**: Albedo, Normal, Metallic, Roughness, Emissive
- **Atlasing**: Combine small props into texture atlases

### Material Setup
```typescript
// Example: Emissive gem material
const gemMaterial = new THREE.MeshStandardMaterial({
    color: 0x00ff00,        // Green emerald
    metalness: 0.1,
    roughness: 0.2,
    emissive: 0x00ff00,     // Glow color
    emissiveIntensity: 0.5, // Glow strength
    transparent: true,
    opacity: 0.9
});
```

---

## License Compliance

### CC Attribution (CC-BY)
- ✅ Free to use
- ✅ Free to modify
- ✅ Commercial use allowed
- ⚠️ **Must credit** the original author
- ⚠️ Include link to original model

### Example Attribution
```
3D Models:
- "Ankh (Egyptian Cross of Life)" by [Author] (https://sketchfab.com/...) 
  Licensed under CC Attribution
- "Eye of Horus" by [Author] (https://sketchfab.com/...)
  Licensed under CC Attribution
```

### Free Download
- ✅ Free to use
- ⚠️ Check individual license terms
- ⚠️ May have restrictions on commercial use
- ⚠️ May require attribution

---

## Integration Checklist

- [ ] Download 15-20 core assets
- [ ] Import to Blender and optimize
- [ ] Export as GLB format
- [ ] Import to MongoDB via GaCLI
- [ ] Tag and categorize assets
- [ ] Create material variants (emissive, reflective)
- [ ] Test in BSP DOOM Explorer
- [ ] Add to asset browser UI
- [ ] Document attribution
- [ ] Create asset placement guide

---

## Next Steps

1. **Download Priority Assets**
   - 3-5 Ankh models
   - 2-3 Eye of Horus models
   - 1 Gem pack (21 shapes)
   - 5-7 Jar/vessel models
   - 3-5 Torch models

2. **Create Asset Pipeline**
   - Blender optimization script
   - GLB export automation
   - MongoDB import tool
   - Asset browser UI

3. **Integrate with BSP Explorer**
   - Asset placement system
   - Material enhancement
   - LOD implementation
   - Performance testing

---

## References

- **ChatGPT Conversation**: "Free Blender models for BSP"
- **Implementation Plan**: [IMPLEMENTATION_PLAN_BLENDER_GROTHENDIECK.md](IMPLEMENTATION_PLAN_BLENDER_GROTHENDIECK.md)
- **Summary**: [SUMMARY_MCP_BLENDER_GROTHENDIECK.md](SUMMARY_MCP_BLENDER_GROTHENDIECK.md)

