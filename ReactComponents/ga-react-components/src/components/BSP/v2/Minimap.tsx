/**
 * Top-down minimap rendered as a small SVG overlay. The legacy
 * implementation used a second R3F Canvas + ortho camera which doubled
 * the GPU cost for ~80 LOC of value. SVG gives the same affordance for
 * a third of the code and zero GPU.
 */

import { type CSSProperties, useMemo } from 'react';
import type { BSPLayout } from './layoutTree';
import { hexForRegion } from './tonalityColors';
import * as THREE from 'three';

interface Props {
  layout: BSPLayout;
  playerPosition: THREE.Vector3;
  currentRegionName: string | null;
  size?: number;
}

const PANEL: CSSProperties = {
  position: 'absolute',
  bottom: 12,
  right: 12,
  background: 'rgba(13, 17, 23, 0.85)',
  border: '1px solid #30363d',
  borderRadius: 6,
  pointerEvents: 'none',
  padding: 6,
};

export function Minimap({ layout, playerPosition, currentRegionName, size = 180 }: Props) {
  const { regions, walls, worldSize } = useMemo(() => {
    const bs = layout.worldBounds.getSize(new THREE.Vector3());
    return {
      regions: layout.regions,
      walls: layout.walls,
      worldSize: Math.max(bs.x, bs.z),
    };
  }, [layout]);

  // World→SVG: world is [-worldSize/2, +worldSize/2]; SVG is [0, size].
  const w2s = (v: number) => (v / worldSize + 0.5) * size;

  return (
    <div style={PANEL}>
      <svg width={size} height={size} style={{ display: 'block' }}>
        <rect width={size} height={size} fill="#070710" />

        <rect width={size} height={size} fill="#0d1117" />

        {regions.map((r) => {
          const x = w2s(r.center.x - r.size.x / 2);
          const y = w2s(r.center.z - r.size.z / 2);
          const w = (r.size.x / worldSize) * size;
          const h = (r.size.z / worldSize) * size;
          const isActive = r.node.region.name === currentRegionName;
          const fill = `#${hexForRegion(r.node.region).toString(16).padStart(6, '0')}`;
          return (
            <g key={r.node.region.name}>
              <rect
                x={x}
                y={y}
                width={w - 1}
                height={h - 1}
                fill={fill}
                opacity={isActive ? 0.9 : 0.45}
                stroke={isActive ? '#e6edf3' : '#30363d'}
                strokeWidth={isActive ? 1.5 : 0.5}
              />
              <text
                x={x + w / 2}
                y={y + h / 2 + 3}
                textAnchor="middle"
                fontSize={9}
                fontFamily="ui-monospace, monospace"
                fontWeight={isActive ? 700 : 500}
                fill={isActive ? '#0d1117' : '#e6edf3'}
                pointerEvents="none"
              >
                {r.node.region.name}
              </text>
            </g>
          );
        })}

        {walls.map((wall, i) => {
          const cx = w2s(wall.position.x);
          const cz = w2s(wall.position.z);
          if (wall.axis === 'x') {
            const h = (wall.size.z / worldSize) * size;
            return <line key={i} x1={cx} y1={cz - h / 2} x2={cx} y2={cz + h / 2} stroke="#8b949e" strokeWidth={1} opacity={0.5} />;
          }
          const w = (wall.size.x / worldSize) * size;
          return <line key={i} x1={cx - w / 2} y1={cz} x2={cx + w / 2} y2={cz} stroke="#8b949e" strokeWidth={1} opacity={0.5} />;
        })}

        {/* Player dot */}
        <circle cx={w2s(playerPosition.x)} cy={w2s(playerPosition.z)} r={3} fill="#e6edf3" stroke="#0d1117" strokeWidth={0.5} />
      </svg>
    </div>
  );
}
