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
import { loadGovernanceData, loadGovernanceDataAsync, getHealthStatus, startLivePolling, updateNodeHealth, type LivePollingHandle, type ViewerInfo } from './DataLoader';
import { DetailPanel } from './DetailPanel';
import { ChatWidget } from './ChatWidget';
import { BrainstormPanel } from './BrainstormPanel';
import { PlanetNav } from './PlanetNav';
import { buildGraphIndex, type GraphIndex } from './DataLoader';
import { createDemerzelFace, updateDemerzelFace } from './DemerzelFace';
import { createRiggedDemerzelFace, updateRiggedDemerzelFace } from './DemerzelRiggedFace';
import { createTarsRobot, updateTarsRobot } from './TarsRobot';
// TrantorGlobe removed — replaced by real Earth + nebulae
import { createSolarSystem, updateSolarSystem, showPlanetLabel, loadArcGISOverlay, removeArcGISOverlay, addLocationMarker, enableEarthAutoLOD } from './SolarSystem';
import { createSpaceStation, updateSpaceStation } from './SpaceStation';
import { createMilkyWay } from './MilkyWay';
import { GalacticClock } from './GalacticClock';
import { TutorialOverlay } from './TutorialOverlay';
import { ActivityPanel } from './ActivityPanel';
import { LLMStatus } from './LLMStatus';
import { IxqlCommandInput } from './IxqlCommandInput';
import { IxqlDemoButton } from './IxqlDemoButton';
import { evaluatePredicate, type IxqlParseResult } from './IxqlControlParser';
import { recordInvocation } from './IxqlTelemetry';
import { DynamicPanel, type DynamicPanelDefinition } from './DynamicPanel';
import { IxqlGridPanel } from './IxqlGridPanel';
import { IxqlVizPanel } from './IxqlVizPanel';
import { IxqlFormPanel } from './IxqlFormPanel';
import { compileGridPanel, compileViz, compileForm, type PanelSpec, type VizSpec, type FormSpec } from './IxqlWidgetSpec';
import type { GraphContext } from './DataFetcher';
import { healthBindingEngine } from './HealthBindingEngine';
import { useHealthBindings } from './useHealthBindings';
import { reactiveEngine } from './ReactiveEngine';
import { violationMonitor } from './ViolationMonitor';
import { savedQueryStore } from './SavedQueryStore';
import { type CriticPhase } from './VisualCriticLoop';
import { startDemerzelDriver } from './DemerzelIxqlDriver';
import { type CriticState } from './DemerzelCriticOverlay';
import { BacklogPanel } from './BacklogPanel';
import { AgentPanel } from './AgentPanel';
import { SeldonDashboard } from './SeldonDashboard';
import { IconRail } from './IconRail';
import type { PanelId } from './PanelRegistry';
import { panelRegistry } from './PanelRegistry';
import { AlgedonicPanel, type AlgedonicSignal } from './AlgedonicPanel';
import { CICDPanel } from './CICDPanel';
import { ClaudeCodePanel } from './ClaudeCodePanel';
import { LibraryPanel } from './LibraryPanel';
import type { AlgedonicSignalEvent, BeliefState } from './DataLoader';
import { CourseViewer } from './CourseViewer';
import { LunarLander } from './LunarLander';
import { LiveNotebook } from './LiveNotebook';
import { TriageDropZone, pushToTriage } from './TriageDropZone';
import { IcicleDrawer } from './IcicleDrawer';
import { GodotScene, setDemerzelEmotion, setDemerzelSpeaking } from './GodotScene';
import { DemerzelFaceOverlay } from './DemerzelFaceOverlay';
import { GisPanel } from './GisPanel';
import { PresencePanel } from './PresencePanel';
import { getConnectionLog } from './ConnectionLog';
import { createGisLayer, type GisLayerManager } from './GisLayer';
import { usePrControl } from './usePrControl';
import { startSignalRGisBridge, type SignalRGisBridgeHandle } from './SignalRGisBridge';
import { useAgentPresence } from './AgentPresence';
import { TheoryTribunal } from './TheoryTribunal';
import { SeldonFacultyPanel } from './SeldonFacultyPanel';
import { CodeTribunal } from './CodeTribunal';
import { AdminInbox } from './AdminInbox';
import { ScreenshotButton } from './ScreenshotButton';
import { useDeepLink } from './DeepLink';
import { createCrystalEiffelTower, type CrystalEiffelTowerHandle } from './CrystalEiffelTower';
import { getNodeMaterialWithGlow } from './CrystalNodeMaterials';
import { createTerminalFilaments, type TerminalFilamentsHandle } from './TerminalFilaments';
import { IxqlCodeGen } from './IxqlCodeGen';
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

// ---------------------------------------------------------------------------
// Admin detection — local dev tool defaults to admin on localhost.
// For deployed instances, set localStorage 'pr-admin-token' to the known hash.
// ---------------------------------------------------------------------------
const PR_ADMIN_HASH = '8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918'; // sha256('admin')

function _checkIsAdmin(): boolean {
  if (typeof window === 'undefined') return false;
  // localhost / 127.0.0.1 → always admin (single-user dev tool)
  const host = window.location.hostname;
  if (host === 'localhost' || host === '127.0.0.1' || host === '::1') return true;
  // Deployed: check localStorage token against known hash
  try {
    const token = localStorage.getItem('pr-admin-token');
    return token === PR_ADMIN_HASH;
  } catch { return false; }
}

