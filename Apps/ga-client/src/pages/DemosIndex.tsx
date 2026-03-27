import { useState, useRef } from 'react';
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
  TextField,
  InputAdornment,
  Collapse,
  IconButton,
} from '@mui/material';
import {
  TableChart as TableChartIcon,
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
  AccountTree as AccountTreeIcon,
  Hub as HubIcon,
  FilterList as FilterListIcon,
  ExpandMore as ExpandMoreIcon,
  ExpandLess as ExpandLessIcon,
  ThreeDRotation as ThreeDRotationIcon,
  MusicNote as MusicNoteIcon,
  SmartToy as SmartToyIcon,
  Build as BuildIcon,
  Security as SecurityIcon,
} from '@mui/icons-material';

interface Demo {
  title: string;
  description: string;
  icon: React.ReactNode;
  link: string;
  tags: string[];
  status: string;
  color: string;
  category: string;
}

interface Category {
  id: string;
  label: string;
  icon: React.ReactNode;
  themeColor: string;
}

const categories: Category[] = [
  { id: '3d-viz', label: '3D & Visualization', icon: <ThreeDRotationIcon />, themeColor: '#42a5f5' },
  { id: 'music', label: 'Music Theory & Analysis', icon: <MusicNoteIcon />, themeColor: '#66bb6a' },
  { id: 'ai-ml', label: 'AI & Machine Learning', icon: <SmartToyIcon />, themeColor: '#ffa726' },
  { id: 'tools', label: 'Tools & DSLs', icon: <BuildIcon />, themeColor: '#ab47bc' },
  { id: 'governance', label: 'Governance', icon: <SecurityIcon />, themeColor: '#5c6bc0' },
];

