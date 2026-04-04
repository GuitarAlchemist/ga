// src/components/PrimeRadiant/FractalDive.ts
// Fractal governance-graph dive state machine.
//
// Governance is self-similar: a constitution contains articles, each article
// contains clauses, each clause contains tests. This module tracks the user's
// current recursion depth and the breadcrumb path, and exposes helpers for
// filtering nodes/edges to the active scale.
//
// No rendering logic here — rendering and camera flight stay in ForceRadiant.
// This is the pure state layer so the dive mechanic is testable and portable.

import type { GovernanceNode, GovernanceEdge } from './types';

/** A single rung in the dive ladder — the node we dove into to reach this scale. */
export interface DiveCrumb {
  nodeId: string;
  nodeName: string;
  scale: number;
}

/** Snapshot of the current dive state — passed to renderers + UI. */
export interface DiveState {
  /** Current LOD depth — 0 = root scale (all top-level artifacts visible). */
  currentScale: number;
  /** Path from root to current scale. Empty at scale 0. */
  path: DiveCrumb[];
  /** ID of the parent node at the current scale (tail of path, or null at root). */
  activeParentId: string | null;
}

/** Event emitted when the dive state changes — ForceRadiant listens + flies camera. */
export type DiveListener = (
  next: DiveState,
  prev: DiveState,
  kind: 'dive' | 'surface' | 'reset',
) => void;

/**
 * Dive state machine. Wrap in a React ref to give the whole component tree
 * read access without prop-drilling. Mutations emit events to all listeners.
 */
export class FractalDiveManager {
  private state: DiveState = {
    currentScale: 0,
    path: [],
    activeParentId: null,
  };
  private listeners: Set<DiveListener> = new Set();

  getState(): DiveState {
    return this.state;
  }

  subscribe(listener: DiveListener): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  /** Dive into a node — push a crumb, increment scale. No-op if node has no children. */
  dive(node: GovernanceNode): boolean {
    if (!node.children || node.children.length === 0) return false;
    const prev = this.state;
    const next: DiveState = {
      currentScale: prev.currentScale + 1,
      path: [...prev.path, { nodeId: node.id, nodeName: node.name, scale: prev.currentScale + 1 }],
      activeParentId: node.id,
    };
    this.state = next;
    this.emit(next, prev, 'dive');
    return true;
  }

  /** Surface one level — pop the last crumb. */
  surface(): boolean {
    if (this.state.path.length === 0) return false;
    const prev = this.state;
    const newPath = prev.path.slice(0, -1);
    const next: DiveState = {
      currentScale: prev.currentScale - 1,
      path: newPath,
      activeParentId: newPath.length > 0 ? newPath[newPath.length - 1].nodeId : null,
    };
    this.state = next;
    this.emit(next, prev, 'surface');
    return true;
  }

  /** Jump to a specific crumb in the breadcrumb trail (clicking a breadcrumb item). */
  jumpTo(crumbIndex: number): boolean {
    if (crumbIndex < -1 || crumbIndex >= this.state.path.length) return false;
    const prev = this.state;
    // crumbIndex === -1 means jump to root
    const newPath = crumbIndex === -1 ? [] : prev.path.slice(0, crumbIndex + 1);
    const next: DiveState = {
      currentScale: newPath.length,
      path: newPath,
      activeParentId: newPath.length > 0 ? newPath[newPath.length - 1].nodeId : null,
    };
    this.state = next;
    this.emit(next, prev, newPath.length === 0 ? 'reset' : 'surface');
    return true;
  }

  /** Reset to root scale — for navigation back to the full graph. */
  reset(): void {
    if (this.state.currentScale === 0) return;
    const prev = this.state;
    this.state = { currentScale: 0, path: [], activeParentId: null };
    this.emit(this.state, prev, 'reset');
  }

  private emit(next: DiveState, prev: DiveState, kind: 'dive' | 'surface' | 'reset'): void {
    for (const l of this.listeners) l(next, prev, kind);
  }
}

// ---------------------------------------------------------------------------
// Filter helpers — decide which nodes/edges are visible at a given dive state
// ---------------------------------------------------------------------------

/**
 * Return the subset of nodes visible at the current dive state.
 *
 * At scale 0: only nodes with `scale === 0` (or undefined, treated as 0).
 * At scale N > 0: only nodes whose parentIds include the activeParentId,
 *                 with scale === N. This lets a constitution expand to its
 *                 articles, then articles to clauses, etc.
 */
export function filterNodesForScale(
  nodes: GovernanceNode[],
  dive: DiveState,
): GovernanceNode[] {
  if (dive.currentScale === 0) {
    return nodes.filter((n) => (n.scale ?? 0) === 0);
  }
  const pid = dive.activeParentId;
  if (!pid) return [];
  return nodes.filter(
    (n) => (n.scale ?? 0) === dive.currentScale && (n.parentIds?.includes(pid) ?? false),
  );
}

/** Filter edges to those connecting two currently-visible nodes. */
export function filterEdgesForScale(
  edges: GovernanceEdge[],
  visibleNodes: GovernanceNode[],
): GovernanceEdge[] {
  const visibleIds = new Set(visibleNodes.map((n) => n.id));
  return edges.filter((e) => visibleIds.has(e.source) && visibleIds.has(e.target));
}

/** True if a node is a dive target (has children at the next scale down). */
export function isDiveable(node: GovernanceNode): boolean {
  return (node.children?.length ?? 0) > 0;
}

/**
 * Build a human-readable breadcrumb string from a dive state.
 * "Asimov › Article 0 › Clause 3"
 */
export function breadcrumbText(dive: DiveState, rootLabel = 'Root'): string {
  if (dive.path.length === 0) return rootLabel;
  return [rootLabel, ...dive.path.map((c) => c.nodeName)].join(' › ');
}
