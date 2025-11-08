/**
 * Immersive Musical World Test Page
 * 
 * Test page for the full immersive 3D musical world.
 */

import React from 'react';
import { Container, Box } from '@mui/material';
import { ImmersiveMusicalWorldDemo } from '../components/BSP';

const ImmersiveMusicalWorldTest: React.FC = () => {
  return (
    <Container maxWidth={false} disableGutters sx={{ height: 'calc(100vh - 48px)', overflow: 'hidden' }}>
      <ImmersiveMusicalWorldDemo />
    </Container>
  );
};

export default ImmersiveMusicalWorldTest;