const demos: Demo[] = [
  {
    title: 'BSP Explorer',
    description: 'Explore music theory concepts in a 3D DOOM-style environment. Navigate through procedurally generated levels where each room represents a musical concept.',
    icon: <ViewInArIcon sx={{ fontSize: 48 }} />,
    link: '/demos/bsp',
    tags: ['3D', 'Interactive', 'BSP'],
    status: 'Available',
    color: '#6bc1ff',
    category: '3d-viz',
  },
  {
    title: 'Floor Manager',
    description: 'BSP floor generation and management for creating procedural 3D environments based on music theory concepts.',
    icon: <TuneIcon sx={{ fontSize: 48 }} />,
    link: '/demos/floor-manager',
    tags: ['3D', 'BSP', 'Procedural'],
    status: 'Available',
    color: '#ff85c0',
    category: '3d-viz',
  },
  {
    title: 'Ecosystem Roadmap',
    description: 'Interactive roadmap with icicle, Poincaré disk, and 3D Poincaré ball views. WebGPU-accelerated.',
    icon: <AccountTreeIcon sx={{ fontSize: 48 }} />,
    link: '/demos/ecosystem-roadmap',
    tags: ['Three.js', 'WebGPU', 'Hyperbolic', 'D3'],
    status: 'Available',
    color: '#f0883e',
    category: '3d-viz',
  },
  {
    title: 'Prime Radiant',
    description: 'Demerzel governance visualization — explore the constitutional hierarchy, policies, and agent personas in an interactive 3D force-directed graph.',
    icon: <HubIcon sx={{ fontSize: 48 }} />,
    link: '/demos/prime-radiant',
    tags: ['3D', 'Governance', 'Visualization'],
    status: 'Available',
    color: '#c4b5fd',
    category: '3d-viz',
  },
  {
    title: 'Chord Naming',
    description: 'Advanced chord naming system that analyzes and identifies complex chord voicings with context-aware naming conventions.',
    icon: <PianoIcon sx={{ fontSize: 48 }} />,
    link: '/demos/chord-naming',
    tags: ['Theory', 'Analysis'],
    status: 'Available',
    color: '#52c41a',
    category: 'music',
  },
  {
    title: 'Fretboard Explorer',
    description: 'Interactive fretboard visualization with real-time chord and scale highlighting. Explore different tunings and positions.',
    icon: <ExploreIcon sx={{ fontSize: 48 }} />,
    link: '/demos/fretboard-explorer',
    tags: ['Interactive', 'Visualization'],
    status: 'Available',
    color: '#faad14',
    category: 'music',
  },
  {
    title: 'Musical Analysis',
    description: 'Advanced music analysis tools for harmonic progression, voice leading, and structural analysis of compositions.',
    icon: <AssessmentIcon sx={{ fontSize: 48 }} />,
    link: '/demos/musical-analysis',
    tags: ['Analysis', 'Theory'],
    status: 'Available',
    color: '#eb2f96',
    category: 'music',
  },
  {
    title: 'Advanced Mathematics',
    description: 'Explore the mathematical foundations of music theory including group theory, category theory, and algebraic structures.',
    icon: <ScienceIcon sx={{ fontSize: 48 }} />,
    link: '/demos/advanced-math',
    tags: ['Math', 'Theory', 'Research'],
    status: 'Available',
    color: '#722ed1',
    category: 'music',
  },
  {
    title: 'Hand Pose Detection',
    description: 'AI-powered hand pose detection for guitar playing analysis. Detect hand positions and finger landmarks using computer vision.',
    icon: <PanToolIcon sx={{ fontSize: 48 }} />,
    link: '/demos/hand-pose',
    tags: ['AI', 'Computer Vision', 'Interactive'],
    status: 'Available',
    color: '#ff7a45',
    category: 'ai-ml',
  },
  {
    title: 'Psychoacoustic Voicing',
    description: 'Intelligent chord voicing based on psychoacoustic principles, optimizing for perceptual clarity and harmonic balance.',
    icon: <PsychologyIcon sx={{ fontSize: 48 }} />,
    link: '/demos/psychoacoustic',
    tags: ['AI', 'Acoustics', 'Theory'],
    status: 'Available',
    color: '#f759ab',
    category: 'ai-ml',
  },
  {
    title: 'Embedding Generator',
    description: 'Generate vector embeddings for music data enabling semantic search and AI-powered music recommendations.',
    icon: <StorageIcon sx={{ fontSize: 48 }} />,
    link: '/demos/embedding',
    tags: ['AI', 'Embeddings', 'ML'],
    status: 'Available',
    color: '#597ef7',
    category: 'ai-ml',
  },
  {
    title: 'Vector Search',
    description: 'Semantic search benchmarks and performance testing for vector similarity search in music databases.',
    icon: <SearchIcon sx={{ fontSize: 48 }} />,
    link: '/demos/vector-search',
    tags: ['Search', 'Performance', 'AI'],
    status: 'Available',
    color: '#73d13d',
    category: 'ai-ml',
  },
  {
    title: 'Sound Bank',
    description: 'Guitar sound sample library with semantic search and AI-powered categorization of guitar tones and techniques.',
    icon: <LibraryMusicIcon sx={{ fontSize: 48 }} />,
    link: '/demos/sound-bank',
    tags: ['Audio', 'AI', 'Search'],
    status: 'Available',
    color: '#ffa940',
    category: 'ai-ml',
  },
  {
    title: 'Practice Routine DSL',
    description: 'Domain-specific language for creating and managing guitar practice routines with intelligent scheduling and progress tracking.',
    icon: <CodeIcon sx={{ fontSize: 48 }} />,
    link: '/demos/practice-routine',
    tags: ['DSL', 'Practice', 'Learning'],
    status: 'Available',
    color: '#9254de',
    category: 'tools',
  },
  {
    title: 'Internet Content',
    description: 'Integration with web content sources for tabs, lessons, and music theory resources from across the internet.',
    icon: <PublicIcon sx={{ fontSize: 48 }} />,
    link: '/demos/internet-content',
    tags: ['Web', 'Integration'],
    status: 'Available',
    color: '#36cfc9',
    category: 'tools',
  },
  {
    title: 'Graphiti Knowledge Graph',
    description: 'Temporal knowledge graph for tracking music theory concepts, relationships, and learning progress over time.',
    icon: <GraphicEqIcon sx={{ fontSize: 48 }} />,
    link: '/demos/graphiti',
    tags: ['Knowledge Graph', 'AI', 'Learning'],
    status: 'Available',
    color: '#ffc53d',
    category: 'tools',
  },
  {
    title: 'Performance Optimization',
    description: 'Benchmarks and performance analysis tools for testing the efficiency of music theory algorithms and data structures.',
    icon: <SpeedIcon sx={{ fontSize: 48 }} />,
    link: '/demos/performance',
    tags: ['Performance', 'Benchmarks'],
    status: 'Available',
    color: '#13c2c2',
    category: 'tools',
  },
  {
    title: 'Prime Radiant',
    description: 'Demerzel governance visualization — explore the constitutional hierarchy, policies, and agent personas in an interactive 3D force-directed graph.',
    icon: <HubIcon sx={{ fontSize: 48 }} />,
    link: '/demos/prime-radiant',
    tags: ['3D', 'Governance', 'Visualization'],
    status: 'Available',
    color: '#c4b5fd',
    category: 'governance',
  },
];

