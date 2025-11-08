# How to Run the Components

This guide shows you how to run the BSP DOOM Explorer with Room/Door navigation and the new Sunburst3D visualization.

---

## Quick Start

### 1. Install Dependencies

```bash
cd ReactComponents/ga-react-components
npm install
```

### 2. Start Development Server

```bash
npm run dev
```

This will start the Vite development server, typically at `http://localhost:5173`

### 3. Navigate to Test Pages

Once the server is running, open your browser and go to:

**Main Test Index:**
```
http://localhost:5173/test
```

This shows all available test pages.

---

## Running BSP DOOM Explorer (with Room/Door Navigation)

### Option A: Direct URL

```
http://localhost:5173/test/bsp-doom-explorer
```

### Option B: From Test Index

1. Go to `http://localhost:5173/test`
2. Find the **"BSP DOOM Explorer"** card
3. Click **"Test BSP DOOM Explorer"** button

### Controls

| Action | Control |
|--------|---------|
| **Move** | WASD keys |
| **Look Around** | Mouse (click canvas to lock pointer) |
| **Move Up** | Space |
| **Move Down** | Shift |
| **Change Floor** | Mouse Wheel |
| **Enter Door** | Click (while looking at door) |
| **Go Back** | Backspace or Escape |
| **Release Pointer** | Escape |

### How to Use Room/Door Navigation

1. **Start on Floor 5 (Voicings)**
   - Scroll with mouse wheel until you reach Floor 5
   - You'll see 6 category doors arranged in a circle

2. **Enter a Room**
   - Look at a door (center crosshair on it)
   - Click to enter
   - Example: Click "Jazz Voicings" door

3. **Explore Sub-Rooms**
   - Inside "Jazz Voicings", you'll see 6 technique doors
   - Click "Drop 2" to see chord quality doors
   - Click "Maj7" to see specific voicings

4. **Navigate Back**
   - Press **Backspace** or **Escape** to go back one level
   - Or click a breadcrumb chip in the HUD to jump to that level

5. **Breadcrumb Trail**
   - Look at the HUD (top-left)
   - You'll see: `Root → Jazz Voicings → Drop 2 → Maj7`
   - Click any breadcrumb to jump back to that room

### Features to Try

- **6 Main Categories:**
  - Jazz Voicings (Drop 2, Drop 3, Rootless, etc.)
  - Classical Voicings (Close Position, SATB, etc.)
  - Rock Voicings (Power Chords, Barre Chords, etc.)
  - CAGED System (C, A, G, E, D shapes)
  - Position-Based (Positions I-V)
  - String Sets (Different string combinations)

- **Visual Elements:**
  - Glowing door archways
  - Circular room layout
  - Paths connecting platform to doors
  - Color-coded categories
  - Child count indicators

---

## Running Sunburst3D Visualization

### Option A: Direct URL

```
http://localhost:5173/test/sunburst-3d
```

### Option B: From Test Index

1. Go to `http://localhost:5173/test`
2. Find the **"Sunburst 3D"** card
3. Click **"Test Sunburst 3D"** button

### Controls

The Sunburst3D demo includes an interactive control panel on the right side:

**Sliders:**
- **Max Depth (LOD):** 1-6 levels
  - Controls how many hierarchy levels to render
  - Lower = better performance, fewer segments
  - Higher = more detail, more segments

- **Slope Angle:** 0-60 degrees
  - Controls the elevation effect
  - 0° = flat (all rings at same height)
  - 30° = moderate slope (recommended)
  - 60° = steep slope (dramatic effect)

**Toggle:**
- **Auto Rotate:** Enable/disable camera rotation

**Interaction:**
- **Hover** over segments to highlight them
- **Click** segments to zoom into that sub-hierarchy
- View **breadcrumb trail** showing current path
- See **selected node info** in the panel

### Features to Try

1. **Adjust Slope Angle**
   - Start at 0° (flat)
   - Gradually increase to 30°
   - Notice how inner rings elevate

