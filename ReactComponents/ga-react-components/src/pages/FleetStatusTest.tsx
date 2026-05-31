import React from 'react';
import { Container } from '@mui/material';
import { FleetStatus } from '../components/FleetStatus';

const FleetStatusTest: React.FC = () => {
  return (
    <Container maxWidth={false} disableGutters sx={{ minHeight: '100vh', bgcolor: '#fafafa' }}>
      <FleetStatus />
    </Container>
  );
};

export default FleetStatusTest;
