# BSP DOOM Explorer - Cylindrical Stele Library

## Overview

The BSP DOOM Explorer Floor 0 features a **cylindrical library of curved stone steles** (monuments) arranged in a circle. Each stele represents one cardinality group of pitch class sets, with detailed engravings and embedded crystals marking each set class.

## Architecture

### Cylindrical Formation

The 6 steles form a complete cylinder with proper spacing:

```
                Trichords
                ╱═════╲
               ╱       ╲
              ╱         ╲
             ╱           ╲
            ╱             ╲
           ║               ║
    Octa   ║               ║   Tetra
    chords ║               ║   chords
           ║               ║
            ╲             ╱
             ╲           ╱
              ╲         ╱
               ╲       ╱
    Septa       ╲═════╱      Penta
    chords    Hexachords     chords
```

**Key Measurements:**
- **Total steles:** 6 (one per cardinality: 3, 4, 5, 6, 7, 8)
- **Angular coverage:** 330° of stone + 30° of gaps
- **Gap size:** 5° between each stele
- **Stele arc:** ~55° per stele
- **Height:** **DYNAMIC** - Each stele sized to fit all its items (12-50 items)
- **Thickness:** 0.5 units
- **Radius:** floorSize × 0.35 (~14 units for default floor)

**Camembert Design:**
- Each section is a separate "slice" of the cylinder
- Heights vary based on content (Hexachords tallest at 44 units, Trichords shortest at 13.6 units)
- All items visible on each section - no overflow or truncation

### Stele Groups

Each stele (camembert section) is **dynamically sized** to fit all its items - the height matches the stack!

| Group | Cardinality | Items | Color | Arc Span | Height |
|-------|-------------|-------|-------|----------|--------|
| Trichords | 3-note | 12 | Purple gradient | 55° | 13.6 units |
| Tetrachords | 4-note | 29 | Purple gradient | 55° | 27.2 units |
| Pentachords | 5-note | 38 | Purple gradient | 55° | 34.4 units |
| Hexachords | 6-note | 50 | Purple gradient | 55° | 44.0 units |
| Septachords | 7-note | 38 | Purple gradient | 55° | 34.4 units |
| Octachords | 8-note | 29 | Purple gradient | 55° | 27.2 units |

**Total:** 93 set classes (OPTIC equivalence classes)

**Height Formula:** `headerSpace (3) + (itemCount × lineHeight (0.8)) + 1`

## Engraving Format

Each item is engraved with detailed contextual information:

### Format Structure

```
◆ [Forte] | [Notes] | [Type] | [Character]
```

### Examples

**Trichords:**
```
◆ 3-1  | 3-note | Trichord | Chromatic
◆ 3-2  | 3-note | Trichord | Compact
◆ 3-3  | 3-note | Trichord | Compact
◆ 3-4  | 3-note | Trichord | Compact
◆ 3-5  | 3-note | Trichord | Diatonic
◆ 3-6  | 3-note | Trichord | Diatonic
◆ 3-7  | 3-note | Trichord | Diatonic
◆ 3-8  | 3-note | Trichord | Distributed
```

**Tetrachords:**
```
◆ 4-1  | 4-note | Tetrachord | Chromatic
◆ 4-2  | 4-note | Tetrachord | Compact
◆ 4-Z15| 4-note | Tetrachord | Distributed
```

### Detail Components

