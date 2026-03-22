// src/components/EcosystemRoadmap/DetailPanel.tsx

import React from 'react';
import { useAtom } from 'jotai';
import {
  Box,
  Typography,
  Chip,
  Link,
  Breadcrumbs,
  Stack,
} from '@mui/material';
import OpenInNewIcon from '@mui/icons-material/OpenInNew';
import { selectedNodeAtom } from './atoms';
import { DOMAIN_COLORS, DOMAIN_LABELS } from './types';
import type { RoadmapNode } from './types';
import { getAncestors } from './roadmapData';

const PANEL_STYLE = {
  bgcolor: '#161b22',
  border: '1px solid #30363d',
  borderRadius: 1,
  p: 1.5,
  minHeight: 200,
  maxHeight: 200,
  overflowY: 'auto' as const,
  color: '#c9d1d9',
};

export const DetailPanel: React.FC = () => {
  const [selectedNode, setSelectedNode] = useAtom(selectedNodeAtom);

  if (!selectedNode) {
    return (
      <Box sx={PANEL_STYLE}>
        <Typography variant="body2" sx={{ color: '#8b949e', fontStyle: 'italic', mt: 1 }}>
          Select a node to view details
        </Typography>
      </Box>
    );
  }

  const ancestors = getAncestors(selectedNode);
  const domainColor = DOMAIN_COLORS[selectedNode.domain];
  const domainLabel = DOMAIN_LABELS[selectedNode.domain];

  const handleAncestorClick = (node: RoadmapNode) => {
    setSelectedNode(node);
  };

  const handleChildClick = (child: RoadmapNode) => {
    setSelectedNode(child);
  };

  return (
    <Box sx={PANEL_STYLE}>
      {/* Breadcrumb */}
      {ancestors.length > 0 && (
        <Breadcrumbs
          sx={{ mb: 1, '& .MuiBreadcrumbs-separator': { color: '#8b949e' } }}
          maxItems={4}
        >
          {ancestors.map((anc) => (
            <Link
              key={anc.id}
              component="button"
              variant="caption"
              onClick={() => handleAncestorClick(anc)}
              sx={{
                color: '#58a6ff',
                cursor: 'pointer',
                textDecoration: 'none',
                '&:hover': { textDecoration: 'underline' },
                background: 'none',
                border: 'none',
                p: 0,
              }}
            >
              {anc.name}
            </Link>
          ))}
        </Breadcrumbs>
      )}

      {/* Name + subtitle */}
      <Stack direction="row" alignItems="center" gap={1} mb={0.5} flexWrap="wrap">
        <Typography variant="h6" sx={{ color: '#e6edf3', fontSize: '1rem', fontWeight: 600 }}>
          {selectedNode.name}
        </Typography>
        {selectedNode.sub && (
          <Chip
            label={selectedNode.sub}
            size="small"
            sx={{
              bgcolor: '#21262d',
              color: '#8b949e',
              fontSize: '0.7rem',
              height: 20,
              border: '1px solid #30363d',
            }}
          />
        )}
      </Stack>

      {/* Domain chip */}
      <Chip
        label={domainLabel}
        size="small"
        sx={{
          bgcolor: `${domainColor}22`,
          color: domainColor,
          border: `1px solid ${domainColor}55`,
          fontSize: '0.7rem',
          height: 20,
          mb: 1,
        }}
      />

      {/* Description */}
      <Typography variant="body2" sx={{ color: '#8b949e', mb: 1, lineHeight: 1.5 }}>
        {selectedNode.description}
      </Typography>

      {/* Links */}
      <Stack direction="row" gap={2} flexWrap="wrap" mb={selectedNode.children?.length ? 1 : 0}>
        {selectedNode.grammarUrl && (
          <Link
            href={selectedNode.grammarUrl}
            target="_blank"
            rel="noopener noreferrer"
            variant="caption"
            sx={{ color: '#58a6ff', display: 'flex', alignItems: 'center', gap: 0.25 }}
          >
            Grammar
            <OpenInNewIcon sx={{ fontSize: '0.75rem' }} />
          </Link>
        )}
        {selectedNode.url && (
          <Link
            href={selectedNode.url}
            target="_blank"
            rel="noopener noreferrer"
            variant="caption"
            sx={{ color: '#58a6ff', display: 'flex', alignItems: 'center', gap: 0.25 }}
          >
            GitHub
            <OpenInNewIcon sx={{ fontSize: '0.75rem' }} />
          </Link>
        )}
      </Stack>

      {/* Children list */}
      {selectedNode.children && selectedNode.children.length > 0 && (
        <Box>
          <Typography variant="caption" sx={{ color: '#8b949e', display: 'block', mb: 0.5 }}>
            Children ({selectedNode.children.length})
          </Typography>
          <Stack direction="row" gap={0.75} flexWrap="wrap">
            {selectedNode.children.map((child) => (
              <Box
                key={child.id}
                component="button"
                onClick={() => handleChildClick(child)}
                sx={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 0.5,
                  background: 'none',
                  border: '1px solid #30363d',
                  borderRadius: 1,
                  px: 0.75,
                  py: 0.25,
                  cursor: 'pointer',
                  color: '#c9d1d9',
                  fontSize: '0.7rem',
                  '&:hover': { bgcolor: '#21262d', borderColor: '#58a6ff' },
                }}
              >
                <Box
                  sx={{
                    width: 6,
                    height: 6,
                    borderRadius: '50%',
                    bgcolor: DOMAIN_COLORS[child.domain],
                    flexShrink: 0,
                  }}
                />
                {child.name}
              </Box>
            ))}
          </Stack>
        </Box>
      )}
    </Box>
  );
};

export default DetailPanel;
