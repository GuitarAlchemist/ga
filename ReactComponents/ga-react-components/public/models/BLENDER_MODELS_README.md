# Blender 3D Models for BSP DOOM Explorer

This directory contains Blender Python scripts to generate high-quality 3D models for the BSP DOOM Explorer and other visualizations.

## 📦 Available Models

### 1. **Ankh (☥)** - `ankh.glb`
- **Script**: `create_ankh.py`
- **Description**: Egyptian symbol of life with golden metallic finish
- **Features**:
  - Detailed torus loop
  - Smooth cylindrical staff and arms
  - Decorative spheres at endpoints
  - Gold material with emission glow
- **Use Case**: Reticle, navigation marker, Egyptian theme

### 2. **Stele (Stone Monument)** - `stele.glb`
- **Script**: `create_stele.py`
- **Description**: Ancient Egyptian stone monument with hieroglyphics
- **Features**:
  - Rounded top (traditional stele shape)
  - Hieroglyphic-like patterns
  - Stepped base platform
  - Sandstone material with noise texture
- **Use Case**: Floor markers, information displays, Egyptian architecture

### 3. **Scarab Beetle** - `scarab.glb`
- **Script**: `create_scarab.py`
- **Description**: Sacred Egyptian scarab beetle
- **Features**:
  - Detailed body with wing covers (elytra)
  - 6 articulated legs
  - Antennae
  - Turquoise faience material (metallic cyan)
  - Slight emission glow
- **Use Case**: Collectibles, decorative elements, Egyptian symbolism

### 4. **Pyramid Platform** - `pyramid.glb`
- **Script**: `create_pyramid.py`
- **Description**: Egyptian pyramid with stepped layers
- **Features**:
  - Main pyramid structure
  - 3 stepped layers (Egyptian style)
  - Corner obelisks
  - Golden capstone with emission
  - Sandstone base with noise texture
- **Use Case**: Floor platforms, level markers, architectural elements

### 5. **Lotus Flower** - `lotus.glb`
- **Script**: `create_lotus.py`
- **Description**: Sacred Egyptian lotus flower
- **Features**:
  - Multi-layered petals (inner and outer)
  - Translucent petal material
  - Stem and lily pad base
  - White/pink coloration
  - Subsurface scattering for realism
- **Use Case**: Decorative elements, nature theme, Egyptian symbolism

## 🚀 Quick Start

### Prerequisites

1. **Install Blender**:
   - Download from: https://www.blender.org/download/
   - Add Blender to your system PATH

2. **Verify Installation**:
   ```powershell
   blender --version
   ```

### Generate All Models (Automated)

Run the PowerShell script to generate all models at once:

```powershell
cd ReactComponents/ga-react-components/public/models
.\generate_all_models.ps1
```

This will:
- ✅ Check for Blender installation
- ✅ Generate all 5 models
- ✅ Show progress and file sizes
- ✅ Provide summary report

### Generate Individual Models

To generate a single model:

```powershell
# Example: Generate the ankh
blender --background --python create_ankh.py

# Example: Generate the stele
blender --background --python create_stele.py

# Example: Generate the scarab
blender --background --python create_scarab.py

# Example: Generate the pyramid
blender --background --python create_pyramid.py

# Example: Generate the lotus
blender --background --python create_lotus.py
```

### Generate with Blender UI (For Editing)

1. Open Blender
2. Go to **Scripting** workspace
3. Click **Open** and select a script (e.g., `create_ankh.py`)
4. Click **Run Script** (▶️ button)
5. The model will be created in the viewport
6. Export manually: **File → Export → glTF 2.0 (.glb)**

## 📊 Model Specifications

| Model | Vertices | Triangles | File Size | Material |
|-------|----------|-----------|-----------|----------|
| Ankh | ~2,000 | ~4,000 | ~115 KB | Gold (metallic) |
| Stele | ~3,500 | ~7,000 | ~180 KB | Sandstone (rough) |
| Scarab | ~4,000 | ~8,000 | ~200 KB | Turquoise (metallic) |
| Pyramid | ~2,500 | ~5,000 | ~150 KB | Sandstone + Gold |
| Lotus | ~5,000 | ~10,000 | ~250 KB | White/Pink (translucent) |

*Note: Actual sizes may vary based on Blender export settings*

## 🎨 Material Properties

### Gold Material (Ankh, Pyramid Capstone)
- **Base Color**: RGB(0.944, 0.776, 0.373) - Rich gold
- **Metallic**: 1.0 - Fully metallic
- **Roughness**: 0.2 - Shiny surface
- **Emission**: 0.3-0.5 - Subtle glow

### Sandstone Material (Stele, Pyramid)
- **Base Color**: RGB(0.76, 0.70, 0.50) - Sandy beige
- **Metallic**: 0.0 - Non-metallic
- **Roughness**: 0.9-0.95 - Very rough
- **Noise Texture**: Procedural variation

### Turquoise Faience (Scarab)
- **Base Color**: RGB(0.0, 0.6, 0.7) - Cyan/turquoise
- **Metallic**: 0.8 - Mostly metallic
- **Roughness**: 0.3 - Semi-shiny
- **Emission**: 0.2 - Magical glow

