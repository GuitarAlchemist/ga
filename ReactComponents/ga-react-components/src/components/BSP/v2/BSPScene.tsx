/**
 * The R3F scene: lighting, sky-ish background, ground, BSP regions and
 * walls, and the player. Keeps no state of its own — pure projection
 * of the laid-out tree.
 */

import { useMemo } from 'react';
import { Sky } from '@react-three/drei';
import * as THREE from 'three';
import type { BSPLayout, LaidOutRegion } from './layoutTree';
import { BSPRegionVolume } from './BSPRegionVolume';
import { BSPPartitionWall } from './BSPPartitionWall';
import { Player } from './Player';

interface Props {
  layout: BSPLayout;
  currentRegionName: string | null;
  moveSpeed: number;
  lookSpeed: number;
  onPositionChange: (p: THREE.Vector3) => void;
}

export function BSPScene({
  layout,
  currentRegionName,
  moveSpeed,
  lookSpeed,
  onPositionChange,
}: Props) {
  const groundSize = useMemo(() => {
    const s = layout.worldBounds.getSize(new THREE.Vector3());
    return Math.max(s.x, s.z) * 1.5;
  }, [layout]);

  return (
    <>
      {/* DOOM-style sector lighting: dim ambient + one strong directional. */}
      <ambientLight intensity={0.35} color={0xbcd0ff} />
      <directionalLight
        position={[40, 80, 40]}
        intensity={1.1}
        castShadow
        shadow-mapSize-width={1024}
        shadow-mapSize-height={1024}
      />
      <fog attach="fog" args={[0x0a0a14, 30, 220]} />

      {/* Faint sky for orientation. */}
      <Sky distance={450000} sunPosition={[40, 80, 40]} turbidity={6} rayleigh={2} />

      {/* Ground plate underneath everything. */}
      <mesh
        rotation={[-Math.PI / 2, 0, 0]}
        position={[0, -0.05, 0]}
        receiveShadow
      >
        <planeGeometry args={[groundSize, groundSize]} />
        <meshStandardMaterial color={0x0e0e1a} roughness={0.95} />
      </mesh>

      {/* BSP regions (rooms) */}
      {layout.regions.map((r: LaidOutRegion) => (
        <BSPRegionVolume
          key={r.node.region.name}
          region={r}
          active={r.node.region.name === currentRegionName}
        />
      ))}

      {/* Partition walls */}
      {layout.walls.map((w, i) => (
        <BSPPartitionWall key={`wall-${i}`} wall={w} />
      ))}

      <Player
        moveSpeed={moveSpeed}
        lookSpeed={lookSpeed}
        initialPosition={[0, 4, layout.worldBounds.max.z - 8]}
        onPositionChange={onPositionChange}
      />
    </>
  );
}