1. **Forte Number** - Standard pitch class set notation (e.g., 3-1, 4-Z15)
2. **Cardinality** - Number of pitch classes (3-note, 4-note, etc.)
3. **Set Class Type** - Trichord, Tetrachord, Pentachord, Hexachord, Septachord, Octachord
4. **Character Hint** - Based on set number:
   - **Chromatic** (set #1) - Tightly packed semitones
   - **Compact** (sets #2-3) - Close intervals
   - **Diatonic** (sets #4-6) - Scale-like patterns ⭐ **SPECIAL GOLDEN STYLING**
   - **Distributed** (sets #7+) - Wider intervals

### Special Styling for Diatonic Items

**Diatonic items (sets #4-6) have distinctive golden styling:**
- **Text Color:** `0xd4af37` (gold) instead of `0x4a4035` (dark brown)
- **Crystal Color:** `0xd4af37` (gold) instead of group color (purple)
- **Crystal Emissive Intensity:** 0.7 (brighter) instead of 0.5
- **Crystal Metalness:** 0.4 (more metallic) instead of 0.2

This makes diatonic sets (which are musically significant scale-like patterns) stand out visually!

## Visual Elements

### Stone Material

**Weathered Ancient Stone:**
- Color: `0x8a7f6f` (warm gray-brown)
- Roughness: 0.9 (very rough, non-reflective)
- Metalness: 0.0 (no metallic properties)
- Flat shading: false (smooth lighting)

**Decorative Base:**
- Color: `0x6a5f4f` (darker brown)
- Roughness: 0.85
- Metalness: 0.0

### Crystal Markers

**Glowing Octahedron Crystals:**
- Geometry: OctahedronGeometry
- Size: 0.18 + (cardinality × 0.03) units
  - 3-note: 0.27 units
  - 4-note: 0.30 units
  - 5-note: 0.33 units
  - 6-note: 0.36 units
  - 7-note: 0.39 units
  - 8-note: 0.42 units
- Material: MeshPhysicalMaterial
  - Color: Group color (purple gradient)
  - Emissive: Group color
  - Emissive intensity: 0.5
  - Transmission: 0.15 (slight transparency)
  - Clearcoat: 1.0 (glass-like finish)
  - IOR: 2.0 (refractive index)

### Text Engraving

**Title (Group Name):**
- Position: Top of stele (height - 1.5)
- Color: Group color (purple gradient)
- Size: 0.8 units
- Rotation: Tangent to curve (centerAngle - π/2)

**Item Details:**
- Position: Stacked vertically, 0.8 units apart
- Color: `0x4a4035` (dark brown) OR `0xd4af37` (gold for diatonic items)
- Size: 0.25 units
- Rotation: Tangent to curve (centerAngle - π/2)
- Max items: **ALL items displayed** (stele height is dynamic)

## Text Orientation

**Natural Engraving:**
- Text follows the curve tangentially
- Rotation: `centerAngle - Math.PI / 2`
- NOT rotated to face the user
- Readable when standing in front of each stele
- Positioned exactly on the cylinder radius

**Positioning:**
```typescript
const textX = Math.cos(centerAngle) * curveRadius;
const textZ = Math.sin(centerAngle) * curveRadius;
textLabel.rotation.y = centerAngle - Math.PI / 2;
```

## Spacing Algorithm

```typescript
const totalGroups = 6;
const spacingAngle = Math.PI / 180 * 5;  // 5 degrees per gap
const availableAngle = (Math.PI * 2) - (totalGroups * spacingAngle);
const anglePerStele = availableAngle / totalGroups;

// Distribution:
// - 6 steles × 55° = 330° of stone
// - 6 gaps × 5° = 30° of space
// - Total: 360° complete cylinder
```

## User Experience

### Walking Around the Cylinder

**Position 1 - In front of a stele:**
- Title clearly readable
- Item details clearly readable
- Crystals visible and glowing
- Text naturally oriented on curve

**Position 2 - Between steles:**
- Gap visible (5° spacing)
- Adjacent steles' text at angles
- Both steles visible but text less readable

**Position 3 - Inside the cylinder:**
- All 6 steles visible simultaneously
- Can see the complete circular arrangement
- Text on all steles visible from different angles

### Interactive Elements

**All elements have userData for interaction:**
```typescript
userData: {
  type: 'element',
  name: elementName,        // e.g., "3-1"
  tonalityType: floor.name, // "Set Classes"
  groupName: group.name,    // e.g., "Trichords"
  depth: 0,                 // Floor index
  cardinality: 3,           // Number of notes
}
```

## Technical Implementation

### Geometry Creation

**Curved Stele (Dynamic Height):**
```typescript
// Calculate height based on number of items
const lineHeight = 0.8;
const headerSpace = 3;
const itemCount = groupElements.length;
const steleHeight = headerSpace + (itemCount * lineHeight) + 1;

const steleGeometry = new THREE.CylinderGeometry(
  curveRadius + steleThickness / 2,  // Top radius
  curveRadius + steleThickness / 2,  // Bottom radius
  steleHeight,                        // Height: DYNAMIC (13.6 - 44 units)
  32,                                 // Radial segments (smooth curve)
  1,                                  // Height segments
  false,                              // Not open-ended
  startAngle,                         // Start of arc
  anglePerStele                       // Angular span (~55°)
);
```

**Curved Base:**
```typescript
const baseGeometry = new THREE.CylinderGeometry(
  curveRadius + steleThickness / 2 + 0.2,  // Slightly wider
  curveRadius + steleThickness / 2 + 0.2,
  0.3,                                      // Height
  32,                                       // Radial segments
  1,
  false,
  startAngle,
  anglePerStele
);
```

### Crystal Positioning

```typescript
// Crystals slightly offset from text, on the surface
const crystalAngle = centerAngle - 0.05; // Slightly to the left
const crystalX = Math.cos(crystalAngle) * (curveRadius + 0.1);
const crystalZ = Math.sin(crystalAngle) * (curveRadius + 0.1);
crystal.position.set(crystalX, textY, crystalZ);
crystal.rotation.y = crystalAngle - Math.PI / 2;
```

## MCP Integration

The stele library is fully integrated with the Three.js MCP server for AI-controlled manipulation:

**Available Commands:**
- `addObject` - Add 3D objects to the scene
- `moveObject` - Move objects to new positions
- `removeObject` - Remove objects from scene
- `startRotation` - Animate object rotation
- `stopRotation` - Stop object rotation
- `getSceneState` - Query current scene state

**WebSocket Connection:**
- Server: `ws://localhost:8082`
- Bidirectional communication
- Real-time scene updates

## Future Enhancements

### Potential Improvements

1. **Interactive Highlighting** - Highlight stele on hover
2. **Detail Panels** - Show prime form and interval vector on click
3. **Audio Playback** - Play the pitch class set when clicked
4. **Filtering** - Show/hide specific character types (chromatic, diatonic, etc.)
5. **Search** - Find specific Forte numbers
6. **Connections** - Show relationships between sets (subset/superset)
7. **Z-relation Highlighting** - Highlight Z-related pairs
8. **Complement Visualization** - Show complement relationships

### Performance Optimizations

1. **LOD (Level of Detail)** - Reduce geometry detail when far away
2. **Instanced Rendering** - Use instancing for crystals
3. **Text Texture Atlas** - Pre-render text to textures
4. **Frustum Culling** - Only render visible steles

## References

- **Forte Number System** - Allen Forte's pitch class set classification
- **OPTIC Equivalence** - Octave, Permutation, Transposition, Inversion, Cardinality
- **Harmonious App** - https://harmoniousapp.net/p/ec/Equivalence-Groups
- **Set Theory** - Post-tonal music theory and analysis

---

**Last Updated:** 2025-10-29  
**Component:** BSPDoomExplorer.tsx  
**Location:** ReactComponents/ga-react-components/src/components/BSP/

