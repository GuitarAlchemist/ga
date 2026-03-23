import React from 'react';
import { Container } from '@mui/material';
import { PrimeRadiant } from '../components/PrimeRadiant';

const PrimeRadiantTest: React.FC = () => {
  return (
    <Container maxWidth={false} disableGutters sx={{ height: '100vh', overflow: 'hidden' }}>
      <PrimeRadiant
        onNodeSelect={(node) => {
          if (node) {
            console.log('[PrimeRadiant] Selected:', node.name, node.type);
          } else {
            console.log('[PrimeRadiant] Selection cleared');
          }
        }}
      />
    </Container>
  );
};

export default PrimeRadiantTest;
