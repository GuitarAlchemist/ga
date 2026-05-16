/**
 * Heads-up display: stats, current region, controls cheat-sheet. DOM
 * overlay (not part of the Three scene) so text stays crisp at any
 * canvas resolution.
 *
 * The DOOM aesthetic (#0f0 mono on black with light borders) is
 * carried over from the v1 component for visual continuity.
 */

import { type CSSProperties, useEffect, useState } from 'react';
import type { BSPRegion } from '../BSPApiService';

interface Props {
  currentRegion: BSPRegion | null;
  source: 'api' | 'demo' | 'pending';
  regionCount: number;
  treeDepth: number;
  showControls?: boolean;
}

const PANEL: CSSProperties = {
  position: 'absolute',
  color: '#0f0',
  background: 'rgba(0, 0, 0, 0.55)',
  border: '1px solid rgba(0, 255, 0, 0.35)',
  fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace',
  fontSize: 12,
  padding: '8px 12px',
  borderRadius: 4,
  backdropFilter: 'blur(2px)',
  pointerEvents: 'none',
  lineHeight: 1.4,
};

export function HUD({
  currentRegion,
  source,
  regionCount,
  treeDepth,
  showControls = true,
}: Props) {
  const [fps, setFps] = useState(0);

  useEffect(() => {
    let frames = 0;
    let last = performance.now();
    let rafId = 0;
    const tick = () => {
      frames += 1;
      const now = performance.now();
      if (now - last >= 500) {
        setFps(Math.round((frames * 1000) / (now - last)));
        frames = 0;
        last = now;
      }
      rafId = requestAnimationFrame(tick);
    };
    rafId = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(rafId);
  }, []);

  return (
    <>
      <div style={{ ...PANEL, top: 12, left: 12 }}>
        <div style={{ color: '#7CFC7C', fontWeight: 600 }}>BSP DOOM EXPLORER</div>
        <div>fps: {fps}</div>
        <div>regions: {regionCount} · depth: {treeDepth}</div>
        <div>source: {source}{source === 'demo' && ' (api unreachable)'}</div>
      </div>

      {currentRegion && (
        <div style={{ ...PANEL, top: 12, right: 12, textAlign: 'right' }}>
          <div style={{ color: '#7CFC7C', fontWeight: 600 }}>{currentRegion.name}</div>
          <div>type: {currentRegion.tonalityType}</div>
          <div>center: {currentRegion.tonalCenter}</div>
          <div>pitches: {currentRegion.pitchClasses.join(' ')}</div>
        </div>
      )}

      {showControls && (
        <div style={{ ...PANEL, bottom: 12, left: 12 }}>
          <div style={{ color: '#7CFC7C', fontWeight: 600 }}>controls</div>
          <div>click canvas → lock pointer</div>
          <div>WASD / arrows · move</div>
          <div>space · up &nbsp;·&nbsp; shift · down</div>
          <div>esc · release pointer</div>
        </div>
      )}
    </>
  );
}
