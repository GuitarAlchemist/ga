// src/components/PrimeRadiant/ForceRadiant.tsx
// Prime Radiant v2 — force-directed graph via 3d-force-graph
// Beliefs applied: force-directed layout, size+brightness hierarchy,
// billboard LOD labels, bloom 0.4/0.5/0.7, orbit controls, breathing animation.

import React, { useCallback, useEffect, useRef, useState } from 'react';
import ForceGraph3D, { type NodeObject, type LinkObject } from '3d-force-graph';
import * as THREE from 'three';
import { UnrealBloomPass } from 'three/examples/jsm/postprocessing/UnrealBloomPass.js';
import { ShaderPass } from 'three/examples/jsm/postprocessing/ShaderPass.js';
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
// Fractal sprite texture — generates a nebula-like glow per node type
// Uses iterative fractal noise on canvas for unique organic shapes
// ---------------------------------------------------------------------------
const fractalTextureCache = new Map<string, THREE.Texture>();

function generateFractalTexture(baseColor: THREE.Color, complexity: number): THREE.Texture {
  const key = `${baseColor.getHexString()}-${complexity}`;
  if (fractalTextureCache.has(key)) return fractalTextureCache.get(key)!;

  const size = 128;
  const canvas = document.createElement('canvas');
  canvas.width = size;
  canvas.height = size;
  const ctx = canvas.getContext('2d')!;

  // Start with transparent black
  ctx.clearRect(0, 0, size, size);

  const cx = size / 2;
  const cy = size / 2;

  // Layer fractal circles — self-similar at multiple scales
  const layers = 3 + Math.floor(complexity * 2);
  for (let layer = 0; layer < layers; layer++) {
    const scale = 1 - (layer / layers) * 0.7;
    const branches = 3 + Math.floor(Math.random() * 4);
    const alpha = (0.15 + layer * 0.05) * scale;

    // Radial gradient base
    const r = (size / 2) * scale;
    const gradient = ctx.createRadialGradient(cx, cy, 0, cx, cy, r);
    gradient.addColorStop(0, `rgba(${Math.floor(baseColor.r * 255)}, ${Math.floor(baseColor.g * 255)}, ${Math.floor(baseColor.b * 255)}, ${alpha})`);
    gradient.addColorStop(0.4, `rgba(${Math.floor(baseColor.r * 200)}, ${Math.floor(baseColor.g * 200)}, ${Math.floor(baseColor.b * 200)}, ${alpha * 0.5})`);
    gradient.addColorStop(1, 'rgba(0, 0, 0, 0)');

    ctx.fillStyle = gradient;
    ctx.beginPath();
    ctx.arc(cx, cy, r, 0, Math.PI * 2);
    ctx.fill();

    // Fractal tendrils — branches radiating from center
    for (let b = 0; b < branches; b++) {
      const angle = (b / branches) * Math.PI * 2 + layer * 0.5;
      const length = r * (0.6 + Math.random() * 0.4);

      ctx.strokeStyle = `rgba(${Math.floor(baseColor.r * 255)}, ${Math.floor(baseColor.g * 255)}, ${Math.floor(baseColor.b * 255)}, ${alpha * 0.8})`;
      ctx.lineWidth = Math.max(1, 3 - layer);
      ctx.beginPath();
      ctx.moveTo(cx, cy);

      // Fractal curve — recursive displacement
      let px = cx, py = cy;
      const steps = 8;
      for (let s = 1; s <= steps; s++) {
        const t = s / steps;
        const jitter = (1 - t) * length * 0.15;
        const nx = cx + Math.cos(angle + Math.sin(t * 5) * 0.3) * length * t + (Math.random() - 0.5) * jitter;
        const ny = cy + Math.sin(angle + Math.cos(t * 5) * 0.3) * length * t + (Math.random() - 0.5) * jitter;
        ctx.quadraticCurveTo(px + (nx - px) * 0.5, py + (ny - py) * 0.5 + jitter * 0.5, nx, ny);
        px = nx;
        py = ny;
      }
      ctx.stroke();

      // Sub-branches (self-similar at smaller scale)
      if (layer < 2) {
        const subBranches = 2 + Math.floor(Math.random() * 2);
        for (let sb = 0; sb < subBranches; sb++) {
          const st = 0.4 + Math.random() * 0.4;
          const sx = cx + Math.cos(angle) * length * st;
          const sy = cy + Math.sin(angle) * length * st;
          const subAngle = angle + (Math.random() - 0.5) * 1.2;
          const subLen = length * 0.3 * (1 - st);

          ctx.strokeStyle = `rgba(${Math.floor(baseColor.r * 255)}, ${Math.floor(baseColor.g * 255)}, ${Math.floor(baseColor.b * 255)}, ${alpha * 0.4})`;
          ctx.lineWidth = 1;
          ctx.beginPath();
          ctx.moveTo(sx, sy);
          ctx.lineTo(sx + Math.cos(subAngle) * subLen, sy + Math.sin(subAngle) * subLen);
          ctx.stroke();
        }
      }
    }
  }

  const texture = new THREE.CanvasTexture(canvas);
  texture.minFilter = THREE.LinearFilter;
  fractalTextureCache.set(key, texture);
  return texture;
}

