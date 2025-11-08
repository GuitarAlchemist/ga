// @ts-nocheck
import React, { useState, useEffect } from 'react';
import {
  Box,
  Paper,
  TextField,
  Button,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Typography,
  Alert,
  CircularProgress,
  Grid,
  Divider,
  IconButton,
  Tooltip,
  SelectChangeEvent,
} from '@mui/material';
import SwapHorizIcon from '@mui/icons-material/SwapHoriz';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import DownloadIcon from '@mui/icons-material/Download';
import UploadIcon from '@mui/icons-material/Upload';
import VexTabViewer from './VexTabViewer';

interface TabFormat {
  name: string;
  description: string;
  extensions: string[];
}

interface ConversionResponse {
  success: boolean;
  result?: string;
  metadata?: {
    sourceFormat: string;
    targetFormat: string;
    conversionDuration: number;
    noteCount: number;
    measureCount: number;
  };
  warnings?: string[];
  errors?: string[];
}

const TabConverter: React.FC = () => {
  const [sourceFormat, setSourceFormat] = useState<string>('ASCII');
  const [targetFormat, setTargetFormat] = useState<string>('VexTab');
  const [sourceContent, setSourceContent] = useState<string>('');
  const [targetContent, setTargetContent] = useState<string>('');
  const [formats, setFormats] = useState<TabFormat[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string>('');
  const [warnings, setWarnings] = useState<string[]>([]);
  const [metadata, setMetadata] = useState<ConversionResponse['metadata']>();

  const API_BASE_URL = 'https://localhost:7003/api/TabConversion';

  // Example tabs
  const examples = {
    ASCII: `e|---0---3---5---7---|
B|---0---0---0---0---|
G|---0---0---0---0---|
D|---2---2---2---2---|
A|---2---3---5---7---|
E|---0---x---x---x---|`,
    VexTab: `tabstave notation=true tablature=true
notes :q 0/1 3/1 5/1 7/1`,
  };

  // Load available formats
  useEffect(() => {
    const loadFormats = async () => {
      try {
        const response = await fetch(`${API_BASE_URL}/formats`);
        if (response.ok) {
          const data = await response.json();
          setFormats(data.formats || []);
        }
      } catch (err) {
        console.error('Failed to load formats:', err);
        // Fallback to default formats
        setFormats([
          { name: 'ASCII', description: 'Plain text ASCII tablature', extensions: ['.txt', '.tab'] },
          { name: 'VexTab', description: 'VexTab notation format', extensions: ['.vextab'] },
        ]);
      }
    };
    loadFormats();
  }, []);

  // Load example on mount
  useEffect(() => {
    setSourceContent(examples.ASCII);
  }, []);

  const handleConvert = async () => {
    if (!sourceContent.trim()) {
      setError('Please enter some tab content to convert');
      return;
    }

    setLoading(true);
    setError('');
    setWarnings([]);
    setMetadata(undefined);

    try {
      const response = await fetch(`${API_BASE_URL}/convert`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          sourceFormat,
          targetFormat,
          content: sourceContent,
          options: {},
        }),
      });

      const data: ConversionResponse = await response.json();

      if (data.success && data.result) {
        setTargetContent(data.result);
        setMetadata(data.metadata);
        setWarnings(data.warnings || []);
      } else {
        setError(data.errors?.join(', ') || 'Conversion failed');
      }
    } catch (err) {
      setError(`Failed to convert: ${err instanceof Error ? err.message : 'Unknown error'}`);
    } finally {
      setLoading(false);
    }
  };

  const handleSwapFormats = () => {
    setSourceFormat(targetFormat);
    setTargetFormat(sourceFormat);
    setSourceContent(targetContent);
    setTargetContent('');
  };

  const handleCopy = (content: string) => {
    navigator.clipboard.writeText(content);
  };

  const handleDownload = (content: string, format: string) => {
    const blob = new Blob([content], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `tab.${format.toLowerCase()}`;
    a.click();
    URL.revokeObjectURL(url);
  };

  const handleFileUpload = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (file) {
      const reader = new FileReader();
      reader.onload = (e) => {
        const content = e.target?.result as string;
        setSourceContent(content);
      };
      reader.readAsText(file);
    }
  };

  const handleSourceFormatChange = (event: SelectChangeEvent<string>) => {
    setSourceFormat(event.target.value);
  };

  const handleTargetFormatChange = (event: SelectChangeEvent<string>) => {
    setTargetFormat(event.target.value);
  };

  return (
    <Box sx={{ p: 3, maxWidth: 1400, mx: 'auto' }}>
      <Typography variant="h4" gutterBottom>
        Guitar Tab Format Converter
      </Typography>
      <Typography variant="body2" color="text.secondary" paragraph>
        Convert between different guitar tablature formats with live preview
      </Typography>

      {/* Format Selection */}
      <Grid container spacing={2} sx={{ mb: 3 }}>
        <Grid item xs={12} md={5}>
          <FormControl fullWidth>
            <InputLabel>Source Format</InputLabel>
            <Select value={sourceFormat} onChange={handleSourceFormatChange} label="Source Format">
              {formats.map((fmt) => (
                <MenuItem key={fmt.name} value={fmt.name}>
                  {fmt.name} - {fmt.description}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        </Grid>
        <Grid item xs={12} md={2} sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
          <Tooltip title="Swap formats">
            <IconButton onClick={handleSwapFormats} color="primary">
              <SwapHorizIcon />
            </IconButton>
          </Tooltip>
        </Grid>
        <Grid item xs={12} md={5}>
          <FormControl fullWidth>
            <InputLabel>Target Format</InputLabel>
            <Select value={targetFormat} onChange={handleTargetFormatChange} label="Target Format">
              {formats.map((fmt) => (
                <MenuItem key={fmt.name} value={fmt.name}>
                  {fmt.name} - {fmt.description}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        </Grid>
      </Grid>

      {/* Error/Warning Display */}
      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError('')}>
          {error}
        </Alert>
      )}
      {warnings.length > 0 && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          {warnings.join(', ')}
        </Alert>
      )}

      {/* Conversion Button */}
      <Box sx={{ mb: 3, display: 'flex', gap: 2 }}>
        <Button
          variant="contained"
          onClick={handleConvert}
          disabled={loading || !sourceContent.trim()}
          startIcon={loading ? <CircularProgress size={20} /> : undefined}
        >
          {loading ? 'Converting...' : 'Convert'}
        </Button>
        <Button variant="outlined" onClick={() => setSourceContent(examples[sourceFormat as keyof typeof examples] || '')}>
          Load Example
        </Button>
        <Button variant="outlined" component="label" startIcon={<UploadIcon />}>
          Upload File
          <input type="file" hidden accept=".txt,.tab,.vextab" onChange={handleFileUpload} />
        </Button>
      </Box>

      {/* Metadata Display */}
      {metadata && (
        <Paper sx={{ p: 2, mb: 3, bgcolor: 'background.default' }}>
          <Typography variant="subtitle2" gutterBottom>
            Conversion Metadata
          </Typography>
          <Grid container spacing={2}>
            <Grid item xs={6} sm={3}>
              <Typography variant="caption" color="text.secondary">
                Duration
              </Typography>
              <Typography variant="body2">{metadata.conversionDuration}ms</Typography>
            </Grid>
            <Grid item xs={6} sm={3}>
              <Typography variant="caption" color="text.secondary">
                Notes
              </Typography>
              <Typography variant="body2">{metadata.noteCount}</Typography>
            </Grid>
            <Grid item xs={6} sm={3}>
              <Typography variant="caption" color="text.secondary">
                Measures
              </Typography>
              <Typography variant="body2">{metadata.measureCount}</Typography>
            </Grid>
          </Grid>
        </Paper>
      )}

      {/* Dual Editor View */}
      <Grid container spacing={3}>
        {/* Source Editor */}
        <Grid item xs={12} md={6}>
          <Paper sx={{ p: 2 }}>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
              <Typography variant="h6">Source ({sourceFormat})</Typography>
              <Box>
                <Tooltip title="Copy">
                  <IconButton size="small" onClick={() => handleCopy(sourceContent)}>
                    <ContentCopyIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              </Box>
            </Box>
            <TextField
              fullWidth
              multiline
              rows={12}
              value={sourceContent}
              onChange={(e) => setSourceContent(e.target.value)}
              placeholder={`Enter ${sourceFormat} tab content here...`}
              sx={{ fontFamily: 'monospace' }}
            />
          </Paper>
        </Grid>

        {/* Target Editor */}
        <Grid item xs={12} md={6}>
          <Paper sx={{ p: 2 }}>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
              <Typography variant="h6">Result ({targetFormat})</Typography>
              <Box>
                <Tooltip title="Copy">
                  <IconButton size="small" onClick={() => handleCopy(targetContent)} disabled={!targetContent}>
                    <ContentCopyIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
                <Tooltip title="Download">
                  <IconButton size="small" onClick={() => handleDownload(targetContent, targetFormat)} disabled={!targetContent}>
                    <DownloadIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              </Box>
            </Box>
            <TextField
              fullWidth
              multiline
              rows={12}
              value={targetContent}
              InputProps={{ readOnly: true }}
              placeholder="Converted tab will appear here..."
              sx={{ fontFamily: 'monospace', bgcolor: 'action.hover' }}
            />
          </Paper>
        </Grid>
      </Grid>

      {/* VexFlow Preview */}
      {targetContent && targetFormat === 'VexTab' && (
        <Paper sx={{ p: 3, mt: 3 }}>
          <Typography variant="h6" gutterBottom>
            Visual Preview
          </Typography>
          <Divider sx={{ mb: 2 }} />
          <Box sx={{ overflow: 'auto' }}>
            <VexTabViewer notation={targetContent.replace(/.*notes\s+:q\s+/, '')} showStandardNotation={true} />
          </Box>
        </Paper>
      )}
    </Box>
  );
};

export default TabConverter;
