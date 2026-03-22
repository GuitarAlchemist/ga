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
        gap: 1,
        justifyContent: 'center',
        alignItems: 'center',
        py: 0.75,
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
