# 🎮 3D Floor Viewer Guide

## Overview

The **3D Floor Viewer** is an advanced visualization component that allows you to explore individual floors in both **2D Canvas** and **3D WebGL** modes. It features interactive room selection, music playback, and immersive 3D navigation.

## Features

### 🎨 Dual Rendering Modes

#### 2D Canvas Mode
- Traditional top-down view
- High-performance 2D rendering
- Clear room layout visualization
- Grid overlay for spatial reference
- Color-coded rooms (purple = music items, gray = empty)

#### 3D WebGL Mode (Three.js)
- Fully interactive 3D environment
- Rooms rendered as 3D boxes with varying heights
- Music rooms are taller (4 units) with purple glow
- Empty rooms are shorter (2 units) in gray
- Real-time shadows and lighting
- Orbit camera controls (rotate, pan, zoom)
- Click-to-select rooms
- Floating room number labels

### 🎵 Music Playback

- **Web Audio API Integration**: Play pitch classes as musical notes
- **Arpeggio Effect**: Notes play sequentially with slight delay
- **Equal Temperament Tuning**: Accurate pitch frequencies based on A4 = 440Hz
- **Envelope Shaping**: Attack, sustain, and release for natural sound

### 🖱️ Interactive Controls

#### 2D Mode
- Static top-down view
- Click room cards to select

#### 3D Mode
- **Left Click + Drag**: Rotate camera around the floor
- **Right Click + Drag**: Pan camera position
- **Scroll Wheel**: Zoom in/out
- **Click Room**: Select room and view details
- **Reset Camera**: Return to default view angle

## How to Use

### 1. Access the 3D Viewer

**From Floor List Page** (`/floors`):
1. Generate a floor (or all floors)
2. Click the **"🎮 3D View"** button on any floor card
3. You'll be redirected to `/floor/{0-5}`

**Direct URL**:
- Navigate to `http://localhost:5233/floor/0` (for Floor 0)
- Replace `0` with any floor number (0-5)

### 2. Switch Between 2D and 3D

- Click **"📐 2D View"** button for canvas rendering
- Click **"🎮 3D View"** button for WebGL rendering
- The view mode persists while exploring the same floor

### 3. Explore Rooms

**In 2D Mode**:
- Scroll through the room list at the bottom
- Click any room card to view details

**In 3D Mode**:
- Use mouse controls to navigate the 3D scene
- Click directly on room boxes to select them
- Purple glowing rooms contain music items
- Taller rooms = music items, shorter rooms = empty

### 4. View Room Details

When a room is selected, a **details panel** appears on the right showing:
- Room ID and position
- Room dimensions
- Music item name (if present)
- Category (Set Class, Chord, etc.)
- Pitch classes as colored badges
- Description (if available)

### 5. Play Music

1. Select a room with a music item
2. Click the **"🎵 Play Sound"** button in the details panel
3. Hear the pitch classes played as musical notes
4. Notes play in arpeggio style (one after another)

### 6. Regenerate Floor

- Adjust **Floor Size** (40-120)
- Enter a **Seed** for reproducible generation
- Click **"🔄 Regenerate"** to create a new layout

## Technical Details

### 3D Rendering Stack

**Three.js (r128)**:
- Scene management
- 3D geometry and materials
- Lighting and shadows
- Camera and controls

**OrbitControls**:
- Mouse-based camera manipulation
- Smooth damping for natural movement
- Configurable zoom and pan limits

**Raycasting**:
- Mouse picking for room selection
- Accurate 3D object intersection detection

### Visual Elements

