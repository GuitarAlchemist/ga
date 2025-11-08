import React from 'react';
import { Box, Container, Typography, Paper } from '@mui/material';
import TabConverter from '../components/TabConverter';

const TabConverterTest: React.FC = () => {
  return (
    <Container maxWidth={false} sx={{ py: 4 }}>
      <Paper elevation={3} sx={{ p: 3, mb: 3 }}>
        <Typography variant="h3" gutterBottom>
          Guitar Tab Format Converter
        </Typography>
        <Typography variant="body1" color="text.secondary" paragraph>
          Convert between different guitar tablature formats with live preview and VexFlow rendering.
        </Typography>
        <Typography variant="body2" color="text.secondary">
          <strong>Supported Formats:</strong> ASCII Tab, VexTab
        </Typography>
        <Typography variant="body2" color="text.secondary">
          <strong>Features:</strong> Bi-directional conversion, Live preview, File upload/download, Example library
        </Typography>
      </Paper>

      <TabConverter />
    </Container>
  );
};

export default TabConverterTest;

