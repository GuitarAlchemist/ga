// src/components/PrimeRadiant/FlowMatrix.ts
// Flow Matrix — per-frame audit of authority transfer between jurisdictions.
//
// Governance meaning: when two jurisdictions' spatial volumes overlap,
// authority is being exchanged between them (treaty, federation, contest,
// or breach). The Flow Matrix quantifies this per pair per frame so that
// constitutional articles can be tested against actual behavior.
//
// Design rationale: this is the CPU-side companion to LPR's "Beam Handoff"
// concept. Shader-side atomic counters would be more precise but require
// WebGPU compute/storage buffers. CPU-side AABB overlap is deterministic,
// testable without a GPU, and produces the same governance signal at a
// tenth the engineering cost.
//
// The produced data — a (typeA, typeB) → overlap volume matrix, plus a
// list of "contested nodes" (nodes sitting inside 2+ jurisdictions) — is
// sufficient to answer questions like:
//   - Which jurisdictions are currently trading authority?
//   - Did Article 7's required federal→state flow actually occur?
//   - Which nodes are operating under multiple authorities simultaneously?

import type { GovernanceNode } from './types';

// ── Types ──

interface GraphNode extends GovernanceNode {
  x?: number;
  y?: number;
  z?: number;
}

interface AABB {
  min: [number, number, number];
  max: [number, number, number];
}

interface JurisdictionAABB {
  type: string;
  aabb: AABB;
  nodeIds: Set<string>;
}

/** A single handoff event emitted for one (typeA, typeB) overlap this frame. */
export interface HandoffEvent {
  /** Alphabetically-ordered pair — (A, B) is the same flow event as (B, A) */
  fromType: string;
  toType: string;
  /** Volume of the AABB intersection (world units^3) */
  overlapVolume: number;
  /**
   * Node IDs that sit physically inside BOTH jurisdictions' AABBs.
   * These are operating under multiple authorities simultaneously.
   */
  contestedNodeIds: string[];
}

/** A snapshot of all handoff events for one frame. */
export interface FlowMatrixFrame {
  /** Monotonically increasing frame counter */
  frameId: number;
  /** performance.now() timestamp */
  tMs: number;
  /** All pairwise overlap events (empty if no jurisdictions overlap) */
  events: HandoffEvent[];
  /** Sum of overlap volumes across all pairs — "total authority in contest" */
  totalOverlapVolume: number;
  /** Number of contested nodes (union across all pairs) */
  contestedNodeCount: number;
}

export interface FlowMatrixHandle {
  /** Compute one frame; returns the snapshot and also pushes it to subscribers */
  tick(nodes: GraphNode[]): FlowMatrixFrame | null;
  /** Subscribe to per-frame snapshots (for audit log panels, etc.) */
  subscribe(fn: (frame: FlowMatrixFrame) => void): () => void;
  /** Latest snapshot (null until first tick) */
  readonly latest: FlowMatrixFrame | null;
  dispose(): void;
}

// ── AABB helpers ──

const AABB_PADDING = 6; // matches JurisdictionVolumetrics

function computeAABBs(nodes: GraphNode[]): JurisdictionAABB[] {
  const byType = new Map<string, { nodeIds: Set<string>; minX: number; minY: number; minZ: number; maxX: number; maxY: number; maxZ: number }>();

  for (const n of nodes) {
    if (n.x === undefined) continue;
    const x = n.x, y = n.y ?? 0, z = n.z ?? 0;
    let entry = byType.get(n.type);
    if (!entry) {
      entry = { nodeIds: new Set(), minX: Infinity, minY: Infinity, minZ: Infinity, maxX: -Infinity, maxY: -Infinity, maxZ: -Infinity };
      byType.set(n.type, entry);
    }
    entry.nodeIds.add(n.id);
    if (x < entry.minX) entry.minX = x;
    if (y < entry.minY) entry.minY = y;
    if (z < entry.minZ) entry.minZ = z;
    if (x > entry.maxX) entry.maxX = x;
    if (y > entry.maxY) entry.maxY = y;
    if (z > entry.maxZ) entry.maxZ = z;
  }

  const result: JurisdictionAABB[] = [];
  for (const [type, e] of byType) {
    if (e.nodeIds.size < 3) continue; // same threshold as LPR proxy creation
    result.push({
      type,
      nodeIds: e.nodeIds,
      aabb: {
        min: [e.minX - AABB_PADDING, e.minY - AABB_PADDING, e.minZ - AABB_PADDING],
        max: [e.maxX + AABB_PADDING, e.maxY + AABB_PADDING, e.maxZ + AABB_PADDING],
      },
    });
  }
  return result;
}

