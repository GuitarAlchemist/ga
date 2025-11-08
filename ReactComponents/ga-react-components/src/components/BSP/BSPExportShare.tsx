import React, { useState } from 'react';
import {
  Box,
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Typography,
  Chip,
  Alert,
  Tabs,
  Tab,
  IconButton,
  Tooltip,
  Snackbar
} from '@mui/material';
import {
  Share,
  Download,
  ContentCopy,
  Close,
  FileDownload,
  Link as LinkIcon
} from '@mui/icons-material';
import { BSPSpatialQueryResponse, BSPTonalContextResponse, BSPProgressionAnalysisResponse } from './BSPApiService';

interface BSPExportShareProps {
  spatialResult: BSPSpatialQueryResponse | null;
  tonalResult: BSPTonalContextResponse | null;
  progressionResult: BSPProgressionAnalysisResponse | null;
  queryParams: {
    spatialQuery: string;
    spatialRadius: number;
    spatialStrategy: string;
    tonalQuery: string;
    progression: Array<{ name: string; pitchClasses: string }>;
  };
}

interface TabPanelProps {
  children?: React.ReactNode;
  index: number;
  value: number;
}

const TabPanel: React.FC<TabPanelProps> = ({ children, value, index }) => (
  <div hidden={value !== index}>
    {value === index && <Box sx={{ pt: 2 }}>{children}</Box>}
  </div>
);

