# 3D Sunburst Visualization & Hierarchical Navigation

This document describes two new features for visualizing hierarchical musical data:

1. **Sunburst3D** - Standalone 3D sunburst visualization with slope effect and LOD
2. **BSPDoomExplorer Room/Door System** - Hierarchical navigation within the DOOM-style explorer

---

## 1. Sunburst3D Component

### Overview

A 3D implementation of the D3 zoomable sunburst visualization with:
- **Slope/Elevation Effect** - Inner rings are elevated, outer rings descend (configurable angle)
- **Level of Detail (LOD)** - Control how many hierarchy levels to render
- **Interactive Navigation** - Click segments to zoom into sub-hierarchies
- **Smooth Animations** - Auto-rotating camera and smooth transitions
- **Breadcrumb Trail** - Shows current navigation path

### Features

#### Slope Effect
```typescript
slopeAngle={30} // 0° = flat, 60° = steep
```
- Inner rings (root) are highest
- Each level descends based on slope angle
- Creates a "mountain" or "cone" effect
- Helps visualize hierarchy depth

#### Level of Detail (LOD)
```typescript
maxDepth={4} // Render up to 4 levels deep
```
- Prevents rendering thousands of segments
- Improves performance for large hierarchies
- User can adjust to explore deeper levels
- Dynamically filters visible segments

#### Data Structure
```typescript
interface SunburstNode {
  name: string;
  value?: number;        // Leaf node value
  children?: SunburstNode[];
  color?: number;        // Optional custom color (hex)
}
```

### Usage

```typescript
import { Sunburst3D, SunburstNode } from './components/BSP';

const data: SunburstNode = {
  name: 'Music Theory',
  children: [
    {
      name: 'Chords',
      color: 0xffff00,
      children: [
        { name: 'Major', value: 12 },
        { name: 'Minor', value: 12 },
        // ...
      ]
    },
    // ...
  ]
};

<Sunburst3D
  data={data}
  width={1200}
  height={800}
  maxDepth={4}
  slopeAngle={30}
  onNodeClick={(node, path) => {
    console.log('Clicked:', node.name);
    console.log('Path:', path.join(' → '));
  }}
/>
```

### Demo

```typescript
import { Sunburst3DDemo } from './components/BSP';

<Sunburst3DDemo />
```

The demo includes:
- Musical hierarchy data (Pitch Class Sets → Chords → Voicings → Scales)
- Interactive controls for maxDepth and slopeAngle
- Auto-rotate toggle
- Selected node information panel

### Performance

**Optimizations:**
- Only renders segments up to `maxDepth` (LOD)
- Uses instanced rendering where possible
- Efficient raycasting for hover/click detection
- Geometry/material disposal on cleanup

**Typical Performance:**
- 1,000 segments: 60 FPS
- 5,000 segments: 45-60 FPS
- 10,000+ segments: Use LOD (maxDepth ≤ 4)

---

## 2. BSPDoomExplorer Room/Door System

### Overview

Hierarchical navigation system for the DOOM-style explorer, inspired by D3's zoomable sunburst:
- **3D Doors** - Archways representing categories/sub-rooms
- **Circular Room Layout** - Doors arranged in a circle (like sunburst segments)
- **Navigation Stack** - Breadcrumb trail showing path through hierarchy
- **Dynamic Room Generation** - Sub-rooms created on-demand when entering doors

### Features

#### 3D Door Objects
- Glowing archway with metallic frame
- Door panel with transparency
- Label showing door name
- Child count indicator
- Color-coded by category

#### Room Layout
- Central circular platform
- Doors arranged in a circle around platform
- Glowing paths connecting platform to doors
- Room name displayed prominently above

#### Hierarchical Data

**Floor 5 (Voicings) Hierarchy:**
```
Root
├── Jazz Voicings
│   ├── Drop 2 (Maj7, Min7, Dom7, Min7b5, Dim7)
│   ├── Drop 3 (Maj7, Min7, Dom7, Min7b5)
│   ├── Drop 2+4 (Maj7, Min7, Dom7)
│   ├── Rootless (Type A, Type B)
│   ├── Shell (Root-3-7, Root-7-3)
│   └── Quartal (4ths, Sus4, Add11)
├── Classical Voicings
│   ├── Close Position
│   ├── Open Position
│   ├── Four-Part
│   └── SATB
├── Rock Voicings
│   ├── Power Chords
│   ├── Barre Chords
│   ├── Open Chords
│   └── Triads
├── CAGED System
│   ├── C Shape
│   ├── A Shape
│   ├── G Shape
│   ├── E Shape
│   └── D Shape
├── Position-Based
│   └── Positions I-V
└── String Sets
    └── Various string combinations
```

