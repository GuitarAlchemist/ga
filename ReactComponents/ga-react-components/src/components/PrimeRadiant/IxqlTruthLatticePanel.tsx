// src/components/PrimeRadiant/IxqlTruthLatticePanel.tsx
// Truth Lattice Panel — visualizes how beliefs move through hexavalent states.
// Shows: state distribution, transition history, decay indicators, lattice graph.
// Driven by CREATE VIZ KIND truth-lattice SOURCE governance.beliefs
//
// The lattice: T ↔ P ↔ U ↔ D ↔ F, with C reachable from any state.
// Each node in the lattice shows how many beliefs are in that state.
// Edges show recent transitions (thicker = more transitions).

import React, { useEffect, useState, useCallback, useRef, useMemo } from 'react';
import type { VizSpec } from './IxqlWidgetSpec';
import { resolve, resolveField } from './DataFetcher';
import type { GraphContext } from './DataFetcher';
import { executePipeline } from './IxqlPipeEngine';
import { useSignals, usePublish, signalBus } from './DashboardSignalBus';
import type { DashboardSignal } from './DashboardSignalBus';
import type { HexavalentValue, HexavalentTransitionEvent } from './HexavalentTemporal';

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const HEX_COLORS: Record<HexavalentValue, string> = {
  T: '#22c55e', P: '#a3e635', U: '#6b7280',
  D: '#f97316', F: '#ef4444', C: '#d946ef',
};

const HEX_LABELS: Record<HexavalentValue, string> = {
  T: 'True', P: 'Probable', U: 'Unknown',
  D: 'Doubtful', F: 'False', C: 'Contradictory',
};

const ALL_VALUES: HexavalentValue[] = ['T', 'P', 'U', 'D', 'F', 'C'];

// Lattice layout — positions for the hexavalent states in SVG space
// Linear chain T-P-U-D-F with C floating above center
const LATTICE_POSITIONS: Record<HexavalentValue, { x: number; y: number }> = {
  T: { x: 60, y: 160 },
  P: { x: 160, y: 160 },
  U: { x: 260, y: 160 },
  D: { x: 360, y: 160 },
  F: { x: 460, y: 160 },
  C: { x: 260, y: 60 },
};

// Allowed transitions (edges in the lattice)
const LATTICE_EDGES: [HexavalentValue, HexavalentValue][] = [
  ['T', 'P'], ['P', 'U'], ['U', 'D'], ['D', 'F'],
  ['T', 'C'], ['P', 'C'], ['U', 'C'], ['D', 'C'], ['F', 'C'],
];

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------

