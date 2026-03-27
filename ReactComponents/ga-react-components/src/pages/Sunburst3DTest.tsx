/**
 * Sunburst3D Test Page
 * 
 * Test page for the 3D Sunburst visualization component.
 */

import React from 'react';
import { Container } from '@mui/material';
import { Sunburst3DDemo } from '../components/BSP';
import { DemoErrorBoundary } from '../components/Common/DemoErrorBoundary';

const Sunburst3DTest: React.FC = () => {
  return (
    <DemoErrorBoundary demoName="Sunburst 3D">
      <Container maxWidth={false} disableGutters sx={{ height: '100vh', overflow: 'hidden' }}>
        <Sunburst3DDemo />
      </Container>
    </DemoErrorBoundary>
  );
};

export default Sunburst3DTest;

