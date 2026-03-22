// src/components/EcosystemRoadmap/EcosystemRoadmapExplorer.tsx

import React, { useCallback, useEffect, useState } from 'react';
import { Provider, useSetAtom } from 'jotai';
import { Box } from '@mui/material';

import { NavigationPanel } from './NavigationPanel';
import { DetailPanel } from './DetailPanel';
import { StatsBar } from './StatsBar';
import { Toolbar } from './Toolbar';
import VisualizationCanvas from './VisualizationCanvas';
import { selectedNodeAtom, expandedTreeNodesAtom, panelWidthAtom } from './atoms';
import { getAncestors } from './roadmapData';
import type { RoadmapNode } from './types';

// ---------------------------------------------------------------------------
// Inner component — must be inside Provider to use atoms
// ---------------------------------------------------------------------------
const EcosystemRoadmapExplorerInner: React.FC = () => {
  const setSelectedNode = useSetAtom(selectedNodeAtom);
  const setExpandedNodes = useSetAtom(expandedTreeNodesAtom);
  const setPanelWidth = useSetAtom(panelWidthAtom);

  const [isDragging, setIsDragging] = useState(false);
  const [hoveredNode, setHoveredNode] = useState<RoadmapNode | null>(null);

  // Handle node selection from the tree (NavigationPanel already sets selectedNodeAtom)
  const handleNodeSelect = useCallback((_node: RoadmapNode) => {
    // NavigationPanel already calls setSelectedNode internally; nothing extra needed
  }, []);

  // Handle node click from the visualization canvas
  const handleNodeClick = useCallback(
    (node: RoadmapNode) => {
      setSelectedNode(node);
      // Expand tree to reveal all ancestors of the clicked node
      const ancestors = getAncestors(node);
      const ancestorIds = ancestors.map((a) => a.id);
      setExpandedNodes((prev) => {
        const merged = new Set([...prev, ...ancestorIds]);
        return Array.from(merged);
      });
    },
    [setSelectedNode, setExpandedNodes],
  );

  // Handle hover from the visualization canvas (local state only)
  const handleNodeHover = useCallback((node: RoadmapNode | null) => {
    setHoveredNode(node);
  }, []);

  // Drag divider: track mousemove/mouseup on window while dragging
  useEffect(() => {
    if (!isDragging) return;

    const onMouseMove = (e: MouseEvent) => {
      const clamped = Math.min(500, Math.max(200, e.clientX));
      setPanelWidth(clamped);
    };

    const onMouseUp = () => {
      setIsDragging(false);
    };

    window.addEventListener('mousemove', onMouseMove);
    window.addEventListener('mouseup', onMouseUp);

    return () => {
      window.removeEventListener('mousemove', onMouseMove);
      window.removeEventListener('mouseup', onMouseUp);
    };
  }, [isDragging, setPanelWidth]);

  return (
    <Box
      display="flex"
      height="100%"
      sx={{
        bgcolor: '#0d1117',
        cursor: isDragging ? 'col-resize' : (hoveredNode ? 'pointer' : 'default'),
      }}
    >
      {/* Left: Navigation panel */}
      <NavigationPanel onNodeSelect={handleNodeSelect} />

      {/* Drag divider */}
      <Box
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

      {/* Right: main area */}
      <Box flex={1} display="flex" flexDirection="column" minWidth={0}>
        <Toolbar />
        <Box flex={1} position="relative" sx={{ overflow: 'hidden' }}>
          <VisualizationCanvas
            onNodeClick={handleNodeClick}
            onNodeHover={handleNodeHover}
          />
        </Box>
        <DetailPanel />
        <StatsBar />
      </Box>
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
