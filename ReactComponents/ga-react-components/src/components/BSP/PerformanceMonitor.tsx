/**
 * Performance Monitor Component
 * 
 * Real-time performance monitoring for BSP DOOM Explorer:
 * - FPS counter with history graph
 * - Frame time graph
 * - Draw call counter
 * - Triangle count
 * - Memory usage
 * - Visible/culled object counts
 * - GPU utilization (if available)
 * 
 * Styled with CRT aesthetic to match BSP Explorer theme
 */

import React, { useEffect, useState, useRef } from 'react';
import { Box, Typography, Paper, Stack, LinearProgress } from '@mui/material';
import { PerformanceStats } from './LODManager';

export interface PerformanceMonitorProps {
  stats: PerformanceStats;
  position?: 'top-left' | 'top-right' | 'bottom-left' | 'bottom-right';
  width?: number;
  height?: number;
  showGraphs?: boolean;
  updateInterval?: number; // ms
}

export const PerformanceMonitor: React.FC<PerformanceMonitorProps> = ({
  stats,
  position = 'top-right',
  width = 300,
  height = 400,
  showGraphs = true,
  updateInterval = 100,
}) => {
  const [fpsHistory, setFpsHistory] = useState<number[]>([]);
  const [frameTimeHistory, setFrameTimeHistory] = useState<number[]>([]);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const maxHistoryLength = 100;

  // Update history
  useEffect(() => {
    const interval = setInterval(() => {
      setFpsHistory(prev => [...prev.slice(-maxHistoryLength + 1), stats.fps]);
      setFrameTimeHistory(prev => [...prev.slice(-maxHistoryLength + 1), stats.frameTime]);
    }, updateInterval);

    return () => clearInterval(interval);
  }, [stats.fps, stats.frameTime, updateInterval]);

  // Draw graphs
  useEffect(() => {
    if (!showGraphs || !canvasRef.current) return;

    const canvas = canvasRef.current;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    // Clear canvas
    ctx.fillStyle = 'rgba(0, 0, 0, 0.8)';
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    // Draw FPS graph
    const graphHeight = canvas.height / 2 - 10;
    const graphWidth = canvas.width - 20;
    const graphX = 10;
    const graphY1 = 10;
    const graphY2 = canvas.height / 2 + 10;

    // FPS graph
    ctx.strokeStyle = '#0f0';
    ctx.lineWidth = 2;
    ctx.beginPath();
    fpsHistory.forEach((fps, i) => {
      const x = graphX + (i / maxHistoryLength) * graphWidth;
      const y = graphY1 + graphHeight - (fps / 120) * graphHeight; // Max 120 FPS
      if (i === 0) {
        ctx.moveTo(x, y);
      } else {
        ctx.lineTo(x, y);
      }
    });
    ctx.stroke();

    // FPS target line (60 FPS)
    ctx.strokeStyle = '#0f04';
    ctx.lineWidth = 1;
    ctx.setLineDash([5, 5]);
    const targetY = graphY1 + graphHeight - (60 / 120) * graphHeight;
    ctx.beginPath();
    ctx.moveTo(graphX, targetY);
    ctx.lineTo(graphX + graphWidth, targetY);
    ctx.stroke();
    ctx.setLineDash([]);

    // Frame time graph
    ctx.strokeStyle = '#ff0';
    ctx.lineWidth = 2;
    ctx.beginPath();
    frameTimeHistory.forEach((frameTime, i) => {
      const x = graphX + (i / maxHistoryLength) * graphWidth;
      const y = graphY2 + graphHeight - (frameTime / 50) * graphHeight; // Max 50ms
      if (i === 0) {
        ctx.moveTo(x, y);
      } else {
        ctx.lineTo(x, y);
      }
    });
    ctx.stroke();

    // Frame time target line (16.67ms = 60 FPS)
    ctx.strokeStyle = '#ff04';
    ctx.lineWidth = 1;
    ctx.setLineDash([5, 5]);
    const targetFrameTimeY = graphY2 + graphHeight - (16.67 / 50) * graphHeight;
    ctx.beginPath();
    ctx.moveTo(graphX, targetFrameTimeY);
    ctx.lineTo(graphX + graphWidth, targetFrameTimeY);
    ctx.stroke();
    ctx.setLineDash([]);

    // Labels
    ctx.fillStyle = '#0f0';
    ctx.font = '12px monospace';
    ctx.fillText('FPS', graphX, graphY1 + 15);
    ctx.fillText('Frame Time (ms)', graphX, graphY2 + 15);

  }, [fpsHistory, frameTimeHistory, showGraphs]);

  // Position styles
  const positionStyles = {
    'top-left': { top: 10, left: 10 },
    'top-right': { top: 10, right: 10 },
    'bottom-left': { bottom: 10, left: 10 },
    'bottom-right': { bottom: 10, right: 10 },
  };

  // Format numbers
  const formatNumber = (num: number) => num.toLocaleString();
  const formatMemory = (bytes: number) => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
    return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
  };

  // FPS color based on performance
  const getFpsColor = (fps: number) => {
    if (fps >= 55) return '#0f0'; // Green
    if (fps >= 30) return '#ff0'; // Yellow
    return '#f00'; // Red
  };

  return (
    <Paper
      sx={{
        position: 'absolute',
        ...positionStyles[position],
        width,
        height,
        backgroundColor: 'rgba(0, 20, 0, 0.9)',
        border: '2px solid #0f0',
        borderRadius: '4px',
        padding: 2,
        fontFamily: 'monospace',
        color: '#0f0',
        boxShadow: '0 0 20px rgba(0, 255, 0, 0.3)',
        overflow: 'auto',
        zIndex: 1000,
        // CRT scanline effect
        backgroundImage: 'repeating-linear-gradient(0deg, rgba(0, 0, 0, 0.15), rgba(0, 0, 0, 0.15) 1px, transparent 1px, transparent 2px)',
      }}
    >
      <Stack spacing={1}>
        {/* Title */}
        <Typography
          variant="h6"
          sx={{
            color: '#0f0',
            fontFamily: 'monospace',
            textAlign: 'center',
            textShadow: '0 0 10px #0f0',
            borderBottom: '1px solid #0f0',
            paddingBottom: 1,
          }}
        >
          ⚡ PERFORMANCE MONITOR
        </Typography>

        {/* FPS */}
        <Box>
          <Typography variant="body2" sx={{ color: getFpsColor(stats.fps), fontWeight: 'bold' }}>
            FPS: {stats.fps}
          </Typography>
          <LinearProgress
            variant="determinate"
            value={Math.min(100, (stats.fps / 60) * 100)}
            sx={{
              height: 8,
              backgroundColor: 'rgba(0, 255, 0, 0.1)',
              '& .MuiLinearProgress-bar': {
                backgroundColor: getFpsColor(stats.fps),
              },
            }}
          />
        </Box>

        {/* Frame Time */}
        <Box>
          <Typography variant="body2">
            Frame Time: {stats.frameTime.toFixed(2)} ms
          </Typography>
          <LinearProgress
            variant="determinate"
            value={Math.min(100, (stats.frameTime / 33.33) * 100)} // 33.33ms = 30 FPS
            sx={{
              height: 8,
              backgroundColor: 'rgba(0, 255, 0, 0.1)',
              '& .MuiLinearProgress-bar': {
                backgroundColor: stats.frameTime <= 16.67 ? '#0f0' : stats.frameTime <= 33.33 ? '#ff0' : '#f00',
              },
            }}
          />
        </Box>

        {/* Rendering Stats */}
        <Box sx={{ borderTop: '1px solid #0f04', paddingTop: 1 }}>
          <Typography variant="body2">Draw Calls: {formatNumber(stats.drawCalls)}</Typography>
          <Typography variant="body2">Triangles: {formatNumber(stats.triangles)}</Typography>
        </Box>

        {/* Object Stats */}
        <Box sx={{ borderTop: '1px solid #0f04', paddingTop: 1 }}>
          <Typography variant="body2">
            Visible: {formatNumber(stats.visibleObjects)} / {formatNumber(stats.totalObjects)}
          </Typography>
          <Typography variant="body2">Culled: {formatNumber(stats.culledObjects)}</Typography>
          <LinearProgress
            variant="determinate"
            value={(stats.visibleObjects / Math.max(1, stats.totalObjects)) * 100}
            sx={{
              height: 8,
              backgroundColor: 'rgba(0, 255, 0, 0.1)',
              '& .MuiLinearProgress-bar': {
                backgroundColor: '#0f0',
              },
            }}
          />
        </Box>

        {/* Memory */}
        <Box sx={{ borderTop: '1px solid #0f04', paddingTop: 1 }}>
          <Typography variant="body2">Memory: {formatMemory(stats.memoryUsage)}</Typography>
        </Box>

        {/* Graphs */}
        {showGraphs && (
          <Box sx={{ borderTop: '1px solid #0f04', paddingTop: 1 }}>
            <canvas
              ref={canvasRef}
              width={width - 32}
              height={200}
              style={{
                width: '100%',
                height: 'auto',
                border: '1px solid #0f04',
                borderRadius: '2px',
              }}
            />
          </Box>
        )}

        {/* Performance Tips */}
        {stats.fps < 30 && (
          <Box
            sx={{
              borderTop: '1px solid #f004',
              paddingTop: 1,
              color: '#f00',
            }}
          >
            <Typography variant="caption">
              ⚠️ Low FPS detected!
            </Typography>
            <Typography variant="caption" display="block">
              • Reduce visible objects
            </Typography>
            <Typography variant="caption" display="block">
              • Enable frustum culling
            </Typography>
            <Typography variant="caption" display="block">
              • Increase LOD distances
            </Typography>
          </Box>
        )}
      </Stack>
    </Paper>
  );
};

