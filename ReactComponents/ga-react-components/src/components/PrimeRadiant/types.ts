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
  | 'lolli';                    // red pulsing (dead references)

// ---------------------------------------------------------------------------
// Health status
// ---------------------------------------------------------------------------
export type HealthStatus = 'healthy' | 'watch' | 'freeze';

export interface HealthMetrics {
  resilienceScore: number;       // 0.0 – 1.0
  lolliCount: number;            // dead references
  ergolCount: number;            // live executed bindings
  markovPrediction?: number[];   // probability distribution over states
  staleness?: number;            // 0.0 – 1.0
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
  children?: string[];           // child node IDs
  metadata?: Record<string, unknown>;
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
// Holographic palette — distinct per type, warm→cool hierarchy gradient
// Cross-model validated (Claude + GPT-4o), tuned for additive blending + bloom on black
export const NODE_COLORS: Record<GovernanceNodeType, string> = {
  constitution: '#FF6B35',   // warm coral — the burning core
  department: '#FFC300',     // sunburst gold — organizational presence
  policy: '#FFD700',         // bright gold — guiding light
  persona: '#33FF88',        // neon mint — living agents
  pipeline: '#00FFAA',       // spring green — data flow
  schema: '#00CED1',         // aqua cyan — structural validation
  test: '#4A90D9',           // ocean blue — verification
  ixql: '#9B72FF',           // electric violet — deep query
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
  healthy: '#FFD700',
  watch: '#FFA500',
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
