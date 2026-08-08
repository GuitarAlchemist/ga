/**
 * Ocean Test Page — TSL + WebGPU
 *
 * Tessendorf-style ocean simulation with Gerstner waves, physical sky,
 * Fresnel reflections, Beer-Lambert absorption, and concentrated sun specular.
 */

import React from 'react';
import { Box, Typography, Paper, Chip } from '@mui/material';
import { Ocean } from '../components/Ocean';
import CastButton from '../components/Common/CastButton';
import { DemoErrorBoundary } from '../components/Common/DemoErrorBoundary';

// Simple 2D compass to keep the viewer oriented over the horizon-less ocean.
const OceanCompass: React.FC = () => (
  <svg width={88} height={88} viewBox="0 0 88 88">
    <circle cx={44} cy={44} r={40} fill="rgba(0,0,0,0.65)" stroke="rgba(0,229,255,0.3)" strokeWidth={1} />
    <line x1={44} y1={8} x2={44} y2={80} stroke="rgba(0,229,255,0.35)" strokeWidth={1} />
    <line x1={8} y1={44} x2={80} y2={44} stroke="rgba(0,229,255,0.35)" strokeWidth={1} />
    <text x={44} y={18} textAnchor="middle" fill="#ff6b6b" fontSize="11" fontFamily="monospace" fontWeight={700}>
      N
    </text>
    <text x={78} y={48} textAnchor="middle" fill="#a0c0d0" fontSize="11" fontFamily="monospace">
      E
    </text>
    <text x={44} y={80} textAnchor="middle" fill="#a0c0d0" fontSize="11" fontFamily="monospace">
      S
    </text>
    <text x={10} y={48} textAnchor="middle" fill="#a0c0d0" fontSize="11" fontFamily="monospace">
      W
    </text>
    <path d="M44 22 L36 40 L44 36 L52 40 Z" fill="#00e5ff" opacity={0.9} />
    <text x={44} y={58} textAnchor="middle" fill="#00e5ff" fontSize="8" fontFamily="monospace">
      waves
    </text>
  </svg>
);

const FEATURES = [
  { label: '8-Wave Gerstner', detail: 'Choppy horizontal displacement', color: '#00e5ff' },
  { label: 'Fresnel Reflections', detail: 'Schlick (F0 = 0.02, IOR 1.33)', color: '#ffab00' },
  { label: 'Beer-Lambert', detail: 'Depth absorption (turquoise → navy)', color: '#aa00ff' },
  { label: 'Sun Specular', detail: 'Blinn-Phong pow(720) × 210', color: '#ff6d00' },
  { label: 'Slope Foam', detail: 'Jacobian approximation at crests', color: '#76ff03' },
  { label: 'Distance Fog', detail: 'FogExp2 horizon blend', color: '#80d8ff' },
] as const;

const OceanTest: React.FC = () => (
  <DemoErrorBoundary demoName="TSL Ocean">
    <Box sx={{ width: '100%', height: '100vh', display: 'flex', flexDirection: 'column', bgcolor: '#0a0a0a', position: 'relative' }}>
      <CastButton />
      {/* Compact header */}
      <Paper
        elevation={0}
        sx={{
          px: 2, py: 1.5,
          bgcolor: 'rgba(0,0,0,0.85)',
          color: '#e0f0ff',
          borderRadius: 0,
          borderBottom: '1px solid rgba(0,229,255,0.3)',
          display: 'flex',
          alignItems: 'center',
          gap: 2,
          flexWrap: 'wrap',
        }}
      >
        <Typography variant="h6" sx={{ fontFamily: 'monospace', fontWeight: 700, whiteSpace: 'nowrap' }}>
          TSL OCEAN
        </Typography>
        <Chip label="WebGPU" size="small" sx={{ bgcolor: 'rgba(0,229,255,0.15)', color: '#00e5ff', fontFamily: 'monospace', fontSize: '0.7rem' }} />
        <Chip label="Phase 0 · Gerstner" size="small" sx={{ bgcolor: 'rgba(118,255,3,0.12)', color: '#76ff03', fontFamily: 'monospace', fontSize: '0.7rem' }} />

        <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', ml: 'auto' }}>
          {FEATURES.map(f => (
            <Typography key={f.label} variant="caption" sx={{ color: f.color, fontFamily: 'monospace', fontSize: '0.65rem' }} title={f.detail}>
              {f.label}
            </Typography>
          ))}
        </Box>
      </Paper>

      {/* 3D viewport */}
      <Box sx={{ flex: 1, position: 'relative', overflow: 'hidden' }}>
        <Ocean />

        {/* Bottom-left HUD */}
        <Box
          sx={{
            position: 'absolute',
            bottom: 12,
            left: 12,
            bgcolor: 'rgba(0,0,0,0.65)',
            backdropFilter: 'blur(6px)',
            color: '#b0d0e0',
            px: 1.5,
            py: 1,
            borderRadius: 1,
            border: '1px solid rgba(0,229,255,0.2)',
            fontFamily: 'monospace',
            fontSize: '0.7rem',
            lineHeight: 1.6,
            maxWidth: 280,
          }}
        >
          <div>4000 × 4000 m &middot; 512 × 512 vertices</div>
          <div>Waves: 60m 40m 22m 15m 8m 5m 3m 2m</div>
          <div>Sun: 78° elev · Specular: pow(720) × 210</div>
          <div style={{ color: '#556070', marginTop: 4 }}>
            Drag: orbit &middot; Scroll: zoom &middot; RMB: pan
          </div>
        </Box>

        {/* Bottom-right compass */}
        <Box
          sx={{
            position: 'absolute',
            bottom: 12,
            right: 12,
            bgcolor: 'rgba(0,0,0,0.45)',
            backdropFilter: 'blur(6px)',
            borderRadius: '50%',
            border: '1px solid rgba(0,229,255,0.2)',
            p: 0.5,
            pointerEvents: 'none',
          }}
        >
          <OceanCompass />
        </Box>
      </Box>
    </Box>
  </DemoErrorBoundary>
);

export default OceanTest;