### Lotus Petal (Lotus)
- **Base Color**: RGB(0.95, 0.85, 0.90) - White with pink tint
- **Metallic**: 0.0 - Non-metallic
- **Roughness**: 0.4 - Soft surface
- **Subsurface**: 0.3 - Translucent petals

## 🔧 Customization

### Modify Colors

Edit the `Base Color` values in the material section of each script:

```python
# Example: Change ankh to silver
bsdf.inputs['Base Color'].default_value = (0.8, 0.8, 0.8, 1.0)  # Silver
```

### Modify Scale

Edit the final scale values:

```python
# Example: Make ankh larger
ankh_mesh.scale = (1.0, 1.0, 1.0)  # Instead of (0.5, 0.5, 0.5)
```

### Add More Detail

Increase subdivision levels:

```python
# Example: More smooth surfaces
subdiv_mod.levels = 3  # Instead of 2
```

## 📁 Integration with React

### Update Models3DTest.tsx

Add new models to the gallery:

```typescript
const models: Record<string, ModelMetadata> = {
  ankh: {
    name: 'Ankh ☥',
    path: '/models/ankh.glb',
    size: '115 KB',
    scale: 1.5,
    position: [0, 0, 0],
    rotation: [0, 0, 0],
  },
  stele: {
    name: 'Stele 𓉔',
    path: '/models/stele.glb',
    size: '180 KB',
    scale: 1.2,
    position: [0, -0.5, 0],
    rotation: [0, 0, 0],
  },
  scarab: {
    name: 'Scarab 𓆣',
    path: '/models/scarab.glb',
    size: '200 KB',
    scale: 1.5,
    position: [0, 0, 0],
    rotation: [0, 0, 0],
  },
  pyramid: {
    name: 'Pyramid 𓉼',
    path: '/models/pyramid.glb',
    size: '150 KB',
    scale: 1.0,
    position: [0, -0.5, 0],
    rotation: [0, 0, 0],
  },
  lotus: {
    name: 'Lotus 𓆸',
    path: '/models/lotus.glb',
    size: '250 KB',
    scale: 1.2,
    position: [0, 0, 0],
    rotation: [0, 0, 0],
  },
};
```

### Update BSPDoomExplorer.tsx

Add models to the MODEL_PATHS:

```typescript
const MODEL_PATHS: Record<string, string> = {
  // Egyptian models
  'ankh': '/models/ankh.glb',
  'stele': '/models/stele.glb',
  'scarab': '/models/scarab.glb',
  'pyramid': '/models/pyramid.glb',
  'lotus': '/models/lotus.glb',
  
  // Existing models...
  'guitar': '/models/guitar.glb',
  'guitar2': '/models/guitar2.glb',
};
```

## 🎯 Best Practices

### Performance
- ✅ Keep polygon count reasonable (< 10K triangles per model)
- ✅ Use LOD (Level of Detail) for distant objects
- ✅ Enable model caching in Three.js
- ✅ Compress textures when possible

### Quality
- ✅ Use smooth shading for organic shapes
- ✅ Add bevel modifiers for realistic edges
- ✅ Use procedural textures for variation
- ✅ Enable shadows (castShadow, receiveShadow)

### Workflow
- ✅ Test models in Blender viewport first
- ✅ Export as GLB (binary) for smaller file size
- ✅ Verify materials export correctly
- ✅ Check scale in Three.js (adjust if needed)

## 🐛 Troubleshooting

### Issue: "Blender not found"
**Solution**: Add Blender to your system PATH or use full path:
```powershell
& "C:\Program Files\Blender Foundation\Blender 4.0\blender.exe" --background --python create_ankh.py
```

### Issue: "Model appears black in Three.js"
**Solution**: 
- Check that materials are exported (`export_materials='EXPORT'`)
- Add proper lighting in Three.js scene
- Verify material nodes are connected correctly

### Issue: "Model is too large/small"
**Solution**: Adjust the final scale in the script or in Three.js:
```typescript
model.scale.multiplyScalar(2.0); // Make 2x larger
```

### Issue: "Hieroglyphics not visible on stele"
**Solution**: 
- Increase the number of glyphs in `create_hieroglyphic_pattern()`
- Adjust glyph depth (z-position)
- Add displacement modifier for deeper indentations

## 📚 Resources

- **Blender Documentation**: https://docs.blender.org/
- **Blender Python API**: https://docs.blender.org/api/current/
- **glTF 2.0 Specification**: https://www.khronos.org/gltf/
- **Three.js GLTFLoader**: https://threejs.org/docs/#examples/en/loaders/GLTFLoader
- **Egyptian Symbols**: https://en.wikipedia.org/wiki/Egyptian_hieroglyphs

## 🎨 Future Models

Potential models to add:
- [ ] Obelisk (tall monument)
- [ ] Sphinx (guardian statue)
- [ ] Papyrus scroll
- [ ] Canopic jar
- [ ] Eye of Horus
- [ ] Djed pillar
- [ ] Was scepter
- [ ] Cartouche (name plate)

## 📝 License

These Blender scripts are part of the Guitar Alchemist project.
Feel free to modify and extend them for your needs!

---

**Status**: ✅ **5 Models Ready to Generate**  
**Last Updated**: 2025-10-30  
**Blender Version**: 3.0+ (tested with 4.0)

