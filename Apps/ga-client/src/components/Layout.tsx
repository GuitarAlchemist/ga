import { ReactNode } from 'react';
import { Link, useLocation } from 'react-router-dom';
import {
  AppBar,
  Box,
  Breadcrumbs,
  Container,
  Drawer,
  IconButton,
  List,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Toolbar,
  Typography,
  useTheme,
} from '@mui/material';
import {
  Menu as MenuIcon,
  Home as HomeIcon,
  MusicNote as MusicNoteIcon,
  Piano as PianoIcon,
  Chat as ChatIcon,
  Science as ScienceIcon,
  ViewInAr as ViewInArIcon,
  Speed as SpeedIcon,
  Psychology as PsychologyIcon,
  Code as CodeIcon,
  Public as PublicIcon,
  NavigateNext as NavigateNextIcon,
  PanTool as PanToolIcon,
  GraphicEq as GraphicEqIcon,
  Storage as StorageIcon,
  Search as SearchIcon,
  Explore as ExploreIcon,
  Assessment as AssessmentIcon,
  LibraryMusic as LibraryMusicIcon,
  Tune as TuneIcon,
} from '@mui/icons-material';
import { useState } from 'react';

interface LayoutProps {
  children: ReactNode;
}

interface NavItem {
  path: string;
  label: string;
  icon: ReactNode;
  description?: string;
}

const navItems: NavItem[] = [
  { path: '/', label: 'Home', icon: <HomeIcon />, description: 'Guitar Alchemist Studio' },
  { path: '/harmonic-studio', label: 'Harmonic Studio', icon: <PianoIcon />, description: 'Context-first fretboard explorer' },
  { path: '/ai-copilot', label: 'AI Copilot', icon: <ChatIcon />, description: 'Chat with AI assistant' },
  { path: '/music-generation', label: 'Music Generation', icon: <MusicNoteIcon />, description: 'AI-powered music creation' },
  { path: '/demos', label: 'Demos', icon: <ScienceIcon />, description: 'All demo applications' },
  { path: '/demos/all', label: 'All Demos Table', icon: <AssessmentIcon />, description: 'Complete catalog: fretboards, simulations & more' },
];

const demoItems: NavItem[] = [
  { path: '/demos/hand-pose', label: 'Hand Pose Detection', icon: <PanToolIcon />, description: 'AI-powered hand pose analysis' },
  { path: '/demos/bsp', label: 'BSP Explorer', icon: <ViewInArIcon />, description: '3D DOOM-style level explorer' },
  { path: '/demos/chord-naming', label: 'Chord Naming', icon: <PianoIcon />, description: 'Advanced chord naming system' },
  { path: '/demos/fretboard-explorer', label: 'Fretboard Explorer', icon: <ExploreIcon />, description: 'Interactive fretboard visualization' },
  { path: '/demos/musical-analysis', label: 'Musical Analysis', icon: <AssessmentIcon />, description: 'Advanced music analysis tools' },
  { path: '/demos/advanced-math', label: 'Advanced Mathematics', icon: <ScienceIcon />, description: 'Mathematical music theory' },
  { path: '/demos/performance', label: 'Performance Optimization', icon: <SpeedIcon />, description: 'Performance benchmarks' },
  { path: '/demos/psychoacoustic', label: 'Psychoacoustic Voicing', icon: <PsychologyIcon />, description: 'Perceptual chord voicing' },
  { path: '/demos/practice-routine', label: 'Practice Routine DSL', icon: <CodeIcon />, description: 'Domain-specific language for practice' },
  { path: '/demos/internet-content', label: 'Internet Content', icon: <PublicIcon />, description: 'Web content integration' },
  { path: '/demos/sound-bank', label: 'Sound Bank', icon: <LibraryMusicIcon />, description: 'Guitar sound sample library' },
  { path: '/demos/embedding', label: 'Embedding Generator', icon: <StorageIcon />, description: 'Vector embeddings for music data' },
  { path: '/demos/vector-search', label: 'Vector Search', icon: <SearchIcon />, description: 'Semantic search benchmarks' },
  { path: '/demos/floor-manager', label: 'Floor Manager', icon: <TuneIcon />, description: 'BSP floor generation' },
  { path: '/demos/graphiti', label: 'Graphiti Knowledge Graph', icon: <GraphicEqIcon />, description: 'Temporal knowledge graph' },
];

