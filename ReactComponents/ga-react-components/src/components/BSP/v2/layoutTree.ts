/**
 * Spatial layout for a BSP tree. Each leaf becomes an axis-aligned room;
 * each internal partition becomes a wall on the shared face of its two
 * children. The layout is **deterministic** given the same tree — same
 * input always produces the same world, which keeps minimap navigation
 * and region detection stable.
 *
 * Algorithm: walk the tree depth-first, alternating split axis (X / Z
 * by depth parity). Each internal node splits its bounding box in half;
 * leaves get the full half-box as their room volume. Depth is mapped to
 * Y so deeper tree levels sit higher in the world — gives the explorer
 * a "stacked floors" feel.
 */

import * as THREE from 'three';
import type { BSPNode } from '../BSPApiService';

export interface LaidOutRegion {
  node: BSPNode;
  center: THREE.Vector3;
  size: THREE.Vector3;
}

export interface LaidOutWall {
  position: THREE.Vector3;
  size: THREE.Vector3;
  axis: 'x' | 'z';
  fromDepth: number;
}

export interface BSPLayout {
  regions: LaidOutRegion[];
  walls: LaidOutWall[];
  worldBounds: THREE.Box3;
}

const ROOM_HEIGHT = 6;
const FLOOR_SPACING = 8; // vertical separation between depth levels

export function layoutTree(root: BSPNode, worldSize = 120): BSPLayout {
  const half = worldSize / 2;
  const regions: LaidOutRegion[] = [];
  const walls: LaidOutWall[] = [];

  function recurse(
    node: BSPNode,
    bounds: { minX: number; maxX: number; minZ: number; maxZ: number },
    depth: number,
  ): void {
    const cx = (bounds.minX + bounds.maxX) / 2;
    const cz = (bounds.minZ + bounds.maxZ) / 2;
    const sx = bounds.maxX - bounds.minX;
    const sz = bounds.maxZ - bounds.minZ;
    const y = depth * FLOOR_SPACING;

    if (node.isLeaf || !node.left || !node.right) {
      regions.push({
        node,
        center: new THREE.Vector3(cx, y + ROOM_HEIGHT / 2, cz),
        size: new THREE.Vector3(sx, ROOM_HEIGHT, sz),
      });
      return;
    }

    // Split on alternating axis by depth parity.
    const splitX = depth % 2 === 0;
    if (splitX) {
      const mid = (bounds.minX + bounds.maxX) / 2;
      walls.push({
        position: new THREE.Vector3(mid, y + ROOM_HEIGHT / 2, cz),
        size: new THREE.Vector3(0.3, ROOM_HEIGHT, sz),
        axis: 'x',
        fromDepth: depth,
      });
      recurse(node.left,  { ...bounds, maxX: mid }, depth + 1);
      recurse(node.right, { ...bounds, minX: mid }, depth + 1);
    } else {
      const mid = (bounds.minZ + bounds.maxZ) / 2;
      walls.push({
        position: new THREE.Vector3(cx, y + ROOM_HEIGHT / 2, mid),
        size: new THREE.Vector3(sx, ROOM_HEIGHT, 0.3),
        axis: 'z',
        fromDepth: depth,
      });
      recurse(node.left,  { ...bounds, maxZ: mid }, depth + 1);
      recurse(node.right, { ...bounds, minZ: mid }, depth + 1);
    }
  }

  recurse(root, { minX: -half, maxX: half, minZ: -half, maxZ: half }, 0);

  const worldBounds = new THREE.Box3(
    new THREE.Vector3(-half, 0, -half),
    new THREE.Vector3(half, ROOM_HEIGHT * 2, half),
  );

  return { regions, walls, worldBounds };
}

/** O(R) lookup: find which laid-out region contains a world-space point. */
export function regionAt(layout: BSPLayout, point: THREE.Vector3): LaidOutRegion | null {
  for (const r of layout.regions) {
    const dx = Math.abs(point.x - r.center.x);
    const dz = Math.abs(point.z - r.center.z);
    const dy = Math.abs(point.y - r.center.y);
    if (dx <= r.size.x / 2 && dz <= r.size.z / 2 && dy <= r.size.y / 2) {
      return r;
    }
  }
  return null;
}
