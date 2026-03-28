// src/components/PrimeRadiant/ForceRadiant.tsx
// Prime Radiant v2 — force-directed graph via 3d-force-graph
// Beliefs applied: force-directed layout, size+brightness hierarchy,
// billboard LOD labels, bloom 0.4/0.5/0.7, orbit controls, breathing animation.

import React, { useCallback, useEffect, useRef, useState } from 'react';
import ForceGraph3D, { type NodeObject, type LinkObject } from '3d-force-graph';
import * as THREE from 'three';
import { UnrealBloomPass } from 'three/examples/jsm/postprocessing/UnrealBloomPass.js';
import { ShaderPass } from 'three/examples/jsm/postprocessing/ShaderPass.js';
import type { GovernanceGraph, GovernanceNode, GovernanceNodeType } from './types';
import { HEALTH_COLORS, HEALTH_STATUS_COLORS, type GovernanceHealthStatus } from './types';
import { loadGovernanceData, loadGovernanceDataAsync, getHealthStatus, startLivePolling, updateNodeHealth, type LivePollingHandle } from './DataLoader';
import { DetailPanel } from './DetailPanel';
import { ChatWidget } from './ChatWidget';
import { buildGraphIndex, type GraphIndex } from './DataLoader';
import { createDemerzelFace, updateDemerzelFace } from './DemerzelFace';
import { createTarsRobot, updateTarsRobot } from './TarsRobot';
// TrantorGlobe removed — replaced by real Earth + nebulae
import { createSolarSystem, updateSolarSystem, showPlanetLabel, getPlanetMeshes, startLiveCloudUpdates } from './SolarSystem';
import { createSpaceStation, updateSpaceStation } from './SpaceStation';
import { createMilkyWay } from './MilkyWay';
import { GalacticClock } from './GalacticClock';
import { TutorialOverlay } from './TutorialOverlay';
import { ActivityPanel } from './ActivityPanel';
import { LLMStatus } from './LLMStatus';
import { IxqlCommandInput } from './IxqlCommandInput';
import { evaluatePredicate, type IxqlParseResult } from './IxqlControlParser';
import { startVisualCriticLoop, type CriticPhase } from './VisualCriticLoop';
import { DemerzelCriticOverlay, type CriticState } from './DemerzelCriticOverlay';
import { BacklogPanel } from './BacklogPanel';
import { AgentPanel } from './AgentPanel';
import { SeldonDashboard } from './SeldonDashboard';
import { IconRail, type PanelId } from './IconRail';
import { AlgedonicPanel, type AlgedonicSignal } from './AlgedonicPanel';
import { CICDPanel } from './CICDPanel';
import { ClaudeCodePanel } from './ClaudeCodePanel';
import type { AlgedonicSignalEvent } from './DataLoader';
import { CourseViewer } from './CourseViewer';
import { LiveNotebook } from './LiveNotebook';
import { IcicleDrawer } from './IcicleDrawer';
import './styles.css';

// ---------------------------------------------------------------------------
// Algedonic ripple types
// ---------------------------------------------------------------------------
interface ActiveRipple {
  mesh: THREE.Mesh;
  startTime: number;
  color: string;
  duration: number;   // seconds
  maxScale: number;
}

interface EdgePropagation {
  startTime: number;
  color: string;
  hop: number;
}

const MAX_CONCURRENT_RIPPLES = 10;
const RIPPLE_DURATION = 2.0;         // seconds
const RIPPLE_MAX_SCALE = 8;
const SURGE_RIPPLE_DURATION = 3.0;
const SURGE_RIPPLE_MAX_SCALE = 15;
const PROPAGATION_HOP_DURATION = 0.5; // seconds per hop
const MAX_PROPAGATION_HOPS = 3;
const COMPOUNDING_WINDOW_SEC = 10;
const COMPOUNDING_THRESHOLD = 3;
const SURGE_BLOOM_STRENGTH = 1.2;
const SURGE_BLOOM_DURATION = 3.0;     // seconds

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
  healthStatus?: GovernanceHealthStatus;
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
const _TYPE_PARTICLES: Record<GovernanceNodeType, number> = {
  constitution: 60,
  department: 35,
  policy: 18,
  persona: 18,
  pipeline: 15,
  schema: 8,
  test: 8,
  ixql: 8,
};

// Edge colors — very subtle so they don't compete with node health colors
const EDGE_COLORS: Record<string, string> = {
  'constitutional-hierarchy': '#FFFFFF30',   // white, subtle structural
  'policy-persona': '#FFFFFF18',             // near-invisible governance links
  'pipeline-flow': '#FFFFFF22',              // faint flow lines
  'cross-repo': '#FFFFFF15',                 // barely visible cross-repo
  'lolli': '#FF444466',                      // red tint — dead refs still notable
};

