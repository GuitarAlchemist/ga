import React, { Suspense } from 'react';
import { Container } from '@mui/material';

// Lazy-load the PrimeRadiant to prevent import-time crashes from killing the whole app
const PrimeRadiant = React.lazy(() =>
  import('../components/PrimeRadiant').then(mod => ({ default: mod.PrimeRadiant }))
);

const PrimeRadiantTest: React.FC = () => {
  console.log('[PrimeRadiantTest] Rendering...');
  return (
    <Container maxWidth={false} disableGutters sx={{ height: '100vh', overflow: 'hidden' }}>
      <Suspense fallback={
        <div style={{
          width: '100%',
          height: '100vh',
          background: '#0d1117',
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
        <PrimeRadiant
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
