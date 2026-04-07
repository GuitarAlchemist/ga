// src/components/PrimeRadiant/GovernanceCompliancePanel.tsx
// Governance Compliance Panel — shows compliance scores from /api/governance
// Displays overall compliance, per-type breakdown, belief distribution,
// and recent violations with auto-refresh every 60s.

import React, { useState, useCallback, useEffect, useRef } from 'react';
import type { GovernanceGraph, GovernanceNode, GovernanceHealthStatus, GovernanceNodeType } from './types';
import type { BeliefState, TetravalentStatus } from './DataLoader';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

interface ComplianceScore {
  label: string;
  healthy: number;
  warning: number;
  error: number;
  contradictory: number;
  unknown: number;
  total: number;
  percentage: number;
}

interface BeliefDistribution {
  T: number;
  P: number;
  U: number;
  D: number;
  F: number;
  C: number;
  total: number;
}

interface Violation {
  id: string;
  name: string;
  type: GovernanceNodeType;
  health: GovernanceHealthStatus;
  description: string;
}

interface ComplianceData {
  overall: ComplianceScore;
  byType: ComplianceScore[];
  beliefs: BeliefDistribution;
  violations: Violation[];
  timestamp: string;
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const HEALTH_COLORS: Record<GovernanceHealthStatus, string> = {
  healthy: '#4caf50',
  warning: '#ff9800',
  error: '#f44336',
  contradictory: '#9c27b0',
  unknown: '#6b7280',
};

const BELIEF_COLORS: Record<string, string> = {
  T: '#4caf50',
  P: '#81c784',
  U: '#6b7280',
  D: '#ffb74d',
  F: '#f44336',
  C: '#9c27b0',
};

const BELIEF_LABELS: Record<string, string> = {
  T: 'True',
  P: 'Probable',
  U: 'Unknown',
  D: 'Doubtful',
  F: 'False',
  C: 'Contradictory',
};

const TYPE_LABELS: Record<GovernanceNodeType, string> = {
  constitution: 'Constitutions',
  policy: 'Policies',
  persona: 'Personas',
  pipeline: 'Pipelines',
  department: 'Departments',
  schema: 'Schemas',
  test: 'Tests',
  ixql: 'IXQL',
};

const REFRESH_INTERVAL_MS = 60_000;

// ---------------------------------------------------------------------------
// Data computation
// ---------------------------------------------------------------------------

function computeScore(label: string, nodes: GovernanceNode[]): ComplianceScore {
  const counts: Record<GovernanceHealthStatus, number> = {
    healthy: 0, warning: 0, error: 0, contradictory: 0, unknown: 0,
  };
  for (const node of nodes) {
    const status = node.healthStatus ?? 'unknown';
    counts[status] = (counts[status] ?? 0) + 1;
  }
  const total = nodes.length;
  const percentage = total > 0
    ? Math.round(((counts.healthy + counts.warning * 0.5) / total) * 100)
    : 0;
  return { label, ...counts, total, percentage };
}

function computeBeliefDistribution(beliefs: BeliefState[]): BeliefDistribution {
  const dist: BeliefDistribution = { T: 0, P: 0, U: 0, D: 0, F: 0, C: 0, total: beliefs.length };
  for (const b of beliefs) {
    const tv = b.truth_value as string;
    if (tv in dist && tv !== 'total') {
      (dist as Record<string, number>)[tv]++;
    }
  }
  return dist;
}

function extractViolations(nodes: GovernanceNode[]): Violation[] {
  return nodes
    .filter(n => n.healthStatus === 'error' || n.healthStatus === 'contradictory')
    .map(n => ({
      id: n.id,
      name: n.name,
      type: n.type,
      health: n.healthStatus!,
      description: n.description,
    }));
}

function processData(graph: GovernanceGraph, beliefs: BeliefState[]): ComplianceData {
  const overall = computeScore('Overall', graph.nodes);

  const byTypeMap = new Map<GovernanceNodeType, GovernanceNode[]>();
  for (const node of graph.nodes) {
    const list = byTypeMap.get(node.type) ?? [];
    list.push(node);
    byTypeMap.set(node.type, list);
  }
  const byType: ComplianceScore[] = [];
  for (const [type, nodes] of byTypeMap) {
    byType.push(computeScore(TYPE_LABELS[type] ?? type, nodes));
  }
  byType.sort((a, b) => a.percentage - b.percentage); // worst first

  return {
    overall,
    byType,
    beliefs: computeBeliefDistribution(beliefs),
    violations: extractViolations(graph.nodes),
    timestamp: graph.timestamp ?? new Date().toISOString(),
  };
}

// ---------------------------------------------------------------------------
// Sub-components
// ---------------------------------------------------------------------------

const ScoreBar: React.FC<{ score: ComplianceScore; compact?: boolean }> = ({ score, compact }) => {
  const pct = score.percentage;
  const barColor = pct >= 80 ? '#4caf50' : pct >= 50 ? '#ff9800' : '#f44336';

  return (
    <div style={{ marginBottom: compact ? 4 : 8 }}>
      <div style={{
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'baseline',
        marginBottom: 2,
        fontSize: compact ? 10 : 11,
      }}>
        <span style={{ color: '#e6e6e6' }}>{score.label}</span>
        <span style={{ color: barColor, fontWeight: 'bold', fontSize: compact ? 9 : 11 }}>
          {pct}%
          <span style={{ color: '#6b7280', fontWeight: 'normal', marginLeft: 4, fontSize: 9 }}>
            {score.total} nodes
          </span>
        </span>
      </div>
      <div style={{
        height: compact ? 3 : 5,
        background: 'rgba(255,255,255,0.06)',
        borderRadius: 2,
        overflow: 'hidden',
      }}>
        <div style={{
          height: '100%',
          width: `${pct}%`,
          background: `linear-gradient(90deg, ${barColor}cc, ${barColor})`,
          borderRadius: 2,
          transition: 'width 0.4s ease',
        }} />
      </div>
    </div>
  );
};

const HealthBreakdown: React.FC<{ score: ComplianceScore }> = ({ score }) => {
  if (score.total === 0) return null;
  const items: Array<{ label: string; count: number; color: string }> = [
    { label: 'Healthy', count: score.healthy, color: HEALTH_COLORS.healthy },
    { label: 'Warning', count: score.warning, color: HEALTH_COLORS.warning },
    { label: 'Error', count: score.error, color: HEALTH_COLORS.error },
    { label: 'Contradictory', count: score.contradictory, color: HEALTH_COLORS.contradictory },
    { label: 'Unknown', count: score.unknown, color: HEALTH_COLORS.unknown },
  ].filter(i => i.count > 0);

  return (
    <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginBottom: 6, fontSize: 9 }}>
      {items.map(i => (
        <span key={i.label} style={{ color: i.color }}>
          {i.count} {i.label.toLowerCase()}
        </span>
      ))}
    </div>
  );
};

