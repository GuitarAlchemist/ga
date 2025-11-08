/**
 * Adaptive AI Dashboard
 * 
 * Real-time dashboard showing:
 * - Player statistics (success rate, learning rate, difficulty)
 * - Performance history graphs
 * - Style profile visualization
 * - Pattern recognition results
 * - Adaptive challenge suggestions
 * 
 * Uses Material-UI for UI components and Recharts for graphs
 */

import React, { useState, useEffect } from 'react';
import {
  Box,
  Paper,
  Typography,
  Grid,
  Card,
  CardContent,
  LinearProgress,
  Chip,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
} from '@mui/material';
import {
  LineChart,
  Line,
  BarChart,
  Bar,
  PieChart,
  Pie,
  Cell,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer,
} from 'recharts';

// ==================
// Types
// ==================

export interface PlayerStats {
  totalAttempts: number;
  successRate: number;
  averageTime: number;
  currentDifficulty: number;
  learningRate: number;
  currentAttractor: string | null;
}

export interface PlayerStyleProfile {
  preferredComplexity: number;
  explorationRate: number;
  topChordFamilies: Record<string, number>;
  favoriteProgressionCount: number;
  totalProgressionsAnalyzed: number;
}

export interface RecognizedPattern {
  pattern: string;
  frequency: number;
  probability: number;
}

export interface AdaptiveAIDashboardProps {
  playerId: string;
  stats: PlayerStats;
  styleProfile: PlayerStyleProfile;
  patterns: RecognizedPattern[];
  performanceHistory?: PerformanceHistoryPoint[];
}

export interface PerformanceHistoryPoint {
  timestamp: number;
  success: boolean;
  timeMs: number;
  difficulty: number;
}

// ==================
// Sub-Components
// ==================

const StatsCard: React.FC<{ title: string; value: string | number; subtitle?: string; color?: string }> = ({
  title,
  value,
  subtitle,
  color = 'primary',
}) => {
  return (
    <Card>
      <CardContent>
        <Typography color="textSecondary" gutterBottom>
          {title}
        </Typography>
        <Typography variant="h4" component="div" color={color}>
          {value}
        </Typography>
        {subtitle && (
          <Typography variant="body2" color="textSecondary">
            {subtitle}
          </Typography>
        )}
      </CardContent>
    </Card>
  );
};

const DifficultyGauge: React.FC<{ difficulty: number }> = ({ difficulty }) => {
  const getColor = (value: number) => {
    if (value < 0.3) return '#4caf50'; // Green (easy)
    if (value < 0.7) return '#ff9800'; // Orange (medium)
    return '#f44336'; // Red (hard)
  };
  
  return (
    <Box>
      <Typography variant="body2" gutterBottom>
        Current Difficulty: {(difficulty * 100).toFixed(0)}%
      </Typography>
      <LinearProgress
        variant="determinate"
        value={difficulty * 100}
        sx={{
          height: 10,
          borderRadius: 5,
          backgroundColor: 'rgba(0,0,0,0.1)',
          '& .MuiLinearProgress-bar': {
            backgroundColor: getColor(difficulty),
          },
        }}
      />
    </Box>
  );
};

const LearningRateGauge: React.FC<{ learningRate: number }> = ({ learningRate }) => {
  const getLevel = (value: number) => {
    if (value < 0.3) return 'Beginner';
    if (value < 0.7) return 'Intermediate';
    return 'Advanced';
  };
  
  return (
    <Box>
      <Typography variant="body2" gutterBottom>
        Learning Rate: {(learningRate * 100).toFixed(0)}% ({getLevel(learningRate)})
      </Typography>
      <LinearProgress
        variant="determinate"
        value={learningRate * 100}
        sx={{
          height: 10,
          borderRadius: 5,
          backgroundColor: 'rgba(0,0,0,0.1)',
          '& .MuiLinearProgress-bar': {
            backgroundColor: '#2196f3',
          },
        }}
      />
    </Box>
  );
};

const PerformanceChart: React.FC<{ history: PerformanceHistoryPoint[] }> = ({ history }) => {
  const data = history.map((point, index) => ({
    index,
    timeMs: point.timeMs,
    difficulty: point.difficulty * 100,
    success: point.success ? 1 : 0,
  }));
  
  return (
    <ResponsiveContainer width="100%" height={300}>
      <LineChart data={data}>
        <CartesianGrid strokeDasharray="3 3" />
        <XAxis dataKey="index" label={{ value: 'Attempt', position: 'insideBottom', offset: -5 }} />
        <YAxis yAxisId="left" label={{ value: 'Time (ms)', angle: -90, position: 'insideLeft' }} />
        <YAxis yAxisId="right" orientation="right" label={{ value: 'Difficulty (%)', angle: 90, position: 'insideRight' }} />
        <Tooltip />
        <Legend />
        <Line yAxisId="left" type="monotone" dataKey="timeMs" stroke="#8884d8" name="Time (ms)" />
        <Line yAxisId="right" type="monotone" dataKey="difficulty" stroke="#82ca9d" name="Difficulty (%)" />
      </LineChart>
    </ResponsiveContainer>
  );
};

type ChordFamilyDatum = { name: string; value: number };

