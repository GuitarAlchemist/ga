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
export const NODE_COLORS: Record<GovernanceNodeType, string> = {
  constitution: '#FFD700',
  policy: '#4CB050',
  persona: '#C678DD',
  pipeline: '#58A6FF',
  department: '#E5C07B',
  schema: '#7289DA',
  test: '#E06C75',
  ixql: '#F0883E',
};

export const NODE_SCALES: Record<GovernanceNodeType, number> = {
  constitution: 2.0,
  policy: 1.2,
  persona: 1.2,
  pipeline: 1.2,
  department: 1.8,
  schema: 0.8,
  test: 0.8,
  ixql: 0.8,
};

export const HEALTH_COLORS: Record<HealthStatus, string> = {
  healthy: '#4CB050',
  watch: '#E5C07B',
  freeze: '#E06C75',
};

export const BACKGROUND_COLOR = 0x0d1117;
export const FOG_COLOR = 0x0d1117;
export const FOG_NEAR = 50;
export const FOG_FAR = 200;
export const BLOOM_STRENGTH = 0.8;
export const BLOOM_RADIUS = 0.4;
export const BLOOM_THRESHOLD = 0.3;