const BeliefDistributionBar: React.FC<{ dist: BeliefDistribution }> = ({ dist }) => {
  if (dist.total === 0) {
    return <div style={{ color: '#6b7280', fontSize: 10, padding: '4px 0' }}>No beliefs loaded</div>;
  }

  const keys: Array<keyof Omit<BeliefDistribution, 'total'>> = ['T', 'P', 'U', 'D', 'F', 'C'];

  return (
    <div style={{ marginBottom: 8 }}>
      {/* Stacked bar */}
      <div style={{
        display: 'flex',
        height: 8,
        borderRadius: 3,
        overflow: 'hidden',
        background: 'rgba(255,255,255,0.06)',
        marginBottom: 6,
      }}>
        {keys.map(k => {
          const count = dist[k];
          if (count === 0) return null;
          const widthPct = (count / dist.total) * 100;
          return (
            <div
              key={k}
              title={`${BELIEF_LABELS[k]}: ${count}`}
              style={{
                width: `${widthPct}%`,
                background: BELIEF_COLORS[k],
                minWidth: count > 0 ? 2 : 0,
              }}
            />
          );
        })}
      </div>
      {/* Legend */}
      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', fontSize: 9 }}>
        {keys.map(k => {
          const count = dist[k];
          if (count === 0) return null;
          return (
            <span key={k} style={{ color: BELIEF_COLORS[k] }}>
              {k}:{count}
            </span>
          );
        })}
      </div>
      {/* Attention items */}
      {(dist.U > 0 || dist.C > 0) && (
        <div style={{ marginTop: 4, fontSize: 9, color: '#ff9800' }}>
          {dist.U > 0 && <span>{dist.U} unknown belief{dist.U > 1 ? 's' : ''} need investigation. </span>}
          {dist.C > 0 && <span style={{ color: '#9c27b0' }}>{dist.C} contradictory belief{dist.C > 1 ? 's' : ''} need escalation.</span>}
        </div>
      )}
    </div>
  );
};

