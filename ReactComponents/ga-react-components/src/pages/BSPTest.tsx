import React from 'react';
import { Container, Typography, Box, Alert, AlertTitle } from '@mui/material';
import { BSPInterface } from '../components/BSP';

const BSPTest: React.FC = () => {
  return (
    <Container maxWidth="xl" sx={{ py: 4 }}>
      <Box sx={{ mb: 4 }}>
        <Typography variant="h3" component="h1" gutterBottom align="center">
          BSP Musical Analysis Demo
        </Typography>
        <Typography variant="h6" component="h2" gutterBottom align="center" color="text.secondary">
          Binary Space Partitioning for Advanced Musical Analysis
        </Typography>
      </Box>

      <Alert severity="info" sx={{ mb: 4 }}>
        <AlertTitle>BSP System Overview</AlertTitle>
        This demo showcases the Binary Space Partitioning (BSP) system for musical analysis. 
        BSP enables sub-millisecond spatial queries, intelligent chord suggestions, and harmonic 
        relationship analysis. The system organizes musical elements in a hierarchical tree structure 
        for efficient similarity searches and tonal context analysis.
      </Alert>

      <Alert severity="warning" sx={{ mb: 4 }}>
        <AlertTitle>Backend Connection Required</AlertTitle>
        This interface requires the Guitar Alchemist API server to be running with BSP endpoints enabled. 
        Make sure the API is accessible at the configured base URL (default: https://localhost:7001).
      </Alert>

      <BSPInterface />

      <Box sx={{ mt: 6, p: 3, bgcolor: 'background.paper', borderRadius: 2 }}>
        <Typography variant="h6" gutterBottom>
          Technical Implementation
        </Typography>
        <Typography variant="body2" paragraph>
          The BSP system is implemented with:
        </Typography>
        <ul>
          <li><strong>Backend API</strong>: ASP.NET Core controllers with BSP service integration</li>
          <li><strong>Core Library</strong>: GA.BSP.Core with TonalBSPService and spatial algorithms</li>
          <li><strong>Frontend Interface</strong>: React components with Material-UI for interactive analysis</li>
          <li><strong>Real-time Analysis</strong>: Sub-millisecond query performance with confidence scoring</li>
        </ul>
        
        <Typography variant="body2" paragraph sx={{ mt: 2 }}>
          <strong>Performance Metrics:</strong>
        </Typography>
        <ul>
          <li>Query Speed: &lt; 1ms for spatial queries</li>
          <li>Tree Depth: Maximum 2 levels (Chromatic → Major/Minor)</li>
          <li>Regions: 3 total tonal regions</li>
          <li>Strategies: 4 partition strategies available</li>
        </ul>
      </Box>
    </Container>
  );
};

export default BSPTest;
