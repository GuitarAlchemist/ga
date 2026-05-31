/**
 * Procedural BSP tree used when the backend `/api/bsp/tree-structure`
 * endpoint is unreachable. The previous implementation hard-failed with
 * a blank screen in this case — that's the root cause of the "broken"
 * live page. Demo mode guarantees the explorer renders something
 * meaningful even without a backend.
 *
 * The structure mirrors the real BSP contract (`BSPNode`/`BSPRegion`)
 * so consumers can't tell the difference at the render layer.
 */

import type { BSPNode, BSPTreeStructureResponse } from '../BSPApiService';

const TONALITIES = [
  ['Major',      'C',  ['C', 'E', 'G']],
  ['Minor',      'A',  ['A', 'C', 'E']],
  ['Dominant',   'G',  ['G', 'B', 'D', 'F']],
  ['Modal',      'D',  ['D', 'E', 'F', 'G', 'A', 'B', 'C']],
  ['Chromatic',  'F',  ['F', 'FSharp', 'G', 'GSharp']],
  ['Atonal',     'E',  ['C', 'CSharp', 'E', 'FSharp', 'GSharp', 'B']],
  ['Diminished', 'B',  ['B', 'D', 'F', 'GSharp']],
  ['Augmented',  'C',  ['C', 'E', 'GSharp']],
] as const;

const PARTITION_STRATEGIES = ['CircleOfFifths', 'ChromaticDistance', 'TonalHierarchy'];

function leaf(idx: number, depth: number): BSPNode {
  const [tonalityType, tonalCenter, pitchClasses] = TONALITIES[idx % TONALITIES.length];
  return {
    region: {
      name: `${tonalityType}-${tonalCenter}`,
      tonalityType,
      tonalCenter: tonalCenter.charCodeAt(0) - 65,
      pitchClasses: [...pitchClasses],
    },
    isLeaf: true,
    depth,
    elements: [],
  };
}

function branch(left: BSPNode, right: BSPNode, depth: number, strategyIdx: number): BSPNode {
  return {
    region: {
      name: `partition-${depth}-${strategyIdx}`,
      tonalityType: 'Internal',
      tonalCenter: 0,
      pitchClasses: [],
    },
    partition: {
      strategy: PARTITION_STRATEGIES[strategyIdx % PARTITION_STRATEGIES.length],
      referencePoint: depth,
      threshold: 0.5,
      normal: [depth % 2, (depth + 1) % 2, 0],
    },
    left,
    right,
    isLeaf: false,
    depth,
    elements: [],
  };
}

/**
 * Build an 8-leaf BSP tree (depth 3, balanced). Enough to demonstrate
 * traversal and region differentiation without overwhelming the renderer.
 */
export function buildDemoTree(): BSPTreeStructureResponse {
  const leaves = Array.from({ length: 8 }, (_, i) => leaf(i, 3));

  // Combine pairs at depth 2
  const d2 = [
    branch(leaves[0], leaves[1], 2, 0),
    branch(leaves[2], leaves[3], 2, 1),
    branch(leaves[4], leaves[5], 2, 2),
    branch(leaves[6], leaves[7], 2, 0),
  ];

  // Depth 1
  const d1 = [
    branch(d2[0], d2[1], 1, 1),
    branch(d2[2], d2[3], 1, 2),
  ];

  const root = branch(d1[0], d1[1], 0, 0);

  return {
    root,
    nodeCount: 15,
    maxDepth: 3,
    regionCount: 8,
    partitionCount: 7,
  };
}