function overlapVolume(a: AABB, b: AABB): number {
  const dx = Math.max(0, Math.min(a.max[0], b.max[0]) - Math.max(a.min[0], b.min[0]));
  const dy = Math.max(0, Math.min(a.max[1], b.max[1]) - Math.max(a.min[1], b.min[1]));
  const dz = Math.max(0, Math.min(a.max[2], b.max[2]) - Math.max(a.min[2], b.min[2]));
  return dx * dy * dz;
}

function nodeInAABB(node: GraphNode, aabb: AABB): boolean {
  if (node.x === undefined) return false;
  const x = node.x, y = node.y ?? 0, z = node.z ?? 0;
  return x >= aabb.min[0] && x <= aabb.max[0]
    && y >= aabb.min[1] && y <= aabb.max[1]
    && z >= aabb.min[2] && z <= aabb.max[2];
}

// ── Factory ──

export function createFlowMatrix(): FlowMatrixHandle {
  let frameId = 0;
  let latest: FlowMatrixFrame | null = null;
  const subscribers = new Set<(f: FlowMatrixFrame) => void>();

  return {
    get latest() { return latest; },

    tick(nodes: GraphNode[]): FlowMatrixFrame | null {
      const jurisdictions = computeAABBs(nodes);
      if (jurisdictions.length < 2) return null;

      const events: HandoffEvent[] = [];
      const contestedUnion = new Set<string>();
      let totalVolume = 0;

      // Pairwise overlap check (N^2 over ~7 jurisdiction types → 21 pairs)
      for (let i = 0; i < jurisdictions.length; i++) {
        for (let j = i + 1; j < jurisdictions.length; j++) {
          const a = jurisdictions[i];
          const b = jurisdictions[j];
          const vol = overlapVolume(a.aabb, b.aabb);
          if (vol <= 0) continue;

          // Find nodes contested between these two — a node from either
          // jurisdiction sitting inside the OTHER's AABB.
          const contested: string[] = [];
          for (const nodeId of a.nodeIds) {
            const node = nodes.find(n => n.id === nodeId);
            if (node && nodeInAABB(node, b.aabb)) {
              contested.push(nodeId);
              contestedUnion.add(nodeId);
            }
          }
          for (const nodeId of b.nodeIds) {
            const node = nodes.find(n => n.id === nodeId);
            if (node && nodeInAABB(node, a.aabb)) {
              contested.push(nodeId);
              contestedUnion.add(nodeId);
            }
          }

          // Alphabetical ordering — (A,B) == (B,A) for audit aggregation
          const [fromType, toType] = a.type < b.type ? [a.type, b.type] : [b.type, a.type];
          events.push({ fromType, toType, overlapVolume: vol, contestedNodeIds: contested });
          totalVolume += vol;
        }
      }

      const frame: FlowMatrixFrame = {
        frameId: frameId++,
        tMs: performance.now(),
        events,
        totalOverlapVolume: totalVolume,
        contestedNodeCount: contestedUnion.size,
      };
      latest = frame;
      for (const fn of subscribers) {
        try { fn(frame); } catch (e) { console.warn('[FlowMatrix] subscriber error:', e); }
      }
      return frame;
    },

    subscribe(fn) {
      subscribers.add(fn);
      return () => { subscribers.delete(fn); };
    },

    dispose() {
      subscribers.clear();
      latest = null;
    },
  };
}