**Rooms**:
- Geometry: `BoxGeometry(width, height, depth)`
- Material: `MeshStandardMaterial` with PBR properties
- Music rooms: Purple (#764ba2) with emissive glow
- Empty rooms: Gray (#718096) with no emission
- Shadows: Both cast and receive

**Corridors**:
- Flat boxes (0.5 units tall)
- Dark gray (#4a5568)
- Connect rooms horizontally

**Floor**:
- Large plane mesh
- Dark background (#2d3748)
- Receives shadows from rooms

**Lighting**:
- Ambient light (40% intensity) for base illumination
- Directional light (80% intensity) with shadow casting
- Dynamic shadow mapping

**Grid**:
- Three.js GridHelper
- 20x20 divisions
- Helps with spatial orientation

### Audio System

**Web Audio API**:
- `AudioContext` for audio processing
- `OscillatorNode` for tone generation
- `GainNode` for volume control and envelopes

**Pitch Calculation**:
```javascript
// Convert pitch class (0-11) to frequency
const semitonesFromA4 = pitchClass - 9; // A is pitch class 9
const frequency = 440 * Math.pow(2, semitonesFromA4 / 12);
```

**Envelope**:
- Attack: 0.05s (fade in)
- Sustain: 0.5s (hold)
- Release: 1.0s (fade out)
- Total duration: ~1.5s per note

## Performance Considerations

### 2D Mode
- Very fast rendering (60+ FPS)
- Low memory usage
- Suitable for all devices
- No GPU requirements

### 3D Mode
- Requires WebGL support
- GPU-accelerated rendering
- Higher memory usage (~50-100MB)
- Recommended: Modern browser with hardware acceleration
- Performance scales with floor size and room count

### Optimization Tips

1. **Reduce Floor Size**: Use 60 instead of 80 for faster generation
2. **Disable Shadows**: Edit JavaScript to set `renderer3D.shadowMap.enabled = false`
3. **Lower Quality**: Reduce `antialias` in WebGLRenderer options
4. **Use 2D Mode**: Switch to 2D for lower-end devices

## Browser Compatibility

### Supported Browsers

✅ **Chrome/Edge** (v90+): Full support, best performance
✅ **Firefox** (v88+): Full support, good performance
✅ **Safari** (v14+): Full support, requires WebGL 2.0
⚠️ **Mobile Browsers**: Limited support, touch controls not optimized

### Required Features

- WebGL 2.0 (for 3D mode)
- Web Audio API (for music playback)
- ES6 JavaScript (arrow functions, async/await)
- Canvas API (for 2D mode)

## Keyboard Shortcuts (Planned)

Future enhancements will include:
- `W/A/S/D`: Move camera
- `Q/E`: Rotate camera
- `Space`: Reset camera
- `1-6`: Switch floors
- `M`: Toggle music
- `Esc`: Close details panel

## Troubleshooting

### 3D View Not Loading

**Problem**: Black screen or "Container not found" error
**Solution**:
1. Check browser console for errors
2. Ensure Three.js CDN is accessible
3. Verify WebGL is enabled in browser settings
4. Try switching to 2D mode

### Music Not Playing

**Problem**: No sound when clicking "Play Sound"
**Solution**:
1. Check browser audio permissions
2. Unmute browser tab
3. Increase system volume
4. Check browser console for Web Audio API errors

### Poor 3D Performance

**Problem**: Low FPS, stuttering, lag
**Solution**:
1. Reduce floor size to 60 or 40
2. Switch to 2D mode
3. Close other browser tabs
4. Update graphics drivers
5. Enable hardware acceleration in browser

### Rooms Not Clickable in 3D

**Problem**: Clicking rooms doesn't select them
**Solution**:
1. Ensure you're clicking directly on room boxes
2. Try zooming in closer
3. Check browser console for raycasting errors
4. Use room list at bottom as alternative

## API Integration

The 3D viewer fetches floor data from:
```
GET http://localhost:5232/api/music-rooms/floor/{0-5}?floorSize=80&seed=42
```

Response includes:
- Floor metadata (name, dimensions, seed)
- Room array (position, size, music items)
- Corridor array (start/end points, width)
- Music data (total items, target rooms)

## Future Enhancements

### Phase 1: Navigation
- [ ] Stairs between floors (3D models)
- [ ] Vertical floor transitions
- [ ] Multi-floor stacking view
- [ ] Minimap overlay

### Phase 2: Player System
- [ ] First-person camera mode
- [ ] WASD movement controls
- [ ] Collision detection
- [ ] Visited room tracking
- [ ] Fog of war effect

### Phase 3: Visual Effects
- [ ] Particle effects for music rooms
- [ ] Animated room transitions
- [ ] Dynamic lighting based on music
- [ ] Post-processing (bloom, SSAO)

### Phase 4: Audio
- [ ] Multiple instrument sounds
- [ ] Chord playback (simultaneous notes)
- [ ] Reverb and spatial audio
- [ ] Background ambient music

### Phase 5: Multiplayer
- [ ] Real-time collaboration
- [ ] Multiple player avatars
- [ ] Shared exploration
- [ ] Chat system

## Code Examples

### Selecting a Room Programmatically

```javascript
// From Blazor C#
await JSRuntime.InvokeVoidAsync("selectRoom3D", roomId);
```

### Customizing Camera Position

```javascript
// In floor-renderer.js
camera3D.position.set(x, y, z);
controls3D.target.set(targetX, targetY, targetZ);
controls3D.update();
```

### Changing Room Colors

```javascript
// Find room mesh
const roomMesh = roomMeshes3D.find(m => m.userData.roomId === roomId);
// Change color
roomMesh.material.color.setHex(0xff0000); // Red
```

## Resources

- **Three.js Documentation**: https://threejs.org/docs/
- **Web Audio API Guide**: https://developer.mozilla.org/en-US/docs/Web/API/Web_Audio_API
- **OrbitControls**: https://threejs.org/docs/#examples/en/controls/OrbitControls
- **WebGL Tutorial**: https://webglfundamentals.org/

---

**Enjoy exploring the music theory dungeon in 3D!** 🎮🎵🏰

