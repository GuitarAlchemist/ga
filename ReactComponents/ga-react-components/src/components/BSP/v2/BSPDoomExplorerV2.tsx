/**
 * BSP DOOM Explorer — v2 rewrite.
 *
 * Replaces the 6,250-line BSPDoomExplorer.tsx with a ~120-line
 * orchestrator that composes ~10 focused components built on
 * react-three-fiber + drei. Same public props; same `/test/bsp-doom-
 * explorer` route (via index.ts re-export); same behavior end-to-end
 * but renders even when the backend BSP API is unreachable (the root
 * cause of the original "broken" live page).
 *
 * Architecture:
 *
 *   useBSPTree ──► layoutTree ──► BSPScene
 *                                  ├─ BSPRegionVolume (x N leaves)
 *                                  ├─ BSPPartitionWall (x M splits)
 *                                  └─ Player (PointerLockControls + WASD)
 *                                            │
 *                                            └─► onPositionChange ──► regionAt
 *                                                                        │
 *                                                                ┌───────┴───────┐
 *                                                              HUD             Minimap
 */

import { useCallback, useMemo, useRef, useState } from 'react';
import { Canvas } from '@react-three/fiber';
import * as THREE from 'three';
import type { BSPRegion } from '../BSPApiService';
import { useBSPTree } from './useBSPTree';
import { layoutTree, regionAt } from './layoutTree';
import { BSPScene } from './BSPScene';
import { HUD } from './HUD';
import { Minimap } from './Minimap';

export interface BSPDoomExplorerV2Props {
  width?: number;
  height?: number;
  moveSpeed?: number;
  lookSpeed?: number;
  showHUD?: boolean;
  showMinimap?: boolean;
  onRegionChange?: (region: BSPRegion) => void;
}

export function BSPDoomExplorerV2({
  width = 1200,
  height = 800,
  moveSpeed = 5.0,
  lookSpeed = 0.002,
  showHUD = true,
  showMinimap = true,
  onRegionChange,
}: BSPDoomExplorerV2Props) {
  const treeState = useBSPTree();
  const layout = useMemo(
    () => (treeState.tree ? layoutTree(treeState.tree.root) : null),
    [treeState.tree],
  );

  const [playerPosition, setPlayerPosition] = useState(() => new THREE.Vector3(0, 4, 20));
  const [currentRegion, setCurrentRegion] = useState<BSPRegion | null>(null);
  const lastRegionName = useRef<string | null>(null);

  const handlePositionChange = useCallback(
    (pos: THREE.Vector3) => {
      // Cheap clone — `useFrame` reuses the same vector instance per frame.
      setPlayerPosition(pos.clone());
      if (!layout) return;
      const r = regionAt(layout, pos);
      const name = r?.node.region.name ?? null;
      if (name !== lastRegionName.current) {
        lastRegionName.current = name;
        const region = r?.node.region ?? null;
        setCurrentRegion(region);
        if (region) onRegionChange?.(region);
      }
    },
    [layout, onRegionChange],
  );

  if (treeState.loading || !layout || !treeState.tree) {
    return (
      <div
        style={{
          width,
          height,
          background: '#000',
          color: '#0f0',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          fontFamily: 'monospace',
        }}
      >
        loading BSP tree…
      </div>
    );
  }

  return (
    <div style={{ width, height, position: 'relative', background: '#000' }}>
      <Canvas
        shadows
        camera={{ fov: 75, near: 0.1, far: 500, position: [0, 4, 20] }}
        gl={{ antialias: true, powerPreference: 'high-performance' }}
      >
        <BSPScene
          layout={layout}
          currentRegionName={currentRegion?.name ?? null}
          moveSpeed={moveSpeed}
          lookSpeed={lookSpeed}
          onPositionChange={handlePositionChange}
        />
      </Canvas>

      {showHUD && (
        <HUD
          currentRegion={currentRegion}
          source={treeState.source}
          regionCount={treeState.tree.regionCount}
          treeDepth={treeState.tree.maxDepth}
        />
      )}

      {showMinimap && (
        <Minimap
          layout={layout}
          playerPosition={playerPosition}
          currentRegionName={currentRegion?.name ?? null}
        />
      )}
    </div>
  );
}
