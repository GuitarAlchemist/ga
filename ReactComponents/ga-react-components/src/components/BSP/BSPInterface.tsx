import React, { useState, useEffect } from 'react';
import {
  Box,
  Card,
  CardContent,
  Typography,
  TextField,
  Button,
  Grid,
  Chip,
  Alert,
  CircularProgress,
  Tabs,
  Tab,
  Paper,
  List,
  ListItem,
  ListItemText,
  Divider,
  LinearProgress,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Badge,
  Tooltip,
  IconButton
} from '@mui/material';
import { Search, Analytics, Timeline, Info, Wifi, WifiOff, Refresh } from '@mui/icons-material';
import { BSPApiService, BSPSpatialQueryResponse, BSPTonalContextResponse, BSPProgressionAnalysisResponse } from './BSPApiService';
import { BSPMetricsDashboard } from './BSPMetricsDashboard';
import { BSPSpatialVisualization } from './BSPSpatialVisualization';
import { BSPExportShare } from './BSPExportShare';
import { BSPTutorial } from './BSPTutorial';
import { BSPTreeVisualization } from './BSPTreeVisualization';
import { ThreeHarmonicNavigator } from './ThreeHarmonicNavigator';

interface TabPanelProps {
  children?: React.ReactNode;
  index: number;
  value: number;
}

function TabPanel(props: TabPanelProps) {
  const { children, value, index, ...other } = props;
  return (
    <div
      role="tabpanel"
      hidden={value !== index}
      id={`bsp-tabpanel-${index}`}
      aria-labelledby={`bsp-tab-${index}`}
      {...other}
    >
      {value === index && <Box sx={{ p: 3 }}>{children}</Box>}
    </div>
  );
}

