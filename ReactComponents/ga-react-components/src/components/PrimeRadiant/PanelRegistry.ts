// src/components/PrimeRadiant/PanelRegistry.ts
// Reactive panel registry — single source of truth for what panels exist.
// Built-in panels pre-register at module load; IXQL CREATE PANEL registers at runtime.

import React, { useSyncExternalStore } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export type BuiltInPanelId = 'activity' | 'backlog' | 'agent' | 'seldon' | 'llm' | 'detail' | 'algedonic' | 'university' | 'cicd' | 'claude' | 'notebook' | 'library' | 'godot' | 'gis' | 'lunar' | 'brainstorm' | 'presence';
export type PanelId = BuiltInPanelId | (string & {});

// ---------------------------------------------------------------------------
// Panel groups — ordered sections in the IconRail
// ---------------------------------------------------------------------------

export type PanelGroupId = 'governance' | 'agents' | 'knowledge' | 'viz' | 'ops';

export interface PanelGroupDef {
  id: PanelGroupId;
  label: string;
  order: number;
}

export const PANEL_GROUPS: PanelGroupDef[] = [
  { id: 'governance', label: 'Gov',       order: 0 },
  { id: 'agents',     label: 'Agents',    order: 1 },
  { id: 'knowledge',  label: 'Knowledge', order: 2 },
  { id: 'viz',        label: 'Viz',       order: 3 },
  { id: 'ops',        label: 'Ops',       order: 4 },
];

export interface PanelDefinition {
  id: string;
  label: string;
  icon: string;            // ICON_CATALOG key
  renderMode: 'side' | 'overlay';
  group?: PanelGroupId;    // rail grouping
  layout?: string;
  source?: string;
  showFields?: string[];
}

export interface PanelRegistration {
  definition: PanelDefinition;
  component?: React.FC<unknown>;   // for hardcoded/custom panels
}

// ---------------------------------------------------------------------------
// Icon catalog — SVG ReactNodes keyed by short name
// ---------------------------------------------------------------------------

const h = React.createElement;

