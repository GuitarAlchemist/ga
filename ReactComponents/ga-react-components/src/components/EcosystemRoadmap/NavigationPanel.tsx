// src/components/EcosystemRoadmap/NavigationPanel.tsx

import React, { useMemo } from 'react';
import { SimpleTreeView, TreeItem } from '@mui/x-tree-view';
import { TextField, InputAdornment, Box, Typography } from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import { useAtom, useAtomValue, useSetAtom } from 'jotai';
import { selectedNodeAtom, expandedTreeNodesAtom, searchFilterAtom, panelWidthAtom } from './atoms';
import { ROADMAP_TREE, searchTree } from './roadmapData';
import type { RoadmapNode } from './types';

interface NavigationPanelProps {
  onNodeSelect?: (node: RoadmapNode) => void;
}

export const NavigationPanel: React.FC<NavigationPanelProps> = ({ onNodeSelect }) => {
  const [expanded, setExpanded] = useAtom(expandedTreeNodesAtom);
  const setSelectedNode = useSetAtom(selectedNodeAtom);
  const [filter, setFilter] = useAtom(searchFilterAtom);
  const width = useAtomValue(panelWidthAtom);

  const visibleIds = useMemo(
    () => (filter ? searchTree(ROADMAP_TREE, filter) : null),
    [filter]
  );

  const handleSelect = (_event: React.SyntheticEvent, itemId: string | null) => {
    if (!itemId) return;
    const node = findNodeById(ROADMAP_TREE, itemId);
    if (node) {
      setSelectedNode(node);
      onNodeSelect?.(node);
    }
  };

  const renderTree = (node: RoadmapNode): React.ReactNode => {
    if (visibleIds && !visibleIds.has(node.id)) return null;
    return (
      <TreeItem
        key={node.id}
        itemId={node.id}
        label={
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, py: 0.25 }}>
            <Box sx={{
              width: 8, height: 8, borderRadius: '50%',
              bgcolor: node.color, flexShrink: 0,
            }} />
            <Typography variant="body2" noWrap>{node.name}</Typography>
            {node.sub && (
              <Typography variant="caption" sx={{ color: '#8b949e', ml: 0.5 }}>
                {node.sub}
              </Typography>
            )}
          </Box>
        }
      >
        {node.children?.map(renderTree)}
      </TreeItem>
    );
  };

  return (
    <Box sx={{ width, minWidth: 200, maxWidth: 500, overflow: 'auto', bgcolor: '#161b22',
               borderRight: '1px solid #30363d', height: '100%' }}>
      <Box sx={{ p: 1 }}>
        <TextField
          fullWidth size="small" placeholder="Search..."
          value={filter} onChange={(e) => setFilter(e.target.value)}
          InputProps={{
            startAdornment: <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment>,
          }}
          sx={{ '& .MuiOutlinedInput-root': { bgcolor: '#0d1117' } }}
        />
      </Box>
      <SimpleTreeView
        expandedItems={expanded}
        onExpandedItemsChange={(_e, ids) => setExpanded(ids)}
        onSelectedItemsChange={handleSelect}
      >
        {renderTree(ROADMAP_TREE)}
      </SimpleTreeView>
    </Box>
  );
};

function findNodeById(node: RoadmapNode, id: string): RoadmapNode | null {
  if (node.id === id) return node;
  for (const child of node.children ?? []) {
    const found = findNodeById(child, id);
    if (found) return found;
  }
  return null;
}
