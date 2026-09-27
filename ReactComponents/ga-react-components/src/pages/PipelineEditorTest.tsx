import React, { useState } from 'react';
import { Box, CssBaseline, Tab, Tabs, ThemeProvider } from '@mui/material';
import { PipelineEditor } from '../components/IxqlViewer/PipelineEditor';
import { PetriEditor } from '../components/IxqlViewer/PetriEditor';
import { useOsTheme } from '../hooks/useOsTheme';

// Local-only: the /ix-pipeline/* routes it calls are gated to direct-local
// traffic, so on the public tunnel the catalog shows as unavailable.
// The editor follows the OS colour scheme (light or dark).
const PipelineEditorTest: React.FC = () => {
  const [tab, setTab] = useState<'pipeline' | 'petri'>('pipeline');
  return (
    <ThemeProvider theme={useOsTheme()}>
      <CssBaseline />
      <Box sx={{ height: 'calc(100vh - 64px)', bgcolor: 'background.default', color: 'text.primary', display: 'flex', flexDirection: 'column' }}>
        <Tabs value={tab} onChange={(_e, v: 'pipeline' | 'petri') => setTab(v)} sx={{ minHeight: 36, borderBottom: 1, borderColor: 'divider', '& .MuiTab-root': { minHeight: 36, py: 0.5 } }}>
          <Tab value="pipeline" label="IX pipeline" />
          <Tab value="petri" label="Petri net" />
        </Tabs>
        <Box sx={{ flex: 1, minHeight: 0 }}>
          {tab === 'pipeline' ? <PipelineEditor /> : <PetriEditor />}
        </Box>
      </Box>
    </ThemeProvider>
  );
};

export default PipelineEditorTest;
