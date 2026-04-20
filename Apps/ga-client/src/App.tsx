import { lazy, Suspense } from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import {
  CircularProgress,
  Container,
  CssBaseline,
  ThemeProvider,
  Typography,
  createTheme,
} from '@mui/material';
import { Provider as JotaiProvider } from 'jotai';
import Layout from './components/Layout';
import Home from './pages/Home';
import DemosIndex from './pages/DemosIndex';
import AllDemosTable from './pages/AllDemosTable';
import HarmonicStudio from './pages/HarmonicStudio';
import MusicGenerationDemo from './components/dashboard/MusicGenerationDemo';
import HandPoseDemo from './pages/demos/HandPoseDemo';
import EcosystemRoadmapDemo from './pages/demos/EcosystemRoadmapDemo';

const PrimeRadiantDemo = lazy(() =>
  import('../../../ReactComponents/ga-react-components/src/components/PrimeRadiant').then(mod => ({ default: mod.ForceRadiant }))
);

const HarmonicNebulaDemo = lazy(() =>
  import('../../../ReactComponents/ga-react-components/src/pages/HarmonicNebulaDemo')
);

const ChatInterface = lazy(() => import('./components/Chat/ChatInterface'));

// ── Test pages from ga-react-components ──────────────────────────────────────
const TestIndex = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/TestIndex'));
const MinimalThreeTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/MinimalThreeTest'));
const MusicTheoryTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/MusicTheoryTest'));
const ThreeFretboardTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/ThreeFretboardTest'));
const ThreeHeadstockTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/ThreeHeadstockTest'));
const RealisticFretboardTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/RealisticFretboardTest'));
const GuitarFretboardTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/GuitarFretboardTest'));
const WebGPUFretboardTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/WebGPUFretboardTest'));
const CapoTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/CapoTest'));
const CapoModelTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/CapoModelTest').then(mod => ({ default: mod.CapoModelTest })));
const BSPTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/BSPTest'));
const MusicHierarchyDemo = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/MusicHierarchyDemo'));
const HarmonicNavigator3DTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/HarmonicNavigator3DTest'));
const InstrumentIconsTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/InstrumentIconsTest'));
const BSPDoomExplorerTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/BSPDoomExplorerTest'));
const FretboardWithHandTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/FretboardWithHandTest'));
const Sunburst3DTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/Sunburst3DTest'));
const ImmersiveMusicalWorldTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/ImmersiveMusicalWorldTest'));
const FluffyGrassTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/FluffyGrassTest'));
const OceanTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/OceanTest'));
const SandDunesTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/SandDunesTest'));
const Guitar3DTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/Guitar3DTest'));
const HandAnimationTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/HandAnimationTest'));
const Models3DTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/Models3DTest'));
const Floor0NavigationTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/Floor0NavigationTest'));
const TabConverterTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/TabConverterTest'));
const GrothendieckDSLTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/GrothendieckDSLTest'));
const ChordProgressionDSLTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/ChordProgressionDSLTest'));
const FretboardNavigationDSLTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/FretboardNavigationDSLTest'));
const InverseKinematicsTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/InverseKinematicsTest'));
const EcosystemRoadmapTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/EcosystemRoadmapTest'));
const PrimeRadiantTest = lazy(() => import('../../../ReactComponents/ga-react-components/src/pages/PrimeRadiantTest'));

const defaultTheme = createTheme({
  palette: {
    mode: 'dark',
    primary: {
      main: '#ff7a45',
    },
    secondary: {
      main: '#6bc1ff',
    },
    background: {
      default: '#0e1014',
      paper: '#161a20',
    },
  },
  typography: {
    fontFamily: 'Inter, Roboto, sans-serif',
  },
});

const LoadingFallback = () => (
  <div style={{ display: 'grid', placeItems: 'center', height: '100vh', padding: '64px 0' }}>
    <CircularProgress />
    <Typography variant="body1" color="text.secondary" sx={{ mt: 2 }}>
      Loading...
    </Typography>
  </div>
);