export interface IxqlTruthLatticePanelProps {
  spec: VizSpec;
  graphContext?: GraphContext;
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export const IxqlTruthLatticePanel: React.FC<IxqlTruthLatticePanelProps> = ({ spec, graphContext }) => {
  const [data, setData] = useState<Record<string, unknown>[]>([]);
  const [loading, setLoading] = useState(true);
  const [transitions, setTransitions] = useState<HexavalentTransitionEvent[]>([]);
  const containerRef = useRef<HTMLDivElement>(null);
  const publishSignal = usePublish(spec.id);
  const subscribedSignals = useSignals([...spec.subscribe, 'belief:transition']);

  // Fetch data
  const fetchData = useCallback(async () => {
    try {
      const raw = await resolve(spec.binding.source, spec.binding.wherePredicates, graphContext);
      let rows = raw as Record<string, unknown>[];
      if (spec.pipeline) {
        rows = executePipeline(rows, spec.pipeline.steps);
      }
      setData(rows);
    } catch {
      // Graceful degradation — show empty lattice
      setData([]);
    } finally {
      setLoading(false);
    }
  }, [spec.binding.source, spec.binding.wherePredicates, spec.pipeline, graphContext]);

  useEffect(() => {
    fetchData();
    if (spec.refresh && spec.refresh > 0) {
      const interval = setInterval(fetchData, spec.refresh);
      return () => clearInterval(interval);
    }
  }, [fetchData, spec.refresh]);

  // Listen for belief:transition events
  useEffect(() => {
    const unsub = signalBus.subscribe(() => {
      const sig = signalBus.get('belief:transition');
      if (sig) {
        const event = sig.value as HexavalentTransitionEvent;
        if (event && event.fromValue && event.toValue) {
          setTransitions(prev => {
            const next = [...prev, event];
            // Keep last 50 transitions
            return next.length > 50 ? next.slice(-50) : next;
          });
        }
      }
    });
    return unsub;
  }, []);

  // Compute state distribution from data
  const distribution = useMemo(() => {
    const dist: Record<HexavalentValue, number> = { T: 0, P: 0, U: 0, D: 0, F: 0, C: 0 };
    const truthField = spec.colorField ?? 'truth_value';
    for (const row of data) {
      const val = String(resolveField(row, truthField) ?? 'U').toUpperCase();
      if (val in dist) dist[val as HexavalentValue]++;
    }
    return dist;
  }, [data, spec.colorField]);

  // Compute edge weights from recent transitions
  const edgeWeights = useMemo(() => {
    const weights = new Map<string, number>();
    for (const t of transitions) {
      const key = `${t.fromValue}-${t.toValue}`;
      weights.set(key, (weights.get(key) ?? 0) + 1);
    }
    return weights;
  }, [transitions]);

  const totalBeliefs = useMemo(() =>
    ALL_VALUES.reduce((sum, v) => sum + distribution[v], 0),
  [distribution]);

  const maxEdgeWeight = useMemo(() => {
    let max = 1;
    for (const w of edgeWeights.values()) {
      if (w > max) max = w;
    }
    return max;
  }, [edgeWeights]);

  if (loading) {
    return (
      <div className="ixql-viz-panel ixql-viz-panel--loading">
        <div className="ixql-viz-panel__header">
          <span className="ixql-viz-panel__title">{spec.id}</span>
        </div>
        <div style={{ padding: 24, color: '#8b949e', fontSize: 12 }}>Loading lattice...</div>
      </div>
    );
  }

  return (
    <div className="ixql-viz-panel" ref={containerRef}>
      <div className="ixql-viz-panel__header">
        <span className="ixql-viz-panel__title">{spec.id}</span>
        <span style={{ fontSize: 10, color: '#6b7280', marginLeft: 'auto' }}>
          truth-lattice | {totalBeliefs} beliefs | {transitions.length} transitions
        </span>
      </div>

      <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
        {/* SVG Lattice */}
        <svg
          viewBox="0 0 520 220"
          style={{
            flex: 2,
            background: 'rgba(13, 17, 23, 0.95)',
            minHeight: 200,
          }}
        >
          {/* Edges */}
          {LATTICE_EDGES.map(([from, to]) => {
            const p1 = LATTICE_POSITIONS[from];
            const p2 = LATTICE_POSITIONS[to];
            const fwdKey = `${from}-${to}`;
            const bwdKey = `${to}-${from}`;
            const weight = (edgeWeights.get(fwdKey) ?? 0) + (edgeWeights.get(bwdKey) ?? 0);
            const normalizedWeight = weight / maxEdgeWeight;
            return (
              <line
                key={fwdKey}
                x1={p1.x} y1={p1.y}
                x2={p2.x} y2={p2.y}
                stroke={weight > 0 ? '#ffd700' : '#30363d'}
                strokeWidth={1 + normalizedWeight * 3}
                strokeOpacity={weight > 0 ? 0.4 + normalizedWeight * 0.6 : 0.2}
              />
            );
          })}

          {/* Nodes */}
          {ALL_VALUES.map(value => {
            const pos = LATTICE_POSITIONS[value];
            const count = distribution[value];
            const radius = 18 + Math.min(20, count * 2);
            const isC = value === 'C';
            return (
              <g key={value}>
                {/* Glow for C */}
                {isC && count > 0 && (
                  <circle
                    cx={pos.x} cy={pos.y}
                    r={radius + 8}
                    fill="none"
                    stroke={HEX_COLORS.C}
                    strokeWidth={2}
                    strokeOpacity={0.3}
                    className="hex-cell--contradictory"
                  />
                )}
                {/* Node circle */}
                <circle
                  cx={pos.x} cy={pos.y}
                  r={radius}
                  fill={count > 0 ? HEX_COLORS[value] : '#1c2128'}
                  fillOpacity={count > 0 ? 0.2 : 0.05}
                  stroke={HEX_COLORS[value]}
                  strokeWidth={count > 0 ? 2 : 1}
                  strokeOpacity={count > 0 ? 0.8 : 0.3}
                />
                {/* Label */}
                <text
                  x={pos.x} y={pos.y - 6}
                  textAnchor="middle"
                  fill={HEX_COLORS[value]}
                  fontSize={14}
                  fontWeight="bold"
                  fontFamily="'JetBrains Mono', monospace"
                >
                  {value}
                </text>
                {/* Count */}
                <text
                  x={pos.x} y={pos.y + 12}
                  textAnchor="middle"
                  fill={count > 0 ? '#e6edf3' : '#6b7280'}
                  fontSize={11}
                  fontFamily="'JetBrains Mono', monospace"
                >
                  {count}
                </text>
              </g>
            );
          })}
        </svg>

        {/* Transition Log */}
        <div style={{
          flex: 1,
          minWidth: 180,
          maxHeight: 220,
          overflow: 'auto',
          borderLeft: '1px solid #30363d',
          padding: '8px 10px',
          fontSize: 10,
          fontFamily: "'JetBrains Mono', monospace",
          color: '#8b949e',
          background: 'rgba(13, 17, 23, 0.8)',
        }}>
          <div style={{ fontWeight: 'bold', marginBottom: 6, color: '#e6edf3', fontSize: 11 }}>
            Transitions ({transitions.length})
          </div>
          {transitions.length === 0 && (
            <div style={{ color: '#6b7280', fontStyle: 'italic' }}>No transitions yet</div>
          )}
          {transitions.slice(-20).reverse().map((t, i) => (
            <div key={i} style={{ marginBottom: 3, display: 'flex', gap: 4, alignItems: 'center' }}>
              <span style={{ color: HEX_COLORS[t.fromValue], fontWeight: 'bold' }}>{t.fromValue}</span>
              <span style={{ color: '#6b7280' }}>&rarr;</span>
              <span style={{ color: HEX_COLORS[t.toValue], fontWeight: 'bold' }}>{t.toValue}</span>
              <span style={{ color: '#484f58', fontSize: 9, marginLeft: 'auto' }}>
                {t.actor}
              </span>
            </div>
          ))}
        </div>
      </div>

      {/* Distribution bar */}
      <div style={{
        display: 'flex',
        height: 6,
        borderTop: '1px solid rgba(255,255,255,0.06)',
      }}>
        {ALL_VALUES.map(value => {
          const pct = totalBeliefs > 0 ? (distribution[value] / totalBeliefs) * 100 : 0;
          if (pct === 0) return null;
          return (
            <div
              key={value}
              style={{
                width: `${pct}%`,
                backgroundColor: HEX_COLORS[value],
                opacity: 0.7,
              }}
              title={`${HEX_LABELS[value]}: ${distribution[value]} (${Math.round(pct)}%)`}
            />
          );
        })}
      </div>
    </div>
  );
};
