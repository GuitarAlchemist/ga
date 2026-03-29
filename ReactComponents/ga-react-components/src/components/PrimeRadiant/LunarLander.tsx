// src/components/PrimeRadiant/LunarLander.tsx
// Apollo LM Descent Simulator — fullscreen overlay triggered from Prime Radiant.
// Embeds the standalone simulation (public/lunar-lander/index.html) via iframe,
// communicating through the postMessage bridge (lunar:ready, lunar:landed, lunar:reset).

import React, { useCallback, useEffect, useRef, useState } from 'react';

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
  /** Custom src for the simulation HTML (defaults to /lunar-lander/index.html) */
  src?: string;
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export const LunarLander: React.FC<LunarLanderProps> = ({
  open,
  onClose,
  onLanded,
  src = '/lunar-lander/index.html',
}) => {
  const iframeRef = useRef<HTMLIFrameElement>(null);
  const [ready, setReady] = useState(false);
  const [landed, setLanded] = useState<{ success: boolean; stats: LunarLanderStats } | null>(null);

  // ── PostMessage bridge ──
  useEffect(() => {
    if (!open) return;

    const handler = (e: MessageEvent) => {
      if (!e.data || typeof e.data !== 'object') return;

      if (e.data.type === 'lunar:ready') {
        setReady(true);
      }

      if (e.data.type === 'lunar:landed') {
        const result = { success: !!e.data.success, stats: e.data.stats as LunarLanderStats };
        setLanded(result);
        onLanded?.(result.success, result.stats);
      }
    };

    window.addEventListener('message', handler);
    return () => window.removeEventListener('message', handler);
  }, [open, onLanded]);

  // ── Reset state on open ──
  useEffect(() => {
    if (open) {
      setReady(false);
      setLanded(null);
    }
  }, [open]);

  // ── Keyboard: Escape to close ──
  useEffect(() => {
    if (!open) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.preventDefault();
        onClose();
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [open, onClose]);

  // ── Send reset command to iframe ──
  const handleReset = useCallback(() => {
    iframeRef.current?.contentWindow?.postMessage({ type: 'lunar:reset' }, '*');
    setLanded(null);
  }, []);

  if (!open) return null;

  return (
    <div className="lunar-lander__overlay">
      <div className="lunar-lander__container">
        {/* Header bar */}
        <div className="lunar-lander__header">
          <div className="lunar-lander__header-left">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M12 3a6 6 0 0 0 0 12 9 9 0 0 0 9-9" />
              <circle cx="12" cy="9" r="1" fill="currentColor" />
            </svg>
            <span className="lunar-lander__title">LUNAR DESCENT SIMULATOR</span>
            {ready && !landed && (
              <span className="lunar-lander__status lunar-lander__status--active">LIVE</span>
            )}
            {landed && (
              <span className={`lunar-lander__status ${landed.success ? 'lunar-lander__status--success' : 'lunar-lander__status--fail'}`}>
                {landed.success ? 'LANDED' : 'CRASH'}
              </span>
            )}
          </div>
          <div className="lunar-lander__header-right">
            <button className="lunar-lander__btn" onClick={handleReset} title="Restart mission">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <polyline points="23 4 23 10 17 10" />
                <path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10" />
              </svg>
              RESTART
            </button>
            <button className="lunar-lander__close" onClick={onClose} title="Close (Esc)">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <line x1="18" y1="6" x2="6" y2="18" />
                <line x1="6" y1="6" x2="18" y2="18" />
              </svg>
            </button>
          </div>
        </div>

        {/* Iframe simulation */}
        <iframe
          ref={iframeRef}
          className="lunar-lander__iframe"
          src={src}
          title="Lunar Descent Simulator"
          allow="autoplay"
          sandbox="allow-scripts allow-same-origin"
        />
      </div>
    </div>
  );
};
