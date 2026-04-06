// src/components/PrimeRadiant/AgentSpectralPanel.tsx
// Visualizes agent interaction topology from /api/spectral/agent-loop

import React, { useEffect, useState, useCallback, useRef } from 'react';

// ---------------------------------------------------------------------------
// Types — mirror the C# models
// ---------------------------------------------------------------------------
interface AgentNode {
  id: string;
  displayName: string;
  weight: number;
  signals: Record<string, number>;
}

interface AgentEdge {
  source: string;
  target: string;
  weight: number;
  features: Record<string, number>;
}

interface SpectralMetrics {
  eigenvalues: number[];
  algebraicConnectivity: number;
  spectralGap: number | null;
  spectralRadius: number;
  degreeDistribution: number[];
  centrality: Record<string, number>;
}

interface TopologySnapshot {
  agents: AgentNode[];
  edges: AgentEdge[];
  metrics: SpectralMetrics | null;
  isDemo: boolean;
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

const DEMO_AGENTS: AgentNode[] = [
  { id: 'demerzel', displayName: 'Demerzel', weight: 1.0, signals: { governance: 0.95, alignment: 0.92 } },
  { id: 'seldon',   displayName: 'Seldon',   weight: 0.9, signals: { knowledge: 0.88, teaching: 0.85 } },
  { id: 'tars',     displayName: 'TARS',     weight: 0.8, signals: { cognition: 0.82, reasoning: 0.79 } },
  { id: 'ix',       displayName: 'ix',        weight: 0.7, signals: { ml: 0.76, forge: 0.72 } },
  { id: 'claude',   displayName: 'Claude',   weight: 0.85, signals: { code: 0.9, review: 0.87 } },
  { id: 'codex',    displayName: 'Codex',    weight: 0.6, signals: { review: 0.7, testing: 0.65 } },
  { id: 'gemini',   displayName: 'Gemini',   weight: 0.65, signals: { research: 0.73, analysis: 0.68 } },
];

const DEMO_EDGES: AgentEdge[] = [
  { source: 'demerzel', target: 'seldon',  weight: 0.9, features: { directive: 0.8, compliance: 0.95 } },
  { source: 'demerzel', target: 'tars',    weight: 0.85, features: { directive: 0.75, compliance: 0.88 } },
  { source: 'demerzel', target: 'ix',      weight: 0.7, features: { directive: 0.6, compliance: 0.82 } },
  { source: 'demerzel', target: 'claude',  weight: 0.8, features: { orchestration: 0.85 } },
  { source: 'seldon',   target: 'tars',    weight: 0.6, features: { knowledge: 0.7 } },
  { source: 'claude',   target: 'codex',   weight: 0.5, features: { review: 0.6 } },
  { source: 'claude',   target: 'gemini',  weight: 0.45, features: { research: 0.55 } },
  { source: 'seldon',   target: 'claude',  weight: 0.55, features: { teaching: 0.65 } },
  { source: 'tars',     target: 'ix',      weight: 0.65, features: { ml_pipeline: 0.7 } },
];

const DEMO_METRICS: SpectralMetrics = {
  eigenvalues: [3.42, 1.87, 1.23, 0.78, 0.45, 0.15, -0.02],
  algebraicConnectivity: 0.45,
  spectralGap: 1.55,
  spectralRadius: 3.42,
  degreeDistribution: [0, 0, 2, 2, 2, 1],
  centrality: {
    demerzel: 0.95, seldon: 0.72, tars: 0.68, ix: 0.52,
    claude: 0.78, codex: 0.35, gemini: 0.32,
  },
};

async function fetchTopology(): Promise<TopologySnapshot> {
  try {
    const base = getApiBase();
    const body = {
      agents: DEMO_AGENTS.map(a => ({
        id: a.id,
        displayName: a.displayName,
        weight: a.weight,
        signals: a.signals,
      })),
      edges: DEMO_EDGES.map(e => ({
        source: e.source,
        target: e.target,
        weight: e.weight,
        features: e.features,
      })),
      isUndirected: true,
    };
    const res = await fetch(`${base}/api/spectral/agent-loop`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
      signal: AbortSignal.timeout(8000),
    });
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const metrics: SpectralMetrics = await res.json();
    return { agents: DEMO_AGENTS, edges: DEMO_EDGES, metrics, isDemo: false };
  } catch {
    return { agents: DEMO_AGENTS, edges: DEMO_EDGES, metrics: DEMO_METRICS, isDemo: true };
  }
}

