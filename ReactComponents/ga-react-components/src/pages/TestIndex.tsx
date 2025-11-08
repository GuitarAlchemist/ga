import React from 'react';
import {
  Container,
  Box,
  Typography,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Chip
} from '@mui/material';
import { useNavigate } from 'react-router-dom';

interface TestPageInfo {
  id: string;
  title: string;
  description: string;
  technology: string;
  path: string;
  features: string[];
  status: 'complete' | 'partial' | 'basic';
}

const testPages: TestPageInfo[] = [
  {
    id: 'minimal-three',
    title: 'MinimalThreeInstrument',
    description: 'Universal 3D instrument renderer - supports ALL instruments from YAML database',
    technology: 'Three.js + WebGPU',
    path: '/test/minimal-three',
    features: ['60+ Instruments', 'Adaptive Geometry', 'WebGPU', 'YAML Database', 'Universal'],
    status: 'complete',
  },
  {
    id: 'three',
    title: 'ThreeFretboard',
    description: '3D fretboard with WebGPU/WebGL rendering using Three.js',
    technology: 'Three.js + WebGPU',
    path: '/test/three-fretboard',
    features: ['3D Rendering', 'PBR Materials', 'Orbit Controls', 'Fullscreen', 'Capo', 'Left-handed'],
    status: 'complete',
  },
  {
    id: 'headstock',
    title: 'ThreeHeadstock',
    description: '3D guitar headstock with different styles and tuning pegs',
    technology: 'Three.js + WebGPU',
    path: '/test/three-headstock',
    features: ['3D Rendering', 'Multiple Styles', 'Tuning Pegs', 'Guitar Models', 'Left-handed'],
    status: 'complete',
  },
  {
    id: 'realistic',
    title: 'RealisticFretboard',
    description: '2D realistic fretboard with wood grain and detailed rendering',
    technology: 'Pixi.js v7',
    path: '/test/realistic-fretboard',
    features: ['Wood Grain', 'Wound Strings', 'Headstock', 'Bridge', 'Capo', 'Left-handed'],
    status: 'complete',
  },
  {
    id: 'webgpu',
    title: 'WebGPUFretboard',
    description: 'Modern WebGPU fretboard using Pixi.js v8',
    technology: 'Pixi.js v8 WebGPU',
    path: '/test/webgpu-fretboard',
    features: ['WebGPU', 'Capo', 'Left-handed', 'Inlays', 'Interactive'],
    status: 'partial',
  },
  {
    id: 'guitar',
    title: 'GuitarFretboard',
    description: 'Legacy SVG-based fretboard (Delphi port)',
    technology: 'SVG',
    path: '/test/guitar-fretboard',
    features: ['SVG', 'Position Markers', 'Fret Numbers', 'String Labels'],
    status: 'basic',
  },
  {
    id: 'bsp',
    title: 'BSP Musical Analysis',
    description: 'Binary Space Partitioning for advanced musical analysis and chord relationships',
    technology: 'React + Material-UI + BSP API',
    path: '/test/bsp',
    features: ['Spatial Queries', 'Tonal Context', 'Progression Analysis', 'Sub-ms Performance', 'API Integration'],
    status: 'complete',
  },
  {
    id: 'music-hierarchy',
    title: 'Music Hierarchy Navigator',
    description: 'GraphQL-driven explorer for Set Classes → Voicings → Scales with master-detail selectors',
    technology: 'React + GraphQL + Material-UI',
    path: '/test/music-hierarchy',
    features: ['GraphQL', 'Master-Detail', 'Atonal Hierarchy', 'Chord Voicings', 'Scale Mapping'],
    status: 'complete',
  },
  {
    id: 'floor0-navigation',
    title: 'Floor 0 Navigation (E2E)',
    description: 'End-to-end demo: Backend room generation → 3D visualization → Navigation',
    technology: 'Three.js + Backend API',
    path: '/test/floor0-navigation',
    features: ['Backend Integration', '3D Rooms', 'Corridors', 'Set Classes', 'Real-time Loading'],
    status: 'complete',
  },
  {
    id: 'harmonic-navigator-3d',
    title: 'Harmonic Navigator 3D',
    description: '3D harmonic space navigation with BSP tetrahedral cells, quaternion modulation, and Plücker voice-leading',
    technology: 'Three.js + WebGL + BSP',
    path: '/test/harmonic-navigator-3d',
    features: ['BSP Cells', 'Quaternion Rotation', 'Plücker Lines', 'Interactive 3D', 'Mode/Scale Visualization'],
    status: 'complete',
  },
  {
    id: 'instrument-icons',
    title: 'Instrument Icons Gallery',
    description: 'SVG icons for all instruments from the YAML database - scalable, themeable, and API-integrated',
    technology: 'React + SVG + API',
    path: '/test/instrument-icons',
    features: ['60+ Instruments', 'SVG Icons', 'Scalable', 'Themeable', 'API Integration', 'Live Preview'],
    status: 'complete',
  },
  {
    id: 'bsp-doom-explorer',
    title: 'BSP DOOM Explorer',
    description: 'First-person DOOM-like navigation through BSP tree structure with partition walls and tonal regions',
    technology: 'Three.js + WebGPU + BSP',
    path: '/test/bsp-doom-explorer',
    features: ['First-Person', 'WASD Controls', 'Mouse Look', 'BSP Walls', 'Tonal Rooms', 'WebGPU', 'HUD', 'Minimap'],
    status: 'complete',
  },
  {
    id: 'fretboard-with-hand',
    title: 'Fretboard with Hand',
    description: '3D fretboard with hand pose visualization showing how to play chords using biomechanical modeling',
    technology: 'Three.js + WebGPU + API',
    path: '/test/fretboard-with-hand',
    features: ['3D Hand', 'Finger Positions', 'API Integration', 'Chord Voicings', 'WebGPU', 'Interactive'],
    status: 'partial',
  },
  {
    id: 'sunburst-3d',
    title: 'Sunburst 3D',
    description: '3D sunburst visualization with slope effect and LOD for hierarchical musical data exploration',
    technology: 'Three.js + WebGL',
    path: '/test/sunburst-3d',
    features: ['3D Sunburst', 'Slope Effect', 'LOD System', 'Interactive Zoom', 'Breadcrumb Trail', 'Auto-Rotate'],
    status: 'complete',
  },
  {
    id: 'immersive-musical-world',
    title: 'Immersive Musical World',
    description: 'Full 3D immersive world where musical hierarchy becomes a physical landscape with floating platforms',
    technology: 'Three.js + WebGL + First-Person',
    path: '/test/immersive-musical-world',
    features: ['First-Person', 'WASD Controls', 'Floating Platforms', 'Skybox', 'Particles', 'Dynamic Lighting', 'Shadows'],
    status: 'complete',
  },
  {
    id: 'fluffy-grass',
    title: 'Fluffy Grass',
    description: 'Realistic grass rendering with billboard technique, instanced rendering, and wind animation',
    technology: 'Three.js + Custom Shaders',
    path: '/test/fluffy-grass',
    features: ['Billboard Grass', 'Instancing', 'Chunking', 'Wind Animation', 'Custom Shaders', 'Fake AO', 'Color Variation'],
    status: 'complete',
  },
  {
    id: 'ocean',
    title: 'Ocean Shader',
    description: 'Realistic ocean water simulation with Gerstner waves, reflections, and dynamic lighting',
    technology: 'Three.js + GLSL Shaders',
    path: '/test/ocean',
    features: ['Gerstner Waves', 'Fresnel Effect', 'Specular Highlights', 'Sky Gradient', 'Real-time Animation'],
    status: 'complete',
  },
  {
    id: 'chord-progression',
    title: 'Chord Progression Visualizer',
    description: '3D visualization of chord progressions with circle of fifths and animated chord transitions',
    technology: 'Three.js + WebGL',
    path: '/test/chord-progression',
    features: ['Circle of Fifths', 'Color-Coded Chords', 'Auto-Progression', 'Stacked Notes', 'VSM Shadows', 'Interactive Camera'],
    status: 'complete',
  },
  {
    id: 'sand-dunes',
    title: 'Sand Dunes Terrain',
    description: 'Procedural desert landscape with ridged multifractal noise, micro ripples, and atmospheric fog',
    technology: 'Three.js + GLSL Shaders',
    path: '/test/sand-dunes',
    features: ['Ridged Multifractal', 'Micro Ripples', 'Slope Shading', 'Atmospheric Fog', 'Wind Direction', 'Procedural Generation'],
    status: 'complete',
  },
  {
    id: 'guitar-3d',
    title: 'Guitar 3D Viewer',
    description: 'Interactive 3D guitar model viewer with GLTF/GLB support, PBR materials, and IBL',
    technology: 'Three.js + GLTF Loader',
    path: '/test/guitar-3d',
    features: ['GLTF/GLB Loading', 'PBR Materials', 'IBL Environment', 'Auto-Centering', 'Shadow Casting', 'Progress Indicator'],
    status: 'complete',
  },
  {
    id: 'hand-animation',
    title: 'Hand Animation',
    description: 'Interactive hand animation with rigged 3D model, skeletal animation, and finger control',
    technology: 'Three.js + GLTF + Skeletal Animation',
    path: '/test/hand-animation',
    features: ['Rigged Hand Model', 'Skeletal Animation', 'Finger Control', 'Preset Poses', 'Auto-Animate', 'WebGPU/WebGL'],
    status: 'complete',
  },
  {
    id: 'models-3d',
    title: '3D Models Gallery',
    description: 'Interactive gallery showcasing all Blender-created 3D models with real-time controls and statistics',
    technology: 'Three.js + GLTF Loader + WebGL',
    path: '/test/models-3d',
    features: ['Model Switching', 'Auto-Rotation', 'Wireframe Mode', 'Real-time Stats', 'Orbit Controls', 'Multi-Light Setup'],
    status: 'complete',
  },
  {
    id: 'capo',
    title: 'Capo Test',
    description: 'Comparison of capo rendering across different fretboard components with geometric capo models',
    technology: 'Multiple Renderers',
    path: '/test/capo',
    features: ['Geometric Capo', 'Multiple Renderers', 'Position Control', 'Comparison View', 'Interactive'],
    status: 'complete',
  },
  {
    id: 'capo-model',
    title: '3D Capo Model Test',
    description: 'Integration of Sketchfab 3D capo model with automatic fallback to geometric representation',
    technology: 'Three.js + GLTF + Sketchfab',
    path: '/test/capo-model',
    features: ['3D Model Loading', 'Sketchfab Integration', 'Fallback Geometry', 'Real-time Positioning', 'CC Attribution'],
    status: 'complete',
  },
  {
    id: 'inverse-kinematics',
    title: 'Inverse Kinematics Demo',
    description: 'Interactive IK solver using genetic algorithms to find optimal hand poses for chord fingerings',
    technology: 'React + Three.js + REST API + GA',
    path: '/test/inverse-kinematics',
    features: ['Genetic Algorithm', 'Forward/Inverse Kinematics', 'Hand Visualization', 'Fitness Metrics', 'Real-time Solving', 'Biomechanics'],
    status: 'complete',
  },
  {
    id: 'tab-converter',
    title: 'Guitar Tab Format Converter',
    description: 'Convert between different guitar tablature formats with live preview and VexFlow rendering',
    technology: 'React + Material-UI + VexFlow + REST API',
    path: '/test/tab-converter',
    features: ['ASCII ↔ VexTab', 'Live Preview', 'VexFlow Rendering', 'File Upload/Download', 'Example Library', 'API Integration'],
    status: 'complete',
  },
  {
    id: 'grothendieck-dsl',
    title: 'Grothendieck Operations DSL',
    description: 'Interactive demo of category theory operations on musical objects with live parsing and AST visualization',
    technology: 'React + Material-UI + F# Parser + REST API',
    path: '/test/grothendieck-dsl',
    features: ['Live Parsing', 'AST Visualization', '15+ Examples', '7 Operation Categories', 'Category Theory', 'Musical Objects'],
    status: 'complete',
  },
  {
    id: 'chord-progression-dsl',
    title: 'Chord Progression DSL',
    description: 'Parse and analyze chord progressions using absolute chords or Roman numerals with metadata support',
    technology: 'React + Material-UI + F# Parser + REST API',
    path: '/test/chord-progression-dsl',
    features: ['Absolute Chords', 'Roman Numerals', 'Metadata (Key/Time/Tempo)', 'Jazz Standards', 'Live Parsing', 'AST Visualization'],
    status: 'complete',
  },
  {
    id: 'fretboard-navigation-dsl',
    title: 'Fretboard Navigation DSL',
    description: 'Navigate guitar fretboard using positions, CAGED shapes, movements, and slides',
    technology: 'React + Material-UI + F# Parser + REST API',
    path: '/test/fretboard-navigation-dsl',
    features: ['Position Notation', 'CAGED Shapes', 'Movement Commands', 'Slide Commands', 'Live Parsing', 'AST Visualization'],
    status: 'complete',
  },
];