const StyleProfileChart: React.FC<{ profile: PlayerStyleProfile }> = ({ profile }) => {
  const data: ChordFamilyDatum[] = Object.entries(profile.topChordFamilies).map(([family, count]) => ({
    name: family,
    value: count,
  }));
  
  const COLORS = ['#0088FE', '#00C49F', '#FFBB28', '#FF8042', '#8884D8'];
  
  return (
    <ResponsiveContainer width="100%" height={300}>
      <PieChart>
        <Pie
          data={data}
          cx="50%"
          cy="50%"
          labelLine={false}
          label={(entry: ChordFamilyDatum) => entry.name}
          outerRadius={80}
          fill="#8884d8"
          dataKey="value"
        >
          {data.map((entry: ChordFamilyDatum, index) => (
            <Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />
          ))}
        </Pie>
        <Tooltip />
      </PieChart>
    </ResponsiveContainer>
  );
};

const PatternTable: React.FC<{ patterns: RecognizedPattern[] }> = ({ patterns }) => {
  return (
    <TableContainer>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Pattern</TableCell>
            <TableCell align="right">Frequency</TableCell>
            <TableCell align="right">Probability</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {patterns.slice(0, 5).map((pattern, index) => (
            <TableRow key={index}>
              <TableCell component="th" scope="row">
                {pattern.pattern}
              </TableCell>
              <TableCell align="right">{pattern.frequency}</TableCell>
              <TableCell align="right">{(pattern.probability * 100).toFixed(1)}%</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
};

// ==================
// Main Component
// ==================

export const AdaptiveAIDashboard: React.FC<AdaptiveAIDashboardProps> = ({
  playerId,
  stats,
  styleProfile,
  patterns,
  performanceHistory = [],
}) => {
  return (
    <Box sx={{ p: 3 }}>
      {/* Header */}
      <Typography variant="h4" gutterBottom>
        🤖 Adaptive AI Dashboard
      </Typography>
      <Typography variant="subtitle1" color="textSecondary" gutterBottom>
        Player: {playerId}
      </Typography>
      
      {/* Stats Cards */}
      <Grid container spacing={3} sx={{ mb: 3 }}>
        <Grid item xs={12} sm={6} md={3}>
          <StatsCard
            title="Total Attempts"
            value={stats.totalAttempts}
            subtitle="Practice sessions"
          />
        </Grid>
        <Grid item xs={12} sm={6} md={3}>
          <StatsCard
            title="Success Rate"
            value={`${(stats.successRate * 100).toFixed(1)}%`}
            subtitle="Overall performance"
            color={stats.successRate > 0.7 ? 'success' : stats.successRate > 0.4 ? 'warning' : 'error'}
          />
        </Grid>
        <Grid item xs={12} sm={6} md={3}>
          <StatsCard
            title="Average Time"
            value={`${stats.averageTime.toFixed(0)}ms`}
            subtitle="Per attempt"
          />
        </Grid>
        <Grid item xs={12} sm={6} md={3}>
          <StatsCard
            title="Current Attractor"
            value={stats.currentAttractor || 'None'}
            subtitle="Comfort zone"
          />
        </Grid>
      </Grid>
      
      {/* Difficulty and Learning Rate */}
      <Grid container spacing={3} sx={{ mb: 3 }}>
        <Grid item xs={12} md={6}>
          <Paper sx={{ p: 2 }}>
            <DifficultyGauge difficulty={stats.currentDifficulty} />
          </Paper>
        </Grid>
        <Grid item xs={12} md={6}>
          <Paper sx={{ p: 2 }}>
            <LearningRateGauge learningRate={stats.learningRate} />
          </Paper>
        </Grid>
      </Grid>
      
      {/* Performance History */}
      {performanceHistory.length > 0 && (
        <Paper sx={{ p: 2, mb: 3 }}>
          <Typography variant="h6" gutterBottom>
            📈 Performance History
          </Typography>
          <PerformanceChart history={performanceHistory} />
        </Paper>
      )}
      
      {/* Style Profile */}
      <Grid container spacing={3} sx={{ mb: 3 }}>
        <Grid item xs={12} md={6}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="h6" gutterBottom>
              🎨 Style Profile
            </Typography>
            <Stack spacing={2}>
              <Box>
                <Typography variant="body2">
                  Preferred Complexity: {(styleProfile.preferredComplexity * 100).toFixed(0)}%
                </Typography>
                <LinearProgress
                  variant="determinate"
                  value={styleProfile.preferredComplexity * 100}
                  sx={{ mt: 1 }}
                />
              </Box>
              <Box>
                <Typography variant="body2">
                  Exploration Rate: {(styleProfile.explorationRate * 100).toFixed(0)}%
                </Typography>
                <LinearProgress
                  variant="determinate"
                  value={styleProfile.explorationRate * 100}
                  sx={{ mt: 1 }}
                />
              </Box>
              <Typography variant="body2">
                Favorite Progressions: {styleProfile.favoriteProgressionCount}
              </Typography>
              <Typography variant="body2">
                Total Analyzed: {styleProfile.totalProgressionsAnalyzed}
              </Typography>
            </Stack>
          </Paper>
        </Grid>
        <Grid item xs={12} md={6}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="h6" gutterBottom>
              🎵 Chord Family Preferences
            </Typography>
            <StyleProfileChart profile={styleProfile} />
          </Paper>
        </Grid>
      </Grid>
      
      {/* Recognized Patterns */}
      <Paper sx={{ p: 2 }}>
        <Typography variant="h6" gutterBottom>
          🔍 Recognized Patterns
        </Typography>
        <PatternTable patterns={patterns} />
      </Paper>
    </Box>
  );
};
