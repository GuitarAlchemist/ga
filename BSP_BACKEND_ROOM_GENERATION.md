# BSP Backend Room Generation System

## Overview

The BSP DOOM Explorer now uses **backend-generated rooms** with real music theory data instead of frontend procedural generation. This provides:

- ✅ **Authentic Music Data**: Real set classes, Forte codes, chords, and voicings from the GA music theory library
- ✅ **Consistent Layouts**: Reproducible room generation using seeds
- ✅ **Performance**: Rooms generated once on the server, cached on the client
- ✅ **Scalability**: Handles 100,000+ voicings efficiently with sampling

---

## Architecture

### Backend (C# / ASP.NET Core)

```
Apps/ga-server/GaApi/
├── Controllers/
│   ├── MusicRoomController.cs      # NEW: Music theory room generation
│   └── BSPRoomController.cs        # Existing: Generic BSP dungeon generation
├── Models/
│   └── BSPModels.cs                # DTOs for rooms, corridors, music data
└── Services/
    └── (uses existing music theory services)

Common/GA.Business.Core/
├── Spatial/
│   └── BSPRoomGenerator.cs         # BSP algorithm implementation
└── Atonal/
    ├── SetClass.cs                 # 93 set classes
    ├── ForteNumber.cs              # 115 Forte codes
    └── PitchClassSet.cs            # 4,096 pitch class sets
```

### Frontend (TypeScript / React / Three.js)

```
ReactComponents/ga-react-components/src/components/BSP/
├── MusicRoomLoader.ts              # NEW: Fetches rooms from API
├── RoomRenderer.ts                 # Renders 3D geometry (existing)
└── BSPDoomExplorer.tsx             # Main component (updated)
```

---

## API Endpoints

### 1. Generate Music Rooms for Floor

**Endpoint**: `GET /api/music-rooms/floor/{floor}`

**Parameters**:
- `floor` (path): Floor number (0-5)
- `floorSize` (query): Size of the floor (default: 100)
- `seed` (query, optional): Seed for reproducible generation

**Response**:
```json
{
  "success": true,
  "data": {
    "floor": 0,
    "floorName": "Set Classes",
    "floorSize": 100,
    "totalItems": 93,
    "categories": ["Chromatic", "Diatonic", "Pentatonic", ...],
    "rooms": [
      {
        "id": "floor0_room0",
        "x": 10,
        "y": 15,
        "width": 20,
        "height": 18,
        "centerX": 20,
        "centerY": 24,
        "floor": 0,
        "category": "Chromatic",
        "items": ["Set Class 1", "Set Class 2", ...],
        "color": "hsl(0, 70%, 50%)",
        "description": "Chromatic - Floor 0"
      },
      ...
    ],
    "corridors": [
      {
        "points": [
          { "x": 20, "y": 24 },
          { "x": 45, "y": 24 },
          { "x": 45, "y": 50 }
        ],
        "width": 1
      },
      ...
    ],
    "seed": 12345
  }
}
```

**Example**:
```bash
curl https://localhost:7001/api/music-rooms/floor/0?floorSize=100&seed=42
```

---

## Floor Hierarchy

### Floor 0: Set Classes (93 rooms)
- **Data Source**: `SetClass.Items` (93 equivalence classes)
- **Categories**: Chromatic, Diatonic, Pentatonic, Hexatonic, Octatonic, Whole Tone, Augmented, Diminished
- **Room Size**: 12-24 units
- **Music Data**: Prime forms, interval class vectors, cardinality

### Floor 1: Forte Codes (115 rooms)
- **Data Source**: `ForteNumber.Items` (115 Forte numbers)
- **Categories**: Triads (3-x), Tetrads (4-x), Pentachords (5-x), Hexachords (6-x), Septachords (7-x), Octachords (8-x)
- **Room Size**: 10-20 units
- **Music Data**: Forte number identifiers (e.g., "3-11")

### Floor 2: Prime Forms (200 rooms)
- **Data Source**: `PitchClassSet.Items` (filtered to prime forms, sampled to 200)
- **Categories**: Major Triads, Minor Triads, Diminished, Augmented, Suspended, Seventh Chords, Extended Chords
- **Room Size**: 8-18 units
- **Music Data**: Prime form pitch class sets

### Floor 3: Chords (350 rooms)
- **Data Source**: MongoDB chord database
- **Categories**: Major, Minor, Dominant 7th, Major 7th, Minor 7th, Diminished 7th, Half-Diminished
- **Room Size**: 8-16 units
- **Music Data**: Chord names, pitch classes, quality

### Floor 4: Chord Inversions (100 rooms, sampled from 4,096)
- **Data Source**: `PitchClassSet.Items` (sampled)
- **Categories**: Root Position, 1st Inversion, 2nd Inversion, 3rd Inversion, Drop 2, Drop 3
- **Room Size**: 6-12 units
- **Music Data**: Inversion patterns, bass notes

### Floor 5: Chord Voicings (200 rooms, sampled from 100,000+)
- **Data Source**: `ChordVoicingLibrary` + MongoDB
- **Categories**: Jazz Voicings, Classical Voicings, Rock Voicings, CAGED System, Position-Based, String Sets
- **Room Size**: 4-10 units
- **Music Data**: Fret positions, fingerings, string sets

