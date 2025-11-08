# Guitar Alchemist - Navigation Guide

## Overview

The Guitar Alchemist frontend now features a comprehensive navigation system with:
- **React Router** for client-side routing
- **Breadcrumbs** for navigation context
- **Drawer Menu** for easy access to all pages
- **Home Page** with feature overview
- **Demo Index** showcasing all demo applications

## Accessing the Application

**Development Server**: http://localhost:5173

## Navigation Structure

### Main Pages

1. **Home** (`/`)
   - Landing page with feature overview
   - Quick links to main sections
   - Feature highlights

2. **Harmonic Studio** (`/harmonic-studio`)
   - Context-first fretboard explorer
   - Key selector and context panel
   - Chord palette and progression explorer
   - Fretboard workbench

3. **AI Copilot** (`/ai-copilot`)
   - Chat interface with AI assistant
   - Music theory Q&A
   - Practice suggestions

4. **Music Generation** (`/music-generation`)
   - AI-powered music generation using Hugging Face models
   - Text-to-audio generation
   - Model selection (MusicGen, Stable Audio, Riffusion)
   - Audio playback and download

5. **Demos** (`/demos`)
   - Index of all demo applications
   - Links to experimental features

### Demo Applications

All demos are accessible from the `/demos` route:

- **BSP Explorer** (`/demos/bsp`)
  - 3D DOOM-style level explorer
  - Music theory concepts in 3D space

- **Chord Naming** (`/demos/chord-naming`)
  - Advanced chord naming system
  - Context-aware chord identification

- **Advanced Mathematics** (`/demos/advanced-math`)
  - Mathematical foundations of music theory
  - Group theory and algebraic structures

- **Performance Optimization** (`/demos/performance`)
  - Benchmarks and performance analysis
  - Algorithm efficiency testing

- **Psychoacoustic Voicing** (`/demos/psychoacoustic`)
  - Intelligent chord voicing
  - Perceptual optimization

- **Practice Routine DSL** (`/demos/practice-routine`)
  - Domain-specific language for practice
  - Intelligent scheduling

- **Internet Content** (`/demos/internet-content`)
  - Web content integration
  - Tabs and lessons from the internet

## How to Navigate

### Using the Menu

1. Click the **hamburger menu icon** (☰) in the top-left corner
2. Browse the navigation menu
3. Click any item to navigate to that page

### Using Breadcrumbs

- Breadcrumbs appear at the top of every page (except Home)
- Click any breadcrumb to navigate back to that level
- Home icon always takes you to the landing page

### Direct URLs

You can navigate directly to any page by entering the URL:
- http://localhost:5173/ - Home
- http://localhost:5173/harmonic-studio - Harmonic Studio
- http://localhost:5173/ai-copilot - AI Copilot
- http://localhost:5173/music-generation - Music Generation
- http://localhost:5173/demos - Demo Index
- http://localhost:5173/demos/bsp - BSP Explorer
- etc.

## Features

### Breadcrumb Navigation
- Shows your current location in the app hierarchy
- Click any breadcrumb to navigate back
- Automatically generated from the URL path

### Drawer Menu
- Accessible from any page via the menu icon
- Organized into main sections and demos
- Shows descriptions for each page
- Highlights the current page

### Responsive Design
- Works on desktop and mobile devices
- Material-UI components with dark theme
- Smooth transitions and hover effects

## Development

### Adding a New Page

1. Create a new component in `src/pages/` or `src/components/`
2. Add a route in `src/App.tsx`:
   ```tsx
   <Route path="/your-page" element={<YourComponent />} />
   ```
3. Add a navigation item in `src/components/Layout.tsx`:
   ```tsx
   { path: '/your-page', label: 'Your Page', icon: <YourIcon /> }
   ```

### File Structure

```
src/
├── components/
│   ├── Layout.tsx           # Main layout with nav and breadcrumbs
│   ├── dashboard/           # Dashboard components
│   │   ├── MusicGenerationDemo.tsx
│   │   ├── ChordPalette.tsx
│   │   └── ...
│   └── Chat/
│       └── ChatInterface.tsx
├── pages/
│   ├── Home.tsx             # Landing page
│   ├── HarmonicStudio.tsx   # Harmonic studio page
│   └── DemosIndex.tsx       # Demo index page
└── App.tsx                  # Main app with routing
```

## Technologies Used

- **React Router v6** - Client-side routing
- **Material-UI (MUI)** - UI components and theming
- **Jotai** - State management
- **Vite** - Build tool and dev server

## Next Steps

To add actual demo implementations:
1. Create components for each demo in `src/pages/demos/`
2. Update the routes in `App.tsx` to use the new components
3. Add any necessary API integrations
4. Update the demo descriptions in `DemosIndex.tsx`

## Support

For issues or questions:
- Check the console for errors
- Verify all files are in the correct locations
- Ensure the dev server is running (`npm run dev`)
- Check that the API is running on http://localhost:5232

