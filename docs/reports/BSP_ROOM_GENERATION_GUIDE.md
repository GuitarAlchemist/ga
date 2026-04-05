# BSP DOOM Explorer - Room Generation System

## 🎯 Overview

This document describes the procedural room generation system for navigating music theory hierarchies in the BSP DOOM Explorer pyramid.

---

## 🏗️ Architecture

### Hierarchy Levels (Floors)

```
Floor 0: Set Classes        →    93 rooms
Floor 1: Forte Codes        →   115 rooms
Floor 2: Prime Forms        →   200 rooms
Floor 3: Chords             →   350 rooms
Floor 4: Chord Inversions   → 4,096 rooms
Floor 5: Chord Voicings     → 100,000+ rooms
```

### Room Generation Pipeline

```
1. BSP Space Partitioning
   ↓
2. Room Creation from Leaf Nodes
   ↓
3. Room Connection (Adjacency Detection)
   ↓
4. Music Data Assignment
   ↓
5. 3D Geometry Rendering
```

---

## 🔧 Core Components

### 1. RoomGenerator (`RoomGenerator.ts`)

**Purpose**: Generates room layout using Binary Space Partitioning (BSP) algorithm

**Key Features**:
- Recursive space subdivision
- Configurable room count and sizes
- Automatic adjacency detection
- Music theory data integration

**Algorithm**:
```typescript
1. Start with entire floor space as root node
2. Recursively split space:
   - Choose split axis (X or Z) based on aspect ratio
   - Split at 30-70% position (randomized)
   - Create left and right child nodes
   - Stop when:
     * Desired room count reached
     * Room too small to split
     * Max depth reached
3. Create rooms from leaf nodes
4. Connect adjacent rooms
5. Assign music data to each room
```

**Usage**:
```typescript
import { RoomGenerator } from './RoomGenerator';

const generator = new RoomGenerator({
  floorSize: 100,
  minRoomSize: 8,
  maxRoomSize: 20,
  roomCount: 93,
  floor: 0,
  musicCategories: ['Major Triads', 'Minor Triads', 'Diminished', ...]
});

const rooms = generator.generate();
```

### 2. RoomRenderer (`RoomRenderer.ts`)

**Purpose**: Creates 3D geometry for generated rooms

**Key Features**:
- Floor, wall, ceiling meshes
- Door placement for connections
- Room labels and decorations
- Music item visualization
- Dynamic lighting

**Room Components**:
```typescript
interface RoomMeshes {
  floor: THREE.Mesh;           // Textured floor plane
  walls: THREE.Mesh[];         // 4 walls (N, S, E, W)
  ceiling?: THREE.Mesh;        // Optional ceiling
  doors: THREE.Mesh[];         // Glowing doors to connected rooms
  decorations: THREE.Object3D[]; // Labels, items, lights
}
```

**Usage**:
```typescript
import { RoomRenderer } from './RoomRenderer';

const renderer = new RoomRenderer(sceneGroup, wallHeight=8, wallThickness=0.5);

rooms.forEach(room => {
  const meshes = renderer.renderRoom(room);
  // Meshes automatically added to scene
});
```

---

## 🎨 Room Types

### Hub
- **Purpose**: Central gathering point with multiple connections
- **Size**: Large (2x average)
- **Features**: Multiple doors, bright lighting, prominent label
- **Use Case**: Main entry points, category centers

### Chamber
- **Purpose**: Standard room for displaying music items
- **Size**: Average
- **Features**: 2-4 doors, moderate lighting, item displays
- **Use Case**: Most common room type

### Gallery
- **Purpose**: Large display room for important collections
- **Size**: Very large (>2x average)
- **Features**: Many items, high ceiling, multiple lights
- **Use Case**: Showcasing major chord families, scale collections

### Corridor
- **Purpose**: Connecting passage between rooms
- **Size**: Small (<0.5x average)
- **Features**: No ceiling, minimal decoration, 2 doors
- **Use Case**: Transitions, pathways

### Sanctuary
- **Purpose**: Special room for rare/important items
- **Size**: Medium-large
- **Features**: Unique materials, special lighting, few connections
- **Use Case**: Rare voicings, special chord types