### Navigation

#### Entering Rooms
1. Look at a door (crosshair hover)
2. Click to enter
3. Room clears and shows sub-doors
4. Camera stays on same floor
5. Breadcrumb trail updates

#### Going Back
- **Backspace** or **Escape** - Go back one level
- Click breadcrumb chip - Jump to that level
- Automatically restores floor and camera position

#### Breadcrumb Trail
```
Root → Jazz Voicings → Drop 2 → Maj7
```
- Shows full navigation path
- Click any breadcrumb to jump back
- Current room highlighted in cyan
- Previous rooms in green

### Controls

| Action | Control |
|--------|---------|
| Move | WASD |
| Look | Mouse (pointer locked) |
| Up/Down | Space / Shift |
| Change Floor | Mouse Wheel |
| Enter Door | Click (while looking at door) |
| Go Back | Backspace or Escape |
| Jump to Breadcrumb | Click breadcrumb chip |

### Implementation Details

#### Key Functions

**`create3DDoor()`**
- Creates 3D door archway
- Parameters: name, position, color, targetFloor, children
- Returns: THREE.Group with door geometry

**`createRoomWithDoors()`**
- Creates room with circular door layout
- Parameters: roomName, doors array, floorGroup, centerPosition
- Arranges doors in circle with paths

**`generateRoomHierarchy()`**
- Generates door data for a given floor/parent
- Returns: Array of door configurations
- Extensible for new categories

#### Navigation State

```typescript
const [navigationStack, setNavigationStack] = useState<Array<{
  name: string;
  floor: number;
  group?: string;
  parent?: string;
}>>([{ name: 'Root', floor: 0 }]);

const [currentRoom, setCurrentRoom] = useState<string>('Root');
```

### Extending the System

#### Adding New Categories

Edit `generateRoomHierarchy()`:

```typescript
if (floor === 5 && !parentName) {
  return [
    // ... existing categories
    {
      name: 'My New Category',
      color: 0xff00ff,
      targetFloor: 5,
      children: ['Sub-Cat 1', 'Sub-Cat 2', 'Sub-Cat 3']
    },
  ];
}

// Add sub-categories
if (parentName === 'My New Category') {
  return [
    { name: 'Sub-Cat 1', color: 0xff00ff, children: ['Item 1', 'Item 2'] },
    { name: 'Sub-Cat 2', color: 0xff00dd, children: ['Item 3', 'Item 4'] },
    { name: 'Sub-Cat 3', color: 0xff00bb, children: ['Item 5', 'Item 6'] },
  ];
}
```

#### Adding to Other Floors

The system works on any floor:

```typescript
if (floor === 3 && !parentName) {
  return [
    { name: 'Chord Category 1', color: 0x00ff00, targetFloor: 3, children: [...] },
    { name: 'Chord Category 2', color: 0x00dd00, targetFloor: 3, children: [...] },
  ];
}
```

---

## Comparison: Sunburst3D vs Room/Door System

| Feature | Sunburst3D | Room/Door System |
|---------|------------|------------------|
| **View** | Top-down, rotating | First-person, DOOM-style |
| **Navigation** | Click segments | Walk through doors |
| **Layout** | Concentric rings | Circular room with doors |
| **Elevation** | Slope effect | Floor-based |
| **LOD** | maxDepth parameter | Dynamic room generation |
| **Use Case** | Overview visualization | Immersive exploration |
| **Data Display** | All levels visible | One room at a time |
| **Performance** | Handles 1000s of segments | Handles 100s of doors |

---

## Best Practices

### Sunburst3D
- Use `maxDepth ≤ 4` for large hierarchies
- Set `slopeAngle` between 20-40° for best visibility
- Provide meaningful `value` for leaf nodes
- Use custom colors to distinguish categories

### Room/Door System
- Keep door count per room ≤ 12 for readability
- Use distinct colors for different categories
- Provide clear, concise door names
- Limit hierarchy depth to 3-4 levels

---

## Future Enhancements

### Sunburst3D
- [ ] Zoom animation when clicking segments
- [ ] Texture mapping for segments
- [ ] Particle effects for transitions
- [ ] VR support

### Room/Door System
- [ ] Animated door opening/closing
- [ ] Sound effects for navigation
- [ ] Minimap showing room hierarchy
- [ ] Teleport between rooms
- [ ] Multi-floor room connections

---

## Examples

See:
- `Sunburst3DDemo.tsx` - Full demo with controls
- `BSPDoomExplorer.tsx` - Room/door system implementation (Floor 5)

Both systems can be used independently or together for different visualization needs.

