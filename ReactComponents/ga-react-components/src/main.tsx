import React, { useState } from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import App from './App';
import GuitarFretboard, { FretboardPosition } from './components/GuitarFretboard';
import RealisticFretboard from './components/RealisticFretboard';
import { ThreeFretboard } from './components/ThreeFretboard';
import { Container, Box, Typography, Button, Stack } from '@mui/material';

// Test pages
import TestIndex from './pages/TestIndex';
import ThreeFretboardTest from './pages/ThreeFretboardTest';
import ThreeHeadstockTest from './pages/ThreeHeadstockTest';
import RealisticFretboardTest from './pages/RealisticFretboardTest';
import GuitarFretboardTest from './pages/GuitarFretboardTest';
import WebGPUFretboardTest from './pages/WebGPUFretboardTest';
import CapoTest from './pages/CapoTest';
import { CapoModelTest } from './pages/CapoModelTest';
import MinimalThreeTest from './pages/MinimalThreeTest';
import MusicTheoryTest from './pages/MusicTheoryTest';
import BSPTest from './pages/BSPTest';
import HarmonicNavigator3DTest from './pages/HarmonicNavigator3DTest';
import InstrumentIconsTest from './pages/InstrumentIconsTest';
import BSPDoomExplorerTest from './pages/BSPDoomExplorerTest';
import FretboardWithHandTest from './pages/FretboardWithHandTest';
import Sunburst3DTest from './pages/Sunburst3DTest';
import ImmersiveMusicalWorldTest from './pages/ImmersiveMusicalWorldTest';
import FluffyGrassTest from './pages/FluffyGrassTest';
import OceanTest from './pages/OceanTest';
import SandDunesTest from './pages/SandDunesTest';
import { GraphitiDemo } from './components/GraphitiDemo/GraphitiDemo';
import Guitar3DTest from './pages/Guitar3DTest';
import HandAnimationTest from './pages/HandAnimationTest';
import Models3DTest from './pages/Models3DTest';
import Floor0NavigationTest from './pages/Floor0NavigationTest';
import TabConverterTest from './pages/TabConverterTest';
import GrothendieckDSLTest from './pages/GrothendieckDSLTest';
import ChordProgressionDSLTest from './pages/ChordProgressionDSLTest';
import FretboardNavigationDSLTest from './pages/FretboardNavigationDSLTest';
import InverseKinematicsTest from './pages/InverseKinematicsTest';
import MusicHierarchyDemo from './pages/MusicHierarchyDemo';

