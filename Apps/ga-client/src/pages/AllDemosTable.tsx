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
  Chip,
  Link,
} from '@mui/material';

interface DemoPageInfo {
  id: string;
  title: string;
  description: string;
  technology: string;
  url: string;
  features: string[];
  status: 'complete' | 'partial' | 'basic';
}

const demoPages: DemoPageInfo[] = [
  // Fretboard Demos
  {
    id: 'minimal-three',
    title: 'MinimalThreeInstrument',
    description: 'Universal 3D instrument renderer - supports ALL instruments from YAML database',
    technology: 'Three.js + WebGPU',
    url: 'http://localhost:5176/test/minimal-three',
    features: ['60+ Instruments', 'Adaptive Geometry', 'WebGPU', 'YAML Database', 'Universal'],
    status: 'complete',
  },
  {
    id: 'three',
    title: 'ThreeFretboard',
    description: '3D fretboard with WebGPU/WebGL rendering using Three.js',
    technology: 'Three.js + WebGPU',
    url: 'http://localhost:5176/test/three-fretboard',
    features: ['3D Rendering', 'PBR Materials', 'Orbit Controls', 'Fullscreen', 'Capo', 'Left-handed'],
    status: 'complete',
  },
  {
    id: 'headstock',
    title: 'ThreeHeadstock',
    description: '3D guitar headstock with different styles and tuning pegs',
    technology: 'Three.js + WebGPU',
    url: 'http://localhost:5176/test/three-headstock',
    features: ['3D Rendering', 'Multiple Styles', 'Tuning Pegs', 'Guitar Models', 'Left-handed'],
    status: 'complete',
  },
  {
    id: 'realistic',
    title: 'RealisticFretboard',
    description: '2D realistic fretboard with wood grain and detailed rendering',
    technology: 'Pixi.js v7',
    url: 'http://localhost:5176/test/realistic-fretboard',
    features: ['Wood Grain', 'Wound Strings', 'Headstock', 'Bridge', 'Capo', 'Left-handed'],
    status: 'complete',
  },
  {
    id: 'webgpu',
    title: 'WebGPUFretboard',
    description: 'Modern WebGPU fretboard using Pixi.js v8',
    technology: 'Pixi.js v8 WebGPU',
    url: 'http://localhost:5176/test/webgpu-fretboard',
    features: ['WebGPU', 'Capo', 'Left-handed', 'Inlays', 'Interactive'],
    status: 'partial',
  },
  {
    id: 'guitar',
    title: 'GuitarFretboard',
    description: 'Legacy SVG-based fretboard (Delphi port)',
    technology: 'SVG',
    url: 'http://localhost:5176/test/guitar-fretboard',
    features: ['SVG', 'Position Markers', 'Fret Numbers', 'String Labels'],
    status: 'basic',
  },
  {
    id: 'fretboard-with-hand',
    title: 'Fretboard with Hand',
    description: '3D fretboard with hand pose visualization showing how to play chords',
    technology: 'Three.js + WebGPU + API',
    url: 'http://localhost:5176/test/fretboard-with-hand',
    features: ['3D Hand', 'Finger Positions', 'API Integration', 'Chord Voicings', 'WebGPU'],
    status: 'partial',
  },
  {
    id: 'capo',
    title: 'Capo Test',
    description: 'Comparison of capo rendering across different fretboard components',
    technology: 'Multiple Renderers',
    url: 'http://localhost:5176/test/capo',
    features: ['Geometric Capo', 'Multiple Renderers', 'Position Control', 'Comparison View'],
    status: 'complete',
  },
  {
    id: 'capo-model',
    title: '3D Capo Model Test',
    description: 'Integration of Sketchfab 3D capo model with automatic fallback',
    technology: 'Three.js + GLTF + Sketchfab',
    url: 'http://localhost:5176/test/capo-model',
    features: ['3D Model Loading', 'Sketchfab Integration', 'Fallback Geometry', 'CC Attribution'],
    status: 'complete',
  },

  // 3D Visualization & Navigation
  {
    id: 'bsp-doom-explorer',
    title: 'BSP DOOM Explorer',
    description: 'First-person DOOM-like navigation through BSP tree structure',
    technology: 'Three.js + WebGPU + BSP',
    url: 'http://localhost:5176/test/bsp-doom-explorer',
    features: ['First-Person', 'WASD Controls', 'Mouse Look', 'BSP Walls', 'Tonal Rooms', 'HUD'],
    status: 'complete',
  },
  {
    id: 'floor0-navigation',
    title: 'Floor 0 Navigation (E2E)',
    description: 'End-to-end demo: Backend room generation → 3D visualization → Navigation',
    technology: 'Three.js + Backend API',
    url: 'http://localhost:5176/test/floor0-navigation',
    features: ['Backend Integration', '3D Rooms', 'Corridors', 'Set Classes', 'Real-time Loading'],
    status: 'complete',
  },
  {
    id: 'harmonic-navigator-3d',
    title: 'Harmonic Navigator 3D',
    description: '3D harmonic space navigation with BSP tetrahedral cells and quaternion modulation',
    technology: 'Three.js + WebGL + BSP',
    url: 'http://localhost:5176/test/harmonic-navigator-3d',
    features: ['BSP Cells', 'Quaternion Rotation', 'Plücker Lines', 'Interactive 3D', 'Mode Visualization'],
    status: 'complete',
  },
  {
    id: 'immersive-musical-world',
    title: 'Immersive Musical World',
    description: 'Full 3D immersive world where musical hierarchy becomes a physical landscape',
    technology: 'Three.js + WebGL + First-Person',
    url: 'http://localhost:5176/test/immersive-musical-world',
    features: ['First-Person', 'WASD Controls', 'Floating Platforms', 'Skybox', 'Particles', 'Shadows'],
    status: 'complete',
  },
  {
    id: 'sunburst-3d',
    title: 'Sunburst 3D',
    description: '3D sunburst visualization with slope effect and LOD for hierarchical data',
    technology: 'Three.js + WebGL',
    url: 'http://localhost:5176/test/sunburst-3d',
    features: ['3D Sunburst', 'Slope Effect', 'LOD System', 'Interactive Zoom', 'Breadcrumb Trail'],
    status: 'complete',
  },

  // Nature Simulations
  {
    id: 'fluffy-grass',
    title: 'Fluffy Grass',
    description: 'Realistic grass rendering with billboard technique and wind animation',
    technology: 'Three.js + Custom Shaders',
    url: 'http://localhost:5176/test/fluffy-grass',
    features: ['Billboard Grass', 'Instancing', 'Chunking', 'Wind Animation', 'Custom Shaders', 'Fake AO'],
    status: 'complete',
  },
  {
    id: 'sand-dunes',
    title: 'Sand Dunes Terrain',
    description: 'Procedural desert landscape with ridged multifractal noise and micro ripples',
    technology: 'Three.js + GLSL Shaders',
    url: 'http://localhost:5176/test/sand-dunes',
    features: ['Ridged Multifractal', 'Micro Ripples', 'Slope Shading', 'Atmospheric Fog', 'Wind Direction'],
    status: 'complete',
  },
  {
    id: 'ocean',
    title: 'Ocean Shader',
    description: 'Realistic ocean water simulation with Gerstner waves and reflections',
    technology: 'Three.js + GLSL Shaders',
    url: 'http://localhost:5176/test/ocean',
    features: ['Gerstner Waves', 'Fresnel Effect', 'Specular Highlights', 'Sky Gradient', 'Real-time Animation'],
    status: 'complete',
  },

  // 3D Models & Animation
  {
    id: 'guitar-3d',
    title: 'Guitar 3D Viewer',
    description: 'Interactive 3D guitar model viewer with GLTF/GLB support and PBR materials',
    technology: 'Three.js + GLTF Loader',
    url: 'http://localhost:5176/test/guitar-3d',
    features: ['GLTF/GLB Loading', 'PBR Materials', 'IBL Environment', 'Auto-Centering', 'Shadow Casting'],
    status: 'complete',
  },
  {
    id: 'hand-animation',
    title: 'Hand Animation',
    description: 'Interactive hand animation with rigged 3D model and skeletal animation',
    technology: 'Three.js + GLTF + Skeletal Animation',
    url: 'http://localhost:5176/test/hand-animation',
    features: ['Rigged Hand Model', 'Skeletal Animation', 'Finger Control', 'Preset Poses', 'Auto-Animate'],
    status: 'complete',
  },
  {
    id: 'models-3d',
    title: '3D Models Gallery',
    description: 'Interactive gallery showcasing all Blender-created 3D models',
    technology: 'Three.js + GLTF Loader + WebGL',
    url: 'http://localhost:5176/test/models-3d',
    features: ['Model Switching', 'Auto-Rotation', 'Wireframe Mode', 'Real-time Stats', 'Orbit Controls'],
    status: 'complete',
  },

  // Music Theory & Analysis
  {
    id: 'bsp',
    title: 'BSP Musical Analysis',
    description: 'Binary Space Partitioning for advanced musical analysis and chord relationships',
    technology: 'React + Material-UI + BSP API',
    url: 'http://localhost:5176/test/bsp',
    features: ['Spatial Queries', 'Tonal Context', 'Progression Analysis', 'Sub-ms Performance', 'API Integration'],
    status: 'complete',
  },
  {
    id: 'instrument-icons',
    title: 'Instrument Icons Gallery',
    description: 'SVG icons for all instruments from the YAML database',
    technology: 'React + SVG + API',
    url: 'http://localhost:5176/test/instrument-icons',
    features: ['60+ Instruments', 'SVG Icons', 'Scalable', 'Themeable', 'API Integration', 'Live Preview'],
    status: 'complete',
  },

  // Biomechanics & IK
  {
    id: 'inverse-kinematics',
    title: 'Inverse Kinematics Demo',
    description: 'Interactive IK solver using genetic algorithms to find optimal hand poses for chord fingerings',
    technology: 'React + Three.js + REST API + GA',
    url: 'http://localhost:5176/test/inverse-kinematics',
    features: ['Genetic Algorithm', 'Forward/Inverse Kinematics', 'Hand Visualization', 'Fitness Metrics', 'Real-time Solving'],
    status: 'complete',
  },

  // DSL & Parsers
  {
    id: 'tab-converter',
    title: 'Guitar Tab Format Converter',
    description: 'Convert between different guitar tablature formats with live preview',
    technology: 'React + Material-UI + VexFlow + REST API',
    url: 'http://localhost:5176/test/tab-converter',
    features: ['ASCII ↔ VexTab', 'Live Preview', 'VexFlow Rendering', 'File Upload/Download', 'Example Library'],
    status: 'complete',
  },
  {
    id: 'grothendieck-dsl',
    title: 'Grothendieck Operations DSL',
    description: 'Interactive demo of category theory operations on musical objects',
    technology: 'React + Material-UI + F# Parser + REST API',
    url: 'http://localhost:5176/test/grothendieck-dsl',
    features: ['Live Parsing', 'AST Visualization', '15+ Examples', '7 Operation Categories', 'Category Theory'],
    status: 'complete',
  },
  {
    id: 'chord-progression-dsl',
    title: 'Chord Progression DSL',
    description: 'Parse and analyze chord progressions using absolute chords or Roman numerals',
    technology: 'React + Material-UI + F# Parser + REST API',
    url: 'http://localhost:5176/test/chord-progression-dsl',
    features: ['Absolute Chords', 'Roman Numerals', 'Metadata (Key/Time/Tempo)', 'Jazz Standards', 'Live Parsing'],
    status: 'complete',
  },
  {
    id: 'fretboard-navigation-dsl',
    title: 'Fretboard Navigation DSL',
    description: 'Navigate guitar fretboard using positions, CAGED shapes, movements, and slides',
    technology: 'React + Material-UI + F# Parser + REST API',
    url: 'http://localhost:5176/test/fretboard-navigation-dsl',
    features: ['Position Notation', 'CAGED Shapes', 'Movement Commands', 'Slide Commands', 'Live Parsing'],
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

export const AllDemosTable: React.FC = () => {
  return (
    <Container maxWidth="xl" sx={{ py: 4 }}>
      <Box sx={{ mb: 3 }}>
        <Typography variant="h3" gutterBottom sx={{ fontWeight: 700 }}>
          🎸 All Demo Applications
        </Typography>
        <Typography variant="body1" color="text.secondary" paragraph>
          Comprehensive collection of fretboard visualizations, 3D environments, nature simulations,
          and music theory tools. Click any row to open the demo in a new tab.
        </Typography>
        <Typography variant="body2" color="info.main" paragraph>
          💡 Demos open in a separate window (port 5176) with their own breadcrumb navigation.
          Use the back arrow button in the demo breadcrumbs to return to this table.
        </Typography>
        <Typography variant="body2" color="warning.main">
          Note: Demos require the React Components dev server running on port 5176
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
            {demoPages.map((page) => (
              <TableRow
                key={page.id}
                component={Link}
                href={page.url}
                target="_blank"
                rel="noopener noreferrer"
                sx={{
                  cursor: 'pointer',
                  textDecoration: 'none',
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

export default AllDemosTable;
