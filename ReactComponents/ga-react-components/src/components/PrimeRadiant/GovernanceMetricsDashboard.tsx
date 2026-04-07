// src/components/PrimeRadiant/GovernanceMetricsDashboard.tsx
// Live governance metrics dashboard for the Prime Radiant command center
// Fetches from /api/metrics/system, /api/governance, /api/metrics/performance

import React, { useEffect, useState, useCallback, useRef } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------
interface SystemMetrics {
  cpuPercent: number;
  memoryPercent: number;
  uptimeSeconds: number;
  activeConnections: number;
  cpuHistory: number[];
  memoryHistory: number[];
}

interface GovernanceData {
  totalNodes: number;
  totalEdges: number;
  healthDistribution: {
    healthy: number;
    warning: number;
    error: number;
    unknown: number;
  };
  stalenessScore: number; // 0-1, percentage of nodes stale 7+ days
  lastUpdated: string;
}

interface PerformanceMetrics {
  responseTimeP50: number;
  responseTimeP95: number;
  responseTimeP99: number;
  throughputPerMin: number;
}

interface TrendDirection {
  direction: 'up' | 'down' | 'stable';
  magnitude: number; // 0-1 normalized
}

interface DashboardState {
  system: SystemMetrics | null;
  governance: GovernanceData | null;
  performance: PerformanceMetrics | null;
  prevGovernance: GovernanceData | null;
  prevPerformance: PerformanceMetrics | null;
  lastFetchedAt: string | null;
  isDemo: boolean;
  error: string | null;
}

// ---------------------------------------------------------------------------
// API helpers
// ---------------------------------------------------------------------------
function getApiBase(): string {
  const envBase = typeof import.meta !== 'undefined'
    ? (import.meta as { env?: Record<string, string> }).env?.VITE_API_BASE_URL
    : undefined;
  return envBase || (typeof window !== 'undefined' ? window.location.origin : 'http://localhost:5232');
}

// ---------------------------------------------------------------------------
// Demo / fallback data
// ---------------------------------------------------------------------------
function demoSystemMetrics(): SystemMetrics {
  const now = Date.now();
  return {
    cpuPercent: 23 + Math.random() * 15,
    memoryPercent: 58 + Math.random() * 8,
    uptimeSeconds: Math.floor((now - new Date('2026-04-01T00:00:00Z').getTime()) / 1000),
    activeConnections: 4 + Math.floor(Math.random() * 3),
    cpuHistory: Array.from({ length: 20 }, () => 15 + Math.random() * 25),
    memoryHistory: Array.from({ length: 20 }, () => 55 + Math.random() * 10),
  };
}

function demoGovernanceData(): GovernanceData {
  return {
    totalNodes: 142,
    totalEdges: 387,
    healthDistribution: { healthy: 118, warning: 14, error: 6, unknown: 4 },
    stalenessScore: 0.12,
    lastUpdated: new Date().toISOString(),
  };
}

function demoPerformanceMetrics(): PerformanceMetrics {
  return {
    responseTimeP50: 42 + Math.random() * 10,
    responseTimeP95: 128 + Math.random() * 30,
    responseTimeP99: 340 + Math.random() * 60,
    throughputPerMin: 120 + Math.floor(Math.random() * 40),
  };
}

// ---------------------------------------------------------------------------
// Fetch helpers
// ---------------------------------------------------------------------------
async function fetchJson<T>(url: string): Promise<T | null> {
  try {
    const res = await fetch(url, { signal: AbortSignal.timeout(6000) });
    if (!res.ok) return null;
    return await res.json();
  } catch {
    return null;
  }
}

