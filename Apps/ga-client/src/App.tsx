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

const ChatInterface = lazy(() => import('./components/Chat/ChatInterface'));

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
              </Routes>
            </Suspense>
          </Layout>
        </BrowserRouter>
      </ThemeProvider>
    </JotaiProvider>
  );
};

export default App;
