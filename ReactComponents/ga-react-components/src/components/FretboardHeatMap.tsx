/**
 * FretboardHeatMap Component
 * 
 * Visualizes probability heat maps on a fretboard using the Grothendieck Monoid
 * Markov walker. Shows which chord shapes are most likely to be visited during
 * a random walk through the shape graph.
 */

import React, { useEffect, useRef, useState } from 'react';
import * as THREE from 'three';
import { getGrothendieckService, HeatMapEntry } from '../services/GrothendieckService';

export interface FretboardHeatMapProps {
  /** Starting pitch class set (e.g., [0, 4, 7] for C major) */
  startPitchClassSet: number[];
  
  /** Guitar tuning (e.g., [40, 45, 50, 55, 59, 64] for standard tuning) */
  tuning: number[];
  
  /** Number of steps in the Markov walk */
  steps?: number;
  
  /** Temperature for softmax (higher = more random) */
  temperature?: number;
  
  /** Maximum fret to consider */
  maxFret?: number;
  
  /** Width of the canvas */
  width?: number;
  
  /** Height of the canvas */
  height?: number;
  
  /** Color scheme for heat map */
  colorScheme?: 'hot' | 'cool' | 'viridis';
  
  /** Show probability values as text */
  showValues?: boolean;
  
  /** Callback when heat map is generated */
  onHeatMapGenerated?: (heatMap: HeatMapEntry[]) => void;
}

/**
 * FretboardHeatMap component
 */
