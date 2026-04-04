// src/components/PrimeRadiant/types.ts
// TypeScript types for the Prime Radiant governance visualization engine

import * as THREE from 'three';

// ---------------------------------------------------------------------------
// Node types — each maps to a distinct 3D shape
// ---------------------------------------------------------------------------
export type GovernanceNodeType =
  | 'constitution'
  | 'policy'
  | 'persona'
  | 'pipeline'
  | 'department'
  | 'schema'
  | 'test'
  | 'ixql';

// ---------------------------------------------------------------------------
// Edge types — relationship rendering styles
// ---------------------------------------------------------------------------
export type GovernanceEdgeType =
  | 'constitutional-hierarchy'  // thick golden beams
  | 'policy-persona'            // thin colored lines
  | 'pipeline-flow'             // animated particle streams
  | 'cross-repo'                // dashed arcs
  | 'contains'                  // fractal dive: parent → child (sub-graph recursion)
  | 'lolli';                    // red pulsing (dead references)

// ---------------------------------------------------------------------------
// Health status
// ---------------------------------------------------------------------------
export type HealthStatus = 'healthy' | 'watch' | 'freeze';
export type GovernanceHealthStatus = 'error' | 'warning' | 'healthy' | 'unknown' | 'contradictory';

export interface HealthMetrics {
  resilienceScore: number;       // 0.0 – 1.0
  lolliCount: number;            // dead references
  ergolCount: number;            // live executed bindings
  markovPrediction?: number[];   // probability distribution over states
  staleness?: number;            // 0.0 – 1.0
}

// ---------------------------------------------------------------------------
// File tree (drill-down in DetailPanel)
// ---------------------------------------------------------------------------
export interface FileTreeNode {
  name: string;
  path: string;
  type: 'file' | 'directory';
  children?: FileTreeNode[];
  extension?: string;  // e.g. 'yaml', 'md', 'json'
}

// ---------------------------------------------------------------------------
// Graph data structures
// ---------------------------------------------------------------------------
export interface GovernanceNode {
  id: string;
  name: string;
  type: GovernanceNodeType;
  description: string;
  color: string;
  repo?: string;                 // ix | tars | ga | demerzel
  domain?: string;               // grouping domain
  version?: string;
  health?: HealthMetrics;
  healthStatus?: GovernanceHealthStatus;
  filePath?: string;             // relative path to governance file
  children?: string[];           // child node IDs (dive targets for fractal zoom)
  parentIds?: string[];          // multi-parent: a node can belong to several parents
  scale?: number;                // LOD depth — 0=root, 1=constitution-articles, 2=clauses...
  metadata?: Record<string, unknown>;
  fileTree?: FileTreeNode[];
}

export interface GovernanceEdge {
  id: string;
  source: string;                // source node ID
  target: string;                // target node ID
  type: GovernanceEdgeType;
  label?: string;
  weight?: number;               // 0.0 – 1.0, affects visual thickness
}

export interface GovernanceGraph {
  nodes: GovernanceNode[];
  edges: GovernanceEdge[];
  globalHealth: HealthMetrics;
  timestamp: string;
}

// ---------------------------------------------------------------------------
// 3D scene node — runtime representation with position
// ---------------------------------------------------------------------------
export interface SceneNode {
  id: string;
  data: GovernanceNode;
  mesh: THREE.Object3D;
  position: THREE.Vector3;
  velocity: THREE.Vector3;
  fixed?: boolean;
}

export interface SceneEdge {
  id: string;
  data: GovernanceEdge;
  line: THREE.Object3D;
  sourceNode: SceneNode;
  targetNode: SceneNode;
  particleOffset?: number;       // for animated particle streams
}

// ---------------------------------------------------------------------------
// Interaction state
// ---------------------------------------------------------------------------
export interface SelectionState {
  selectedNodeId: string | null;
  hoveredNodeId: string | null;
  connectedNodeIds: Set<string>;
  connectedEdgeIds: Set<string>;
}

// ---------------------------------------------------------------------------
// Component props
// ---------------------------------------------------------------------------
export interface PrimeRadiantProps {
  data?: GovernanceGraph;
  width?: number | string;
  height?: number | string;
  onNodeSelect?: (node: GovernanceNode | null) => void;
  showDetailPanel?: boolean;
  showSearchBar?: boolean;
  showTimeSlider?: boolean;
  className?: string;
}

// ---------------------------------------------------------------------------
// Node type visual config
// ---------------------------------------------------------------------------
export interface NodeTypeConfig {
  type: GovernanceNodeType;
  geometryFactory: () => THREE.BufferGeometry;
  defaultColor: string;
  scale: number;
  emissiveIntensity: number;
  label: string;
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------
// Semantic governance health colors — the PRIMARY visual signal
// Maps governance health state to color, cross-model validated for additive blending + bloom on black
export const HEALTH_STATUS_COLORS: Record<GovernanceHealthStatus, string> = {
  error: '#FF4444',          // red — something broken
  warning: '#FFB300',        // amber — needs attention
  healthy: '#33CC66',        // green — going well
  unknown: '#888888',        // gray — no data
  contradictory: '#FF44FF',  // magenta — conflicting signals
};

// Subtle type palette — used for shape/icon differentiation only, NOT the primary visual signal
export const NODE_COLORS: Record<GovernanceNodeType, string> = {
  constitution: '#8A8A9A',   // warm gray — structural anchor
  department: '#7A7A8A',     // cool gray — organizational
  policy: '#9090A0',         // light gray — governance
  persona: '#7A8A7A',        // sage gray — agents
  pipeline: '#7A8A90',       // teal gray — data flow
  schema: '#808090',         // blue gray — structural
  test: '#8A8090',           // purple gray — verification
  ixql: '#908080',           // rose gray — query
};

export const NODE_SCALES: Record<GovernanceNodeType, number> = {
  constitution: 4.0,
  policy: 1.6,
  persona: 1.6,
  pipeline: 1.4,
  department: 3.0,
  schema: 1.0,
  test: 1.0,
  ixql: 0.9,
};

export const HEALTH_COLORS: Record<HealthStatus, string> = {
  healthy: '#33CC66',
  watch: '#FFB300',
  freeze: '#FF4444',
};

export const BACKGROUND_COLOR = 0x000010;
export const FOG_COLOR = 0x000010;
export const FOG_NEAR = 80;
export const FOG_FAR = 300;
export const BLOOM_STRENGTH = 0.7;
export const BLOOM_RADIUS = 0.35;
export const BLOOM_THRESHOLD = 0.4;

// Spherical shell radii — hierarchy maps to depth within the sphere
export const SPHERE_RADIUS_CORE = 6;        // constitution
export const SPHERE_RADIUS_MID = 14;        // policy, persona, pipeline, department
export const SPHERE_RADIUS_OUTER = 22;      // schema, test, ixql

// Particles per node cluster (scaled by NODE_SCALES)
export const NODE_PARTICLE_BASE = 120;

// Fresnel sphere
export const CONTAINMENT_SPHERE_RADIUS = 30;
