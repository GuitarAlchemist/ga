import { Container } from '@mui/material';
import { EcosystemRoadmapExplorer } from 'ga-react-components/src/components/EcosystemRoadmap';

const EcosystemRoadmapDemo = () => {
  return (
    <Container maxWidth={false} disableGutters sx={{ height: 'calc(100vh - 64px)', overflow: 'hidden' }}>
      <EcosystemRoadmapExplorer />
    </Container>
  );
};

export default EcosystemRoadmapDemo;
