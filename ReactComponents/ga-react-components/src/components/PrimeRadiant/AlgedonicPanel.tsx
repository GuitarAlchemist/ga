// src/components/PrimeRadiant/AlgedonicPanel.tsx
// Algedonic signal panel — real-time pain/pleasure governance alerts

import React, { useState } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------
export type AlgedonicSignalType = 'pain' | 'pleasure';
export type AlgedonicSeverity = 'emergency' | 'warning' | 'info';
export type AlgedonicStatus = 'active' | 'acknowledged' | 'resolved';

export interface AlgedonicSignal {
  id: string;
  timestamp: string;
  signal: string;
  type: AlgedonicSignalType;
  source: string;
  severity: AlgedonicSeverity;
  status: AlgedonicStatus;
  description?: string;
}

// ---------------------------------------------------------------------------
// Mock data — realistic governance algedonic signals
// ---------------------------------------------------------------------------
const mockAlgedonicSignals: AlgedonicSignal[] = [
  {
    id: 'alg-001',
    timestamp: '2026-03-26T14:30:00Z',
    signal: 'belief_collapse',
    type: 'pain',
    source: 'tars',
    severity: 'emergency',
    status: 'resolved',
    description: 'Belief confidence dropped below 0.3 in music-theory domain',
  },
  {
    id: 'alg-002',
    timestamp: '2026-03-26T13:45:00Z',
    signal: 'policy_violation',
    type: 'pain',
    source: 'ga',
    severity: 'warning',
    status: 'acknowledged',
    description: 'Unbounded autonomy detected in chatbot loop iteration',
  },
  {
    id: 'alg-003',
    timestamp: '2026-03-26T12:00:00Z',
    signal: 'domain_convergence',
    type: 'pleasure',
    source: 'ix',
    severity: 'info',
    status: 'acknowledged',
    description: 'Cross-model validation achieved 95% consensus on IxQL grammar',
  },
  {
    id: 'alg-004',
    timestamp: '2026-03-26T10:15:00Z',
    signal: 'resilience_recovery',
    type: 'pleasure',
    source: 'demerzel',
    severity: 'info',
    status: 'resolved',
    description: 'Resilience score recovered from 0.64 to 0.82 after red team fixes',
  },
  {
    id: 'alg-005',
    timestamp: '2026-03-25T22:30:00Z',
    signal: 'lolli_inflation',
    type: 'pain',
    source: 'demerzel',
    severity: 'warning',
    status: 'resolved',
    description: 'LOLLI count exceeded ERGOL — 12 artifacts without consumers',
  },
  {
    id: 'alg-006',
    timestamp: '2026-03-25T18:00:00Z',
    signal: 'schema_drift',
    type: 'pain',
    source: 'tars',
    severity: 'warning',
    status: 'resolved',
    description: 'Persona schema v2.1 incompatible with 3 consumer repos',
  },
  {
    id: 'alg-007',
    timestamp: '2026-03-25T15:00:00Z',
    signal: 'knowledge_harvest',
    type: 'pleasure',
    source: 'seldon',
    severity: 'info',
    status: 'resolved',
    description: 'Streeling harvest yielded 14 new course modules across 6 departments',
  },
  {
    id: 'alg-008',
    timestamp: '2026-03-25T09:15:00Z',
    signal: 'test_coverage_milestone',
    type: 'pleasure',
    source: 'ix',
    severity: 'info',
    status: 'resolved',
    description: '100% behavioral test coverage achieved for all governance policies',
  },
];

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------
const SEVERITY_COLOR: Record<AlgedonicSeverity, string> = {
  emergency: '#FF4444',
  warning: '#FFB300',
  info: '#33CC66',
};

const STATUS_ICON: Record<AlgedonicStatus, string> = {
  active: '\u25CF',     // filled circle
  acknowledged: '\u25CB', // hollow circle
  resolved: '\u2713',    // checkmark
};

const SOURCE_COLOR: Record<string, string> = {
  tars: '#4FC3F7',
  ix: '#73d13d',
  ga: '#FFB300',
  demerzel: '#FFD700',
  seldon: '#c4b5fd',
};

