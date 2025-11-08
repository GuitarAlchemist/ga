import React from 'react';
import { Chip } from '@mui/material';
import { ConnectionStatus } from './types';

interface ConnectionStatusChipProps {
  status: ConnectionStatus;
}

const ConnectionStatusChip: React.FC<ConnectionStatusChipProps> = ({ status }) => {
  const color = status === 'connected' ? 'success' : status === 'checking' ? 'warning' : 'error';
  return <Chip size="small" label={status} color={color} />;
};

export default ConnectionStatusChip;
