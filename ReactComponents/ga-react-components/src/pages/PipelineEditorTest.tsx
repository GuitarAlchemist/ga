import React from 'react';
import { Box, CssBaseline, ThemeProvider } from '@mui/material';
import { PipelineEditor } from '../components/IxqlViewer/PipelineEditor';
import { useOsTheme } from '../hooks/useOsTheme';

// Local-only: the /ix-pipeline/* routes it calls are gated to direct-local
// traffic, so on the public tunnel the catalog shows as unavailable.
// The editor follows the OS colour scheme (light or dark).
const PipelineEditorTest: React.FC = () => (
  <ThemeProvider theme={useOsTheme()}>
    <CssBaseline />
    <Box sx={{ height: 'calc(100vh - 64px)', bgcolor: 'background.default', color: 'text.primary' }}>
      <PipelineEditor />
    </Box>
  </ThemeProvider>
);

export default PipelineEditorTest;
