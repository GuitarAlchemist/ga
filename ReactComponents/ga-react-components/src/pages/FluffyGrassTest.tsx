/**
 * FluffyGrass Test Page
 * 
 * Test page for the Fluffy Grass component.
 */

import React from 'react';
import { Container } from '@mui/material';
import { FluffyGrassDemo } from '../components/FluffyGrass';
import { DemoErrorBoundary } from '../components/Common/DemoErrorBoundary';

const FluffyGrassTest: React.FC = () => {
  return (
    <DemoErrorBoundary demoName="Fluffy Grass">
      <Container maxWidth={false} disableGutters sx={{ height: '100vh', overflow: 'hidden' }}>
        <FluffyGrassDemo />
      </Container>
    </DemoErrorBoundary>
  );
};

export default FluffyGrassTest;

