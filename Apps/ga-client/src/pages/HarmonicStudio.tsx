import { Container, Grid, Typography } from '@mui/material';
import KeySelector from '../components/KeySelector';
import KeyContextPanel from '../components/dashboard/KeyContextPanel';
import ChordPalette from '../components/dashboard/ChordPalette';
import ProgressionExplorer from '../components/dashboard/ProgressionExplorer';
import FretboardWorkbench from '../components/dashboard/FretboardWorkbench';

const HarmonicStudio = () => {
  return (
    <Container maxWidth="xl" sx={{ py: 4 }}>
      <div style={{ display: 'flex', alignItems: 'flex-start', gap: '16px', marginBottom: '24px', flexWrap: 'wrap' }}>
        <div style={{ flexGrow: 1 }}>
          <Typography variant="h3" sx={{ fontWeight: 700 }}>
            Harmonic Studio
          </Typography>
          <Typography variant="subtitle1" color="text.secondary">
            Context-first fretboard explorer powered by GaApi analytics
          </Typography>
        </div>
      </div>

      <KeySelector />

      <Grid container spacing={3} sx={{ mt: 1 }}>
        <Grid item xs={12} md={4}>
          <KeyContextPanel />
        </Grid>
        <Grid item xs={12} md={8}>
          <FretboardWorkbench />
        </Grid>
        <Grid item xs={12} md={5}>
          <ChordPalette />
        </Grid>
        <Grid item xs={12} md={7}>
          <ProgressionExplorer />
        </Grid>
      </Grid>
    </Container>
  );
};

export default HarmonicStudio;

