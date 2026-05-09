/**
 * Immersive Musical World Test Page
 *
 * Test page for the full immersive 3D musical world.
 */

import React from 'react';
import { Box, Container } from '@mui/material';
import { ImmersiveMusicalWorldDemo } from '../components/BSP';
import CastButton from '../components/Common/CastButton';
import { DemoErrorBoundary } from '../components/Common/DemoErrorBoundary';

const ImmersiveMusicalWorldTest: React.FC = () => {
  return (
    <DemoErrorBoundary demoName="Immersive Musical World">
      <Container maxWidth={false} disableGutters sx={{ height: 'calc(100vh - 48px)', overflow: 'hidden' }}>
        <Box sx={{ position: 'relative', width: '100%', height: '100%' }}>
          <ImmersiveMusicalWorldDemo />
          <CastButton />
        </Box>
      </Container>
    </DemoErrorBoundary>
  );
};

export default ImmersiveMusicalWorldTest;