// ---------------------------------------------------------------------------
// Formatting helpers
// ---------------------------------------------------------------------------
function formatUptime(seconds: number): string {
  const d = Math.floor(seconds / 86400);
  const h = Math.floor((seconds % 86400) / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  if (d > 0) return `${d}d ${h}h`;
  if (h > 0) return `${h}h ${m}m`;
  return `${m}m`;
}

function computeTrend(current: number, previous: number | undefined): TrendDirection {
  if (previous === undefined || previous === 0) return { direction: 'stable', magnitude: 0 };
  const delta = (current - previous) / previous;
  if (Math.abs(delta) < 0.02) return { direction: 'stable', magnitude: 0 };
  return {
    direction: delta > 0 ? 'up' : 'down',
    magnitude: Math.min(1, Math.abs(delta)),
  };
}

// ---------------------------------------------------------------------------
// Micro-components (CSS-only visualizations)
// ---------------------------------------------------------------------------

const FONT_MONO = "'JetBrains Mono', monospace";

const StatusDot: React.FC<{ color: string; size?: number; glow?: boolean }> = ({ color, size = 8, glow = false }) => (
  <span style={{
    display: 'inline-block',
    width: size,
    height: size,
    borderRadius: '50%',
    backgroundColor: color,
    flexShrink: 0,
    boxShadow: glow ? `0 0 6px ${color}88` : 'none',
  }} />
);

const PercentBar: React.FC<{
  value: number;
  max?: number;
  color: string;
  height?: number;
  label?: string;
}> = ({ value, max = 100, color, height = 6, label }) => {
  const pct = Math.min(100, (value / max) * 100);
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 6, width: '100%' }}>
      {label && (
        <span style={{ fontSize: '0.65rem', color: '#6b7280', fontFamily: FONT_MONO, width: 32, flexShrink: 0 }}>
          {label}
        </span>
      )}
      <div style={{
        flex: 1,
        height,
        background: '#21262d',
        borderRadius: height / 2,
        overflow: 'hidden',
      }}>
        <div style={{
          height: '100%',
          width: `${pct}%`,
          background: `linear-gradient(90deg, ${color}88, ${color})`,
          borderRadius: height / 2,
          transition: 'width 0.6s ease',
        }} />
      </div>
      <span style={{
        fontSize: '0.65rem',
        color,
        fontFamily: FONT_MONO,
        fontWeight: 600,
        width: 36,
        textAlign: 'right',
        flexShrink: 0,
      }}>
        {value.toFixed(0)}%
      </span>
    </div>
  );
};

