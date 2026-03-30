// src/components/PrimeRadiant/SignalGraph.tsx
// Weak Signal Interaction Graph — canvas-based force graph for algedonic signal relationships
// Per JPP cybernetics: "analyze interactions, not isolated lists."

import React, { useRef, useEffect, useState, useCallback } from 'react';
import type { AlgedonicSignal, AlgedonicSeverity, AlgedonicSignalType } from './AlgedonicPanel';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------
interface GraphNode {
  id: string;
  signal: string;
  type: AlgedonicSignalType;
  severity: AlgedonicSeverity;
  source: string;
  timestamp: number;
  description?: string;
  x: number;
  y: number;
  vx: number;
  vy: number;
  radius: number;
}

interface GraphEdge {
  source: number;
  target: number;
  kind: 'same-source' | 'same-type';
  opacity: number;
}

export interface SignalGraphProps {
  signals: AlgedonicSignal[];
  width?: number;
  height?: number;
  onNodeClick?: (signal: AlgedonicSignal) => void;
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------
const DEFAULT_WIDTH = 300;
const DEFAULT_HEIGHT = 250;

const TYPE_COLORS: Record<AlgedonicSignalType, string> = {
  pain: '#FF4444',
  pleasure: '#33CC66',
};

const SEVERITY_RADIUS: Record<AlgedonicSeverity, number> = {
  emergency: 12,
  warning: 8,
  info: 6,
};

const FALLBACK_SIGNALS: AlgedonicSignal[] = [
  { id: 'fb-1', timestamp: new Date(Date.now() - 120000).toISOString(), signal: 'governance_drift', type: 'pain', source: 'recon', severity: 'warning', status: 'active', description: 'Governance policy drift detected across consumer repos' },
  { id: 'fb-2', timestamp: new Date(Date.now() - 300000).toISOString(), signal: 'belief_stale', type: 'pain', source: 'staleness-detector', severity: 'info', status: 'acknowledged', description: 'Stale beliefs detected in music-theory domain' },
  { id: 'fb-3', timestamp: new Date(Date.now() - 60000).toISOString(), signal: 'ci_failure', type: 'pain', source: 'github-actions', severity: 'emergency', status: 'active', description: 'CI pipeline failure on main branch' },
  { id: 'fb-4', timestamp: new Date(Date.now() - 180000).toISOString(), signal: 'lolli_inflation', type: 'pain', source: 'anti-lolli', severity: 'warning', status: 'acknowledged', description: 'LOLLI count exceeds ERGOL threshold' },
  { id: 'fb-5', timestamp: new Date(Date.now() - 240000).toISOString(), signal: 'model_degraded', type: 'pain', source: 'ollama-health', severity: 'warning', status: 'active', description: 'Ollama model response latency exceeds threshold' },
  { id: 'fb-6', timestamp: new Date(Date.now() - 30000).toISOString(), signal: 'kaizen_win', type: 'pleasure', source: 'recon', severity: 'info', status: 'resolved', description: 'Continuous improvement cycle completed successfully' },
];

const SPRING_LENGTH = 60;
const SPRING_K = 0.004;
const REPULSION = 800;
const DAMPING = 0.92;
const CENTER_GRAVITY = 0.002;

// ---------------------------------------------------------------------------
// Physics helpers
// ---------------------------------------------------------------------------
function buildGraph(signals: AlgedonicSignal[], width: number, height: number): { nodes: GraphNode[]; edges: GraphEdge[] } {
  const timestamps = signals.map(s => new Date(s.timestamp).getTime());
  const minTs = Math.min(...timestamps);
  const maxTs = Math.max(...timestamps);
  const tsRange = maxTs - minTs || 1;

  const nodes: GraphNode[] = signals.map((s, i) => {
    const angle = (2 * Math.PI * i) / signals.length;
    const spread = Math.min(width, height) * 0.3;
    return {
      id: s.id,
      signal: s.signal,
      type: s.type,
      severity: s.severity,
      source: s.source,
      timestamp: new Date(s.timestamp).getTime(),
      description: s.description,
      x: width / 2 + Math.cos(angle) * spread + (Math.random() - 0.5) * 20,
      y: height / 2 + Math.sin(angle) * spread + (Math.random() - 0.5) * 20,
      vx: 0,
      vy: 0,
      radius: SEVERITY_RADIUS[s.severity],
    };
  });

  const edges: GraphEdge[] = [];

  for (let i = 0; i < nodes.length; i++) {
    for (let j = i + 1; j < nodes.length; j++) {
      const a = nodes[i];
      const b = nodes[j];

      // Same source => solid connection
      if (a.source === b.source) {
        const temporalProximity = 1 - Math.abs(a.timestamp - b.timestamp) / tsRange;
        edges.push({ source: i, target: j, kind: 'same-source', opacity: 0.3 + temporalProximity * 0.5 });
      }
      // Same type, different source => dashed connection
      else if (a.type === b.type) {
        const temporalProximity = 1 - Math.abs(a.timestamp - b.timestamp) / tsRange;
        edges.push({ source: i, target: j, kind: 'same-type', opacity: 0.15 + temporalProximity * 0.35 });
      }
    }
  }

  return { nodes, edges };
}

function stepPhysics(nodes: GraphNode[], edges: GraphEdge[], width: number, height: number): void {
  const cx = width / 2;
  const cy = height / 2;

  // Repulsion between all node pairs
  for (let i = 0; i < nodes.length; i++) {
    for (let j = i + 1; j < nodes.length; j++) {
      const dx = nodes[j].x - nodes[i].x;
      const dy = nodes[j].y - nodes[i].y;
      const dist = Math.sqrt(dx * dx + dy * dy) || 1;
      const force = REPULSION / (dist * dist);
      const fx = (dx / dist) * force;
      const fy = (dy / dist) * force;
      nodes[i].vx -= fx;
      nodes[i].vy -= fy;
      nodes[j].vx += fx;
      nodes[j].vy += fy;
    }
  }

  // Spring attraction along edges
  for (const edge of edges) {
    const a = nodes[edge.source];
    const b = nodes[edge.target];
    const dx = b.x - a.x;
    const dy = b.y - a.y;
    const dist = Math.sqrt(dx * dx + dy * dy) || 1;
    const displacement = dist - SPRING_LENGTH;
    const force = SPRING_K * displacement;
    const fx = (dx / dist) * force;
    const fy = (dy / dist) * force;
    a.vx += fx;
    a.vy += fy;
    b.vx -= fx;
    b.vy -= fy;
  }

  // Center gravity + integration
  for (const node of nodes) {
    node.vx += (cx - node.x) * CENTER_GRAVITY;
    node.vy += (cy - node.y) * CENTER_GRAVITY;
    node.vx *= DAMPING;
    node.vy *= DAMPING;
    node.x += node.vx;
    node.y += node.vy;
    // Clamp to bounds
    const pad = node.radius + 2;
    node.x = Math.max(pad, Math.min(width - pad, node.x));
    node.y = Math.max(pad + 14, Math.min(height - pad, node.y));
  }
}

function truncateLabel(name: string, max: number): string {
  const label = name.replace(/_/g, ' ');
  return label.length > max ? label.slice(0, max - 1) + '\u2026' : label;
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
export const SignalGraph: React.FC<SignalGraphProps> = ({
  signals,
  width = DEFAULT_WIDTH,
  height = DEFAULT_HEIGHT,
  onNodeClick,
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const graphRef = useRef<{ nodes: GraphNode[]; edges: GraphEdge[] } | null>(null);
  const hoveredRef = useRef<number>(-1);
  const animRef = useRef<number>(0);

  const [tooltip, setTooltip] = useState<{ x: number; y: number; node: GraphNode } | null>(null);

  const effectiveSignals = signals.length > 0 ? signals : FALLBACK_SIGNALS;

  // Build graph when signals change
  useEffect(() => {
    graphRef.current = buildGraph(effectiveSignals, width, height);
  }, [effectiveSignals, width, height]);

  // Animation loop
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    let running = true;

    const draw = () => {
      if (!running || !graphRef.current) return;
      const { nodes, edges } = graphRef.current;

      stepPhysics(nodes, edges, width, height);

      ctx.clearRect(0, 0, width, height);

      // Draw edges
      for (const edge of edges) {
        const a = nodes[edge.source];
        const b = nodes[edge.target];
        const isHighlighted = hoveredRef.current === edge.source || hoveredRef.current === edge.target;

        ctx.beginPath();
        ctx.strokeStyle = isHighlighted
          ? `rgba(255, 255, 255, ${Math.min(edge.opacity + 0.3, 1)})`
          : `rgba(139, 148, 158, ${edge.opacity})`;
        ctx.lineWidth = isHighlighted ? 1.5 : 1;

        if (edge.kind === 'same-type') {
          ctx.setLineDash([3, 3]);
        } else {
          ctx.setLineDash([]);
        }

        ctx.moveTo(a.x, a.y);
        ctx.lineTo(b.x, b.y);
        ctx.stroke();
        ctx.setLineDash([]);
      }

      // Draw nodes
      for (let i = 0; i < nodes.length; i++) {
        const node = nodes[i];
        const isHovered = hoveredRef.current === i;
        const color = TYPE_COLORS[node.type];

        // Glow for hovered
        if (isHovered) {
          ctx.beginPath();
          ctx.arc(node.x, node.y, node.radius + 4, 0, Math.PI * 2);
          ctx.fillStyle = `${color}33`;
          ctx.fill();
        }

        // Node circle
        ctx.beginPath();
        ctx.arc(node.x, node.y, node.radius, 0, Math.PI * 2);
        ctx.fillStyle = isHovered ? color : `${color}cc`;
        ctx.fill();
        ctx.strokeStyle = isHovered ? '#ffffff' : `${color}88`;
        ctx.lineWidth = isHovered ? 2 : 1;
        ctx.stroke();

        // Label
        ctx.fillStyle = isHovered ? '#ffffff' : '#8b949e';
        ctx.font = isHovered ? 'bold 9px monospace' : '8px monospace';
        ctx.textAlign = 'center';
        ctx.fillText(truncateLabel(node.signal, 12), node.x, node.y + node.radius + 11);
      }

      animRef.current = requestAnimationFrame(draw);
    };

    animRef.current = requestAnimationFrame(draw);

    return () => {
      running = false;
      cancelAnimationFrame(animRef.current);
    };
  }, [effectiveSignals, width, height]);

  // Hit detection
  const findNodeAt = useCallback((mx: number, my: number): number => {
    if (!graphRef.current) return -1;
    const { nodes } = graphRef.current;
    for (let i = nodes.length - 1; i >= 0; i--) {
      const dx = mx - nodes[i].x;
      const dy = my - nodes[i].y;
      if (dx * dx + dy * dy <= (nodes[i].radius + 4) * (nodes[i].radius + 4)) {
        return i;
      }
    }
    return -1;
  }, []);

  const handleMouseMove = useCallback((e: React.MouseEvent<HTMLCanvasElement>) => {
    const canvas = canvasRef.current;
    if (!canvas || !graphRef.current) return;
    const rect = canvas.getBoundingClientRect();
    const mx = e.clientX - rect.left;
    const my = e.clientY - rect.top;
    const idx = findNodeAt(mx, my);
    hoveredRef.current = idx;

    if (idx >= 0) {
      const node = graphRef.current.nodes[idx];
      setTooltip({ x: e.clientX - rect.left, y: e.clientY - rect.top, node });
      canvas.style.cursor = 'pointer';
    } else {
      setTooltip(null);
      canvas.style.cursor = 'default';
    }
  }, [findNodeAt]);

  const handleClick = useCallback((e: React.MouseEvent<HTMLCanvasElement>) => {
    const canvas = canvasRef.current;
    if (!canvas || !graphRef.current || !onNodeClick) return;
    const rect = canvas.getBoundingClientRect();
    const mx = e.clientX - rect.left;
    const my = e.clientY - rect.top;
    const idx = findNodeAt(mx, my);
    if (idx >= 0) {
      const node = graphRef.current.nodes[idx];
      const original = effectiveSignals.find(s => s.id === node.id);
      if (original) onNodeClick(original);
    }
  }, [findNodeAt, onNodeClick, effectiveSignals]);

  const handleMouseLeave = useCallback(() => {
    hoveredRef.current = -1;
    setTooltip(null);
  }, []);

  return (
    <div className="signal-graph__container" style={{ position: 'relative', width, height }}>
      <canvas
        ref={canvasRef}
        width={width}
        height={height}
        className="signal-graph__canvas"
        onMouseMove={handleMouseMove}
        onClick={handleClick}
        onMouseLeave={handleMouseLeave}
      />
      {tooltip && (
        <div
          className="signal-graph__tooltip"
          style={{
            left: Math.min(tooltip.x + 12, width - 160),
            top: Math.max(tooltip.y - 60, 4),
          }}
        >
          <div className="signal-graph__tooltip-name" style={{ color: TYPE_COLORS[tooltip.node.type] }}>
            {tooltip.node.signal.replace(/_/g, ' ')}
          </div>
          <div className="signal-graph__tooltip-meta">
            <span>{tooltip.node.severity}</span>
            <span className="signal-graph__tooltip-sep">{'\u00B7'}</span>
            <span>{tooltip.node.source}</span>
          </div>
          {tooltip.node.description && (
            <div className="signal-graph__tooltip-desc">{tooltip.node.description}</div>
          )}
        </div>
      )}
      {signals.length === 0 && (
        <div className="signal-graph__fallback-label">sample data</div>
      )}
    </div>
  );
};