---

## 🎵 Music Data Integration

### Floor-Specific Categories

**Floor 0 (Set Classes)**:
```typescript
categories: [
  'Chromatic', 'Diatonic', 'Pentatonic', 'Hexatonic',
  'Octatonic', 'Whole Tone', 'Augmented', 'Diminished'
]
```

**Floor 1 (Forte Codes)**:
```typescript
categories: [
  '3-11 (Major/Minor Triad)', '4-23 (Quartal)',
  '5-35 (Pentatonic)', '7-35 (Diatonic)', ...
]
```

**Floor 2 (Prime Forms)**:
```typescript
categories: [
  '[0,4,7] (Major)', '[0,3,7] (Minor)',
  '[0,3,6] (Diminished)', '[0,4,8] (Augmented)', ...
]
```

**Floor 3 (Chords)**:
```typescript
categories: [
  'Major', 'Minor', 'Dominant 7th', 'Major 7th',
  'Minor 7th', 'Diminished 7th', 'Half-Diminished', ...
]
```

**Floor 4 (Chord Inversions)**:
```typescript
categories: [
  'Root Position', '1st Inversion', '2nd Inversion',
  '3rd Inversion', 'Drop 2', 'Drop 3', ...
]
```

**Floor 5 (Chord Voicings)**:
```typescript
categories: [
  'Jazz Voicings', 'Classical Voicings', 'Rock Voicings',
  'CAGED System', 'Position-Based', 'String Sets'
]
```

### Data Assignment

Each room receives:
```typescript
musicData: {
  floor: number;              // 0-5
  category: string;           // Category name
  items: string[];            // List of music items in room
  color: number;              // Hex color for category
  description: string;        // Human-readable description
}
```

---

## 🚪 Navigation System

### Door Mechanics

**Door Creation**:
- Doors placed at room boundaries
- Glowing material with category color
- Interactive (clickable/walkable)
- Labeled with destination room

**Door Interaction**:
```typescript
// On door click/collision:
1. Fade out current room
2. Transition camera to new room
3. Fade in new room
4. Update minimap
5. Update HUD (current room info)
```

### Pathfinding

**A* Algorithm** for optimal path between rooms:
```typescript
function findPath(startRoom: Room, endRoom: Room): Room[] {
  // Use A* with Manhattan distance heuristic
  // Returns array of rooms to traverse
}
```

**Auto-Navigation**:
- Click on minimap to auto-navigate
- Click on distant room to find path
- Follow glowing path markers

---

## 🎮 Integration with BSP DOOM Explorer

### Step 1: Import Modules

```typescript
import { RoomGenerator, RoomGenerationConfig } from './RoomGenerator';
import { RoomRenderer } from './RoomRenderer';
```

### Step 2: Generate Rooms for Each Floor

```typescript
const generateFloorRooms = (floor: number, floorSize: number) => {
  const configs: Record<number, RoomGenerationConfig> = {
    0: { floorSize, minRoomSize: 10, maxRoomSize: 20, roomCount: 93, floor: 0, musicCategories: [...] },
    1: { floorSize, minRoomSize: 8, maxRoomSize: 18, roomCount: 115, floor: 1, musicCategories: [...] },
    2: { floorSize, minRoomSize: 8, maxRoomSize: 16, roomCount: 200, floor: 2, musicCategories: [...] },
    3: { floorSize, minRoomSize: 6, maxRoomSize: 14, roomCount: 350, floor: 3, musicCategories: [...] },
    4: { floorSize, minRoomSize: 4, maxRoomSize: 10, roomCount: 100, floor: 4, musicCategories: [...] }, // Sample of 4096
    5: { floorSize, minRoomSize: 3, maxRoomSize: 8, roomCount: 200, floor: 5, musicCategories: [...] },  // Sample of 100k
  };

  const generator = new RoomGenerator(configs[floor]);
  return generator.generate();
};
```

### Step 3: Render Rooms

```typescript
const renderFloorRooms = (rooms: Room[], floorGroup: THREE.Group) => {
  const renderer = new RoomRenderer(floorGroup);
  
  rooms.forEach(room => {
    renderer.renderRoom(room);
  });
};
```

