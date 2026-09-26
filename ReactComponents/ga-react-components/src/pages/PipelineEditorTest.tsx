import React from 'react';
import { Box } from '@mui/material';
import { PipelineEditor } from '../components/IxqlViewer/PipelineEditor';

// Local-only: the /ix-pipeline/* routes it calls are gated to direct-local
// traffic, so on the public tunnel the catalog shows as unavailable.
const PipelineEditorTest: React.FC = () => (
  <Box sx={{ height: 'calc(100vh - 64px)' }}>
    <PipelineEditor />
  </Box>
);

export default PipelineEditorTest;
