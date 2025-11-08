# 🏰 Multi-Floor Dungeon Manager - Blazor Application

## Overview

The **Floor Manager** is a dedicated Blazor Server application for managing and visualizing the 6-floor music theory dungeon system. It provides an interactive web interface for exploring procedurally generated dungeons with integrated music theory content.

## Architecture

### Backend (GaApi)
- **Port**: `http://localhost:5232`
- **Endpoints**:
  - `GET /api/music-rooms/floor/{0-5}?floorSize=80&seed=42`
  - `GET /api/bsp-rooms/generate?seed=42`
- **Technology**: ASP.NET Core Web API (.NET 9)
- **Features**:
  - BSP (Binary Space Partitioning) dungeon generation
  - MongoDB persistence for layouts
  - Seed-based reproducible generation
  - Music theory data integration

### Frontend (FloorManager)
- **Port**: `http://localhost:5233`
- **Main Route**: `/floors`
- **Technology**: Blazor Server (.NET 9)
- **Features**:
  - Interactive floor visualization
  - Real-time canvas rendering
  - Room selection and details
  - Multi-floor navigation
  - Batch floor generation

## The 6 Floors

| Floor | Name | Music Theory Content | Status |
|-------|------|---------------------|--------|
| **0** | Set Classes | Chromatic, Diatonic, Pentatonic sets | ✅ Working |
| **1** | Forte Codes | Allen Forte's pitch class set notation | ✅ Working |
| **2** | Prime Forms | Normal form representations | ✅ Working |
| **3** | Chords | Triads, seventh chords, extended chords | ✅ Working |
| **4** | Chord Inversions | Root position, 1st, 2nd inversions | ✅ Working |
| **5** | Chord Voicings | Drop voicings, spread voicings | ✅ Working |

## Project Structure

```
Apps/FloorManager/
├── Components/
│   ├── Layout/
│   │   ├── MainLayout.razor
│   │   └── NavMenu.razor
│   ├── Pages/
│   │   ├── Home.razor
│   │   ├── FloorViewer.razor          # Main floor visualization page
│   │   └── FloorViewer.razor.css      # Scoped styles
│   └── App.razor
├── Services/
│   └── FloorService.cs                 # API client service
├── wwwroot/
│   └── js/
│       └── floor-renderer.js           # Canvas rendering logic
├── Properties/
│   └── launchSettings.json
├── Program.cs
└── FloorManager.csproj
```

## Key Components

### FloorService.cs
- HTTP client wrapper for GaApi
- Methods:
  - `GetFloorAsync(int floorNumber, int floorSize, int? seed)`
  - `GetAllFloorsAsync(int floorSize, int? seed)`
- Handles JSON deserialization and error logging

### FloorViewer.razor
- Main interactive component
- Features:
  - Floor selector buttons (0-5)
  - Floor size and seed configuration
  - Single floor or batch generation
  - Canvas-based dungeon rendering
  - Room selection and details panel
  - Music item display

### floor-renderer.js
- JavaScript canvas rendering
- Features:
  - Automatic scaling to fit canvas
  - Room rendering (purple for music items, gray for empty)
  - Corridor rendering
  - Grid overlay
  - Room numbering
  - Music note indicators (♪)

## Running the Application

### Prerequisites
1. **GaApi Backend** must be running on `http://localhost:5232`
2. **.NET 9 SDK** installed

### Start GaApi Backend
```bash
cd C:/Users/spare/source/repos/ga
dotnet run --project Apps/ga-server/GaApi/GaApi.csproj
```

### Start FloorManager Frontend
```bash
cd C:/Users/spare/source/repos/ga
dotnet run --project Apps/FloorManager/FloorManager.csproj
```

### Access the Application
Open your browser to: **http://localhost:5233/floors**

## Usage Guide

### 1. Generate a Single Floor
1. Select floor size (40-120, default: 80)
2. Optionally enter a seed for reproducible generation
3. Click "🎲 Generate Floor X" button
4. View the generated dungeon on the canvas

### 2. Generate All Floors
1. Configure floor size and seed (optional)
2. Click "🏗️ Generate All Floors" button
3. Wait for all 6 floors to generate
4. Navigate between floors using the floor selector buttons

