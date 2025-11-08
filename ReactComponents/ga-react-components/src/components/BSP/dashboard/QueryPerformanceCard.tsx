import React from 'react';
import { Card, CardContent } from '@mui/material';
import SpeedIcon from '@mui/icons-material/Speed';
import SectionHeader from './SectionHeader';
import AverageTimeIndicator from './AverageTimeIndicator';
import { PerformanceMetrics } from './types';

interface QueryPerformanceCardProps {
  metrics: PerformanceMetrics;
}

const QueryPerformanceCard: React.FC<QueryPerformanceCardProps> = ({ metrics }) => {
  const rows: Array<{ label: string; content: React.ReactNode }> = [
    { label: 'Total Queries', content: metrics.queryCount.toLocaleString() },
    { label: 'Average Query Time', content: <AverageTimeIndicator value={metrics.averageQueryTime} /> },
    { label: 'Fastest Query', content: metrics.fastestQuery.toFixed(2) + 'ms' },
    { label: 'Slowest Query', content: metrics.slowestQuery.toFixed(2) + 'ms' },
  ];

  if (typeof metrics.lastQueryTime === 'number') {
    rows.push({ label: 'Last Query Time', content: metrics.lastQueryTime.toFixed(2) + 'ms' });
  }

  return (
    <Card>
      <CardContent>
        <SectionHeader icon={<SpeedIcon />} text="Query Performance" />
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

export default QueryPerformanceCard;
