import React from 'react';
import { Card, CardContent } from '@mui/material';
import MemoryIcon from '@mui/icons-material/Memory';
import SectionHeader from './SectionHeader';
import ConnectionStatusChip from './ConnectionStatusChip';
import UsageProgress from './UsageProgress';
import { PerformanceMetrics, ConnectionStatus } from './types';

interface SystemHealthCardProps {
  metrics: PerformanceMetrics;
  connectionStatus: ConnectionStatus;
  uptimeLabel: string;
}

const SystemHealthCard: React.FC<SystemHealthCardProps> = ({ metrics, connectionStatus, uptimeLabel }) => {
  const rows: Array<{ label: string; content: React.ReactNode }> = [
    { label: 'Connection Status', content: <ConnectionStatusChip status={connectionStatus} /> },
    { label: 'Cache Hit Rate', content: <UsageProgress value={metrics.cacheHitRate} good={80} warn={60} /> },
    { label: 'Memory Usage', content: <UsageProgress value={metrics.memoryUsage} good={70} warn={85} /> },
    { label: 'Uptime', content: uptimeLabel },
  ];

  return (
    <Card>
      <CardContent>
        <SectionHeader icon={<MemoryIcon />} text="System Health" />
        <dl className="metrics-dashboard__stats">
          {rows.map((row) => (
            <div key={row.label}>
              <dt>{row.label}</dt>
              <dd>{row.content}</dd>
            </div>
          ))}
        </dl>
      </CardContent>
    </Card>
  );
};

export default SystemHealthCard;
