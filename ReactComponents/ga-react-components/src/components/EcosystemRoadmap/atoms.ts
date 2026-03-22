// src/components/EcosystemRoadmap/atoms.ts

import { atom } from 'jotai';
import type { RoadmapNode, ViewMode, RendererType } from './types';

export const selectedNodeAtom = atom<RoadmapNode | null>(null);
export const viewModeAtom = atom<ViewMode>('disk');
export const zoomLevelAtom = atom<number>(1.0);
export const expandedTreeNodesAtom = atom<string[]>([]); // string[] for MUI TreeView compat
export const searchFilterAtom = atom<string>('');
export const rendererTypeAtom = atom<RendererType>('webgpu');
export const panelWidthAtom = atom<number>(280);