// ---------------------------------------------------------------------------
// Layout — force-directed in 2D SVG
// ---------------------------------------------------------------------------
interface LayoutNode {
  id: string;
  displayName: string;
  x: number;
  y: number;
  vx: number;
  vy: number;
  radius: number;
  centrality: number;
  color: string;
}

const AGENT_COLORS: Record<string, string> = {
  demerzel: '#FFD700',
  seldon:   '#4FC3F7',
  tars:     '#4FC3F7',
  ix:       '#73d13d',
  claude:   '#c084fc',
  codex:    '#f97316',
  gemini:   '#34d399',
};

function layoutNodes(
  agents: AgentNode[],
  centrality: Record<string, number>,
  width: number,
  height: number,
): LayoutNode[] {
  const cx = width / 2;
  const cy = height / 2;
  const angleStep = (2 * Math.PI) / agents.length;
  const baseR = Math.min(width, height) * 0.32;

  return agents.map((a, i) => {
    const c = centrality[a.id] ?? 0.5;
    // Higher centrality = closer to center
    const dist = baseR * (1 - c * 0.6);
    const angle = angleStep * i - Math.PI / 2;
    return {
      id: a.id,
      displayName: a.displayName,
      x: cx + dist * Math.cos(angle),
      y: cy + dist * Math.sin(angle),
      vx: 0,
      vy: 0,
      radius: 8 + c * 16,
      centrality: c,
      color: AGENT_COLORS[a.id] ?? '#8b949e',
    };
  });
}

