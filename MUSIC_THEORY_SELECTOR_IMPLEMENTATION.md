# Music Theory Selector Implementation

## 📋 Overview

This document describes the implementation of the **Music Theory Selector** feature, which allows users to select a musical key, mode, and scale degree, with data fed from the C# backend API. The selected music theory context is then used to highlight scale tones on the fretboard.

---

## 🎯 Features Implemented

### **Backend API** (`Apps/ga-server/GaApi`)

✅ **New Controller**: `MusicTheoryController.cs`

**Endpoints:**

1. **`GET /api/music-theory/keys`**
   - Returns all 30 musical keys (15 major + 15 minor)
   - Includes: name, root, mode, key signature, accidental kind, notes
   - Example response:
     ```json
     {
       "success": true,
       "data": [
         {
           "name": "Key of C",
           "root": "C",
           "mode": "Major",
           "keySignature": 0,
           "accidentalKind": "Natural",
           "notes": ["C", "D", "E", "F", "G", "A", "B"]
         },
         ...
       ],
       "metadata": {
         "totalKeys": 30,
         "majorKeys": 15,
         "minorKeys": 15
       }
     }
     ```

2. **`GET /api/music-theory/modes`**
   - Returns all 7 modes (Ionian, Dorian, Phrygian, Lydian, Mixolydian, Aeolian, Locrian)
   - Includes: name, degree, isMinor flag, intervals, characteristic notes
   - Example response:
     ```json
     {
       "success": true,
       "data": [
         {
           "name": "Ionian",
           "degree": 1,
           "isMinor": false,
           "intervals": ["P1", "M2", "M3", "P4", "P5", "M6", "M7"],
           "characteristicNotes": ["F", "B"]
         },
         ...
       ]
     }
     ```

3. **`GET /api/music-theory/scale-degrees`**
   - Returns scale degrees I-VII with Roman numerals
   - Includes: degree number, Roman numeral, functional name
   - Example response:
     ```json
     {
       "success": true,
       "data": [
         { "degree": 1, "romanNumeral": "I", "name": "Tonic" },
         { "degree": 2, "romanNumeral": "II", "name": "Supertonic" },
         ...
       ]
     }
     ```

4. **`GET /api/music-theory/keys/{keyName}/notes`**
   - Returns notes for a specific key (e.g., "C Major", "A Minor")
   - Includes: key name, root, mode, notes array, key signature
   - Example: `/api/music-theory/keys/C%20Major/notes`

---

### **Frontend Components** (`ReactComponents/ga-react-components`)

✅ **New Component**: `MusicTheorySelector.tsx`

**Features:**
- **Tonality Toggle**: Switch between "Atonal" (no key center) and "Tonal" (with key center)
- **Key Selector**: Dropdown with all 30 keys from backend
- **Mode Selector**: Dropdown with all 7 modes from backend
- **Scale Degree Selector**: Optional dropdown with Roman numerals (I-VII)
- **Notes Display**: Shows chips with all notes in the selected key
- **Context Callback**: `onContextChange` provides complete music theory context

**Props:**
```typescript
interface MusicTheorySelectorProps {
  context?: MusicTheoryContext;
  onContextChange?: (context: MusicTheoryContext) => void;
  apiBaseUrl?: string;  // default: http://localhost:7001
  compact?: boolean;
}

interface MusicTheoryContext {
  tonality: 'atonal' | 'tonal';
  key?: string;           // e.g., "C Major", "A Minor"
  mode?: string;          // e.g., "Ionian", "Dorian"
  scaleDegree?: number;   // 1-7 (I-VII)
  notes?: string[];       // Notes in the selected key/mode
}
```

✅ **Test Page**: `MusicTheoryTest.tsx`

**Features:**
- Demonstrates integration with `MinimalThreeInstrument`
- Generates fretboard positions for selected key/mode
- Highlights scale tones on the fretboard
- Shows current context in JSON format
- Includes usage instructions

**Route**: `http://localhost:5173/test/music-theory`

---

## 📁 Files Created/Modified

### **Backend (C#)**

**Created:**
- `Apps/ga-server/GaApi/Controllers/MusicTheoryController.cs` - New API controller with 4 endpoints

### **Frontend (React/TypeScript)**

**Created:**
- `ReactComponents/ga-react-components/src/components/MusicTheorySelector.tsx` - Main component
- `ReactComponents/ga-react-components/src/pages/MusicTheoryTest.tsx` - Test page

**Modified:**
- `ReactComponents/ga-react-components/src/components/index.ts` - Added exports
- `ReactComponents/ga-react-components/src/main.tsx` - Added route
- `ReactComponents/ga-react-components/src/pages/TestIndex.tsx` - Added link to test page

---

## 🚀 How to Use

### **1. Start the Backend**

```powershell
# From repository root
.\Scripts\start-all.ps1 -Dashboard
```

This starts:
- **GaApi** on `https://localhost:7001` (backend API)
- **Aspire Dashboard** on `https://localhost:15001` (monitoring)

### **2. Start the Frontend**

```bash
# From ReactComponents/ga-react-components
npm run dev
```

This starts the Vite dev server on `http://localhost:5173`

### **3. Navigate to Test Page**

Open browser to: **`http://localhost:5173/test/music-theory`**

Or from the test index: **`http://localhost:5173/test`** → Click "Test Music Theory"

