import React, { Suspense } from 'react';
import { Container } from '@mui/material';

// Use the new force-directed Prime Radiant
const ForceRadiant = React.lazy(() =>
  import('../components/PrimeRadiant').then(mod => ({ default: mod.ForceRadiant }))
);

const PrimeRadiantTest: React.FC = () => {
  console.log('[PrimeRadiantTest] Rendering ForceRadiant...');
  return (
    <Container maxWidth={false} disableGutters sx={{ height: '100vh', overflow: 'hidden', position: 'fixed', top: 0, left: 0, right: 0, bottom: 0 }}>
      <Suspense fallback={
        <div style={{
          width: '100%',
          height: '100vh',
          background: '#000008',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          color: '#FFD700',
          fontFamily: 'monospace',
          fontSize: 18,
        }}>
          Loading Prime Radiant...
        </div>
      }>
        <ForceRadiant
          liveDataUrl="/api/governance"
          liveHubUrl="/hubs/governance"
          onNodeSelect={(node) => {
            if (node) {
              console.log('[PrimeRadiant] Selected:', node.name, node.type);
            } else {
              console.log('[PrimeRadiant] Selection cleared');
            }
          }}
        />
      </Suspense>
    </Container>
  );
};

export default PrimeRadiantTest;