// Example component to demonstrate the library
const DemoApp = () => {
  const [selectedPositions, setSelectedPositions] = useState<FretboardPosition[]>([]);

  // Example: C Major chord positions (CORRECT - open position)
  // String numbering: 0 = high E (thinnest), 5 = low E (thickest)
  const cMajorChord: FretboardPosition[] = [
    { string: 0, fret: 0, label: 'E', color: '#4dabf7' },           // High E string - open (E note)
    { string: 1, fret: 1, label: 'C', color: '#ff6b6b', emphasized: true }, // B string - 1st fret (C note - root)
    { string: 2, fret: 0, label: 'G', color: '#51cf66' },           // G string - open (G note)
    { string: 3, fret: 2, label: 'E', color: '#4dabf7' },           // D string - 2nd fret (E note)
    { string: 4, fret: 3, label: 'C', color: '#ff6b6b', emphasized: true }, // A string - 3rd fret (C note - root)
    // Low E string (string 5) - not played in standard C major, or optionally 3rd fret for C
    { string: 5, fret: 3, label: 'C', color: '#ff6b6b', emphasized: true }, // Low E string - 3rd fret (C note - optional)
  ];

  // Example: C Major scale positions
  const cMajorScale: FretboardPosition[] = [
    { string: 0, fret: 0, label: 'C', emphasized: true },
    { string: 0, fret: 2, label: 'D' },
    { string: 0, fret: 4, label: 'E' },
    { string: 1, fret: 0, label: 'G' },
    { string: 1, fret: 2, label: 'A' },
    { string: 1, fret: 3, label: 'B' },
    { string: 2, fret: 0, label: 'C', emphasized: true },
    { string: 2, fret: 2, label: 'D' },
    { string: 3, fret: 0, label: 'E' },
    { string: 3, fret: 2, label: 'F#' },
    { string: 4, fret: 0, label: 'G' },
    { string: 4, fret: 2, label: 'A' },
    { string: 5, fret: 0, label: 'C', emphasized: true },
  ];

  const handlePositionClick = (position: FretboardPosition) => {
    console.log('Position clicked:', position);
  };

  return (
    <App>
      <Box sx={{
        width: '100vw',
        height: '100vh',
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
        bgcolor: '#f5f5f5'
      }}>
        {/* Header */}
        <Box sx={{
          p: 3,
          bgcolor: 'white',
          borderBottom: '1px solid #e0e0e0',
          flexShrink: 0
        }}>
          <Typography variant="h3" sx={{ mb: 1, fontWeight: 'bold' }}>
            🎸 Guitar Alchemist React Components
          </Typography>
          <Typography variant="subtitle1" sx={{ color: '#666' }}>
            Component library is running in development mode.
          </Typography>
        </Box>

        {/* Main Content Area */}
        <Box sx={{
          flex: 1,
          overflow: 'auto',
          p: 3
        }}>
        <Stack spacing={4}>
          {/* Three.js 3D Fretboard Section - AT TOP */}
          <Box>
            <Typography variant="h5" sx={{ mb: 3, fontWeight: 'bold' }}>
              🎸 3D Fretboard (Three.js + WebGPU)
            </Typography>
            <Typography variant="body2" sx={{ mb: 3, color: '#666' }}>
              Real 3D rendering with WebGPU support, orbit controls, and realistic guitar geometry
            </Typography>

            {/* C Major Chord - 3D */}
            <Box sx={{ mb: 4 }}>
              <ThreeFretboard
                title="C Major Chord - 3D (Three.js)"
                positions={cMajorChord}
                config={{
                  fretCount: 22,
                  stringCount: 6,
                  tuning: ['E', 'B', 'G', 'D', 'A', 'E'],
                  showFretNumbers: true,
                  showStringLabels: true,
                  width: 1200,
                  height: 600,
                  guitarModel: 'electric_fender_strat',
                  enableOrbitControls: true,
                }}
                onPositionClick={(str, fret) => console.log(`3D Clicked: String ${str}, Fret ${fret}`)}
              />
            </Box>
          </Box>

          {/* Realistic Fretboard Section - MOVED DOWN */}
          <Box>
            <Typography variant="h5" sx={{ mb: 3, fontWeight: 'bold' }}>
              🎸 Realistic Fretboard (Pixi.js with WebGPU)
            </Typography>
            <Typography variant="body2" sx={{ mb: 3, color: '#666' }}>
              High-performance 2D rendering with wood texture, realistic fret spacing, and guitar model selection
            </Typography>

            {/* C Major Chord - Realistic */}
            <Box sx={{ mb: 4, overflowX: 'auto' }}>
              <RealisticFretboard
                title="C Major Chord - Realistic"
                positions={cMajorChord}
                config={{
                  fretCount: 22,
                  stringCount: 6,
                  tuning: ['E', 'B', 'G', 'D', 'A', 'E'],
                  showFretNumbers: true,
                  showStringLabels: true,
                  width: 1600,
                  height: 180,
                  spacingMode: 'realistic',
                  flipped: true,
                }}
                onPositionClick={(str, fret) => console.log(`Clicked: String ${str}, Fret ${fret}`)}
              />
            </Box>
          </Box>

          {/* Original Fretboard Demo Section - MOVED DOWN */}
          <Box>
            <Typography variant="h5" sx={{ mb: 3, fontWeight: 'bold' }}>
              SVG Fretboard Component (Legacy)
            </Typography>

            {/* Schematic Mode Section */}
            <Box sx={{ mb: 6 }}>
              <Typography variant="h6" sx={{ mb: 2, color: '#555', fontStyle: 'italic' }}>
                Schematic Mode (Linear Spacing)
              </Typography>

              {/* C Major Chord - Schematic */}
              <Box sx={{ mb: 4 }}>
                <GuitarFretboard
                  title="C Major Chord - Schematic"
                  positions={cMajorChord}
                  displayMode="chord"
                  config={{
                    fretCount: 12,
                    stringCount: 6,
                    tuning: ['E', 'B', 'G', 'D', 'A', 'E'],
                    showFretNumbers: true,
                    showStringLabels: true,
                    width: 800,
                    height: 150,
                    spacingMode: 'schematic',
                  }}
                  onPositionClick={handlePositionClick}
                />
              </Box>

              {/* C Major Scale - Schematic */}
              <Box sx={{ mb: 4 }}>
                <GuitarFretboard
                  title="C Major Scale - Schematic"
                  positions={cMajorScale}
                  displayMode="scale"
                  config={{
                    fretCount: 12,
                    stringCount: 6,
                    tuning: ['E', 'B', 'G', 'D', 'A', 'E'],
                    showFretNumbers: true,
                    showStringLabels: true,
                    width: 800,
                    height: 150,
                    spacingMode: 'schematic',
                  }}
                  onPositionClick={handlePositionClick}
                />
              </Box>
            </Box>

            {/* Realistic Mode Section */}
            <Box sx={{ mb: 6 }}>
              <Typography variant="h6" sx={{ mb: 2, color: '#555', fontStyle: 'italic' }}>
                Realistic Mode (Logarithmic Spacing - Like Real Guitars)
              </Typography>

              {/* C Major Chord - Realistic */}
              <Box sx={{ mb: 4 }}>
                <GuitarFretboard
                  title="C Major Chord - Realistic"
                  positions={cMajorChord}
                  displayMode="chord"
                  config={{
                    fretCount: 12,
                    stringCount: 6,
                    tuning: ['E', 'B', 'G', 'D', 'A', 'E'],
                    showFretNumbers: true,
                    showStringLabels: true,
                    width: 800,
                    height: 150,
                    spacingMode: 'realistic',
                  }}
                  onPositionClick={handlePositionClick}
                />
              </Box>

              {/* C Major Scale - Realistic */}
              <Box sx={{ mb: 4 }}>
                <GuitarFretboard
                  title="C Major Scale - Realistic"
                  positions={cMajorScale}
                  displayMode="scale"
                  config={{
                    fretCount: 12,
                    stringCount: 6,
                    tuning: ['E', 'B', 'G', 'D', 'A', 'E'],
                    showFretNumbers: true,
                    showStringLabels: true,
                    width: 800,
                    height: 150,
                    spacingMode: 'realistic',
                  }}
                  onPositionClick={handlePositionClick}
                />
              </Box>
            </Box>

            {/* Empty Fretboard - Both Modes */}
            <Box>
              <Typography variant="h6" sx={{ mb: 2, color: '#555', fontStyle: 'italic' }}>
                Empty Fretboard Comparison
              </Typography>

              <Box sx={{ mb: 4 }}>
                <GuitarFretboard
                  title="Empty Fretboard - Schematic"
                  positions={[]}
                  displayMode="chord"
                  config={{
                    fretCount: 24,
                    stringCount: 6,
                    tuning: ['E', 'B', 'G', 'D', 'A', 'E'],
                    showFretNumbers: true,
                    showStringLabels: true,
                    width: 800,
                    height: 150,
                    spacingMode: 'schematic',
                  }}
                />
              </Box>

              <Box>
                <GuitarFretboard
                  title="Empty Fretboard - Realistic"
                  positions={[]}
                  displayMode="chord"
                  config={{
                    fretCount: 24,
                    stringCount: 6,
                    tuning: ['E', 'B', 'G', 'D', 'A', 'E'],
                    showFretNumbers: true,
                    showStringLabels: true,
                    width: 800,
                    height: 150,
                    spacingMode: 'realistic',
                  }}
                />
              </Box>
            </Box>
          </Box>


        </Stack>
        </Box>
      </Box>
    </App>
  );
};