const EDGE_WIDTH: Record<string, number> = {
  'constitutional-hierarchy': 1.2,
  'policy-persona': 0.4,
  'pipeline-flow': 0.6,
  'cross-repo': 0.3,
  'lolli': 0.8,
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
// Raymarched volumetric sphere shader — fractal noise inside each node
// Each node looks like a glowing plasma orb with swirling internal structure
// ---------------------------------------------------------------------------
const volumetricVertexShader = /* glsl */ `
  varying vec3 vWorldPos;
  varying vec3 vNormal;
  varying vec3 vViewDir;
  varying vec2 vUv;

  void main() {
    vUv = uv;
    vNormal = normalize(normalMatrix * normal);
    vec4 worldPos = modelMatrix * vec4(position, 1.0);
    vWorldPos = worldPos.xyz;
    vViewDir = normalize(cameraPosition - worldPos.xyz);
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  }
`;

const volumetricFragmentShader = /* glsl */ `
  uniform vec3 uColor;
  uniform float uTime;
  uniform float uComplexity;
  uniform float uIntensity;

  varying vec3 vWorldPos;
  varying vec3 vNormal;
  varying vec3 vViewDir;
  varying vec2 vUv;

  // Simplex-like 3D noise (compact hash-based)
  vec3 hash33(vec3 p) {
    p = fract(p * vec3(443.897, 441.423, 437.195));
    p += dot(p, p.yzx + 19.19);
    return fract((p.xxy + p.yxx) * p.zyx);
  }

  float noise3d(vec3 p) {
    vec3 i = floor(p);
    vec3 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    float n = mix(
      mix(mix(dot(hash33(i), f), dot(hash33(i + vec3(1,0,0)), f - vec3(1,0,0)), f.x),
          mix(dot(hash33(i + vec3(0,1,0)), f - vec3(0,1,0)), dot(hash33(i + vec3(1,1,0)), f - vec3(1,1,0)), f.x), f.y),
      mix(mix(dot(hash33(i + vec3(0,0,1)), f - vec3(0,0,1)), dot(hash33(i + vec3(1,0,1)), f - vec3(1,0,1)), f.x),
          mix(dot(hash33(i + vec3(0,1,1)), f - vec3(0,1,1)), dot(hash33(i + vec3(1,1,1)), f - vec3(1,1,1)), f.x), f.y), f.z);
    return n * 0.5 + 0.5;
  }

  // Fractal Brownian motion — self-similar at multiple scales
  float fbm(vec3 p, int octaves) {
    float value = 0.0;
    float amplitude = 0.5;
    float frequency = 1.0;
    for (int i = 0; i < 3; i++) {
      if (i >= octaves) break;
      value += amplitude * noise3d(p * frequency);
      frequency *= 2.0;
      amplitude *= 0.5;
    }
    return value;
  }

  void main() {
    // Fresnel rim glow
    float fresnel = 1.0 - dot(vNormal, vViewDir);
    fresnel = pow(fresnel, 2.5);

    // Fractal noise in object space (animated)
    vec3 noiseCoord = vWorldPos * 0.3 + uTime * 0.15;
    int octaves = int(uComplexity) + 2;
    float fractalNoise = fbm(noiseCoord, octaves);

    // Second noise layer at different frequency (creates swirling)
    float swirl = fbm(noiseCoord * 2.3 + vec3(uTime * 0.1, 0.0, uTime * -0.08), octaves);

    // Combine: core brightness + fractal pattern + Fresnel rim
    float coreBright = smoothstep(0.6, 0.0, length(vUv - 0.5) * 2.0);
    float pattern = mix(fractalNoise, swirl, 0.4) * coreBright;

    // Color: base color modulated by fractal, brighter at center
    vec3 col = uColor * (0.3 + pattern * 1.5);
    col += uColor * fresnel * 0.8; // rim glow in node color
    col += vec3(1.0) * coreBright * 0.15; // white-hot center

    // Alpha: solid at center, fractal fade at edges, Fresnel rim
    float alpha = coreBright * 0.8 + fresnel * 0.6 + pattern * 0.3;
    alpha = clamp(alpha * uIntensity, 0.0, 1.0);

    gl_FragColor = vec4(col, alpha);
  }
`;

// Cache shader materials per color+complexity
const shaderMaterialCache = new Map<string, THREE.ShaderMaterial>();

function createVolumetricMaterial(color: THREE.Color, complexity: number, intensity: number): THREE.ShaderMaterial {
  const key = `${color.getHexString()}-${complexity}-${intensity}`;
  if (shaderMaterialCache.has(key)) return shaderMaterialCache.get(key)!.clone();

  const mat = new THREE.ShaderMaterial({
    uniforms: {
      uColor: { value: color },
      uTime: { value: 0 },
      uComplexity: { value: complexity },
      uIntensity: { value: intensity },
    },
    vertexShader: volumetricVertexShader,
    fragmentShader: volumetricFragmentShader,
    transparent: true,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    side: THREE.FrontSide,
  });

  shaderMaterialCache.set(key, mat);
  return mat.clone();
}

// ---------------------------------------------------------------------------
// Create custom Three.js node — raymarched core + fractal sprite + dust
// ---------------------------------------------------------------------------
// Shape by type — each governance artifact type gets a distinct geometry
const TYPE_GEOMETRY: Record<GovernanceNodeType, (r: number) => THREE.BufferGeometry> = {
  constitution: (r) => new THREE.DodecahedronGeometry(r, 0),
  department: (r) => new THREE.OctahedronGeometry(r, 0),
  policy: (r) => new THREE.BoxGeometry(r * 1.4, r * 1.4, r * 1.4),
  persona: (r) => new THREE.ConeGeometry(r, r * 2, 8),
  pipeline: (r) => new THREE.CylinderGeometry(r * 0.5, r * 0.5, r * 2, 8),
  schema: (r) => new THREE.TetrahedronGeometry(r, 0),
  test: (r) => new THREE.IcosahedronGeometry(r, 1),
  ixql: (r) => new THREE.TorusKnotGeometry(r * 0.6, r * 0.2, 32, 8),
};

// Visual prominence config per health state — problems JUMP OUT
const HEALTH_PROMINENCE: Record<GovernanceHealthStatus, {
  sizeMult: number; particleMult: number; opacity: number; glowIntensity: number;
  spinSpeed: number;  // radians/sec — 0 = static, >0 = active
}> = {
  error:         { sizeMult: 1.5, particleMult: 2.0, opacity: 1.0, glowIntensity: 1.8, spinSpeed: 2.0 },
  warning:       { sizeMult: 1.3, particleMult: 1.5, opacity: 1.0, glowIntensity: 1.4, spinSpeed: 0.6 },
  healthy:       { sizeMult: 1.0, particleMult: 1.0, opacity: 1.0, glowIntensity: 1.0, spinSpeed: 0.1 },
  unknown:       { sizeMult: 0.7, particleMult: 0.5, opacity: 0.4, glowIntensity: 0.5, spinSpeed: 0.0 },
  contradictory: { sizeMult: 1.5, particleMult: 2.0, opacity: 1.0, glowIntensity: 1.8, spinSpeed: 1.5 },
};

function createNodeObject(node: GraphNode): THREE.Object3D {
  const group = new THREE.Group();
  const hs = node.healthStatus ?? 'unknown';
  const prominence = HEALTH_PROMINENCE[hs];

  const baseRadius = Math.pow(TYPE_SIZE[node.type] ?? 5, 0.5) * 0.8;
  const radius = baseRadius * prominence.sizeMult;
  // Node color comes from the data layer (already set to health status color)
  const nodeColor = new THREE.Color(node.color);

  const complexity = {
    constitution: 4, department: 3, policy: 2, persona: 2,
    pipeline: 2, schema: 1, test: 1, ixql: 1,
  }[node.type] ?? 1;

  const intensity = ({
    constitution: 1.4, department: 1.2, policy: 1.0, persona: 1.0,
    pipeline: 0.9, schema: 0.8, test: 0.8, ixql: 0.7,
  }[node.type] ?? 0.8) * prominence.glowIntensity;

  // Store health state on group for animation tick
  group.userData.healthStatus = hs;

  // 1. Raymarched volumetric core — shape encodes artifact type
  const geoFactory = TYPE_GEOMETRY[node.type] ?? ((r: number) => new THREE.SphereGeometry(r, 32, 32));
  const coreGeo = geoFactory(radius * 0.5);
  if (!coreGeo.attributes.normal) coreGeo.computeVertexNormals();
  const coreMat = createVolumetricMaterial(nodeColor, complexity, intensity);
  const core = new THREE.Mesh(coreGeo, coreMat);
  core.userData = { isVolumetricCore: true };
  group.add(core);

  // 2. Fractal nebula sprite — outer halo with branching tendrils
  const fractalTex = generateFractalTexture(nodeColor, complexity);
  const spriteMat = new THREE.SpriteMaterial({
    map: fractalTex,
    transparent: true,
    opacity: 0.5 * prominence.opacity,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
  });
  const sprite = new THREE.Sprite(spriteMat);
  const spriteScale = radius * 3.0;
  sprite.scale.set(spriteScale, spriteScale, 1);
  group.add(sprite);

  // 3. Orbiting particle dust — count scaled by health prominence
  const dustCount = Math.floor(complexity * 10 * prominence.particleMult);
  const positions = new Float32Array(dustCount * 3);
  const dustColors = new Float32Array(dustCount * 3);
  for (let i = 0; i < dustCount; i++) {
    // Orbital ring distribution (not uniform sphere)
    const ringAngle = Math.random() * Math.PI * 2;
    const ringTilt = (Math.random() - 0.5) * 0.4;
    const r = radius * (0.6 + Math.random() * 1.2);
    positions[i * 3] = r * Math.cos(ringAngle);
    positions[i * 3 + 1] = r * Math.sin(ringAngle) * Math.cos(ringTilt);
    positions[i * 3 + 2] = r * Math.sin(ringAngle) * Math.sin(ringTilt) + (Math.random() - 0.5) * radius * 0.3;

    const v = 0.5 + Math.random() * 0.5;
    dustColors[i * 3] = nodeColor.r * v;
    dustColors[i * 3 + 1] = nodeColor.g * v;
    dustColors[i * 3 + 2] = nodeColor.b * v;
  }

  const dustGeo = new THREE.BufferGeometry();
  dustGeo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
  dustGeo.setAttribute('color', new THREE.BufferAttribute(dustColors, 3));
  const dustMat = new THREE.PointsMaterial({
    size: radius * 0.08,
    vertexColors: true,
    transparent: true,
    opacity: 0.5 * prominence.opacity,
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
    healthStatus: n.healthStatus,
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
  /** URL to fetch governance data. Defaults to '/api/governance'. Set to '' to use bundled static data. */
  liveDataUrl?: string;
  /** SignalR hub URL for real-time push updates (e.g., /hubs/governance). Preferred over polling. */
  liveHubUrl?: string;
  /** Poll interval in ms when WebSocket unavailable (default 30000) */
  pollIntervalMs?: number;
}

export const ForceRadiant: React.FC<ForceRadiantProps> = ({
  data,
  width = '100%',
  height = '100%',
  onNodeSelect,
  showDetailPanel = true,
  className = '',
  liveDataUrl,
  liveHubUrl,
  pollIntervalMs = 30000,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const graphRef = useRef<ReturnType<typeof ForceGraph3D> | null>(null);

  const [selectedNode, setSelectedNode] = useState<GovernanceNode | null>(null);
  const [graphData, setGraphData] = useState<GovernanceGraph | null>(null);
  const [activePanel, setActivePanel] = useState<PanelId | null>(null);
  const [graphIndex, setGraphIndex] = useState<GraphIndex | null>(null);
  const [algedonicSignals, setAlgedonicSignals] = useState<AlgedonicSignal[]>([]);
  const [backendStatus, setBackendStatus] = useState<'checking' | 'connected' | 'disconnected'>('checking');
  const [activeHealthTip, setActiveHealthTip] = useState<string | null>(null);

  // Algedonic effect refs — mutable state for animation loop (no re-render needed)
  const activeRipplesRef = useRef<Map<string, ActiveRipple>>(new Map());
  const edgePropagationsRef = useRef<Map<string, EdgePropagation>>(new Map());
  const pleasureWindowRef = useRef<number[]>([]);
  const bloomPassRef = useRef<UnrealBloomPass | null>(null);
  const surgeBloomRef = useRef<{ startTime: number; originalStrength: number } | null>(null);
  const solarFollowCameraRef = useRef(true); // when false, solar system stays in place (planet zoom)
  const trackedPlanetRef = useRef<string | null>(null); // mutable for animation loop
  const [trackedPlanetName, setTrackedPlanetName] = useState<string | null>(null); // for UI indicator
  const [criticState, setCriticState] = useState<CriticState>({
    phase: 'idle', result: null, history: [], lastAnalysis: null,
  });

  // Phase 3: IXql command handler — applies visual overrides to graph nodes
  const handleIxqlCommand = useCallback((result: IxqlParseResult) => {
    const fg = graphRef.current;
    if (!fg || !result.ok || !result.command) return;

    if (result.command.type === 'reset') {
      // Clear all IXql overrides
      fg.graphData().nodes.forEach((node: object) => {
        const n = node as GraphNode & { __threeObj?: THREE.Object3D };
        if (n.__threeObj) {
          n.__threeObj.userData.ixqlOverrides = undefined;
        }
      });
      return;
    }

    const cmd = result.command;
    if (cmd.target === 'nodes' && cmd.predicates && cmd.assignments) {
      fg.graphData().nodes.forEach((node: object) => {
        const n = node as GraphNode & { __threeObj?: THREE.Object3D };
        const matches = cmd.predicates!.every(p =>
          evaluatePredicate(p, n as unknown as Record<string, unknown>),
        );
        if (matches && n.__threeObj) {
          const overrides: Record<string, unknown> = {};
          cmd.assignments!.forEach(a => { overrides[a.property] = a.value; });
          n.__threeObj.userData.ixqlOverrides = overrides;
        }
      });
    }
  }, []);

  // ─── Create a ripple ring mesh at a world position ───
  const createRippleAtPosition = useCallback((
    fg: ReturnType<typeof ForceGraph3D>,
    x: number, y: number, z: number,
    color: string, startTime: number,
    duration: number, maxScale: number,
    id: string,
  ) => {
    // Cap concurrent ripples
    if (activeRipplesRef.current.size >= MAX_CONCURRENT_RIPPLES) {
      // Remove the oldest ripple
      const oldest = [...activeRipplesRef.current.entries()].sort((a, b) => a[1].startTime - b[1].startTime)[0];
      if (oldest) {
        fg.scene().remove(oldest[1].mesh);
        oldest[1].mesh.geometry.dispose();
        (oldest[1].mesh.material as THREE.Material).dispose();
        activeRipplesRef.current.delete(oldest[0]);
      }
    }

    const ringGeo = new THREE.RingGeometry(1.5, 2.5, 32);
    const ringMat = new THREE.MeshBasicMaterial({
      color: new THREE.Color(color),
      transparent: true,
      opacity: 0.8,
      side: THREE.DoubleSide,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    });
    const ringMesh = new THREE.Mesh(ringGeo, ringMat);
    ringMesh.position.set(x, y, z);
    // Face camera by looking up (billboard effect happens in tick via lookAt)
    ringMesh.userData.isBillboard = true;
    fg.scene().add(ringMesh);

    activeRipplesRef.current.set(id, {
      mesh: ringMesh,
      startTime,
      color,
      duration,
      maxScale,
    });
  }, []);

  // ─── Start edge propagation from a source node ───
  const startEdgePropagation = useCallback((
    fg: ReturnType<typeof ForceGraph3D>,
    sourceNodeId: string,
    color: string,
    startTime: number,
  ) => {
    const graphDataObj = fg.graphData() as { nodes: GraphNode[]; links: GraphLink[] };
    const links = graphDataObj.links;

    // BFS to find edges up to MAX_PROPAGATION_HOPS hops away
    const visited = new Set<string>([sourceNodeId]);
    let frontier = [sourceNodeId];

    for (let hop = 0; hop < MAX_PROPAGATION_HOPS; hop++) {
      const nextFrontier: string[] = [];
      for (const nodeId of frontier) {
        for (const link of links) {
          const srcId = typeof link.source === 'string' ? link.source : (link.source as GraphNode).id;
          const tgtId = typeof link.target === 'string' ? link.target : (link.target as GraphNode).id;
          const linkId = `${srcId}-${tgtId}`;

          if (srcId === nodeId || tgtId === nodeId) {
            const neighborId = srcId === nodeId ? tgtId : srcId;
            if (!visited.has(neighborId)) {
              edgePropagationsRef.current.set(linkId, {
                startTime: startTime + hop * PROPAGATION_HOP_DURATION,
                color,
                hop,
              });
              nextFrontier.push(neighborId);
            }
          }
        }
      }
      for (const nid of nextFrontier) visited.add(nid);
      frontier = nextFrontier;
    }
  }, []);

  // ─── Trigger compounding surge ───
  const triggerCompoundingSurge = useCallback((
    fg: ReturnType<typeof ForceGraph3D>,
    sourceNodeId: string | undefined,
    now: number,
  ) => {
    // Emit frontend-generated compounding_surge signal
    const surgeSignal: AlgedonicSignal = {
      id: `surge-${Date.now()}`,
      timestamp: new Date().toISOString(),
      signal: 'compounding_surge',
      type: 'pleasure',
      source: 'frontend',
      severity: 'info',
      status: 'active',
      description: 'Compounding surge detected — multiple positive signals in rapid succession',
    };
    setAlgedonicSignals(prev => [surgeSignal, ...prev].slice(0, 100));

    const graphNodes = (fg.graphData() as { nodes: GraphNode[] }).nodes;

    // Find connected cluster via BFS from source node (or all nodes if no source)
    let clusterNodes: (GraphNode & { x?: number; y?: number; z?: number })[];
    if (sourceNodeId) {
      const links = (fg.graphData() as { links: GraphLink[] }).links;
      const visited = new Set<string>([sourceNodeId]);
      const queue = [sourceNodeId];
      while (queue.length > 0) {
        const current = queue.shift()!;
        for (const link of links) {
          const srcId = typeof link.source === 'string' ? link.source : (link.source as GraphNode).id;
          const tgtId = typeof link.target === 'string' ? link.target : (link.target as GraphNode).id;
          if (srcId === current && !visited.has(tgtId)) { visited.add(tgtId); queue.push(tgtId); }
          if (tgtId === current && !visited.has(srcId)) { visited.add(srcId); queue.push(srcId); }
        }
      }
      clusterNodes = graphNodes.filter(n => visited.has(n.id)) as (GraphNode & { x?: number; y?: number; z?: number })[];
    } else {
      clusterNodes = graphNodes as (GraphNode & { x?: number; y?: number; z?: number })[];
    }

    // Create oversized ripple on all cluster nodes
    for (const node of clusterNodes) {
      if (node.x !== undefined) {
        createRippleAtPosition(
          fg, node.x, node.y ?? 0, node.z ?? 0,
          '#FFFFAA', now, SURGE_RIPPLE_DURATION, SURGE_RIPPLE_MAX_SCALE,
          `surge-${node.id}-${Date.now()}`,
        );
      }
    }

    // Boost bloom
    const bloomPass = bloomPassRef.current;
    if (bloomPass) {
      surgeBloomRef.current = {
        startTime: now,
        originalStrength: bloomPass.strength,
      };
      bloomPass.strength = SURGE_BLOOM_STRENGTH;
    }

    // Edge propagation from source with unlimited hops (use all edges)
    if (sourceNodeId) {
      const links = (fg.graphData() as { links: GraphLink[] }).links;
      for (const link of links) {
        const srcId = typeof link.source === 'string' ? link.source : (link.source as GraphNode).id;
        const tgtId = typeof link.target === 'string' ? link.target : (link.target as GraphNode).id;
        const linkId = `${srcId}-${tgtId}`;
        if (!edgePropagationsRef.current.has(linkId)) {
          edgePropagationsRef.current.set(linkId, {
            startTime: now,
            color: '#FFFFAA',
            hop: 0,
          });
        }
      }
    }
  }, [createRippleAtPosition]);

  // ─── Algedonic signal handler — creates ripples, propagation, checks compounding ───
  const handleAlgedonicSignal = useCallback((signal: AlgedonicSignalEvent) => {
    // Append to panel state
    const panelSignal: AlgedonicSignal = {
      id: signal.id,
      timestamp: signal.timestamp,
      signal: signal.signal,
      type: signal.type,
      source: signal.source,
      severity: signal.severity,
      status: signal.status,
      description: signal.description,
    };
    setAlgedonicSignals(prev => [panelSignal, ...prev].slice(0, 100));

    const fg = graphRef.current;
    if (!fg) return;

    const now = Date.now() * 0.001;
    const signalColor = signal.type === 'pain' ? '#FF4444' : '#FFD700';

    // ─── Unit 4: Node ripple ───
    if (signal.nodeId) {
      const graphNodes = (fg.graphData() as { nodes: GraphNode[] }).nodes;
      const targetNode = graphNodes.find(n => n.id === signal.nodeId) as (GraphNode & { x?: number; y?: number; z?: number }) | undefined;

      if (targetNode?.x !== undefined) {
        createRippleAtPosition(
          fg, targetNode.x, targetNode.y ?? 0, targetNode.z ?? 0,
          signalColor, now, RIPPLE_DURATION, RIPPLE_MAX_SCALE, signal.id,
        );

        // ─── Unit 5: Edge propagation ───
        startEdgePropagation(fg, signal.nodeId, signalColor, now);
      }
    }

    // ─── Unit 6: Compounding surge — track pleasure signals ───
    if (signal.type === 'pleasure') {
      const nowMs = Date.now();
      const window = pleasureWindowRef.current;
      window.push(nowMs);
      // Filter entries older than the compounding window
      const cutoff = nowMs - COMPOUNDING_WINDOW_SEC * 1000;
      pleasureWindowRef.current = window.filter(ts => ts > cutoff);

      if (pleasureWindowRef.current.length >= COMPOUNDING_THRESHOLD) {
        triggerCompoundingSurge(fg, signal.nodeId, now);
        // Reset window to prevent immediate re-trigger
        pleasureWindowRef.current = [];
      }
    }
  }, [createRippleAtPosition, startEdgePropagation, triggerCompoundingSurge]);

  // ─── Initialize ───
  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    let disposed = false;
    let autoZoomTimeoutOuter: ReturnType<typeof setTimeout> | null = null;
    let pollingHandleOuter: LivePollingHandle | undefined;
    let cloudCleanupOuter: (() => void) | undefined;
    let criticCleanupOuter: (() => void) | undefined;
    let solarMouseMoveHandler: ((e: MouseEvent) => void) | null = null;
    let milkyWayToggleHandler: ((e: KeyboardEvent) => void) | null = null;
    let solarClickHandler: (() => void) | null = null;

    // Load data — try API first, fall back to static
    const initGraph = async () => {
      const graph = liveDataUrl
        ? await loadGovernanceDataAsync(liveDataUrl)
        : loadGovernanceData(data);
      if (disposed) return;
      initScene(graph);
    };

    const initScene = (graph: ReturnType<typeof loadGovernanceData>) => {

    setGraphData(graph);
    setGraphIndex(buildGraphIndex(graph));

    const forceData = toForceData(graph);
    const _healthStatus = getHealthStatus(graph.globalHealth.resilienceScore);

    // ─── Phase 1.1: Node lookup map — O(1) instead of O(n) per link callback ───
    const nodeMap = new Map<string, GraphNode>();
    forceData.nodes.forEach((n: object) => {
      const gn = n as GraphNode;
      nodeMap.set(gn.id, gn);
    });
    const getLinkNodeId = (endpoint: string | object): string =>
      typeof endpoint === 'string' ? endpoint : (endpoint as GraphNode).id;
    const getLinkNodes = (l: GraphLink) => ({
      src: nodeMap.get(getLinkNodeId(l.source)),
      tgt: nodeMap.get(getLinkNodeId(l.target)),
    });

    // ─── Phase 2.1: Importance scores for adaptive node sizing ───
    const edgeCountMap = new Map<string, number>();
    forceData.links.forEach((l: object) => {
      const link = l as GraphLink;
      const srcId = getLinkNodeId(link.source);
      const tgtId = getLinkNodeId(link.target);
      edgeCountMap.set(srcId, (edgeCountMap.get(srcId) ?? 0) + 1);
      edgeCountMap.set(tgtId, (edgeCountMap.get(tgtId) ?? 0) + 1);
    });
    const maxEdges = Math.max(...edgeCountMap.values(), 1);
    const maxErgol = Math.max(...graph.nodes.map(n => n.health?.ergolCount ?? 0), 1);

    const importanceMap = new Map<string, number>();
    graph.nodes.forEach(n => {
      const normEdge = (edgeCountMap.get(n.id) ?? 0) / maxEdges;
      const normErgol = (n.health?.ergolCount ?? 0) / maxErgol;
      const resilience = n.health?.resilienceScore ?? 0.5;
      const staleness = n.health?.staleness ?? 0;
      const importance = 0.4 * normEdge + 0.3 * normErgol + 0.2 * resilience + 0.1 * (1 - staleness);
      importanceMap.set(n.id, importance);
    });

    // ─── Phase 1.4: Cancellable auto-zoom timeout ───
    let autoZoomTimeout: ReturnType<typeof setTimeout> | null = null;
    let userInteracted = false;

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
        const typeLabel = n.type.charAt(0).toUpperCase() + n.type.slice(1);
        const hs = n.healthStatus ?? 'unknown';
        const hsColor = HEALTH_STATUS_COLORS[hs] ?? '#888888';
        const hsLabel = hs.charAt(0).toUpperCase() + hs.slice(1);
        const healthInfo = n.health
          ? `<div style="font-size:9px;color:#8b949e;margin-top:2px">R: ${(n.health.resilienceScore * 100).toFixed(0)}% · E: ${n.health.ergolCount} · L: ${n.health.lolliCount}</div>`
          : '';
        return `<div style="font-family:'JetBrains Mono',monospace;padding:4px 8px;background:rgba(0,0,8,0.85);border:1px solid ${hsColor}44;border-radius:4px;backdrop-filter:blur(4px)">
          <div style="color:${hsColor};font-size:11px;font-weight:600">${n.name}</div>
          <div style="color:#8b949e;font-size:9px;text-transform:uppercase;letter-spacing:0.5px">${typeLabel}${n.repo ? ' · ' + n.repo : ''} · <span style="color:${hsColor}">${hsLabel}</span></div>
          ${healthInfo}
        </div>`;
      })

      // Edge rendering — undulating energy flow between collaborating nodes
      .linkColor((link: object) => {
        const l = link as GraphLink;
        // Check for active algedonic propagation
        const srcId = getLinkNodeId(l.source);
        const tgtId = getLinkNodeId(l.target);
        const linkId = `${srcId}-${tgtId}`;
        const prop = edgePropagationsRef.current.get(linkId);
        if (prop) {
          const elapsed = Date.now() * 0.001 - prop.startTime;
          const window = PROPAGATION_HOP_DURATION * 2;
          if (elapsed >= 0 && elapsed < window) {
            // Fade the propagation color over the window
            const fade = 1 - elapsed / window;
            const c = new THREE.Color(prop.color);
            // Mix with original color based on fade
            const orig = new THREE.Color(l.color);
            orig.lerp(c, fade);
            return '#' + orig.getHexString();
          }
        }
        return l.color;
      })
      .linkWidth((link: object) => {
        const l = link as GraphLink;
        const { src, tgt } = getLinkNodes(l);
        const srcActive = src?.healthStatus && src.healthStatus !== 'unknown';
        const tgtActive = tgt?.healthStatus && tgt.healthStatus !== 'unknown';
        return (srcActive && tgtActive) ? l.width * 2.0 : l.width;
      })
      .linkOpacity(0.25)
      .linkDirectionalParticles((link: object) => {
        const { src, tgt } = getLinkNodes(link as GraphLink);
        const srcActive = src?.healthStatus && src.healthStatus !== 'unknown';
        const tgtActive = tgt?.healthStatus && tgt.healthStatus !== 'unknown';
        return (srcActive && tgtActive) ? 6 : 2;
      })
      .linkDirectionalParticleWidth((link: object) => {
        const { src, tgt } = getLinkNodes(link as GraphLink);
        const bothError = src?.healthStatus === 'error' || tgt?.healthStatus === 'error';
        return bothError ? 2.5 : 1.0;
      })
      .linkDirectionalParticleSpeed((link: object) => {
        const { src, tgt } = getLinkNodes(link as GraphLink);
        const anyError = src?.healthStatus === 'error' || tgt?.healthStatus === 'error';
        const anyWarning = src?.healthStatus === 'warning' || tgt?.healthStatus === 'warning';
        return anyError ? 0.006 : anyWarning ? 0.003 : 0.002;
      })
      .linkDirectionalParticleColor((link: object) => {
        const { src, tgt } = getLinkNodes(link as GraphLink);
        const urgentNode = [src, tgt].find((n) => n?.healthStatus === 'error')
          ?? [src, tgt].find((n) => n?.healthStatus === 'contradictory')
          ?? [src, tgt].find((n) => n?.healthStatus === 'warning');
        return urgentNode ? urgentNode.color : (link as GraphLink).color;
      })
      .linkCurvature((link: object) => {
        // Dynamic curvature — stored on link object, animated per tick
        return (link as GraphLink & { _curvature?: number })._curvature ?? 0.15;
      })

      // Interaction
      .onNodeClick((node: object) => {
        const n = node as GraphNode;
        const govNode = nodeMap.get(n.id) ? graph.nodes.find((gn) => gn.id === n.id) ?? null : null;
        setSelectedNode(govNode);
        if (govNode) setActivePanel('detail');
        onNodeSelect?.(govNode);
        // Phase 1.4: Cancel auto-zoom on user interaction
        userInteracted = true;
        if (autoZoomTimeout) { clearTimeout(autoZoomTimeout); autoZoomTimeout = null; }
        // Stop planet tracking when clicking governance graph
        trackedPlanetRef.current = null;
        setTrackedPlanetName(null);
        solarFollowCameraRef.current = true;

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
        // Phase 1.4: Cancel auto-zoom on user interaction
        userInteracted = true;
        if (autoZoomTimeout) { clearTimeout(autoZoomTimeout); autoZoomTimeout = null; }
        // Stop planet tracking
        trackedPlanetRef.current = null;
        setTrackedPlanetName(null);
        solarFollowCameraRef.current = true;
      })
      .onNodeHover((node: object | null, prevNode: object | null) => {
        // Phase 1.3: Store hover state so breathing animation doesn't override
        if (prevNode) {
          const prev = prevNode as GraphNode & { __threeObj?: THREE.Object3D };
          if (prev.__threeObj) prev.__threeObj.userData.isHovered = false;
        }
        if (node) {
          const n = node as GraphNode & { __threeObj?: THREE.Object3D };
          if (n.__threeObj) {
            n.__threeObj.userData.isHovered = true;
            n.__threeObj.scale.setScalar(1.3);
          }
        }
        if (containerRef.current) {
          containerRef.current.style.cursor = node ? 'pointer' : 'grab';
        }
      })

      // Force configuration — cluster by hierarchy
      .d3AlphaDecay(0.02)
      .d3VelocityDecay(0.3);

    // Add bloom post-processing — increased strength so red/magenta health nodes pop
    const bloomPass = new UnrealBloomPass(
      new THREE.Vector2(container.clientWidth, container.clientHeight),
      0.6,   // strength (up from 0.4 — health colors need to glow)
      0.6,   // radius
      0.5,   // threshold (lower = more bloom on bright health nodes)
    );
    fg.postProcessingComposer().addPass(bloomPass);
    bloomPassRef.current = bloomPass;

    // Chromatic aberration post-processing
    const chromaticShader = {
      uniforms: {
        tDiffuse: { value: null },
        uOffset: { value: new THREE.Vector2(0.0008, 0.0008) },
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

    // ─── ADAPTIVE QUALITY — auto-downgrade on crappy GPUs ───
    let frameCount = 0;
    let lastFpsCheck = Date.now();
    let currentFps = 60;
    let qualityLevel: 'high' | 'medium' | 'low' = 'high';
    // FPS counter element
    const fpsEl = document.createElement('div');
    fpsEl.style.cssText = 'position:absolute;top:8px;left:8px;color:#8b949e;font:11px/1 "JetBrains Mono",monospace;z-index:20;pointer-events:none;';
    container.style.position = 'relative';
    container.appendChild(fpsEl);

    let lastCameraSave = 0;
    fg.onEngineTick(() => {
      const t = Date.now() * 0.001;

      // ─── Save camera state every 2 seconds ───
      const now2 = Date.now();
      if (now2 - lastCameraSave > 2000) {
        lastCameraSave = now2;
        try {
          const cp = fgCam.position;
          const ct = fg.scene().position; // lookAt target approximation
          localStorage.setItem('prime-radiant-camera', JSON.stringify({
            px: cp.x, py: cp.y, pz: cp.z,
            lx: ct.x, ly: ct.y, lz: ct.z,
          }));
        } catch { /* quota exceeded or private browsing */ }
      }

      // ─── FPS measurement + adaptive quality ───
      frameCount++;
      const now = Date.now();
      if (now - lastFpsCheck >= 1000) {
        currentFps = frameCount;
        frameCount = 0;
        lastFpsCheck = now;
        fpsEl.textContent = `${currentFps} FPS${qualityLevel !== 'high' ? ' [' + qualityLevel + ']' : ''}`;
        fpsEl.style.color = currentFps >= 45 ? '#33CC66' : currentFps >= 25 ? '#FFB300' : '#FF4444';

        // Auto-downgrade
        if (currentFps < 20 && qualityLevel !== 'low') {
          qualityLevel = 'low';
          ambientDust.visible = false;
          starField.visible = false;
          demerzelFace.visible = false;
          bloomPass.strength = 0.2;
        } else if (currentFps < 35 && qualityLevel === 'high') {
          qualityLevel = 'medium';
          ambientDust.visible = false;
          bloomPass.strength = 0.4;
        } else if (currentFps >= 50 && qualityLevel !== 'high') {
          qualityLevel = 'high';
          ambientDust.visible = true;
          starField.visible = true;
          demerzelFace.visible = true;
          bloomPass.strength = 0.6;
        }
      }
      fg.graphData().nodes.forEach((node: object) => {
        const n = node as GraphNode & { __threeObj?: THREE.Object3D };
        if (!n.__threeObj) return;

        const group = n.__threeObj as THREE.Group;
        const phase = (n.id?.length ?? 3) * 0.7;
        const hs = group.userData.healthStatus as GovernanceHealthStatus | undefined ?? 'unknown';

        // Pulse behavior driven by health state
        let breathe = 1.0;
        if (hs === 'error') {
          // Fast urgent pulse — something is broken
          const raw = Math.sin(t * 3.0 + phase);
          breathe = 1 + raw * 0.08;
        } else if (hs === 'warning') {
          // Slow gentle pulse — needs attention
          const raw = Math.sin(t * 1.2 + phase);
          breathe = 1 + raw * 0.05;
        } else if (hs === 'contradictory') {
          // Erratic flicker — conflicting signals
          const raw = Math.sin(t * 4.0 + phase) * Math.sin(t * 2.7 + phase * 1.3);
          breathe = 1 + raw * 0.1;
        } else if (hs === 'unknown') {
          // No pulse — dim and static
          breathe = 1.0;
        } else {
          // Healthy — very calm, barely noticeable breathing
          const raw = Math.sin(t * 0.5 + phase);
          breathe = 1 + raw * 0.02;
        }
        // Spin + prominence — must come before adaptive sizing which uses prom
        const prom = HEALTH_PROMINENCE[hs];

        // Phase 2.1: Adaptive sizing — scale by importance
        const importance = importanceMap.get(n.id) ?? 0.5;
        const importanceScale = 0.7 + importance * 0.6; // range: 0.7 – 1.3

        // Phase 2.2: Activity effects — stale nodes dim
        const staleness = n.health?.staleness ?? 0;
        const stalenessDim = staleness > 0.5 ? 1 - (staleness - 0.5) * 0.6 : 1.0;

        // Phase 1.3: Skip breathing scale when hovered — let hover pop (1.3x) persist
        if (!group.userData.isHovered) {
          const targetScale = breathe * importanceScale;
          const currentScale = group.userData.currentScale ?? targetScale;
          const newScale = currentScale + (targetScale - currentScale) * 0.05;
          group.userData.currentScale = newScale;
          group.scale.setScalar(newScale);
        }

        // Phase 2.2: Activity effects — dim stale nodes
        if (staleness > 0.5) {
          group.traverse((child: THREE.Object3D) => {
            if (child instanceof THREE.Mesh && child.material instanceof THREE.ShaderMaterial) {
              if (child.material.uniforms?.uOpacity) {
                child.material.uniforms.uOpacity.value = prom.opacity * stalenessDim;
              }
            }
            if (child instanceof THREE.Sprite) {
              (child.material as THREE.SpriteMaterial).opacity = 0.5 * prom.opacity * stalenessDim;
            }
          });
        }

        // Spin — active nodes rotate, faster = more urgent
        if (prom.spinSpeed > 0) {
          // Contradictory gets direction wobble (oscillating axis)
          if (hs === 'contradictory') {
            group.rotation.y = t * prom.spinSpeed * Math.sin(t * 0.7 + phase);
            group.rotation.x = Math.sin(t * 1.3 + phase) * 0.3;
          } else {
            group.rotation.y = t * prom.spinSpeed;
          }
        }

        // Update volumetric shader time for fractal swirl
        for (const child of group.children) {
          if (child instanceof THREE.Mesh && child.userData.isVolumetricCore) {
            const mat = child.material as THREE.ShaderMaterial;
            if (mat.uniforms?.uTime) {
              mat.uniforms.uTime.value = t;
            }
            // Phase 3: Apply IXql glow/color overrides
            const overrides = group.userData.ixqlOverrides as Record<string, unknown> | undefined;
            if (overrides) {
              if (overrides.glow && mat.uniforms?.uColor) {
                mat.uniforms.uColor.value = new THREE.Color(overrides.glow as string);
              }
              if (overrides.opacity !== undefined && mat.uniforms?.uOpacity) {
                mat.uniforms.uOpacity.value = Number(overrides.opacity);
              }
            }
          }
        }
      });

      // ─── Starfield follows camera (skybox behavior) ───
      // Stars follow camera exactly, but Milky Way lags slightly for parallax depth
      const camPos = fg.camera().position;
      starField.position.copy(camPos);
      // Milky Way parallax: offset by a fraction of camera position → feels infinitely far
      milkyWayMesh.position.set(-camPos.x * 0.002, -camPos.y * 0.002, -camPos.z * 0.002);

      // ─── Edge undulation — sinusoidal curvature on active edges ───
      // Skip on low quality for performance
      if (qualityLevel === 'low') { /* skip undulation */ } else {
      // Phase 2.3: Type-specific edge undulation (R3)
      const links = fg.graphData().links as (GraphLink & { _curvature?: number })[];
      links.forEach((link, idx: number) => {
        const { src, tgt } = getLinkNodes(link);
        const srcActive = src?.healthStatus && src.healthStatus !== 'unknown';
        const tgtActive = tgt?.healthStatus && tgt.healthStatus !== 'unknown';

        // Type-specific undulation parameters
        const edgeType = link.type ?? 'policy-persona';
        let amplitude = 0.1;
        let frequency = 0.4;

        if (edgeType === 'pipeline-flow') {
          amplitude = 0.3; frequency = 0.5; // Smooth sine, source→target
        } else if (edgeType === 'constitutional-hierarchy') {
          amplitude = 0.15; frequency = 0.2; // Stately golden wave
        } else if (edgeType === 'cross-repo') {
          amplitude = 0.05; frequency = 2.0; // Shimmer
        } else if (edgeType === 'lolli') {
          // Erratic jitter for dead references
          const jitter = (Math.random() - 0.5) * 0.2;
          link._curvature = 0.15 + jitter;
          return;
        }

        if (srcActive && tgtActive) {
          const wave = Math.sin(t * frequency * Math.PI * 2 + idx * 0.8) * amplitude;
          link._curvature = 0.15 + wave;
        } else {
          // Inactive edges: very subtle breathing
          link._curvature = 0.15 + Math.sin(t * 0.3 + idx) * 0.02;
        }
      });
      } // end quality gate

      // ─── Ambient dust drift ───
      const dPos = ambientDust.geometry.attributes.position as THREE.BufferAttribute;
      for (let i = 0; i < dustCount; i++) {
        let x = dPos.getX(i) + dustVelocities[i*3] * 0.016;
        let y = dPos.getY(i) + dustVelocities[i*3+1] * 0.016;
        let z = dPos.getZ(i) + dustVelocities[i*3+2] * 0.016;
        const halfR = dustRange / 2;
        if (Math.abs(x) > halfR) x *= -0.9;
        if (Math.abs(y) > halfR) y *= -0.9;
        if (Math.abs(z) > halfR) z *= -0.9;
        dPos.setXYZ(i, x, y, z);
      }
      dPos.needsUpdate = true;

      // ─── Orbital rings track their nodes ───
      for (const ring of ringMeshes) {
        const nodeId = ring.userData.trackNodeId as string;
        const rNode = (fg.graphData() as { nodes: GraphNode[] }).nodes.find((nd) => nd.id === nodeId) as (GraphNode & { x?: number; y?: number; z?: number }) | undefined;
        if (rNode?.x !== undefined) {
          ring.position.set(rNode.x, rNode.y ?? 0, rNode.z ?? 0);
          ring.rotation.x = ring.userData.ringTilt + t * 0.2;
          ring.rotation.z = t * 0.15;
        }
      }

      // ─── God ray animation ───
      (godRay.material as THREE.ShaderMaterial).uniforms.uTime.value = t;
      const gr2Mat = godRay2.material as THREE.ShaderMaterial;
      if (gr2Mat.uniforms?.uTime) gr2Mat.uniforms.uTime.value = t;

      // ─── HUD companions — positioned in world space near graph ───
      const cam = fg.camera() as THREE.PerspectiveCamera;

      // TARS — far left, lower
      updateTarsRobot(tarsRobot, t);
      const tarsOffset = new THREE.Vector3(-50, -28, -50);
      tarsOffset.applyQuaternion(cam.quaternion);
      tarsRobot.position.copy(cam.position).add(tarsOffset);
      tarsRobot.quaternion.copy(cam.quaternion);

      // Demerzel face — far left, above TARS
      updateDemerzelFace(demerzelFace, t, cam.position, false);
      const faceOffset = new THREE.Vector3(-50, -8, -40);
      faceOffset.applyQuaternion(cam.quaternion);
      demerzelFace.position.copy(cam.position).add(faceOffset);
      demerzelFace.quaternion.copy(cam.quaternion);

      // (Trantor removed — replaced by Earth + nebulae)

      // ─── Solar system — compact orrery, top-right of view ───
      // Phase 1.2: Quality-gate solar system updates
      if (qualityLevel !== 'low') {
        solarSystem.userData.qualityLevel = qualityLevel; // pass to flare system
        if (qualityLevel === 'high' || frameCount % 3 === 0) {
          updateSolarSystem(solarSystem, t);
        }
      }
      if (solarFollowCameraRef.current) {
        const solarOffset = new THREE.Vector3(6, 4, -12);
        solarOffset.applyQuaternion(cam.quaternion);
        solarSystem.position.copy(cam.position).add(solarOffset);
      } else {
        // ─── Planet tracking mode — camera follows orbiting planet ───
        const tracked = trackedPlanetRef.current;
        if (tracked) {
          const trackedObj = solarSystem.getObjectByName(tracked);
          if (trackedObj) {
            const wp = new THREE.Vector3();
            trackedObj.getWorldPosition(wp);
            // Offset proportional to planet size — fills most of viewport
            // Get the planet's bounding sphere radius for proper framing
            const geo = (trackedObj as THREE.Mesh).geometry;
            const planetRadius = geo?.boundingSphere?.radius ?? 0.02;
            const viewDist = planetRadius * 1.8; // close enough to nearly fill screen
            const offset = new THREE.Vector3(0, planetRadius * 0.05, viewDist); // nearly centered, slight upward tilt
            const targetCamPos = wp.clone().add(offset);
            // Use matched lerp speeds to prevent jitter from speed mismatch
            const lerpSpeed = 0.08;
            cam.position.lerp(targetCamPos, lerpSpeed);
            const controls = fg.controls() as { target?: THREE.Vector3 };
            if (controls.target) {
              controls.target.lerp(wp, lerpSpeed);
            }
          }
        } else {
          // Auto-resume follow when camera moves far from the solar system
          const distToSolar = cam.position.distanceTo(solarSystem.position);
          if (distToSolar > 20) {
            solarFollowCameraRef.current = true;
          }
        }
      }

      // ─── Jarvis Space Station — top-left of view ───
      if (qualityLevel !== 'low') {
        updateSpaceStation(spaceStation, t);
      }
      const stationOffset = new THREE.Vector3(-8, 8, -20);
      stationOffset.applyQuaternion(cam.quaternion);
      spaceStation.position.copy(cam.position).add(stationOffset);

      // ─── Algedonic ripple animation ───
      const ripples = activeRipplesRef.current;
      for (const [rippleId, ripple] of ripples.entries()) {
        const elapsed = t - ripple.startTime;
        if (elapsed >= ripple.duration) {
          // Animation complete — remove and dispose
          fg.scene().remove(ripple.mesh);
          ripple.mesh.geometry.dispose();
          (ripple.mesh.material as THREE.Material).dispose();
          ripples.delete(rippleId);
          continue;
        }

        const progress = elapsed / ripple.duration;
        // Ease-out: fast start, slow end
        const eased = 1 - Math.pow(1 - progress, 2);

        // Scale from 1 to maxScale
        const scale = 1 + eased * (ripple.maxScale - 1);
        ripple.mesh.scale.setScalar(scale);

        // Opacity from 0.8 to 0
        const opacity = 0.8 * (1 - progress);
        (ripple.mesh.material as THREE.MeshBasicMaterial).opacity = opacity;

        // Billboard: face camera
        if (ripple.mesh.userData.isBillboard) {
          ripple.mesh.lookAt(cam.position);
        }
      }

      // ─── Algedonic edge propagation color ───
      const propagations = edgePropagationsRef.current;
      for (const [propId, prop] of propagations.entries()) {
        const elapsed = t - prop.startTime;
        const window = PROPAGATION_HOP_DURATION * 2; // visible duration per link
        if (elapsed > window) {
          propagations.delete(propId);
        }
      }

      // ─── Compounding surge bloom ease-back ───
      const surgeBloom = surgeBloomRef.current;
      if (surgeBloom && bloomPass) {
        const elapsed = t - surgeBloom.startTime;
        if (elapsed >= SURGE_BLOOM_DURATION) {
          bloomPass.strength = surgeBloom.originalStrength;
          surgeBloomRef.current = null;
        } else {
          // Ease back from SURGE_BLOOM_STRENGTH to original
          const progress = elapsed / SURGE_BLOOM_DURATION;
          const eased = progress * progress; // ease-in (slow start)
          bloomPass.strength = SURGE_BLOOM_STRENGTH + (surgeBloom.originalStrength - SURGE_BLOOM_STRENGTH) * eased;
        }
      }
    });

    // Slow auto-rotate
    const controls = fg.controls() as {
      autoRotate?: boolean; autoRotateSpeed?: number;
      enableDamping?: boolean; dampingFactor?: number;
      zoomSpeed?: number;
    };
    if (controls) {
      controls.autoRotate = true;
      controls.autoRotateSpeed = 0.3;
      controls.enableDamping = true;
      controls.dampingFactor = 0.08;
      controls.zoomSpeed = 0.8;
      (controls as Record<string, unknown>).minDistance = 0.05;
      (controls as Record<string, unknown>).maxDistance = 500;
    }

    // ─── AMBIENT PARTICLE FIELD ───
    // Reduced from 5000 for performance
    const dustCount = 1500;
    const dustPositions = new Float32Array(dustCount * 3);
    const dustVelocities = new Float32Array(dustCount * 3);
    const dustColors = new Float32Array(dustCount * 3);
    const dustRange = 400;
    for (let i = 0; i < dustCount; i++) {
      dustPositions[i*3] = (Math.random() - 0.5) * dustRange;
      dustPositions[i*3+1] = (Math.random() - 0.5) * dustRange;
      dustPositions[i*3+2] = (Math.random() - 0.5) * dustRange;
      dustVelocities[i*3] = (Math.random() - 0.5) * 0.3;
      dustVelocities[i*3+1] = (Math.random() - 0.5) * 0.3;
      dustVelocities[i*3+2] = (Math.random() - 0.5) * 0.3;
      // Mix of warm and cool dust
      const hue = Math.random();
      if (hue < 0.3) { dustColors[i*3] = 0.4; dustColors[i*3+1] = 0.35; dustColors[i*3+2] = 0.15; }     // warm gold
      else if (hue < 0.6) { dustColors[i*3] = 0.1; dustColors[i*3+1] = 0.3; dustColors[i*3+2] = 0.35; }  // cool cyan
      else { dustColors[i*3] = 0.2; dustColors[i*3+1] = 0.15; dustColors[i*3+2] = 0.35; }                 // violet
    }
    const dustGeo = new THREE.BufferGeometry();
    dustGeo.setAttribute('position', new THREE.BufferAttribute(dustPositions, 3));
    dustGeo.setAttribute('color', new THREE.BufferAttribute(dustColors, 3));
    const dustMat = new THREE.PointsMaterial({
      size: 0.3, vertexColors: true, transparent: true, opacity: 0.35,
      blending: THREE.AdditiveBlending, depthWrite: false, sizeAttenuation: true,
    });
    const ambientDust = new THREE.Points(dustGeo, dustMat);
    ambientDust.name = 'ambient-dust';
    fg.scene().add(ambientDust);

    // ─── SKYBOX — nebula background sphere + multi-layer starfield ───

    // Layer 0: Deep space gradient sphere (subtle purple-blue nebula)
    const skyGeo = new THREE.SphereGeometry(5000, 32, 32);
    const skyMat = new THREE.ShaderMaterial({
      side: THREE.BackSide,
      depthWrite: false,
      uniforms: {},
      vertexShader: `varying vec3 vWorldPos; void main() { vWorldPos = position; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }`,
      fragmentShader: `
        varying vec3 vWorldPos;

        vec3 hash33(vec3 p) {
          p = fract(p * vec3(443.897, 441.423, 437.195));
          p += dot(p, p.yzx + 19.19);
          return fract((p.xxy + p.yxx) * p.zyx);
        }
        float noise3d(vec3 p) {
          vec3 i = floor(p); vec3 f = fract(p);
          f = f*f*(3.0-2.0*f);
          return mix(mix(mix(dot(hash33(i),f), dot(hash33(i+vec3(1,0,0)),f-vec3(1,0,0)), f.x),
            mix(dot(hash33(i+vec3(0,1,0)),f-vec3(0,1,0)), dot(hash33(i+vec3(1,1,0)),f-vec3(1,1,0)), f.x), f.y),
            mix(mix(dot(hash33(i+vec3(0,0,1)),f-vec3(0,0,1)), dot(hash33(i+vec3(1,0,1)),f-vec3(1,0,1)), f.x),
            mix(dot(hash33(i+vec3(0,1,1)),f-vec3(0,1,1)), dot(hash33(i+vec3(1,1,1)),f-vec3(1,1,1)), f.x), f.y), f.z)*0.5+0.5;
        }
        float fbm(vec3 p) {
          float v=0.0, a=0.5;
          for(int i=0;i<4;i++) { v+=a*noise3d(p); p*=2.1; a*=0.5; }
          return v;
        }

        void main() {
          vec3 dir = normalize(vWorldPos);
          float y = dir.y;

          // Base gradient — near black space
          vec3 col = mix(vec3(0.003, 0.002, 0.008), vec3(0.001, 0.001, 0.004), smoothstep(-0.5, 0.8, y));

          // Orion-like nebula — reddish-pink cloud region
          float neb1 = fbm(dir * 3.0 + vec3(1.5, 0.0, 2.3));
          neb1 = smoothstep(0.4, 0.7, neb1);
          float neb1Mask = smoothstep(0.3, 0.0, length(dir - vec3(0.5, 0.2, -0.8)));
          col += vec3(0.12, 0.02, 0.04) * neb1 * neb1Mask;

          // Carina-like nebula — blue-teal cloud
          float neb2 = fbm(dir * 4.0 + vec3(-2.0, 1.0, 0.5));
          neb2 = smoothstep(0.45, 0.7, neb2);
          float neb2Mask = smoothstep(0.35, 0.0, length(dir - vec3(-0.6, -0.3, 0.7)));
          col += vec3(0.02, 0.06, 0.1) * neb2 * neb2Mask;

          // Pillars of creation — golden dust cloud
          float neb3 = fbm(dir * 5.0 + vec3(0.0, 3.0, -1.0));
          neb3 = smoothstep(0.5, 0.75, neb3);
          float neb3Mask = smoothstep(0.25, 0.0, length(dir - vec3(-0.3, 0.7, 0.4)));
          col += vec3(0.08, 0.05, 0.01) * neb3 * neb3Mask;

          // Milky Way band — bright horizontal band
          float milkyWay = exp(-8.0 * y * y);
          float mwNoise = fbm(dir * 6.0);
          col += vec3(0.03, 0.025, 0.04) * milkyWay * mwNoise;

          // Nearby bright stars (fixed positions, glowing)
          vec3 stars[5];
          vec3 starColors[5];
          stars[0] = normalize(vec3(0.8, 0.1, -0.5));  // Sirius — blue-white
          stars[1] = normalize(vec3(-0.3, 0.6, 0.7));  // Betelgeuse — orange
          stars[2] = normalize(vec3(0.1, -0.8, 0.5));  // Rigel — blue
          stars[3] = normalize(vec3(-0.7, 0.2, -0.6)); // Aldebaran — orange-red
          stars[4] = normalize(vec3(0.4, 0.7, 0.5));   // Vega — white
          starColors[0] = vec3(0.7, 0.8, 1.0);
          starColors[1] = vec3(1.0, 0.5, 0.2);
          starColors[2] = vec3(0.5, 0.6, 1.0);
          starColors[3] = vec3(1.0, 0.4, 0.15);
          starColors[4] = vec3(0.9, 0.92, 1.0);

          for (int i = 0; i < 5; i++) {
            float d = length(dir - stars[i]);
            float glow = exp(-500.0 * d * d) * 0.5;   // tight small core
            float halo = exp(-40.0 * d * d) * 0.08;   // subtle halo
            col += starColors[i] * (glow + halo);
          }

          gl_FragColor = vec4(col, 1.0);
        }
      `,
    });
    const skySphere = new THREE.Mesh(skyGeo, skyMat);
    skySphere.name = 'sky-nebula';
    skySphere.renderOrder = -2;
    // Added to starField group below (follows camera — no parallax)

    // Layer 1: Bright prominent stars (few, large, colorful)
    const brightCount = 200;
    const brightPos = new Float32Array(brightCount * 3);
    const brightCol = new Float32Array(brightCount * 3);
    const brightSizes = new Float32Array(brightCount);
    for (let i = 0; i < brightCount; i++) {
      const r = 4500;
      const theta = Math.random() * Math.PI * 2;
      const phi = Math.acos(2 * Math.random() - 1);
      brightPos[i*3] = r * Math.sin(phi) * Math.cos(theta);
      brightPos[i*3+1] = r * Math.sin(phi) * Math.sin(theta);
      brightPos[i*3+2] = r * Math.cos(phi);
      // Varied star colors: white, blue-white, gold, orange
      const hue = Math.random();
      if (hue < 0.4) { brightCol[i*3] = 0.9; brightCol[i*3+1] = 0.92; brightCol[i*3+2] = 1.0; }       // blue-white
      else if (hue < 0.6) { brightCol[i*3] = 1.0; brightCol[i*3+1] = 0.95; brightCol[i*3+2] = 0.8; }   // warm white
      else if (hue < 0.8) { brightCol[i*3] = 1.0; brightCol[i*3+1] = 0.85; brightCol[i*3+2] = 0.4; }   // gold
      else { brightCol[i*3] = 1.0; brightCol[i*3+1] = 0.6; brightCol[i*3+2] = 0.3; }                    // orange
      brightSizes[i] = 1.5 + Math.random() * 3.0; // varied sizes
    }
    const brightGeo = new THREE.BufferGeometry();
    brightGeo.setAttribute('position', new THREE.BufferAttribute(brightPos, 3));
    brightGeo.setAttribute('color', new THREE.BufferAttribute(brightCol, 3));
    brightGeo.setAttribute('size', new THREE.BufferAttribute(brightSizes, 1));
    const brightMat = new THREE.PointsMaterial({
      size: 2.5, vertexColors: true, transparent: true, opacity: 0.9,
      sizeAttenuation: false, blending: THREE.AdditiveBlending, depthWrite: false,
    });
    const brightStars = new THREE.Points(brightGeo, brightMat);
    brightStars.name = 'stars-bright';

    // Layer 2: Dim background stars (many, tiny)
    const dimCount = 1200;
    const dimPos = new Float32Array(dimCount * 3);
    const dimCol = new Float32Array(dimCount * 3);
    for (let i = 0; i < dimCount; i++) {
      const r = 4500;
      const theta = Math.random() * Math.PI * 2;
      const phi = Math.acos(2 * Math.random() - 1);
      dimPos[i*3] = r * Math.sin(phi) * Math.cos(theta);
      dimPos[i*3+1] = r * Math.sin(phi) * Math.sin(theta);
      dimPos[i*3+2] = r * Math.cos(phi);
      const b = 0.3 + Math.random() * 0.4;
      dimCol[i*3] = b; dimCol[i*3+1] = b * 0.95; dimCol[i*3+2] = b * 1.05;
    }
    const dimGeo = new THREE.BufferGeometry();
    dimGeo.setAttribute('position', new THREE.BufferAttribute(dimPos, 3));
    dimGeo.setAttribute('color', new THREE.BufferAttribute(dimCol, 3));
    const dimMat = new THREE.PointsMaterial({
      size: 1.0, vertexColors: true, transparent: true, opacity: 0.5,
      sizeAttenuation: false, depthWrite: false,
    });
    const dimStars = new THREE.Points(dimGeo, dimMat);
    dimStars.name = 'stars-dim';

    // Milky Way band — large sphere behind everything, additive blended
    const milkyWayMesh = createMilkyWay(8000);
    const milkyWayPref = (() => { try { return localStorage.getItem('prime-radiant-milky-way'); } catch { return null; } })();
    milkyWayMesh.visible = milkyWayPref !== 'false'; // default ON

    // Group all sky layers — follows camera together
    const starField = new THREE.Group();
    starField.name = 'starfield';
    starField.add(milkyWayMesh); // behind everything (renderOrder -3)
    starField.add(skySphere);
    starField.add(brightStars);
    starField.add(dimStars);
    starField.renderOrder = -1;
    fg.scene().add(starField);

    // M key toggles Milky Way visibility
    milkyWayToggleHandler = (e: KeyboardEvent) => {
      if (e.key === 'm' || e.key === 'M') {
        const tag = (e.target as HTMLElement)?.tagName;
        if (tag === 'INPUT' || tag === 'TEXTAREA') return;
        milkyWayMesh.visible = !milkyWayMesh.visible;
        try { localStorage.setItem('prime-radiant-milky-way', milkyWayMesh.visible ? 'true' : 'false'); } catch { /* ignore */ }
      }
    };
    window.addEventListener('keydown', milkyWayToggleHandler);

    // ─── ORBITAL RINGS on constitution/department nodes ───
    // Rings colored by health state — problems visible even at distance
    const ringNodes = forceData.nodes.filter((n) => n.type === 'constitution' || n.type === 'department');
    const ringMeshes: THREE.Mesh[] = [];
    for (const rn of ringNodes) {
      const rnProminence = HEALTH_PROMINENCE[rn.healthStatus ?? 'unknown'];
      const ringRadius = Math.pow(TYPE_SIZE[rn.type] ?? 5, 0.5) * 1.2 * rnProminence.sizeMult;
      const torusGeo = new THREE.TorusGeometry(ringRadius, ringRadius * 0.03, 8, 64);
      const torusMat = new THREE.MeshBasicMaterial({
        color: new THREE.Color(rn.color),
        transparent: true,
        opacity: 0.4,
        blending: THREE.AdditiveBlending,
        depthWrite: false,
      });
      const ring = new THREE.Mesh(torusGeo, torusMat);
      ring.userData = { trackNodeId: rn.id, ringTilt: Math.random() * Math.PI * 0.5 };
      fg.scene().add(ring);
      ringMeshes.push(ring);
    }

    // ─── GOD RAY LIGHT SHAFTS from center ───
    // Volumetric light cone pointing outward from the graph center
    const godRayGeo = new THREE.ConeGeometry(80, 300, 32, 1, true);
    const godRayMat = new THREE.ShaderMaterial({
      transparent: true,
      side: THREE.DoubleSide,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
      uniforms: { uTime: { value: 0 }, uColor: { value: new THREE.Color('#FF6B35') } },
      vertexShader: `varying vec2 vUv; void main() { vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }`,
      fragmentShader: `
        uniform float uTime;
        uniform vec3 uColor;
        varying vec2 vUv;
        void main() {
          float fade = smoothstep(0.0, 0.5, vUv.y) * smoothstep(1.0, 0.5, vUv.y);
          float rays = sin(vUv.x * 40.0 + uTime * 0.5) * 0.5 + 0.5;
          rays *= sin(vUv.x * 17.0 - uTime * 0.3) * 0.5 + 0.5;
          float alpha = fade * rays * 0.03;
          gl_FragColor = vec4(uColor, alpha);
        }
      `,
    });
    const godRay = new THREE.Mesh(godRayGeo, godRayMat);
    godRay.rotation.x = Math.PI; // point upward
    fg.scene().add(godRay);

    // Second god ray at different angle
    const godRay2 = godRay.clone();
    (godRay2.material as THREE.ShaderMaterial).uniforms = {
      uTime: { value: 0 },
      uColor: { value: new THREE.Color('#00CED1') },
    };
    godRay2.rotation.set(Math.PI * 0.7, 0, Math.PI * 0.3);
    fg.scene().add(godRay2);

    // Effects are animated in the main onEngineTick (merged below)

    // ─── DEMERZEL HOLOGRAPHIC FACE ───
    // Floating wireframe head — Demerzel's presence in the Prime Radiant
    const demerzelFace = createDemerzelFace(0.5); // TODO: replace with rigged Blender GLTF model
    demerzelFace.position.set(0, 25, 0); // floating above center, repositioned per tick
    fg.scene().add(demerzelFace);

    // ─── TARS ROBOT ───
    // Articulated monolith — patrols the graph, cycles through modes
    const tarsRobot = createTarsRobot(0.4);
    tarsRobot.position.set(20, 0, 20); // offset from center
    fg.scene().add(tarsRobot);

    // ─── TRANTOR GLOBE ───
    // Holographic ecumenopolis — the capital world, top-right HUD position
    // Trantor removed — replaced by Earth + nebula clouds in skybox

    // ─── SOLAR SYSTEM — Sun + 8 planets + moons ───
    const solarSystem = createSolarSystem(0.03);
    fg.scene().add(solarSystem);

    // Start live weather cloud updates on Earth (requires VITE_OWM_API_KEY)
    const stopCloudUpdates = startLiveCloudUpdates(solarSystem);

    // ─── Solar system planet hover detection (raycasting) ───
    const solarRaycaster = new THREE.Raycaster();
    solarRaycaster.params.Line = { threshold: 0 }; // ignore lines
    const solarMouse = new THREE.Vector2();
    let currentHoveredPlanet: string | null = null;

    const onSolarMouseMove = (event: MouseEvent) => {
      const canvas = containerRef.current?.querySelector('canvas');
      if (!canvas) return;
      const rect = canvas.getBoundingClientRect();
      solarMouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
      solarMouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

      const cam = fg.camera() as THREE.PerspectiveCamera;
      solarRaycaster.setFromCamera(solarMouse, cam);

      const planetMeshes = getPlanetMeshes(solarSystem);
      const hits = solarRaycaster.intersectObjects(planetMeshes, false);

      const hitName = hits.length > 0 ? (hits[0].object.name || null) : null;
      if (hitName !== currentHoveredPlanet) {
        currentHoveredPlanet = hitName;
        showPlanetLabel(solarSystem, hitName);
        if (canvas) {
          canvas.style.cursor = hitName ? 'pointer' : '';
        }
      }
    };

    const onSolarClick = () => {
      // If hovering a planet, start tracking it
      if (currentHoveredPlanet) {
        solarFollowCameraRef.current = false;
        trackedPlanetRef.current = currentHoveredPlanet;
        setTrackedPlanetName(currentHoveredPlanet.charAt(0).toUpperCase() + currentHoveredPlanet.slice(1));
      }
    };

    solarMouseMoveHandler = onSolarMouseMove;
    solarClickHandler = onSolarClick;
    container.addEventListener('mousemove', onSolarMouseMove);
    container.addEventListener('click', onSolarClick);

    // ─── JARVIS SPACE STATION — modular station with docking animation ───
    const spaceStation = createSpaceStation(0.6);
    fg.scene().add(spaceStation);

    // ─── Auto-select the most connected (central) node on load ───
    // Find the node with the most edges — that's the true hub
    const edgeCounts = new Map<string, number>();
    for (const e of graph.edges) {
      edgeCounts.set(e.source, (edgeCounts.get(e.source) ?? 0) + 1);
      edgeCounts.set(e.target, (edgeCounts.get(e.target) ?? 0) + 1);
    }
    const centralNode = graph.nodes.reduce((best, n) =>
      (edgeCounts.get(n.id) ?? 0) > (edgeCounts.get(best.id) ?? 0) ? n : best,
      graph.nodes[0],
    );
    if (centralNode) {
      setSelectedNode(centralNode);
      onNodeSelect?.(centralNode);
      // Zoom to it after force layout settles (Phase 1.4: cancellable)
      autoZoomTimeout = setTimeout(() => {
        if (userInteracted) return; // User already clicked something — don't override
        const fNode = nodeMap.get(centralNode.id) as (GraphNode & { x?: number; y?: number; z?: number }) | undefined;
        if (fNode?.x !== undefined) {
          fg.cameraPosition(
            { x: fNode.x, y: (fNode.y ?? 0) + 30, z: (fNode.z ?? 0) + 80 },
            { x: fNode.x, y: fNode.y ?? 0, z: fNode.z ?? 0 },
            1500,
          );
        }
      }, 2000);
    }

    graphRef.current = fg;

    // ─── Fix camera near plane for solar system zoom ───
    const fgCam = fg.camera() as THREE.PerspectiveCamera;
    fgCam.near = 0.001;
    fgCam.far = 5000;
    fgCam.updateProjectionMatrix();

    // ─── Restore camera state from localStorage ───
    try {
      const saved = localStorage.getItem('prime-radiant-camera');
      if (saved) {
        const cam = JSON.parse(saved) as { px: number; py: number; pz: number; lx: number; ly: number; lz: number };
        fg.cameraPosition({ x: cam.px, y: cam.py, z: cam.pz }, { x: cam.lx, y: cam.ly, z: cam.lz }, 0);
      }
    } catch { /* ignore corrupt/missing state */ }

    // ─── Live data polling — update nodes in-place without graph rebuild ───
    let pollingHandle: LivePollingHandle | undefined;
    if (liveDataUrl || liveHubUrl) {
      pollingHandle = startLivePolling({
        url: liveDataUrl ?? '',
        hubUrl: liveHubUrl,
        intervalMs: pollIntervalMs,
        onScreenshotRequest: (reason: string) => {
          console.log('[PrimeRadiant] Screenshot requested:', reason);
          const canvas = containerRef.current?.querySelector('canvas');
          if (canvas) {
            const dataUrl = canvas.toDataURL('image/png');
            pollingHandle?.submitScreenshot(dataUrl, 'image/png').catch(err =>
              console.warn('[PrimeRadiant] Screenshot submit failed:', err),
            );
          } else {
            console.warn('[PrimeRadiant] No canvas found for screenshot');
          }
        },
        onUpdate: (freshGraph) => {
          const currentNodes = (fg.graphData() as { nodes: GraphNode[] }).nodes;
          const { updated, changed } = updateNodeHealth(
            currentNodes as unknown as GovernanceNode[],
            freshGraph.nodes,
          );
          if (changed) {
            // Update Three.js objects for changed nodes
            for (const nodeId of updated) {
              const gn = currentNodes.find(n => n.id === nodeId);
              if (!gn) continue;
              const threeObj = (gn as GraphNode & { __threeObj?: THREE.Object3D }).__threeObj;
              if (threeObj) {
                threeObj.userData.healthStatus = (gn as unknown as GovernanceNode).healthStatus;
              }
              // Update node map
              nodeMap.set(nodeId, gn);
              // Recompute importance for this node
              const normEdge = (edgeCountMap.get(nodeId) ?? 0) / maxEdges;
              const health = (gn as unknown as GovernanceNode).health;
              const normErgol = (health?.ergolCount ?? 0) / maxErgol;
              const resilience = health?.resilienceScore ?? 0.5;
              const staleness = health?.staleness ?? 0;
              importanceMap.set(nodeId, 0.4 * normEdge + 0.3 * normErgol + 0.2 * resilience + 0.1 * (1 - staleness));
            }
            // Update graph data reference to keep React state in sync
            setGraphData(freshGraph);
            setGraphIndex(buildGraphIndex(freshGraph));
          }
        },
        onAlgedonicSignal: handleAlgedonicSignal,
        onError: (err) => console.warn('[PrimeRadiant] Live poll error:', err.message),
      });
    }

    // Expose cleanup handles to outer scope
    autoZoomTimeoutOuter = autoZoomTimeout;
    pollingHandleOuter = pollingHandle;
    cloudCleanupOuter = stopCloudUpdates;

    // ─── DEMERZEL VISUAL CRITIC — autonomous self-healing loop ───
    // Captures canvas → Claude vision → IXQL fixes → algedonic signals
    const criticCanvas = containerRef.current?.querySelector('canvas');
    if (criticCanvas) {
      criticCleanupOuter = startVisualCriticLoop(criticCanvas as HTMLCanvasElement, {
        enabled: true,           // Demerzel drives
        intervalMs: 90_000,      // analyze every 90 seconds
        autoFix: true,           // auto-execute IXQL commands
        onPhaseChange: (phase: CriticPhase) => {
          setCriticState(prev => ({ ...prev, phase }));
        },
        onResult: (result) => {
          // Update overlay state
          setCriticState(prev => ({
            ...prev,
            result,
            history: [...prev.history.slice(-19), result],
            lastAnalysis: new Date(),
          }));
          // Log to console
          const bar = '█'.repeat(result.quality) + '░'.repeat(10 - result.quality);
          console.info(`[Demerzel] Visual Quality: [${bar}] ${result.quality}/10`);
          if (result.signal_type === 'pain') {
            console.warn(`[Demerzel] Pain signal: ${result.signal_description}`);
          }
        },
        onIxqlCommand: handleIxqlCommand,
      });
    }

    }; // end initScene

    // Kick off async init
    initGraph();

    return () => {
      disposed = true;
      if (milkyWayToggleHandler) window.removeEventListener('keydown', milkyWayToggleHandler);
      if (autoZoomTimeoutOuter) clearTimeout(autoZoomTimeoutOuter);
      pollingHandleOuter?.stop();
      cloudCleanupOuter?.();
      criticCleanupOuter?.();
      if (solarMouseMoveHandler) {
        container.removeEventListener('mousemove', solarMouseMoveHandler);
      }
      if (solarClickHandler) {
        container.removeEventListener('click', solarClickHandler);
      }
      if (graphRef.current) {
        (graphRef.current as ReturnType<typeof ForceGraph3D> & { _destructor: () => void })._destructor();
        graphRef.current = null;
      }
    };
  }, [data, liveDataUrl]); // eslint-disable-line react-hooks/exhaustive-deps

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

  // ─── Backend connectivity check ───
  useEffect(() => {
    // Use VITE env var, or same origin as the page (works for deployed demo), or localhost fallback
    const envBase = typeof import.meta !== 'undefined'
      ? (import.meta as { env?: Record<string, string> }).env?.VITE_API_BASE_URL
      : undefined;
    const baseUrl = envBase || (typeof window !== 'undefined' ? window.location.origin : 'http://localhost:5232');

    const checkBackend = async () => {
      try {
        const url = `${baseUrl}/api/chatbot/status`;
        console.log('[PrimeRadiant] Checking backend at:', url);
        const res = await fetch(url, { signal: AbortSignal.timeout(10000) });
        console.log('[PrimeRadiant] Backend status:', res.status);
        setBackendStatus(res.ok ? 'connected' : 'disconnected');
      } catch (err) {
        console.warn('[PrimeRadiant] Backend check failed:', err);
        setBackendStatus('disconnected');
      }
    };

    void checkBackend();
    const interval = setInterval(() => void checkBackend(), 30000);
    return () => clearInterval(interval);
  }, []);

  // ─── Escape key to close active panel + click-outside to close health tooltip ───
  useEffect(() => {
    const keyHandler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        setActiveHealthTip(null);
        setActivePanel(prev => {
          if (prev) return null;
          return prev;
        });
        setSelectedNode(null);
      }
    };
    const clickHandler = (e: MouseEvent) => {
      // Don't close if click is inside the health bar
      const healthBar = document.querySelector('.prime-radiant__health');
      if (healthBar && healthBar.contains(e.target as Node)) return;
      setActiveHealthTip(null);
    };
    window.addEventListener('keydown', keyHandler);
    window.addEventListener('click', clickHandler);
    return () => {
      window.removeEventListener('keydown', keyHandler);
      window.removeEventListener('click', clickHandler);
    };
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
    setActivePanel(null);
    onNodeSelect?.(null);
  }, [onNodeSelect]);

  const handlePanelToggle = useCallback((panelId: PanelId) => {
    setActivePanel(prev => prev === panelId ? null : panelId);
  }, []);

  const healthStatus = graphData
    ? getHealthStatus(graphData.globalHealth.resilienceScore)
    : 'healthy';

  return (
    <div className={`prime-radiant ${className}`} style={{ width, height }}>
      {/* Canvas area — fills remaining space */}
      <div className="prime-radiant__canvas-area">
        <div ref={containerRef} style={{ width: '100%', flex: 1, minHeight: 0 }} />

      {/* Backend connection status badge with capabilities popover */}
      <div className={`prime-radiant__backend-status prime-radiant__backend-status--${backendStatus}`}>
        <span className="prime-radiant__backend-dot" />
        <span>{backendStatus === 'connected' ? 'API Connected' : backendStatus === 'checking' ? 'Checking...' : 'API Offline'}</span>
        <div className="prime-radiant__api-popover">
          {backendStatus === 'connected' ? (
            <>
              <div className="prime-radiant__api-popover-title">Backend Capabilities</div>
              <div className="prime-radiant__api-popover-item prime-radiant__api-popover-item--ok">Chatbot (Ollama RAG)</div>
              <div className="prime-radiant__api-popover-item prime-radiant__api-popover-item--ok">Governance Graph + SignalR</div>
              <div className="prime-radiant__api-popover-item prime-radiant__api-popover-item--ok">Algedonic Signals</div>
              <div className="prime-radiant__api-popover-item prime-radiant__api-popover-item--ok">Voice TTS (Voxtral)</div>
              <div className="prime-radiant__api-popover-item prime-radiant__api-popover-item--ok">File Content Viewer</div>
              <div className="prime-radiant__api-popover-item prime-radiant__api-popover-item--ok">YouTube → Tab Pipeline</div>
              <div className="prime-radiant__api-popover-item prime-radiant__api-popover-item--ok">Vector Search (OPTIC-K)</div>
              <div className="prime-radiant__api-popover-item prime-radiant__api-popover-item--ok">GraphQL (HotChocolate)</div>
            </>
          ) : backendStatus === 'disconnected' ? (
            <>
              <div className="prime-radiant__api-popover-title">Backend Offline</div>
              <div className="prime-radiant__api-popover-item prime-radiant__api-popover-item--off">Chatbot unavailable</div>
              <div className="prime-radiant__api-popover-item prime-radiant__api-popover-item--off">Live governance updates paused</div>
              <div className="prime-radiant__api-popover-item prime-radiant__api-popover-item--ok">Graph (cached data)</div>
              <div className="prime-radiant__api-popover-item prime-radiant__api-popover-item--ok">3D visualization</div>
              <div className="prime-radiant__api-popover-item prime-radiant__api-popover-item--ok">Planet navigation</div>
            </>
          ) : (
            <div className="prime-radiant__api-popover-title">Checking connection...</div>
          )}
        </div>
      </div>

      {/* HUD overlays on the canvas — floating, center-top */}
      {graphData && (
        <div className="prime-radiant__health">
          {/* API status */}
          <span
            className={`prime-radiant__health-metric ${activeHealthTip === 'api' ? 'prime-radiant__health-metric--active' : ''}`}
            onClick={(e) => { e.stopPropagation(); setActiveHealthTip(prev => prev === 'api' ? null : 'api'); }}
          >
            <span className={`prime-radiant__health-dot prime-radiant__health-dot--${backendStatus === 'connected' ? 'healthy' : 'freeze'}`} />
            <span>API</span>
            {activeHealthTip === 'api' && (
              <div className="prime-radiant__health-tooltip" onClick={e => e.stopPropagation()}>
                <div className="prime-radiant__health-tooltip-title">Backend API Status</div>
                <div className="prime-radiant__health-tooltip-desc">
                  {backendStatus === 'connected'
                    ? 'Connected to ga-server. Live governance data, SignalR hub, and belief state updates are active.'
                    : 'Backend API is unreachable. Displaying cached/sample data. Some features (live beliefs, algedonic signals) are unavailable.'}
                </div>
                <div className="prime-radiant__health-tooltip-link" onClick={() => { setActiveHealthTip(null); handlePanelToggle('cicd'); }}>
                  View CI/CD status →
                </div>
              </div>
            )}
          </span>

          <span style={{ color: '#30363d' }}>|</span>

          {/* Resilience */}
          <span
            className={`prime-radiant__health-metric ${activeHealthTip === 'resilience' ? 'prime-radiant__health-metric--active' : ''}`}
            onClick={(e) => { e.stopPropagation(); setActiveHealthTip(prev => prev === 'resilience' ? null : 'resilience'); }}
          >
            <span>
              R:{' '}
              <span style={{ color: HEALTH_COLORS[healthStatus], fontWeight: 600 }}>
                {(graphData.globalHealth.resilienceScore * 100).toFixed(0)}%
              </span>
            </span>
            {activeHealthTip === 'resilience' && (
              <div className="prime-radiant__health-tooltip" onClick={e => e.stopPropagation()}>
                <div className="prime-radiant__health-tooltip-title">Resilience Score</div>
                <div className="prime-radiant__health-tooltip-desc">
                  Aggregate health of the governance ecosystem. Computed from node staleness, test coverage, policy compliance, and Markov state predictions across all {graphData.nodes.length} artifacts.
                </div>
                <div className="prime-radiant__health-tooltip-thresholds">
                  <span style={{ color: '#33CC66' }}>≥90% Healthy</span>
                  <span style={{ color: '#FFB300' }}>70-89% Watch</span>
                  <span style={{ color: '#FF4444' }}>&lt;70% Freeze</span>
                </div>
                <div className="prime-radiant__health-tooltip-link" onClick={() => { setActiveHealthTip(null); handlePanelToggle('seldon'); }}>
                  Open Seldon Dashboard →
                </div>
              </div>
            )}
          </span>

          <span style={{ color: '#30363d' }}>|</span>

          {/* ERGOL */}
          <span
            className={`prime-radiant__health-metric ${activeHealthTip === 'ergol' ? 'prime-radiant__health-metric--active' : ''}`}
            onClick={(e) => { e.stopPropagation(); setActiveHealthTip(prev => prev === 'ergol' ? null : 'ergol'); }}
          >
            <span>
              ERGOL: <span style={{ color: '#FFD700' }}>{graphData.globalHealth.ergolCount}</span>
            </span>
            {activeHealthTip === 'ergol' && (
              <div className="prime-radiant__health-tooltip" onClick={e => e.stopPropagation()}>
                <div className="prime-radiant__health-tooltip-title">ERGOL — Executed References (Governance Operational Links)</div>
                <div className="prime-radiant__health-tooltip-desc">
                  Live, actively consumed governance bindings. Each ERGOL represents a policy, persona, or constitution that is actively referenced by a consumer repo (ix, tars, ga). Higher is better — it means governance artifacts are being used.
                </div>
                <div className="prime-radiant__health-tooltip-value" style={{ color: '#FFD700' }}>
                  {graphData.globalHealth.ergolCount} active bindings across {graphData.edges.length} edges
                </div>
              </div>
            )}
          </span>

          <span style={{ color: '#30363d' }}>|</span>

          {/* LOLLI */}
          <span
            className={`prime-radiant__health-metric ${activeHealthTip === 'lolli' ? 'prime-radiant__health-metric--active' : ''}`}
            onClick={(e) => { e.stopPropagation(); setActiveHealthTip(prev => prev === 'lolli' ? null : 'lolli'); }}
          >
            <span>
              LOLLI: <span style={{ color: graphData.globalHealth.lolliCount > 0 ? '#FF4444' : '#8b949e' }}>
                {graphData.globalHealth.lolliCount}
              </span>
            </span>
            {activeHealthTip === 'lolli' && (
              <div className="prime-radiant__health-tooltip" onClick={e => e.stopPropagation()}>
                <div className="prime-radiant__health-tooltip-title">LOLLI — Lapsed/Orphan Links (Inflation Indicator)</div>
                <div className="prime-radiant__health-tooltip-desc">
                  Dead or orphaned governance references — artifacts that exist but have no active consumer. LOLLI inflation means we're creating governance artifacts nobody uses. Target: 0. Any LOLLI should be either adopted or deleted.
                </div>
                {graphData.globalHealth.lolliCount > 0 && (
                  <div className="prime-radiant__health-tooltip-value" style={{ color: '#FF4444' }}>
                    {graphData.globalHealth.lolliCount} orphan reference{graphData.globalHealth.lolliCount > 1 ? 's' : ''} — consider remediation
                  </div>
                )}
                <div className="prime-radiant__health-tooltip-link" onClick={() => { setActiveHealthTip(null); handlePanelToggle('backlog'); }}>
                  View backlog for cleanup →
                </div>
              </div>
            )}
          </span>
        </div>
      )}

      <GalacticClock />
      <ChatWidget selectedNode={selectedNode} />
      <TutorialOverlay />
      <IxqlCommandInput onCommand={handleIxqlCommand} />

      {/* Demerzel visual critic overlay — shows self-improvement process */}
      <DemerzelCriticOverlay state={criticState} />

      {/* Planet quick-nav bar — bottom center */}
      <div className="prime-radiant__planet-bar">
        {[
          { icon: '🤖', name: 'Demerzel', color: '#FFD700', target: 'demerzel-head' },
          { icon: '☀', name: 'Sun', color: '#FFD700', target: 'sun' },
          { icon: '⚫', name: 'Mercury', color: '#9e9e9e', target: 'mercury' },
          { icon: '🟡', name: 'Venus', color: '#e3d500', target: 'venus' },
          { icon: '🌍', name: 'Earth', color: '#4d88ff', target: 'earth' },
          { icon: '🔴', name: 'Mars', color: '#ff4422', target: 'mars' },
          { icon: '🟠', name: 'Jupiter', color: '#ffaa77', target: 'jupiter' },
          { icon: '💛', name: 'Saturn', color: '#ffeecc', target: 'saturn' },
          { icon: '🔵', name: 'Uranus', color: '#88ccdd', target: 'uranus' },
          { icon: '🔵', name: 'Neptune', color: '#4444cc', target: 'neptune' },
        ].map((p) => (
          <button
            key={p.name}
            className="prime-radiant__planet-btn"
            title={p.name}
            onClick={() => {
              const fg = graphRef.current;
              if (!fg) return;

              if (p.target === 'demerzel-head') {
                const cam = fg.camera() as THREE.PerspectiveCamera;
                const faceOffset = new THREE.Vector3(-50, -8, -40);
                faceOffset.applyQuaternion(cam.quaternion);
                const facePos = cam.position.clone().add(faceOffset);
                fg.cameraPosition(
                  { x: facePos.x + 5, y: facePos.y + 2, z: facePos.z + 12 },
                  { x: facePos.x, y: facePos.y, z: facePos.z },
                  1200,
                );
                // Resume solar system follow when navigating elsewhere
                solarFollowCameraRef.current = true;
                trackedPlanetRef.current = null;
                setTrackedPlanetName(null);
                return;
              }

              // Stop solar system from following the camera so we can zoom into it
              solarFollowCameraRef.current = false;
              // Start tracking this planet
              trackedPlanetRef.current = p.target;
              setTrackedPlanetName(p.name);

              const solarSystemGroup = fg.scene().getObjectByName('sun')?.parent;
              if (!solarSystemGroup) return;

              // Get the solar system's current world position (where it's pinned now)
              const solarCenter = new THREE.Vector3();
              solarSystemGroup.getWorldPosition(solarCenter);

              if (p.target === 'sun') {
                // Zoom to solar system center — close enough to see the Sun
                fg.cameraPosition(
                  { x: solarCenter.x, y: solarCenter.y + 0.8, z: solarCenter.z + 2.5 },
                  { x: solarCenter.x, y: solarCenter.y, z: solarCenter.z },
                  1200,
                );
              } else {
                // Find the planet mesh by name within the solar system
                const obj = solarSystemGroup.getObjectByName(p.target);
                if (obj) {
                  const wp = new THREE.Vector3();
                  obj.getWorldPosition(wp);
                  // Solar system is at 0.03 scale — planets are tiny in world space
                  // Get the planet's world-space radius to compute proper zoom distance
                  const worldScale = obj.getWorldScale(new THREE.Vector3()).x;
                  // Planet radius in local coords varies (earth=0.35, jupiter=0.7, etc.)
                  // Use bounding sphere if available, otherwise estimate from scale
                  let planetWorldRadius = worldScale * 0.35; // fallback estimate
                  if (obj.children.length > 0) {
                    const mesh = obj.children[0] as THREE.Mesh;
                    if (mesh.geometry) {
                      mesh.geometry.computeBoundingSphere();
                      planetWorldRadius = (mesh.geometry.boundingSphere?.radius ?? 0.35) * worldScale;
                    }
                  }
                  // Camera distance: ~2.5x radius fills ~75% of screen
                  const camDist = Math.max(planetWorldRadius * 2.5, 0.02);
                  // Approach from slightly above and to the side for a nice angle
                  fg.cameraPosition(
                    { x: wp.x + camDist * 0.3, y: wp.y + camDist * 0.4, z: wp.z + camDist * 0.85 },
                    { x: wp.x, y: wp.y, z: wp.z },
                    1500,
                  );
                } else {
                  // Fallback: zoom to solar system center
                  fg.cameraPosition(
                    { x: solarCenter.x, y: solarCenter.y + 0.8, z: solarCenter.z + 2.5 },
                    { x: solarCenter.x, y: solarCenter.y, z: solarCenter.z },
                    1200,
                  );
                }
              }
            }}
          >
            <span style={{ fontSize: '14px' }}>{p.icon}</span>
            <span className="prime-radiant__planet-label">{p.name}</span>
          </button>
        ))}
        {trackedPlanetName && (
          <div
            className="prime-radiant__tracking-indicator"
            style={{
              position: 'absolute',
              bottom: '48px',
              left: '50%',
              transform: 'translateX(-50%)',
              background: 'rgba(0, 20, 40, 0.85)',
              border: '1px solid rgba(0, 200, 255, 0.4)',
              borderRadius: '4px',
              padding: '2px 10px',
              fontSize: '11px',
              color: '#00ccff',
              fontFamily: 'monospace',
              pointerEvents: 'auto',
              cursor: 'pointer',
              whiteSpace: 'nowrap',
            }}
            title="Click to stop tracking"
            onClick={() => {
              trackedPlanetRef.current = null;
              setTrackedPlanetName(null);
              solarFollowCameraRef.current = true;
            }}
          >
            Tracking: {trackedPlanetName} <span style={{ opacity: 0.5 }}>[x]</span>
          </div>
        )}
      </div>

      {/* Bottom drawer — icicle navigator + file viewer */}
      <IcicleDrawer graphData={graphData} />

      </div>{/* end canvas-area */}

      {/* Icon rail — right edge (desktop/tablet) or bottom tab bar (phone) */}
      <IconRail
        activePanel={activePanel}
        onPanelToggle={handlePanelToggle}
        panelStatuses={{
          cicd: 'error',
          algedonic: 'warn',
          activity: 'ok',
        }}
      />

      {/* Click-outside-to-close backdrop */}
      <div
        className={`prime-radiant__side-panel-backdrop ${activePanel ? 'prime-radiant__side-panel-backdrop--active' : ''}`}
        onClick={() => { if (activePanel) setActivePanel(null); }}
      />

      {/* Side panel area — one panel at a time */}
      <div className={`prime-radiant__side-panel ${activePanel ? 'prime-radiant__side-panel--open' : ''}`}>
        {/* Close button for mobile overlay */}
        <button
          className="prime-radiant__side-panel-close"
          onClick={() => setActivePanel(null)}
          aria-label="Close panel"
        >
          <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <line x1="18" y1="6" x2="6" y2="18" />
            <line x1="6" y1="6" x2="18" y2="18" />
          </svg>
        </button>

        {activePanel === 'detail' && showDetailPanel && (
          <DetailPanel
            node={selectedNode}
            graphIndex={graphIndex}
            onClose={handleCloseDetail}
            onNavigate={handleNavigate}
          />
        )}
        {activePanel === 'activity' && <ActivityPanel />}
        {activePanel === 'backlog' && <BacklogPanel />}
        {activePanel === 'agent' && <AgentPanel />}
        {activePanel === 'seldon' && graphData && (
          <SeldonDashboard
            open={true}
            onClose={() => setActivePanel(null)}
            graph={graphData}
            selectedNode={selectedNode}
          />
        )}
        {activePanel === 'llm' && <LLMStatus />}
        {activePanel === 'algedonic' && <AlgedonicPanel signals={algedonicSignals} />}
        {activePanel === 'cicd' && <CICDPanel />}
        {activePanel === 'claude' && <ClaudeCodePanel />}
      </div>

      {/* CourseViewer renders as full-screen overlay, outside side panel */}
      {activePanel === 'university' && (
        <CourseViewer open={true} onClose={() => setActivePanel(null)} />
      )}

      {/* LiveNotebook renders as full-screen overlay, outside side panel */}
      {activePanel === 'notebook' && (
        <LiveNotebook open={true} onClose={() => setActivePanel(null)} />
      )}

    </div>
  );
};