// Simple force simulation (few iterations at mount, no animation loop needed)
function applyForces(
  nodes: LayoutNode[],
  edges: AgentEdge[],
  width: number,
  height: number,
  iterations: number = 80,
): LayoutNode[] {
  const result = nodes.map(n => ({ ...n }));
  const nodeMap = new Map(result.map(n => [n.id, n]));

  for (let iter = 0; iter < iterations; iter++) {
    const alpha = 1 - iter / iterations;
    const repulsion = 2000 * alpha;
    const attraction = 0.005 * alpha;

    // Repulsion between all pairs
    for (let i = 0; i < result.length; i++) {
      for (let j = i + 1; j < result.length; j++) {
        const a = result[i];
        const b = result[j];
        const dx = a.x - b.x;
        const dy = a.y - b.y;
        const dist = Math.sqrt(dx * dx + dy * dy) || 1;
        const force = repulsion / (dist * dist);
        const fx = (dx / dist) * force;
        const fy = (dy / dist) * force;
        a.vx += fx; a.vy += fy;
        b.vx -= fx; b.vy -= fy;
      }
    }

    // Attraction along edges
    for (const e of edges) {
      const s = nodeMap.get(e.source);
      const t = nodeMap.get(e.target);
      if (!s || !t) continue;
      const dx = t.x - s.x;
      const dy = t.y - s.y;
      const dist = Math.sqrt(dx * dx + dy * dy) || 1;
      const force = attraction * dist * e.weight;
      const fx = (dx / dist) * force;
      const fy = (dy / dist) * force;
      s.vx += fx; s.vy += fy;
      t.vx -= fx; t.vy -= fy;
    }

    // Center gravity
    const cx = width / 2;
    const cy = height / 2;
    for (const n of result) {
      n.vx += (cx - n.x) * 0.01 * alpha;
      n.vy += (cy - n.y) * 0.01 * alpha;
    }

    // Apply velocity + damping
    for (const n of result) {
      n.x += n.vx * 0.4;
      n.y += n.vy * 0.4;
      n.vx *= 0.7;
      n.vy *= 0.7;
      // Clamp inside bounds
      n.x = Math.max(n.radius + 4, Math.min(width - n.radius - 4, n.x));
      n.y = Math.max(n.radius + 4, Math.min(height - n.radius - 4, n.y));
    }
  }

  return result;
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
export const AgentSpectralPanel: React.FC = () => {
  const [snapshot, setSnapshot] = useState<TopologySnapshot | null>(null);
  const [collapsed, setCollapsed] = useState(false);
  const [hovered, setHovered] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const svgRef = useRef<SVGSVGElement>(null);

  const SVG_W = 380;
  const SVG_H = 300;

  const refresh = useCallback(async () => {
    setRefreshing(true);
    const data = await fetchTopology();
    setSnapshot(data);
    setRefreshing(false);
  }, []);

  useEffect(() => {
    refresh();
    const id = setInterval(refresh, 30_000);
    return () => clearInterval(id);
  }, [refresh]);

  if (!snapshot) {
    return (
      <div className="prime-radiant__activity">
        <div className="prime-radiant__activity-header">
          <span className="prime-radiant__activity-title">Agent Topology</span>
        </div>
        <div style={{ padding: 16, color: '#6b7280', fontSize: '0.75rem' }}>Loading...</div>
      </div>
    );
  }

  const { agents, edges, metrics, isDemo } = snapshot;
  const centrality = metrics?.centrality ?? {};
  const initialNodes = layoutNodes(agents, centrality, SVG_W, SVG_H);
  const nodes = applyForces(initialNodes, edges, SVG_W, SVG_H);
  const nodeMap = new Map(nodes.map(n => [n.id, n]));

  // Summary line
  const connectivity = metrics?.algebraicConnectivity?.toFixed(2) ?? '?';
  const gap = metrics?.spectralGap != null ? metrics.spectralGap.toFixed(2) : '--';

  return (
    <div className="prime-radiant__activity" style={{ maxHeight: '70vh', overflowY: 'auto' }}>
      <div
        className="prime-radiant__activity-header"
        onClick={() => setCollapsed(!collapsed)}
      >
        <span className="prime-radiant__activity-title">
          Agent Topology
          <span
            className="spectral-status-dot"
            style={{
              display: 'inline-block',
              width: 8,
              height: 8,
              borderRadius: '50%',
              backgroundColor: isDemo ? '#eab308' : '#22c55e',
              marginLeft: 6,
              marginRight: 4,
              verticalAlign: 'middle',
              boxShadow: isDemo
                ? '0 0 4px rgba(234,179,8,0.6)'
                : '0 0 4px rgba(34,197,94,0.6)',
            }}
            title={isDemo ? 'Demo data — API unavailable' : 'Live — connected to API'}
          />
          <span style={{ fontSize: '0.6rem', color: isDemo ? '#eab308' : '#22c55e', marginRight: 6 }}>
            {isDemo ? 'Demo' : 'Live'}
          </span>
          <span className="prime-radiant__activity-count">
            {agents.length} agents &middot; {edges.length} edges
          </span>
        </span>
        <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <button
            className="spectral-refresh-btn"
            onClick={(e) => {
              e.stopPropagation();
              refresh();
            }}
            disabled={refreshing}
            title="Refresh topology"
            style={{
              background: 'none',
              border: '1px solid #30363d',
              borderRadius: 4,
              color: '#8b949e',
              cursor: refreshing ? 'wait' : 'pointer',
              padding: '2px 6px',
              fontSize: '0.65rem',
              fontFamily: "'JetBrains Mono', monospace",
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
        <div style={{ padding: '4px 0' }}>
          {/* SVG topology graph */}
          <svg
            ref={svgRef}
            viewBox={`0 0 ${SVG_W} ${SVG_H}`}
            className="spectral-topology-svg"
            style={{ width: '100%', height: 'auto', display: 'block' }}
          >
            {/* Background */}
            <rect width={SVG_W} height={SVG_H} fill="transparent" />

            {/* Edges */}
            {edges.map((e, i) => {
              const s = nodeMap.get(e.source);
              const t = nodeMap.get(e.target);
              if (!s || !t) return null;
              const isActive = hovered === e.source || hovered === e.target;
              const opacity = hovered
                ? (isActive ? 0.8 : 0.15)
                : 0.4;
              return (
                <line
                  key={`e-${i}`}
                  x1={s.x} y1={s.y}
                  x2={t.x} y2={t.y}
                  stroke={isActive ? '#FFD700' : '#30363d'}
                  strokeWidth={1 + e.weight * 2}
                  strokeOpacity={opacity}
                  strokeLinecap="round"
                />
              );
            })}

            {/* Nodes */}
            {nodes.map(n => {
              const isActive = hovered === n.id;
              const isConnected = hovered
                ? edges.some(e =>
                    (e.source === hovered && e.target === n.id) ||
                    (e.target === hovered && e.source === n.id)
                  )
                : false;
              const opacity = hovered
                ? (isActive ? 1 : isConnected ? 0.85 : 0.3)
                : 0.9;

              return (
                <g
                  key={n.id}
                  onMouseEnter={() => setHovered(n.id)}
                  onMouseLeave={() => setHovered(null)}
                  style={{ cursor: 'pointer' }}
                >
                  {/* Glow */}
                  {isActive && (
                    <circle
                      cx={n.x} cy={n.y} r={n.radius + 6}
                      fill="none"
                      stroke={n.color}
                      strokeWidth={2}
                      strokeOpacity={0.4}
                    />
                  )}
                  {/* Node circle */}
                  <circle
                    cx={n.x} cy={n.y} r={n.radius}
                    fill={n.color}
                    fillOpacity={opacity * 0.25}
                    stroke={n.color}
                    strokeWidth={isActive ? 2 : 1.2}
                    strokeOpacity={opacity}
                  />
                  {/* Label */}
                  <text
                    x={n.x} y={n.y + n.radius + 14}
                    textAnchor="middle"
                    fill={isActive ? n.color : '#8b949e'}
                    fontSize={isActive ? 11 : 9}
                    fontFamily="'JetBrains Mono', monospace"
                    fontWeight={isActive ? 600 : 400}
                    opacity={opacity}
                  >
                    {n.displayName}
                  </text>
                  {/* Centrality score inside node */}
                  <text
                    x={n.x} y={n.y + 3.5}
                    textAnchor="middle"
                    fill={n.color}
                    fontSize={8}
                    fontFamily="'JetBrains Mono', monospace"
                    fontWeight={600}
                    opacity={opacity * 0.9}
                  >
                    {(centrality[n.id] ?? 0).toFixed(2)}
                  </text>
                </g>
              );
            })}
          </svg>

          {/* Spectral metrics summary */}
          {metrics && (
            <div className="spectral-metrics-bar">
              <div className="spectral-metric">
                <span className="spectral-metric-label">Connectivity</span>
                <span className="spectral-metric-value">{connectivity}</span>
              </div>
              <div className="spectral-metric">
                <span className="spectral-metric-label">Spectral Gap</span>
                <span className="spectral-metric-value">{gap}</span>
              </div>
              <div className="spectral-metric">
                <span className="spectral-metric-label">Radius</span>
                <span className="spectral-metric-value">{metrics.spectralRadius.toFixed(2)}</span>
              </div>
              <div className="spectral-metric">
                <span className="spectral-metric-label">Eigenvalues</span>
                <span className="spectral-metric-value">{metrics.eigenvalues.length}</span>
              </div>
            </div>
          )}

          {/* Hovered agent detail */}
          {hovered && (() => {
            const agent = agents.find(a => a.id === hovered);
            if (!agent) return null;
            const c = centrality[hovered] ?? 0;
            const connCount = edges.filter(e => e.source === hovered || e.target === hovered).length;
            return (
              <div className="spectral-hover-detail">
                <span style={{ color: AGENT_COLORS[hovered] ?? '#8b949e', fontWeight: 600 }}>
                  {agent.displayName}
                </span>
                <span style={{ color: '#6b7280' }}>
                  centrality {c.toFixed(2)} &middot; {connCount} connections &middot; weight {agent.weight.toFixed(1)}
                </span>
                {Object.keys(agent.signals).length > 0 && (
                  <span style={{ color: '#4a5568', fontSize: '0.65rem' }}>
                    signals: {Object.entries(agent.signals).map(([k, v]) => `${k}=${v.toFixed(2)}`).join(', ')}
                  </span>
                )}
              </div>
            );
          })()}
        </div>
      )}
    </div>
  );
};