// Module-level mobile detection for node creation (before component mounts)
const _isMobileDevice = typeof navigator !== 'undefined' && /Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent);
const _isLowEndDevice = _isMobileDevice || (typeof navigator !== 'undefined' && navigator.hardwareConcurrency <= 4);

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
  const nodeColor = new THREE.Color(node.color);

  // ── Mobile fast path: simple emissive sphere, no shaders/dust/sprites ──
  if (_isLowEndDevice) {
    const geo = new THREE.SphereGeometry(radius * 0.5, 8, 8);
    const mat = new THREE.MeshBasicMaterial({
      color: nodeColor,
      transparent: true,
      opacity: prominence.opacity,
    });
    group.add(new THREE.Mesh(geo, mat));
    group.userData.healthStatus = hs;
    return group;
  }

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

  // 1. Crystal/metallic core — MeshPhysicalMaterial per governance type
  const geoFactory = TYPE_GEOMETRY[node.type] ?? ((r: number) => new THREE.SphereGeometry(r, 32, 32));
  const coreGeo = geoFactory(radius * 0.5);
  if (!coreGeo.attributes.normal) coreGeo.computeVertexNormals();
  const coreMat = getNodeMaterialWithGlow(node.type, prominence.glowIntensity);
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
// ---------------------------------------------------------------------------
// Markov enrichment — ensure every node has a markovPrediction for Seldon panel
// ---------------------------------------------------------------------------
function enrichNodesWithMarkov(graph: GovernanceGraph): void {
  for (const n of graph.nodes) {
    if (!n.health) {
      n.health = {
        resilienceScore: 0.7 + Math.random() * 0.25,
        staleness: Math.random() * 0.4,
        ergolCount: Math.floor(Math.random() * 10),
        lolliCount: 0,
        markovPrediction: [0.6, 0.25, 0.1, 0.05],
      };
    } else if (!n.health.markovPrediction || n.health.markovPrediction.length < 4) {
      const status = n.healthStatus ?? 'unknown';
      if (status === 'healthy' || status === 'ok') {
        n.health.markovPrediction = [0.7 + Math.random() * 0.2, 0.15, 0.1, 0.05];
      } else if (status === 'warning') {
        n.health.markovPrediction = [0.3, 0.35, 0.25, 0.1];
      } else if (status === 'error' || status === 'critical') {
        n.health.markovPrediction = [0.1, 0.15, 0.35, 0.4];
      } else {
        n.health.markovPrediction = [0.5, 0.25, 0.15, 0.1];
      }
    }
  }
}

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

  // Phase 3: Declarative health bindings → IconRail status dots
  const panelHealthStatuses = useHealthBindings();

  const [selectedNode, setSelectedNode] = useState<GovernanceNode | null>(null);
  const [graphData, setGraphData] = useState<GovernanceGraph | null>(null);
  const [activePanel, setActivePanel] = useState<PanelId | null>(null);
  const [graphIndex, setGraphIndex] = useState<GraphIndex | null>(null);
  const [algedonicSignals, setAlgedonicSignals] = useState<AlgedonicSignal[]>([]);
  const [beliefs, setBeliefs] = useState<BeliefState[]>([]);
  const [backendStatus, setBackendStatus] = useState<'checking' | 'connected' | 'disconnected'>('checking');
  // A2A agent presence — live status of ix, TARS, GA, Seldon, Demerzel
  const a2aAgents = useAgentPresence();
  // Deep linking — read/write URL params for shareable state
  const [shareToast, setShareToast] = useState(false);
  const deepLink = useDeepLink({
    activePanel,
    selectedNodeId: selectedNode?.id ?? null,
    onPanelChange: (p) => setActivePanel(p),
    onNodeSelect: (id) => {
      const node = graphData?.nodes.find(n => n.id === id);
      if (node) {
        setSelectedNode(node);
        setActivePanel('detail');
        // Zoom camera to the node
        const fg = graphRef.current;
        if (fg) {
          const fgNodes = fg.graphData().nodes as (GraphNode & { x?: number; y?: number; z?: number })[];
          const fNode = fgNodes.find(n => n.id === id);
          if (fNode?.x !== undefined) {
            fg.cameraPosition(
              { x: fNode.x, y: (fNode.y ?? 0) + 20, z: (fNode.z ?? 0) + 50 },
              { x: fNode.x, y: fNode.y ?? 0, z: fNode.z ?? 0 },
              1500,
            );
          }
        }
      }
    },
  });
  // Admin mode: true on localhost or when token matches
  const [isAdmin] = useState(() => {
    if (typeof window === 'undefined') return false;
    const { hostname } = window.location;
    if (hostname === 'localhost' || hostname === '127.0.0.1') return true;
    if (hostname.endsWith('.guitaralchemist.com') || hostname === 'guitaralchemist.com') return true;
    return localStorage.getItem('pr-admin-token') === 'ga-owner-2026';
  });
  const [activeHealthTip, setActiveHealthTip] = useState<string | null>(null);
  const [viewers, setViewers] = useState<ViewerInfo[]>([]);
  const selfConnectionIdRef = useRef<string | null>(null);

  // Algedonic effect refs — mutable state for animation loop (no re-render needed)
  const activeRipplesRef = useRef<Map<string, ActiveRipple>>(new Map());
  const edgePropagationsRef = useRef<Map<string, EdgePropagation>>(new Map());
  const pleasureWindowRef = useRef<number[]>([]);
  const bloomPassRef = useRef<UnrealBloomPass | null>(null);
  const surgeBloomRef = useRef<{ startTime: number; originalStrength: number } | null>(null);
  const solarFollowCameraRef = useRef(true); // when false, solar system stays in place (planet zoom)
  const trackedPlanetRef = useRef<string | null>(null); // mutable for animation loop
  const [_trackedPlanetName, setTrackedPlanetName] = useState<string | null>(null); // for UI indicator
  const [_criticState, setCriticState] = useState<CriticState>({
    phase: 'idle', result: null, history: [], lastAnalysis: null,
  });

  // Godot fullscreen overlay toggle (independent of panel state)
  const [godotFullscreen, setGodotFullscreen] = useState(false);

  // GIS layer managers (one per planet)
  const gisManagersRef = useRef<Map<string, GisLayerManager>>(new Map());
  const [gisManagers, setGisManagers] = useState<Map<string, GisLayerManager>>(new Map());
  const signalRGisBridgeRef = useRef<SignalRGisBridgeHandle | null>(null);

  // Phase 2: Dynamic panel definitions created via IXQL CREATE PANEL
  const dynamicPanelDefsRef = useRef<Map<string, DynamicPanelDefinition>>(new Map());
  // Phase 6: Grid panel specs created via IXQL CREATE PANEL KIND grid
  const gridPanelSpecsRef = useRef<Map<string, PanelSpec>>(new Map());
  const vizSpecsRef = useRef<Map<string, VizSpec>>(new Map());
  // Phase 8: Form specs created via IXQL CREATE FORM
  const formSpecsRef = useRef<Map<string, FormSpec>>(new Map());

  // IXql command handler — dispatches visual overrides, panel CRUD, and more
  const handleIxqlCommand = useCallback((result: IxqlParseResult) => {
    const fg = graphRef.current;
    if (!result.ok) {
      recordInvocation('parse-error', false);
      return;
    }

    const cmd = result.command;
    recordInvocation(cmd.type, true);

    if (cmd.type === 'reset') {
      // Clear all IXql overrides (nodes + edges)
      if (!fg) return;
      fg.graphData().nodes.forEach((node: object) => {
        const n = node as GraphNode & { __threeObj?: THREE.Object3D };
        if (n.__threeObj) {
          n.__threeObj.userData.ixqlOverrides = undefined;
        }
      });
      fg.graphData().links.forEach((link: object) => {
        (link as GraphLink & { ixqlOverrides?: Record<string, unknown> }).ixqlOverrides = undefined;
      });
      return;
    }

    if (cmd.type === 'select' && cmd.target === 'nodes' && cmd.predicates.length > 0 && cmd.assignments.length > 0) {
      if (!fg) return;
      fg.graphData().nodes.forEach((node: object) => {
        const n = node as GraphNode & { __threeObj?: THREE.Object3D };
        const matches = cmd.predicates.every(p =>
          evaluatePredicate(p, n as unknown as Record<string, unknown>),
        );
        if (matches && n.__threeObj) {
          const overrides: Record<string, unknown> = {};
          cmd.assignments.forEach(a => { overrides[a.property] = a.value; });
          n.__threeObj.userData.ixqlOverrides = overrides;
        }
      });
      return;
    }

    // SELECT edges — apply visual overrides to graph edges (color, width, opacity, particles)
    if (cmd.type === 'select' && cmd.target === 'edges' && cmd.assignments.length > 0) {
      if (!fg) return;
      fg.graphData().links.forEach((link: object) => {
        const l = link as GraphLink & { ixqlOverrides?: Record<string, unknown> };
        const matchObj: Record<string, unknown> = {
          source: typeof l.source === 'string' ? l.source : (l.source as GraphNode).id,
          target: typeof l.target === 'string' ? l.target : (l.target as GraphNode).id,
          type: l.type,
          color: l.color,
          width: l.width,
        };
        const matches = cmd.predicates.length === 0 ||
          cmd.predicates.every(p => evaluatePredicate(p, matchObj));
        if (matches) {
          const overrides: Record<string, unknown> = {};
          cmd.assignments.forEach(a => { overrides[a.property] = a.value; });
          l.ixqlOverrides = overrides;
        }
      });
      return;
    }

    // Phase 2: CREATE PANEL — register in PanelRegistry and auto-open
    if (cmd.type === 'create-panel') {
      panelRegistry.register({
        definition: {
          id: cmd.id,
          label: cmd.id.replace(/[-_]/g, ' ').replace(/\b\w/g, c => c.toUpperCase()),
          icon: cmd.icon || 'detail',
          renderMode: 'side',
          layout: cmd.layout,
          source: cmd.source,
          showFields: cmd.showFields,
        },
      });
      // Store the full command for DynamicPanel rendering
      dynamicPanelDefsRef.current.set(cmd.id, {
        id: cmd.id,
        label: cmd.id.replace(/[-_]/g, ' ').replace(/\b\w/g, c => c.toUpperCase()),
        source: cmd.source,
        layout: cmd.layout,
        wherePredicates: cmd.wherePredicates,
        showFields: cmd.showFields,
        filter: cmd.filter,
      });
      setActivePanel(cmd.id);
      return;
    }

    // Phase 6: CREATE PANEL KIND grid — compile to PanelSpec and register
    if (cmd.type === 'create-grid-panel') {
      const spec = compileGridPanel(cmd);
      gridPanelSpecsRef.current.set(cmd.id, spec);
      panelRegistry.register({
        definition: {
          id: cmd.id,
          label: cmd.id.replace(/[-_]/g, ' ').replace(/\b\w/g, c => c.toUpperCase()),
          icon: 'grid',
          renderMode: 'side',
          group: 'governance',
        },
      });
      setActivePanel(cmd.id);
      return;
    }

    // Phase 7: CREATE VIZ — compile to VizSpec and register
    if (cmd.type === 'create-viz') {
      const vizSpec = compileViz(cmd);
      vizSpecsRef.current.set(cmd.id, vizSpec);
      panelRegistry.register({
        definition: {
          id: cmd.id,
          label: cmd.id.replace(/[-_]/g, ' ').replace(/\b\w/g, c => c.toUpperCase()),
          icon: 'activity',
          renderMode: 'side',
          group: 'viz',
        },
      });
      setActivePanel(cmd.id);
      return;
    }

    // Phase 8: CREATE FORM — compile to FormSpec and register
    if (cmd.type === 'create-form') {
      const formSpec = compileForm(cmd);
      formSpecsRef.current.set(cmd.id, formSpec);
      panelRegistry.register({
        definition: {
          id: cmd.id,
          label: cmd.id.replace(/[-_]/g, ' ').replace(/\b\w/g, c => c.toUpperCase()),
          icon: 'detail',
          renderMode: 'side',
          group: 'governance',
        },
      });
      setActivePanel(cmd.id);
      return;
    }

    // Phase 2: DROP PANEL — unregister and close if active
    if (cmd.type === 'drop') {
      panelRegistry.unregister(cmd.id);
      dynamicPanelDefsRef.current.delete(cmd.id);
      gridPanelSpecsRef.current.delete(cmd.id);
      vizSpecsRef.current.delete(cmd.id);
      formSpecsRef.current.delete(cmd.id);
      setActivePanel(prev => prev === cmd.id ? null : prev);
      return;
    }

    // Phase 3: BIND HEALTH — register declarative health rule
    if (cmd.type === 'bind-health') {
      const bindingId = cmd.targetKind === 'panel' ? cmd.targetId : `node:${cmd.targetSelector.map(p => p.field).join(',')}`;
      healthBindingEngine.register({ id: bindingId, command: cmd });
      return;
    }

    // Phase 5: ON...THEN — register reactive trigger
    if (cmd.type === 'on-changed') {
      const ruleId = `on:${cmd.source}`;
      reactiveEngine.register(ruleId, cmd, (result) => handleIxqlCommand(result));
      return;
    }

    // Phase 10: ON VIOLATION — register agentic violation rule
    if (cmd.type === 'on-violation') {
      const ruleId = `violation:${cmd.source}:${cmd.severity}`;
      violationMonitor.registerViolation({
        id: ruleId,
        source: cmd.source,
        predicates: cmd.condition,
        severity: cmd.severity,
        actions: cmd.actions,
        notify: cmd.notify,
      });
      return;
    }

    // Phase 10: SAVE QUERY — persist named query as governance artifact
    if (cmd.type === 'save') {
      // Reconstruct command text from parsed fields (raw input not available in callback)
      const cmdText = `SAVE QUERY "${cmd.id}"${cmd.asArtifact ? ' AS artifact' : ''}${cmd.rationale ? ` RATIONALE "${cmd.rationale}"` : ''}`;
      savedQueryStore.save(cmd.id, cmdText, cmd.asArtifact, cmd.rationale);
      return;
    }

    // ── Epistemic Constitution commands (Articles E-0 to E-9) ──

    if (cmd.type === 'show-epistemic') {
      if (!fg) return;
      const nodes = fg.graphData().nodes as GraphNode[];

      // Epistemic tensor visual mappings
      const TENSOR_COLORS: Record<string, string> = {
        'C_T': '#FFD700',   // wisdom = gold
        'T_C': '#CE93D8',   // hunch = shimmer purple
        'U_F': '#4FC3F7',   // blindspot discovered = bright blue
        'U_U': '#6b7280',   // absolute unknown = gray
        'C_C': '#FF4444',   // contradictory ground = red
      };

      if (cmd.visualize) {
        // Apply visual overrides based on epistemic query
        nodes.forEach((node: GraphNode) => {
          const n = node as GraphNode & { __threeObj?: THREE.Object3D };
          if (!n.__threeObj) return;
          const nodeData = n as unknown as Record<string, unknown>;
          const matches = cmd.predicates.length === 0 ||
            cmd.predicates.every(p => evaluatePredicate(p, nodeData));
          if (matches) {
            const tensor = (nodeData['tensorConfig'] as string) ?? 'U_U';
            const overrides: Record<string, unknown> = {
              glow: TENSOR_COLORS[tensor] ?? '#8b949e',
              pulse: tensor === 'C_T' ? 1.5 : tensor.startsWith('C') ? 2.0 : 0,
              opacity: tensor === 'U_U' ? 0.4 : 1.0,
            };
            // High viscosity = rigid/frozen appearance
            const viscosity = Number(nodeData['viscosity'] ?? 0);
            if (viscosity > 0.8) {
              overrides['speed'] = 0;
              overrides['color'] = '#88ccdd';
            }
            n.__threeObj.userData.ixqlOverrides = overrides;
          }
        });
      }

      // Log the query for telemetry
      console.log(`[IXQL] SHOW ${cmd.target}`, {
        predicates: cmd.predicates,
        orderBy: cmd.orderBy,
        limit: cmd.limit,
        visualize: cmd.visualize,
        matchCount: cmd.visualize ? nodes.filter((n: GraphNode) => {
          const d = n as unknown as Record<string, unknown>;
          return cmd.predicates.length === 0 || cmd.predicates.every(p => evaluatePredicate(p, d));
        }).length : 'n/a',
      });
      return;
    }

    if (cmd.type === 'methylate') {
      console.log(`[IXQL] METHYLATE strategy '${cmd.strategyId}'`, cmd.reason ? `reason: ${cmd.reason}` : '');
      // Store methylation state in localStorage for persistence
      const key = `epistemic-methylation-${cmd.strategyId}`;
      localStorage.setItem(key, JSON.stringify({
        methylated: true,
        reason: cmd.reason,
        methylatedAt: new Date().toISOString(),
      }));
      return;
    }

    if (cmd.type === 'demethylate') {
      console.log(`[IXQL] DEMETHYLATE strategy '${cmd.strategyId}'`);
      localStorage.removeItem(`epistemic-methylation-${cmd.strategyId}`);
      return;
    }

    if (cmd.type === 'amnesia') {
      const scheduledFor = new Date(Date.now() + cmd.scheduleDays * 86400000).toISOString();
      console.log(`[IXQL] AMNESIA belief '${cmd.beliefId}' scheduled for ${scheduledFor}`);
      const schedule = JSON.parse(localStorage.getItem('epistemic-amnesia-schedule') ?? '[]');
      schedule.push({ beliefId: cmd.beliefId, scheduledFor, executed: false });
      localStorage.setItem('epistemic-amnesia-schedule', JSON.stringify(schedule));
      return;
    }

    if (cmd.type === 'broadcast') {
      console.log(`[IXQL] BROADCAST ${cmd.target}`, { predicates: cmd.predicates });
      // Federated epistemology: broadcast via Galactic Protocol (Article E-9)
      // In production, this would send to SignalR hub for peer agents
      return;
    }
  }, []);

  // Phase 10: Wire violation monitor dispatch to IXQL handler
  useEffect(() => {
    violationMonitor.setDispatch((result) => handleIxqlCommand(result));
  }, [handleIxqlCommand]);

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
    // Drive Demerzel's face emotion from governance signals
    if (signal.type === 'pain') {
      setDemerzelEmotion(signal.severity === 'critical' ? 'alert' : 'concerned');
    } else {
      setDemerzelEmotion('pleased');
    }

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

    // ── Mobile/low-end detection — skip heavy resources upfront ──
    const isMobile = /Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent);
    const isLowEnd = isMobile || navigator.hardwareConcurrency <= 4 || window.innerWidth < 768;
    if (isLowEnd) {
      console.info(`[PrimeRadiant] Low-end mode: mobile=${isMobile}, cores=${navigator.hardwareConcurrency}, width=${window.innerWidth}`);
    }

    let disposed = false;
    let autoZoomTimeoutOuter: ReturnType<typeof setTimeout> | null = null;
    let pollingHandleOuter: LivePollingHandle | undefined;
    let cloudCleanupOuter: (() => void) | undefined;
    let markerCleanupOuter: (() => void) | undefined;
    let lodCleanupOuter: (() => void) | undefined;
    let criticCleanupOuter: (() => void) | undefined;
    let solarMouseMoveHandler: ((e: MouseEvent) => void) | null = null;
    let solarDblClickHandler: (() => void) | null = null;
    let milkyWayToggleHandler: ((e: KeyboardEvent) => void) | null = null;
    let solarClickHandler: (() => void) | null = null;
    let eiffelHandleOuter: CrystalEiffelTowerHandle | null = null;
    let filamentsHandle: TerminalFilamentsHandle | null = null;
    let zoomInertiaHandlerOuter: ((e: WheelEvent) => void) | null = null;
    let zoomVelocityRef = { v: 0 };

    // Load data — try API first, fall back to static
    const initGraph = async () => {
      const graph = liveDataUrl
        ? await loadGovernanceDataAsync(liveDataUrl)
        : loadGovernanceData(data);
      if (disposed) return;
      initScene(graph);
    };

    const initScene = (graph: ReturnType<typeof loadGovernanceData>) => {

    // Enrich nodes with Markov transition probabilities for Seldon panel
    enrichNodesWithMarkov(graph);

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
        const l = link as GraphLink & { ixqlOverrides?: Record<string, unknown> };
        // IXQL edge override takes priority
        if (l.ixqlOverrides?.color) return String(l.ixqlOverrides.color);
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
        const l = link as GraphLink & { ixqlOverrides?: Record<string, unknown> };
        if (l.ixqlOverrides?.width) return Number(l.ixqlOverrides.width);
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

    // Add bloom post-processing — reduced on mobile to save GPU fill rate
    const bloomSize = isLowEnd
      ? new THREE.Vector2(container.clientWidth * 0.5, container.clientHeight * 0.5)
      : new THREE.Vector2(container.clientWidth, container.clientHeight);
    const bloomPass = new UnrealBloomPass(
      bloomSize,
      isLowEnd ? 0.3 : 0.6,   // strength
      isLowEnd ? 0.3 : 0.6,   // radius
      isLowEnd ? 0.8 : 0.5,   // threshold (higher on mobile = less bloom)
    );
    fg.postProcessingComposer().addPass(bloomPass);
    bloomPassRef.current = bloomPass;

    // Set renderer pixel ratio — cap at 1.5 on mobile to save fill rate
    if (isLowEnd) {
      fg.renderer().setPixelRatio(Math.min(window.devicePixelRatio, 1.5));
    }

    // Chromatic aberration post-processing — skip on mobile (extra shader pass = costly)
    if (!isLowEnd) {
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
    }

    // ─── ADAPTIVE QUALITY — auto-downgrade on crappy GPUs ───
    let frameCount = 0;
    let lastFpsCheck = Date.now();
    let currentFps = 60;
    let qualityLevel: 'high' | 'medium' | 'low' = isLowEnd ? 'low' : 'high';
    // ─── Performance info panel (FPS + GPU + memory) ───
    // Detect GPU name from WebGL context
    let gpuName = 'Unknown GPU';
    let maxTextureSize = 0;
    try {
      const canvas = container.querySelector('canvas');
      if (canvas) {
        const gl = canvas.getContext('webgl2') || canvas.getContext('webgl');
        if (gl) {
          const ext = gl.getExtension('WEBGL_debug_renderer_info');
          if (ext) {
            gpuName = gl.getParameter(ext.UNMASKED_RENDERER_WEBGL) as string || 'Unknown GPU';
          }
          maxTextureSize = gl.getParameter(gl.MAX_TEXTURE_SIZE) as number || 0;
        }
      }
    } catch { /* some browsers block GPU detection */ }

    // Build perf info DOM
    const perfEl = document.createElement('div');
    perfEl.className = 'prime-radiant__perf-info';
    const badgeEl = document.createElement('div');
    badgeEl.className = 'prime-radiant__perf-badge';
    badgeEl.textContent = '-- FPS';
    const detailsEl = document.createElement('div');
    detailsEl.className = 'prime-radiant__perf-details';
    perfEl.appendChild(badgeEl);
    perfEl.appendChild(detailsEl);
    container.style.position = 'relative';
    container.appendChild(perfEl);
    // Expand on hover
    perfEl.addEventListener('mouseenter', () => perfEl.classList.add('prime-radiant__perf-info--expanded'));
    perfEl.addEventListener('mouseleave', () => perfEl.classList.remove('prime-radiant__perf-info--expanded'));

    let lastCameraSave = 0;
    // Pre-allocate reusable vectors OUTSIDE the tick loop (zero GC pressure)
    const _tarsOffset = new THREE.Vector3();
    const _faceOffset = new THREE.Vector3();
    let _filamentPosMap: Map<string, THREE.Vector3> | null = null;
    const _riggedFaceOffset = new THREE.Vector3();
    const _solarOffset = new THREE.Vector3();
    const _stationOffset = new THREE.Vector3();
    const _trackWp = new THREE.Vector3();
    const _trackOffset = new THREE.Vector3();

    fg.onEngineTick(() => {
      try {
      const t = Date.now() * 0.001;

      // ─── Zoom inertia — apply momentum and decay ───
      if (Math.abs(zoomVelocityRef.v) > 0.001) {
        const zCam = fg.camera() as THREE.PerspectiveCamera;
        const zDir = new THREE.Vector3();
        zCam.getWorldDirection(zDir);
        zCam.position.addScaledVector(zDir, -zoomVelocityRef.v);
        zoomVelocityRef.v *= 0.92;
      } else {
        zoomVelocityRef.v = 0;
      }

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
        // Performance grade from FPS
        const grade = currentFps >= 60 ? 'Excellent' : currentFps >= 45 ? 'Good' : currentFps >= 30 ? 'Fair' : 'Poor';
        const gradeColor = currentFps >= 60 ? '#33CC66' : currentFps >= 45 ? '#88CC33' : currentFps >= 30 ? '#FFB300' : '#FF4444';
        const qualityTag = qualityLevel !== 'high' ? ` [${qualityLevel}]` : '';
        badgeEl.innerHTML = `<span style="color:${gradeColor}">${currentFps} FPS</span> <span class="prime-radiant__perf-grade" style="color:${gradeColor}">${grade}</span>${qualityTag}`;
        // Update details (only when expanded — cheap innerHTML update once per second)
        const perf = performance as unknown as { memory?: { usedJSHeapSize: number; jsHeapSizeLimit: number } };
        const heapInfo = perf.memory
          ? `${Math.round(perf.memory.usedJSHeapSize / 1048576)} MB / ${Math.round(perf.memory.jsHeapSizeLimit / 1048576)} MB`
          : 'N/A';
        const texInfo = maxTextureSize ? `Max Tex: ${maxTextureSize}px` : '';
        detailsEl.innerHTML = [
          `<div class="prime-radiant__perf-row"><span class="prime-radiant__perf-label">GPU</span><span class="prime-radiant__perf-value">${gpuName}</span></div>`,
          `<div class="prime-radiant__perf-row"><span class="prime-radiant__perf-label">Grade</span><span class="prime-radiant__perf-value" style="color:${gradeColor}">${grade}${qualityTag}</span></div>`,
          `<div class="prime-radiant__perf-row"><span class="prime-radiant__perf-label">JS Heap</span><span class="prime-radiant__perf-value">${heapInfo}</span></div>`,
          texInfo ? `<div class="prime-radiant__perf-row"><span class="prime-radiant__perf-label">Texture</span><span class="prime-radiant__perf-value">${texInfo}</span></div>` : '',
        ].join('');

        // Auto-downgrade
        if (currentFps < 25 && qualityLevel !== 'low') {
          qualityLevel = 'low';
          ambientDust.visible = false;
          starField.visible = false;
          demerzelFace.visible = false;
          bloomPass.strength = 0.15;
          if (filamentsHandle) filamentsHandle.group.visible = false;
        } else if (currentFps < 40 && qualityLevel === 'high') {
          qualityLevel = 'medium';
          ambientDust.visible = false;
          bloomPass.strength = 0.3;
        } else if (currentFps >= 55 && qualityLevel !== 'high') {
          qualityLevel = 'high';
          ambientDust.visible = true;
          starField.visible = true;
          demerzelFace.visible = true;
          bloomPass.strength = 0.5;
          if (filamentsHandle) filamentsHandle.group.visible = true;
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
            // IXQL visual overrides — color, glow, pulse, opacity
            const overrides = group.userData.ixqlOverrides as Record<string, unknown> | undefined;
            if (overrides) {
              // Color override
              if (overrides.color && mat.uniforms?.uColor) {
                mat.uniforms.uColor.value = new THREE.Color(String(overrides.color));
              }
              // Glow: boost emissive intensity
              if (overrides.glow && mat.uniforms?.uColor) {
                const baseColor = mat.uniforms.uColor.value as THREE.Color;
                const boost = 1.0 + Math.sin(t * 3.0) * 0.3;
                mat.uniforms.uColor.value = baseColor.clone().multiplyScalar(boost);
              }
              // Pulse: oscillate scale
              if (overrides.pulse) {
                const pulseScale = 1.0 + Math.sin(t * 5.0) * 0.25;
                group.scale.setScalar(pulseScale);
              }
              // Opacity override
              if (overrides.opacity !== undefined && mat.uniforms?.uOpacity) {
                mat.uniforms.uOpacity.value = Number(overrides.opacity);
              }
              // Visibility
              if (overrides.visible === false) {
                group.visible = false;
              }
            } else {
              // Reset pulse scale if no overrides
              if (group.scale.x !== 1) group.scale.setScalar(1);
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
      _tarsOffset.set(-50, -28, -50);
      _tarsOffset.applyQuaternion(cam.quaternion);
      tarsRobot.position.copy(cam.position).add(_tarsOffset);
      tarsRobot.quaternion.copy(cam.quaternion);

      // Demerzel procedural face — far left, above TARS
      updateDemerzelFace(demerzelFace, t, cam.position, false);
      _faceOffset.set(-50, -8, -40);
      _faceOffset.applyQuaternion(cam.quaternion);
      demerzelFace.position.copy(cam.position).add(_faceOffset);
      demerzelFace.quaternion.copy(cam.quaternion);

      // (Rigged face rendered in separate overlay — DemerzelFaceOverlay component)

      // (Trantor removed — replaced by Earth + nebulae)

      // ─── Solar system — compact orrery, top-right of view ───
      // Position FIRST so updateSolarSystem gets correct Sun world position for terminator
      if (solarFollowCameraRef.current) {
        _solarOffset.set(12, 6, -20);
        _solarOffset.applyQuaternion(cam.quaternion);
        solarSystem.position.copy(cam.position).add(_solarOffset);
      }
      // When solarFollowCameraRef is false, solar system stays frozen in place
      // (planet zoom mode — user clicks Reset View to resume)
      // Phase 1.2: Quality-gate solar system updates (after position set)
      if (qualityLevel !== 'low') {
        solarSystem.userData.qualityLevel = qualityLevel; // pass to flare system
        if (qualityLevel === 'high' || frameCount % 3 === 0) {
          updateSolarSystem(solarSystem, t);
          // Update GIS layer animations (pulse rings, animated paths)
          for (const mgr of gisManagersRef.current.values()) mgr.update(t);
        }
      }

      // ─── Terminal filaments — organic sway + pulsing tips ───
      if (filamentsHandle) {
        // Reuse persistent map — avoid GC pressure from 180 Vector3s/frame
        if (!_filamentPosMap) _filamentPosMap = new Map();
        const allNodes = fg.graphData().nodes as GraphNode[];
        for (const n of allNodes) {
          if (n.x !== undefined) {
            let v = _filamentPosMap.get(n.id);
            if (!v) { v = new THREE.Vector3(); _filamentPosMap.set(n.id, v); }
            v.set(n.x, n.y ?? 0, n.z ?? 0);
          }
        }
        filamentsHandle.update(t, _filamentPosMap);
      }

      // ─── Crystal Eiffel Tower — sparking below the graph ───
      if (eiffelHandleOuter) eiffelHandleOuter.update(t);

      // ─── Jarvis Space Station — top-left of view ───
      if (qualityLevel !== 'low') {
        updateSpaceStation(spaceStation, t);
      }
      _stationOffset.set(-8, 8, -20);
      _stationOffset.applyQuaternion(cam.quaternion);
      spaceStation.position.copy(cam.position).add(_stationOffset);

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
      } catch (err) {
        // Never let a single tick error kill the animation loop
        console.warn('[PrimeRadiant] Tick error:', err);
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
      controls.dampingFactor = 0.12;   // smooth inertia on all movements
      controls.zoomSpeed = 1.2;        // faster zoom response
      (controls as Record<string, unknown>).minDistance = 0.05;
      (controls as Record<string, unknown>).maxDistance = 500;
      (controls as Record<string, unknown>).enableZoom = true;
      (controls as Record<string, unknown>).rotateSpeed = 0.8;
      (controls as Record<string, unknown>).panSpeed = 0.6;
    }

    // ─── Zoom inertia — OrbitControls damping doesn't affect zoom ───
    const ZOOM_FRICTION = 0.92;
    const ZOOM_SENSITIVITY = 0.15;
    zoomInertiaHandlerOuter = (e: WheelEvent) => {
      zoomVelocityRef.v += Math.sign(e.deltaY) * ZOOM_SENSITIVITY;
    };
    container.addEventListener('wheel', zoomInertiaHandlerOuter, { passive: true });

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

          // ─── Nearby galaxies ───
          // Andromeda (M31) — large, tilted elliptical smudge
          vec3 andromedaDir = normalize(vec3(0.2, 0.35, -0.9));
          float andrDist = length(dir - andromedaDir);
          float andrCore = exp(-800.0 * andrDist * andrDist) * 0.12;
          float andrDisk = exp(-80.0 * andrDist * andrDist) * 0.04;
          // Elliptical stretch — flatten along one axis
          vec3 andrLocal = dir - andromedaDir;
          float andrTilt = dot(andrLocal, normalize(vec3(0.5, 1.0, 0.2)));
          float andrEllipse = exp(-200.0 * andrTilt * andrTilt);
          col += vec3(0.85, 0.82, 0.95) * (andrCore + andrDisk * andrEllipse);

          // Triangulum Galaxy (M33) — smaller, fainter, near Andromeda
          vec3 m33Dir = normalize(vec3(0.35, 0.5, -0.78));
          float m33Dist = length(dir - m33Dir);
          float m33Glow = exp(-300.0 * m33Dist * m33Dist) * 0.03;
          float m33Halo = exp(-60.0 * m33Dist * m33Dist) * 0.015;
          col += vec3(0.7, 0.75, 0.9) * (m33Glow + m33Halo);

          // Large Magellanic Cloud (LMC) — southern sky, irregular, blue
          vec3 lmcDir = normalize(vec3(0.6, -0.7, 0.35));
          float lmcDist = length(dir - lmcDir);
          float lmcCore = exp(-100.0 * lmcDist * lmcDist) * 0.05;
          float lmcNoise = fbm(dir * 8.0 + vec3(5.0, 2.0, 1.0));
          col += vec3(0.4, 0.5, 0.8) * lmcCore * (0.5 + lmcNoise);

          // Small Magellanic Cloud (SMC) — companion to LMC
          vec3 smcDir = normalize(vec3(0.45, -0.6, 0.6));
          float smcDist = length(dir - smcDir);
          float smcGlow = exp(-200.0 * smcDist * smcDist) * 0.025;
          col += vec3(0.5, 0.55, 0.75) * smcGlow;

          // ─── Cosmic Microwave Background ───
          // Very faint, large-scale temperature anisotropy pattern
          // Subtle warm/cool spots across the entire sky
          float cmb = fbm(dir * 2.0 + vec3(42.0, 17.0, 73.0));
          float cmbAnisotropy = (cmb - 0.5) * 0.008; // extremely subtle
          // CMB is ~2.7K — map to faint red-blue temperature fluctuations
          vec3 cmbWarm = vec3(0.015, 0.003, 0.0);   // warm spots (red)
          vec3 cmbCool = vec3(0.0, 0.003, 0.015);    // cool spots (blue)
          col += cmbAnisotropy > 0.0 ? cmbWarm * cmbAnisotropy * 100.0
                                     : cmbCool * abs(cmbAnisotropy) * 100.0;

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

    // Milky Way band — skip on mobile (8000-unit sphere is expensive)
    const milkyWayMesh = isLowEnd ? new THREE.Group() : createMilkyWay(8000);
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
    const demerzelFace = isLowEnd ? new THREE.Group() : createDemerzelFace(0.5);
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
    const solarSystem = createSolarSystem(0.15);
    fg.scene().add(solarSystem);

    // ─── CRYSTAL EIFFEL TOWER — toggle via ?tower=1 URL param ───
    if (typeof window !== 'undefined' && new URLSearchParams(window.location.search).has('tower')) {
      eiffelHandleOuter = createCrystalEiffelTower(fg.scene());
      eiffelHandleOuter.group.position.set(0, -50, -80);
    }

    // Live cloud updates disabled — GIBS Mercator→equirectangular mismatch causes blur
    // Static 2k_earth_clouds.jpg looks better until proper reprojection is implemented
    const stopCloudUpdates = () => {}; // no-op

    // Add "you are here" location marker on Earth
    const removeLocationMarker = addLocationMarker(solarSystem);

    // Auto-LOD: load high-res satellite imagery when camera is close to Earth
    // Disabled on mobile — fetches large tile textures that blow the memory budget
    const stopEarthLOD = isLowEnd ? (() => {}) : enableEarthAutoLOD(solarSystem, () => fg.camera());

    // GIS layers — on mobile, only Earth to save memory
    const gisMgrs = new Map<string, GisLayerManager>();
    const gisPlanets = isLowEnd ? ['earth'] : ['earth', 'mars', 'venus', 'jupiter', 'saturn', 'mercury'];
    for (const name of gisPlanets) {
      const mgr = createGisLayer(solarSystem, name);
      if (mgr) gisMgrs.set(name, mgr);
    }
    gisManagersRef.current = gisMgrs;
    setGisManagers(gisMgrs);

    // ─── SignalR → GIS bridge (algedonic signals + belief changes → live pins) ───
    const earthGis = gisMgrs.get('earth');
    if (earthGis) {
      signalRGisBridgeRef.current = startSignalRGisBridge(earthGis, (event) => {
        console.log('[SignalRGisBridge]', event);
      });
    }

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

      // Check all meshes in solar system (planets + moons) recursively
      const hits = solarRaycaster.intersectObjects(solarSystem.children, true);

      // Only consider hits on named objects (skip rings, atmospheres, orbit lines)
      const namedHit = hits.find(h => h.object.name);
      const hitName = namedHit ? namedHit.object.name : null;
      if (hitName !== currentHoveredPlanet) {
        currentHoveredPlanet = hitName;
        showPlanetLabel(solarSystem, hitName);
        if (canvas) {
          canvas.style.cursor = hitName ? 'pointer' : '';
        }
      }
    };

    const onSolarClick = () => {
      // Single click — just show label (handled by hover)
    };

    // Double-click on a planet/moon → fly camera closer to it
    // Special: double-click on Moon launches Lunar Lander simulation
    const onSolarDblClick = () => {
      if (!currentHoveredPlanet) return;

      // Moon → launch Lunar Lander
      if (currentHoveredPlanet === 'moon') {
        setActivePanel('lunar');
        return;
      }

      const group = fg.scene().getObjectByName('sun')?.parent;
      if (!group) return;

      const obj = group.getObjectByName(currentHoveredPlanet);
      if (!obj) return;

      // Freeze solar system, snapshot position, fly toward it
      solarFollowCameraRef.current = false;
      const wp = new THREE.Vector3();
      obj.getWorldPosition(wp);

      // Keep enough distance so OrbitControls zoom still works (above minDistance)
      const cam = fg.camera() as THREE.PerspectiveCamera;
      const camPos = cam.position.clone();
      const dir = wp.clone().sub(camPos).normalize();
      const dist = camPos.distanceTo(wp);
      // Fly to 40% of current distance (never closer than 2 units)
      const flyDist = Math.max(dist * 0.4, 2);
      const target = wp.clone().sub(dir.multiplyScalar(flyDist));

      fg.cameraPosition(
        { x: target.x, y: target.y, z: target.z },
        { x: wp.x, y: wp.y, z: wp.z },
        1500,
      );

      // Resume follow after animation
      setTimeout(() => { solarFollowCameraRef.current = true; }, 1600);
    };

    solarMouseMoveHandler = onSolarMouseMove;
    solarClickHandler = onSolarClick;
    solarDblClickHandler = onSolarDblClick;
    container.addEventListener('mousemove', onSolarMouseMove);
    container.addEventListener('click', onSolarClick);
    container.addEventListener('dblclick', onSolarDblClick);

    // ─── JARVIS SPACE STATION — modular station with docking animation ───
    const spaceStation = isLowEnd ? new THREE.Group() : createSpaceStation(0.12);
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

    // ─── Terminal filaments — data-driven tendrils on leaf nodes ───
    // Filament count reflects real governance depth: more connections = more filaments.
    // Color reflects node health status. Speed reflects staleness.
    setTimeout(() => {
      if (disposed) return;
      const graphNodes = fg.graphData().nodes as GraphNode[];
      const graphLinks = fg.graphData().links as GraphLink[];
      // Find terminal nodes (no outgoing edges)
      const sourceIds = new Set(graphLinks.map(l => getLinkNodeId(l.source)));
      // Count incoming edges per node for filament density
      const incomingCount = new Map<string, number>();
      for (const l of graphLinks) {
        const targetId = getLinkNodeId(l.target);
        incomingCount.set(targetId, (incomingCount.get(targetId) ?? 0) + 1);
      }
      const terminalNodes = graphNodes
        .filter(n => !sourceIds.has(n.id) && n.x !== undefined)
        .map(n => ({
          position: new THREE.Vector3(n.x ?? 0, n.y ?? 0, n.z ?? 0),
          color: new THREE.Color(n.color),
          nodeId: n.id,
        }));
      if (terminalNodes.length > 0) {
        // Data-driven filament count: 1 base + 1 per incoming connection (max 6)
        const avgIncoming = terminalNodes.reduce((sum, n) =>
          sum + (incomingCount.get(n.nodeId) ?? 0), 0) / terminalNodes.length;
        const filamentCount = Math.min(6, Math.max(2, Math.round(1 + avgIncoming)));
        filamentsHandle = createTerminalFilaments(terminalNodes, {
          count: filamentCount,
          speed: 0.8,
        });
        fg.scene().add(filamentsHandle.group);
      }
    }, 4000); // Wait for force layout to settle

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
          enrichNodesWithMarkov(freshGraph);
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
          // Phase 10: Feed live data to violation monitor for agentic checks
          for (const node of freshGraph.nodes) {
            violationMonitor.checkViolations(node as unknown as Record<string, unknown>, 'governance');
          }
        },
        onAlgedonicSignal: (signal) => {
          handleAlgedonicSignal(signal);
          signalRGisBridgeRef.current?.handleAlgedonicSignal(signal);
        },
        onBeliefUpdate: (belief) => {
          signalRGisBridgeRef.current?.handleBeliefUpdate(belief);
        },
        onViewersChanged: (viewerList) => {
          // Diff previous vs new viewer list for connect/disconnect logging
          const prevIds = new Set(viewers.map(v => v.connectionId));
          const nextIds = new Set(viewerList.map(v => v.connectionId));
          const connLog = getConnectionLog();

          // Detect new connections
          for (const v of viewerList) {
            if (!prevIds.has(v.connectionId)) {
              const isSelf = v.connectionId === selfConnectionIdRef.current;
              connLog.logConnect(v.connectionId, v.browser, v.color, isSelf);
            }
          }
          // Detect disconnections
          for (const v of viewers) {
            if (!nextIds.has(v.connectionId)) {
              const isSelf = v.connectionId === selfConnectionIdRef.current;
              connLog.logDisconnect(v.connectionId, v.browser, v.color, isSelf);
            }
          }

          setViewers(viewerList);
          // Track our own connection ID — it's the one we just added
          if (!selfConnectionIdRef.current && viewerList.length > 0) {
            // The most recently connected viewer is likely us
            const sorted = [...viewerList].sort((a, b) =>
              new Date(b.connectedAt).getTime() - new Date(a.connectedAt).getTime(),
            );
            selfConnectionIdRef.current = sorted[0].connectionId;
          }
        },
        onError: (err) => console.warn('[PrimeRadiant] Live poll error:', err.message),
      });
    }

    // Expose cleanup handles to outer scope
    autoZoomTimeoutOuter = autoZoomTimeout;
    pollingHandleOuter = pollingHandle;
    cloudCleanupOuter = stopCloudUpdates;
    markerCleanupOuter = removeLocationMarker;
    lodCleanupOuter = stopEarthLOD;

    // ─── DEMERZEL AUTONOMOUS DRIVER — rule-based IXQL self-healing ───
    // Evaluates governance graph health → emits IXQL commands → no LLM needed
    // Disabled until stability is confirmed — was causing freezes
    criticCleanupOuter = startDemerzelDriver({
      enabled: false,
      intervalMs: 30_000,        // evaluate every 30 seconds
      getGraphData: () => fg.graphData() as { nodes: unknown[]; links: unknown[] },
      onPhaseChange: (phase: CriticPhase) => {
        setCriticState(prev => ({ ...prev, phase }));
      },
      onResult: (result) => {
        setCriticState(prev => ({
          ...prev,
          result,
          history: [...prev.history.slice(-19), result],
          lastAnalysis: new Date(),
        }));
      },
      onIxqlCommand: handleIxqlCommand,
    });

    }; // end initScene

    // Kick off async init
    initGraph();

    return () => {
      disposed = true;
      if (milkyWayToggleHandler) window.removeEventListener('keydown', milkyWayToggleHandler);
      if (autoZoomTimeoutOuter) clearTimeout(autoZoomTimeoutOuter);
      pollingHandleOuter?.stop();
      signalRGisBridgeRef.current?.cleanup();
      signalRGisBridgeRef.current = null;
      cloudCleanupOuter?.();
      markerCleanupOuter?.();
      lodCleanupOuter?.();
      criticCleanupOuter?.();
      if (zoomInertiaHandlerOuter) container.removeEventListener('wheel', zoomInertiaHandlerOuter);
      filamentsHandle?.dispose();
      eiffelHandleOuter?.dispose();
      reactiveEngine.dispose();
      violationMonitor.dispose();
      if (solarMouseMoveHandler) {
        container.removeEventListener('mousemove', solarMouseMoveHandler);
      }
      if (solarClickHandler) {
        container.removeEventListener('click', solarClickHandler);
      }
      if (solarDblClickHandler) {
        container.removeEventListener('dblclick', solarDblClickHandler);
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

  // ─── Fetch belief states for Seldon panel ───
  useEffect(() => {
    const FALLBACK_BELIEFS: BeliefState[] = [
      { id: 'belief-asimov', proposition: 'All agent actions comply with Asimov constitutional articles', truth_value: 'true' as const, confidence: 0.92, last_updated: '2026-03-28T18:00:00Z', evaluated_by: 'governance-audit' },
      { id: 'belief-kaizen', proposition: 'PDCA improvement cycles are executing within SLA', truth_value: 'true' as const, confidence: 0.85, last_updated: '2026-03-28T17:30:00Z', evaluated_by: 'kaizen-policy' },
      { id: 'belief-conscience', proposition: 'Proto-conscience discomfort signals processed within 72h', truth_value: 'both' as const, confidence: 0.61, last_updated: '2026-03-28T16:00:00Z', evaluated_by: 'conscience-observability' },
      { id: 'belief-ml', proposition: 'ML feedback calibration recommendations are acted upon', truth_value: 'unknown' as const, confidence: 0.35, last_updated: '2026-03-27T12:00:00Z', evaluated_by: 'ml-feedback-policy' },
      { id: 'belief-ergol', proposition: 'ERGOL operational links cover all governance artifacts', truth_value: 'true' as const, confidence: 0.88, last_updated: '2026-03-28T19:00:00Z', evaluated_by: 'ergol-scanner' },
      { id: 'belief-streeling', proposition: 'Streeling knowledge transfers absorbed by target repos', truth_value: 'false' as const, confidence: 0.72, last_updated: '2026-03-28T15:00:00Z', evaluated_by: 'streeling-policy' },
      { id: 'belief-rollback', proposition: 'Automatic rollback mechanisms tested and operational', truth_value: 'true' as const, confidence: 0.95, last_updated: '2026-03-28T18:30:00Z', evaluated_by: 'rollback-policy' },
    ];
    const fetchBeliefs = async () => {
      try {
        // Try SignalR first (already connected), HTTP as last resort
        const res = await fetch('/api/governance/file-content?filePath=governance/state/beliefs/core-beliefs.belief.json', {
          signal: AbortSignal.timeout(3000),
        });
        if (res.ok) {
          const data = await res.json();
          setBeliefs(Array.isArray(data) ? data : FALLBACK_BELIEFS);
          return;
        }
      } catch { /* SignalR will provide beliefs — this is just a fallback */ }
      // Don't override if SignalR already delivered beliefs
      setBeliefs(prev => prev.length > 0 ? prev : FALLBACK_BELIEFS);
    };
    fetchBeliefs();
    const interval = setInterval(fetchBeliefs, 60_000);
    return () => clearInterval(interval);
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

  // --- Prime Radiant Control API — Claude can drive PR via HTTP ---
  usePrControl({
    openPanel: (id) => setActivePanel(id),
    closePanel: () => setActivePanel(null),
    selectNode: (nodeId) => {
      // Exact match first, then partial/contains match
      const node = graphData?.nodes.find(n => n.id === nodeId)
        ?? graphData?.nodes.find(n => n.id.toLowerCase().includes(nodeId.toLowerCase()));
      if (node) { setSelectedNode(node); setActivePanel('detail'); }
    },
    getGisManager: (planet) => gisManagersRef.current.get(planet),
    getState: () => ({
      activePanel,
      selectedNode: selectedNode?.id ?? null,
      nodeCount: graphData?.nodes.length ?? 0,
      edgeCount: graphData?.edges.length ?? 0,
      backendStatus,
      gis: Object.fromEntries(
        Array.from(gisManagersRef.current.entries()).map(([k, m]) => [k, { pins: m.pinCount, paths: m.pathCount, clusters: m.clusterCount }])
      ),
      godotFullscreen,
      timestamp: Date.now(),
    }),
    setDemerzelEmotion: (emotion) => {
      setDemerzelEmotion(emotion);
    },
    setDemerzelSpeaking: (speaking) => {
      setDemerzelSpeaking(speaking);
    },
    flyCamera: (pos, lookAt, durationMs) => {
      const fg = graphRef.current;
      if (!fg) return;
      fg.cameraPosition(
        pos,
        lookAt ?? { x: 0, y: 0, z: 0 },
        durationMs ?? 1200,
      );
    },
  });

  return (
    <div className={`prime-radiant ${className}`} style={{ width, height }}>
      {/* Canvas area — fills remaining space */}
      <div className="prime-radiant__canvas-area">
        <div ref={containerRef} style={{ width: '100%', flex: 1, minHeight: 0 }} />

      {/* Backend connection status badge with capabilities popover */}
      <div className={`prime-radiant__backend-status prime-radiant__backend-status--${backendStatus}`}>
        <span className="prime-radiant__backend-dot" />
        <span>{backendStatus === 'connected' ? 'API Connected' : backendStatus === 'checking' ? 'Checking...' : 'API Offline'}</span>
        <span className={`prime-radiant__admin-badge prime-radiant__admin-badge--${isAdmin ? 'admin' : 'view'}`}>
          {isAdmin ? 'Admin' : 'View'}
        </span>
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

          {/* Admin indicator */}
          <span className="prime-radiant__health-metric" title={isAdmin ? 'Admin mode (full access)' : 'Read-only mode'}>
            <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke={isAdmin ? '#33CC66' : '#8b949e'} strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
              {isAdmin
                ? <><rect x="3" y="11" width="18" height="11" rx="2" ry="2" /><path d="M7 11V7a5 5 0 0 1 10 0v4" /></>
                : <><rect x="3" y="11" width="18" height="11" rx="2" ry="2" /><path d="M7 11V7a5 5 0 0 1 10 0v4" /><line x1="12" y1="16" x2="12" y2="16.01" /></>
              }
            </svg>
            <span>{isAdmin ? 'Admin' : 'View'}</span>
          </span>
          {isAdmin && (
            <>
              <span style={{ color: '#30363d', margin: '0 4px' }}>|</span>
              <ScreenshotButton />
              <span
                style={{ cursor: 'pointer', marginLeft: 6, opacity: shareToast ? 1 : 0.6, transition: 'opacity 0.2s', position: 'relative' }}
                title="Copy shareable URL"
                onClick={async () => {
                  await deepLink.share();
                  setShareToast(true);
                  setTimeout(() => setShareToast(false), 2000);
                }}
              >
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke={shareToast ? '#33CC66' : '#8b949e'} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <circle cx="18" cy="5" r="3" /><circle cx="6" cy="12" r="3" /><circle cx="18" cy="19" r="3" />
                  <line x1="8.59" y1="13.51" x2="15.42" y2="17.49" /><line x1="15.41" y1="6.51" x2="8.59" y2="10.49" />
                </svg>
                {shareToast && (
                  <span style={{ position: 'absolute', top: -20, left: '50%', transform: 'translateX(-50)', fontSize: 9, color: '#33CC66', whiteSpace: 'nowrap', fontWeight: 600 }}>
                    Copied!
                  </span>
                )}
              </span>
            </>
          )}
        </div>
      )}

      <GalacticClock />

      {/* Viewer presence indicator */}
      {viewers.length > 0 && (
        <div
          className="prime-radiant__viewers"
          title={`${viewers.length} viewer${viewers.length !== 1 ? 's' : ''} online:\n${viewers.map(v => `${v.browser} (${v.color})`).join('\n')}`}
        >
          {viewers.slice(0, 8).map(v => (
            <span
              key={v.connectionId}
              className={`prime-radiant__viewer-dot${v.connectionId === selfConnectionIdRef.current ? ' prime-radiant__viewer-dot--self' : ''}`}
              style={{ backgroundColor: v.color }}
            />
          ))}
          {viewers.length > 8 && (
            <span className="prime-radiant__viewer-count">+{viewers.length - 8}</span>
          )}
        </div>
      )}

      <DemerzelFaceOverlay size={100} />

      <ChatWidget
        selectedNode={selectedNode}
        onNavigateToPlanet={(planet) => {
          // Reuse the same planet navigation logic as the planet bar
          const fg = graphRef.current;
          if (!fg) return;
          const name = planet.toLowerCase();

          if (name === 'demerzel') {
            const cam = fg.camera() as THREE.PerspectiveCamera;
            const faceOffset = new THREE.Vector3(-50, -8, -40);
            faceOffset.applyQuaternion(cam.quaternion);
            const facePos = cam.position.clone().add(faceOffset);
            fg.cameraPosition(
              { x: facePos.x + 5, y: facePos.y + 2, z: facePos.z + 12 },
              { x: facePos.x, y: facePos.y, z: facePos.z },
              1200,
            );
            return;
          }

          solarFollowCameraRef.current = false;
          const group = fg.scene().getObjectByName('sun')?.parent;
          if (!group) return;
          const obj = group.getObjectByName(name);
          if (!obj) return;

          const pw = new THREE.Vector3();
          obj.getWorldPosition(pw);
          const worldScale = obj.getWorldScale(new THREE.Vector3()).x;
          const camDist = Math.max(worldScale * 0.35 * 2.5, 0.02);

          fg.cameraPosition(
            { x: pw.x + camDist * 0.3, y: pw.y + camDist * 0.4, z: pw.z + camDist * 0.85 },
            { x: pw.x, y: pw.y, z: pw.z },
            1500,
          );
          setTimeout(() => { solarFollowCameraRef.current = true; }, 1600);
        }}
        onNavigateToNode={(query) => {
          // Find and navigate to a governance node matching the query
          const fg = graphRef.current;
          if (!fg || !graphData) return;
          const q = query.toLowerCase();
          const node = graphData.nodes.find(n =>
            n.name.toLowerCase().includes(q) || n.id.toLowerCase().includes(q)
          );
          if (node) {
            const gn = (fg.graphData() as { nodes: GraphNode[] }).nodes.find(n => n.id === node.id);
            if (gn && gn.x !== undefined) {
              fg.cameraPosition(
                { x: gn.x, y: (gn.y ?? 0) + 20, z: (gn.z ?? 0) + 60 },
                { x: gn.x, y: gn.y ?? 0, z: gn.z ?? 0 },
                1200,
              );
              setSelectedNode(node);
              setActivePanel('detail');
            }
          }
        }}
        onLoadArcGIS={(layer) => {
          const fg = graphRef.current;
          if (!fg) return;
          const solarGroup = fg.scene().getObjectByName('sun')?.parent;
          if (!solarGroup) return;
          loadArcGISOverlay(solarGroup as THREE.Group, layer as 'imagery' | 'streets' | 'topo' | 'borders' | 'darkgray')
            .then(() => console.log(`[PrimeRadiant] ArcGIS ${layer} loaded on Earth`))
            .catch(err => console.warn(`[PrimeRadiant] ArcGIS ${layer} failed:`, err));
        }}
      />
      <TutorialOverlay />
      <IxqlCommandInput onCommand={handleIxqlCommand} />
      <IxqlDemoButton
        onCommand={handleIxqlCommand}
        onCameraReset={() => {
          const fg = graphRef.current;
          if (fg) fg.zoomToFit(800, 50);
        }}
      />

      {/* Demerzel visual critic overlay — shows self-improvement process */}
      {/* DemerzelCriticOverlay disabled — re-enable when driver is stable */}
      {/* <DemerzelCriticOverlay state={criticState} /> */}

      {/* Planet nav — left side collapsible menu */}
      <PlanetNav
        onNavigateToPlanet={(target) => {
          const fg = graphRef.current;
          if (!fg) return;

          if (target === 'demerzel-head') {
            const cam = fg.camera() as THREE.PerspectiveCamera;
            const faceOffset = new THREE.Vector3(-50, -8, -40);
            faceOffset.applyQuaternion(cam.quaternion);
            const facePos = cam.position.clone().add(faceOffset);
            fg.cameraPosition(
              { x: facePos.x + 5, y: facePos.y + 2, z: facePos.z + 12 },
              { x: facePos.x, y: facePos.y, z: facePos.z },
              1200,
            );
            solarFollowCameraRef.current = true;
            trackedPlanetRef.current = null;
            setTrackedPlanetName(null);
            return;
          }

          solarFollowCameraRef.current = false;
          const group = fg.scene().getObjectByName('sun')?.parent;
          if (!group) return;
          const obj = group.getObjectByName(target);
          if (!obj) return;
          const pw = new THREE.Vector3();
          obj.getWorldPosition(pw);
          // Get planet radius for close zoom — fall back to bounding sphere
          const mesh = obj as THREE.Mesh;
          mesh.geometry?.computeBoundingSphere();
          const planetRadius = mesh.geometry?.boundingSphere?.radius ?? 0.5;
          // Position camera at 3x planet radius — close enough to fill the view
          const zoomDist = planetRadius * 3.5;
          const cam = fg.camera() as THREE.PerspectiveCamera;
          const camP = cam.position.clone();
          const dir = pw.clone().sub(camP).normalize();
          const tgt = pw.clone().sub(dir.multiplyScalar(zoomDist));
          fg.cameraPosition(
            { x: tgt.x, y: tgt.y, z: tgt.z },
            { x: pw.x, y: pw.y, z: pw.z },
            1500,
          );
          // Don't resume follow — keep solar system frozen so planet stays visible
          // User clicks Reset View to resume normal mode
        }}
        onLoadArcGIS={(layer) => {
          const fg = graphRef.current;
          if (!fg) return;
          const solarGroup = fg.scene().getObjectByName('sun')?.parent;
          if (!solarGroup) return;
          loadArcGISOverlay(solarGroup as THREE.Group, layer as 'imagery' | 'streets' | 'topo' | 'borders' | 'darkgray')
            .then(() => console.log(`[PrimeRadiant] ArcGIS ${layer} loaded on Earth`))
            .catch(err => console.warn(`[PrimeRadiant] ArcGIS ${layer} failed:`, err));
        }}
        onRemoveArcGIS={(layer) => {
          const fg = graphRef.current;
          if (!fg) return;
          const solarGroup = fg.scene().getObjectByName('sun')?.parent;
          if (!solarGroup) return;
          removeArcGISOverlay(solarGroup as THREE.Group, layer);
        }}
        onResetView={() => {
          const fg = graphRef.current;
          if (!fg) return;
          solarFollowCameraRef.current = true;
          trackedPlanetRef.current = null;
          setTrackedPlanetName(null);
          fg.cameraPosition(
            { x: 0, y: 40, z: 120 },
            { x: 0, y: 0, z: 0 },
            1500,
          );
        }}
        onLaunchLunarLander={() => setActivePanel('lunar')}
        onLaunchGodot={() => {
          // Try WebSocket connection to Godot MCP
          try {
            const ws = new WebSocket('ws://localhost:6505');
            ws.onopen = () => {
              ws.send(JSON.stringify({
                jsonrpc: '2.0', id: 1, method: 'tools/call',
                params: { name: 'play_scene', arguments: {} },
              }));
              console.log('[PrimeRadiant] Godot 3D Engine launched via MCP');
              setTimeout(() => ws.close(), 2000);
            };
            ws.onerror = () => {
              alert('Godot is not running.\n\n1. Open the ga-godot project in Godot Editor\n2. Press F5 to run the Prime Radiant scene\n\nThe 3D Governance Engine will launch.');
            };
          } catch {
            alert('Godot MCP not available. Open Godot Editor and press F5.');
          }
        }}
      />

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

              // FREEZE solar system in place, fly camera to planet, then resume
              solarFollowCameraRef.current = false;

              const group = fg.scene().getObjectByName('sun')?.parent;
              if (!group) return;

              const obj = group.getObjectByName(p.target);
              if (!obj) return;

              // Snapshot planet world position NOW (before camera moves)
              const pw = new THREE.Vector3();
              obj.getWorldPosition(pw);

              // Fly camera toward planet — keep enough distance for zoom to work
              const cam = fg.camera() as THREE.PerspectiveCamera;
              const camP = cam.position.clone();
              const dir = pw.clone().sub(camP).normalize();
              const dist = camP.distanceTo(pw);
              const flyDist = Math.max(dist * 0.4, 2);
              const tgt = pw.clone().sub(dir.multiplyScalar(flyDist));

              fg.cameraPosition(
                { x: tgt.x, y: tgt.y, z: tgt.z },
                { x: pw.x, y: pw.y, z: pw.z },
                1500,
              );

              // Resume follow after animation completes
              setTimeout(() => {
                solarFollowCameraRef.current = true;
              }, 1600);
            }}
          >
            <span style={{ fontSize: '10px', lineHeight: 1 }}>{p.icon}</span>
            <span className="prime-radiant__planet-label">{p.name}</span>
          </button>
        ))}
        {/* Tracking indicator removed — tracking disabled */}
      </div>

      {/* Bottom drawer — icicle navigator + file viewer */}
      <IcicleDrawer graphData={graphData} />

      {/* Triage drop zone — bottom center, drag/paste anything for AI classification */}
      <TriageDropZone
        onNavigateToPanel={(panelId) => setActivePanel(panelId as PanelId)}
        onDispatch={(dispatch) => {
          // Store dispatched items in localStorage for target panels to pick up
          const key = `ixql-triage-dispatched-${dispatch.category}`;
          const existing = JSON.parse(localStorage.getItem(key) ?? '[]');
          existing.unshift({
            content: dispatch.content,
            summary: dispatch.summary,
            type: dispatch.type,
            timestamp: new Date().toISOString(),
          });
          localStorage.setItem(key, JSON.stringify(existing.slice(0, 50)));
        }}
      />

      </div>{/* end canvas-area */}

      {/* Icon rail — right edge (desktop/tablet) or bottom tab bar (phone) */}
      <IconRail
        activePanel={activePanel}
        onPanelToggle={handlePanelToggle}
        panelStatuses={panelHealthStatuses}
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

        {/* Panels with custom props — explicit conditionals */}
        {activePanel === 'detail' && showDetailPanel && (
          <DetailPanel
            node={selectedNode}
            graphIndex={graphIndex}
            onClose={handleCloseDetail}
            onNavigate={handleNavigate}
          />
        )}
        {activePanel === 'seldon' && graphData && (
          <SeldonDashboard
            open={true}
            onClose={() => setActivePanel(null)}
            graph={graphData}
            selectedNode={selectedNode}
            beliefs={beliefs}
          />
        )}
        {activePanel === 'algedonic' && (
          <AlgedonicPanel
            signals={algedonicSignals}
            onTriageAction={(action, signalName, source) => {
              pushToTriage('text', `[${source}] ${signalName.replace(/_/g, ' ')}: ${action}`, 'task');
            }}
          />
        )}
        {activePanel === 'godot' && (
          <GodotScene
            mode="panel"
            onNodeClick={(nodeId) => {
              const node = graphData?.nodes.find(n => n.id === nodeId);
              if (node) { setSelectedNode(node); setActivePanel('detail'); }
            }}
            onExpand={() => { setGodotFullscreen(true); setActivePanel(null); }}
          />
        )}
        {activePanel === 'gis' && (
          <GisPanel managers={gisManagers} />
        )}
        {activePanel === 'presence' && (
          <PresencePanel
            viewers={viewers}
            selfConnectionId={selfConnectionIdRef.current}
            agents={a2aAgents}
          />
        )}
        {/* Simple side panels — registry-backed lookup */}
        {activePanel && !['detail', 'seldon', 'algedonic', 'university', 'notebook', 'godot', 'gis', 'presence', 'lunar'].includes(activePanel) && (() => {
          const SIMPLE_PANELS: Record<string, React.FC> = {
            activity: ActivityPanel,
            backlog: BacklogPanel,
            agent: AgentPanel,
            llm: LLMStatus,
            cicd: CICDPanel,
            claude: ClaudeCodePanel,
            library: LibraryPanel,
            brainstorm: BrainstormPanel,
            tribunal: TheoryTribunal,
            faculty: SeldonFacultyPanel,
            'code-tribunal': CodeTribunal,
            inbox: AdminInbox,
            'ixql-gen': IxqlCodeGen,
          };
          const Component = SIMPLE_PANELS[activePanel];
          if (Component) return React.createElement(Component);
          // Dynamic panel from registry (IXQL CREATE PANEL)
          const reg = panelRegistry.get(activePanel);
          if (reg?.component) return React.createElement(reg.component);
          // Phase 6: Render IxqlGridPanel for IXQL CREATE PANEL KIND grid
          const gridSpec = gridPanelSpecsRef.current.get(activePanel);
          if (gridSpec) {
            const gridGraphCtx: GraphContext | undefined = graphRef.current
              ? {
                  nodes: (graphRef.current.graphData().nodes as Record<string, unknown>[]),
                  edges: (graphRef.current.graphData().links as Record<string, unknown>[]),
                }
              : undefined;
            return React.createElement(IxqlGridPanel, { spec: gridSpec, graphContext: gridGraphCtx });
          }
          // Phase 7: Render IxqlVizPanel for IXQL CREATE VIZ
          const vizSpec = vizSpecsRef.current.get(activePanel);
          if (vizSpec) {
            const vizGraphCtx: GraphContext | undefined = graphRef.current
              ? {
                  nodes: (graphRef.current.graphData().nodes as Record<string, unknown>[]),
                  edges: (graphRef.current.graphData().links as Record<string, unknown>[]),
                }
              : undefined;
            return React.createElement(IxqlVizPanel, { spec: vizSpec, graphContext: vizGraphCtx });
          }
          // Phase 8: Render IxqlFormPanel for IXQL CREATE FORM
          const formSpec = formSpecsRef.current.get(activePanel);
          if (formSpec) {
            return React.createElement(IxqlFormPanel, { spec: formSpec });
          }
          // Phase 2: Render DynamicPanel for IXQL-created panels
          const dynDef = dynamicPanelDefsRef.current.get(activePanel);
          if (dynDef) {
            const graphCtx: GraphContext | undefined = graphRef.current
              ? {
                  nodes: (graphRef.current.graphData().nodes as Record<string, unknown>[]),
                  edges: (graphRef.current.graphData().links as Record<string, unknown>[]),
                }
              : undefined;
            return React.createElement(DynamicPanel, { definition: dynDef, graphContext: graphCtx });
          }
          if (reg) return React.createElement('div', { style: { padding: '1rem', color: '#ccc' } }, `Panel: ${activePanel}`);
          return null;
        })()}
      </div>

      {/* CourseViewer renders as full-screen overlay, outside side panel */}
      {activePanel === 'university' && (
        <CourseViewer open={true} onClose={() => setActivePanel(null)} />
      )}

      {/* LiveNotebook renders as full-screen overlay, outside side panel */}
      {activePanel === 'notebook' && (
        <LiveNotebook open={true} onClose={() => setActivePanel(null)} />
      )}

      {/* Lunar Lander renders as full-screen overlay, outside side panel */}
      {activePanel === 'lunar' && (
        <LunarLander open={true} onClose={() => setActivePanel(null)} />
      )}

      {/* Godot 3D fullscreen overlay — independent of panel state */}
      {godotFullscreen && (
        <GodotScene
          mode="fullscreen"
          onClose={() => setGodotFullscreen(false)}
          onNodeClick={(nodeId) => {
            const node = graphData?.nodes.find(n => n.id === nodeId);
            if (node) {
              setGodotFullscreen(false);
              setSelectedNode(node);
              setActivePanel('detail');
            }
          }}
        />
      )}

    </div>
  );
};