const Sparkline: React.FC<{ data: number[]; color: string; width?: number; height?: number }> = ({
  data, color, width = 80, height = 20,
}) => {
  if (data.length < 2) return null;
  const min = Math.min(...data);
  const max = Math.max(...data);
  const range = max - min || 1;
  const stepX = width / (data.length - 1);
  const points = data.map((v, i) => {
    const x = i * stepX;
    const y = height - ((v - min) / range) * (height - 2) - 1;
    return `${x},${y}`;
  }).join(' ');

  return (
    <svg width={width} height={height} style={{ display: 'block', flexShrink: 0 }}>
      <polyline
        points={points}
        fill="none"
        stroke={color}
        strokeWidth={1.5}
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
};

const TrendArrow: React.FC<{ trend: TrendDirection; goodDirection: 'up' | 'down' }> = ({ trend, goodDirection }) => {
  if (trend.direction === 'stable') {
    return <span style={{ color: '#6b7280', fontSize: '0.7rem' }}>=</span>;
  }
  const isGood = trend.direction === goodDirection;
  const color = isGood ? '#33CC66' : '#FF4444';
  const arrow = trend.direction === 'up' ? '\u25B2' : '\u25BC';
  return <span style={{ color, fontSize: '0.6rem', fontWeight: 600 }}>{arrow}</span>;
};

const PieChart: React.FC<{
  segments: { value: number; color: string; label: string }[];
  size?: number;
}> = ({ segments, size = 60 }) => {
  const total = segments.reduce((s, seg) => s + seg.value, 0);
  if (total === 0) return null;
  const r = size / 2 - 2;
  const cx = size / 2;
  const cy = size / 2;
  let currentAngle = -Math.PI / 2;

  const paths = segments
    .filter(seg => seg.value > 0)
    .map((seg, i) => {
      const angle = (seg.value / total) * 2 * Math.PI;
      const startX = cx + r * Math.cos(currentAngle);
      const startY = cy + r * Math.sin(currentAngle);
      const endX = cx + r * Math.cos(currentAngle + angle);
      const endY = cy + r * Math.sin(currentAngle + angle);
      const largeArc = angle > Math.PI ? 1 : 0;
      const d = `M ${cx} ${cy} L ${startX} ${startY} A ${r} ${r} 0 ${largeArc} 1 ${endX} ${endY} Z`;
      currentAngle += angle;
      return <path key={i} d={d} fill={seg.color} fillOpacity={0.75} stroke="#0d1117" strokeWidth={1} />;
    });

  return (
    <svg width={size} height={size} style={{ display: 'block', flexShrink: 0 }}>
      {paths}
    </svg>
  );
};

// ---------------------------------------------------------------------------
// Section wrapper
// ---------------------------------------------------------------------------
const Section: React.FC<{ title: string; children: React.ReactNode }> = ({ title, children }) => (
  <div style={{
    padding: '8px 12px',
    borderBottom: '1px solid #21262d',
  }}>
    <div style={{
      fontSize: '0.65rem',
      fontFamily: FONT_MONO,
      fontWeight: 600,
      color: '#8b949e',
      textTransform: 'uppercase',
      letterSpacing: '0.06em',
      marginBottom: 8,
    }}>
      {title}
    </div>
    {children}
  </div>
);

const MetricRow: React.FC<{
  label: string;
  value: string;
  color?: string;
  trend?: TrendDirection;
  goodDirection?: 'up' | 'down';
}> = ({ label, value, color = '#c9d1d9', trend, goodDirection = 'up' }) => (
  <div style={{
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: '2px 0',
    fontSize: '0.7rem',
    fontFamily: FONT_MONO,
  }}>
    <span style={{ color: '#6b7280' }}>{label}</span>
    <span style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
      {trend && <TrendArrow trend={trend} goodDirection={goodDirection} />}
      <span style={{ color, fontWeight: 600 }}>{value}</span>
    </span>
  </div>
);

const Unavailable: React.FC = () => (
  <div style={{
    padding: '8px 0',
    color: '#4a5568',
    fontSize: '0.65rem',
    fontFamily: FONT_MONO,
    fontStyle: 'italic',
    textAlign: 'center',
  }}>
    Metric unavailable
  </div>
);

// ---------------------------------------------------------------------------
// Main component
// ---------------------------------------------------------------------------
const REFRESH_INTERVAL = 30_000;

export const GovernanceMetricsDashboard: React.FC = () => {
  const [state, setState] = useState<DashboardState>({
    system: null,
    governance: null,
    performance: null,
    prevGovernance: null,
    prevPerformance: null,
    lastFetchedAt: null,
    isDemo: true,
    error: null,
  });
  const [collapsed, setCollapsed] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const prevStateRef = useRef<{ governance: GovernanceData | null; performance: PerformanceMetrics | null }>({
    governance: null,
    performance: null,
  });

  const refresh = useCallback(async () => {
    setRefreshing(true);
    const base = getApiBase();

    const [sys, gov, perf] = await Promise.all([
      fetchJson<SystemMetrics>(`${base}/api/metrics/system`),
      fetchJson<GovernanceData>(`${base}/api/governance`),
      fetchJson<PerformanceMetrics>(`${base}/api/metrics/performance`),
    ]);

    const anyLive = sys !== null || gov !== null || perf !== null;

    setState(prev => {
      const newState: DashboardState = {
        system: sys ?? demoSystemMetrics(),
        governance: gov ?? demoGovernanceData(),
        performance: perf ?? demoPerformanceMetrics(),
        prevGovernance: prevStateRef.current.governance,
        prevPerformance: prevStateRef.current.performance,
        lastFetchedAt: new Date().toISOString(),
        isDemo: !anyLive,
        error: null,
      };
      // Store current values for next trend comparison
      prevStateRef.current = {
        governance: newState.governance,
        performance: newState.performance,
      };
      return newState;
    });

    setRefreshing(false);
  }, []);

  useEffect(() => {
    refresh();
    const id = setInterval(refresh, REFRESH_INTERVAL);
    return () => clearInterval(id);
  }, [refresh]);

  const { system, governance, performance, prevGovernance, prevPerformance, isDemo } = state;

  // Compute trends
  const edgeDensity = governance && governance.totalNodes > 0
    ? governance.totalEdges / governance.totalNodes
    : 0;
  const prevEdgeDensity = prevGovernance && prevGovernance.totalNodes > 0
    ? prevGovernance.totalEdges / prevGovernance.totalNodes
    : undefined;

  const nodesTrend = computeTrend(governance?.totalNodes ?? 0, prevGovernance?.totalNodes);
  const densityTrend = computeTrend(edgeDensity, prevEdgeDensity);
  const stalenessTrend = computeTrend(governance?.stalenessScore ?? 0, prevGovernance?.stalenessScore);
  const p50Trend = computeTrend(performance?.responseTimeP50 ?? 0, prevPerformance?.responseTimeP50);
  const throughputTrend = computeTrend(performance?.throughputPerMin ?? 0, prevPerformance?.throughputPerMin);

  // Health colors
  const healthColors = {
    healthy: '#33CC66',
    warning: '#FFB300',
    error: '#FF4444',
    unknown: '#8b5cf6',
  };

  // Overall status
  const errorCount = governance?.healthDistribution.error ?? 0;
  const warningCount = governance?.healthDistribution.warning ?? 0;
  const overallColor = errorCount > 0 ? '#FF4444' : warningCount > 0 ? '#FFB300' : '#33CC66';

  if (!system && !governance && !performance) {
    return (
      <div className="prime-radiant__activity">
        <div className="prime-radiant__activity-header">
          <span className="prime-radiant__activity-title">Governance Metrics</span>
        </div>
        <div style={{ padding: 16, color: '#6b7280', fontSize: '0.75rem' }}>Loading...</div>
      </div>
    );
  }

  return (
    <div className="prime-radiant__activity" style={{ maxHeight: '70vh', overflowY: 'auto' }}>
      {/* Header */}
      <div
        className="prime-radiant__activity-header"
        onClick={() => setCollapsed(!collapsed)}
      >
        <span className="prime-radiant__activity-title">
          Governance Metrics
          <StatusDot color={isDemo ? '#eab308' : '#22c55e'} glow />
          <span style={{ fontSize: '0.6rem', color: isDemo ? '#eab308' : '#22c55e', marginLeft: 4, marginRight: 6 }}>
            {isDemo ? 'Demo' : 'Live'}
          </span>
          <span className="prime-radiant__activity-count">
            {governance?.totalNodes ?? 0} artifacts &middot;
            {' '}{errorCount > 0
              ? <span style={{ color: '#FF4444' }}>{errorCount} errors</span>
              : <span style={{ color: '#33CC66' }}>healthy</span>
            }
          </span>
        </span>
        <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <button
            onClick={(e) => { e.stopPropagation(); refresh(); }}
            disabled={refreshing}
            title="Refresh metrics"
            style={{
              background: 'none',
              border: '1px solid #30363d',
              borderRadius: 4,
              color: '#8b949e',
              cursor: refreshing ? 'wait' : 'pointer',
              padding: '2px 6px',
              fontSize: '0.65rem',
              fontFamily: FONT_MONO,
              opacity: refreshing ? 0.5 : 1,
              transition: 'opacity 0.2s',
            }}
          >
            {refreshing ? '\u21BB ...' : '\u21BB'}
          </button>
          <span className="prime-radiant__activity-toggle">
            {collapsed ? '\u25B6' : '\u25BC'}
          </span>
        </span>
      </div>

      {!collapsed && (
        <div>
          {/* Section 1: System Health */}
          <Section title="System Health">
            {system ? (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                  <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: 4 }}>
                    <PercentBar
                      value={system.cpuPercent}
                      color={system.cpuPercent > 80 ? '#FF4444' : system.cpuPercent > 60 ? '#FFB300' : '#33CC66'}
                      label="CPU"
                    />
                    <PercentBar
                      value={system.memoryPercent}
                      color={system.memoryPercent > 85 ? '#FF4444' : system.memoryPercent > 70 ? '#FFB300' : '#33CC66'}
                      label="MEM"
                    />
                  </div>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 2, alignItems: 'center' }}>
                    <Sparkline data={system.cpuHistory} color="#33CC66" width={64} height={16} />
                    <Sparkline data={system.memoryHistory} color="#4FC3F7" width={64} height={16} />
                  </div>
                </div>
                <div style={{ display: 'flex', gap: 16, fontSize: '0.65rem', fontFamily: FONT_MONO, color: '#6b7280' }}>
                  <span>Uptime: <span style={{ color: '#c9d1d9', fontWeight: 600 }}>{formatUptime(system.uptimeSeconds)}</span></span>
                  <span>Connections: <span style={{ color: '#c9d1d9', fontWeight: 600 }}>{system.activeConnections}</span></span>
                </div>
              </div>
            ) : <Unavailable />}
          </Section>

          {/* Section 2: Governance KPIs */}
          <Section title="Governance KPIs">
            {governance ? (
              <div style={{ display: 'flex', gap: 12, alignItems: 'flex-start' }}>
                {/* Pie chart */}
                <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4 }}>
                  <PieChart
                    size={56}
                    segments={[
                      { value: governance.healthDistribution.healthy, color: healthColors.healthy, label: 'Healthy' },
                      { value: governance.healthDistribution.warning, color: healthColors.warning, label: 'Warning' },
                      { value: governance.healthDistribution.error, color: healthColors.error, label: 'Error' },
                      { value: governance.healthDistribution.unknown, color: healthColors.unknown, label: 'Unknown' },
                    ]}
                  />
                  {/* Legend */}
                  <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', justifyContent: 'center' }}>
                    {Object.entries(governance.healthDistribution).map(([key, val]) => (
                      <span key={key} style={{ display: 'flex', alignItems: 'center', gap: 2, fontSize: '0.55rem', fontFamily: FONT_MONO, color: '#6b7280' }}>
                        <StatusDot color={healthColors[key as keyof typeof healthColors]} size={5} />
                        {val}
                      </span>
                    ))}
                  </div>
                </div>
                {/* KPI rows */}
                <div style={{ flex: 1 }}>
                  <MetricRow
                    label="Total Artifacts"
                    value={governance.totalNodes.toString()}
                    color={overallColor}
                    trend={nodesTrend}
                    goodDirection="up"
                  />
                  <MetricRow
                    label="Edge Density"
                    value={edgeDensity.toFixed(2)}
                    trend={densityTrend}
                    goodDirection="up"
                  />
                  <MetricRow
                    label="Staleness"
                    value={`${(governance.stalenessScore * 100).toFixed(0)}%`}
                    color={governance.stalenessScore > 0.3 ? '#FF4444' : governance.stalenessScore > 0.15 ? '#FFB300' : '#33CC66'}
                    trend={stalenessTrend}
                    goodDirection="down"
                  />
                </div>
              </div>
            ) : <Unavailable />}
          </Section>

          {/* Section 3: Performance Metrics */}
          <Section title="Performance">
            {performance ? (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                <MetricRow
                  label="p50"
                  value={`${performance.responseTimeP50.toFixed(0)}ms`}
                  color={performance.responseTimeP50 > 200 ? '#FF4444' : '#33CC66'}
                  trend={p50Trend}
                  goodDirection="down"
                />
                <MetricRow
                  label="p95"
                  value={`${performance.responseTimeP95.toFixed(0)}ms`}
                  color={performance.responseTimeP95 > 500 ? '#FF4444' : performance.responseTimeP95 > 200 ? '#FFB300' : '#33CC66'}
                />
                <MetricRow
                  label="p99"
                  value={`${performance.responseTimeP99.toFixed(0)}ms`}
                  color={performance.responseTimeP99 > 1000 ? '#FF4444' : performance.responseTimeP99 > 500 ? '#FFB300' : '#33CC66'}
                />
                <div style={{ marginTop: 4, borderTop: '1px solid #21262d', paddingTop: 4 }}>
                  <MetricRow
                    label="Throughput"
                    value={`${performance.throughputPerMin.toFixed(0)} req/min`}
                    color="#4FC3F7"
                    trend={throughputTrend}
                    goodDirection="up"
                  />
                </div>
              </div>
            ) : <Unavailable />}
          </Section>

          {/* Section 4: Trend Summary */}
          <div style={{
            padding: '6px 12px',
            display: 'flex',
            justifyContent: 'space-between',
            fontSize: '0.6rem',
            fontFamily: FONT_MONO,
            color: '#4a5568',
          }}>
            <span>
              Last refreshed: {state.lastFetchedAt
                ? new Date(state.lastFetchedAt).toLocaleTimeString()
                : '--'
              }
            </span>
            <span>30s auto-refresh</span>
          </div>
        </div>
      )}
    </div>
  );
};