const ViolationList: React.FC<{ violations: Violation[] }> = ({ violations }) => {
  if (violations.length === 0) {
    return (
      <div style={{ color: '#4caf50', fontSize: 10, padding: '4px 0' }}>
        No violations detected
      </div>
    );
  }

  return (
    <div style={{ maxHeight: 120, overflow: 'auto' }}>
      {violations.slice(0, 15).map(v => (
        <div key={v.id} style={{
          display: 'flex',
          alignItems: 'center',
          gap: 6,
          padding: '3px 0',
          fontSize: 10,
          borderBottom: '1px solid rgba(255,255,255,0.04)',
        }}>
          <span style={{
            width: 6,
            height: 6,
            borderRadius: '50%',
            background: HEALTH_COLORS[v.health],
            flexShrink: 0,
          }} />
          <span style={{ color: '#e6e6e6', flex: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
            {v.name}
          </span>
          <span style={{
            color: HEALTH_COLORS[v.health],
            fontSize: 8,
            textTransform: 'uppercase',
            fontWeight: 'bold',
            flexShrink: 0,
          }}>
            {v.health}
          </span>
        </div>
      ))}
      {violations.length > 15 && (
        <div style={{ color: '#6b7280', fontSize: 9, padding: '4px 0' }}>
          +{violations.length - 15} more
        </div>
      )}
    </div>
  );
};

// ---------------------------------------------------------------------------
// Section wrapper (collapsible)
// ---------------------------------------------------------------------------

const Section: React.FC<{
  title: string;
  badge?: string;
  badgeColor?: string;
  defaultOpen?: boolean;
  children: React.ReactNode;
}> = ({ title, badge, badgeColor, defaultOpen = true, children }) => {
  const [open, setOpen] = useState(defaultOpen);

  return (
    <div style={{ marginBottom: 2 }}>
      <div
        onClick={() => setOpen(!open)}
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: 6,
          padding: '6px 0',
          cursor: 'pointer',
          fontSize: 11,
          fontWeight: 'bold',
          color: '#c9d1d9',
          borderBottom: '1px solid rgba(255,255,255,0.06)',
          userSelect: 'none',
        }}
      >
        <span style={{ fontSize: 8, color: '#6b7280' }}>{open ? '\u25BC' : '\u25B6'}</span>
        <span>{title}</span>
        {badge && (
          <span style={{
            fontSize: 9,
            fontWeight: 'normal',
            color: badgeColor ?? '#6b7280',
            marginLeft: 'auto',
          }}>
            {badge}
          </span>
        )}
      </div>
      {open && <div style={{ padding: '6px 0 4px 0' }}>{children}</div>}
    </div>
  );
};

// ---------------------------------------------------------------------------
// Main Component
// ---------------------------------------------------------------------------

