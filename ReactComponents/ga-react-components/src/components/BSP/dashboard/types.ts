export type ConnectionStatus = 'connected' | 'disconnected' | 'checking';

export interface PerformanceMetrics {
  queryCount: number;
  averageQueryTime: number;
  fastestQuery: number;
  slowestQuery: number;
  cacheHitRate: number;
  memoryUsage: number;
  uptime: number;
  lastQueryTime?: number;
}

export interface BSPMetricsDashboardProps {
  connectionStatus: ConnectionStatus;
  recentQueryTimes: number[];
}
