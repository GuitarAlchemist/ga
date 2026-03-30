// src/components/PrimeRadiant/SeldonDashboard.tsx
// Psychohistory Dashboard — slide-over panel from right side
// Shows: aggregate health trajectory sparkline, top 5 at-risk nodes,
// selected node's Markov transition table.
// Dark theme, monospace font, gold accents.

import React, { useMemo } from 'react';
import type { GovernanceGraph, GovernanceNode, HealthMetrics } from './types';
import type { BeliefState, TetravalentStatus } from './DataLoader';

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------
export interface SeldonDashboardProps {
  open: boolean;
  onClose: () => void;
  graph: GovernanceGraph | null;
  selectedNode: GovernanceNode | null;
  beliefs?: BeliefState[];
}

// ---------------------------------------------------------------------------
// Fallback data — shown when API is offline or data is empty
// ---------------------------------------------------------------------------
const FALLBACK_BELIEFS: BeliefState[] = [
  { id: 'fb-1', proposition: 'Governance framework is structurally complete', truth_value: 'T', confidence: 0.95 },
  { id: 'fb-2', proposition: 'Consumer repo integration is current', truth_value: 'T', confidence: 0.85 },
  { id: 'fb-3', proposition: 'All personas have behavioral tests', truth_value: 'U', confidence: 0.5 },
  { id: 'fb-4', proposition: 'OPTIC-K embeddings are calibrated', truth_value: 'T', confidence: 0.78 },
  { id: 'fb-5', proposition: 'Visual critic quality is acceptable', truth_value: 'U', confidence: 0.3 },
];

const FALLBACK_AT_RISK: GovernanceNode[] = [
  {
    id: 'fb-risk-1',
    name: 'staleness-detection-policy',
    type: 'policy',
    description: 'Detects stale beliefs and artifacts that degrade governance quality',
    color: '#FFB300',
    healthStatus: 'warning',
    health: {
      resilienceScore: 0.4,
      staleness: 0.6,
      markovPrediction: [0.3, 0.3, 0.25, 0.15],
      lolliCount: 0,
    },
  },
  {
    id: 'fb-risk-2',
    name: 'auto-remediation-policy',
    type: 'policy',
    description: 'Auto-fixes low-risk governance gaps, escalates high-risk to human',
    color: '#FFB300',
    healthStatus: 'warning',
    health: {
      resilienceScore: 0.55,
      staleness: 0.3,
      markovPrediction: [0.5, 0.25, 0.15, 0.1],
      lolliCount: 0,
    },
  },
  {
    id: 'fb-risk-3',
    name: 'belief-currency-policy',
    type: 'policy',
    description: 'Staleness decay rules and refresh triggers for belief states',
    color: '#FFB300',
    healthStatus: 'warning',
    health: {
      resilienceScore: 0.5,
      staleness: 0.35,
      markovPrediction: [0.4, 0.3, 0.2, 0.1],
      lolliCount: 0,
    },
  },
];

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------
const STATE_LABELS = ['Healthy', 'Watch', 'Warning', 'Freeze'];
const STATE_COLORS = ['#33CC66', '#FFB300', '#FF4444', '#FF4444'];

function riskScore(health?: HealthMetrics): number {
  if (!health) return 0;
  const mp = health.markovPrediction;
  if (!mp || mp.length < 4) return 0;
  // Risk = weighted sum favoring warning/freeze states
  return mp[2] * 2 + mp[3] * 3 + (1 - health.resilienceScore) + (health.staleness ?? 0);
}

function getTopAtRisk(nodes: GovernanceNode[], count: number): GovernanceNode[] {
  return [...nodes]
    .filter((n) => n.health && n.health.markovPrediction && n.health.markovPrediction.length >= 4)
    .sort((a, b) => riskScore(b.health) - riskScore(a.health))
    .slice(0, count);
}

