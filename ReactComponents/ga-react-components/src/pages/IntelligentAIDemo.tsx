/**
 * Intelligent AI Demo Application
 * 
 * Complete demo showcasing ALL AI features:
 * 1. Intelligent BSP Level Generation
 * 2. Adaptive Difficulty System
 * 3. Style Learning
 * 4. Pattern Recognition
 * 5. Real-time Performance Tracking
 * 
 * This is a fully functional demo that demonstrates the integration
 * of all 9 advanced mathematical techniques in a practical application.
 */

import React, { useState, useEffect } from 'react';
import {
  Box,
  Container,
  Typography,
  Button,
  Paper,
  Tabs,
  Tab,
  Grid,
  TextField,
  Alert,
  CircularProgress,
  Snackbar,
} from '@mui/material';
import {
  IntelligentBSPVisualizer,
  AdaptiveAIDashboard,
  aiApiService,
  IntelligentBSPLevel,
  PlayerStats,
  PlayerStyleProfile,
  RecognizedPattern,
  PerformanceHistoryPoint,
} from '../components/AI';

// ==================
// Types
// ==================

interface TabPanelProps {
  children?: React.ReactNode;
  index: number;
  value: number;
}

// ==================
// Components
// ==================

const TabPanel: React.FC<TabPanelProps> = ({ children, value, index }) => {
  return (
    <div role="tabpanel" hidden={value !== index}>
      {value === index && <Box sx={{ p: 3 }}>{children}</Box>}
    </div>
  );
};

// ==================
// Main Demo Component
// ==================

