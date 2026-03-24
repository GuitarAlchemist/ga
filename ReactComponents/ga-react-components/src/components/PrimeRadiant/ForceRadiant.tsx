// src/components/PrimeRadiant/ForceRadiant.tsx
// Prime Radiant v2 — force-directed graph via 3d-force-graph
// Beliefs applied: force-directed layout, size+brightness hierarchy,
// billboard LOD labels, bloom 0.4/0.5/0.7, orbit controls, breathing animation.

import React, { useCallback, useEffect, useRef, useState } from 'react';
import ForceGraph3D, { type NodeObject, type LinkObject } from '3d-force-graph';
import * as THREE from 'three';
import { UnrealBloomPass } from 'three/examples/jsm/postprocessing/UnrealBloomPass.js';
import type { GovernanceGraph, GovernanceNode, GovernanceEdge, GovernanceNodeType } from './types';
import { NODE_COLORS, HEALTH_COLORS } from './types';
import { loadGovernanceData, getHealthStatus } from './DataLoader';
import { DetailPanel } from './DetailPanel';
import { buildGraphIndex, type GraphIndex } from './DataLoader';
import './styles.css';

// ---------------------------------------------------------------------------
// Node/Link types for 3d-force-graph
// ---------------------------------------------------------------------------
interface GraphNode extends NodeObject {
  id: string;
  name: string;
  type: GovernanceNodeType;
  description: string;
  color: string;
  val: number;        // size factor
  repo?: string;
  version?: string;
  health?: GovernanceNode['health'];
  children?: string[];
  metadata?: Record<string, unknown>;
  // force-graph adds these at runtime:
  x?: number;
  y?: number;
  z?: number;
}

interface GraphLink extends LinkObject<GraphNode> {
  source: string;
  target: string;
  type: string;
  color: string;
  width: number;
}

// ---------------------------------------------------------------------------
// Size by type — hierarchy belief: size + brightness = importance
// ---------------------------------------------------------------------------
const TYPE_SIZE: Record<GovernanceNodeType, number> = {
  constitution: 30,
  department: 18,
  policy: 8,
  persona: 8,
  pipeline: 7,
  schema: 4,
  test: 4,
  ixql: 4,
};

// Particle count per node type (more = more important)
const TYPE_PARTICLES: Record<GovernanceNodeType, number> = {
  constitution: 60,
  department: 35,
  policy: 18,
  persona: 18,
  pipeline: 15,
  schema: 8,
  test: 8,
  ixql: 8,
};

// Edge colors by type
const EDGE_COLORS: Record<string, string> = {
  'constitutional-hierarchy': '#FFD700',
  'policy-persona': '#FFA50088',
  'pipeline-flow': '#00CED1AA',
  'cross-repo': '#008B8B88',
  'lolli': '#FF444488',
};

const EDGE_WIDTH: Record<string, number> = {
  'constitutional-hierarchy': 2.5,
  'policy-persona': 0.8,
  'pipeline-flow': 1.2,
  'cross-repo': 0.6,
  'lolli': 1.5,
};

// ---------------------------------------------------------------------------
// Create custom Three.js node object — particle cluster with glow
// ---------------------------------------------------------------------------
function createNodeObject(node: GraphNode): THREE.Object3D {
  const group = new THREE.Group();
  const count = TYPE_PARTICLES[node.type] ?? 12;
  const radius = Math.pow(TYPE_SIZE[node.type] ?? 5, 0.5) * 0.8;
  const color = new THREE.Color(node.color);
  const isUnhealthy = node.health && node.health.lolliCount > 0;

  // Core sphere — small, bright, solid
  const coreGeo = new THREE.SphereGeometry(radius * 0.3, 16, 16);
  const coreMat = new THREE.MeshBasicMaterial({
    color: isUnhealthy ? new THREE.Color('#FF4444') : color,
    transparent: true,
    opacity: 0.9,
  });
  const core = new THREE.Mesh(coreGeo, coreMat);
  group.add(core);

  // Particle halo — additive glow around the core
  const positions = new Float32Array(count * 3);
  const colors = new Float32Array(count * 3);
  for (let i = 0; i < count; i++) {
    const theta = Math.random() * Math.PI * 2;
    const phi = Math.acos(2 * Math.random() - 1);
    const r = radius * (0.4 + Math.random() * 0.6);
    positions[i * 3] = r * Math.sin(phi) * Math.cos(theta);
    positions[i * 3 + 1] = r * Math.sin(phi) * Math.sin(theta);
    positions[i * 3 + 2] = r * Math.cos(phi);

    const v = 0.6 + Math.random() * 0.4;
    const c = isUnhealthy ? new THREE.Color('#FF4444') : color.clone();
    colors[i * 3] = c.r * v;
    colors[i * 3 + 1] = c.g * v;
    colors[i * 3 + 2] = c.b * v;
  }

  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
  geo.setAttribute('color', new THREE.BufferAttribute(colors, 3));

  const mat = new THREE.PointsMaterial({
    size: radius * 0.15,
    vertexColors: true,
    transparent: true,
    opacity: 0.7,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    sizeAttenuation: true,
  });

  group.add(new THREE.Points(geo, mat));

  return group;
}

