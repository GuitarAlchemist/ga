// src/components/PrimeRadiant/LunarLander.tsx
// Apollo LM Descent Simulator — native React/Three.js component.
// Renders the simulation directly via LunarLanderEngine (no iframe).

import React, { useCallback, useEffect, useRef, useState } from 'react';
import { LunarLanderEngine } from './LunarLanderEngine';
import type { LanderState, LanderStats } from './LunarLanderEngine';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface LunarLanderStats {
  vSpeed: string;
  hSpeed: string;
  tilt: string;
  fuel: string;
  time: string;
  distance: string;
}

export interface LunarLanderProps {
  /** Whether the overlay is open */
  open: boolean;
  /** Close handler */
  onClose: () => void;
  /** Callback when the LM lands (success or crash) */
  onLanded?: (success: boolean, stats: LunarLanderStats) => void;
  /** @deprecated No longer used — simulation is rendered natively */
  src?: string;
}

// ---------------------------------------------------------------------------
// Initial state
// ---------------------------------------------------------------------------

const initialState: LanderState = {
  altitude: 500,
  verticalSpeed: -5,
  horizontalSpeed: 2.1,
  rangeToLZ: 0,
  fuelPercent: 100,
  throttlePercent: 0,
  pitchDeg: 0,
  rollDeg: 0,
  yawDeg: 0,
  massKg: 12900,
  cameraMode: 'ORBIT',
  phase: 'DESCENT ORBIT',
  missionTime: '0:00',
  contactLight: false,
  gameState: 'waiting',
  calloutText: '',
  cinematicMode: false,
  mothershipVisible: false,
  autopilot: false,
};

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Classify a numeric value for HUD coloring */
function vSpeedClass(v: number): string {
  if (v < -3) return 'lunar-hud__value--warn';
  if (v < -1.6) return 'lunar-hud__value--caution';
  return '';
}

function hSpeedClass(h: number): string {
  if (h > 2) return 'lunar-hud__value--warn';
  if (h > 1) return 'lunar-hud__value--caution';
  return '';
}

function fuelClass(pct: number): string {
  if (pct < 5) return 'lunar-hud__value--warn';
  if (pct < 15) return 'lunar-hud__value--caution';
  return '';
}

function fuelBarClass(pct: number): string {
  if (pct < 5) return 'lunar-hud__bar-fill lunar-hud__bar-fill--critical';
  if (pct < 15) return 'lunar-hud__bar-fill lunar-hud__bar-fill--low';
  return 'lunar-hud__bar-fill';
}

