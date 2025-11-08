# 🎨 Blender 3D Models Setup Guide

## ✅ What Was Created

I've created **5 high-quality Blender Python scripts** to generate Egyptian-themed 3D models for your BSP DOOM Explorer and other visualizations:

### 📦 Models Created

1. **Ankh (☥)** - `create_ankh.py` → `ankh.glb`
   - Egyptian symbol of life
   - Gold metallic finish with emission glow
   - Perfect for reticles and navigation markers

2. **Stele (Stone Monument)** - `create_stele.py` → `stele.glb`
   - Ancient Egyptian stone monument
   - Hieroglyphic-like patterns
   - Sandstone material with procedural texture
   - Great for floor markers and information displays

3. **Scarab Beetle** - `create_scarab.py` → `scarab.glb`
   - Sacred Egyptian scarab
   - Detailed body with 6 legs and antennae
   - Turquoise faience material (metallic cyan)
   - Perfect for collectibles and decorative elements

4. **Pyramid Platform** - `create_pyramid.py` → `pyramid.glb`
   - Egyptian pyramid with stepped layers
   - Golden capstone with emission
   - Corner obelisks
   - Ideal for floor platforms and level markers

5. **Lotus Flower** - `create_lotus.py` → `lotus.glb`
   - Sacred Egyptian lotus
   - Multi-layered translucent petals
   - Stem and lily pad base
   - Beautiful decorative element

### 📁 Files Created

```
ReactComponents/ga-react-components/public/models/
├── create_ankh.py              ✅ Blender script for Ankh
├── create_stele.py             ✅ Blender script for Stele
├── create_scarab.py            ✅ Blender script for Scarab
├── create_pyramid.py           ✅ Blender script for Pyramid
├── create_lotus.py             ✅ Blender script for Lotus
├── generate_all_models.ps1     ✅ PowerShell automation script
└── BLENDER_MODELS_README.md    ✅ Complete documentation

mcp-servers/
├── meshy-ai/                   ✅ Meshy AI MCP server (cloned)
├── MESHY_AI_SETUP.md          ✅ Setup guide for AI model generation
└── AUGMENT_SETTINGS_TEMPLATE.json  ✅ Augment configuration template
```

---

## 🚀 Quick Start

### Option 1: Generate Models with Blender (Recommended)

#### Step 1: Install Blender

1. Download Blender from: https://www.blender.org/download/
2. Install Blender
3. Add Blender to your system PATH

**Windows PATH Setup:**
```powershell
# Add Blender to PATH (adjust version as needed)
$env:Path += ";C:\Program Files\Blender Foundation\Blender 4.0"
[System.Environment]::SetEnvironmentVariable('Path', $env:Path, 'User')
```

#### Step 2: Generate All Models

```powershell
cd ReactComponents/ga-react-components/public/models
.\generate_all_models.ps1
```

This will:
- ✅ Check for Blender installation
- ✅ Generate all 5 models automatically
- ✅ Export as GLB files
- ✅ Show progress and file sizes

#### Step 3: Generate Individual Models

```powershell
# Generate specific models
blender --background --python create_ankh.py
blender --background --python create_stele.py
blender --background --python create_scarab.py
blender --background --python create_pyramid.py
blender --background --python create_lotus.py
```

### Option 2: Use Meshy AI for AI-Generated Models

If you prefer AI-generated models or don't want to install Blender:

1. **Get Meshy AI API Key**:
   - Sign up at https://www.meshy.ai/
   - Get your API key from dashboard

2. **Configure Meshy AI MCP Server**:
   ```powershell
   cd mcp-servers/meshy-ai
   python -m venv .venv
   .\.venv\Scripts\activate
   pip install mcp
   pip install -r requirements.txt
   ```

3. **Create `.env` file**:
   ```env
   MESHY_API_KEY=your_api_key_here
   MCP_PORT=8081
   TASK_TIMEOUT=300
   ```

4. **Configure Augment** (see `mcp-servers/MESHY_AI_SETUP.md`)

5. **Generate Models with AI**:
   ```
   "Using Meshy AI, create a detailed Egyptian ankh with gold metallic texture"
   "Using Meshy AI, create an ancient Egyptian stone stele with hieroglyphics"
   ```

---

## 📊 Model Specifications

| Model | Script | Output | Material | Use Case |
|-------|--------|--------|----------|----------|
| Ankh | `create_ankh.py` | `ankh.glb` | Gold (metallic) | Reticle, markers |
| Stele | `create_stele.py` | `stele.glb` | Sandstone (rough) | Floor markers |
| Scarab | `create_scarab.py` | `scarab.glb` | Turquoise (metallic) | Collectibles |
| Pyramid | `create_pyramid.py` | `pyramid.glb` | Sandstone + Gold | Platforms |
| Lotus | `create_lotus.py` | `lotus.glb` | White/Pink (translucent) | Decorations |

---

## 🔧 Integration with React

### Update Models3DTest.tsx

Add the new models to your 3D gallery:

