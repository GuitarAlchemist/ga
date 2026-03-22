import React from 'react';
import { Container } from '@mui/material';
import { EcosystemRoadmapExplorer } from '../components/EcosystemRoadmap';

const EcosystemRoadmapTest: React.FC = () => {
  return (
    <Container maxWidth={false} disableGutters sx={{ height: '100vh', overflow: 'hidden' }}>
      <EcosystemRoadmapExplorer />
    </Container>
  );
};

export default EcosystemRoadmapTest;
