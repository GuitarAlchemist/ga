# Guitar Alchemist - Fretboard App

A modern React application for visualizing guitar fretboard positions including chords, scales, modes, and arpeggios.

## Technology Stack

- **Vite** - Fast build tool and development server
- **React 18** - UI library
- **TypeScript** - Type-safe JavaScript
- **Material-UI (MUI)** - Component library for UI elements
- **Jotai** - Lightweight state management
- **ESLint** - Code linting

## Features

### 1. Collapsible Navigation Drawer
- Navy-themed sidebar navigation
- Smooth collapse/expand animation
- Placeholder menu items for future features
- Powered by MUI Drawer component

### 2. Guitar Fretboard Component
- SVG-based rendering for crisp visuals
- Realistic fret spacing using logarithmic scale (12th root of 2)
- Standard fret markers (dots at 3, 5, 7, 9, 15, 17, 19, 21 and double dots at 12, 24)
- Configurable display options (fret numbers, string labels, tuning)
- Interactive position markers with click handlers

### 3. Display Modes
The fretboard supports four visualization modes:
- **Chord** - Display chord voicings and finger positions
- **Scale** - Show scale patterns across the fretboard
- **Mode** - Visualize modal patterns
- **Arpeggio** - Display arpeggio positions

### 4. Backend-Agnostic Architecture
The fretboard component is designed to be presentation-only:
- Accepts position data via props
- No music theory calculations in the frontend
- All chord voicings, scale patterns, etc. should come from backend API
- Clean separation of concerns

## Project Structure

```
ga-fretboard-app/
├── src/
│   ├── components/
│   │   ├── GuitarFretboard.tsx    # Main fretboard component
│   │   └── NavigationDrawer.tsx   # Collapsible navigation menu
│   ├── data/
│   │   └── mockData.ts            # Sample data for demonstration
│   ├── store/
│   │   └── atoms.ts               # Jotai state atoms
│   ├── types/
│   │   └── fretboard.types.ts     # TypeScript interfaces
│   ├── App.tsx                    # Main application component
│   ├── main.tsx                   # Application entry point
│   └── index.css                  # Global styles
├── index.html                     # HTML template
├── package.json                   # Dependencies and scripts
├── tsconfig.json                  # TypeScript configuration
├── vite.config.ts                 # Vite configuration
└── README.md                      # This file
```

## Getting Started

### Prerequisites
- Node.js 18+
- pnpm (recommended) or npm

### Installation

1. Navigate to the project directory:
```bash
cd ga-fretboard-app
```

2. Install dependencies:
```bash
pnpm install
# or
npm install
```

**Note:** We recommend using `pnpm` as it handles optional dependencies better and avoids issues with Rollup on Windows.

3. Start the development server:
```bash
pnpm run dev
# or
npm run dev
```

4. Open your browser to the URL shown in the terminal (typically http://localhost:5173)

### Available Scripts

- `pnpm run dev` (or `npm run dev`) - Start development server
- `pnpm run build` (or `npm run build`) - Build for production
- `pnpm run preview` (or `npm run preview`) - Preview production build
- `pnpm run lint` (or `npm run lint`) - Run ESLint

## Component API

### GuitarFretboard Props

```typescript
interface GuitarFretboardProps {
  config?: FretboardConfig;           // Display configuration
  positions?: FretboardPosition[];    // Positions to display
  displayMode?: DisplayMode;          // 'chord' | 'scale' | 'mode' | 'arpeggio'
  title?: string;                     // Optional title
  onPositionClick?: (position: FretboardPosition) => void;  // Click handler
}
```

### FretboardPosition Interface

```typescript
interface FretboardPosition {
  string: number;        // String number (0-5, 0 = high E)
  fret: number;          // Fret number (0 = open)
  label?: string;        // Display label (note name, interval, etc.)
  color?: string;        // Marker color
  emphasized?: boolean;  // Highlight this position (e.g., root note)
}
```

### FretboardConfig Interface

```typescript
interface FretboardConfig {
  fretCount?: number;          // Number of frets (default: 24)
  stringCount?: number;        // Number of strings (default: 6)
  startFret?: number;          // Starting fret (default: 0)
  tuning?: string[];           // String labels (default: standard tuning)
  showFretNumbers?: boolean;   // Show fret numbers (default: true)
  showStringLabels?: boolean;  // Show string labels (default: true)
  width?: number;              // SVG width (default: 1200)
  height?: number;             // SVG height (default: 200)
}
```

## Integration with Backend

To integrate with your backend API:

1. Replace the mock data in `src/data/mockData.ts` with API calls
2. Create a service layer to fetch position data from your backend
3. The backend should calculate:
   - Chord voicings and finger positions
   - Scale patterns for all keys
   - Mode positions across the fretboard
   - Arpeggio positions
4. Return data in the `FretboardPosition[]` format
5. Pass the data to the `GuitarFretboard` component via props

Example API integration:

```typescript
// services/fretboardService.ts
export async function getChordPositions(chordName: string): Promise<FretboardPosition[]> {
  const response = await fetch(`/api/chords/${chordName}/positions`);
  return response.json();
}

// In your component:
const [positions, setPositions] = useState<FretboardPosition[]>([]);

useEffect(() => {
  getChordPositions('C Major').then(setPositions);
}, []);

return <GuitarFretboard positions={positions} displayMode="chord" />;
```

## Customization

### Theming
The app uses MUI's theming system. Modify the theme in `src/App.tsx`:

```typescript
const darkTheme = createTheme({
  palette: {
    mode: 'dark',
    primary: { main: '#2196F3' },
    // ... customize colors
  },
});
```

### Navigation Menu
Add or modify menu items in `src/components/NavigationDrawer.tsx`:

```typescript
const menuItems: MenuItem[] = [
  { id: 'fretboard', label: 'Fretboard', icon: <MusicNoteIcon /> },
  // Add your menu items here
];
```

## Future Enhancements

Potential features to add:
- Multiple fretboard views (horizontal/vertical)
- Playback functionality (audio)
- Export fretboard diagrams as images
- Custom tuning support
- Left-handed mode
- Mobile-responsive design improvements
- Animation of scale/arpeggio patterns
- Integration with MIDI input

## License

This project is part of the Guitar Alchemist suite.

## Contributing

This component is designed to be reusable and extensible. When contributing:
- Keep music theory logic in the backend
- Maintain TypeScript type safety
- Follow the existing code style
- Add tests for new features
- Update documentation

