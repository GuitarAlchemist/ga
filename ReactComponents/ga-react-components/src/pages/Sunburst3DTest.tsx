/**
 * Sunburst3D Test Page
 * 
 * Test page for the 3D Sunburst visualization component.
 */

import React from 'react';
import { Container, Box } from '@mui/material';
import { Sunburst3DDemo } from '../components/BSP';

const Sunburst3DTest: React.FC = () => {
  return (
    <Container maxWidth={false} disableGutters sx={{ height: '100vh', overflow: 'hidden' }}>
      <Sunburst3DDemo />
    </Container>
  );
};

export default Sunburst3DTest;