### Step 4: Add to BSPDoomExplorer.tsx

```typescript
// In buildFloors function:
for (let i = 0; i < floorData.length; i++) {
  const floorGroup = new THREE.Group();
  floorGroup.position.y = i * 20;
  
  // Generate and render rooms
  const rooms = generateFloorRooms(i, floorSize);
  renderFloorRooms(rooms, floorGroup);
  
  parent.add(floorGroup);
  floorGroupsRef.current.push(floorGroup);
}
```

---

## 🎨 Visual Design

### Color Scheme

**Floor-Based Colors**:
```typescript
const FLOOR_COLORS = {
  0: 0x8B4513, // Brown (Set Classes - Earth)
  1: 0x4169E1, // Royal Blue (Forte Codes - Water)
  2: 0x32CD32, // Lime Green (Prime Forms - Nature)
  3: 0xFF8C00, // Dark Orange (Chords - Fire)
  4: 0x9370DB, // Medium Purple (Inversions - Air)
  5: 0xFFD700, // Gold (Voicings - Light)
};
```

**Category-Based Colors**:
- Hue rotation based on category index
- Saturation: 70%
- Lightness: 50%

### Materials

**Floors**:
- Marble texture with procedural noise
- Emissive glow matching category color
- Roughness: 0.8, Metalness: 0.2

**Walls**:
- Stone texture
- Roughness: 0.9, Metalness: 0.1
- Sanctuary walls: Golden tint

**Doors**:
- Glowing material
- Emissive intensity: 0.3
- Transparency: 0.8
- Pulsing animation

---

## 🚀 Performance Optimization

### Level of Detail (LOD)

```typescript
// Only render rooms within view distance
const RENDER_DISTANCE = 50;

rooms.forEach(room => {
  const distance = camera.position.distanceTo(room.center);
  room.visible = distance < RENDER_DISTANCE;
});
```

### Instanced Rendering

```typescript
// Use instanced meshes for repeated elements
const doorGeometry = new THREE.BoxGeometry(3, 6, 0.2);
const doorMaterial = new THREE.MeshPhysicalMaterial({...});
const instancedDoors = new THREE.InstancedMesh(doorGeometry, doorMaterial, totalDoors);
```

### Occlusion Culling

```typescript
// Don't render rooms behind walls
const frustum = new THREE.Frustum();
frustum.setFromProjectionMatrix(camera.projectionMatrix);

rooms.forEach(room => {
  room.visible = frustum.containsPoint(room.center);
});
```

---

## 📊 Statistics

### Room Counts by Floor

| Floor | Name | Rooms | Avg Size | Total Area |
|-------|------|-------|----------|------------|
| 0 | Set Classes | 93 | 15×15 | 20,925 |
| 1 | Forte Codes | 115 | 13×13 | 19,435 |
| 2 | Prime Forms | 200 | 12×12 | 28,800 |
| 3 | Chords | 350 | 10×10 | 35,000 |
| 4 | Inversions | 100* | 7×7 | 4,900 |
| 5 | Voicings | 200* | 5×5 | 5,000 |

*Sampled subset for performance

---

## 🎯 Next Steps

1. **Integrate with Meshy AI**:
   - Generate unique 3D models for each room type
   - Create Egyptian-themed decorations
   - Generate custom door designs

2. **Add Interactive Elements**:
   - Clickable music items
   - Playable chord previews
   - Visual fretboard displays

3. **Enhance Navigation**:
   - Minimap with room layout
   - Breadcrumb trail
   - Teleportation between floors

4. **Add Sound**:
   - Ambient music per floor
   - Chord playback on item interaction
   - Footstep sounds

5. **Multiplayer**:
   - Shared exploration
   - Collaborative learning
   - Music jam sessions

---

**Status**: ✅ **READY FOR INTEGRATION**  
**Files Created**: `RoomGenerator.ts`, `RoomRenderer.ts`  
**Next**: Integrate into `BSPDoomExplorer.tsx`  
**Ready to explore music theory in 3D! 🎸🎹🎵**

