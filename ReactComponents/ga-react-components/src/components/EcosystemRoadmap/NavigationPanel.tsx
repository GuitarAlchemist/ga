// src/components/EcosystemRoadmap/NavigationPanel.tsx

import React, { useEffect, useMemo } from 'react';
import { SimpleTreeView, TreeItem } from '@mui/x-tree-view';
import {
  TextField,
  InputAdornment,
  Box,
  Typography,
  IconButton,
} from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import ClearIcon from '@mui/icons-material/Clear';
import { useAtom, useSetAtom } from 'jotai';
import {
  selectedNodeAtom,
  expandedTreeNodesAtom,
  searchFilterAtom,
} from './atoms';
import { ROADMAP_TREE, searchTree } from './roadmapData';
import type { RoadmapNode } from './types';

interface NavigationPanelProps {
  onNodeSelect?: (node: RoadmapNode) => void;
}

export const NavigationPanel: React.FC<NavigationPanelProps> = ({ onNodeSelect }) => {
  const [expanded, setExpanded] = useAtom(expandedTreeNodesAtom);
  const setSelectedNode = useSetAtom(selectedNodeAtom);
  const [filter, setFilter] = useAtom(searchFilterAtom);

  const visibleIds = useMemo(
    () => (filter ? searchTree(ROADMAP_TREE, filter) : null),
    [filter],
  );

  // When the user searches, expand every matching node's ancestors so the
  // hits are actually visible (otherwise filtered children sit hidden
  // under a still-collapsed parent — the original tree-panel bug).
  useEffect(() => {
    if (!visibleIds) return;
    setExpanded((prev) => {
      const merged = new Set(prev);
      visibleIds.forEach((id) => merged.add(id));
      return Array.from(merged);
    });
  }, [visibleIds, setExpanded]);

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
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, py: 0.25 }}>
            <Box
              sx={{
                width: 9,
                height: 9,
                borderRadius: '50%',
                bgcolor: node.color,
                flexShrink: 0,
                boxShadow: `0 0 6px ${node.color}55`,
              }}
            />
            <Typography
              variant="body2"
              noWrap
              sx={{ color: '#e6edf3', fontSize: '0.85rem' }}
            >
              {node.name}
            </Typography>
            {node.sub && (
              <Typography
                variant="caption"
                sx={{ color: '#8b949e', ml: 0.5, fontSize: '0.7rem' }}
              >
                {node.sub}
              </Typography>
            )}
          </Box>
        }
        sx={{
          '& .MuiTreeItem-content': {
            borderRadius: 0.75,
            py: 0.25,
            '&:hover': { bgcolor: '#1f2937' },
            '&.Mui-selected, &.Mui-selected.Mui-focused': {
              bgcolor: '#1f6feb33',
            },
          },
          '& .MuiTreeItem-iconContainer svg': { color: '#8b949e' },
        }}
      >
        {node.children?.map(renderTree)}
      </TreeItem>
    );
  };

  return (
    <Box
      sx={{
        width: '100%',
        height: '100%',
        display: 'flex',
        flexDirection: 'column',
        bgcolor: '#161b22',
        overflow: 'hidden',
      }}
    >
      <Box
        sx={{
          p: 1,
          borderBottom: '1px solid #30363d',
          flexShrink: 0,
        }}
      >
        <TextField
          fullWidth
          size="small"
          placeholder="Search the ecosystem…"
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          InputProps={{
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon fontSize="small" sx={{ color: '#8b949e' }} />
              </InputAdornment>
            ),
            endAdornment: filter ? (
              <InputAdornment position="end">
                <IconButton
                  size="small"
                  aria-label="clear search"
                  onClick={() => setFilter('')}
                  sx={{ color: '#8b949e' }}
                >
                  <ClearIcon fontSize="small" />
                </IconButton>
              </InputAdornment>
            ) : undefined,
          }}
          sx={{
            '& .MuiOutlinedInput-root': {
              bgcolor: '#0d1117',
              color: '#e6edf3',
              fontSize: '0.85rem',
              '& fieldset': { borderColor: '#30363d' },
              '&:hover fieldset': { borderColor: '#484f58' },
              '&.Mui-focused fieldset': { borderColor: '#58a6ff' },
            },
          }}
        />
      </Box>
      <Box sx={{ flex: 1, overflow: 'auto', p: 0.5 }}>
        <SimpleTreeView
          expandedItems={expanded}
          onExpandedItemsChange={(_e, ids) => setExpanded(ids)}
          onSelectedItemsChange={handleSelect}
        >
          {renderTree(ROADMAP_TREE)}
        </SimpleTreeView>
      </Box>
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