// ---------------------------------------------------------------------------
// Create custom Three.js node — fractal sprite + core + particle dust
// ---------------------------------------------------------------------------
function createNodeObject(node: GraphNode): THREE.Object3D {
  const group = new THREE.Group();
  const radius = Math.pow(TYPE_SIZE[node.type] ?? 5, 0.5) * 0.8;
  const color = new THREE.Color(node.color);
  const isUnhealthy = node.health && node.health.lolliCount > 0;
  const nodeColor = isUnhealthy ? new THREE.Color('#FF4444') : color;

  // Complexity scales with hierarchy importance
  const complexity = {
    constitution: 4, department: 3, policy: 2, persona: 2,
    pipeline: 2, schema: 1, test: 1, ixql: 1,
  }[node.type] ?? 1;

  // 1. Fractal nebula sprite — the main visual
  const fractalTex = generateFractalTexture(nodeColor, complexity);
  const spriteMat = new THREE.SpriteMaterial({
    map: fractalTex,
    transparent: true,
    opacity: 0.85,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
  });
  const sprite = new THREE.Sprite(spriteMat);
  const spriteScale = radius * 2.5;
  sprite.scale.set(spriteScale, spriteScale, 1);
  group.add(sprite);

  // 2. Core point — bright center
  const coreGeo = new THREE.SphereGeometry(radius * 0.2, 12, 12);
  const coreMat = new THREE.MeshBasicMaterial({
    color: nodeColor,
    transparent: true,
    opacity: 1.0,
  });
  group.add(new THREE.Mesh(coreGeo, coreMat));

  // 3. Orbiting particle dust (fewer, larger particles for clarity)
  const dustCount = Math.floor(complexity * 6);
  const positions = new Float32Array(dustCount * 3);
  const dustColors = new Float32Array(dustCount * 3);
  for (let i = 0; i < dustCount; i++) {
    const theta = Math.random() * Math.PI * 2;
    const phi = Math.acos(2 * Math.random() - 1);
    const r = radius * (0.5 + Math.random() * 1.0);
    positions[i * 3] = r * Math.sin(phi) * Math.cos(theta);
    positions[i * 3 + 1] = r * Math.sin(phi) * Math.sin(theta);
    positions[i * 3 + 2] = r * Math.cos(phi);

    const v = 0.7 + Math.random() * 0.3;
    dustColors[i * 3] = nodeColor.r * v;
    dustColors[i * 3 + 1] = nodeColor.g * v;
    dustColors[i * 3 + 2] = nodeColor.b * v;
  }

  const dustGeo = new THREE.BufferGeometry();
  dustGeo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
  dustGeo.setAttribute('color', new THREE.BufferAttribute(dustColors, 3));
  const dustMat = new THREE.PointsMaterial({
    size: radius * 0.12,
    vertexColors: true,
    transparent: true,
    opacity: 0.6,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    sizeAttenuation: true,
  });
  group.add(new THREE.Points(dustGeo, dustMat));

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
      .onNodeHover((node: object | null) => {
        if (node) {
          const n = node as GraphNode & { __threeObj?: THREE.Object3D };
          if (n.__threeObj) {
            n.__threeObj.scale.setScalar(1.3); // pop on hover
          }
        }
        // Container cursor
        if (containerRef.current) {
          containerRef.current.style.cursor = node ? 'pointer' : 'grab';
        }
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

    // Chromatic aberration post-processing
    const chromaticShader = {
      uniforms: {
        tDiffuse: { value: null },
        uOffset: { value: new THREE.Vector2(0.002, 0.002) },
      },
      vertexShader: `varying vec2 vUv; void main() { vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }`,
      fragmentShader: `uniform sampler2D tDiffuse; uniform vec2 uOffset; varying vec2 vUv;
    void main() {
      float r = texture2D(tDiffuse, vUv + uOffset).r;
      float g = texture2D(tDiffuse, vUv).g;
      float b = texture2D(tDiffuse, vUv - uOffset).b;
      float a = texture2D(tDiffuse, vUv).a;
      gl_FragColor = vec4(r, g, b, a);
    }`,
    };
    fg.postProcessingComposer().addPass(new ShaderPass(chromaticShader));

    // Breathing animation — nodes pulse gently
    fg.onEngineTick(() => {
      const t = Date.now() * 0.001;
      fg.graphData().nodes.forEach((node: object) => {
        const n = node as GraphNode & { __threeObj?: THREE.Object3D };
        if (n.__threeObj) {
          // Gentle breathing: scale oscillates ±5% based on node type phase
          const phase = n.id.length * 0.7; // deterministic phase per node
          const breathe = 1 + Math.sin(t * 0.8 + phase) * 0.05;
          n.__threeObj.scale.setScalar(breathe);
        }
      });
    });

    // Slow auto-rotate
    const controls = fg.controls() as { autoRotate?: boolean; autoRotateSpeed?: number };
    if (controls) {
      controls.autoRotate = true;
      controls.autoRotateSpeed = 0.3;
    }

    // Starfield background
    const starCount = 2000;
    const starPositions = new Float32Array(starCount * 3);
    const starColors = new Float32Array(starCount * 3);
    for (let i = 0; i < starCount; i++) {
      const r = 800 + Math.random() * 1200;
      const theta = Math.random() * Math.PI * 2;
      const phi = Math.acos(2 * Math.random() - 1);
      starPositions[i*3] = r * Math.sin(phi) * Math.cos(theta);
      starPositions[i*3+1] = r * Math.sin(phi) * Math.sin(theta);
      starPositions[i*3+2] = r * Math.cos(phi);
      const b = 0.3 + Math.random() * 0.5;
      starColors[i*3] = b * 0.85; starColors[i*3+1] = b * 0.9; starColors[i*3+2] = b;
    }
    const starGeo = new THREE.BufferGeometry();
    starGeo.setAttribute('position', new THREE.BufferAttribute(starPositions, 3));
    starGeo.setAttribute('color', new THREE.BufferAttribute(starColors, 3));
    const starMat = new THREE.PointsMaterial({ size: 0.5, vertexColors: true, transparent: true, opacity: 0.6, sizeAttenuation: true });
    fg.scene().add(new THREE.Points(starGeo, starMat));

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
