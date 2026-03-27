/**
 * Immersive Musical World Test Page
 * 
 * Test page for the full immersive 3D musical world.
 */

import React from 'react';
import { Container } from '@mui/material';
import { ImmersiveMusicalWorldDemo } from '../components/BSP';
import { DemoErrorBoundary } from '../components/Common/DemoErrorBoundary';

const ImmersiveMusicalWorldTest: React.FC = () => {
  return (
    <DemoErrorBoundary demoName="Immersive Musical World">
      <Container maxWidth={false} disableGutters sx={{ height: 'calc(100vh - 48px)', overflow: 'hidden' }}>
        <ImmersiveMusicalWorldDemo />
      </Container>
    </DemoErrorBoundary>
  );
};

export default ImmersiveMusicalWorldTest;