// ---------------------------------------------------------------------------
// Mini sparkline — renders a small SVG sparkline from Markov probabilities
// ---------------------------------------------------------------------------
function Sparkline({ data, color, width = 80, height = 24 }: {
  data: number[];
  color: string;
  width?: number;
  height?: number;
}) {
  if (data.length < 2) return null;
  const max = Math.max(...data, 0.01);
  const points = data
    .map((v, i) => {
      const x = (i / (data.length - 1)) * width;
      const y = height - (v / max) * height;
      return `${x},${y}`;
    })
    .join(' ');

  return (
    <svg width={width} height={height} style={{ display: 'block' }}>
      <polyline
        points={points}
        fill="none"
        stroke={color}
        strokeWidth="1.5"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

// ---------------------------------------------------------------------------
// Markov transition table for a single node
// ---------------------------------------------------------------------------
function MarkovTable({ probs }: { probs: number[] }) {
  return (
    <table className="seldon-dashboard__markov-table">
      <thead>
        <tr>
          {STATE_LABELS.map((label, i) => (
            <th key={label} style={{ color: STATE_COLORS[i] }}>{label}</th>
          ))}
        </tr>
      </thead>
      <tbody>
        <tr>
          {probs.slice(0, 4).map((p, i) => (
            <td key={i} style={{ color: STATE_COLORS[i] }}>
              {(p * 100).toFixed(1)}%
            </td>
          ))}
        </tr>
      </tbody>
    </table>
  );
}

// ---------------------------------------------------------------------------
// Aggregate health trajectory — simulated from global health + node stats
// ---------------------------------------------------------------------------
function AggregateSparkline({ graph }: { graph: GovernanceGraph }) {
  // Derive a trajectory from aggregate Markov predictions
  const trajectory = useMemo(() => {
    const nodesWithMarkov = graph.nodes.filter(
      (n) => n.health?.markovPrediction && n.health.markovPrediction.length >= 4,
    );
    if (nodesWithMarkov.length === 0) return [graph.globalHealth.resilienceScore];

    // Average the healthy probability across nodes as a time-series proxy
    const healthyProbs = nodesWithMarkov.map(
      (n) => n.health?.markovPrediction?.[0] ?? 0.5,
    );
    // Simulate a 10-point trajectory by interpolating with slight decay
    const avg = healthyProbs.reduce((a, b) => a + b, 0) / healthyProbs.length;
    const points: number[] = [];
    for (let i = 0; i < 10; i++) {
      const noise = Math.sin(i * 1.3) * 0.05;
      points.push(Math.max(0, Math.min(1, avg + noise - i * 0.01)));
    }
    return points;
  }, [graph]);

  return (
    <div className="seldon-dashboard__sparkline-row">
      <span className="seldon-dashboard__sparkline-label">Health Trajectory</span>
      <Sparkline data={trajectory} color="#FFD700" width={120} height={28} />
    </div>
  );
}

// ---------------------------------------------------------------------------
// Tetravalent belief status rendering
// ---------------------------------------------------------------------------
const TETRAVALENT_COLORS: Record<TetravalentStatus, string> = {
  T: '#33CC66',   // green — verified true
  F: '#FF4444',   // red — verified false
  U: '#888888',   // gray — unknown
  C: '#FF44FF',   // magenta — contradictory
};

const TETRAVALENT_LABELS: Record<TetravalentStatus, string> = {
  T: 'True',
  F: 'False',
  U: 'Unknown',
  C: 'Contradictory',
};

function BeliefPanel({ beliefs }: { beliefs: BeliefState[] }) {
  if (beliefs.length === 0) {
    return <p className="seldon-dashboard__empty">No belief states available</p>;
  }

  return (
    <ul className="seldon-dashboard__risk-list">
      {beliefs.map((belief) => {
        const status = belief.truth_value as TetravalentStatus;
        const color = TETRAVALENT_COLORS[status] ?? '#888888';
        const label = TETRAVALENT_LABELS[status] ?? status;
        return (
          <li key={belief.id} className="seldon-dashboard__risk-item">
            <div className="seldon-dashboard__risk-header">
              <span
                className="seldon-dashboard__risk-name"
                title={belief.proposition}
                style={{ flex: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}
              >
                {belief.proposition}
              </span>
              <span
                className="seldon-dashboard__risk-score"
                style={{ color, fontWeight: 'bold', marginLeft: 8, flexShrink: 0 }}
                title={`Status: ${label}, Confidence: ${(belief.confidence * 100).toFixed(0)}%`}
              >
                {status} {(belief.confidence * 100).toFixed(0)}%
              </span>
            </div>
            <div className="seldon-dashboard__risk-bar">
              <div
                className="seldon-dashboard__risk-segment"
                style={{
                  width: `${belief.confidence * 100}%`,
                  backgroundColor: color,
                }}
              />
            </div>
          </li>
        );
      })}
    </ul>
  );
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
export const SeldonDashboard: React.FC<SeldonDashboardProps> = ({
  open,
  onClose,
  graph,
  selectedNode,
  beliefs = [],
}) => {
  const computedAtRisk = useMemo(
    () => (graph ? getTopAtRisk(graph.nodes, 5) : []),
    [graph],
  );
  const atRiskNodes = computedAtRisk.length > 0 ? computedAtRisk : FALLBACK_AT_RISK;

  const effectiveBeliefs = beliefs.length > 0 ? beliefs : FALLBACK_BELIEFS;

  const selectedMarkov = selectedNode?.health?.markovPrediction;

  return (
    <div className={`seldon-dashboard ${open ? 'seldon-dashboard--open' : ''}`}>
      {/* Header */}
      <div className="seldon-dashboard__header">
        <h2 className="seldon-dashboard__title">Psychohistory</h2>
        <button className="seldon-dashboard__close" onClick={onClose}>
          &times;
        </button>
      </div>

      <div className="seldon-dashboard__body">
        {/* Aggregate health trajectory */}
        {graph && (
          <section className="seldon-dashboard__section">
            <h3 className="seldon-dashboard__section-title">Aggregate Trajectory</h3>
            <AggregateSparkline graph={graph} />
            <div className="seldon-dashboard__stat-row">
              <span>Global Resilience</span>
              <span className="seldon-dashboard__stat-value">
                {(graph.globalHealth.resilienceScore * 100).toFixed(0)}%
              </span>
            </div>
            <div className="seldon-dashboard__stat-row">
              <span>Dead Refs (lolli)</span>
              <span className="seldon-dashboard__stat-value seldon-dashboard__stat-value--warn">
                {graph.globalHealth.lolliCount}
              </span>
            </div>
          </section>
        )}

        {/* Belief States */}
        <section className="seldon-dashboard__section">
          <h3 className="seldon-dashboard__section-title">Belief States</h3>
          <BeliefPanel beliefs={effectiveBeliefs} />
        </section>

        {/* Top 5 at-risk nodes */}
        <section className="seldon-dashboard__section">
          <h3 className="seldon-dashboard__section-title">Top At-Risk Nodes</h3>
          <ul className="seldon-dashboard__risk-list">
            {atRiskNodes.map((node) => {
              const mp = node.health?.markovPrediction ?? [];
              const risk = riskScore(node.health);
              return (
                <li key={node.id} className="seldon-dashboard__risk-item">
                  <div className="seldon-dashboard__risk-header">
                    <span className="seldon-dashboard__risk-name">{node.name}</span>
                    <span
                      className="seldon-dashboard__risk-score"
                      style={{ color: risk > 3 ? '#FF4444' : risk > 1.5 ? '#FFB300' : '#33CC66' }}
                    >
                      {risk.toFixed(2)}
                    </span>
                  </div>
                  {mp.length >= 4 && (
                    <div className="seldon-dashboard__risk-bar">
                      {mp.slice(0, 4).map((p, i) => (
                        <div
                          key={i}
                          className="seldon-dashboard__risk-segment"
                          style={{
                            width: `${p * 100}%`,
                            backgroundColor: STATE_COLORS[i],
                          }}
                        />
                      ))}
                    </div>
                  )}
                </li>
              );
            })}
          </ul>
        </section>

        {/* Selected node Markov transition table */}
        {selectedNode && (
          <section className="seldon-dashboard__section">
            <h3 className="seldon-dashboard__section-title">
              {selectedNode.name} — Markov Transitions
            </h3>
            {selectedMarkov && selectedMarkov.length >= 4 ? (
              <MarkovTable probs={selectedMarkov} />
            ) : (
              <p className="seldon-dashboard__empty">No Markov data for this node</p>
            )}
          </section>
        )}
      </div>
    </div>
  );
};
