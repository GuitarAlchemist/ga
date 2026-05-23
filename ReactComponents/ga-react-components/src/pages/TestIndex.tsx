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
  Button,
  Stack,
  Tabs,
  Tab,
} from '@mui/material';
import ChatIcon from '@mui/icons-material/Chat';
import BuildIcon from '@mui/icons-material/Build';
import ScienceIcon from '@mui/icons-material/Science';
import { useNavigate } from 'react-router-dom';
import DevelopmentSection from './DevelopmentSection';

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
    id: 'modal-meadow',
    title: 'Modal Meadow',
    description: 'Walk through a meadow split into seven musical mode-regions (Ionian → Locrian); ambient chord progression, grass color, and wind shift as you cross between modes. v0.8 adds rolling hills, ponds, and a descent effect.',
    technology: 'Three.js + Web Audio + FPS Controls',
    path: '/test/modal-meadow',
    features: ['First-Person', 'WASD Walker', 'Pointer Lock', 'Seven Modes', 'Auto-Walk Default', 'Hills + Ponds', 'Web Audio Pad', 'Crossfade'],
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
    id: 'sand-dunes',
    title: 'Sand Dunes Terrain',
    description: 'Procedural desert landscape with ridged multifractal noise, micro ripples, and atmospheric fog',
    technology: 'Three.js + GLSL Shaders',
    path: '/test/sand-dunes',
    features: ['Ridged Multifractal', 'Micro Ripples', 'Slope Shading', 'Atmospheric Fog', 'Wind Direction', 'Procedural Generation'],
    status: 'complete',
  },
  {
    id: 'cheese-avalanche',
    title: 'Cheese Avalanche',
    description: 'Apéricubes and Babybels tumble down a procedural mountain — heightfield physics with rolling friction',
    technology: 'Three.js + custom physics',
    path: '/test/cheese-avalanche',
    features: ['Heightfield Collision', 'Rolling Friction', 'InstancedMesh', 'Day/Night Cycle', 'Bloom', 'Chromecast'],
    status: 'complete',
  },
  {
    id: 'stonehenge',
    title: 'Stonehenge (Restored)',
    description: 'The monument as it stood c. 2500 BCE — outer sarsen circle with continuous lintel ring, trilithon horseshoe, bluestones, heel stone',
    technology: 'Three.js + GLSL Shaders',
    path: '/test/stonehenge',
    features: ['Sarsen Circle', 'Trilithon Horseshoe', 'Bluestones', 'Lichen Weathering', 'Solstice Alignment', 'Day/Night', 'Ravens'],
    status: 'complete',
  },
  {
    id: 'fluffy-animals',
    title: 'Fluffy Animals',
    description: 'Bezier-blade fur on five animal bodies — bear, sheep, fox, bunny, cat — using the FluffyGrass shader idiom on ellipsoid surfaces',
    technology: 'Three.js + GLSL Shaders',
    path: '/test/fluffy-animals',
    features: ['Instanced Fur', 'Surface Sampling', 'Per-instance Color', 'Wind Sway', 'Bloom', 'Chromecast'],
    status: 'complete',
  },
  {
    id: 'mandelbulb',
    title: 'Mandelbulb',
    description: 'True raymarched Mandelbulb fractal with distance estimation, orbit-trap coloring, soft shadows, and interactive power controls',
    technology: 'Three.js + GLSL raymarching',
    path: '/test/mandelbulb',
    features: ['Distance Estimator', 'Raymarching', 'Orbit-Trap Coloring', 'Soft Shadows', 'Ambient Occlusion', 'Power Control'],
    status: 'complete',
  },
  {
    id: 'gaussian-splat',
    title: 'Gaussian Splat Viewer',
    description: 'Native Three.js renderer for 3D Gaussian Splat captures, streaming compressed PLY directly from SuperSplat CDN. Includes Bumblebee macro preset (PLY) and SOG-format scenes (Vegetables HQ, Queen\'s Hamlet) that show a friendly format-not-supported message until upstream PR #478 lands.',
    technology: 'Three.js + @mkkellogg/gaussian-splats-3d',
    path: '/test/gaussian-splat',
    features: ['3D Gaussian Splatting', 'Compressed PLY Streaming', 'Multi-Version CDN Resolver', 'CPU Worker Sort', 'SOG Format Detection', 'Custom Scene URL'],
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
  {
    id: 'ecosystem-roadmap',
    title: 'Ecosystem Roadmap Explorer',
    description: 'Three-mode roadmap: icicle, Poincaré disk, Poincaré ball (WebGPU)',
    technology: 'Three.js WebGPU, D3, MUI TreeView, Jotai',
    path: '/test/ecosystem-roadmap',
    features: ['WebGPU rendering', 'Hyperbolic geometry', 'Master-detail layout', 'Bidirectional sync'],
    status: 'complete',
  },
  {
    id: 'ga-chat',
    title: 'GA Chat (AG-UI)',
    description: 'AG-UI protocol chat panel with streaming text and diatonic chord table driven by domain events',
    technology: 'React + AG-UI + SSE + GaApi',
    path: '/test/ga-chat',
    features: ['AG-UI Protocol', 'SSE Streaming', 'Diatonic Chords', 'Routing Metadata', 'Hook Pipeline'],
    status: 'complete',
  },
  {
    id: 'prime-radiant',
    title: 'Prime Radiant',
    description: '3D governance visualization engine — Demerzel ecosystem as explorable force-directed graph',
    technology: 'Three.js + WebGL + Bloom',
    path: '/test/prime-radiant',
    features: ['Force-Directed Graph', '3D Node Types', 'Particle Streams', 'Health Overlay', 'Search', 'Detail Panel'],
    status: 'complete',
  },
  {
    id: 'ix-hand-voicing',
    title: 'IX Hand Voicing Lab',
    description: 'Webcam hand-pose landmarks become fretboard contacts, then IX-style ranking estimates playable chord voicings',
    technology: 'MediaPipe + Rust WASM + IX scoring',
    path: '/test/ix-hand-voicing',
    features: ['Hand Pose', 'Fretboard Calibration', 'Rust WASM', 'Voicing Ranking', 'Intent Detection', 'Wire Hand'],
    status: 'partial',
  },
  {
    id: 'fleet',
    title: 'Fleet Status',
    description: 'Unified "what is happening across the 5 sibling repos right now" view — PRs, install-audit scores, active initiatives, blockers. Static page, baked by CI cron.',
    technology: 'React + MUI + GitHub Actions cron + agent-blackbox install-audit',
    path: '/test/fleet',
    features: ['Active PRs', 'Install-audit scores', 'Active initiatives', 'Surface blockers', 'Markdown mirror', 'CI-baked'],
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
  // Development is the default landing surface — it's the live "behind the
  // scenes" view (epic progress, QA, AI contributors, commit activity) and
  // is publicly readable. Demos remains one click away.
  const [tab, setTab] = React.useState<'dev' | 'demos'>('dev');

  return (
    <Container maxWidth="xl" sx={{ py: 4 }}>
      {/* HERO: GA Chatbot is the headline product on this site. The component
          test suite below is supporting / dev material. */}
      <Paper
        elevation={4}
        sx={{
          mb: 4,
          p: { xs: 3, md: 4 },
          background: 'linear-gradient(135deg, #1d4ed8 0%, #1f6feb 50%, #06b6d4 100%)',
          color: 'white',
          borderRadius: 3,
        }}
      >
        <Stack
          direction={{ xs: 'column', md: 'row' }}
          spacing={3}
          alignItems={{ xs: 'flex-start', md: 'center' }}
          justifyContent="space-between"
        >
          <Box sx={{ flex: 1 }}>
            <Stack direction="row" spacing={1.5} alignItems="center" sx={{ mb: 1 }}>
              <ChatIcon sx={{ fontSize: 36 }} />
              <Typography variant="h3" sx={{ fontWeight: 700, m: 0 }}>
                GA Chatbot
              </Typography>
            </Stack>
            <Typography variant="h6" sx={{ fontWeight: 400, opacity: 0.95, maxWidth: 720 }}>
              Ask grounded music theory questions — chord voicings, voice leading, scale modes,
              set-class algebra. Powered by Guitar Alchemist's symbolic engine and a local LLM.
            </Typography>
          </Box>
          <Stack direction={{ xs: 'row', md: 'column' }} spacing={1.5} sx={{ minWidth: { md: 220 } }}>
            <Button
              variant="contained"
              size="large"
              href="/chatbot/"
              sx={{
                bgcolor: 'white',
                color: 'primary.main',
                fontWeight: 700,
                fontSize: '1.05rem',
                px: 3,
                py: 1.5,
                '&:hover': { bgcolor: '#f1f5f9' },
              }}
              startIcon={<ChatIcon />}
            >
              Launch Chatbot
            </Button>
            <Button
              variant="outlined"
              size="large"
              href="/chatbot/"
              target="_blank"
              rel="noopener noreferrer"
              sx={{
                borderColor: 'white',
                color: 'white',
                '&:hover': { borderColor: 'white', bgcolor: 'rgba(255,255,255,0.1)' },
              }}
            >
              Open in new tab
            </Button>
          </Stack>
        </Stack>
      </Paper>

      <Box sx={{ borderBottom: 1, borderColor: 'divider', mb: 3 }}>
        <Tabs value={tab} onChange={(_, v) => setTab(v as 'dev' | 'demos')} aria-label="Test index sections">
          <Tab
            value="dev"
            label="Development"
            icon={<BuildIcon fontSize="small" />}
            iconPosition="start"
            sx={{ minHeight: 48 }}
          />
          <Tab
            value="demos"
            label={`Demos (${testPages.length})`}
            icon={<ScienceIcon fontSize="small" />}
            iconPosition="start"
            sx={{ minHeight: 48 }}
          />
        </Tabs>
      </Box>

      {tab === 'dev' && <DevelopmentSection />}

      {tab === 'demos' && (
      <>
      <Box sx={{ mb: 2 }}>
        <Typography variant="h5" gutterBottom>
          🎸 Component Test Suite
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Supporting demos and component sandboxes. Click any row to open.
        </Typography>
      </Box>

      <TableContainer
        component={Paper}
        sx={{
          boxShadow: 3,
          maxHeight: 'calc(100vh - 200px)',
          overflow: 'auto',
        }}
      >
        <Table
          size="small"
          stickyHeader
          sx={{
            '& .MuiTableCell-root': {
              py: 1,
              whiteSpace: 'nowrap',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
            },
          }}
        >
          <TableHead>
            <TableRow>
              <TableCell sx={{ color: 'white', fontWeight: 'bold', width: '18%', backgroundColor: 'primary.main' }}>Component</TableCell>
              <TableCell sx={{ color: 'white', fontWeight: 'bold', width: '32%', maxWidth: 0, backgroundColor: 'primary.main' }}>Description</TableCell>
              <TableCell sx={{ color: 'white', fontWeight: 'bold', width: '18%', backgroundColor: 'primary.main' }}>Technology</TableCell>
              <TableCell sx={{ color: 'white', fontWeight: 'bold', width: '27%', backgroundColor: 'primary.main' }}>Features</TableCell>
              <TableCell sx={{ color: 'white', fontWeight: 'bold', width: '5%', textAlign: 'center', backgroundColor: 'primary.main' }}>Status</TableCell>
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
                <TableCell
                  sx={{ fontSize: '0.875rem', maxWidth: 0 }}
                  title={page.description}
                >
                  {page.description}
                </TableCell>
                <TableCell sx={{ fontSize: '0.875rem', color: 'primary.main' }}>{page.technology}</TableCell>
                <TableCell>
                  <Box sx={{ display: 'flex', flexWrap: 'nowrap', gap: 0.5, overflow: 'hidden' }}>
                    {page.features.slice(0, 3).map((feature) => (
                      <Chip
                        key={feature}
                        label={feature}
                        size="small"
                        variant="outlined"
                        sx={{ fontSize: '0.7rem', height: '20px', flexShrink: 0 }}
                      />
                    ))}
                    {page.features.length > 3 && (
                      <Chip
                        label={`+${page.features.length - 3}`}
                        size="small"
                        sx={{ fontSize: '0.7rem', height: '20px', flexShrink: 0 }}
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
      </>
      )}
    </Container>
  );
};

export default TestIndex;