const App = () => {
  return (
    <JotaiProvider>
      <ThemeProvider theme={defaultTheme}>
        <CssBaseline />
        <BrowserRouter>
          <Layout>
            <Suspense fallback={<LoadingFallback />}>
              <Routes>
                <Route path="/" element={<Home />} />
                <Route path="/harmonic-studio" element={<HarmonicStudio />} />
                <Route path="/ai-copilot" element={<ChatInterface />} />
                <Route path="/music-generation" element={
                  <Container maxWidth="lg" sx={{ py: 4 }}>
                    <MusicGenerationDemo />
                  </Container>
                } />
                <Route path="/demos" element={<DemosIndex />} />
                <Route path="/demos/all" element={<AllDemosTable />} />
                <Route path="/demos/hand-pose" element={<HandPoseDemo />} />
                <Route path="/demos/ecosystem-roadmap" element={<EcosystemRoadmapDemo />} />
                <Route path="/demos/bsp" element={
                  <Container maxWidth="lg" sx={{ py: 4 }}>
                    <Typography variant="h4">BSP Explorer</Typography>
                    <Typography color="text.secondary">Coming soon...</Typography>
                  </Container>
                } />
                <Route path="/demos/chord-naming" element={
                  <Container maxWidth="lg" sx={{ py: 4 }}>
                    <Typography variant="h4">Chord Naming Demo</Typography>
                    <Typography color="text.secondary">Coming soon...</Typography>
                  </Container>
                } />
                <Route path="/demos/fretboard-explorer" element={
                  <Container maxWidth="lg" sx={{ py: 4 }}>
                    <Typography variant="h4">Fretboard Explorer</Typography>
                    <Typography color="text.secondary">Coming soon...</Typography>
                  </Container>
                } />
                <Route path="/demos/musical-analysis" element={
                  <Container maxWidth="lg" sx={{ py: 4 }}>
                    <Typography variant="h4">Musical Analysis</Typography>
                    <Typography color="text.secondary">Coming soon...</Typography>
                  </Container>
                } />
                <Route path="/demos/advanced-math" element={
                  <Container maxWidth="lg" sx={{ py: 4 }}>
                    <Typography variant="h4">Advanced Mathematics</Typography>
                    <Typography color="text.secondary">Coming soon...</Typography>
                  </Container>
                } />
                <Route path="/demos/performance" element={
                  <Container maxWidth="lg" sx={{ py: 4 }}>
                    <Typography variant="h4">Performance Optimization</Typography>
                    <Typography color="text.secondary">Coming soon...</Typography>
                  </Container>
                } />
                <Route path="/demos/psychoacoustic" element={
                  <Container maxWidth="lg" sx={{ py: 4 }}>
                    <Typography variant="h4">Psychoacoustic Voicing</Typography>
                    <Typography color="text.secondary">Coming soon...</Typography>
                  </Container>
                } />
                <Route path="/demos/practice-routine" element={
                  <Container maxWidth="lg" sx={{ py: 4 }}>
                    <Typography variant="h4">Practice Routine DSL</Typography>
                    <Typography color="text.secondary">Coming soon...</Typography>
                  </Container>
                } />
                <Route path="/demos/internet-content" element={
                  <Container maxWidth="lg" sx={{ py: 4 }}>
                    <Typography variant="h4">Internet Content</Typography>
                    <Typography color="text.secondary">Coming soon...</Typography>
                  </Container>
                } />
                <Route path="/demos/sound-bank" element={
                  <Container maxWidth="lg" sx={{ py: 4 }}>
                    <Typography variant="h4">Sound Bank</Typography>
                    <Typography color="text.secondary">Coming soon...</Typography>
                  </Container>
                } />
                <Route path="/demos/embedding" element={
                  <Container maxWidth="lg" sx={{ py: 4 }}>
                    <Typography variant="h4">Embedding Generator</Typography>
                    <Typography color="text.secondary">Coming soon...</Typography>
                  </Container>
                } />
                <Route path="/demos/vector-search" element={
                  <Container maxWidth="lg" sx={{ py: 4 }}>
                    <Typography variant="h4">Vector Search</Typography>
                    <Typography color="text.secondary">Coming soon...</Typography>
                  </Container>
                } />
                <Route path="/demos/floor-manager" element={
                  <Container maxWidth="lg" sx={{ py: 4 }}>
                    <Typography variant="h4">Floor Manager</Typography>
                    <Typography color="text.secondary">Coming soon...</Typography>
                  </Container>
                } />
                <Route path="/demos/graphiti" element={
                  <Container maxWidth="lg" sx={{ py: 4 }}>
                    <Typography variant="h4">Graphiti Knowledge Graph</Typography>
                    <Typography color="text.secondary">Coming soon...</Typography>
                  </Container>
                } />
                <Route path="/demos/prime-radiant" element={
                  <Container maxWidth={false} disableGutters sx={{ height: 'calc(100vh - 64px)', overflow: 'hidden' }}>
                    <PrimeRadiantDemo />
                  </Container>
                } />
                <Route path="/demos/harmonic-nebula" element={
                  <Container maxWidth={false} disableGutters sx={{ height: 'calc(100vh - 64px)', overflow: 'hidden' }}>
                    <HarmonicNebulaDemo />
                  </Container>
                } />

                {/* ── Test pages (from ga-react-components) ──────────────── */}
                <Route path="/test" element={<TestIndex />} />
                <Route path="/test/minimal-three" element={<MinimalThreeTest />} />
                <Route path="/test/music-theory" element={<MusicTheoryTest />} />
                <Route path="/test/three-fretboard" element={<ThreeFretboardTest />} />
                <Route path="/test/three-headstock" element={<ThreeHeadstockTest />} />
                <Route path="/test/realistic-fretboard" element={<RealisticFretboardTest />} />
                <Route path="/test/guitar-fretboard" element={<GuitarFretboardTest />} />
                <Route path="/test/webgpu-fretboard" element={<WebGPUFretboardTest />} />
                <Route path="/test/capo" element={<CapoTest />} />
                <Route path="/test/capo-model" element={<CapoModelTest />} />
                <Route path="/test/bsp" element={<BSPTest />} />
                <Route path="/test/music-hierarchy" element={<MusicHierarchyDemo />} />
                <Route path="/test/harmonic-navigator-3d" element={<HarmonicNavigator3DTest />} />
                <Route path="/test/instrument-icons" element={<InstrumentIconsTest />} />
                <Route path="/test/bsp-doom-explorer" element={<BSPDoomExplorerTest />} />
                <Route path="/test/fretboard-with-hand" element={<FretboardWithHandTest />} />
                <Route path="/test/sunburst-3d" element={<Sunburst3DTest />} />
                <Route path="/test/immersive-musical-world" element={<ImmersiveMusicalWorldTest />} />
                <Route path="/test/fluffy-grass" element={<FluffyGrassTest />} />
                <Route path="/test/ocean" element={<OceanTest />} />
                <Route path="/test/sand-dunes" element={<SandDunesTest />} />
                <Route path="/test/guitar-3d" element={<Guitar3DTest />} />
                <Route path="/test/hand-animation" element={<HandAnimationTest />} />
                <Route path="/test/models-3d" element={<Models3DTest />} />
                <Route path="/test/floor0-navigation" element={<Floor0NavigationTest />} />
                <Route path="/test/tab-converter" element={<TabConverterTest />} />
                <Route path="/test/grothendieck-dsl" element={<GrothendieckDSLTest />} />
                <Route path="/test/chord-progression-dsl" element={<ChordProgressionDSLTest />} />
                <Route path="/test/fretboard-navigation-dsl" element={<FretboardNavigationDSLTest />} />
                <Route path="/test/inverse-kinematics" element={<InverseKinematicsTest />} />
                <Route path="/test/ecosystem-roadmap" element={<EcosystemRoadmapTest />} />
                <Route path="/test/prime-radiant" element={<PrimeRadiantTest />} />
                <Route path="/test/harmonic-nebula" element={
                  <Container maxWidth={false} disableGutters sx={{ height: 'calc(100vh - 64px)', overflow: 'hidden' }}>
                    <HarmonicNebulaDemo />
                  </Container>
                } />
              </Routes>
            </Suspense>
          </Layout>
        </BrowserRouter>
      </ThemeProvider>
    </JotaiProvider>
  );
};

export default App;