export const BSPInterface: React.FC = () => {
  const [tabValue, setTabValue] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Connection Status State
  const [connectionStatus, setConnectionStatus] = useState<'connected' | 'disconnected' | 'checking'>('checking');
  const [lastConnectionCheck, setLastConnectionCheck] = useState<Date | null>(null);

  // Performance Metrics State
  const [queryTimes, setQueryTimes] = useState<number[]>([]);

  // Spatial Query State
  const [spatialQuery, setSpatialQuery] = useState('C,E,G');
  const [spatialRadius, setSpatialRadius] = useState(0.5);
  const [spatialStrategy, setSpatialStrategy] = useState('CircleOfFifths');
  const [spatialResult, setSpatialResult] = useState<BSPSpatialQueryResponse | null>(null);
  const [visualizationMode, setVisualizationMode] = useState<'2d' | '3d'>('2d');

  // Tonal Context State
  const [tonalQuery, setTonalQuery] = useState('A,C,E');
  const [tonalResult, setTonalResult] = useState<BSPTonalContextResponse | null>(null);

  // Progression Analysis State
  const [progression, setProgression] = useState([
    { name: 'C Major', pitchClasses: 'C,E,G' },
    { name: 'A Minor', pitchClasses: 'A,C,E' },
    { name: 'F Major', pitchClasses: 'F,A,C' },
    { name: 'G Major', pitchClasses: 'G,B,D' }
  ]);
  const [progressionResult, setProgressionResult] = useState<BSPProgressionAnalysisResponse | null>(null);

  // Connection Status Functions
  const checkConnection = async () => {
    setConnectionStatus('checking');
    try {
      const isConnected = await BSPApiService.testConnection();
      setConnectionStatus(isConnected ? 'connected' : 'disconnected');
      setLastConnectionCheck(new Date());
      if (!isConnected) {
        setError('Unable to connect to BSP API. Please ensure the server is running.');
      } else if (error?.includes('connect')) {
        setError(null); // Clear connection-related errors
      }
    } catch (err) {
      setConnectionStatus('disconnected');
      setLastConnectionCheck(new Date());
      setError('Failed to check BSP API connection');
    }
  };

  // Auto-check connection on mount and periodically
  useEffect(() => {
    checkConnection();
    const interval = setInterval(checkConnection, 30000); // Check every 30 seconds
    return () => clearInterval(interval);
  }, []);

  const handleTabChange = (event: React.SyntheticEvent, newValue: number) => {
    setTabValue(newValue);
    setError(null);
  };

  const handleSpatialQuery = async () => {
    setLoading(true);
    setError(null);
    const startTime = performance.now();
    try {
      const result = await BSPApiService.spatialQuery(spatialQuery, spatialRadius, spatialStrategy);
      setSpatialResult(result);

      // Track query performance
      const queryTime = performance.now() - startTime;
      setQueryTimes(prev => [...prev, queryTime].slice(-50)); // Keep last 50 queries
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to perform spatial query');
    } finally {
      setLoading(false);
    }
  };

  const handleTonalContext = async () => {
    setLoading(true);
    setError(null);
    const startTime = performance.now();
    try {
      const result = await BSPApiService.getTonalContext(tonalQuery);
      setTonalResult(result);

      // Track query performance
      const queryTime = performance.now() - startTime;
      setQueryTimes(prev => [...prev, queryTime].slice(-50)); // Keep last 50 queries
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to get tonal context');
    } finally {
      setLoading(false);
    }
  };

  const handleProgressionAnalysis = async () => {
    setLoading(true);
    setError(null);
    const startTime = performance.now();
    try {
      const result = await BSPApiService.analyzeProgression(progression);
      setProgressionResult(result);

      // Track query performance
      const queryTime = performance.now() - startTime;
      setQueryTimes(prev => [...prev, queryTime].slice(-50)); // Keep last 50 queries
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to analyze progression');
    } finally {
      setLoading(false);
    }
  };

  const addChordToProgression = () => {
    setProgression([...progression, { name: '', pitchClasses: '' }]);
  };

  const updateProgressionChord = (index: number, field: 'name' | 'pitchClasses', value: string) => {
    const updated = [...progression];
    updated[index][field] = value;
    setProgression(updated);
  };

  const removeChordFromProgression = (index: number) => {
    setProgression(progression.filter((_, i) => i !== index));
  };

  // Tutorial Demo Handlers
  const handleTutorialDemo = (demoType: 'spatial' | 'tonal' | 'progression') => {
    switch (demoType) {
      case 'spatial':
        setSpatialQuery('C,E,G');
        setSpatialRadius(0.5);
        setSpatialStrategy('CircleOfFifths');
        setTabValue(0);
        // Auto-run the query after a short delay
        setTimeout(() => {
          if (connectionStatus === 'connected') {
            handleSpatialQuery();
          }
        }, 500);
        break;

      case 'tonal':
        setTonalQuery('A,C,E');
        setTabValue(1);
        // Auto-run the query after a short delay
        setTimeout(() => {
          if (connectionStatus === 'connected') {
            handleTonalContext();
          }
        }, 500);
        break;

      case 'progression':
        setProgression([
          { name: 'C Major', pitchClasses: 'C,E,G' },
          { name: 'A Minor', pitchClasses: 'A,C,E' },
          { name: 'F Major', pitchClasses: 'F,A,C' },
          { name: 'G Major', pitchClasses: 'G,B,D' }
        ]);
        setTabValue(2);
        // Auto-run the analysis after a short delay
        setTimeout(() => {
          if (connectionStatus === 'connected') {
            handleProgressionAnalysis();
          }
        }, 500);
        break;
    }
  };

  return (
    <Box sx={{ width: '100%', maxWidth: 1200, margin: '0 auto', p: 2 }} data-testid="bsp-interface">
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
        <Typography variant="h4" component="h1" sx={{ flexGrow: 1, textAlign: 'center' }}>
          🎵 BSP Musical Analysis Interface
        </Typography>

        {/* Connection Status and Actions */}
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }} data-testid="header-actions">
          <BSPTutorial onStartDemo={handleTutorialDemo} />

          <BSPExportShare
            spatialResult={spatialResult}
            tonalResult={tonalResult}
            progressionResult={progressionResult}
            queryParams={{
              spatialQuery,
              spatialRadius,
              spatialStrategy,
              tonalQuery,
              progression
            }}
          />

          <Tooltip title={
            connectionStatus === 'connected'
              ? `Connected to BSP API${lastConnectionCheck ? ` (${lastConnectionCheck.toLocaleTimeString()})` : ''}`
              : connectionStatus === 'disconnected'
              ? `Disconnected from BSP API${lastConnectionCheck ? ` (${lastConnectionCheck.toLocaleTimeString()})` : ''}`
              : 'Checking connection...'
          }>
            <Badge
              color={connectionStatus === 'connected' ? 'success' : connectionStatus === 'disconnected' ? 'error' : 'warning'}
              variant="dot"
              data-testid="connection-status"
            >
              {connectionStatus === 'checking' ? (
                <CircularProgress size={20} />
              ) : connectionStatus === 'connected' ? (
                <Wifi color="success" />
              ) : (
                <WifiOff color="error" />
              )}
            </Badge>
          </Tooltip>

          <Tooltip title="Refresh connection">
            <span>
              <IconButton
                size="small"
                onClick={checkConnection}
                disabled={connectionStatus === 'checking'}
              >
                <Refresh />
              </IconButton>
            </span>
          </Tooltip>
        </Box>
      </Box>

      <Typography variant="body1" sx={{ textAlign: 'center', mb: 4, color: 'text.secondary' }}>
        Explore Binary Space Partitioning for advanced musical analysis and chord relationships
      </Typography>

      {error && (
        <Alert severity="error" sx={{ mb: 3 }}>
          {error}
        </Alert>
      )}

      <Paper sx={{ width: '100%' }}>
        <Tabs value={tabValue} onChange={handleTabChange} aria-label="BSP analysis tabs">
          <Tab icon={<Search />} label="Spatial Query" />
          <Tab icon={<Analytics />} label="Tonal Context" />
          <Tab icon={<Timeline />} label="Progression Analysis" />
          <Tab icon={<Info />} label="BSP Info" />
        </Tabs>

        <TabPanel value={tabValue} index={0}>
          <Typography variant="h6" gutterBottom>
            Spatial Query - Find Similar Chords
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
            Search for chords within a specified spatial radius using BSP algorithms
          </Typography>

          <Grid container spacing={3}>
            <Grid item xs={12} md={6}>
              <Card>
                <CardContent>
                  <Typography variant="h6" gutterBottom>Query Parameters</Typography>
                  
                  <TextField
                    fullWidth
                    label="Pitch Classes"
                    value={spatialQuery}
                    onChange={(e) => setSpatialQuery(e.target.value)}
                    placeholder="C,E,G"
                    helperText="Comma-separated pitch classes (e.g., C,E,G for C Major)"
                    sx={{ mb: 2 }}
                  />

                  <TextField
                    fullWidth
                    type="number"
                    label="Search Radius"
                    value={spatialRadius}
                    onChange={(e) => setSpatialRadius(parseFloat(e.target.value))}
                    inputProps={{ min: 0.1, max: 2.0, step: 0.1 }}
                    helperText="Spatial search radius (0.1 - 2.0)"
                    sx={{ mb: 2 }}
                  />

                  <FormControl fullWidth sx={{ mb: 3 }}>
                    <InputLabel>Partition Strategy</InputLabel>
                    <Select
                      value={spatialStrategy}
                      onChange={(e) => setSpatialStrategy(e.target.value)}
                      label="Partition Strategy"
                    >
                      <MenuItem value="CircleOfFifths">Circle of Fifths</MenuItem>
                      <MenuItem value="ChromaticDistance">Chromatic Distance</MenuItem>
                      <MenuItem value="SetComplexity">Set Complexity</MenuItem>
                      <MenuItem value="TonalHierarchy">Tonal Hierarchy</MenuItem>
                    </Select>
                  </FormControl>

                  <Button
                    variant="contained"
                    onClick={handleSpatialQuery}
                    disabled={loading || connectionStatus !== 'connected'}
                    startIcon={loading ? <CircularProgress size={20} /> : <Search />}
                    fullWidth
                    sx={{ mb: 2 }}
                  >
                    {loading ? 'Searching...' : 'Perform Spatial Query'}
                  </Button>

                  <Typography variant="subtitle2" gutterBottom sx={{ mt: 2 }}>
                    Quick Examples:
                  </Typography>
                  <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
                    {BSPApiService.getChordExamples().slice(0, 6).map((example) => (
                      <Chip
                        key={example.name}
                        label={example.name}
                        size="small"
                        clickable
                        onClick={() => setSpatialQuery(example.pitchClasses)}
                        variant={spatialQuery === example.pitchClasses ? 'filled' : 'outlined'}
                      />
                    ))}
                  </Box>
                </CardContent>
              </Card>
            </Grid>

            <Grid item xs={12} md={6}>
              {spatialResult && (
                <Card>
                  <CardContent>
                    <Typography variant="h6" gutterBottom>Query Results</Typography>
                    
                    <Box sx={{ mb: 2 }}>
                      <Typography variant="body2" color="text.secondary">
                        Query: <Chip label={spatialResult.queryChord} size="small" />
                      </Typography>
                      <Typography variant="body2" color="text.secondary">
                        Strategy: {spatialResult.strategy}
                      </Typography>
                      <Typography variant="body2" color="text.secondary">
                        Query Time: {spatialResult.queryTimeMs.toFixed(2)}ms
                      </Typography>
                    </Box>

                    <Typography variant="subtitle1" gutterBottom>
                      Tonal Region: {spatialResult.region.name}
                    </Typography>
                    <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                      Type: {spatialResult.region.tonalityType} | 
                      Center: {spatialResult.region.tonalCenter} | 
                      Confidence: {(spatialResult.confidence * 100).toFixed(1)}%
                    </Typography>

                    <LinearProgress 
                      variant="determinate" 
                      value={spatialResult.confidence * 100} 
                      sx={{ mb: 2 }}
                    />

                    <Typography variant="subtitle2" gutterBottom>
                      Found Elements ({spatialResult.elements.length}):
                    </Typography>
                    <List dense>
                      {spatialResult.elements.map((element, index) => (
                        <ListItem key={index}>
                          <ListItemText
                            primary={element.name}
                            secondary={`${element.tonalityType} | ${element.pitchClasses.join(', ')}`}
                          />
                        </ListItem>
                      ))}
                    </List>
                  </CardContent>
                </Card>
              )}
            </Grid>
          </Grid>

          {/* Spatial Visualization */}
          {spatialResult && (
            <Box sx={{ mt: 3 }}>
              <Card>
                <CardContent>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
                    <Typography variant="h6">Spatial Visualization</Typography>
                    <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
                      <Typography variant="body2" color="text.secondary">Mode:</Typography>
                      <Chip
                        label="2D"
                        size="small"
                        clickable
                        color={visualizationMode === '2d' ? 'primary' : 'default'}
                        onClick={() => setVisualizationMode('2d')}
                      />
                      <Chip
                        label="3D"
                        size="small"
                        clickable
                        color={visualizationMode === '3d' ? 'primary' : 'default'}
                        onClick={() => setVisualizationMode('3d')}
                      />
                    </Box>
                  </Box>

                  {visualizationMode === '2d' ? (
                    <BSPSpatialVisualization
                      spatialResult={spatialResult}
                      queryRadius={spatialRadius}
                      onRadiusChange={setSpatialRadius}
                    />
                  ) : (
                    <Box sx={{ height: 600, width: '100%' }}>
                      <ThreeHarmonicNavigator
                        spatialResult={spatialResult}
                        width={800}
                        height={600}
                      />
                    </Box>
                  )}
                </CardContent>
              </Card>
            </Box>
          )}
        </TabPanel>

        <TabPanel value={tabValue} index={1}>
          <Typography variant="h6" gutterBottom>
            Tonal Context Analysis
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
            Analyze the tonal context and regional fit of a chord using BSP classification
          </Typography>

          <Grid container spacing={3}>
            <Grid item xs={12} md={6}>
              <Card>
                <CardContent>
                  <Typography variant="h6" gutterBottom>Chord Analysis</Typography>
                  
                  <TextField
                    fullWidth
                    label="Pitch Classes"
                    value={tonalQuery}
                    onChange={(e) => setTonalQuery(e.target.value)}
                    placeholder="A,C,E"
                    helperText="Comma-separated pitch classes (e.g., A,C,E for A Minor)"
                    sx={{ mb: 3 }}
                  />

                  <Button
                    variant="contained"
                    onClick={handleTonalContext}
                    disabled={loading || connectionStatus !== 'connected'}
                    startIcon={loading ? <CircularProgress size={20} /> : <Analytics />}
                    fullWidth
                    sx={{ mb: 2 }}
                  >
                    {loading ? 'Analyzing...' : 'Analyze Tonal Context'}
                  </Button>

                  <Typography variant="subtitle2" gutterBottom sx={{ mt: 2 }}>
                    Quick Examples:
                  </Typography>
                  <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
                    {BSPApiService.getChordExamples().slice(0, 6).map((example) => (
                      <Chip
                        key={example.name}
                        label={example.name}
                        size="small"
                        clickable
                        onClick={() => setTonalQuery(example.pitchClasses)}
                        variant={tonalQuery === example.pitchClasses ? 'filled' : 'outlined'}
                      />
                    ))}
                  </Box>
                </CardContent>
              </Card>
            </Grid>

            <Grid item xs={12} md={6}>
              {tonalResult && (
                <Card>
                  <CardContent>
                    <Typography variant="h6" gutterBottom>Context Analysis</Typography>
                    
                    <Box sx={{ mb: 2 }}>
                      <Typography variant="body2" color="text.secondary">
                        Query: <Chip label={tonalResult.queryChord} size="small" />
                      </Typography>
                      <Typography variant="body2" color="text.secondary">
                        Query Time: {tonalResult.queryTimeMs.toFixed(2)}ms
                      </Typography>
                    </Box>

                    <Typography variant="subtitle1" gutterBottom>
                      Best Fit Region: {tonalResult.region.name}
                    </Typography>
                    <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                      Type: {tonalResult.region.tonalityType} | 
                      Center: {tonalResult.region.tonalCenter}
                    </Typography>

                    <Typography variant="subtitle2" gutterBottom>
                      Fit Analysis:
                    </Typography>
                    <Box sx={{ mb: 2 }}>
                      <Typography variant="body2">
                        Confidence: {(tonalResult.confidence * 100).toFixed(1)}%
                      </Typography>
                      <LinearProgress 
                        variant="determinate" 
                        value={tonalResult.confidence * 100} 
                        sx={{ mb: 1 }}
                      />
                      
                      {tonalResult.analysis && (
                        <>
                          <Typography variant="body2">
                            Common Tones: {tonalResult.analysis.commonTones} / {tonalResult.analysis.totalTones}
                          </Typography>
                          <Typography variant="body2">
                            Fit Percentage: {tonalResult.analysis.fitPercentage.toFixed(1)}%
                          </Typography>
                          <Typography variant="body2">
                            Fully Contained: {tonalResult.analysis.containedInRegion ? 'Yes' : 'No'}
                          </Typography>
                        </>
                      )}
                    </Box>

                    <Typography variant="subtitle2" gutterBottom>
                      Region Pitch Classes:
                    </Typography>
                    <Box>
                      {tonalResult.region.pitchClasses.map((pc, index) => (
                        <Chip key={index} label={pc} size="small" sx={{ mr: 0.5, mb: 0.5 }} />
                      ))}
                    </Box>
                  </CardContent>
                </Card>
              )}
            </Grid>
          </Grid>
        </TabPanel>

        <TabPanel value={tabValue} index={2}>
          <Typography variant="h6" gutterBottom>
            Chord Progression Analysis
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
            Analyze harmonic relationships and transitions in chord progressions using BSP
          </Typography>

          <Grid container spacing={3}>
            <Grid item xs={12} md={6}>
              <Card>
                <CardContent>
                  <Typography variant="h6" gutterBottom>Chord Progression</Typography>

                  {progression.map((chord, index) => (
                    <Box key={index} sx={{ mb: 2, p: 2, border: '1px solid #e0e0e0', borderRadius: 1 }}>
                      <Grid container spacing={2} alignItems="center">
                        <Grid item xs={4}>
                          <TextField
                            fullWidth
                            size="small"
                            label="Chord Name"
                            value={chord.name}
                            onChange={(e) => updateProgressionChord(index, 'name', e.target.value)}
                            placeholder="C Major"
                          />
                        </Grid>
                        <Grid item xs={6}>
                          <TextField
                            fullWidth
                            size="small"
                            label="Pitch Classes"
                            value={chord.pitchClasses}
                            onChange={(e) => updateProgressionChord(index, 'pitchClasses', e.target.value)}
                            placeholder="C,E,G"
                          />
                        </Grid>
                        <Grid item xs={2}>
                          <Button
                            size="small"
                            color="error"
                            onClick={() => removeChordFromProgression(index)}
                            disabled={progression.length <= 1}
                          >
                            Remove
                          </Button>
                        </Grid>
                      </Grid>
                    </Box>
                  ))}

                  <Box sx={{ mb: 3 }}>
                    <Button
                      variant="outlined"
                      onClick={addChordToProgression}
                      sx={{ mr: 2 }}
                    >
                      Add Chord
                    </Button>
                  </Box>

                  <Typography variant="subtitle2" gutterBottom>
                    Load Example Progressions:
                  </Typography>
                  <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1, mb: 3 }}>
                    {BSPApiService.getProgressionExamples().map((example) => (
                      <Chip
                        key={example.name}
                        label={example.name}
                        size="small"
                        clickable
                        onClick={() => setProgression(example.chords)}
                        variant="outlined"
                      />
                    ))}
                  </Box>

                  <Button
                    variant="contained"
                    onClick={handleProgressionAnalysis}
                    disabled={loading || connectionStatus !== 'connected' || progression.some(c => !c.name || !c.pitchClasses)}
                    startIcon={loading ? <CircularProgress size={20} /> : <Timeline />}
                    fullWidth
                  >
                    {loading ? 'Analyzing...' : 'Analyze Progression'}
                  </Button>
                </CardContent>
              </Card>
            </Grid>

            <Grid item xs={12} md={6}>
              {progressionResult && (
                <Card>
                  <CardContent>
                    <Typography variant="h6" gutterBottom>Progression Analysis</Typography>

                    <Typography variant="subtitle1" gutterBottom>
                      Overall Statistics
                    </Typography>
                    <Box sx={{ mb: 3 }}>
                      <Typography variant="body2">
                        Average Confidence: {(progressionResult.overallAnalysis.averageConfidence * 100).toFixed(1)}%
                      </Typography>
                      <LinearProgress
                        variant="determinate"
                        value={progressionResult.overallAnalysis.averageConfidence * 100}
                        sx={{ mb: 1 }}
                      />
                      <Typography variant="body2">
                        Average Smoothness: {(progressionResult.overallAnalysis.averageSmoothness * 100).toFixed(1)}%
                      </Typography>
                      <LinearProgress
                        variant="determinate"
                        value={progressionResult.overallAnalysis.averageSmoothness * 100}
                        sx={{ mb: 1 }}
                      />
                      <Typography variant="body2">
                        Total Common Tones: {progressionResult.overallAnalysis.totalCommonTones}
                      </Typography>
                    </Box>

                    <Divider sx={{ my: 2 }} />

                    <Typography variant="subtitle1" gutterBottom>
                      Chord Analysis
                    </Typography>
                    <List dense>
                      {progressionResult.chordAnalyses.map((analysis, index) => (
                        <ListItem key={index}>
                          <ListItemText
                            primary={analysis.name}
                            secondary={`Region: ${analysis.region.name} | Confidence: ${(analysis.confidence * 100).toFixed(1)}%`}
                          />
                        </ListItem>
                      ))}
                    </List>

                    <Divider sx={{ my: 2 }} />

                    <Typography variant="subtitle1" gutterBottom>
                      Transitions
                    </Typography>
                    <List dense>
                      {progressionResult.transitions.map((transition, index) => (
                        <ListItem key={index}>
                          <ListItemText
                            primary={`${transition.fromChord} → ${transition.toChord}`}
                            secondary={`Distance: ${transition.distance.toFixed(3)} | Common Tones: ${transition.commonTones} | Smoothness: ${(transition.smoothness * 100).toFixed(1)}%`}
                          />
                        </ListItem>
                      ))}
                    </List>
                  </CardContent>
                </Card>
              )}
            </Grid>
          </Grid>
        </TabPanel>

        <TabPanel value={tabValue} index={3}>
          <Typography variant="h6" gutterBottom>
            BSP System Information
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
            Learn about the Binary Space Partitioning system for musical analysis
          </Typography>

          {/* Performance Metrics Dashboard */}
          <Box sx={{ mb: 4 }}>
            <BSPMetricsDashboard
              connectionStatus={connectionStatus}
              recentQueryTimes={queryTimes}
            />
          </Box>

          <Divider sx={{ my: 4 }} />

          {/* BSP Tree Visualization */}
          <Box sx={{ mb: 4 }}>
            <BSPTreeVisualization />
          </Box>

          <Divider sx={{ my: 4 }} />

          <Grid container spacing={3}>
            <Grid item xs={12} md={6}>
              <Card>
                <CardContent>
                  <Typography variant="h6" gutterBottom>What is BSP?</Typography>
                  <Typography variant="body2" paragraph>
                    Binary Space Partitioning (BSP) is a spatial data structure that recursively 
                    subdivides space using hyperplanes. In musical analysis, we apply this to 
                    organize tonal spaces and enable efficient similarity searches.
                  </Typography>
                  <Typography variant="body2" paragraph>
                    The BSP tree organizes musical elements hierarchically, allowing for:
                  </Typography>
                  <List dense>
                    <ListItem>
                      <ListItemText primary="Fast spatial queries (< 1ms)" />
                    </ListItem>
                    <ListItem>
                      <ListItemText primary="Intelligent chord suggestions" />
                    </ListItem>
                    <ListItem>
                      <ListItemText primary="Harmonic relationship analysis" />
                    </ListItem>
                    <ListItem>
                      <ListItemText primary="Voice leading optimization" />
                    </ListItem>
                  </List>
                </CardContent>
              </Card>
            </Grid>

            <Grid item xs={12} md={6}>
              <Card>
                <CardContent>
                  <Typography variant="h6" gutterBottom>Performance Metrics</Typography>
                  <List dense>
                    <ListItem>
                      <ListItemText 
                        primary="Query Speed" 
                        secondary="Sub-millisecond spatial queries"
                      />
                    </ListItem>
                    <ListItem>
                      <ListItemText 
                        primary="Tree Depth" 
                        secondary="Maximum depth of 2 levels"
                      />
                    </ListItem>
                    <ListItem>
                      <ListItemText 
                        primary="Regions" 
                        secondary="3 total regions (Chromatic, Major, Minor)"
                      />
                    </ListItem>
                    <ListItem>
                      <ListItemText 
                        primary="Strategies" 
                        secondary="4 partition strategies available"
                      />
                    </ListItem>
                  </List>
                </CardContent>
              </Card>
            </Grid>
          </Grid>
        </TabPanel>
      </Paper>
    </Box>
  );
};