const GraphitiDemoTest: React.FC = () => {
  return (
    <App>
      <Container maxWidth="xl" sx={{ py: 4 }}>
        <Typography variant="h4" component="h1" gutterBottom>
          🎸 Graphiti Temporal Knowledge Graphs Demo
        </Typography>
        <Typography variant="body1" sx={{ mb: 3 }}>
          Experience the power of temporal knowledge graphs for music learning with real-time AI recommendations and progress tracking.
        </Typography>
        <GraphitiDemo />
      </Container>
    </App>
  );
};

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <BrowserRouter>
      <Routes>
        {/* Test pages */}
        <Route path="/test" element={<App><TestIndex /></App>} />
        <Route path="/test/minimal-three" element={<App><MinimalThreeTest /></App>} />
        <Route path="/test/music-theory" element={<App><MusicTheoryTest /></App>} />
        <Route path="/test/three-fretboard" element={<App><ThreeFretboardTest /></App>} />
        <Route path="/test/three-headstock" element={<App><ThreeHeadstockTest /></App>} />
        <Route path="/test/realistic-fretboard" element={<App><RealisticFretboardTest /></App>} />
        <Route path="/test/guitar-fretboard" element={<App><GuitarFretboardTest /></App>} />
        <Route path="/test/webgpu-fretboard" element={<App><WebGPUFretboardTest /></App>} />
        <Route path="/test/capo" element={<App><CapoTest /></App>} />
        <Route path="/test/graphiti-demo" element={<App><GraphitiDemoTest /></App>} />
        <Route path="/test/capo-model" element={<App><CapoModelTest /></App>} />
        <Route path="/test/bsp" element={<App><BSPTest /></App>} />
        <Route path="/test/music-hierarchy" element={<App><MusicHierarchyDemo /></App>} />
        <Route path="/test/harmonic-navigator-3d" element={<App><HarmonicNavigator3DTest /></App>} />
        <Route path="/test/instrument-icons" element={<App><InstrumentIconsTest /></App>} />
        <Route path="/test/bsp-doom-explorer" element={<App><BSPDoomExplorerTest /></App>} />
        <Route path="/test/fretboard-with-hand" element={<App><FretboardWithHandTest /></App>} />
        <Route path="/test/sunburst-3d" element={<App><Sunburst3DTest /></App>} />
        <Route path="/test/immersive-musical-world" element={<App><ImmersiveMusicalWorldTest /></App>} />
        <Route path="/test/fluffy-grass" element={<App><FluffyGrassTest /></App>} />
        <Route path="/test/ocean" element={<App><OceanTest /></App>} />
        <Route path="/test/sand-dunes" element={<App><SandDunesTest /></App>} />
        <Route path="/test/guitar-3d" element={<App><Guitar3DTest /></App>} />
        <Route path="/test/hand-animation" element={<App><HandAnimationTest /></App>} />
        <Route path="/test/models-3d" element={<App><Models3DTest /></App>} />
        <Route path="/test/floor0-navigation" element={<App><Floor0NavigationTest /></App>} />
        <Route path="/test/tab-converter" element={<App><TabConverterTest /></App>} />
        <Route path="/test/grothendieck-dsl" element={<App><GrothendieckDSLTest /></App>} />
        <Route path="/test/chord-progression-dsl" element={<App><ChordProgressionDSLTest /></App>} />
        <Route path="/test/fretboard-navigation-dsl" element={<App><FretboardNavigationDSLTest /></App>} />
        <Route path="/test/inverse-kinematics" element={<App><InverseKinematicsTest /></App>} />

        {/* Demo page (original) */}
        <Route path="/demo" element={<DemoApp />} />

        {/* Default route - redirect to test index */}
        <Route path="/" element={<Navigate to="/test" replace />} />
      </Routes>
    </BrowserRouter>
  </React.StrictMode>
);