export const BSPExportShare: React.FC<BSPExportShareProps> = ({
  spatialResult,
  tonalResult,
  progressionResult,
  queryParams
}) => {
  const [open, setOpen] = useState(false);
  const [tabValue, setTabValue] = useState(0);
  const [shareUrl, setShareUrl] = useState('');
  const [snackbarOpen, setSnackbarOpen] = useState(false);
  const [snackbarMessage, setSnackbarMessage] = useState('');

  const hasResults = spatialResult || tonalResult || progressionResult;

  const generateShareUrl = () => {
    const baseUrl = window.location.origin + window.location.pathname;
    const params = new URLSearchParams();
    
    if (spatialResult) {
      params.set('spatialQuery', queryParams.spatialQuery);
      params.set('spatialRadius', queryParams.spatialRadius.toString());
      params.set('spatialStrategy', queryParams.spatialStrategy);
    }
    
    if (tonalResult) {
      params.set('tonalQuery', queryParams.tonalQuery);
    }
    
    if (progressionResult) {
      params.set('progression', JSON.stringify(queryParams.progression));
    }
    
    return `${baseUrl}?${params.toString()}`;
  };

  const exportToJSON = () => {
    const exportData = {
      timestamp: new Date().toISOString(),
      queryParams,
      results: {
        spatial: spatialResult,
        tonal: tonalResult,
        progression: progressionResult
      }
    };

    const blob = new Blob([JSON.stringify(exportData, null, 2)], {
      type: 'application/json'
    });
    
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `bsp-analysis-${new Date().toISOString().split('T')[0]}.json`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);

    showSnackbar('Analysis exported to JSON file');
  };

  const exportToCSV = () => {
    let csvContent = 'Type,Query,Result,Confidence,QueryTime\n';
    
    if (spatialResult) {
      csvContent += `Spatial,"${spatialResult.queryChord}","${spatialResult.elements.length} elements",${spatialResult.confidence},${spatialResult.queryTimeMs}\n`;
    }
    
    if (tonalResult) {
      csvContent += `Tonal,"${tonalResult.queryChord}","${tonalResult.region.name}",${tonalResult.confidence},${tonalResult.queryTimeMs}\n`;
    }
    
    if (progressionResult) {
      csvContent += `Progression,"${progressionResult.progression.join(' - ')}","${progressionResult.overallAnalysis.averageConfidence}",${progressionResult.overallAnalysis.averageConfidence},N/A\n`;
    }

    const blob = new Blob([csvContent], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `bsp-analysis-${new Date().toISOString().split('T')[0]}.csv`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);

    showSnackbar('Analysis exported to CSV file');
  };

  const exportToMarkdown = () => {
    let markdown = '# BSP Musical Analysis Report\n\n';
    markdown += `Generated on: ${new Date().toLocaleString()}\n\n`;
    
    if (spatialResult) {
      markdown += '## Spatial Query Results\n\n';
      markdown += `- **Query Chord**: ${spatialResult.queryChord}\n`;
      markdown += `- **Search Radius**: ${spatialResult.radius}\n`;
      markdown += `- **Strategy**: ${spatialResult.strategy}\n`;
      markdown += `- **Region**: ${spatialResult.region.name} (${spatialResult.region.tonalityType})\n`;
      markdown += `- **Confidence**: ${(spatialResult.confidence * 100).toFixed(1)}%\n`;
      markdown += `- **Query Time**: ${spatialResult.queryTimeMs.toFixed(2)}ms\n`;
      markdown += `- **Elements Found**: ${spatialResult.elements.length}\n\n`;
    }
    
    if (tonalResult) {
      markdown += '## Tonal Context Analysis\n\n';
      markdown += `- **Query Chord**: ${tonalResult.queryChord}\n`;
      markdown += `- **Best Fit Region**: ${tonalResult.region.name}\n`;
      markdown += `- **Tonality Type**: ${tonalResult.region.tonalityType}\n`;
      markdown += `- **Confidence**: ${(tonalResult.confidence * 100).toFixed(1)}%\n`;
      markdown += `- **Query Time**: ${tonalResult.queryTimeMs.toFixed(2)}ms\n\n`;
    }
    
    if (progressionResult) {
      markdown += '## Progression Analysis\n\n';
      markdown += `- **Progression**: ${progressionResult.progression.join(' → ')}\n`;
      markdown += `- **Average Confidence**: ${(progressionResult.overallAnalysis.averageConfidence * 100).toFixed(1)}%\n`;
      markdown += `- **Average Smoothness**: ${(progressionResult.overallAnalysis.averageSmoothness * 100).toFixed(1)}%\n`;
      markdown += `- **Total Common Tones**: ${progressionResult.overallAnalysis.totalCommonTones}\n\n`;
      
      markdown += '### Chord Analysis\n\n';
      progressionResult.chordAnalyses.forEach((analysis, index) => {
        markdown += `${index + 1}. **${analysis.name}**: ${analysis.region.name} (${(analysis.confidence * 100).toFixed(1)}% confidence)\n`;
      });
      markdown += '\n';
    }

    const blob = new Blob([markdown], { type: 'text/markdown' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `bsp-analysis-${new Date().toISOString().split('T')[0]}.md`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);

    showSnackbar('Analysis exported to Markdown file');
  };

  const copyShareUrl = () => {
    const url = generateShareUrl();
    navigator.clipboard.writeText(url).then(() => {
      showSnackbar('Share URL copied to clipboard');
    }).catch(() => {
      showSnackbar('Failed to copy URL to clipboard');
    });
  };

  const showSnackbar = (message: string) => {
    setSnackbarMessage(message);
    setSnackbarOpen(true);
  };

  const handleOpen = () => {
    setOpen(true);
    setShareUrl(generateShareUrl());
  };

  return (
    <>
      <Tooltip title="Export and Share Analysis Results">
        <span>
          <Button
            variant="outlined"
            startIcon={<Share />}
            onClick={handleOpen}
            disabled={!hasResults}
            size="small"
          >
            Export & Share
          </Button>
        </span>
      </Tooltip>

      <Dialog open={open} onClose={() => setOpen(false)} maxWidth="md" fullWidth>
        <DialogTitle>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            Export & Share Analysis
            <IconButton onClick={() => setOpen(false)} size="small">
              <Close />
            </IconButton>
          </Box>
        </DialogTitle>
        
        <DialogContent>
          <Tabs value={tabValue} onChange={(_, newValue) => setTabValue(newValue)}>
            <Tab icon={<LinkIcon />} label="Share" />
            <Tab icon={<FileDownload />} label="Export" />
          </Tabs>

          <TabPanel value={tabValue} index={0}>
            <Typography variant="h6" gutterBottom>
              Share Your Analysis
            </Typography>
            <Typography variant="body2" color="text.secondary" paragraph>
              Generate a shareable URL that includes your current query parameters.
            </Typography>
            
            <TextField
              fullWidth
              label="Share URL"
              value={shareUrl}
              InputProps={{
                readOnly: true,
                endAdornment: (
                  <IconButton onClick={copyShareUrl} edge="end">
                    <ContentCopy />
                  </IconButton>
                )
              }}
              sx={{ mb: 2 }}
            />
            
            <Alert severity="info">
              This URL will allow others to reproduce your current BSP analysis queries.
              Note that the actual results may vary if the backend data changes.
            </Alert>
          </TabPanel>

          <TabPanel value={tabValue} index={1}>
            <Typography variant="h6" gutterBottom>
              Export Analysis Results
            </Typography>
            <Typography variant="body2" color="text.secondary" paragraph>
              Download your analysis results in various formats for further processing or documentation.
            </Typography>
            
            <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap' }}>
              <Button
                variant="outlined"
                startIcon={<Download />}
                onClick={exportToJSON}
                disabled={!hasResults}
              >
                Export JSON
              </Button>
              
              <Button
                variant="outlined"
                startIcon={<Download />}
                onClick={exportToCSV}
                disabled={!hasResults}
              >
                Export CSV
              </Button>
              
              <Button
                variant="outlined"
                startIcon={<Download />}
                onClick={exportToMarkdown}
                disabled={!hasResults}
              >
                Export Markdown
              </Button>
            </Box>
            
            <Box sx={{ mt: 3 }}>
              <Typography variant="subtitle2" gutterBottom>
                Available Data:
              </Typography>
              <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
                {spatialResult && <Chip label="Spatial Query" size="small" color="primary" />}
                {tonalResult && <Chip label="Tonal Context" size="small" color="primary" />}
                {progressionResult && <Chip label="Progression Analysis" size="small" color="primary" />}
                {!hasResults && <Chip label="No data available" size="small" />}
              </Box>
            </Box>
          </TabPanel>
        </DialogContent>
        
        <DialogActions>
          <Button onClick={() => setOpen(false)}>Close</Button>
        </DialogActions>
      </Dialog>

      <Snackbar
        open={snackbarOpen}
        autoHideDuration={3000}
        onClose={() => setSnackbarOpen(false)}
        message={snackbarMessage}
      />
    </>
  );
};