function altClass(alt: number): string {
  return alt < 30 ? 'lunar-hud__value--caution' : '';
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export const LunarLander: React.FC<LunarLanderProps> = ({
  open,
  onClose,
  onLanded,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const engineRef = useRef<LunarLanderEngine | null>(null);
  const [state, setState] = useState<LanderState>(initialState);
  const [landingResult, setLandingResult] = useState<{
    success: boolean;
    stats: LanderStats;
    failures?: string[];
  } | null>(null);
  const [helpVisible, setHelpVisible] = useState(false);

  // ── Engine lifecycle ──
  useEffect(() => {
    if (!open || !containerRef.current) return;

    const engine = new LunarLanderEngine(containerRef.current);
    engineRef.current = engine;

    engine.onLanded((success, stats) => {
      setLandingResult({ success, stats });
      onLanded?.(success, {
        vSpeed: stats.vSpeed,
        hSpeed: stats.hSpeed,
        tilt: stats.tilt,
        fuel: stats.fuel,
        time: stats.time,
        distance: stats.distance,
      });
    });

    engine.start();

    // State polling for HUD
    const interval = setInterval(() => {
      setState(engine.getState());
    }, 50);

    return () => {
      clearInterval(interval);
      engine.stop();
      engine.dispose();
      engineRef.current = null;
    };
  }, [open]); // eslint-disable-line react-hooks/exhaustive-deps

  // ── Resize observer ──
  useEffect(() => {
    if (!open || !containerRef.current) return;
    const container = containerRef.current;

    const ro = new ResizeObserver((entries) => {
      for (const entry of entries) {
        const { width, height } = entry.contentRect;
        engineRef.current?.resize(width, height);
      }
    });
    ro.observe(container);
    return () => ro.disconnect();
  }, [open]);

  // ── Keyboard: Escape to close, H for help ──
  useEffect(() => {
    if (!open) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.preventDefault();
        onClose();
      }
      if (e.code === 'KeyH') {
        setHelpVisible((v) => !v);
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [open, onClose]);

  // ── Handlers ──
  const handleRestart = useCallback(() => {
    engineRef.current?.restart();
    setLandingResult(null);
  }, []);

  const handleCycleCamera = useCallback(() => {
    engineRef.current?.cycleCamera();
  }, []);

  // ── Reset on open ──
  useEffect(() => {
    if (open) {
      setLandingResult(null);
      setHelpVisible(false);
      setState(initialState);
    }
  }, [open]);

  if (!open) return null;

  const isFlying = state.gameState === 'flying';
  const isEnded = state.gameState === 'landed' || state.gameState === 'crashed';
  const isCockpit = state.cameraMode === 'COCKPIT';
  const showAttitude = state.cameraMode === 'CHASE' || state.cameraMode === 'COCKPIT';

  return (
    <div style={styles.overlay}>
      {/* ── Header bar ── */}
      <div style={styles.header}>
        <div style={styles.headerLeft}>
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M12 3a6 6 0 0 0 0 12 9 9 0 0 0 9-9" />
            <circle cx="12" cy="9" r="1" fill="currentColor" />
          </svg>
          <span style={styles.title}>LUNAR DESCENT SIMULATOR</span>
          {isFlying && !landingResult && (
            <span style={{ ...styles.statusBadge, ...styles.statusActive }}>LIVE</span>
          )}
          {landingResult && (
            <span style={{
              ...styles.statusBadge,
              ...(landingResult.success ? styles.statusSuccess : styles.statusFail),
            }}>
              {landingResult.success ? 'LANDED' : 'CRASH'}
            </span>
          )}
        </div>
        <div style={styles.headerRight}>
          <button style={styles.btn} onClick={handleCycleCamera} title="Cycle camera (C)">
            CAM: {state.cameraMode}
          </button>
          <button style={styles.btn} onClick={handleRestart} title="Restart mission (R)">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <polyline points="23 4 23 10 17 10" />
              <path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10" />
            </svg>
            {' '}RESTART
          </button>
          <button style={styles.closeBtn} onClick={onClose} title="Close (Esc)">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <line x1="18" y1="6" x2="6" y2="18" />
              <line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </button>
        </div>
      </div>

      {/* ── Three.js container ── */}
      <div ref={containerRef} style={styles.canvasContainer} />

      {/* ── Scanlines overlay ── */}
      <div style={styles.scanlines} />

      {/* ── HUD (only while flying) ── */}
      {isFlying && (
        <>
          {/* Left panel */}
          <div style={{ ...styles.hudPanel, top: 60, left: 20, minWidth: 200 }}>
            <HudRow label="ALT" unit="M">
              <span className={`lunar-hud__value ${altClass(state.altitude)}`} style={styles.value}>
                {state.altitude.toFixed(1)}
              </span>
            </HudRow>
            <HudRow label="V/S" unit="M/S">
              <span className={`lunar-hud__value ${vSpeedClass(state.verticalSpeed)}`} style={{
                ...styles.value,
                ...(state.verticalSpeed < -3 ? styles.valueWarn : state.verticalSpeed < -1.6 ? styles.valueCaution : {}),
              }}>
                {state.verticalSpeed.toFixed(1)}
              </span>
            </HudRow>
            <HudRow label="H/S" unit="M/S">
              <span style={{
                ...styles.value,
                ...(state.horizontalSpeed > 2 ? styles.valueWarn : state.horizontalSpeed > 1 ? styles.valueCaution : {}),
              }}>
                {state.horizontalSpeed.toFixed(1)}
              </span>
            </HudRow>
            <HudRow label="RANGE TO LZ" unit="M">
              <span style={styles.value}>{state.rangeToLZ.toFixed(1)}</span>
            </HudRow>
          </div>

          {/* Right panel */}
          <div style={{ ...styles.hudPanel, top: 60, right: 20, minWidth: 200, textAlign: 'right' as const }}>
            <HudRow label="FUEL" unit="%">
              <span style={{
                ...styles.value,
                ...(state.fuelPercent < 5 ? styles.valueWarn : state.fuelPercent < 15 ? styles.valueCaution : {}),
              }}>
                {state.fuelPercent.toFixed(0)}
              </span>
            </HudRow>
            <div style={styles.barOuter}>
              <div style={{
                ...styles.barFillFuel,
                width: `${state.fuelPercent}%`,
                ...(state.fuelPercent < 5 ? styles.barCritical : state.fuelPercent < 15 ? styles.barLow : {}),
              }} />
            </div>

            <HudRow label="DPS THROTTLE" unit="%">
              <span style={styles.value}>{state.throttlePercent.toFixed(0)}</span>
            </HudRow>
            <div style={styles.barOuter}>
              <div style={{ ...styles.barFillThrottle, width: `${state.throttlePercent}%` }} />
            </div>

            <div style={{ marginTop: 12 }}>
              <div><span style={styles.label}>PITCH </span><span style={styles.value}>{state.pitchDeg.toFixed(1)}</span><span style={styles.label}> deg</span></div>
              <div><span style={styles.label}>ROLL  </span><span style={styles.value}>{state.rollDeg.toFixed(1)}</span><span style={styles.label}> deg</span></div>
              <div><span style={styles.label}>YAW   </span><span style={styles.value}>{state.yawDeg.toFixed(1)}</span><span style={styles.label}> deg</span></div>
            </div>

            <HudRow label="MASS" unit="KG">
              <span style={styles.value}>{Math.round(state.massKg)}</span>
            </HudRow>
          </div>

          {/* Bottom panel */}
          <div style={{ ...styles.hudPanel, bottom: 20, left: '50%', transform: 'translateX(-50%)', textAlign: 'center' as const, whiteSpace: 'nowrap' as const }}>
            <span style={styles.label}>{state.phase}</span>
            {'  \u2022  '}
            <span style={styles.label}>T+</span>
            <span style={{ ...styles.value, fontSize: 12 }}>{state.missionTime}</span>
          </div>

          {/* Contact light */}
          {/* Ground Control callout text */}
          {state.calloutText && (
            <div style={{
              position: 'absolute', bottom: '80px', left: '50%', transform: 'translateX(-50%)',
              background: 'rgba(0,0,0,0.75)', border: '1px solid rgba(255,170,0,0.3)',
              borderRadius: '6px', padding: '8px 20px',
              fontFamily: "'Courier New', monospace", fontSize: '13px',
              color: '#ffcc44', letterSpacing: '0.5px', whiteSpace: 'nowrap',
              animation: 'fadeIn 0.3s ease-out',
            }}>
              <span style={{ color: '#88aaff', marginRight: '8px' }}>HOUSTON:</span>
              {state.calloutText}
            </div>
          )}

          {/* Cinematic mode indicator */}
          {state.cinematicMode && (
            <div style={{
              position: 'absolute', top: '60px', left: '50%', transform: 'translateX(-50%)',
              background: 'rgba(0,0,0,0.6)', borderRadius: '4px', padding: '4px 14px',
              fontFamily: "'Courier New', monospace", fontSize: '11px',
              color: '#ff4444', letterSpacing: '2px', textTransform: 'uppercase',
            }}>
              CINEMATIC — press V to exit
            </div>
          )}

          {state.contactLight && (
            <div style={styles.contactLight}>CONTACT LIGHT</div>
          )}

          {/* Crosshair for cockpit */}
          {isCockpit && (
            <div style={styles.crosshair}>
              <div style={styles.crosshairPipH} />
              <div style={styles.crosshairPipV} />
              <div style={styles.crosshairDot} />
            </div>
          )}

          {/* Attitude indicator */}
          {showAttitude && (
            <div style={styles.attitudeIndicator}>
              <div style={{
                ...styles.aiHorizon,
                transform: `rotate(${-state.rollDeg}deg) translateY(${state.pitchDeg * 0.8}px)`,
              }} />
              <div style={styles.aiCenter} />
            </div>
          )}
        </>
      )}

      {/* ── Landing result overlay ── */}
      {isEnded && landingResult && (
        <div style={styles.endOverlay}>
          <h1 style={{
            ...styles.endTitle,
            color: landingResult.success ? '#44ff44' : '#ff4444',
            textShadow: `0 0 40px ${landingResult.success ? '#44ff44' : '#ff4444'}, 0 0 80px ${landingResult.success ? '#44ff44' : '#ff4444'}`,
          }}>
            {landingResult.success ? 'THE EAGLE HAS LANDED' : 'MISSION FAILURE'}
          </h1>
          <div style={{
            ...styles.endStats,
            color: landingResult.success ? '#88cc88' : '#cc8888',
          }}>
            <div><span style={styles.statLabel}>VERTICAL SPEED</span> {landingResult.stats.vSpeed} M/S</div>
            <div><span style={styles.statLabel}>HORIZONTAL SPEED</span> {landingResult.stats.hSpeed} M/S</div>
            <div><span style={styles.statLabel}>TILT AT CONTACT</span> {landingResult.stats.tilt} deg</div>
            <div><span style={styles.statLabel}>FUEL REMAINING</span> {landingResult.stats.fuel}%</div>
            <div><span style={styles.statLabel}>FLIGHT TIME</span> {landingResult.stats.time}S</div>
            <div><span style={styles.statLabel}>DISTANCE FROM LZ</span> {landingResult.stats.distance}M</div>
          </div>
          <div style={{
            ...styles.restartHint,
            color: landingResult.success ? '#44ff44' : '#ff4444',
          }}>
            PRESS R TO RESTART
          </div>
        </div>
      )}

      {/* ── Always-visible control hints (bottom center) ── */}
      {state.gameState === 'flying' && !helpVisible && (
        <div style={{
          position: 'absolute', bottom: '40px', left: '50%', transform: 'translateX(-50%)',
          display: 'flex', gap: '12px', flexWrap: 'wrap', justifyContent: 'center',
          background: 'rgba(0,0,0,0.5)', borderRadius: '8px', padding: '6px 14px',
          fontFamily: "'Courier New', monospace", fontSize: '9px', letterSpacing: '1px',
          color: 'rgba(255,170,0,0.5)', pointerEvents: 'none',
        }}>
          <span><b style={{color:'#ffaa00'}}>SHIFT</b> THRUST UP</span>
          <span><b style={{color:'#ffaa00'}}>CTRL</b> THRUST DOWN</span>
          <span><b style={{color:'#ffaa00'}}>ARROWS</b> STEER</span>
          <span><b style={{color:'#ffaa00'}}>A</b> AUTOPILOT{state.autopilot ? ' ON' : ''}</span>
          <span><b style={{color:'#ffaa00'}}>V</b> CINEMATIC</span>
          <span><b style={{color:'#ffaa00'}}>H</b> HELP</span>
        </div>
      )}

      {/* ── Help overlay ── */}
      {helpVisible && (
        <div style={styles.helpOverlay}>
          <h2 style={styles.helpTitle}>CONTROLS</h2>
          <div style={styles.helpDivider} />
          <div style={styles.helpSection}>ATTITUDE</div>
          <div><span style={styles.helpKey}>ARROW UP / DOWN</span> PITCH FORWARD / BACK</div>
          <div><span style={styles.helpKey}>ARROW LEFT / RIGHT</span> ROLL LEFT / RIGHT</div>
          <div><span style={styles.helpKey}>Q / E</span> YAW LEFT / RIGHT</div>
          <div style={styles.helpDivider} />
          <div style={styles.helpSection}>PROPULSION</div>
          <div><span style={styles.helpKey}>SHIFT</span> THROTTLE UP (+20%/S)</div>
          <div><span style={styles.helpKey}>CTRL</span> THROTTLE DOWN (-20%/S)</div>
          <div style={styles.helpDivider} />
          <div style={styles.helpSection}>RCS TRANSLATION</div>
          <div><span style={styles.helpKey}>SPACE + UP/DOWN</span> TRANSLATE FWD / BACK</div>
          <div><span style={styles.helpKey}>SPACE + LEFT/RIGHT</span> TRANSLATE LEFT / RIGHT</div>
          <div style={styles.helpDivider} />
          <div style={styles.helpSection}>SYSTEM</div>
          <div><span style={styles.helpKey}>C</span> CYCLE CAMERA MODE</div>
          <div><span style={styles.helpKey}>H</span> TOGGLE THIS HELP</div>
          <div><span style={styles.helpKey}>R</span> RESTART MISSION</div>
          <div style={styles.helpDivider} />
          <div style={{ textAlign: 'center', marginTop: 10, color: '#887030', fontSize: 10, letterSpacing: 2 }}>
            SAFE LANDING: V/S &lt; 2 M/S &bull; H/S &lt; 1 M/S &bull; TILT &lt; 15 deg
          </div>
        </div>
      )}
    </div>
  );
};

// ---------------------------------------------------------------------------
// HUD Row sub-component
// ---------------------------------------------------------------------------

const HudRow: React.FC<{ label: string; unit: string; children: React.ReactNode }> = ({ label, unit, children }) => (
  <div style={styles.hudRow}>
    <span style={styles.label}>{label}</span><br />
    {children} <span style={styles.label}>{unit}</span>
  </div>
);

// ---------------------------------------------------------------------------
// Inline styles — amber CRT aesthetic
// ---------------------------------------------------------------------------

const AMBER = '#d4a017';
const AMBER_DIM = '#887030';

const styles: Record<string, React.CSSProperties> = {
  overlay: {
    position: 'fixed',
    inset: 0,
    zIndex: 9000,
    background: '#000',
    fontFamily: "'Courier New', monospace",
    userSelect: 'none',
    overflow: 'hidden',
  },

  // Header
  header: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    height: 40,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: '0 16px',
    background: 'rgba(0,0,0,0.85)',
    borderBottom: `1px solid rgba(212,160,23,0.25)`,
    zIndex: 9010,
    color: AMBER,
    fontSize: 13,
    letterSpacing: 1,
  },
  headerLeft: {
    display: 'flex',
    alignItems: 'center',
    gap: 10,
  },
  headerRight: {
    display: 'flex',
    alignItems: 'center',
    gap: 8,
  },
  title: {
    fontSize: 14,
    letterSpacing: 3,
    fontWeight: 300,
  },
  statusBadge: {
    fontSize: 10,
    letterSpacing: 2,
    padding: '2px 8px',
    borderRadius: 2,
    fontWeight: 'bold',
  },
  statusActive: {
    color: '#44ff44',
    border: '1px solid rgba(68,255,68,0.4)',
  },
  statusSuccess: {
    color: '#44ff44',
    border: '1px solid rgba(68,255,68,0.4)',
  },
  statusFail: {
    color: '#ff4444',
    border: '1px solid rgba(255,68,68,0.4)',
  },
  btn: {
    background: 'transparent',
    border: `1px solid rgba(212,160,23,0.4)`,
    color: AMBER,
    fontFamily: "'Courier New', monospace",
    fontSize: 11,
    letterSpacing: 2,
    padding: '4px 12px',
    cursor: 'pointer',
    display: 'flex',
    alignItems: 'center',
    gap: 4,
  },
  closeBtn: {
    background: 'transparent',
    border: 'none',
    color: AMBER,
    cursor: 'pointer',
    padding: 4,
    display: 'flex',
    alignItems: 'center',
  },

  // Canvas
  canvasContainer: {
    position: 'absolute',
    top: 0,
    left: 0,
    width: '100%',
    height: '100%',
  },

  // Scanlines
  scanlines: {
    position: 'absolute',
    top: 0,
    left: 0,
    width: '100%',
    height: '100%',
    pointerEvents: 'none',
    zIndex: 9001,
    background: `repeating-linear-gradient(
      0deg,
      transparent,
      transparent 2px,
      rgba(0,0,0,0.03) 2px,
      rgba(0,0,0,0.03) 4px
    )`,
    opacity: 0.5,
  },

  // HUD panels
  hudPanel: {
    position: 'absolute',
    padding: '12px 16px',
    background: 'rgba(0,0,0,0.7)',
    border: `1px solid rgba(212,160,23,0.25)`,
    color: AMBER,
    fontSize: 13,
    lineHeight: '1.7',
    letterSpacing: 1,
    textShadow: '0 0 6px rgba(212,160,23,0.25)',
    borderRadius: 2,
    backdropFilter: 'blur(2px)',
    zIndex: 9002,
    pointerEvents: 'none',
  },
  hudRow: {
    marginTop: 8,
  },
  label: {
    color: AMBER_DIM,
    fontSize: 10,
    letterSpacing: 2,
    textTransform: 'uppercase' as const,
  },
  value: {
    color: AMBER,
    fontSize: 16,
    fontWeight: 'bold',
    fontVariantNumeric: 'tabular-nums',
  },
  valueWarn: {
    color: '#ff4444',
    textShadow: '0 0 8px rgba(255,68,68,0.4)',
  },
  valueCaution: {
    color: '#ffaa22',
  },

  // Bars
  barOuter: {
    width: '100%',
    height: 8,
    background: 'rgba(100,80,20,0.2)',
    border: '1px solid rgba(212,160,23,0.3)',
    marginTop: 4,
    borderRadius: 1,
  },
  barFillFuel: {
    height: '100%',
    background: 'linear-gradient(90deg, #d4a017, #e8c040)',
    borderRadius: 1,
    transition: 'width 0.08s linear',
  },
  barFillThrottle: {
    height: '100%',
    background: 'linear-gradient(90deg, #2266cc, #44aaff)',
    borderRadius: 1,
    transition: 'width 0.08s linear',
  },
  barLow: {
    background: 'linear-gradient(90deg, #ff2222, #ff6644)',
  },
  barCritical: {
    background: '#ff2222',
    animation: 'lunar-blink 0.5s infinite',
  },

  // Contact light
  contactLight: {
    position: 'absolute',
    bottom: 90,
    left: '50%',
    transform: 'translateX(-50%)',
    color: '#44ff44',
    fontSize: 26,
    letterSpacing: 8,
    textShadow: '0 0 30px rgba(68,255,68,0.7), 0 0 60px rgba(68,255,68,0.3)',
    fontWeight: 'bold',
    zIndex: 9002,
    pointerEvents: 'none',
    animation: 'lunar-blink-contact 0.4s infinite',
  },

  // Crosshair
  crosshair: {
    position: 'absolute',
    top: '50%',
    left: '50%',
    transform: 'translate(-50%, -50%)',
    width: 60,
    height: 60,
    zIndex: 9002,
    pointerEvents: 'none',
  },
  crosshairPipH: {
    position: 'absolute',
    width: '100%',
    height: 1,
    top: '50%',
    left: 0,
    background: 'rgba(212,160,23,0.4)',
  },
  crosshairPipV: {
    position: 'absolute',
    width: 1,
    height: '100%',
    top: 0,
    left: '50%',
    background: 'rgba(212,160,23,0.4)',
  },
  crosshairDot: {
    position: 'absolute',
    width: 4,
    height: 4,
    borderRadius: '50%',
    background: 'rgba(212,160,23,0.6)',
    top: '50%',
    left: '50%',
    transform: 'translate(-50%, -50%)',
  },

  // Attitude indicator
  attitudeIndicator: {
    position: 'absolute',
    bottom: 20,
    right: 20,
    width: 120,
    height: 120,
    border: '1px solid rgba(212,160,23,0.3)',
    borderRadius: '50%',
    overflow: 'hidden',
    background: 'rgba(0,0,0,0.6)',
    zIndex: 9002,
    pointerEvents: 'none',
  },
  aiHorizon: {
    position: 'absolute',
    width: '200%',
    height: '200%',
    left: '-50%',
    top: '-50%',
    background: `linear-gradient(180deg,
      rgba(40,60,120,0.4) 0%, rgba(40,60,120,0.4) 50%,
      rgba(80,60,30,0.4) 50%, rgba(80,60,30,0.4) 100%
    )`,
    transformOrigin: 'center center',
  },
  aiCenter: {
    position: 'absolute',
    top: '50%',
    left: '50%',
    transform: 'translate(-50%, -50%)',
    width: 30,
    height: 2,
    background: AMBER,
  },

  // End screen
  endOverlay: {
    position: 'absolute',
    top: 0,
    left: 0,
    width: '100%',
    height: '100%',
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    zIndex: 9005,
    background: 'radial-gradient(ellipse at center, rgba(0,0,0,0.75) 0%, rgba(0,0,0,0.92) 80%)',
    fontFamily: "'Courier New', monospace",
    pointerEvents: 'none',
  },
  endTitle: {
    fontSize: 40,
    letterSpacing: 6,
    fontWeight: 300,
    margin: 0,
  },
  endStats: {
    fontSize: 13,
    lineHeight: '2.2',
    letterSpacing: 2,
    margin: '24px 0',
    textAlign: 'left' as const,
  },
  statLabel: {
    opacity: 0.5,
    display: 'inline-block',
    minWidth: 180,
  },
  restartHint: {
    fontSize: 13,
    letterSpacing: 4,
    marginTop: 30,
    opacity: 0.7,
  },

  // Help overlay
  helpOverlay: {
    position: 'absolute',
    top: '50%',
    left: '50%',
    transform: 'translate(-50%, -50%)',
    background: 'rgba(0,0,0,0.92)',
    border: '1px solid rgba(212,160,23,0.4)',
    padding: '30px 44px',
    color: AMBER,
    fontSize: 13,
    lineHeight: '2.2',
    letterSpacing: 1,
    zIndex: 9010,
    minWidth: 420,
    backdropFilter: 'blur(4px)',
    fontFamily: "'Courier New', monospace",
  },
  helpTitle: {
    textAlign: 'center' as const,
    marginBottom: 18,
    letterSpacing: 6,
    fontSize: 18,
    fontWeight: 300,
  },
  helpDivider: {
    width: '100%',
    height: 1,
    background: 'linear-gradient(90deg, transparent, rgba(212,160,23,0.3), transparent)',
    margin: '8px 0',
  },
  helpSection: {
    color: AMBER_DIM,
    fontSize: 10,
    letterSpacing: 3,
    marginTop: 8,
  },
  helpKey: {
    color: '#44aaff',
    display: 'inline-block',
    minWidth: 160,
  },
};

// ---------------------------------------------------------------------------
// Global keyframe styles (injected once)
// ---------------------------------------------------------------------------

const STYLE_ID = 'lunar-lander-keyframes';
if (typeof document !== 'undefined' && !document.getElementById(STYLE_ID)) {
  const styleEl = document.createElement('style');
  styleEl.id = STYLE_ID;
  styleEl.textContent = `
    @keyframes lunar-blink {
      0%, 100% { opacity: 1; }
      50% { opacity: 0.3; }
    }
    @keyframes lunar-blink-contact {
      0%, 100% { opacity: 1; }
      50% { opacity: 0.2; }
    }
  `;
  document.head.appendChild(styleEl);
}
