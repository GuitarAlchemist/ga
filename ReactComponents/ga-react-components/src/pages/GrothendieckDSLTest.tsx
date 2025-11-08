import React from 'react';
import { Box, Container, Typography, Paper } from '@mui/material';
import GrothendieckDSLDemo from '../components/DSL/GrothendieckDSLDemo';

const GrothendieckDSLTest: React.FC = () => {
  return (
    <Container maxWidth={false} sx={{ py: 4 }}>
      <Paper elevation={3} sx={{ p: 3, mb: 3 }}>
        <Typography variant="h3" gutterBottom>
          Grothendieck Operations DSL
        </Typography>
        <Typography variant="body1" color="text.secondary" paragraph>
          Interactive demo of the Grothendieck Operations Domain-Specific Language (DSL) for category theory operations on musical objects.
        </Typography>
        <Typography variant="body2" color="text.secondary">
          <strong>Features:</strong> Live parsing, AST visualization, 15+ example operations, 7 operation categories
        </Typography>
        <Typography variant="body2" color="text.secondary">
          <strong>Categories:</strong> Category operations, Functors, Natural transformations, Limits, Colimits, Topos operations, Sheaves
        </Typography>
      </Paper>

      <GrothendieckDSLDemo />
    </Container>
  );
};

export default GrothendieckDSLTest;

