// src/components/EcosystemRoadmap/atoms.ts

import { atom } from 'jotai';
import type { RoadmapNode, ViewMode, RendererType } from './types';
import { ROADMAP_TREE } from './roadmapData';

// Default expansion: root + every depth-1 child so the tree reveals
// the top-level structure (Constitution, Repos, Personas, ...) on
// first render instead of a single collapsed bullet.
const DEFAULT_EXPANDED: string[] = [
  ROADMAP_TREE.id,
  ...(ROADMAP_TREE.children?.map((c) => c.id) ?? []),
];

export const selectedNodeAtom = atom<RoadmapNode | null>(null);
export const viewModeAtom = atom<ViewMode>('disk');
export const zoomLevelAtom = atom<number>(1.0);
export const expandedTreeNodesAtom = atom<string[]>(DEFAULT_EXPANDED);
export const searchFilterAtom = atom<string>('');
export const rendererTypeAtom = atom<RendererType>('webgpu');
export const panelWidthAtom = atom<number>(300);
// Mobile-only: tree is in a drawer, default closed. Desktop ignores this.
export const navDrawerOpenAtom = atom<boolean>(false);
