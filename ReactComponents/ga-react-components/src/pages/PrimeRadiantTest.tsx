import React, { Suspense, useEffect } from 'react';
import { Container } from '@mui/material';

// Use the new force-directed Prime Radiant
const ForceRadiant = React.lazy(() =>
  import('../components/PrimeRadiant').then(mod => ({ default: mod.ForceRadiant }))
);

const PrimeRadiantTest: React.FC = () => {
  console.log('[PrimeRadiantTest] Rendering ForceRadiant...');

  // Prevent body scrolling while Prime Radiant is mounted
  useEffect(() => {
    const prev = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    document.documentElement.style.overflow = 'hidden';
    return () => {
      document.body.style.overflow = prev;
      document.documentElement.style.overflow = '';
    };
  }, []);

  return (
    <Container maxWidth={false} disableGutters sx={{ flex: 1, overflow: 'hidden' }}>
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