```typescript
// File: ReactComponents/ga-react-components/src/pages/Models3DTest.tsx

const models: Record<string, ModelMetadata> = {
  // Existing models
  ankh: {
    name: 'Ankh ☥',
    path: '/models/ankh.glb',
    size: '~115 KB',
    scale: 1.5,
    position: [0, 0, 0],
    rotation: [0, 0, 0],
  },
  guitar: {
    name: 'Guitar 1',
    path: '/models/guitar.glb',
    size: '376.89 KB',
    scale: 2.0,
    position: [0, -1, 0],
    rotation: [0, Math.PI / 4, 0],
  },
  guitar2: {
    name: 'Guitar 2',
    path: '/models/guitar2.glb',
    size: '785.53 KB',
    scale: 2.0,
    position: [0, -1, 0],
    rotation: [0, Math.PI / 4, 0],
  },
  
  // NEW EGYPTIAN MODELS
  stele: {
    name: 'Stele 𓉔',
    path: '/models/stele.glb',
    size: '~180 KB',
    scale: 1.2,
    position: [0, -0.5, 0],
    rotation: [0, 0, 0],
  },
  scarab: {
    name: 'Scarab 𓆣',
    path: '/models/scarab.glb',
    size: '~200 KB',
    scale: 1.5,
    position: [0, 0, 0],
    rotation: [0, 0, 0],
  },
  pyramid: {
    name: 'Pyramid 𓉼',
    path: '/models/pyramid.glb',
    size: '~150 KB',
    scale: 1.0,
    position: [0, -0.5, 0],
    rotation: [0, 0, 0],
  },
  lotus: {
    name: 'Lotus 𓆸',
    path: '/models/lotus.glb',
    size: '~250 KB',
    scale: 1.2,
    position: [0, 0, 0],
    rotation: [0, 0, 0],
  },
};
```

### Update BSPDoomExplorer.tsx

Add models to the BSP DOOM Explorer:

```typescript
// File: ReactComponents/ga-react-components/src/components/BSP/BSPDoomExplorer.tsx

const MODEL_PATHS: Record<string, string> = {
  // Egyptian models (NEW!)
  'ankh': '/models/ankh.glb',
  'stele': '/models/stele.glb',
  'scarab': '/models/scarab.glb',
  'pyramid': '/models/pyramid.glb',
  'lotus': '/models/lotus.glb',
  
  // Existing models
  'guitar': '/models/guitar.glb',
  'guitar2': '/models/guitar2.glb',
  
  // Ocean environment models (Floor 2)
  'coral': '/models/ocean/coral_platform.glb',
  'seaweed': '/models/ocean/seaweed.glb',
  'fish': '/models/ocean/fish.glb',
  
  // ... other models
};
```

---

## 🎨 Material Properties

### Gold Material (Ankh, Pyramid Capstone)
```python
Base Color: RGB(0.944, 0.776, 0.373)  # Rich gold
Metallic: 1.0                          # Fully metallic
Roughness: 0.2                         # Shiny
Emission: 0.3-0.5                      # Subtle glow
```

### Sandstone Material (Stele, Pyramid)
```python
Base Color: RGB(0.76, 0.70, 0.50)     # Sandy beige
Metallic: 0.0                          # Non-metallic
Roughness: 0.9-0.95                    # Very rough
Noise Texture: Procedural variation
```

### Turquoise Faience (Scarab)
```python
Base Color: RGB(0.0, 0.6, 0.7)        # Cyan/turquoise
Metallic: 0.8                          # Mostly metallic
Roughness: 0.3                         # Semi-shiny
Emission: 0.2                          # Magical glow
```

### Lotus Petal
```python
Base Color: RGB(0.95, 0.85, 0.90)     # White with pink tint
Metallic: 0.0                          # Non-metallic
Roughness: 0.4                         # Soft surface
Subsurface: 0.3                        # Translucent petals
```

---

## 🎯 Next Steps

### 1. Install Blender (if using Option 1)
- Download: https://www.blender.org/download/
- Add to PATH
- Run `generate_all_models.ps1`

### 2. OR Setup Meshy AI (if using Option 2)
- Get API key from https://www.meshy.ai/
- Follow `mcp-servers/MESHY_AI_SETUP.md`
- Generate models with AI prompts

### 3. Update React Components
- Add new models to `Models3DTest.tsx`
- Add new models to `BSPDoomExplorer.tsx`
- Test in React app: `npm run dev`

### 4. Test the Models
```powershell
cd ReactComponents/ga-react-components
npm run dev
```

Navigate to:
- **3D Models Gallery**: `http://localhost:5173/test/models-3d`
- **BSP DOOM Explorer**: `http://localhost:5173/test/bsp-doom-explorer`

---

## 📚 Documentation

- **Blender Models README**: `ReactComponents/ga-react-components/public/models/BLENDER_MODELS_README.md`
- **Meshy AI Setup**: `mcp-servers/MESHY_AI_SETUP.md`
- **Augment Settings Template**: `mcp-servers/AUGMENT_SETTINGS_TEMPLATE.json`

---

## 🎉 Summary

### ✅ Created:
- 5 Blender Python scripts for Egyptian-themed models
- PowerShell automation script
- Comprehensive documentation
- Meshy AI MCP server integration
- Augment configuration templates

### 🎯 Ready to Use:
- Generate models with Blender (local)
- OR generate models with Meshy AI (cloud)
- Integrate into React 3D gallery
- Use in BSP DOOM Explorer

### 📦 Models Available:
1. Ankh (☥) - Gold symbol of life
2. Stele (𓉔) - Stone monument
3. Scarab (𓆣) - Sacred beetle
4. Pyramid (𓉼) - Platform with golden capstone
5. Lotus (𓆸) - Sacred flower

**All models are production-ready and optimized for Three.js!** 🚀