export const FretboardHeatMap: React.FC<FretboardHeatMapProps> = ({
  startPitchClassSet,
  tuning,
  steps = 1000,
  temperature = 1.0,
  maxFret = 12,
  width = 800,
  height = 400,
  colorScheme = 'hot',
  showValues = false,
  onHeatMapGenerated
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [heatMap, setHeatMap] = useState<HeatMapEntry[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    generateHeatMap();
  }, [startPitchClassSet, tuning, steps, temperature, maxFret]);

  useEffect(() => {
    if (heatMap.length > 0) {
      renderHeatMap();
    }
  }, [heatMap, width, height, colorScheme, showValues]);

  const generateHeatMap = async () => {
    setLoading(true);
    setError(null);

    try {
      const service = getGrothendieckService();
      const result = await service.generateHeatMap(
        startPitchClassSet,
        tuning,
        steps,
        temperature,
        maxFret
      );
      
      setHeatMap(result);
      
      if (onHeatMapGenerated) {
        onHeatMapGenerated(result);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to generate heat map');
      console.error('Heat map generation error:', err);
    } finally {
      setLoading(false);
    }
  };

  const renderHeatMap = () => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    // Clear canvas
    ctx.clearRect(0, 0, width, height);

    // Calculate fretboard dimensions
    const numStrings = tuning.length;
    const numFrets = maxFret + 1;
    const fretWidth = width / numFrets;
    const stringHeight = height / numStrings;

    // Draw fretboard background
    ctx.fillStyle = '#2a1810';
    ctx.fillRect(0, 0, width, height);

    // Draw fret lines
    ctx.strokeStyle = '#888';
    ctx.lineWidth = 1;
    for (let fret = 0; fret <= numFrets; fret++) {
      const x = fret * fretWidth;
      ctx.beginPath();
      ctx.moveTo(x, 0);
      ctx.lineTo(x, height);
      ctx.stroke();
    }

    // Draw string lines
    for (let string = 0; string <= numStrings; string++) {
      const y = string * stringHeight;
      ctx.beginPath();
      ctx.moveTo(0, y);
      ctx.lineTo(width, y);
      ctx.stroke();
    }

    // Find max probability for normalization
    const maxProbability = Math.max(...heatMap.map(entry => entry.probability));

    // Draw heat map
    heatMap.forEach(entry => {
      const normalizedProb = entry.probability / maxProbability;
      const color = getHeatMapColor(normalizedProb, colorScheme);

      // For each pitch class in the set, find all fretboard positions
      entry.pitchClassSet.forEach(pitchClass => {
        tuning.forEach((openString, stringIndex) => {
          for (let fret = 0; fret <= maxFret; fret++) {
            const notePitch = openString + fret;
            const notePitchClass = notePitch % 12;

            if (notePitchClass === pitchClass) {
              // Draw heat map circle
              const x = (fret + 0.5) * fretWidth;
              const y = (stringIndex + 0.5) * stringHeight;
              const radius = Math.min(fretWidth, stringHeight) * 0.3;

              ctx.fillStyle = color;
              ctx.globalAlpha = 0.7;
              ctx.beginPath();
              ctx.arc(x, y, radius, 0, Math.PI * 2);
              ctx.fill();
              ctx.globalAlpha = 1.0;

              // Draw probability value
              if (showValues) {
                ctx.fillStyle = '#fff';
                ctx.font = '10px monospace';
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                ctx.fillText(
                  (entry.probability * 100).toFixed(1) + '%',
                  x,
                  y
                );
              }
            }
          }
        });
      });
    });

    // Draw fret markers
    const fretMarkers = [3, 5, 7, 9, 12];
    ctx.fillStyle = '#ccc';
    fretMarkers.forEach(fret => {
      if (fret <= maxFret) {
        const x = (fret - 0.5) * fretWidth;
        const y = height / 2;
        const radius = 5;
        ctx.beginPath();
        ctx.arc(x, y, radius, 0, Math.PI * 2);
        ctx.fill();
      }
    });
  };

  const getHeatMapColor = (value: number, scheme: string): string => {
    // Clamp value between 0 and 1
    const v = Math.max(0, Math.min(1, value));

    switch (scheme) {
      case 'hot':
        // Red -> Yellow -> White
        if (v < 0.5) {
          const r = 255;
          const g = Math.floor(v * 2 * 255);
          const b = 0;
          return `rgb(${r}, ${g}, ${b})`;
        } else {
          const r = 255;
          const g = 255;
          const b = Math.floor((v - 0.5) * 2 * 255);
          return `rgb(${r}, ${g}, ${b})`;
        }

      case 'cool':
        // Blue -> Cyan -> White
        if (v < 0.5) {
          const r = 0;
          const g = Math.floor(v * 2 * 255);
          const b = 255;
          return `rgb(${r}, ${g}, ${b})`;
        } else {
          const r = Math.floor((v - 0.5) * 2 * 255);
          const g = 255;
          const b = 255;
          return `rgb(${r}, ${g}, ${b})`;
        }

      case 'viridis':
        // Viridis color scheme approximation
        const r = Math.floor(68 + v * (253 - 68));
        const g = Math.floor(1 + v * (231 - 1));
        const b = Math.floor(84 + v * (37 - 84));
        return `rgb(${r}, ${g}, ${b})`;

      default:
        return `rgb(${Math.floor(v * 255)}, 0, 0)`;
    }
  };

  return (
    <div style={{ position: 'relative', width, height }}>
      <canvas
        ref={canvasRef}
        width={width}
        height={height}
        style={{
          border: '1px solid #333',
          borderRadius: '4px',
          backgroundColor: '#000'
        }}
      />
      
      {loading && (
        <div
          style={{
            position: 'absolute',
            top: '50%',
            left: '50%',
            transform: 'translate(-50%, -50%)',
            backgroundColor: 'rgba(0, 0, 0, 0.8)',
            color: '#fff',
            padding: '20px',
            borderRadius: '8px',
            fontSize: '16px'
          }}
        >
          Generating heat map...
        </div>
      )}
      
      {error && (
        <div
          style={{
            position: 'absolute',
            top: '10px',
            left: '10px',
            right: '10px',
            backgroundColor: 'rgba(255, 0, 0, 0.8)',
            color: '#fff',
            padding: '10px',
            borderRadius: '4px',
            fontSize: '14px'
          }}
        >
          Error: {error}
        </div>
      )}
      
      {!loading && !error && heatMap.length > 0 && (
        <div
          style={{
            position: 'absolute',
            bottom: '10px',
            right: '10px',
            backgroundColor: 'rgba(0, 0, 0, 0.7)',
            color: '#fff',
            padding: '8px 12px',
            borderRadius: '4px',
            fontSize: '12px'
          }}
        >
          {heatMap.length} shapes | {steps} steps
        </div>
      )}
    </div>
  );
};

export default FretboardHeatMap;

