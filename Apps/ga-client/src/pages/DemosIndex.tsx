import { Link } from 'react-router-dom';
import {
  Box,
  Card,
  CardContent,
  Container,
  Grid,
  Typography,
  Chip,
  useTheme,
  Button,
  Alert,
} from '@mui/material';
import { TableChart as TableChartIcon } from '@mui/icons-material';
import {
  ViewInAr as ViewInArIcon,
  Piano as PianoIcon,
  Science as ScienceIcon,
  Speed as SpeedIcon,
  Psychology as PsychologyIcon,
  Code as CodeIcon,
  Public as PublicIcon,
  PanTool as PanToolIcon,
  GraphicEq as GraphicEqIcon,
  Storage as StorageIcon,
  Search as SearchIcon,
  Explore as ExploreIcon,
  Assessment as AssessmentIcon,
  LibraryMusic as LibraryMusicIcon,
  Tune as TuneIcon,
} from '@mui/icons-material';

const DemosIndex = () => {
  const theme = useTheme();

  const demos = [
    {
      title: 'Hand Pose Detection',
      description: 'AI-powered hand pose detection for guitar playing analysis. Detect hand positions and finger landmarks using computer vision.',
      icon: <PanToolIcon sx={{ fontSize: 48 }} />,
      link: '/demos/hand-pose',
      tags: ['AI', 'Computer Vision', 'Interactive'],
      status: 'Available',
      color: '#ff7a45',
    },
    {
      title: 'BSP Explorer',
      description: 'Explore music theory concepts in a 3D DOOM-style environment. Navigate through procedurally generated levels where each room represents a musical concept.',
      icon: <ViewInArIcon sx={{ fontSize: 48 }} />,
      link: '/demos/bsp',
      tags: ['3D', 'Interactive', 'BSP'],
      status: 'Available',
      color: '#6bc1ff',
    },
    {
      title: 'Chord Naming',
      description: 'Advanced chord naming system that analyzes and identifies complex chord voicings with context-aware naming conventions.',
      icon: <PianoIcon sx={{ fontSize: 48 }} />,
      link: '/demos/chord-naming',
      tags: ['Theory', 'Analysis'],
      status: 'Available',
      color: '#52c41a',
    },
    {
      title: 'Fretboard Explorer',
      description: 'Interactive fretboard visualization with real-time chord and scale highlighting. Explore different tunings and positions.',
      icon: <ExploreIcon sx={{ fontSize: 48 }} />,
      link: '/demos/fretboard-explorer',
      tags: ['Interactive', 'Visualization'],
      status: 'Available',
      color: '#faad14',
    },
    {
      title: 'Musical Analysis',
      description: 'Advanced music analysis tools for harmonic progression, voice leading, and structural analysis of compositions.',
      icon: <AssessmentIcon sx={{ fontSize: 48 }} />,
      link: '/demos/musical-analysis',
      tags: ['Analysis', 'Theory'],
      status: 'Available',
      color: '#eb2f96',
    },
    {
      title: 'Advanced Mathematics',
      description: 'Explore the mathematical foundations of music theory including group theory, category theory, and algebraic structures.',
      icon: <ScienceIcon sx={{ fontSize: 48 }} />,
      link: '/demos/advanced-math',
      tags: ['Math', 'Theory', 'Research'],
      status: 'Available',
      color: '#722ed1',
    },
    {
      title: 'Performance Optimization',
      description: 'Benchmarks and performance analysis tools for testing the efficiency of music theory algorithms and data structures.',
      icon: <SpeedIcon sx={{ fontSize: 48 }} />,
      link: '/demos/performance',
      tags: ['Performance', 'Benchmarks'],
      status: 'Available',
      color: '#13c2c2',
    },
    {
      title: 'Psychoacoustic Voicing',
      description: 'Intelligent chord voicing based on psychoacoustic principles, optimizing for perceptual clarity and harmonic balance.',
      icon: <PsychologyIcon sx={{ fontSize: 48 }} />,
      link: '/demos/psychoacoustic',
      tags: ['AI', 'Acoustics', 'Theory'],
      status: 'Available',
      color: '#f759ab',
    },
    {
      title: 'Practice Routine DSL',
      description: 'Domain-specific language for creating and managing guitar practice routines with intelligent scheduling and progress tracking.',
      icon: <CodeIcon sx={{ fontSize: 48 }} />,
      link: '/demos/practice-routine',
      tags: ['DSL', 'Practice', 'Learning'],
      status: 'Available',
      color: '#9254de',
    },
    {
      title: 'Internet Content',
      description: 'Integration with web content sources for tabs, lessons, and music theory resources from across the internet.',
      icon: <PublicIcon sx={{ fontSize: 48 }} />,
      link: '/demos/internet-content',
      tags: ['Web', 'Integration'],
      status: 'Available',
      color: '#36cfc9',
    },
    {
      title: 'Sound Bank',
      description: 'Guitar sound sample library with semantic search and AI-powered categorization of guitar tones and techniques.',
      icon: <LibraryMusicIcon sx={{ fontSize: 48 }} />,
      link: '/demos/sound-bank',
      tags: ['Audio', 'AI', 'Search'],
      status: 'Available',
      color: '#ffa940',
    },
    {
      title: 'Embedding Generator',
      description: 'Generate vector embeddings for music data enabling semantic search and AI-powered music recommendations.',
      icon: <StorageIcon sx={{ fontSize: 48 }} />,
      link: '/demos/embedding',
      tags: ['AI', 'Embeddings', 'ML'],
      status: 'Available',
      color: '#597ef7',
    },
    {
      title: 'Vector Search',
      description: 'Semantic search benchmarks and performance testing for vector similarity search in music databases.',
      icon: <SearchIcon sx={{ fontSize: 48 }} />,
      link: '/demos/vector-search',
      tags: ['Search', 'Performance', 'AI'],
      status: 'Available',
      color: '#73d13d',
    },
    {
      title: 'Floor Manager',
      description: 'BSP floor generation and management for creating procedural 3D environments based on music theory concepts.',
      icon: <TuneIcon sx={{ fontSize: 48 }} />,
      link: '/demos/floor-manager',
      tags: ['3D', 'BSP', 'Procedural'],
      status: 'Available',
      color: '#ff85c0',
    },
    {
      title: 'Graphiti Knowledge Graph',
      description: 'Temporal knowledge graph for tracking music theory concepts, relationships, and learning progress over time.',
      icon: <GraphicEqIcon sx={{ fontSize: 48 }} />,
      link: '/demos/graphiti',
      tags: ['Knowledge Graph', 'AI', 'Learning'],
      status: 'Available',
      color: '#ffc53d',
    },
  ];

  return (
    <Container maxWidth="lg" sx={{ py: 4 }}>
      <Box sx={{ mb: 4 }}>
        <Typography variant="h3" sx={{ fontWeight: 700, mb: 2 }}>
          Demo Applications
        </Typography>
        <Typography variant="body1" color="text.secondary" paragraph>
          Explore experimental features and research prototypes showcasing advanced music theory concepts,
          AI-powered analysis, and innovative visualization techniques.
        </Typography>

        <Alert severity="info" sx={{ mb: 2 }}>
          <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <Typography variant="body2">
              Looking for fretboard demos, grass/dunes simulations, and 3D visualizations?
            </Typography>
            <Button
              component={Link}
              to="/demos/all"
              variant="contained"
              startIcon={<TableChartIcon />}
              size="small"
            >
              View All Demos Table
            </Button>
          </Box>
        </Alert>
      </Box>

      <Grid container spacing={3}>
        {demos.map((demo) => (
          <Grid item xs={12} md={6} lg={4} key={demo.title}>
            <Card
              component={Link}
              to={demo.link}
              sx={{
                height: '100%',
                display: 'flex',
                flexDirection: 'column',
                textDecoration: 'none',
                transition: 'transform 0.2s, box-shadow 0.2s',
                '&:hover': {
                  transform: 'translateY(-4px)',
                  boxShadow: theme.shadows[8],
                },
              }}
            >
              <CardContent sx={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
                <Box sx={{ color: demo.color, mb: 2 }}>
                  {demo.icon}
                </Box>

                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
                  <Typography variant="h6" sx={{ fontWeight: 600, flex: 1 }}>
                    {demo.title}
                  </Typography>
                  <Chip
                    label={demo.status}
                    size="small"
                    color="success"
                    sx={{ height: 20 }}
                  />
                </Box>

                <Typography variant="body2" color="text.secondary" sx={{ mb: 2, flex: 1 }}>
                  {demo.description}
                </Typography>

                <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
                  {demo.tags.map((tag) => (
                    <Chip
                      key={tag}
                      label={tag}
                      size="small"
                      variant="outlined"
                      sx={{ height: 24 }}
                    />
                  ))}
                </Box>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>

      <Box sx={{ mt: 6, p: 3, bgcolor: theme.palette.background.paper, borderRadius: 2 }}>
        <Typography variant="h5" sx={{ fontWeight: 600, mb: 2 }}>
          About These Demos
        </Typography>
        <Typography variant="body2" color="text.secondary" paragraph>
          These demo applications showcase various aspects of the Guitar Alchemist platform. Each demo
          focuses on a specific area of music theory, technology, or user experience.
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Some demos are experimental and may contain features that are still under development.
          They serve as research prototypes and proof-of-concept implementations for new ideas.
        </Typography>
      </Box>
    </Container>
  );
};

export default DemosIndex;

