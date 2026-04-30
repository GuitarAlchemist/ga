import { Container } from '@mui/material';
import { VoicingsScatterPlot } from 'ga-react-components/src/components/Voicings';

const VoicingsCorpusDemo = () => {
  return (
    <Container maxWidth={false} disableGutters sx={{ p: 0 }}>
      <VoicingsScatterPlot src="/voicings-tsne.json" width={window.innerWidth - 16} height={window.innerHeight - 100} />
    </Container>
  );
};

export default VoicingsCorpusDemo;
