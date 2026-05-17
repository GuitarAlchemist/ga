/**
 * One BSP leaf region rendered as a colored translucent box plus a
 * solid floor plate. The floor anchors the player visually; the
 * translucent volume signals "you are in region X" without occluding
 * the partition walls.
 *
 * When the region is a curated musical leaf, the floor also carries a
 * tonic letter — overlaid via drei's <Text> so a guitarist sees the
 * key from a distance even before the HUD updates.
 */

import { useRef } from 'react';
import * as THREE from 'three';
import { Text } from '@react-three/drei';
import type { LaidOutRegion } from './layoutTree';
import { colorForRegion } from './tonalityColors';
import type { MusicalRegion } from './musicalTree';

interface Props {
  region: LaidOutRegion;
  active: boolean; // current player region — pulses brighter
}

export function BSPRegionVolume({ region, active }: Props) {
  const meshRef = useRef<THREE.Mesh>(null);
  const color = colorForRegion(region.node.region);
  const music = (region.node.region as MusicalRegion).music;

  const floorY = region.center.y - region.size.y / 2 + 0.05;
  const labelY = floorY + 0.02;

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
          emissiveIntensity={active ? 0.5 : 0.12}
          roughness={0.7}
          metalness={0.1}
        />
      </mesh>

      {/* Tonic letter on the floor — readable from the air. */}
      {music && (
        <Text
          position={[region.center.x, labelY, region.center.z]}
          rotation={[-Math.PI / 2, 0, 0]}
          fontSize={Math.min(region.size.x, region.size.z) * 0.35}
          color={active ? '#ffffff' : '#0d1117'}
          anchorX="center"
          anchorY="middle"
          outlineWidth={0.03}
          outlineColor={active ? '#0d1117' : '#ffffff'}
        >
          {music.shortName}
        </Text>
      )}

      {/* Atmospheric volume — translucent envelope. */}
      <mesh ref={meshRef} position={region.center.toArray()}>
        <boxGeometry args={[region.size.x - 0.4, region.size.y - 0.2, region.size.z - 0.4]} />
        <meshBasicMaterial
          color={color}
          transparent
          opacity={active ? 0.16 : 0.05}
          depthWrite={false}
        />
      </mesh>
    </group>
  );
}
