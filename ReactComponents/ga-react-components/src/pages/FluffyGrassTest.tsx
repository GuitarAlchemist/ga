/**
 * FluffyGrass Test Page
 * 
 * Test page for the Fluffy Grass component.
 */

import React from 'react';
import { Container, Box } from '@mui/material';
import { FluffyGrassDemo } from '../components/FluffyGrass';

const FluffyGrassTest: React.FC = () => {
  return (
    <Container maxWidth={false} disableGutters sx={{ height: '100vh', overflow: 'hidden' }}>
      <FluffyGrassDemo />
    </Container>
  );
};

export default FluffyGrassTest;

