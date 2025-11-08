# Fretboard with Hand Component

## Overview

The `FretboardWithHand` component combines a 3D fretboard visualization with hand pose rendering to show how to play guitar chords. It integrates with the backend API to fetch chord voicings and displays finger positions in 3D space.

## Features

### ✅ Implemented
- **3D Fretboard Rendering**: Uses Three.js with WebGPU/WebGL fallback
- **API Integration**: Fetches chord voicings from `ContextualChords/voicings` endpoint
- **Color-Coded Markers**: ✨ **NEW!** Finger position markers with color coding:
  - 🟢 Green = Index finger (1)
  - 🔵 Blue = Middle finger (2)
  - 🟠 Orange = Ring finger (3)
  - 🔴 Pink = Pinky finger (4)
- **Rigged Hand Model**: ✨ **NEW!** Loads actual 3D hand model from GLB file
- **Finger Bone Detection**: Automatically detects and maps all finger bones (thumb, index, middle, ring, pinky)
- **Adaptive Finger Curling**: ✨ **NEW!** Curl angle adapts based on fret position (more curl for lower frets)
- **Finger Abduction**: ✨ **NEW!** Index and pinky fingers spread slightly for natural positioning
- **Natural Resting Pose**: Unused fingers curl slightly for realistic appearance
- **Improved Hand Positioning**: ✨ **NEW!** Better scale (0.08x), position, and rotation for natural fretting angle
- **Thumb Opposition**: ✨ **NEW!** Thumb positioned behind neck in opposition to fingers
- **Interactive Controls**: Orbit controls for rotating and zooming the view
- **Difficulty Display**: Shows chord difficulty level from API
- **Fallback Chord**: Uses hardcoded open G chord if API fails

### 🚧 TODO
- **Biomechanical IK**: Use the existing `BiomechanicalAnalyzer` and `InverseKinematicsSolver` for realistic poses
- **Precise Finger Positioning**: Calculate exact 3D positions for fingertips on fretboard
- **Finger Animation**: Animate fingers moving to chord positions
- **Finger Assignment**: Get actual finger assignments from API (currently uses fallback)
- **Hand Size Customization**: Allow users to select hand size (Small/Medium/Large/XL)
- **Multiple Voicings**: Show alternative fingerings for the same chord
- **Voice Leading**: Visualize smooth transitions between chords
- **Thumb Positioning**: Position thumb on back of neck for proper fretting technique

## Usage

```typescript
import { FretboardWithHand } from 'ga-react-components';

function MyComponent() {
  return (
    <FretboardWithHand
      chordName="G"
      apiBaseUrl="https://localhost:7001"
      width={1200}
      height={600}
    />
  );
}
```

## Props

| Prop | Type | Default | Description |
|------|------|---------|-------------|
| `chordName` | `string` | `'G'` | Name of the chord to display (e.g., 'C', 'Gmaj7', 'Am') |
| `apiBaseUrl` | `string` | `'https://localhost:7001'` | Base URL for the backend API |
| `width` | `number` | `1200` | Canvas width in pixels |
| `height` | `number` | `600` | Canvas height in pixels |

## API Integration

The component fetches chord voicings from the backend:

```
GET /ContextualChords/voicings/{chordName}?limit=1&maxDifficulty=Easy
```

**Response Format:**
```json
{
  "success": true,
  "data": [
    {
      "chordName": "G",
      "positions": [3, 2, 0, 0, 0, 3],  // Fret positions for each string
      "difficulty": "Easy",
      "physical": {
        "fretSpan": 3,
        "fingerStretch": 2,
        "lowestFret": 0,
        "highestFret": 3
      }
    }
  ]
}
```

## Architecture

### Component Structure

```
FretboardWithHand
├── API Service (fetch voicing)
├── Three.js Scene Setup
│   ├── WebGPU/WebGL Renderer
│   ├── Camera & Lights
│   └── Orbit Controls
├── Fretboard Geometry
│   ├── Fretboard Base
│   ├── Frets
│   ├── Strings
│   └── Position Markers
└── Hand Model
    ├── Simple Finger Cylinders (current)
    └── Rigged Hand Model (TODO)
```

### Coordinate System

- **X-axis**: Across the fretboard (string positions)
- **Y-axis**: Vertical (height above fretboard)
- **Z-axis**: Along the fretboard (fret positions)

### Fretboard Dimensions

```typescript
const fretboardLength = 20;  // Units along Z-axis
const fretboardWidth = 6;    // Units along X-axis
const numFrets = 5;          // Number of frets to display
```

## Integration with Existing Systems

### Biomechanical Hand Model

The codebase already has a sophisticated biomechanical hand modeling system:

**Location**: `Common/GA.Business.Core/Fretboard/Biomechanics/`

**Key Classes**:
- `HandModel`: 19+ degrees of freedom, realistic joint structure
- `InverseKinematicsSolver`: GA-based IK solver for optimal finger positions
- `BiomechanicalAnalyzer`: Analyzes chord playability
- `PersonalizedHandModel`: Creates hand models for different sizes

**Example Usage** (C#):
```csharp
// Create hand model
var handModel = PersonalizedHandModel.Create(HandSize.Medium);

// Analyze chord playability
var analyzer = new BiomechanicalAnalyzer(handModel);
var analysis = analyzer.AnalyzeChordPlayability(positions);

// Get hand pose
var pose = analysis.OptimalHandPose;
```

### Rigged Hand Model

**Location**: `Experiments/React/reactapp1.client/src/RiggedHand.tsx`

The codebase includes a rigged 3D hand model:
- **File**: `/assets/Dorchester3D_com_rigged_hand.glb`
- **Format**: GLB (binary glTF)
- **Features**: Skeletal rig with finger bones

**Example Loading** (React Three Fiber):
```typescript
import { useGLTF } from '@react-three/drei';

function HandModel() {
  const { scene } = useGLTF('/assets/Dorchester3D_com_rigged_hand.glb');
  return <primitive object={scene} scale={0.01} />;
}
```

## Next Steps

### Phase 1: Load Rigged Hand Model
1. Copy hand model GLB file to public assets
2. Use GLTFLoader to load the model
3. Position hand above fretboard
4. Scale and orient correctly

### Phase 2: Finger Positioning
1. Parse finger bone hierarchy from GLB
2. Map finger positions to fret locations
3. Calculate joint angles for each finger
4. Apply rotations to finger bones

### Phase 3: Biomechanical Integration
1. Create TypeScript bindings for C# biomechanical API
2. Add endpoint to return hand pose data
3. Use IK solver results to position fingers
4. Validate poses against joint constraints

### Phase 4: Animation
1. Animate fingers from rest position to chord
2. Add smooth transitions between chords
3. Highlight active fingers
4. Show finger pressure/force indicators

## Test Page

Access the test page at: `http://localhost:5173/test/fretboard-with-hand`

**Features**:
- Chord name input field
- Quick select buttons for common chords
- Live 3D visualization
- Difficulty display
- Error handling with fallback

## Related Components

- **ThreeFretboard**: 3D fretboard without hand
- **MinimalThreeInstrument**: Universal instrument renderer
- **BiomechanicsController**: Backend API for hand analysis

## References

- [Three.js Documentation](https://threejs.org/docs/)
- [GLTFLoader](https://threejs.org/docs/#examples/en/loaders/GLTFLoader)
- [Biomechanical Implementation](../../Common/GA.Business.Core/Fretboard/Biomechanics/IMPLEMENTATION_COMPLETE.md)
- [Hand Model Research](../../Common/GA.Business.Core/Fretboard/Biomechanics/IMPLEMENTATION_SUMMARY.md)

