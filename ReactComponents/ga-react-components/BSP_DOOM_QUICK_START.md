# BSP DOOM Explorer - Quick Start Guide 🎮

Get up and running with the BSP DOOM Explorer in minutes!

## 🚀 Quick Start

### 1. Start the Development Server

```bash
cd ReactComponents/ga-react-components
npm install
npm run dev
```

### 2. Open the Test Page

Navigate to: **http://localhost:5173/test/bsp-doom-explorer**

### 3. Start Exploring!

1. **Click anywhere** on the black canvas to lock your pointer
2. **Move** with WASD keys
3. **Look around** by moving your mouse
4. **Fly up/down** with Space/Shift
5. **Press ESC** to release pointer lock

## 🎯 What You'll See

### Main View
- **Black background** - The void of tonal space
- **Green glowing walls** - BSP partition planes
- **Colored floor regions** - Different tonal areas
- **Distance fog** - Atmospheric depth

### HUD (Top-Left)
- **Renderer type** - WebGPU or WebGL
- **FPS counter** - Performance metric
- **Current region** - Which tonal area you're in
- **BSP tree stats** - Total regions and depth
- **Controls guide** - Quick reference

### Minimap (Bottom-Right)
- **Green dot** - Your position
- **Black background** - The world map

### Settings Panel (Right Side)
- **Show HUD toggle** - Hide/show the HUD
- **Show Minimap toggle** - Hide/show the minimap
- **Move Speed slider** - Adjust movement speed (1-20)
- **Look Speed slider** - Adjust mouse sensitivity (0.5-5)
- **Current Region info** - Details about where you are
- **Region History** - Last 10 regions visited

## 🎮 Controls Reference

| Input | Action |
|-------|--------|
| **W** | Move forward |
| **S** | Move backward |
| **A** | Strafe left |
| **D** | Strafe right |
| **Space** | Move up |
| **Shift** | Move down |
| **Mouse** | Look around (when locked) |
| **Click** | Lock/unlock pointer |
| **ESC** | Release pointer lock |

## 🎨 Understanding the Colors

Each tonal region has a distinct color:

- 🟢 **Green** - Major tonality (stable, happy)
- 🔵 **Blue** - Minor tonality (melancholic, sad)
- 🟣 **Magenta** - Modal tonality (exotic, ancient)
- 🔴 **Red** - Atonal (chaotic, dissonant)
- 🟡 **Yellow** - Chromatic (transitional, all notes)
- 🔷 **Cyan** - Pentatonic (simple, folk)
- 🟣 **Purple** - Blues (soulful, expressive)
- 🟠 **Orange** - Whole Tone (dreamy, impressionist)
- 🌸 **Pink** - Diminished (tense, unstable)

## 🏗️ The BSP Structure

### What You're Exploring

The BSP (Binary Space Partitioning) tree divides musical tonal space into regions:

1. **Root Node** - The entire chromatic space (all 12 notes)
2. **Partition Planes** - Walls that split space based on musical relationships
3. **Child Regions** - Subdivisions representing different tonalities
4. **Leaf Nodes** - Final regions containing specific scales/chords

### Partition Strategies

The green walls represent different ways of dividing tonal space:

- **Circle of Fifths** - Based on perfect fifth relationships
- **Chromatic Distance** - Based on semitone proximity
- **Harmonic Series** - Based on overtone relationships
- **Modal Brightness** - Based on sharp/flat tendency
- **Tonal Stability** - Based on consonance/dissonance

## 💡 Tips & Tricks

### Navigation
- **Hold W** to move forward continuously
- **Combine WASD** for diagonal movement
- **Use Space/Shift** to get a bird's-eye view
- **Slow down** near walls to examine partition planes

### Exploration
- **Cross partition walls** to move between BSP subtrees
- **Watch the HUD** to see when you enter new regions
- **Check the minimap** to track your position
- **Use the settings panel** to adjust comfort level

### Performance
- **WebGPU** provides best performance (Chrome/Edge 113+)
- **WebGL** fallback works on all browsers
- **Lower move speed** if experiencing motion sickness
- **Toggle HUD/minimap** for cleaner view

## 🔧 Customization

### Adjust Movement Speed

Use the slider in the settings panel:
- **1-5**: Slow, careful exploration
- **5-10**: Normal walking speed
- **10-20**: Fast, arcade-style movement

### Adjust Look Sensitivity

Use the slider in the settings panel:
- **0.5-1.5**: Low sensitivity (precise aiming)
- **1.5-3.0**: Medium sensitivity (balanced)
- **3.0-5.0**: High sensitivity (quick turns)

### Toggle UI Elements

- **Show HUD**: Turn off for immersive experience
- **Show Minimap**: Turn off for more challenge

## 🐛 Troubleshooting

### Pointer Won't Lock
- **Solution**: Click directly on the black canvas area
- **Note**: Some browsers require user interaction first

### Low FPS
- **Check**: HUD shows current FPS
- **Solutions**:
  - Close other browser tabs
  - Use Chrome/Edge for WebGPU
  - Lower window size
  - Update graphics drivers

### Can't See Anything
- **Check**: You might be inside a wall
- **Solution**: Use Space/Shift to move up/down
- **Reset**: Refresh the page to restart at spawn point

### Mouse Too Sensitive/Slow
- **Solution**: Adjust "Look Speed" slider in settings panel
- **Range**: 0.5 (slow) to 5.0 (fast)

### Movement Too Fast/Slow
- **Solution**: Adjust "Move Speed" slider in settings panel
- **Range**: 1 (slow) to 20 (fast)

## 📚 Next Steps

### Learn More
- Read the full documentation: `BSP_DOOM_EXPLORER.md`
- Explore the code: `src/components/BSP/BSPDoomExplorer.tsx`
- Check the test page: `src/pages/BSPDoomExplorerTest.tsx`

### Integrate Into Your App

```tsx
import { BSPDoomExplorer } from './components/BSP';

function MyApp() {
  return (
    <BSPDoomExplorer
      width={1200}
      height={800}
      onRegionChange={(region) => {
        console.log('Entered:', region.name);
      }}
    />
  );
}
```

### Extend the Component
- Add sound effects for region transitions
- Implement real BSP tree data from API
- Add texture mapping to walls
- Create procedural BSP generation
- Add VR support with WebXR

## 🎓 Understanding BSP Trees

### Why DOOM Used BSP
- **Fast rendering** - Efficient visibility determination
- **Collision detection** - Quick spatial queries
- **Level structure** - Natural way to organize 3D space

### How It Works
1. **Partition** - Split space with a plane
2. **Recurse** - Repeat for each side
3. **Traverse** - Walk the tree to render/query
4. **Optimize** - Choose good partition planes

### Musical Application
- **Tonal space** - 12-dimensional pitch class space
- **Partitions** - Musical relationships (fifths, thirds, etc.)
- **Regions** - Scales, modes, chord families
- **Queries** - Find similar chords, analyze progressions

## 🌟 Have Fun!

The BSP DOOM Explorer is designed to make abstract musical theory concepts tangible and fun. Explore, experiment, and enjoy navigating through tonal space!

**Happy exploring! 🎮🎸**

---

## Quick Links

- **Test Page**: http://localhost:5173/test/bsp-doom-explorer
- **Test Index**: http://localhost:5173/test
- **Full Docs**: `BSP_DOOM_EXPLORER.md`
- **Implementation**: `BSP_DOOM_EXPLORER_IMPLEMENTATION.md`