export const GovernanceCompliancePanel: React.FC = () => {
  const [data, setData] = useState<ComplianceData | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastRefresh, setLastRefresh] = useState<string | null>(null);
  const intervalRef = useRef<number | null>(null);

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [graphRes, beliefsRes] = await Promise.allSettled([
        fetch('/api/governance'),
        fetch('/api/governance/beliefs'),
      ]);

      let graph: GovernanceGraph | null = null;
      let beliefs: BeliefState[] = [];

      if (graphRes.status === 'fulfilled' && graphRes.value.ok) {
        graph = await graphRes.value.json();
      }
      if (beliefsRes.status === 'fulfilled' && beliefsRes.value.ok) {
        const beliefsData = await beliefsRes.value.json();
        beliefs = Array.isArray(beliefsData) ? beliefsData : (beliefsData.beliefs ?? []);
      }

      if (!graph) {
        setError('Governance API unavailable');
        setData(null);
      } else {
        setData(processData(graph, beliefs));
        setLastRefresh(new Date().toLocaleTimeString());
      }
    } catch (err) {
      setError('Governance API unavailable');
      setData(null);
    } finally {
      setLoading(false);
    }
  }, []);

  // Initial fetch + auto-refresh every 60s
  useEffect(() => {
    fetchData();
    intervalRef.current = window.setInterval(fetchData, REFRESH_INTERVAL_MS);
    return () => {
      if (intervalRef.current) window.clearInterval(intervalRef.current);
    };
  }, [fetchData]);

  // Error / unavailable state
  if (error && !data) {
    return (
      <div style={{
        fontFamily: "'JetBrains Mono', monospace",
        fontSize: 11,
        color: '#e6edf3',
        height: '100%',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        gap: 12,
        padding: 20,
      }}>
        <span style={{ color: '#f44336', fontSize: 13, fontWeight: 'bold' }}>
          Governance API unavailable
        </span>
        <span style={{ color: '#6b7280', fontSize: 10, textAlign: 'center' }}>
          Could not reach /api/governance. Ensure the backend is running.
        </span>
        <button
          onClick={fetchData}
          disabled={loading}
          style={{
            background: 'rgba(255,215,0,0.1)',
            border: '1px solid rgba(255,215,0,0.3)',
            borderRadius: 4,
            color: '#ffd700',
            cursor: loading ? 'wait' : 'pointer',
            fontSize: 10,
            fontWeight: 'bold',
            padding: '6px 16px',
          }}
        >
          {loading ? 'Retrying...' : 'Retry'}
        </button>
      </div>
    );
  }

  return (
    <div style={{
      fontFamily: "'JetBrains Mono', monospace",
      fontSize: 11,
      color: '#e6edf3',
      height: '100%',
      display: 'flex',
      flexDirection: 'column',
      overflow: 'hidden',
    }}>
      {/* Header */}
      <div style={{
        display: 'flex',
        alignItems: 'center',
        gap: 8,
        padding: '10px 12px',
        borderBottom: '1px solid rgba(255,255,255,0.06)',
        background: 'rgba(0,0,0,0.3)',
        flexShrink: 0,
      }}>
        <span style={{ fontWeight: 'bold', fontSize: 13 }}>Compliance</span>
        {data && (
          <span style={{
            background: data.overall.percentage >= 80
              ? 'rgba(76,175,80,0.15)'
              : data.overall.percentage >= 50
                ? 'rgba(255,152,0,0.15)'
                : 'rgba(244,67,54,0.15)',
            color: data.overall.percentage >= 80
              ? '#4caf50'
              : data.overall.percentage >= 50
                ? '#ff9800'
                : '#f44336',
            padding: '2px 8px',
            borderRadius: 4,
            fontSize: 10,
            fontWeight: 'bold',
          }}>
            {data.overall.percentage}%
          </span>
        )}
        <div style={{ flex: 1 }} />
        <button
          onClick={fetchData}
          disabled={loading}
          style={{
            background: 'rgba(255,215,0,0.1)',
            border: '1px solid rgba(255,215,0,0.3)',
            borderRadius: 4,
            color: '#ffd700',
            cursor: loading ? 'wait' : 'pointer',
            fontSize: 10,
            fontWeight: 'bold',
            padding: '3px 10px',
          }}
        >
          {loading ? '...' : 'Refresh'}
        </button>
      </div>

      {/* Body */}
      <div style={{ flex: 1, overflow: 'auto', padding: '6px 12px' }}>
        {!data ? (
          <div style={{ color: '#6b7280', fontSize: 10, padding: 8 }}>Loading...</div>
        ) : (
          <>
            {/* Overall Compliance */}
            <Section title="Overall Compliance" badge={`${data.overall.total} nodes`}>
              <ScoreBar score={data.overall} />
              <HealthBreakdown score={data.overall} />
            </Section>

            {/* Per-Type Breakdown */}
            <Section
              title="By Type"
              badge={`${data.byType.length} types`}
              defaultOpen={true}
            >
              {data.byType.map(score => (
                <ScoreBar key={score.label} score={score} compact />
              ))}
            </Section>

            {/* Belief Distribution */}
            <Section
              title="Belief Distribution"
              badge={`${data.beliefs.total} beliefs`}
              defaultOpen={true}
            >
              <BeliefDistributionBar dist={data.beliefs} />
            </Section>

            {/* Violations */}
            <Section
              title="Violations"
              badge={`${data.violations.length}`}
              badgeColor={data.violations.length > 0 ? '#f44336' : '#4caf50'}
              defaultOpen={data.violations.length > 0}
            >
              <ViolationList violations={data.violations} />
            </Section>
          </>
        )}
      </div>

      {/* Footer */}
      {lastRefresh && (
        <div style={{
          padding: '6px 12px',
          borderTop: '1px solid rgba(255,255,255,0.06)',
          fontSize: 9,
          color: '#6b7280',
          flexShrink: 0,
          display: 'flex',
          justifyContent: 'space-between',
        }}>
          <span>Last: {lastRefresh}</span>
          <span>Auto-refresh 60s</span>
        </div>
      )}
    </div>
  );
};