// ---------------------------------------------------------------------------
// Convert governance graph to force-graph format
// ---------------------------------------------------------------------------
function toForceData(graph: GovernanceGraph): { nodes: GraphNode[]; links: GraphLink[] } {
  const nodes: GraphNode[] = graph.nodes.map((n) => ({
    id: n.id,
    name: n.name,
    type: n.type,
    description: n.description,
    color: n.color,
    val: TYPE_SIZE[n.type] ?? 5,
    repo: n.repo,
    version: n.version,
    health: n.health,
    children: n.children,
    metadata: n.metadata,
  }));

  const links: GraphLink[] = graph.edges.map((e) => ({
    source: e.source,
    target: e.target,
    type: e.type,
    color: EDGE_COLORS[e.type] ?? '#444444',
    width: EDGE_WIDTH[e.type] ?? 0.5,
  }));

  return { nodes, links };
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
export interface ForceRadiantProps {
  data?: GovernanceGraph;
  width?: number | string;
  height?: number | string;
  onNodeSelect?: (node: GovernanceNode | null) => void;
  showDetailPanel?: boolean;
  className?: string;
}

export const ForceRadiant: React.FC<ForceRadiantProps> = ({
  data,
  width = '100%',
  height = '100%',
  onNodeSelect,
  showDetailPanel = true,
  className = '',
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const graphRef = useRef<ReturnType<typeof ForceGraph3D> | null>(null);

  const [selectedNode, setSelectedNode] = useState<GovernanceNode | null>(null);
  const [graphData, setGraphData] = useState<GovernanceGraph | null>(null);
  const [graphIndex, setGraphIndex] = useState<GraphIndex | null>(null);

  // ─── Initialize ───
  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    const graph = loadGovernanceData(data);
    setGraphData(graph);
    setGraphIndex(buildGraphIndex(graph));

    const forceData = toForceData(graph);
    const healthStatus = getHealthStatus(graph.globalHealth.resilienceScore);

    // Create force graph
    const fg = ForceGraph3D({ controlType: 'orbit' })(container)
      .graphData(forceData)
      .backgroundColor('#000008')
      .showNavInfo(false)

      // Node rendering
      .nodeThreeObject((node: object) => createNodeObject(node as GraphNode))
      .nodeThreeObjectExtend(false)
      .nodeLabel((node: object) => {
        const n = node as GraphNode;
        return `<div style="color:${n.color};font-family:monospace;font-size:12px;text-shadow:0 0 6px ${n.color}">${n.name}</div>`;
      })

      // Edge rendering
      .linkColor((link: object) => (link as GraphLink).color)
      .linkWidth((link: object) => (link as GraphLink).width)
      .linkOpacity(0.4)
      .linkDirectionalParticles(3)
      .linkDirectionalParticleWidth(1.5)
      .linkDirectionalParticleSpeed(0.005)
      .linkDirectionalParticleColor((link: object) => (link as GraphLink).color)
      .linkCurvature(0.15)

      // Interaction
      .onNodeClick((node: object) => {
        const n = node as GraphNode;
        const govNode = graph.nodes.find((gn) => gn.id === n.id) ?? null;
        setSelectedNode(govNode);
        onNodeSelect?.(govNode);

        // Zoom to clicked node
        const distance = 60;
        const p = n as GraphNode & { x: number; y: number; z: number };
        if (p.x !== undefined) {
          fg.cameraPosition(
            { x: p.x, y: p.y + distance * 0.3, z: p.z + distance },
            { x: p.x, y: p.y, z: p.z },
            1200,
          );
        }
      })
      .onBackgroundClick(() => {
        setSelectedNode(null);
        onNodeSelect?.(null);
      })

      // Force configuration — cluster by hierarchy
      .d3AlphaDecay(0.02)
      .d3VelocityDecay(0.3);

    // Add bloom post-processing (belief: strength 0.4, radius 0.5, threshold 0.7)
    const bloomPass = new UnrealBloomPass(
      new THREE.Vector2(container.clientWidth, container.clientHeight),
      0.4,   // strength
      0.5,   // radius
      0.7,   // threshold
    );
    fg.postProcessingComposer().addPass(bloomPass);

    // Slow auto-rotate
    const controls = fg.controls() as { autoRotate?: boolean; autoRotateSpeed?: number };
    if (controls) {
      controls.autoRotate = true;
      controls.autoRotateSpeed = 0.3;
    }

    graphRef.current = fg;

    return () => {
      fg._destructor();
      graphRef.current = null;
    };
  }, [data]); // eslint-disable-line react-hooks/exhaustive-deps

  // ─── Resize ───
  useEffect(() => {
    const container = containerRef.current;
    if (!container || !graphRef.current) return;

    const ro = new ResizeObserver(() => {
      graphRef.current?.width(container.clientWidth);
      graphRef.current?.height(container.clientHeight);
    });
    ro.observe(container);
    return () => ro.disconnect();
  }, []);

  // ─── Detail panel handlers ───
  const handleNavigate = useCallback((nodeId: string) => {
    const fg = graphRef.current;
    if (!fg) return;
    const node = (fg.graphData() as { nodes: GraphNode[] }).nodes.find((n) => n.id === nodeId);
    if (node && node.x !== undefined) {
      fg.cameraPosition(
        { x: node.x, y: (node.y ?? 0) + 20, z: (node.z ?? 0) + 60 },
        { x: node.x, y: node.y ?? 0, z: node.z ?? 0 },
        1200,
      );
      const govNode = graphData?.nodes.find((gn) => gn.id === nodeId) ?? null;
      setSelectedNode(govNode);
      onNodeSelect?.(govNode);
    }
  }, [graphData, onNodeSelect]);

  const handleCloseDetail = useCallback(() => {
    setSelectedNode(null);
    onNodeSelect?.(null);
  }, [onNodeSelect]);

  const healthStatus = graphData
    ? getHealthStatus(graphData.globalHealth.resilienceScore)
    : 'healthy';

  return (
    <div className={`prime-radiant ${className}`} style={{ width, height }}>
      <div ref={containerRef} style={{ width: '100%', height: '100%' }} />

      {/* Health indicator */}
      {graphData && (
        <div className="prime-radiant__health">
          <span className={`prime-radiant__health-dot prime-radiant__health-dot--${healthStatus}`} />
          <span>
            R:{' '}
            <span style={{ color: HEALTH_COLORS[healthStatus], fontWeight: 600 }}>
              {(graphData.globalHealth.resilienceScore * 100).toFixed(0)}%
            </span>
          </span>
          <span style={{ color: '#30363d' }}>|</span>
          <span>
            ERGOL: <span style={{ color: '#FFD700' }}>{graphData.globalHealth.ergolCount}</span>
          </span>
          <span style={{ color: '#30363d' }}>|</span>
          <span>
            LOLLI: <span style={{ color: graphData.globalHealth.lolliCount > 0 ? '#FF4444' : '#8b949e' }}>
              {graphData.globalHealth.lolliCount}
            </span>
          </span>
        </div>
      )}

      {/* Detail panel */}
      {showDetailPanel && (
        <DetailPanel
          node={selectedNode}
          graphIndex={graphIndex}
          onClose={handleCloseDetail}
          onNavigate={handleNavigate}
        />
      )}
    </div>
  );
};