### **4. Use the Component**

1. **Select Tonality**: Choose "Tonal" to enable key/mode selection
2. **Select Key**: Choose a musical key (e.g., "Key of C", "Key of Am")
3. **Select Mode**: Choose a mode (e.g., "Ionian", "Dorian")
4. **View Scale Tones**: The fretboard highlights all notes in the selected key/mode
5. **Interact**: Hover over positions to see string/fret information

---

## 🎸 Integration with Fretboard

The `MusicTheorySelector` component can be integrated with any fretboard component:

```typescript
import { MusicTheorySelector, MusicTheoryContext } from './components/MusicTheorySelector';
import { MinimalThreeInstrument } from './components/MinimalThree';

const [musicTheoryContext, setMusicTheoryContext] = useState<MusicTheoryContext>({
  tonality: 'atonal'
});

const [positions, setPositions] = useState<FretboardPosition[]>([]);

const handleContextChange = (context: MusicTheoryContext) => {
  setMusicTheoryContext(context);
  
  // Generate fretboard positions for the selected key/mode
  if (context.tonality === 'tonal' && context.notes) {
    const newPositions = generatePositionsForNotes(context.notes, tuning);
    setPositions(newPositions);
  } else {
    setPositions([]);
  }
};

return (
  <>
    <MusicTheorySelector
      context={musicTheoryContext}
      onContextChange={handleContextChange}
      apiBaseUrl="http://localhost:7001"
    />
    
    <MinimalThreeInstrument
      instrument={guitarConfig}
      positions={positions}
      renderMode="3d-webgl"
      // ... other props
    />
  </>
);
```

---

## 🔌 API Integration

The component fetches data from the backend on mount:

```typescript
// Fetch keys
const keysResponse = await fetch(`${apiBaseUrl}/api/music-theory/keys`);
const keysData = await keysResponse.json();
setKeys(keysData.data || []);

// Fetch modes
const modesResponse = await fetch(`${apiBaseUrl}/api/music-theory/modes`);
const modesData = await modesResponse.json();
setModes(modesData.data || []);

// Fetch scale degrees
const degreesResponse = await fetch(`${apiBaseUrl}/api/music-theory/scale-degrees`);
const degreesData = await degreesResponse.json();
setScaleDegrees(degreesData.data || []);
```

When a key is selected, it fetches the notes:

```typescript
const response = await fetch(
  `${apiBaseUrl}/api/music-theory/keys/${encodeURIComponent(keyName)}/notes`
);
const data = await response.json();
setContext({ ...context, key: keyName, notes: data.data.notes });
```

---

## 🎨 UI Preview

```
┌─────────────────────────────────────┐
│ 🎵 Music Theory Context             │
├─────────────────────────────────────┤
│ Tonality:  [Atonal] [Tonal]         │
│                                     │
│ Key: [C Major ▼]                    │
│ Mode: [Ionian ▼]                    │
│ Scale Degree: [I - Tonic ▼]        │
│                                     │
│ Notes in Key of C:                  │
│ [🎹 C] [🎹 D] [🎹 E] [🎹 F]        │
│ [🎹 G] [🎹 A] [🎹 B]                │
└─────────────────────────────────────┘
```

---

## 💡 Future Enhancements

Once the basic integration is working, you can add:

1. **Chord Degree Highlighting** - Show I, IV, V chords in the key
2. **Modal Interchange** - Borrow chords from parallel modes
3. **Modulation Suggestions** - Use `/api/contextual-chords/modulation` endpoint
4. **Scale Pattern Visualization** - Show CAGED patterns for the selected scale
5. **Interval Highlighting** - Color-code by interval from root (P1, M3, P5, etc.)
6. **Harmonic Function** - Show tonic, subdominant, dominant functions
7. **Chord Suggestions** - Use `/api/contextual-chords/keys/{keyName}` endpoint

---

## 🧪 Testing

### **Backend Endpoints**

Test the API endpoints using curl or browser:

```bash
# Get all keys
curl http://localhost:7001/api/music-theory/keys

# Get all modes
curl http://localhost:7001/api/music-theory/modes

# Get scale degrees
curl http://localhost:7001/api/music-theory/scale-degrees

# Get notes for C Major
curl "http://localhost:7001/api/music-theory/keys/C%20Major/notes"
```

### **Frontend Component**

1. Navigate to `http://localhost:5173/test/music-theory`
2. Open browser console (F12)
3. Select different keys/modes
4. Verify console logs show context changes
5. Verify fretboard highlights scale tones

---

## 📝 Notes

- **Backend Required**: The component requires the GaApi backend to be running
- **CORS**: The backend has CORS enabled for `http://localhost:5173`
- **Error Handling**: The component shows error alerts if backend is unavailable
- **Loading State**: Shows spinner while fetching data from backend
- **Caching**: Backend uses in-memory caching for performance (1 hour TTL)

---

## 🎯 Summary

This implementation provides a complete solution for music theory context selection with:

✅ **Backend API** - 4 endpoints exposing keys, modes, scale degrees, and key notes  
✅ **Frontend Component** - Reusable React component with Material-UI  
✅ **Test Page** - Demonstrates integration with fretboard  
✅ **Documentation** - Complete usage guide and examples  

The component is ready to use and can be integrated into any fretboard visualization! 🎸✨

