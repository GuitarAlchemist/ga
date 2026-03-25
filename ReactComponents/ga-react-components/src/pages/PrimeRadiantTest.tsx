import React, { Suspense } from 'react';
import { Container } from '@mui/material';

// Use the new force-directed Prime Radiant
const ForceRadiant = React.lazy(() =>
  import('../components/PrimeRadiant').then(mod => ({ default: mod.ForceRadiant }))
);

const PrimeRadiantTest: React.FC = () => {
  console.log('[PrimeRadiantTest] Rendering ForceRadiant...');
  return (
    <Container maxWidth={false} disableGutters sx={{ height: 'calc(100vh - 28px)', overflow: 'hidden' }}>
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