const DemosIndex = () => {
  const theme = useTheme();
  const [searchQuery, setSearchQuery] = useState('');
  const [collapsed, setCollapsed] = useState<Record<string, boolean>>({});
  const sectionRefs = useRef<Record<string, HTMLDivElement | null>>({});

  const filteredDemos = searchQuery.trim()
    ? demos.filter((d) => {
        const q = searchQuery.toLowerCase();
        return (
          d.title.toLowerCase().includes(q) ||
          d.description.toLowerCase().includes(q) ||
          d.tags.some((t) => t.toLowerCase().includes(q))
        );
      })
    : demos;

  const scrollToCategory = (id: string) => {
    sectionRefs.current[id]?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  const toggleCategory = (id: string) => {
    setCollapsed((prev) => ({ ...prev, [id]: !prev[id] }));
  };

  const getCategoryDemos = (categoryId: string) =>
    filteredDemos.filter((d) => d.category === categoryId);

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

      {/* Search */}
      <TextField
        fullWidth
        size="small"
        placeholder="Filter demos by name, description, or tag..."
        value={searchQuery}
        onChange={(e) => setSearchQuery(e.target.value)}
        sx={{ mb: 2 }}
        InputProps={{
          startAdornment: (
            <InputAdornment position="start">
              <FilterListIcon fontSize="small" />
            </InputAdornment>
          ),
        }}
      />

      {/* Jump-to chips */}
      <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', mb: 4 }}>
        {categories.map((cat) => {
          const count = getCategoryDemos(cat.id).length;
          if (count === 0) return null;
          return (
            <Chip
              key={cat.id}
              icon={cat.icon as React.ReactElement}
              label={`${cat.label} (${count})`}
              onClick={() => scrollToCategory(cat.id)}
              sx={{
                borderColor: cat.themeColor,
                color: cat.themeColor,
                '& .MuiChip-icon': { color: cat.themeColor },
              }}
              variant="outlined"
              clickable
            />
          );
        })}
      </Box>

      {/* Category sections */}
      {categories.map((cat) => {
        const catDemos = getCategoryDemos(cat.id);
        if (catDemos.length === 0) return null;
        const isCollapsed = collapsed[cat.id] ?? false;

        return (
          <Box
            key={cat.id}
            ref={(el: HTMLDivElement | null) => { sectionRefs.current[cat.id] = el; }}
            sx={{ mb: 5, scrollMarginTop: 16 }}
          >
            {/* Category header */}
            <Box
              sx={{
                display: 'flex',
                alignItems: 'center',
                gap: 1.5,
                mb: 2,
                pb: 1,
                borderBottom: `2px solid ${cat.themeColor}`,
                cursor: 'pointer',
                userSelect: 'none',
              }}
              onClick={() => toggleCategory(cat.id)}
            >
              <Box sx={{ color: cat.themeColor, display: 'flex', alignItems: 'center' }}>
                {cat.icon}
              </Box>
              <Typography variant="h5" sx={{ fontWeight: 600, flex: 1 }}>
                {cat.label}
              </Typography>
              <Chip
                label={catDemos.length}
                size="small"
                sx={{
                  bgcolor: cat.themeColor,
                  color: '#fff',
                  fontWeight: 700,
                  minWidth: 28,
                }}
              />
              <IconButton size="small">
                {isCollapsed ? <ExpandMoreIcon /> : <ExpandLessIcon />}
              </IconButton>
            </Box>

            {/* Cards */}
            <Collapse in={!isCollapsed}>
              <Grid container spacing={3}>
                {catDemos.map((demo) => (
                  <Grid item xs={12} md={6} lg={4} key={`${cat.id}-${demo.title}`}>
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
            </Collapse>
          </Box>
        );
      })}

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