const svg = (children: React.ReactNode) =>
  h('svg', { width: 20, height: 20, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round' }, children);

export const ICON_CATALOG: Record<string, React.ReactNode> = {
  activity: svg(h('polyline', { points: '22 12 18 12 15 21 9 3 6 12 2 12' })),
  backlog: svg([
    h('path', { key: 'p', d: 'M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2' }),
    h('rect', { key: 'r', x: 8, y: 2, width: 8, height: 4, rx: 1, ry: 1 }),
  ]),
  agent: svg([
    h('rect', { key: 'r1', x: 4, y: 4, width: 16, height: 16, rx: 2, ry: 2 }),
    h('rect', { key: 'r2', x: 9, y: 9, width: 6, height: 6 }),
    h('line', { key: 'l1', x1: 9, y1: 1, x2: 9, y2: 4 }),
    h('line', { key: 'l2', x1: 15, y1: 1, x2: 15, y2: 4 }),
    h('line', { key: 'l3', x1: 9, y1: 20, x2: 9, y2: 23 }),
    h('line', { key: 'l4', x1: 15, y1: 20, x2: 15, y2: 23 }),
    h('line', { key: 'l5', x1: 20, y1: 9, x2: 23, y2: 9 }),
    h('line', { key: 'l6', x1: 20, y1: 14, x2: 23, y2: 14 }),
    h('line', { key: 'l7', x1: 1, y1: 9, x2: 4, y2: 9 }),
    h('line', { key: 'l8', x1: 1, y1: 14, x2: 4, y2: 14 }),
  ]),
  seldon: svg([
    h('line', { key: 'l1', x1: 18, y1: 20, x2: 18, y2: 10 }),
    h('line', { key: 'l2', x1: 12, y1: 20, x2: 12, y2: 4 }),
    h('line', { key: 'l3', x1: 6, y1: 20, x2: 6, y2: 14 }),
  ]),
  llm: svg([
    h('path', { key: 'p', d: 'M12 2a7 7 0 0 1 7 7c0 2.5-1.3 4.7-3.2 6H8.2C6.3 13.7 5 11.5 5 9a7 7 0 0 1 7-7z' }),
    h('line', { key: 'l1', x1: 9, y1: 17, x2: 15, y2: 17 }),
    h('line', { key: 'l2', x1: 10, y1: 20, x2: 14, y2: 20 }),
  ]),
  detail: svg([
    h('path', { key: 'p', d: 'M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z' }),
    h('polyline', { key: 'pl', points: '14 2 14 8 20 8' }),
    h('line', { key: 'l1', x1: 16, y1: 13, x2: 8, y2: 13 }),
    h('line', { key: 'l2', x1: 16, y1: 17, x2: 8, y2: 17 }),
  ]),
  algedonic: svg([
    h('path', { key: 'p1', d: 'M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z' }),
    h('path', { key: 'p2', d: 'M3 12h4l3-9 4 18 3-9h4', opacity: 0.6 }),
  ]),
  cicd: svg([
    h('circle', { key: 'c', cx: 12, cy: 12, r: 3 }),
    h('path', { key: 'p', d: 'M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z' }),
  ]),
  university: svg([
    h('path', { key: 'p1', d: 'M22 10v6M2 10l10-5 10 5-10 5z' }),
    h('path', { key: 'p2', d: 'M6 12v5c0 1.1 2.7 3 6 3s6-1.9 6-3v-5' }),
  ]),
  library: svg([
    h('path', { key: 'p1', d: 'M4 19.5A2.5 2.5 0 0 1 6.5 17H20' }),
    h('path', { key: 'p2', d: 'M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z' }),
    h('line', { key: 'l1', x1: 8, y1: 7, x2: 16, y2: 7 }),
    h('line', { key: 'l2', x1: 8, y1: 11, x2: 13, y2: 11 }),
  ]),
  claude: svg([
    h('polyline', { key: 'pl', points: '4 17 10 11 4 5' }),
    h('line', { key: 'l', x1: 12, y1: 19, x2: 20, y2: 19 }),
  ]),
  notebook: svg([
    h('path', { key: 'p1', d: 'M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z' }),
    h('path', { key: 'p2', d: 'M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z' }),
  ]),
  godot: svg([
    h('path', { key: 'p1', d: 'M12 2L2 7l10 5 10-5-10-5z' }),
    h('path', { key: 'p2', d: 'M2 17l10 5 10-5' }),
    h('path', { key: 'p3', d: 'M2 12l10 5 10-5' }),
  ]),
  gis: svg([
    h('circle', { key: 'c', cx: 12, cy: 12, r: 10 }),
    h('path', { key: 'p1', d: 'M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z' }),
    h('line', { key: 'l1', x1: 2, y1: 12, x2: 22, y2: 12 }),
  ]),
  brainstorm: svg([
    h('path', { key: 'p1', d: 'M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707' }),
    h('path', { key: 'p2', d: 'M8.464 15.536a5 5 0 1 1 7.072 0l-.548.547A3.374 3.374 0 0 0 14 18.469V19a2 2 0 1 1-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z' }),
  ]),
  lunar: svg([
    h('path', { key: 'p1', d: 'M12 3a6 6 0 0 0 0 12 9 9 0 0 0 9-9' }),
    h('circle', { key: 'c1', cx: 9, cy: 8, r: 1, fill: 'currentColor', stroke: 'none' }),
    h('circle', { key: 'c2', cx: 14, cy: 11, r: 0.7, fill: 'currentColor', stroke: 'none' }),
    h('circle', { key: 'c3', cx: 11, cy: 13, r: 0.5, fill: 'currentColor', stroke: 'none' }),
  ]),
  presence: svg([
    h('path', { key: 'p1', d: 'M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2' }),
    h('circle', { key: 'c1', cx: 9, cy: 7, r: 4 }),
    h('path', { key: 'p2', d: 'M23 21v-2a4 4 0 0 0-3-3.87' }),
    h('path', { key: 'p3', d: 'M16 3.13a4 4 0 0 1 0 7.75' }),
  ]),
};

// ---------------------------------------------------------------------------
// Registry singleton
// ---------------------------------------------------------------------------

type Listener = () => void;

class PanelRegistryStore {
  private panels = new Map<string, PanelRegistration>();
  private listeners = new Set<Listener>();
  private snapshot: PanelRegistration[] = [];
  private dirty = true;

  register(reg: PanelRegistration): void {
    this.panels.set(reg.definition.id, reg);
    this.dirty = true;
    this.notify();
  }

  unregister(id: string): void {
    if (this.panels.delete(id)) {
      this.dirty = true;
      this.notify();
    }
  }

  get(id: string): PanelRegistration | undefined {
    return this.panels.get(id);
  }

  getAll(): PanelRegistration[] {
    if (this.dirty) {
      this.snapshot = Array.from(this.panels.values());
      this.dirty = false;
    }
    return this.snapshot;
  }

  subscribe(listener: Listener): () => void {
    this.listeners.add(listener);
    return () => { this.listeners.delete(listener); };
  }

  private notify(): void {
    for (const fn of this.listeners) fn();
  }
}

export const panelRegistry = new PanelRegistryStore();

// ---------------------------------------------------------------------------
// React hook — re-renders on registry changes
// ---------------------------------------------------------------------------

export function usePanelRegistry(): PanelRegistration[] {
  return useSyncExternalStore(
    (cb) => panelRegistry.subscribe(cb),
    () => panelRegistry.getAll(),
  );
}

// ---------------------------------------------------------------------------
// Pre-register built-in panels (lazy imports not needed — they're already
// imported by ForceRadiant, so we just register metadata here)
// ---------------------------------------------------------------------------

const BUILTIN_PANELS: PanelDefinition[] = [
  // ── Governance ──
  { id: 'activity',   label: 'Activity',      icon: 'activity',   renderMode: 'side',    group: 'governance' },
  { id: 'algedonic',  label: 'Signals',        icon: 'algedonic',  renderMode: 'side',    group: 'governance' },
  { id: 'detail',     label: 'Detail',         icon: 'detail',     renderMode: 'side',    group: 'governance' },
  { id: 'backlog',    label: 'Backlog',        icon: 'backlog',    renderMode: 'side',    group: 'governance' },
  // ── Agents ──
  { id: 'agent',      label: 'Agents',         icon: 'agent',      renderMode: 'side',    group: 'agents' },
  { id: 'seldon',     label: 'Seldon',         icon: 'seldon',     renderMode: 'side',    group: 'agents' },
  { id: 'llm',        label: 'LLM',            icon: 'llm',        renderMode: 'side',    group: 'agents' },
  { id: 'claude',     label: 'Claude Code',    icon: 'claude',     renderMode: 'side',    group: 'agents' },
  { id: 'presence',   label: 'Presence',        icon: 'presence',   renderMode: 'side',    group: 'agents' },
  // ── Knowledge ──
  { id: 'university', label: 'University',     icon: 'university', renderMode: 'overlay', group: 'knowledge' },
  { id: 'library',    label: 'Library',        icon: 'library',    renderMode: 'side',    group: 'knowledge' },
  { id: 'notebook',   label: 'Live Notebook',  icon: 'notebook',   renderMode: 'overlay', group: 'knowledge' },
  { id: 'brainstorm', label: "What's Next?",    icon: 'brainstorm', renderMode: 'side',    group: 'knowledge' },
  // ── Visualization ──
  { id: 'godot',      label: 'Godot 3D',       icon: 'godot',      renderMode: 'side',    group: 'viz' },
  { id: 'gis',        label: 'GIS',            icon: 'gis',        renderMode: 'side',    group: 'viz' },
  { id: 'lunar',      label: 'Lunar Lander',   icon: 'lunar',      renderMode: 'overlay', group: 'viz' },
  // ── Ops ──
  { id: 'cicd',       label: 'CI/CD',          icon: 'cicd',       renderMode: 'side',    group: 'ops' },
];

for (const def of BUILTIN_PANELS) {
  panelRegistry.register({ definition: def });
}
