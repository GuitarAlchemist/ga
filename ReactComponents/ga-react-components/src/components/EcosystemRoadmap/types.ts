import * as THREE from 'three';

export type Domain = 'core' | 'gov' | 'music' | 'sci' | 'human' | 'infra' | 'meta';
export type ViewMode = 'icicle' | 'disk' | 'ball';
export type RendererType = 'webgpu' | 'webgl';
export type NodeStatus = 'active' | 'horizon' | 'in-progress' | 'new';

export const DOMAIN_COLORS: Record<Domain, string> = {
  core: '#f0883e',
  gov: '#4cb050',
  music: '#7289da',
  sci: '#e06c75',
  human: '#c678dd',
  infra: '#56b6c2',
  meta: '#e5c07b',
};

export const DOMAIN_LABELS: Record<Domain, string> = {
  core: 'Core',
  gov: 'Governance',
  music: 'Music',
  sci: 'Science',
  human: 'Human',
  infra: 'Infrastructure',
  meta: 'Meta',
};

export interface RoadmapNode {
  id: string;
  name: string;
  color: string;
  domain: Domain;
  description: string;
  status?: NodeStatus;
  sub?: string;
  url?: string;
  grammarUrl?: string;
  children?: RoadmapNode[];
  _depth?: number;
}

export interface StatItem {
  label: string;
  value: string;
  url: string;
}

// Shared view interface for Tasks 7-9
export interface ViewCallbacks {
  onNodeClick: (node: RoadmapNode) => void;
  onNodeHover: (node: RoadmapNode | null) => void;
}

export interface RoadmapView {
  update: (selected: RoadmapNode | null, zoom: number) => void;
  handleClick: (raycaster: THREE.Raycaster) => void;
  handleHover: (raycaster: THREE.Raycaster) => void;
  dispose: () => void;
}

export const LOD_THRESHOLDS = {
  LABELS_DEPTH_01: 0.5,
  LABELS_DEPTH_02: 1.5,
} as const;
