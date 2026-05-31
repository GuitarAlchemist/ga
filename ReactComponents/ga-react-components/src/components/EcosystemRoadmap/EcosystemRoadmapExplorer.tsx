// src/components/EcosystemRoadmap/EcosystemRoadmapExplorer.tsx

import React, { useCallback, useEffect, useState } from 'react';
import { Provider, useAtom, useSetAtom } from 'jotai';
import {
  Box,
  Drawer,
  useMediaQuery,
  useTheme,
} from '@mui/material';

import { NavigationPanel } from './NavigationPanel';
import { DetailPanel } from './DetailPanel';
import { StatsBar } from './StatsBar';
import { Toolbar } from './Toolbar';
import VisualizationCanvas from './VisualizationCanvas';
import {
  selectedNodeAtom,
  expandedTreeNodesAtom,
  panelWidthAtom,
  navDrawerOpenAtom,
} from './atoms';
import { getAncestors } from './roadmapData';
import type { RoadmapNode } from './types';

const MIN_PANEL_WIDTH = 220;
const MAX_PANEL_WIDTH = 480;

// ---------------------------------------------------------------------------
// Inner component — must be inside Provider to use atoms
// ---------------------------------------------------------------------------
const EcosystemRoadmapExplorerInner: React.FC = () => {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));

  const setSelectedNode = useSetAtom(selectedNodeAtom);
  const setExpandedNodes = useSetAtom(expandedTreeNodesAtom);
  const [panelWidth, setPanelWidth] = useAtom(panelWidthAtom);
  const [drawerOpen, setDrawerOpen] = useAtom(navDrawerOpenAtom);

  const [isDragging, setIsDragging] = useState(false);
  const [hoveredNode, setHoveredNode] = useState<RoadmapNode | null>(null);

  const handleNodeSelect = useCallback(
    (_node: RoadmapNode) => {
      // NavigationPanel already calls setSelectedNode internally.
      // Close the drawer on mobile so the user sees the visualization.
      if (isMobile) setDrawerOpen(false);
    },
    [isMobile, setDrawerOpen],
  );

  // Handle node click from the visualization canvas
  const handleNodeClick = useCallback(
    (node: RoadmapNode) => {
      setSelectedNode(node);
      // Expand tree to reveal all ancestors of the clicked node
      const ancestors = getAncestors(node);
      const ancestorIds = ancestors.map((a) => a.id);
      setExpandedNodes((prev) => {
        const merged = new Set([...prev, ...ancestorIds, node.id]);
        return Array.from(merged);
      });
    },
    [setSelectedNode, setExpandedNodes],
  );

  // Handle hover from the visualization canvas (local state only)
  const handleNodeHover = useCallback((node: RoadmapNode | null) => {
    setHoveredNode(node);
  }, []);

  // Drag divider: track mousemove/mouseup on window while dragging.
  // Desktop only — mobile uses the drawer instead.
  useEffect(() => {
    if (!isDragging || isMobile) return;

    const onMouseMove = (e: MouseEvent) => {
      const clamped = Math.min(MAX_PANEL_WIDTH, Math.max(MIN_PANEL_WIDTH, e.clientX));
      setPanelWidth(clamped);
    };

    const onMouseUp = () => setIsDragging(false);

    window.addEventListener('mousemove', onMouseMove);
    window.addEventListener('mouseup', onMouseUp);

    return () => {
      window.removeEventListener('mousemove', onMouseMove);
      window.removeEventListener('mouseup', onMouseUp);
    };
  }, [isDragging, isMobile, setPanelWidth]);

  // Close the drawer automatically when crossing the desktop breakpoint up.
  useEffect(() => {
    if (!isMobile && drawerOpen) setDrawerOpen(false);
  }, [isMobile, drawerOpen, setDrawerOpen]);

  const treePanel = (
    <Box
      sx={{
        width: isMobile ? '85vw' : panelWidth,
        maxWidth: isMobile ? 360 : MAX_PANEL_WIDTH,
        minWidth: MIN_PANEL_WIDTH,
        height: '100%',
        borderRight: isMobile ? 'none' : '1px solid #30363d',
      }}
    >
      <NavigationPanel onNodeSelect={handleNodeSelect} />
    </Box>
  );

  return (
    <Box
      display="flex"
      height="100%"
      sx={{
        bgcolor: '#0d1117',
        cursor: isDragging ? 'col-resize' : hoveredNode ? 'pointer' : 'default',
      }}
    >
      {/* Left: tree panel — inline on desktop, drawer on mobile */}
      {!isMobile && treePanel}

      {/* Drag divider — desktop only */}
      {!isMobile && (
        <Box
          aria-label="resize navigation panel"
          role="separator"
          sx={{
            width: 4,
            flexShrink: 0,
            bgcolor: '#30363d',
            cursor: 'col-resize',
            transition: 'background-color 0.15s',
            '&:hover': { bgcolor: '#58a6ff' },
            userSelect: 'none',
          }}
          onMouseDown={() => setIsDragging(true)}
        />
      )}

      {/* Right: main area */}
      <Box flex={1} display="flex" flexDirection="column" minWidth={0}>
        <Toolbar
          showMenuButton={isMobile}
          onMenuClick={() => setDrawerOpen(true)}
        />
        <Box flex={1} position="relative" sx={{ overflow: 'hidden', minHeight: 0 }}>
          <VisualizationCanvas
            onNodeClick={handleNodeClick}
            onNodeHover={handleNodeHover}
          />
        </Box>
        <DetailPanel compact={isMobile} />
        <StatsBar />
      </Box>

      {/* Mobile drawer */}
      <Drawer
        anchor="left"
        open={isMobile && drawerOpen}
        onClose={() => setDrawerOpen(false)}
        PaperProps={{
          sx: {
            bgcolor: '#161b22',
            backgroundImage: 'none',
            borderRight: '1px solid #30363d',
          },
        }}
      >
        {treePanel}
      </Drawer>
    </Box>
  );
};

// ---------------------------------------------------------------------------
// Public export — wrapped in Jotai Provider to scope atoms to this instance
// ---------------------------------------------------------------------------
export const EcosystemRoadmapExplorer: React.FC = () => {
  return (
    <Provider>
      <EcosystemRoadmapExplorerInner />
    </Provider>
  );
};

