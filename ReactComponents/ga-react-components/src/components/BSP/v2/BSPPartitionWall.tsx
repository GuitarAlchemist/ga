/**
 * A BSP partition plane rendered as a thin semi-transparent wall. The
 * color shifts toward white at greater depths, conveying tree hierarchy
 * without needing a separate legend.
 */

import * as THREE from 'three';
import type { LaidOutWall } from './layoutTree';

interface Props {
  wall: LaidOutWall;
}

const DEPTH_TINT = [0x2dd4bf, 0x60a5fa, 0xa78bfa, 0xfacc15, 0xf472b6, 0xffffff];

export function BSPPartitionWall({ wall }: Props) {
  const tint = DEPTH_TINT[Math.min(wall.fromDepth, DEPTH_TINT.length - 1)];

  return (
    <mesh position={wall.position.toArray()}>
      <boxGeometry args={[wall.size.x, wall.size.y, wall.size.z]} />
      <meshStandardMaterial
        color={tint}
        emissive={tint}
        emissiveIntensity={0.4}
        transparent
        opacity={0.35}
        roughness={0.2}
        metalness={0.6}
        side={THREE.DoubleSide}
      />
    </mesh>
  );
}
