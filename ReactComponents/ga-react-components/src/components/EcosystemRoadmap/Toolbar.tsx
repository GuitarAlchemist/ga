// src/components/EcosystemRoadmap/Toolbar.tsx

import React from 'react';
import { useAtom, useAtomValue } from 'jotai';
import {
  Box,
  Chip,
  IconButton,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from '@mui/material';
import MenuIcon from '@mui/icons-material/Menu';
import ViewListIcon from '@mui/icons-material/ViewList';
import AdjustIcon from '@mui/icons-material/Adjust';
import PublicIcon from '@mui/icons-material/Public';
import ZoomInIcon from '@mui/icons-material/ZoomIn';
import ZoomOutIcon from '@mui/icons-material/ZoomOut';

import { viewModeAtom, zoomLevelAtom, rendererTypeAtom } from './atoms';
import type { ViewMode } from './types';

const ZOOM_MIN = 0.25;
const ZOOM_MAX = 4.0;
const ZOOM_STEP = 0.25;

interface ToolbarProps {
  /** Show the burger menu (mobile drawer trigger). */
  showMenuButton?: boolean;
  /** Called when the burger menu is tapped. */
  onMenuClick?: () => void;
}

export const Toolbar: React.FC<ToolbarProps> = ({
  showMenuButton = false,
  onMenuClick,
}) => {
  const [viewMode, setViewMode] = useAtom(viewModeAtom);
  const [zoomLevel, setZoomLevel] = useAtom(zoomLevelAtom);
  const rendererType = useAtomValue(rendererTypeAtom);

  const handleViewModeChange = (
    _event: React.MouseEvent<HTMLElement>,
    newMode: ViewMode | null,
  ) => {
    // Disallow deselecting all buttons
    if (newMode !== null) {
      setViewMode(newMode);
    }
  };

  const handleZoomIn = () => {
    setZoomLevel((prev) => Math.min(ZOOM_MAX, Math.round((prev + ZOOM_STEP) * 100) / 100));
  };

  const handleZoomOut = () => {
    setZoomLevel((prev) => Math.max(ZOOM_MIN, Math.round((prev - ZOOM_STEP) * 100) / 100));
  };

  const zoomPercent = `${Math.round(zoomLevel * 100)}%`;

  const isWebGPU = rendererType === 'webgpu';

  return (
    <Box
      sx={{
        display: 'flex',
        flexDirection: 'row',
        alignItems: 'center',
        flexWrap: 'wrap',
        rowGap: 0.5,
        columnGap: { xs: 1, sm: 2 },
        px: 1,
        py: 0.5,
        backgroundColor: '#161b22',
        borderBottom: '1px solid #30363d',
        minHeight: 40,
        flexShrink: 0,
      }}
    >
      {showMenuButton && (
        <IconButton
          aria-label="open navigation"
          onClick={onMenuClick}
          size="small"
          sx={{
            color: '#c9d1d9',
            mr: 0.5,
            '&:hover': { color: '#fff', backgroundColor: '#21262d' },
          }}
        >
          <MenuIcon fontSize="small" />
        </IconButton>
      )}

      {/* View Mode Toggle */}
      <ToggleButtonGroup
        value={viewMode}
        exclusive
        onChange={handleViewModeChange}
        size="small"
        aria-label="view mode"
        sx={{
          '& .MuiToggleButton-root': {
            color: '#8b949e',
            borderColor: '#30363d',
            padding: '4px 8px',
            '&.Mui-selected': {
              color: '#c9d1d9',
              backgroundColor: '#21262d',
            },
            '&:hover': {
              backgroundColor: '#21262d',
            },
          },
        }}
      >
        <ToggleButton value="icicle" aria-label="icicle view">
          <ViewListIcon fontSize="small" />
        </ToggleButton>
        <ToggleButton value="disk" aria-label="disk view">
          <AdjustIcon fontSize="small" />
        </ToggleButton>
        <ToggleButton value="ball" aria-label="ball view">
          <PublicIcon fontSize="small" />
        </ToggleButton>
      </ToggleButtonGroup>

      {/* Renderer Chip */}
      <Chip
        label={isWebGPU ? 'WebGPU ✓' : 'WebGL'}
        size="small"
        sx={{
          backgroundColor: 'transparent',
          border: `1px solid ${isWebGPU ? '#238636' : '#9e6a03'}`,
          color: isWebGPU ? '#3fb950' : '#d29922',
          fontFamily: 'monospace',
          fontSize: '0.7rem',
          height: 24,
        }}
      />

      {/* Spacer */}
      <Box sx={{ flexGrow: 1 }} />

      {/* Zoom Controls */}
      <Box
        sx={{
          display: 'flex',
          flexDirection: 'row',
          alignItems: 'center',
          gap: 0.5,
        }}
      >
        <IconButton
          onClick={handleZoomOut}
          disabled={zoomLevel <= ZOOM_MIN}
          size="small"
          aria-label="zoom out"
          sx={{
            color: zoomLevel <= ZOOM_MIN ? '#484f58' : '#8b949e',
            padding: '4px',
            '&:hover': { color: '#c9d1d9', backgroundColor: '#21262d' },
          }}
        >
          <ZoomOutIcon fontSize="small" />
        </IconButton>

        <Typography
          variant="caption"
          sx={{
            color: '#8b949e',
            minWidth: 36,
            textAlign: 'center',
            fontFamily: 'monospace',
            fontSize: '0.75rem',
            userSelect: 'none',
          }}
        >
          {zoomPercent}
        </Typography>

        <IconButton
          onClick={handleZoomIn}
          disabled={zoomLevel >= ZOOM_MAX}
          size="small"
          aria-label="zoom in"
          sx={{
            color: zoomLevel >= ZOOM_MAX ? '#484f58' : '#8b949e',
            padding: '4px',
            '&:hover': { color: '#c9d1d9', backgroundColor: '#21262d' },
          }}
        >
          <ZoomInIcon fontSize="small" />
        </IconButton>
      </Box>
    </Box>
  );
};

export default Toolbar;
