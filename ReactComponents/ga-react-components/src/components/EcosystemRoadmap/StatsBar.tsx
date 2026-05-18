// src/components/EcosystemRoadmap/StatsBar.tsx

import React from 'react';
import { Box, Chip } from '@mui/material';
import { STATS } from './roadmapData';

export const StatsBar: React.FC = () => {
  return (
    <Box
      sx={{
        display: 'flex',
        flexDirection: 'row',
        flexWrap: 'wrap',
        gap: 0.75,
        justifyContent: 'center',
        alignItems: 'center',
        px: 1,
        py: { xs: 0.5, sm: 0.75 },
        bgcolor: '#0d1117',
        borderTop: '1px solid #30363d',
        flexShrink: 0,
      }}
    >
      {STATS.map((stat) => (
        <Chip
          key={stat.label}
          label={`${stat.value} ${stat.label}`}
          variant="outlined"
          size="small"
          onClick={() => window.open(stat.url, '_blank', 'noopener,noreferrer')}
          sx={{
            color: '#58a6ff',
            borderColor: '#58a6ff',
            fontSize: '0.75rem',
            cursor: 'pointer',
            '&:hover': {
              bgcolor: '#58a6ff22',
            },
          }}
        />
      ))}
    </Box>
  );
};

export default StatsBar;
