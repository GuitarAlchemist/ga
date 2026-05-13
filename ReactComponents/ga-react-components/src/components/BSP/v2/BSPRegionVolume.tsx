/**
 * One BSP leaf region rendered as a colored translucent box plus a
 * solid floor plate. The floor anchors the player visually; the
 * translucent volume signals "you are in region X" without occluding
 * the partition walls.
 */

import { useRef } from 'react';
import * as THREE from 'three';
import type { LaidOutRegion } from './layoutTree';
import { colorFor } from './tonalityColors';

interface Props {
  region: LaidOutRegion;
  active: boolean; // current player region — pulses brighter
}

export function BSPRegionVolume({ region, active }: Props) {
  const meshRef = useRef<THREE.Mesh>(null);
  const color = colorFor(region.node.region.tonalityType);

  // Floor plate sits at the bottom of the region volume.
  const floorY = region.center.y - region.size.y / 2 + 0.05;

  return (
    <group>
      {/* Floor — solid, walkable. */}
      <mesh
        position={[region.center.x, floorY, region.center.z]}
        receiveShadow
      >
        <boxGeometry args={[region.size.x - 0.4, 0.1, region.size.z - 0.4]} />
        <meshStandardMaterial
          color={color}
          emissive={color}
          emissiveIntensity={active ? 0.35 : 0.08}
          roughness={0.7}
          metalness={0.1}
        />
      </mesh>

      {/* Atmospheric volume — translucent envelope. */}
      <mesh ref={meshRef} position={region.center.toArray()}>
        <boxGeometry args={[region.size.x - 0.4, region.size.y - 0.2, region.size.z - 0.4]} />
        <meshBasicMaterial
          color={color}
          transparent
          opacity={active ? 0.12 : 0.04}
          depthWrite={false}
        />
      </mesh>
    </group>
  );
}