const Layout = ({ children }: LayoutProps) => {
  const [drawerOpen, setDrawerOpen] = useState(false);
  const location = useLocation();
  const theme = useTheme();

  const toggleDrawer = () => {
    setDrawerOpen(!drawerOpen);
  };

  // Generate breadcrumbs from current path
  const generateBreadcrumbs = () => {
    const pathnames = location.pathname.split('/').filter((x) => x);

    const breadcrumbs = [
      <Link
        key="home"
        to="/"
        style={{
          color: theme.palette.text.secondary,
          textDecoration: 'none',
          display: 'flex',
          alignItems: 'center',
        }}
      >
        <HomeIcon sx={{ mr: 0.5, fontSize: 20 }} />
        Home
      </Link>,
    ];

    pathnames.forEach((value, index) => {
      const to = `/${pathnames.slice(0, index + 1).join('/')}`;
      const label = value
        .split('-')
        .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
        .join(' ');

      const isLast = index === pathnames.length - 1;

      breadcrumbs.push(
        isLast ? (
          <Typography key={to} color="text.primary" sx={{ display: 'flex', alignItems: 'center' }}>
            {label}
          </Typography>
        ) : (
          <Link
            key={to}
            to={to}
            style={{
              color: theme.palette.text.secondary,
              textDecoration: 'none',
            }}
          >
            {label}
          </Link>
        )
      );
    });

    return breadcrumbs;
  };

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100vh' }}>
      {/* App Bar */}
      <AppBar position="static" sx={{ bgcolor: theme.palette.background.paper }}>
        <Toolbar>
          <IconButton
            edge="start"
            color="inherit"
            aria-label="menu"
            onClick={toggleDrawer}
            sx={{ mr: 2 }}
          >
            <MenuIcon />
          </IconButton>
          <Typography variant="h6" component="div" sx={{ flexGrow: 1, fontWeight: 700 }}>
            Guitar Alchemist
          </Typography>
        </Toolbar>
      </AppBar>

      {/* Breadcrumbs */}
      {location.pathname !== '/' && (
        <Box sx={{ bgcolor: theme.palette.background.default, px: 3, py: 1.5, borderBottom: `1px solid ${theme.palette.divider}` }}>
          <Breadcrumbs separator={<NavigateNextIcon fontSize="small" />} aria-label="breadcrumb">
            {generateBreadcrumbs()}
          </Breadcrumbs>
        </Box>
      )}

      {/* Navigation Drawer */}
      <Drawer anchor="left" open={drawerOpen} onClose={toggleDrawer}>
        <Box sx={{ width: 280, pt: 2 }}>
          <Typography variant="h6" sx={{ px: 2, pb: 1, fontWeight: 700 }}>
            Navigation
          </Typography>
          <List>
            {navItems.map((item) => (
              <ListItem key={item.path} disablePadding>
                <ListItemButton
                  component={Link}
                  to={item.path}
                  onClick={toggleDrawer}
                  selected={location.pathname === item.path}
                >
                  <ListItemIcon>{item.icon}</ListItemIcon>
                  <ListItemText
                    primary={item.label}
                    secondary={item.description}
                    secondaryTypographyProps={{ variant: 'caption' }}
                  />
                </ListItemButton>
              </ListItem>
            ))}
          </List>

          <Typography variant="h6" sx={{ px: 2, pt: 2, pb: 1, fontWeight: 700 }}>
            Demo Applications
          </Typography>
          <List>
            {demoItems.map((item) => (
              <ListItem key={item.path} disablePadding>
                <ListItemButton
                  component={Link}
                  to={item.path}
                  onClick={toggleDrawer}
                  selected={location.pathname === item.path}
                >
                  <ListItemIcon>{item.icon}</ListItemIcon>
                  <ListItemText
                    primary={item.label}
                    secondary={item.description}
                    secondaryTypographyProps={{ variant: 'caption' }}
                  />
                </ListItemButton>
              </ListItem>
            ))}
          </List>
        </Box>
      </Drawer>

      {/* Main Content */}
      <Box component="main" sx={{ flex: 1, bgcolor: theme.palette.background.default }}>
        {children}
      </Box>
    </Box>
  );
};

export default Layout;