### 3. Explore Rooms
1. Click on floor selector buttons to switch between floors
2. View room statistics in the floor info panel
3. Click on room cards in the "Rooms with Music Items" section
4. View detailed music theory information in the room details panel

### 4. Room Details
When a room with a music item is selected, you'll see:
- Room ID and position
- Room dimensions
- Music item name
- Category (e.g., "Set Class", "Chord")
- Pitch classes (e.g., [0, 2, 4, 5, 7, 9, 11])
- Description (if available)

## Visual Features

### Color Coding
- **Purple rooms** (🟪): Contain music theory items
- **Gray rooms** (🟦): Empty rooms
- **Corridors**: Dark gray connecting paths
- **Grid**: Semi-transparent white overlay

### Interactive Elements
- **Floor buttons**: 
  - White border: Not loaded
  - Green border: Loaded
  - Purple gradient: Currently selected
- **Room cards**: 
  - Hover effect with elevation
  - Purple gradient when selected
- **Canvas**: 
  - Centered dungeon layout
  - Automatic scaling
  - Room numbers displayed

## Testing Results

All 6 floors have been tested and verified working:

```
✅ Floor 0: OK - 22 rooms (Set Classes)
✅ Floor 1: OK - 39 rooms (Forte Codes)
✅ Floor 2: OK - 47 rooms (Prime Forms)
✅ Floor 3: OK - 47 rooms (Chords)
✅ Floor 4: OK - 47 rooms (Chord Inversions)
✅ Floor 5: OK - 47 rooms (Chord Voicings)
```

## Next Steps (Planned Features)

### Phase 1: Enhanced Interactivity
- [ ] Click on canvas to select rooms
- [ ] Hover tooltips on rooms
- [ ] Path highlighting between connected rooms
- [ ] Keyboard navigation (arrow keys)

### Phase 2: Stairs & Multi-Floor Navigation
- [ ] Staircase generation between floors
- [ ] Visual stair indicators (up/down arrows)
- [ ] Click stairs to navigate floors
- [ ] 3D floor stacking visualization

### Phase 3: Music Playback
- [ ] Web Audio API integration
- [ ] Play chords/scales on room entry
- [ ] Piano keyboard visualization
- [ ] Volume controls and mute toggle

### Phase 4: Player Movement
- [ ] Player avatar sprite
- [ ] WASD/Arrow key movement
- [ ] Collision detection
- [ ] Visited room tracking
- [ ] Fog of war effect

### Phase 5: 3D Visualization
- [ ] Three.js integration
- [ ] 3D floor stacking
- [ ] Camera controls (orbit, zoom, pan)
- [ ] Toggle 2D/3D views

### Phase 6: Advanced Features
- [ ] Save/load dungeon layouts
- [ ] Export to image/PDF
- [ ] Dungeon statistics dashboard
- [ ] Music theory learning mode
- [ ] Multiplayer exploration

## Technical Notes

### Performance
- Canvas rendering is optimized for 800x600 resolution
- Floor generation typically takes 200-600ms per floor
- All 6 floors can be generated in ~3-4 seconds

### Browser Compatibility
- Tested on modern browsers (Chrome, Edge, Firefox)
- Requires JavaScript enabled
- Canvas API support required

### MongoDB Persistence
- Layouts are automatically saved to MongoDB
- Seed-based generation allows reproducible dungeons
- Layouts can be retrieved by floor number and seed

## Troubleshooting

### "Failed to load floor X"
- Ensure GaApi backend is running on port 5232
- Check browser console for CORS errors
- Verify MongoDB is running and accessible

### Canvas not rendering
- Check browser console for JavaScript errors
- Ensure `floor-renderer.js` is loaded
- Verify canvas element exists in DOM

### Slow generation
- Reduce floor size (try 60 instead of 80)
- Check MongoDB connection performance
- Monitor server logs for errors

## Credits

- **BSP Algorithm**: Binary Space Partitioning for procedural dungeon generation
- **Music Theory**: Integration with GA.Business.Core music theory library
- **UI Framework**: Blazor Server with Bootstrap
- **Rendering**: HTML5 Canvas API

---

**Built with ❤️ using .NET 9, Blazor, and ASP.NET Core**