const statusColors = {
  complete: 'success',
  partial: 'warning',
  basic: 'info',
} as const;

const statusLabels = {
  complete: 'Complete',
  partial: 'Partial',
  basic: 'Basic',
};

export const TestIndex: React.FC = () => {
  const navigate = useNavigate();

  return (
    <Container maxWidth="xl" sx={{ py: 4 }}>
      <Box sx={{ mb: 3 }}>
        <Typography variant="h4" gutterBottom>
          🎸 Component Test Suite
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Click any row to open the test page
        </Typography>
      </Box>

      <TableContainer component={Paper} sx={{ boxShadow: 3 }}>
        <Table size="small" sx={{ '& .MuiTableCell-root': { py: 1 } }}>
          <TableHead>
            <TableRow sx={{ backgroundColor: 'primary.main' }}>
              <TableCell sx={{ color: 'white', fontWeight: 'bold', width: '20%' }}>Component</TableCell>
              <TableCell sx={{ color: 'white', fontWeight: 'bold', width: '35%' }}>Description</TableCell>
              <TableCell sx={{ color: 'white', fontWeight: 'bold', width: '20%' }}>Technology</TableCell>
              <TableCell sx={{ color: 'white', fontWeight: 'bold', width: '20%' }}>Features</TableCell>
              <TableCell sx={{ color: 'white', fontWeight: 'bold', width: '5%', textAlign: 'center' }}>Status</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {testPages.map((page) => (
              <TableRow
                key={page.id}
                onClick={() => navigate(page.path)}
                sx={{
                  cursor: 'pointer',
                  '&:hover': {
                    backgroundColor: 'action.hover',
                  },
                  '&:nth-of-type(odd)': {
                    backgroundColor: 'action.selected',
                  },
                }}
              >
                <TableCell sx={{ fontWeight: 'medium' }}>{page.title}</TableCell>
                <TableCell sx={{ fontSize: '0.875rem' }}>{page.description}</TableCell>
                <TableCell sx={{ fontSize: '0.875rem', color: 'primary.main' }}>{page.technology}</TableCell>
                <TableCell>
                  <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
                    {page.features.slice(0, 3).map((feature) => (
                      <Chip
                        key={feature}
                        label={feature}
                        size="small"
                        variant="outlined"
                        sx={{ fontSize: '0.7rem', height: '20px' }}
                      />
                    ))}
                    {page.features.length > 3 && (
                      <Chip
                        label={`+${page.features.length - 3}`}
                        size="small"
                        sx={{ fontSize: '0.7rem', height: '20px' }}
                      />
                    )}
                  </Box>
                </TableCell>
                <TableCell sx={{ textAlign: 'center' }}>
                  <Chip
                    label={statusLabels[page.status]}
                    color={statusColors[page.status]}
                    size="small"
                    sx={{ fontSize: '0.7rem', height: '20px' }}
                  />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Container>
  );
};

export default TestIndex;
