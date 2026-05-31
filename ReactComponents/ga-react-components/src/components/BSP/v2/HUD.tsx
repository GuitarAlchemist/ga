/**
 * Heads-up display for the BSP DOOM Explorer.
 *
 * The legacy DOOM-green HUD told you almost nothing musical. This one
 * is built around the question "where am I, harmonically?" — the
 * payload is the *current key*, its scale, its diatonic chords with
 * Roman numerals, and a list of click-to-teleport neighbour keys
 * representing real modulation moves.
 *
 * Palette matches the GA dark theme (`#0d1117` / `#161b22` / `#30363d`
 * / `#e6edf3`) so this demo stops looking like a different product.
 */

import { type CSSProperties, useEffect, useState } from 'react';
import type { BSPRegion } from '../BSPApiService';
import type { MusicalLeafMeta, MusicalRegion } from './musicalTree';

interface Props {
  currentRegion: BSPRegion | null;
  source: 'api' | 'demo' | 'pending' | 'curated';
  regionCount: number;
  treeDepth: number;
  onTeleport?: (keyName: string) => void;
  tourActive?: boolean;
  onTourToggle?: () => void;
  showControls?: boolean;
}

const PANEL: CSSProperties = {
  position: 'absolute',
  color: '#e6edf3',
  background: 'rgba(13, 17, 23, 0.88)',
  border: '1px solid #30363d',
  fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace',
  fontSize: 12,
  padding: '10px 12px',
  borderRadius: 6,
  backdropFilter: 'blur(4px)',
  lineHeight: 1.5,
};

const HEADER: CSSProperties = {
  color: '#79c0ff',
  fontWeight: 700,
  letterSpacing: 0.4,
  textTransform: 'uppercase',
  fontSize: 10,
  marginBottom: 4,
};

const CHIP: CSSProperties = {
  display: 'inline-block',
  padding: '2px 7px',
  borderRadius: 3,
  marginRight: 4,
  marginTop: 4,
  fontSize: 11,
  border: '1px solid #30363d',
  background: '#161b22',
  color: '#e6edf3',
};

const TELE_CHIP: CSSProperties = {
  ...CHIP,
  cursor: 'pointer',
  pointerEvents: 'auto',
  background: '#161b22',
  borderColor: '#388bfd',
  color: '#79c0ff',
};

export function HUD({
  currentRegion,
  source,
  regionCount,
  treeDepth,
  onTeleport,
  tourActive,
  onTourToggle,
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

  const music: MusicalLeafMeta | undefined = (currentRegion as MusicalRegion | null)?.music;

  return (
    <>
      {/* Top-left: stats. */}
      <div style={{ ...PANEL, top: 12, left: 12, pointerEvents: 'none' }}>
        <div style={HEADER}>BSP Doom Explorer</div>
        <div>fps: {fps}</div>
        <div>regions: {regionCount} · depth: {treeDepth}</div>
        <div style={{ color: '#8b949e' }}>source: {source}</div>
      </div>

      {/* Top-right: "Where am I" card.
       *  Always renders something — even before the player enters a
       *  room — so the page never looks empty.                       */}
      <div
        style={{
          ...PANEL,
          top: 12,
          right: 12,
          width: 280,
          pointerEvents: 'auto',
        }}
      >
        <div style={HEADER}>Where am I</div>
        {music ? (
          <>
            <div style={{ fontSize: 16, fontWeight: 700, color: '#e6edf3', marginBottom: 4 }}>
              {music.key}
            </div>
            <div style={{ color: '#8b949e', fontSize: 11, marginBottom: 6 }}>
              mode: {music.mode} · tonic: {music.tonic}
            </div>

            <div style={{ marginTop: 6 }}>
              <div style={{ color: '#8b949e', fontSize: 10 }}>scale</div>
              <div>
                {music.scaleNotes.map((n) => (
                  <span key={n} style={CHIP}>{n}</span>
                ))}
              </div>
            </div>

            <div style={{ marginTop: 6 }}>
              <div style={{ color: '#8b949e', fontSize: 10 }}>diatonic chords</div>
              {music.diatonicChords.map((c, i) => (
                <span key={c} style={{ ...CHIP, display: 'inline-flex', flexDirection: 'column', alignItems: 'center', padding: '2px 8px' }}>
                  <span style={{ color: '#79c0ff', fontSize: 10 }}>{music.diatonicRoman[i]}</span>
                  <span>{c}</span>
                </span>
              ))}
            </div>

            {music.neighbours.length > 0 && onTeleport && (
              <div style={{ marginTop: 8 }}>
                <div style={{ color: '#8b949e', fontSize: 10 }}>modulate to →</div>
                {music.neighbours.map((n) => (
                  <span
                    key={`${n.label}:${n.key}`}
                    style={TELE_CHIP}
                    onClick={() => onTeleport(n.key)}
                    role="button"
                    tabIndex={0}
                    title={`Teleport to ${n.key} — ${n.label}`}
                  >
                    {n.key}
                    <span style={{ color: '#8b949e', marginLeft: 4, fontSize: 10 }}>
                      {n.label}
                    </span>
                  </span>
                ))}
              </div>
            )}
          </>
        ) : (
          <div style={{ color: '#8b949e', fontSize: 12 }}>
            Click the canvas to lock the pointer, then walk into a room
            with WASD. Each room is a key. The HUD will show its scale,
            diatonic chords, and where you can modulate next.
          </div>
        )}
      </div>

      {/* Bottom-left: controls cheat-sheet (one line each, no walls of text). */}
      {showControls && (
        <div style={{ ...PANEL, bottom: 12, left: 12, pointerEvents: 'none' }}>
          <div style={HEADER}>controls</div>
          <div><b>click</b> canvas to lock pointer · <b>esc</b> to release</div>
          <div><b>WASD</b> / arrows to move · <b>space/shift</b> for up/down</div>
          <div><b>click a neighbour key</b> to modulate (teleport)</div>
        </div>
      )}

      {/* Bottom-centre: Tour toggle. */}
      {onTourToggle && (
        <div
          style={{
            position: 'absolute',
            bottom: 12,
            left: '50%',
            transform: 'translateX(-50%)',
            pointerEvents: 'auto',
          }}
        >
          <button
            type="button"
            onClick={onTourToggle}
            style={{
              padding: '6px 14px',
              fontFamily: 'ui-monospace, monospace',
              fontSize: 12,
              color: tourActive ? '#0d1117' : '#79c0ff',
              background: tourActive ? '#79c0ff' : '#161b22',
              border: '1px solid #388bfd',
              borderRadius: 6,
              cursor: 'pointer',
            }}
            aria-pressed={tourActive}
            title="Auto-walk through a ii–V–I progression in the key world"
          >
            {tourActive ? '■ stop tour' : '▶ ii–V–I tour'}
          </button>
        </div>
      )}
    </>
  );
}
