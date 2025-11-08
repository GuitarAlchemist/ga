import { Link } from 'react-router-dom';
import {
  Box,
  Button,
  Card,
  CardContent,
  Container,
  Grid,
  Typography,
  useTheme,
} from '@mui/material';
import {
  MusicNote as MusicNoteIcon,
  Piano as PianoIcon,
  Chat as ChatIcon,
  Science as ScienceIcon,
  ArrowForward as ArrowForwardIcon,
} from '@mui/icons-material';

const Home = () => {
  const theme = useTheme();

  const features = [
    {
      title: 'Harmonic Studio',
      description: 'Context-first fretboard explorer powered by GaApi analytics. Explore chords, scales, and progressions with intelligent suggestions.',
      icon: <PianoIcon sx={{ fontSize: 48 }} />,
      link: '/harmonic-studio',
      color: theme.palette.primary.main,
    },
    {
      title: 'AI Copilot',
      description: 'Chat with an AI assistant that understands music theory, guitar techniques, and can help you learn and practice.',
      icon: <ChatIcon sx={{ fontSize: 48 }} />,
      link: '/ai-copilot',
      color: theme.palette.secondary.main,
    },
    {
      title: 'Music Generation',
      description: 'Generate guitar music using AI models from Hugging Face. Create backing tracks, chord progressions, and more.',
      icon: <MusicNoteIcon sx={{ fontSize: 48 }} />,
      link: '/music-generation',
      color: '#ff7a45',
    },
    {
      title: 'Demo Applications',
      description: 'Explore advanced features including BSP level generation, psychoacoustic voicing, performance optimization, and more.',
      icon: <ScienceIcon sx={{ fontSize: 48 }} />,
      link: '/demos',
      color: '#6bc1ff',
    },
  ];

  return (
    <Container maxWidth="lg" sx={{ py: 8 }}>
      <Box sx={{ textAlign: 'center', mb: 8 }}>
        <Typography variant="h2" sx={{ fontWeight: 700, mb: 2 }}>
          Guitar Alchemist
        </Typography>
        <Typography variant="h5" color="text.secondary" sx={{ mb: 4 }}>
          Transform your guitar playing with AI-powered music theory
        </Typography>
        <Typography variant="body1" color="text.secondary" sx={{ maxWidth: 800, mx: 'auto' }}>
          A comprehensive platform for guitar learning, music theory exploration, and AI-assisted composition.
          Combining advanced mathematics, music theory, and artificial intelligence to help you master the guitar.
        </Typography>
      </Box>

      <Grid container spacing={4}>
        {features.map((feature) => (
          <Grid item xs={12} md={6} key={feature.title}>
            <Card
              sx={{
                height: '100%',
                display: 'flex',
                flexDirection: 'column',
                transition: 'transform 0.2s, box-shadow 0.2s',
                '&:hover': {
                  transform: 'translateY(-4px)',
                  boxShadow: theme.shadows[8],
                },
              }}
            >
              <CardContent sx={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
                <Box sx={{ color: feature.color, mb: 2 }}>
                  {feature.icon}
                </Box>
                <Typography variant="h5" sx={{ fontWeight: 600, mb: 2 }}>
                  {feature.title}
                </Typography>
                <Typography variant="body2" color="text.secondary" sx={{ mb: 3, flex: 1 }}>
                  {feature.description}
                </Typography>
                <Button
                  component={Link}
                  to={feature.link}
                  variant="contained"
                  endIcon={<ArrowForwardIcon />}
                  sx={{
                    bgcolor: feature.color,
                    '&:hover': {
                      bgcolor: feature.color,
                      filter: 'brightness(1.1)',
                    },
                  }}
                >
                  Explore
                </Button>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>

      <Box sx={{ mt: 8, textAlign: 'center' }}>
        <Typography variant="h4" sx={{ fontWeight: 600, mb: 3 }}>
          Features
        </Typography>
        <Grid container spacing={3}>
          <Grid item xs={12} md={4}>
            <Typography variant="h6" sx={{ fontWeight: 600, mb: 1 }}>
              🎸 Fretboard Visualization
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Interactive 3D fretboard with real-time chord and scale visualization
            </Typography>
          </Grid>
          <Grid item xs={12} md={4}>
            <Typography variant="h6" sx={{ fontWeight: 600, mb: 1 }}>
              🧠 AI-Powered Analysis
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Intelligent chord suggestions and progression analysis using machine learning
            </Typography>
          </Grid>
          <Grid item xs={12} md={4}>
            <Typography variant="h6" sx={{ fontWeight: 600, mb: 1 }}>
              🎵 Music Generation
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Generate backing tracks and audio samples using Hugging Face AI models
            </Typography>
          </Grid>
          <Grid item xs={12} md={4}>
            <Typography variant="h6" sx={{ fontWeight: 600, mb: 1 }}>
              📊 Advanced Theory
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Deep music theory integration with modal interchange and voice leading
            </Typography>
          </Grid>
          <Grid item xs={12} md={4}>
            <Typography variant="h6" sx={{ fontWeight: 600, mb: 1 }}>
              🎮 BSP Level Explorer
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Explore music theory concepts in a 3D DOOM-style environment
            </Typography>
          </Grid>
          <Grid item xs={12} md={4}>
            <Typography variant="h6" sx={{ fontWeight: 600, mb: 1 }}>
              🔬 Research Demos
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Experimental features showcasing cutting-edge music technology
            </Typography>
          </Grid>
        </Grid>
      </Box>
    </Container>
  );
};

export default Home;

