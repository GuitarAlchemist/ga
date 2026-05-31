/**
 * BSP DOOM Explorer — v2 rewrite (2026-05-15) + musical rebuild (2026-05-17).
 *
 * Replaces the 6,250-line BSPDoomExplorer.tsx with a ~180-line
 * orchestrator that composes a handful of focused components built on
 * react-three-fiber + drei. Same public props; same `/test/bsp-doom-
 * explorer` route (via index.ts re-export).
 *
 * The 2026-05-15 v2 fixed the rendering (procedural fallback so the
 * page is never blank). The 2026-05-17 musical rebuild made it useful
 * — each room is a real key+mode, the HUD shows scale notes + Roman
 * numerals, and "modulate to →" chips teleport the camera through
 * harmonic neighbours.
 */

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Canvas } from '@react-three/fiber';
import * as THREE from 'three';
import type { BSPRegion } from '../BSPApiService';
import { useBSPTree } from './useBSPTree';
import { layoutTree, regionAt, type LaidOutRegion } from './layoutTree';
import { BSPScene } from './BSPScene';
import { HUD } from './HUD';
import { Minimap } from './Minimap';
import type { PlayerHandle } from './Player';

export interface BSPDoomExplorerV2Props {
  width?: number;
  height?: number;
  moveSpeed?: number;
  lookSpeed?: number;
  showHUD?: boolean;
  showMinimap?: boolean;
  onRegionChange?: (region: BSPRegion) => void;
}

/** The ii–V–I tour path through the curated tree. */
const TOUR_PATH = ['D minor', 'G major', 'C major'];
const TOUR_STEP_MS = 2200;

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
  const [tourActive, setTourActive] = useState(false);
  const lastRegionName = useRef<string | null>(null);
  const playerRef = useRef<PlayerHandle>(null);
  const tourTimer = useRef<number | null>(null);

  // Index leaves by *both* short name ("C maj") and full key name ("C major").
  // Neighbours are listed by full key name; minimap/short floor labels use
  // the short name. We need lookups for either.
  const regionsByName = useMemo(() => {
    if (!layout) return new Map<string, LaidOutRegion>();
    const map = new Map<string, LaidOutRegion>();
    for (const r of layout.regions) {
      map.set(r.node.region.name, r);
      const music = (r.node.region as { music?: { key: string } }).music;
      if (music?.key) map.set(music.key, r);
    }
    return map;
  }, [layout]);

  const handlePositionChange = useCallback(
    (pos: THREE.Vector3) => {
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

  const teleportTo = useCallback(
    (keyName: string) => {
      const target = regionsByName.get(keyName);
      if (!target || !playerRef.current) return;
      // Stand in the middle of the room, eye-height above the floor.
      const dest = new THREE.Vector3(
        target.center.x,
        target.center.y - target.size.y / 2 + 4,
        target.center.z,
      );
      playerRef.current.teleportTo(dest);
      // Optimistic update — region detector will catch up next frame.
      lastRegionName.current = target.node.region.name;
      setCurrentRegion(target.node.region);
      onRegionChange?.(target.node.region);
    },
    [regionsByName, onRegionChange],
  );

  // ---- Tour ----
  // Walks D minor → G major → C major on a 2.2 s cadence. Stops on its
  // own at the end of the path; user can stop early by re-clicking the
  // toggle.
  useEffect(() => {
    if (!tourActive) {
      if (tourTimer.current !== null) {
        window.clearInterval(tourTimer.current);
        tourTimer.current = null;
      }
      return;
    }
    let i = 0;
    teleportTo(TOUR_PATH[i]);
    tourTimer.current = window.setInterval(() => {
      i += 1;
      if (i >= TOUR_PATH.length) {
        setTourActive(false);
        return;
      }
      teleportTo(TOUR_PATH[i]);
    }, TOUR_STEP_MS);
    return () => {
      if (tourTimer.current !== null) {
        window.clearInterval(tourTimer.current);
        tourTimer.current = null;
      }
    };
  }, [tourActive, teleportTo]);

  const handleTourToggle = useCallback(() => setTourActive((v) => !v), []);

  if (treeState.loading || !layout || !treeState.tree) {
    return (
      <div
        style={{
          width,
          height,
          background: '#0d1117',
          color: '#8b949e',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          fontFamily: 'ui-monospace, monospace',
        }}
      >
        loading BSP tree…
      </div>
    );
  }

  return (
    <div style={{ width, height, position: 'relative', background: '#0d1117' }}>
      <Canvas
        shadows
        camera={{ fov: 75, near: 0.1, far: 500, position: [0, 4, 20] }}
        gl={{ antialias: true, powerPreference: 'high-performance' }}
      >
        <BSPScene
          ref={playerRef}
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
          onTeleport={teleportTo}
          tourActive={tourActive}
          onTourToggle={handleTourToggle}
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
