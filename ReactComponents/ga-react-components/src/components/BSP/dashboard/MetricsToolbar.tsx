import React from 'react';
import { Button, CircularProgress, Typography } from '@mui/material';
import RefreshIcon from '@mui/icons-material/Refresh';

interface MetricsToolbarProps {
  loading: boolean;
  canRefresh: boolean;
  onRefresh: () => void;
}

const MetricsToolbar: React.FC<MetricsToolbarProps> = ({ loading, canRefresh, onRefresh }) => (
  <div className="metrics-dashboard__toolbar">
    <Typography variant="h6">Performance Metrics</Typography>
    <Button
      variant="outlined"
      size="small"
      startIcon={loading ? <CircularProgress size={16} /> : <RefreshIcon />}
      onClick={onRefresh}
      disabled={loading || !canRefresh}
    >
      {loading ? 'Refreshing…' : 'Refresh'}
    </Button>
  </div>
);

export default MetricsToolbar;