2. **Control LOD**
   - Set Max Depth to 2 (only 2 levels visible)
   - Increase to 4 (more detail)
   - Notice performance difference

3. **Navigate Hierarchy**
   - Click "Voicings" segment
   - Then click "Jazz Voicings"
   - Then click "Drop 2"
   - Watch breadcrumb trail update

4. **Explore Data**
   - Hover over segments to see:
     - Node name
     - Depth level
     - Value (number of items)

---

## Comparison: When to Use Each

### Use BSP DOOM Explorer When:
- ✅ You want immersive, first-person exploration
- ✅ You want to "walk through" musical hierarchies
- ✅ You prefer DOOM-style navigation
- ✅ You want to focus on one room at a time
- ✅ You want a gaming-like experience

### Use Sunburst3D When:
- ✅ You want to see the entire hierarchy at once
- ✅ You want a top-down overview
- ✅ You need to compare different branches
- ✅ You want quick navigation via clicking
- ✅ You prefer a more traditional visualization

---

## Troubleshooting

### Port Already in Use

If you see an error like "Port 5173 is already in use":

```bash
# Kill the process using the port (Windows)
netstat -ano | findstr :5173
taskkill /PID <PID> /F

# Or use a different port
npm run dev -- --port 5174
```

### WebGPU Not Available

If you see "WebGPU not available, falling back to WebGL":
- This is normal! The components work with both WebGPU and WebGL
- WebGPU is only available in Chrome/Edge 113+ and requires a compatible GPU
- WebGL fallback provides the same functionality

### Performance Issues

**BSP DOOM Explorer:**
- Reduce the number of elements per floor
- Disable auto-navigation
- Lower the move speed

**Sunburst3D:**
- Reduce Max Depth to 3 or 4
- Disable Auto Rotate
- Close other browser tabs

### Text Not Visible

If text labels are cut off or not visible:
- This was fixed in the latest update
- Make sure you're running the latest code
- Try refreshing the page (Ctrl+F5)

---

## Development Tips

### Hot Module Replacement (HMR)

Vite supports HMR, so changes to the code will automatically reload:

1. Edit a file (e.g., `BSPDoomExplorer.tsx`)
2. Save the file
3. Browser automatically updates (no manual refresh needed)

### Browser DevTools

Press **F12** to open DevTools and see:
- Console logs (useful for debugging)
- Performance metrics
- Network requests
- WebGPU/WebGL info

### Useful Console Logs

The components log useful information:

**BSP DOOM Explorer:**
```
✅ Floor 5: Created room with 6 category doors
🚪 Entered room: Jazz Voicings with 6 sub-doors
⬅️ Back to room: Root
```

**Sunburst3D:**
```
✅ Sunburst3D: Rendered 156 segments (depth 4)
```

---

## Building for Production

To create a production build:

```bash
npm run build
```

This creates optimized files in the `dist/` directory.

To preview the production build:

```bash
npm run preview
```

---

## Next Steps

### Extend the Room/Door System

Add new categories to Floor 5:

1. Edit `BSPDoomExplorer.tsx`
2. Find the `generateRoomHierarchy()` function
3. Add your category:

```typescript
if (floor === 5 && !parentName) {
  return [
    // ... existing categories
    {
      name: 'My New Category',
      color: 0xff00ff,
      targetFloor: 5,
      children: ['Sub-Cat 1', 'Sub-Cat 2']
    },
  ];
}
```

### Customize Sunburst3D Data

Edit `Sunburst3DDemo.tsx` and modify the `musicalHierarchyData` object to add your own hierarchical data.

---

## Support

If you encounter issues:

1. Check the browser console for errors (F12)
2. Verify all dependencies are installed (`npm install`)
3. Try clearing the cache (Ctrl+Shift+Delete)
4. Restart the dev server

Enjoy exploring! 🎸🎮

