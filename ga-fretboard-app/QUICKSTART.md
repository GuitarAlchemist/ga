# Quick Start Guide

## Running the Application

1. **Navigate to the project directory:**
   ```bash
   cd ga-fretboard-app
   ```

2. **Install dependencies (if not already done):**
   ```bash
   npm install
   ```

3. **Start the development server:**
   ```bash
   npm run dev
   ```

4. **Open your browser:**
   - The terminal will display a URL (typically `http://localhost:5173`)
   - Open this URL in your browser
   - You should see the Guitar Alchemist Fretboard App

## What You'll See

### Navigation Drawer (Left Side)
- **Navy-colored sidebar** with the Guitar Alchemist branding
- **Collapse/Expand button** (chevron icon) to toggle the drawer
- **Menu items:**
  - Fretboard
  - Chords
  - Scales
  - Settings

### Main Content Area

#### Controls Panel
- **Display Mode Toggle:** Switch between Chord, Scale, Mode, and Arpeggio
- **Selection Dropdown:** Choose specific items to display (e.g., "C Major", "G Major")

#### Fretboard Display
- **Interactive SVG fretboard** with:
  - 24 frets with realistic spacing
  - 6 strings (standard tuning: E-A-D-G-B-E)
  - Fret markers (dots at 3, 5, 7, 9, etc.)
  - String labels showing tuning
  - Fret numbers
  - Colored position markers showing notes/positions

#### Information Panel
- Description of the application
- Feature list
- Architecture notes

## Try These Features

### 1. Toggle Display Modes
Click the mode buttons to see different visualizations:
- **Chord:** See chord voicings (C Major, G Major)
- **Scale:** View scale patterns (C Major Scale, A Minor Pentatonic)
- **Mode:** Explore modal patterns (D Dorian)
- **Arpeggio:** Display arpeggio positions (C Major, E Minor)

### 2. Collapse the Navigation
- Click the chevron icon in the navigation drawer
- Watch the smooth animation as it collapses to icon-only view
- Click again to expand

### 3. Select Different Items
- Use the dropdown menu to switch between different chords/scales/modes/arpeggios
- Watch the fretboard update with new positions

### 4. Click on Positions
- Click any colored dot on the fretboard
- Check the browser console (F12) to see the position data logged

## Understanding the Display

### Position Markers
- **Blue circles with gold border:** Root notes (emphasized)
- **Colored circles:** Other notes in the pattern
- **Labels:** Show note names, intervals, or finger numbers
- **Different colors:** Indicate different note types or functions

### Fretboard Elements
- **Thick gold line (left):** The nut (0th fret)
- **Thin silver lines:** Frets
- **Horizontal lines:** Strings (thicker = lower pitch)
- **Gray dots:** Standard fret markers
- **Numbers at bottom:** Fret numbers

## Mock Data

The application currently uses mock data for demonstration. In a production environment:
- Replace mock data with API calls to your backend
- Backend should calculate all music theory (chord voicings, scale patterns, etc.)
- Frontend only handles visual rendering

## Next Steps

1. **Explore the code:**
   - `src/components/GuitarFretboard.tsx` - Main fretboard component
   - `src/components/NavigationDrawer.tsx` - Navigation menu
   - `src/types/fretboard.types.ts` - TypeScript interfaces
   - `src/data/mockData.ts` - Sample data

2. **Customize:**
   - Modify colors in `src/App.tsx` (theme configuration)
   - Add menu items in `src/components/NavigationDrawer.tsx`
   - Create new mock data patterns in `src/data/mockData.ts`

3. **Integrate with backend:**
   - Create API service layer
   - Replace mock data with real API calls
   - Implement music theory calculations in backend

## Troubleshooting

### Port already in use
If port 5173 is already in use, Vite will automatically try the next available port.

### Dependencies not installed
Run `npm install` in the `ga-fretboard-app` directory.

### Build errors
Run `npm run build` to check for TypeScript or build errors.

### Browser compatibility
This app uses modern web features. Use a recent version of Chrome, Firefox, Safari, or Edge.

## Development Commands

```bash
# Start development server
npm run dev

# Build for production
npm run build

# Preview production build
npm run preview

# Run linter
npm run lint
```

## Support

For issues or questions, refer to the main README.md file.

