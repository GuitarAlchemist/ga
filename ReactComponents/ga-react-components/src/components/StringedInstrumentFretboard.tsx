// @ts-nocheck
/**
 * Generic Stringed Instrument Fretboard Component
 * 
 * This component can render ANY stringed instrument (guitar, bass, ukulele, banjo, etc.)
 * using different rendering modes (SVG, Canvas, WebGL, WebGPU).
 * 
 * It replaces the need for separate GuitarFretboard, ThreeFretboard, RealisticFretboard, etc.
 */

import React, { useState, useEffect, useRef } from 'react';
import {
  Box,
  Stack,
  Typography,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  FormControlLabel,
  Switch,
  ToggleButtonGroup,
  ToggleButton,
  Paper,
  Chip,
} from '@mui/material';
import type {
  InstrumentConfig,
  StringedInstrumentFretboardProps,
  RenderMode,
} from '../types/InstrumentConfig';
import { getStringCount } from '../types/InstrumentConfig';
import { MinimalThreeInstrument } from './MinimalThree';
import { WebGPUFretboard } from './WebGPUFretboard';
import GuitarFretboard from './GuitarFretboard';
import { RealisticFretboard } from './RealisticFretboard';

/**
 * Main generic fretboard component
 */
export const StringedInstrumentFretboard: React.FC<StringedInstrumentFretboardProps> = ({
  instrument,
  renderMode: initialRenderMode = '3d-webgpu',
  positions = [],
  capoFret: initialCapoFret = 0,
  leftHanded: initialLeftHanded = false,
  options = {},
  onPositionClick,
  onPositionHover,
  onCapoChange,
  onLeftHandedChange,
  title,
  showControls = true,
}) => {
  // State
  const [renderMode, setRenderMode] = useState<RenderMode>(initialRenderMode);
  const [capoFret, setCapoFret] = useState(initialCapoFret);
  const [leftHanded, setLeftHanded] = useState(initialLeftHanded);
  const containerRef = useRef<HTMLDivElement>(null);

  // Derived values
  const stringCount = getStringCount(instrument);
  const maxCapoFret = Math.min(12, instrument.fretCount);

  // Handle capo change
  const handleCapoChange = (fret: number) => {
    setCapoFret(fret);
    onCapoChange?.(fret);
  };

  // Handle left-handed toggle
  const handleLeftHandedChange = (value: boolean) => {
    setLeftHanded(value);
    onLeftHandedChange?.(value);
  };

  // Render the appropriate fretboard component based on renderMode
  const renderFretboard = () => {
    const commonProps = {
      width: options?.width || 1200,
      height: options?.height || 400,
    };

    switch (renderMode) {
      case '3d-webgpu':
      case '3d-webgl':
        return (
          <MinimalThreeInstrument
            instrument={instrument}
            positions={positions}
            renderMode={renderMode}
            capoFret={capoFret}
            leftHanded={leftHanded}
            showLabels={options?.showStringLabels ?? true}
            showInlays={options?.showInlays ?? true}
            enableOrbitControls={options?.enableOrbitControls ?? true}
            onPositionClick={onPositionClick}
            onPositionHover={onPositionHover}
            {...commonProps}
          />
        );

      case '2d-canvas':
        // Use RealisticFretboard for canvas rendering
        return (
          <RealisticFretboard
            positions={positions}
            onPositionClick={onPositionClick}
            onPositionHover={onPositionHover}
            config={{
              capoFret,
              leftHanded,
              guitarModel: instrument.bodyStyle as any,
              ...commonProps,
            }}
          />
        );

      case '2d-svg':
        // Use GuitarFretboard for SVG rendering
        return (
          <GuitarFretboard
            positions={positions}
            onPositionClick={(pos) => {
              onPositionClick?.(pos.string, pos.fret);
            }}
            onPositionHover={(pos: any) => {
              if (pos) {
                onPositionHover?.(pos.string, pos.fret);
              } else {
                onPositionHover?.(null, null);
              }
            }}
            config={{
              width: commonProps.width,
              height: commonProps.height,
            }}
          />
        );

      default:
        return (
          <MinimalThreeInstrument
            instrument={instrument}
            positions={positions}
            renderMode="3d-webgpu"
            capoFret={capoFret}
            leftHanded={leftHanded}
            onPositionClick={onPositionClick}
            onPositionHover={onPositionHover}
            {...commonProps}
          />
        );
    }
  };

  return (
    <Stack spacing={2}>
      {/* Header */}
      <Box display="flex" alignItems="center" gap={2}>
        <Typography variant="h5">
          {title || instrument.displayName}
        </Typography>
        
        <Chip 
          label={`${stringCount} strings`} 
          size="small" 
          color="primary" 
          variant="outlined"
        />
        
        <Chip 
          label={`${instrument.fretCount} frets`} 
          size="small" 
          color="secondary" 
          variant="outlined"
        />
        
        <Chip 
          label={instrument.bodyStyle} 
          size="small" 
          variant="outlined"
        />
      </Box>

      {/* Tuning Display */}
      <Box display="flex" gap={1} flexWrap="wrap">
        <Typography variant="body2" color="text.secondary">
          Tuning:
        </Typography>
        {instrument.tuning.map((pitch, index) => (
          <Chip
            key={index}
            label={`${index + 1}: ${pitch}`}
            size="small"
            variant="outlined"
          />
        ))}
      </Box>

      {/* Controls */}
      {showControls && (
        <Paper elevation={1} sx={{ p: 2 }}>
          <Stack direction="row" spacing={2} flexWrap="wrap" alignItems="center">
            {/* Render Mode Selector */}
            <FormControl sx={{ minWidth: 200 }}>
              <InputLabel id="render-mode-label">Render Mode</InputLabel>
              <Select
                labelId="render-mode-label"
                id="render-mode-select"
                value={renderMode}
                label="Render Mode"
                onChange={(e) => setRenderMode(e.target.value as RenderMode)}
              >
                <MenuItem value="2d-svg">2D SVG (Legacy)</MenuItem>
                <MenuItem value="2d-canvas">2D Canvas (Realistic)</MenuItem>
                <MenuItem value="3d-webgl">3D WebGL</MenuItem>
                <MenuItem value="3d-webgpu">3D WebGPU</MenuItem>
              </Select>
            </FormControl>

            {/* Capo Position Selector */}
            <FormControl sx={{ minWidth: 150 }}>
              <InputLabel id="capo-position-label">Capo Position</InputLabel>
              <Select
                labelId="capo-position-label"
                id="capo-position-select"
                value={capoFret}
                label="Capo Position"
                onChange={(e) => handleCapoChange(Number(e.target.value))}
              >
                <MenuItem value={0}>No Capo</MenuItem>
                {Array.from({ length: maxCapoFret }, (_, i) => i + 1).map((fret) => (
                  <MenuItem key={fret} value={fret}>
                    Fret {fret}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            {/* Left-Handed Toggle */}
            <FormControlLabel
              control={
                <Switch
                  checked={leftHanded}
                  onChange={(e) => handleLeftHandedChange(e.target.checked)}
                  id="left-handed-toggle"
                />
              }
              label="Left-Handed"
            />
          </Stack>
        </Paper>
      )}

      {/* Fretboard Container */}
      <Paper elevation={2} sx={{ p: 2 }}>
        <Box
          ref={containerRef}
          sx={{
            width: '100%',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            bgcolor: 'background.default',
            borderRadius: 1,
          }}
        >
          {renderFretboard()}
        </Box>
      </Paper>

      {/* Info Panel */}
      <Paper elevation={1} sx={{ p: 2, bgcolor: 'background.paper' }}>
        <Stack spacing={1}>
          <Typography variant="subtitle2" color="text.secondary">
            Instrument Details
          </Typography>
          <Box display="grid" gridTemplateColumns="auto 1fr" gap={1}>
            <Typography variant="body2" fontWeight="bold">Scale Length:</Typography>
            <Typography variant="body2">{instrument.scaleLength}mm</Typography>
            
            <Typography variant="body2" fontWeight="bold">Nut Width:</Typography>
            <Typography variant="body2">{instrument.nutWidth}mm</Typography>
            
            <Typography variant="body2" fontWeight="bold">Bridge Width:</Typography>
            <Typography variant="body2">{instrument.bridgeWidth}mm</Typography>
            
            <Typography variant="body2" fontWeight="bold">Body Style:</Typography>
            <Typography variant="body2">{instrument.bodyStyle}</Typography>
            
            {instrument.fullName && (
              <>
                <Typography variant="body2" fontWeight="bold">Full Name:</Typography>
                <Typography variant="body2">{instrument.fullName}</Typography>
              </>
            )}
          </Box>
        </Stack>
      </Paper>
    </Stack>
  );
};

/**
 * Backward compatibility wrapper for ThreeFretboard
 */
export const ThreeFretboardCompat: React.FC<any> = (props) => {
  // Convert old props to new InstrumentConfig format
  const instrument: InstrumentConfig = {
    family: 'Guitar',
    variant: 'Standard',
    displayName: 'Standard Guitar',
    tuning: ['E2', 'A2', 'D3', 'G3', 'B3', 'E4'],
    scaleLength: 650,
    nutWidth: 52,
    bridgeWidth: 70,
    fretCount: 19,
    bodyStyle: props.guitarStyle?.category || 'classical',
  };

  return (
    <StringedInstrumentFretboard
      instrument={instrument}
      renderMode="3d-webgpu"
      capoFret={props.capoFret}
      leftHanded={props.leftHanded}
      positions={props.positions}
      onCapoChange={props.onCapoChange}
      onLeftHandedChange={props.onLeftHandedChange}
    />
  );
};

/**
 * Backward compatibility wrapper for RealisticFretboard
 */
export const RealisticFretboardCompat: React.FC<any> = (props) => {
  const instrument: InstrumentConfig = {
    family: 'Guitar',
    variant: 'Standard',
    displayName: 'Standard Guitar',
    tuning: ['E2', 'A2', 'D3', 'G3', 'B3', 'E4'],
    scaleLength: 650,
    nutWidth: 52,
    bridgeWidth: 70,
    fretCount: 19,
    bodyStyle: props.guitarStyle?.category || 'classical',
  };

  return (
    <StringedInstrumentFretboard
      instrument={instrument}
      renderMode="2d-canvas"
      capoFret={props.capoFret}
      leftHanded={props.leftHanded}
      positions={props.positions}
      onCapoChange={props.onCapoChange}
      onLeftHandedChange={props.onLeftHandedChange}
    />
  );
};

export default StringedInstrumentFretboard;
