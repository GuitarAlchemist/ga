import React, { useEffect, useState } from 'react';
import { Grid } from '@mui/material';
import './BSPMetricsDashboard.css';
import { BSPMetricsDashboardProps, PerformanceMetrics } from './dashboard/types';
import MetricsToolbar from './dashboard/MetricsToolbar';
import QueryPerformanceCard from './dashboard/QueryPerformanceCard';
import SystemHealthCard from './dashboard/SystemHealthCard';
import QueryHistoryCard from './dashboard/QueryHistoryCard';

const BSPMetricsDashboard: React.FC<BSPMetricsDashboardProps> = ({ connectionStatus, recentQueryTimes }) => {
  const [metrics, setMetrics] = useState<PerformanceMetrics>({
    queryCount: 0,
    averageQueryTime: 0,
    fastestQuery: 0,
    slowestQuery: 0,
    cacheHitRate: 0,
    memoryUsage: 0,
    uptime: 0,
  });
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!recentQueryTimes.length) return;

    const avgTime = recentQueryTimes.reduce((sum, time) => sum + time, 0) / recentQueryTimes.length;
    const fastestTime = Math.min(...recentQueryTimes);
    const slowestTime = Math.max(...recentQueryTimes);
    const lastTime = recentQueryTimes[recentQueryTimes.length - 1];

    setMetrics((prev) => ({
      ...prev,
      queryCount: prev.queryCount + 1,
      averageQueryTime: avgTime,
      fastestQuery: fastestTime,
      slowestQuery: slowestTime,
      lastQueryTime: lastTime,
    }));
  }, [recentQueryTimes]);

  const refreshMetrics = async () => {
    setLoading(true);
    try {
      await new Promise((resolve) => setTimeout(resolve, 1000));
      setMetrics((prev) => ({
        ...prev,
        cacheHitRate: Math.random() * 100,
        memoryUsage: Math.random() * 100,
        uptime: Date.now() - Math.random() * 86400000,
      }));
    } finally {
      setLoading(false);
    }
  };

  const formatUptime = (milliseconds: number) => {
    const seconds = Math.floor(milliseconds / 1000);
    const minutes = Math.floor(seconds / 60);
    const hours = Math.floor(minutes / 60);

    if (hours > 0) return hours + 'h ' + (minutes % 60) + 'm';
    if (minutes > 0) return minutes + 'm ' + (seconds % 60) + 's';
    return seconds + 's';
  };

  return (
    <div>
      <MetricsToolbar
        loading={loading}
        canRefresh={connectionStatus === 'connected'}
        onRefresh={refreshMetrics}
      />

      <Grid container spacing={3}>
        <Grid item xs={12} md={6}>
          <QueryPerformanceCard metrics={metrics} />
        </Grid>
        <Grid item xs={12} md={6}>
          <SystemHealthCard
            metrics={metrics}
            connectionStatus={connectionStatus}
            uptimeLabel={formatUptime(metrics.uptime)}
          />
        </Grid>
        <Grid item xs={12}>
          <QueryHistoryCard recentQueryTimes={recentQueryTimes} />
        </Grid>
      </Grid>
    </div>
  );
};

export { BSPMetricsDashboard };
export default BSPMetricsDashboard;
