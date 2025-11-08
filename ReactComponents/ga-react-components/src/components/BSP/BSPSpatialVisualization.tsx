import React, { useRef, useEffect, useState } from 'react';
import {
  Box,
  Card,
  CardContent,
  Typography,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Slider,
  Switch,
  FormControlLabel,
  Tooltip
} from '@mui/material';
import { BSPSpatialQueryResponse, BSPElement } from './BSPApiService';

interface BSPSpatialVisualizationProps {
  spatialResult: BSPSpatialQueryResponse | null;
  queryRadius: number;
  onRadiusChange: (radius: number) => void;
}

interface ChordPoint {
  x: number;
  y: number;
  element: BSPElement;
  isQuery: boolean;
  distance?: number;
}

export const BSPSpatialVisualization: React.FC<BSPSpatialVisualizationProps> = ({
  spatialResult,
  queryRadius,
  onRadiusChange
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [viewMode, setViewMode] = useState<'2d' | 'circle'>('2d');
  const [showLabels, setShowLabels] = useState(true);
  const [showRadius, setShowRadius] = useState(true);

  // Convert pitch classes to coordinates for visualization
  const pitchClassToAngle = (pitchClass: string): number => {
    const pitchMap: { [key: string]: number } = {
      'C': 0, 'CSharp': 1, 'D': 2, 'DSharp': 3, 'E': 4, 'F': 5,
      'FSharp': 6, 'G': 7, 'GSharp': 8, 'A': 9, 'ASharp': 10, 'B': 11
    };
    return (pitchMap[pitchClass] || 0) * (Math.PI * 2 / 12);
  };

  const calculateChordPosition = (element: BSPElement, centerX: number, centerY: number): ChordPoint => {
    if (viewMode === 'circle') {
      // Circle of fifths layout
      const angle = pitchClassToAngle(element.pitchClasses[0] || 'C');
      const radius = 80;
      return {
        x: centerX + Math.cos(angle) * radius,
        y: centerY + Math.sin(angle) * radius,
        element,
        isQuery: false
      };
    } else {
      // 2D spatial layout based on tonal center and complexity
      const tonalCenter = element.tonalCenter || 0;
      const complexity = element.pitchClasses.length;
      return {
        x: centerX + (tonalCenter - 6) * 20,
        y: centerY + (complexity - 3) * 30,
        element,
        isQuery: false
      };
    }
  };

  const drawVisualization = () => {
    const canvas = canvasRef.current;
    if (!canvas || !spatialResult) return;

    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const width = canvas.width;
    const height = canvas.height;
    const centerX = width / 2;
    const centerY = height / 2;

    // Clear canvas
    ctx.clearRect(0, 0, width, height);

    // Set up styles
    ctx.font = '12px Arial';
    ctx.textAlign = 'center';

    // Draw background grid
    ctx.strokeStyle = '#f0f0f0';
    ctx.lineWidth = 1;
    for (let i = 0; i < width; i += 40) {
      ctx.beginPath();
      ctx.moveTo(i, 0);
      ctx.lineTo(i, height);
      ctx.stroke();
    }
    for (let i = 0; i < height; i += 40) {
      ctx.beginPath();
      ctx.moveTo(0, i);
      ctx.lineTo(width, i);
      ctx.stroke();
    }

    // Calculate query chord position (center)
    const queryPoint: ChordPoint = {
      x: centerX,
      y: centerY,
      element: {
        name: spatialResult.queryChord,
        tonalityType: spatialResult.region.tonalityType,
        tonalCenter: spatialResult.region.tonalCenter,
        pitchClasses: spatialResult.queryChord.split(',')
      },
      isQuery: true
    };

    // Calculate positions for all elements
    const points: ChordPoint[] = [queryPoint];
    spatialResult.elements.forEach(element => {
      const point = calculateChordPosition(element, centerX, centerY);
      point.distance = Math.sqrt(
        Math.pow(point.x - queryPoint.x, 2) + Math.pow(point.y - queryPoint.y, 2)
      ) / 40; // Normalize distance
      points.push(point);
    });

    // Draw search radius circle
    if (showRadius) {
      ctx.strokeStyle = '#2196F3';
      ctx.lineWidth = 2;
      ctx.setLineDash([5, 5]);
      ctx.beginPath();
      ctx.arc(centerX, centerY, queryRadius * 40, 0, Math.PI * 2);
      ctx.stroke();
      ctx.setLineDash([]);
    }

    // Draw connections between query and results
    ctx.strokeStyle = '#e0e0e0';
    ctx.lineWidth = 1;
    points.slice(1).forEach(point => {
      if (point.distance && point.distance <= queryRadius) {
        ctx.beginPath();
        ctx.moveTo(queryPoint.x, queryPoint.y);
        ctx.lineTo(point.x, point.y);
        ctx.stroke();
      }
    });

    // Draw chord points
    points.forEach(point => {
      const isInRadius = point.isQuery || (point.distance && point.distance <= queryRadius);

      // Draw chord circle
      ctx.fillStyle = point.isQuery
        ? '#f44336'
        : isInRadius
        ? '#4caf50'
        : '#9e9e9e';

      ctx.beginPath();
      ctx.arc(point.x, point.y, point.isQuery ? 12 : 8, 0, Math.PI * 2);
      ctx.fill();

      // Draw border
      ctx.strokeStyle = '#fff';
      ctx.lineWidth = 2;
      ctx.stroke();

      // Draw labels
      if (showLabels) {
        ctx.fillStyle = '#333';
        ctx.fillText(
          point.element.name,
          point.x,
          point.y + (point.isQuery ? 25 : 20)
        );

        if (!point.isQuery && point.distance) {
          ctx.fillStyle = '#666';
          ctx.font = '10px Arial';
          ctx.fillText(
            `d: ${point.distance.toFixed(2)}`,
            point.x,
            point.y + (point.isQuery ? 35 : 30)
          );
          ctx.font = '12px Arial';
        }
      }
    });

    // Draw legend
    ctx.fillStyle = '#333';
    ctx.textAlign = 'left';
    ctx.font = '12px Arial';
    ctx.fillText('Legend:', 10, 20);

    // Query chord
    ctx.fillStyle = '#f44336';
    ctx.beginPath();
    ctx.arc(20, 35, 6, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = '#333';
    ctx.fillText('Query Chord', 35, 40);

    // Similar chords
    ctx.fillStyle = '#4caf50';
    ctx.beginPath();
    ctx.arc(20, 55, 6, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = '#333';
    ctx.fillText('Similar Chords', 35, 60);

    // Out of range
    ctx.fillStyle = '#9e9e9e';
    ctx.beginPath();
    ctx.arc(20, 75, 6, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = '#333';
    ctx.fillText('Out of Range', 35, 80);
  };

  useEffect(() => {
    drawVisualization();
  }, [spatialResult, queryRadius, viewMode, showLabels, showRadius]);

  // Handle canvas resize
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const resizeCanvas = () => {
      const container = canvas.parentElement;
      if (container) {
        canvas.width = container.clientWidth;
        canvas.height = 400;
        drawVisualization();
      }
    };

    resizeCanvas();
    window.addEventListener('resize', resizeCanvas);
    return () => window.removeEventListener('resize', resizeCanvas);
  }, []);

  return (
    <Card>
      <CardContent>
        <Typography variant="h6" gutterBottom>
          Spatial Relationship Visualization
        </Typography>

        {/* Controls */}
        <Box sx={{ display: 'flex', gap: 2, mb: 2, flexWrap: 'wrap', alignItems: 'center' } as any}>
          <FormControl size="small" sx={{ minWidth: 120 }}>
            <InputLabel>View Mode</InputLabel>
            <Select
              value={viewMode}
              onChange={(e) => setViewMode(e.target.value as '2d' | 'circle')}
              label="View Mode"
            >
              <MenuItem value="2d">2D Spatial</MenuItem>
              <MenuItem value="circle">Circle of Fifths</MenuItem>
            </Select>
          </FormControl>

          <FormControlLabel
            control={
              <Switch
                checked={showLabels}
                onChange={(e) => setShowLabels(e.target.checked)}
                size="small"
              />
            }
            label="Show Labels"
          />

          <FormControlLabel
            control={
              <Switch
                checked={showRadius}
                onChange={(e) => setShowRadius(e.target.checked)}
                size="small"
              />
            }
            label="Show Radius"
          />

          <Box sx={{ minWidth: 200 }}>
            <Typography variant="body2" gutterBottom>
              Search Radius: {queryRadius}
            </Typography>
            <Slider
              value={queryRadius}
              onChange={(_, value) => onRadiusChange(value as number)}
              min={0.1}
              max={2.0}
              step={0.1}
              size="small"
            />
          </Box>
        </Box>

        {/* Canvas */}
        <Box sx={{ border: '1px solid #e0e0e0', borderRadius: 1, overflow: 'hidden' }}>
          <canvas
            ref={canvasRef}
            style={{ display: 'block', width: '100%', height: '400px' }}
          />
        </Box>

        {spatialResult && (
          <Box sx={{ mt: 2 }}>
            <Typography variant="body2" color="text.secondary">
              Showing {spatialResult.elements.length} elements for query "{spatialResult.queryChord}"
              with radius {spatialResult.radius} using {spatialResult.strategy} strategy.
              Query time: {spatialResult.queryTimeMs.toFixed(2)}ms
            </Typography>
          </Box>
        )}

        {!spatialResult && (
          <Box sx={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            height: 200,
            color: 'text.secondary'
          }}>
            <Typography variant="body1">
              Perform a spatial query to see the visualization
            </Typography>
          </Box>
        )}
      </CardContent>
    </Card>
  );
};