---

## BSP Algorithm

The backend uses the same BSP (Binary Space Partitioning) algorithm as the frontend:

1. **Start** with entire floor space as root node
2. **Recursively split** space:
   - Choose split direction (horizontal/vertical) based on aspect ratio
   - Split at random position between min/max room sizes
   - Create left and right child nodes
   - Stop when:
     - Desired room count reached
     - Room too small to split
     - Max depth reached
3. **Create rooms** in leaf nodes with random sizes within bounds
4. **Connect rooms** with L-shaped corridors
5. **Assign music data** to rooms based on categories

---

## Frontend Integration

### Using MusicRoomLoader

```typescript
import { getMusicRoomLoader } from './components/BSP/MusicRoomLoader';

// Get singleton instance
const loader = getMusicRoomLoader();

// Load a specific floor
const layout = await loader.loadFloor(0, 100, 42);

console.log(`Loaded ${layout.rooms.length} rooms for ${layout.floorName}`);
console.log(`Categories: ${layout.categories.join(', ')}`);

// Access room data
layout.rooms.forEach(room => {
  console.log(`Room ${room.id}: ${room.category}`);
  console.log(`  Items: ${room.items.join(', ')}`);
  console.log(`  Position: (${room.center.x}, ${room.center.z})`);
  console.log(`  Color: #${room.color.toString(16)}`);
});

// Preload all floors
await loader.preloadAllFloors(100, 42);
```

### Rendering Rooms

```typescript
import { RoomRenderer } from './components/BSP/RoomRenderer';

const renderer = new RoomRenderer(scene);

layout.rooms.forEach(room => {
  const meshes = renderer.renderRoom(room);
  // Meshes are automatically added to scene
});

layout.corridors.forEach(corridor => {
  const mesh = renderer.renderCorridor(corridor);
  // Corridor mesh added to scene
});
```

---

## Performance Optimization

### Backend
- **Caching**: Room layouts cached by floor number
- **Sampling**: Large floors (4, 5) sample data to keep room count manageable
- **Efficient BSP**: O(n log n) generation time

### Frontend
- **Client Caching**: Loaded floors cached in `MusicRoomLoader`
- **Lazy Loading**: Load floors on-demand as user navigates
- **Preloading**: Option to preload all floors during initialization
- **LOD**: Use Level of Detail for distant rooms (future enhancement)

---

## Configuration

### Backend Configuration

Edit `MusicRoomController.cs` to adjust:

```csharp
// Room size ranges by floor
private RoomParameters CalculateRoomParameters(int floor, int floorSize, int totalItems)
{
    return new RoomParameters
    {
        MinRoomSize = floor switch
        {
            0 => 12,  // Larger rooms for set classes
            1 => 10,
            2 => 8,
            3 => 8,
            4 => 6,
            5 => 4,   // Smaller rooms for voicings
            _ => 8
        },
        MaxRoomSize = floor switch
        {
            0 => 24,
            1 => 20,
            2 => 18,
            3 => 16,
            4 => 12,
            5 => 10,
            _ => 16
        },
        MaxDepth = (int)Math.Ceiling(Math.Log2(targetRoomCount))
    };
}
```

### Frontend Configuration

Edit `MusicRoomLoader.ts` to adjust:

```typescript
// API base URL
const loader = new MusicRoomLoader('https://localhost:7001');

// Floor size
const layout = await loader.loadFloor(0, 150); // Larger floor

// Seed for reproducibility
const layout = await loader.loadFloor(0, 100, 42);
```

---

## Testing

### Test Backend API

```bash
# Start the API
cd Apps/ga-server/GaApi
dotnet run

# Test floor generation
curl https://localhost:7001/api/music-rooms/floor/0?floorSize=100&seed=42

# Test all floors
for i in {0..5}; do
  curl "https://localhost:7001/api/music-rooms/floor/$i?floorSize=100&seed=42"
done
```

### Test Frontend Integration

```typescript
// In BSPDoomExplorer.tsx
useEffect(() => {
  const testRoomLoading = async () => {
    const loader = getMusicRoomLoader();
    
    try {
      const layout = await loader.loadFloor(0, 100, 42);
      console.log('✅ Successfully loaded floor 0:', layout);
    } catch (error) {
      console.error('❌ Failed to load floor 0:', error);
    }
  };
  
  testRoomLoading();
}, []);
```

---

## Next Steps

1. **Update BSPDoomExplorer.tsx** to use `MusicRoomLoader` instead of local generation
2. **Add loading states** and error handling in the UI
3. **Implement floor transitions** with smooth camera movement
4. **Add music item visualization** (3D models, text labels, interactive elements)
5. **Enhance corridors** with decorations and lighting
6. **Add minimap** showing current floor layout
7. **Implement search** to find specific music items across floors

---

## Benefits

✅ **Authentic Data**: Real music theory from GA library  
✅ **Consistency**: Same layout every time with seeds  
✅ **Performance**: Server-side generation, client-side caching  
✅ **Scalability**: Handles massive datasets efficiently  
✅ **Maintainability**: Single source of truth for music data  
✅ **Extensibility**: Easy to add new floors or categories  

---

**Status**: ✅ **Backend room generation system complete and ready to integrate!**

