import React from 'react';
import { Chip } from '@mui/material';

interface AverageTimeIndicatorProps {
  value: number;
}

const AverageTimeIndicator: React.FC<AverageTimeIndicatorProps> = ({ value }) => {
  const label = value < 1 ? 'Excellent' : value < 5 ? 'Good' : 'Slow';
  const color = value < 1 ? 'success' : value < 5 ? 'warning' : 'error';

  return (
    <span className="metrics-dashboard__inline-row">
      <span>{value.toFixed(2)}ms</span>
      <Chip size="small" label={label} color={color} />
    </span>
  );
};

export default AverageTimeIndicator;