function timeAgo(dateStr: string): string {
  const seconds = Math.floor((Date.now() - new Date(dateStr).getTime()) / 1000);
  if (seconds < 60) return `${seconds}s ago`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
export interface AlgedonicPanelProps {
  /** Live signals from SignalR. If provided, used instead of mock data. */
  signals?: AlgedonicSignal[];
}

export const AlgedonicPanel: React.FC<AlgedonicPanelProps> = ({ signals: signalsProp }) => {
  const [collapsed, setCollapsed] = useState(false);
  const [filter, setFilter] = useState<'all' | 'pain' | 'pleasure'>('all');

  const allSignals = signalsProp && signalsProp.length > 0 ? signalsProp : mockAlgedonicSignals;

  const signals = allSignals.filter(
    (s) => filter === 'all' || s.type === filter,
  );

  const painCount = allSignals.filter((s) => s.type === 'pain').length;
  const pleasureCount = allSignals.filter((s) => s.type === 'pleasure').length;
  const activeCount = allSignals.filter((s) => s.status === 'active').length;

  return (
    <div className="prime-radiant__algedonic">
      {/* Header */}
      <div
        className="prime-radiant__algedonic-header"
        onClick={() => setCollapsed(!collapsed)}
      >
        <span className="prime-radiant__algedonic-title">
          Algedonic
          <span className="prime-radiant__algedonic-counts">
            <span style={{ color: '#FF4444' }}>{painCount}P</span>
            {' / '}
            <span style={{ color: '#33CC66' }}>{pleasureCount}G</span>
            {activeCount > 0 && (
              <span className="prime-radiant__algedonic-active">
                {activeCount} active
              </span>
            )}
          </span>
        </span>
        <span className="prime-radiant__algedonic-toggle">
          {collapsed ? '\u25B6' : '\u25BC'}
        </span>
      </div>

      {!collapsed && (
        <>
          {/* Filter bar */}
          <div className="prime-radiant__algedonic-filters">
            {(['all', 'pain', 'pleasure'] as const).map((f) => (
              <button
                key={f}
                className={`prime-radiant__algedonic-filter${filter === f ? ' prime-radiant__algedonic-filter--active' : ''}`}
                onClick={() => setFilter(f)}
                style={{
                  color: f === 'pain' ? '#FF4444' : f === 'pleasure' ? '#33CC66' : '#8b949e',
                }}
              >
                {f === 'all' ? 'All' : f === 'pain' ? 'Pain' : 'Pleasure'}
              </button>
            ))}
          </div>

          {/* Timeline */}
          <div className="prime-radiant__algedonic-timeline">
            {signals.map((s) => (
              <div key={s.id} className="prime-radiant__algedonic-signal">
                {/* Pulse dot */}
                <span
                  className={`prime-radiant__algedonic-dot${s.status === 'active' ? ' prime-radiant__algedonic-dot--pulse' : ''}`}
                  style={{
                    backgroundColor: s.type === 'pain' ? '#FF4444' : '#33CC66',
                    boxShadow: s.status === 'active'
                      ? `0 0 6px ${s.type === 'pain' ? '#FF4444' : '#33CC66'}`
                      : 'none',
                  }}
                />

                {/* Signal info */}
                <div className="prime-radiant__algedonic-info">
                  <div className="prime-radiant__algedonic-signal-name">
                    <span style={{ color: SEVERITY_COLOR[s.severity] }}>
                      {s.signal.replace(/_/g, ' ')}
                    </span>
                    <span
                      className="prime-radiant__algedonic-source"
                      style={{ color: SOURCE_COLOR[s.source] ?? '#8b949e' }}
                    >
                      {s.source}
                    </span>
                  </div>
                  {s.description && (
                    <div className="prime-radiant__algedonic-desc">
                      {s.description}
                    </div>
                  )}
                  <div className="prime-radiant__algedonic-meta">
                    <span className="prime-radiant__algedonic-time">
                      {timeAgo(s.timestamp)}
                    </span>
                    <span
                      className="prime-radiant__algedonic-status"
                      style={{
                        color: s.status === 'active'
                          ? '#FF4444'
                          : s.status === 'acknowledged'
                            ? '#FFB300'
                            : '#484f58',
                      }}
                    >
                      {STATUS_ICON[s.status]} {s.status}
                    </span>
                    <span
                      className="prime-radiant__algedonic-severity"
                      style={{ color: SEVERITY_COLOR[s.severity] }}
                    >
                      {s.severity}
                    </span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </>
      )}
    </div>
  );
};