export const IntelligentAIDemo: React.FC = () => {
  // State
  const [tabValue, setTabValue] = useState(0);
  const [playerId, setPlayerId] = useState('demo-player-1');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  // BSP Level State
  const [bspLevel, setBspLevel] = useState<IntelligentBSPLevel | null>(null);
  const [pitchClassSets, setPitchClassSets] = useState<string[]>([
    '047', '037', '048', '0258', '0268', '0369', '0148', '0158',
  ]);

  // Adaptive AI State
  const [playerStats, setPlayerStats] = useState<PlayerStats>({
    totalAttempts: 0,
    successRate: 0,
    averageTime: 0,
    currentDifficulty: 0.5,
    learningRate: 0,
    currentAttractor: null,
  });
  const [performanceHistory, setPerformanceHistory] = useState<PerformanceHistoryPoint[]>([]);

  // Style Learning State
  const [styleProfile, setStyleProfile] = useState<PlayerStyleProfile>({
    preferredComplexity: 0.5,
    explorationRate: 0.5,
    topChordFamilies: {},
    favoriteProgressionCount: 0,
    totalProgressionsAnalyzed: 0,
  });
  const [patterns, setPatterns] = useState<RecognizedPattern[]>([]);

  // ==================
  // Effects
  // ==================

  useEffect(() => {
    // Load initial data
    loadPlayerData();
  }, [playerId]);

  // ==================
  // Handlers
  // ==================

  const loadPlayerData = async () => {
    try {
      setLoading(true);
      const [stats, profile, patternsData] = await Promise.all([
        aiApiService.getPlayerStats(playerId).catch(() => playerStats),
        aiApiService.getStyleProfile(playerId).catch(() => styleProfile),
        aiApiService.getPatterns(playerId, 10).catch(() => []),
      ]);
      setPlayerStats(stats);
      setStyleProfile(profile);
      setPatterns(patternsData);
    } catch (err: any) {
      console.error('Error loading player data:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleGenerateLevel = async () => {
    try {
      setLoading(true);
      setError(null);
      
      const level = await aiApiService.generateLevel(pitchClassSets, undefined, {
        chordFamilyCount: 5,
        landmarkCount: 10,
        bridgeChordCount: 5,
        learningPathLength: 8,
      });
      
      setBspLevel(level);
      setSuccess('✅ Intelligent BSP level generated successfully!');
    } catch (err: any) {
      setError(err.message || 'Failed to generate level');
    } finally {
      setLoading(false);
    }
  };

  const handleSimulatePerformance = async () => {
    try {
      setLoading(true);
      setError(null);
      
      // Simulate a performance
      const success = Math.random() > 0.3;
      const timeMs = 2000 + Math.random() * 5000;
      const attempts = Math.floor(1 + Math.random() * 3);
      const shapeId = pitchClassSets[Math.floor(Math.random() * pitchClassSets.length)];
      
      const stats = await aiApiService.recordPerformance(
        playerId,
        success,
        timeMs,
        attempts,
        shapeId
      );
      
      setPlayerStats(stats);
      
      // Add to history
      setPerformanceHistory(prev => [
        ...prev,
        {
          timestamp: Date.now(),
          success,
          timeMs,
          difficulty: stats.currentDifficulty,
        },
      ]);
      
      setSuccess(`✅ Performance recorded: ${success ? 'Success' : 'Failure'} in ${timeMs.toFixed(0)}ms`);
    } catch (err: any) {
      setError(err.message || 'Failed to record performance');
    } finally {
      setLoading(false);
    }
  };

  const handleGenerateChallenge = async () => {
    try {
      setLoading(true);
      setError(null);
      
      const recentProgression = pitchClassSets.slice(0, 4);
      const challenge = await aiApiService.generateChallenge(
        playerId,
        pitchClassSets,
        recentProgression
      );
      
      setSuccess(`✅ Adaptive challenge generated: ${challenge.shapeIds.length} shapes, quality=${challenge.quality.toFixed(2)}`);
    } catch (err: any) {
      setError(err.message || 'Failed to generate challenge');
    } finally {
      setLoading(false);
    }
  };

  const handleLearnStyle = async () => {
    try {
      setLoading(true);
      setError(null);
      
      const progression = pitchClassSets.slice(0, 6);
      const profile = await aiApiService.learnStyle(
        playerId,
        pitchClassSets,
        progression
      );
      
      setStyleProfile(profile);
      
      // Refresh patterns
      const patternsData = await aiApiService.getPatterns(playerId, 10);
      setPatterns(patternsData);
      
      setSuccess('✅ Style learned successfully!');
    } catch (err: any) {
      setError(err.message || 'Failed to learn style');
    } finally {
      setLoading(false);
    }
  };

  const handleResetSession = async () => {
    try {
      setLoading(true);
      setError(null);
      
      await aiApiService.resetSession(playerId);
      await loadPlayerData();
      setPerformanceHistory([]);
      
      setSuccess('✅ Session reset successfully!');
    } catch (err: any) {
      setError(err.message || 'Failed to reset session');
    } finally {
      setLoading(false);
    }
  };

  // ==================
  // Render
  // ==================

  return (
    <Container maxWidth="xl" sx={{ py: 4 }}>
      {/* Header */}
      <Paper sx={{ p: 3, mb: 3, background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)' }}>
        <Typography variant="h3" sx={{ color: 'white', fontWeight: 'bold' }}>
          🧠 Intelligent AI Demo
        </Typography>
        <Typography variant="h6" sx={{ color: 'rgba(255,255,255,0.9)', mt: 1 }}>
          Showcasing ALL 9 Advanced Mathematical Techniques
        </Typography>
      </Paper>

      {/* Player ID */}
      <Paper sx={{ p: 2, mb: 3 }}>
        <Grid container spacing={2} alignItems="center">
          <Grid item xs={12} md={6}>
            <TextField
              fullWidth
              label="Player ID"
              value={playerId}
              onChange={(e) => setPlayerId(e.target.value)}
              variant="outlined"
            />
          </Grid>
          <Grid item xs={12} md={6}>
            <Button
              variant="outlined"
              color="error"
              onClick={handleResetSession}
              disabled={loading}
              fullWidth
            >
              Reset Session
            </Button>
          </Grid>
        </Grid>
      </Paper>

      {/* Tabs */}
      <Paper sx={{ mb: 3 }}>
        <Tabs value={tabValue} onChange={(_, newValue) => setTabValue(newValue)}>
          <Tab label="🏢 Intelligent BSP" />
          <Tab label="🤖 Adaptive AI" />
          <Tab label="🎨 Style Learning" />
          <Tab label="🔍 Pattern Recognition" />
        </Tabs>
      </Paper>

      {/* Tab Panels */}
      <TabPanel value={tabValue} index={0}>
        <Paper sx={{ p: 3 }}>
          <Typography variant="h5" gutterBottom>
            🏢 Intelligent BSP Level Generation
          </Typography>
          <Typography variant="body1" color="textSecondary" paragraph>
            Uses ALL 9 mathematical techniques to create musically-aware BSP levels with floors,
            landmarks, portals, safe zones, and challenge paths.
          </Typography>
          
          <Button
            variant="contained"
            size="large"
            onClick={handleGenerateLevel}
            disabled={loading}
            sx={{ mb: 3 }}
          >
            {loading ? <CircularProgress size={24} /> : 'Generate Intelligent Level'}
          </Button>
          
          {bspLevel && (
            <IntelligentBSPVisualizer
              level={bspLevel}
              width={1200}
              height={800}
              showFloors
              showLandmarks
              showPortals
              showSafeZones
              showChallengePaths
              showLearningPath
              animateLearningPath
            />
          )}
        </Paper>
      </TabPanel>

      <TabPanel value={tabValue} index={1}>
        <Paper sx={{ p: 3 }}>
          <Typography variant="h5" gutterBottom>
            🤖 Adaptive Difficulty System
          </Typography>
          <Typography variant="body1" color="textSecondary" paragraph>
            Real-time adaptation based on player performance using information theory and dynamical systems.
          </Typography>
          
          <Grid container spacing={2} sx={{ mb: 3 }}>
            <Grid item>
              <Button
                variant="contained"
                onClick={handleSimulatePerformance}
                disabled={loading}
              >
                Simulate Performance
              </Button>
            </Grid>
            <Grid item>
              <Button
                variant="contained"
                color="secondary"
                onClick={handleGenerateChallenge}
                disabled={loading}
              >
                Generate Adaptive Challenge
              </Button>
            </Grid>
          </Grid>
          
          <AdaptiveAIDashboard
            playerId={playerId}
            stats={playerStats}
            styleProfile={styleProfile}
            patterns={patterns}
            performanceHistory={performanceHistory}
          />
        </Paper>
      </TabPanel>

      <TabPanel value={tabValue} index={2}>
        <Paper sx={{ p: 3 }}>
          <Typography variant="h5" gutterBottom>
            🎨 Style Learning System
          </Typography>
          <Typography variant="body1" color="textSecondary" paragraph>
            Learns player's musical style preferences using spectral analysis and information theory.
          </Typography>
          
          <Button
            variant="contained"
            size="large"
            onClick={handleLearnStyle}
            disabled={loading}
            sx={{ mb: 3 }}
          >
            {loading ? <CircularProgress size={24} /> : 'Learn from Progression'}
          </Button>
          
          <Alert severity="info" sx={{ mt: 2 }}>
            Style profile is displayed in the Adaptive AI tab
          </Alert>
        </Paper>
      </TabPanel>

      <TabPanel value={tabValue} index={3}>
        <Paper sx={{ p: 3 }}>
          <Typography variant="h5" gutterBottom>
            🔍 Pattern Recognition System
          </Typography>
          <Typography variant="body1" color="textSecondary" paragraph>
            Detects recurring patterns using Markov chains and sequence mining.
          </Typography>
          
          <Alert severity="info">
            Patterns are displayed in the Adaptive AI tab. Play more progressions to see patterns emerge!
          </Alert>
        </Paper>
      </TabPanel>

      {/* Snackbars */}
      <Snackbar
        open={!!error}
        autoHideDuration={6000}
        onClose={() => setError(null)}
      >
        <Alert severity="error" onClose={() => setError(null)}>
          {error}
        </Alert>
      </Snackbar>

      <Snackbar
        open={!!success}
        autoHideDuration={3000}
        onClose={() => setSuccess(null)}
      >
        <Alert severity="success" onClose={() => setSuccess(null)}>
          {success}
        </Alert>
      </Snackbar>
    </Container>
  );
};

export default IntelligentAIDemo;

