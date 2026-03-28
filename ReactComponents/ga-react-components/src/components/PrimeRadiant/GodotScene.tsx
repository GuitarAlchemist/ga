// src/components/PrimeRadiant/GodotScene.tsx
// Embeds the Godot Prime Radiant 3D scene via iframe (HTML5 web export).
// Supports two modes:
//   - Side panel: compact iframe docked in the standard panel slot
//   - Fullscreen overlay: immersive view replacing the entire viewport
// PostMessage bridge for React <-> Godot governance data exchange.

import React, { useCallback, useEffect, useRef, useState } from 'react';
import { GodotSceneBuilder } from './GodotSceneBuilder';

// ---------------------------------------------------------------------------
// Message protocol — React <-> Godot postMessage bridge
// ---------------------------------------------------------------------------

/** Messages sent from React into the Godot iframe */
export type GodotInboundMessage =
  | { type: 'governance:update'; nodes: unknown[]; edges: unknown[] }
  | { type: 'governance:select'; nodeId: string }
  | { type: 'governance:algedonic'; signal: unknown }
  | { type: 'governance:belief'; nodeId: string; state: string; confidence: number };

/** Messages received from Godot */
export type GodotOutboundMessage =
  | { type: 'godot:ready' }
  | { type: 'godot:node-clicked'; nodeId: string }
  | { type: 'godot:algedonic'; severity: string; description: string }
  | { type: 'godot:camera-changed'; position: { x: number; y: number; z: number } };

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------

export interface GodotSceneProps {
  /** URL of the Godot HTML5 export index.html */
  src?: string;
  /** Display mode */
  mode: 'panel' | 'fullscreen';
  /** Called when Godot reports a node click */
  onNodeClick?: (nodeId: string) => void;
  /** Called when user requests closing the fullscreen overlay */
  onClose?: () => void;
  /** Called when Godot runtime signals ready */
  onReady?: () => void;
  /** Called when user clicks "expand" in panel mode */
  onExpand?: () => void;
}

// Default location for the Godot web export (served from public/ or a known path)
const DEFAULT_GODOT_SRC = '/godot/index.html';

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export const GodotScene: React.FC<GodotSceneProps> = ({
  src = DEFAULT_GODOT_SRC,
  mode,
  onNodeClick,
  onClose,
  onReady,
  onExpand,
}) => {
  const iframeRef = useRef<HTMLIFrameElement>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const readyRef = useRef(false);

  // --- Inbound message handler (Godot -> React) ---
  useEffect(() => {
    const handler = (ev: MessageEvent) => {
      // Only accept messages from our iframe origin
      if (iframeRef.current && ev.source !== iframeRef.current.contentWindow) return;

      const msg = ev.data as GodotOutboundMessage;
      if (!msg || typeof msg.type !== 'string') return;

      switch (msg.type) {
        case 'godot:ready':
          readyRef.current = true;
          setLoading(false);
          onReady?.();
          break;
        case 'godot:node-clicked':
          onNodeClick?.(msg.nodeId);
          break;
        default:
          break;
      }
    };

    window.addEventListener('message', handler);
    return () => window.removeEventListener('message', handler);
  }, [onNodeClick, onReady]);

  // --- Outbound: send message to Godot ---
  const postToGodot = useCallback((msg: GodotInboundMessage) => {
    if (!iframeRef.current?.contentWindow || !readyRef.current) return;
    iframeRef.current.contentWindow.postMessage(msg, '*');
  }, []);

  // Expose postToGodot via ref for parent components
  useEffect(() => {
    const iframe = iframeRef.current;
    if (iframe) {
      (iframe as unknown as { postToGodot: typeof postToGodot }).postToGodot = postToGodot;
    }
  }, [postToGodot]);

  // --- Loading timeout: if Godot doesn't signal ready within 15s, show fallback ---
  useEffect(() => {
    const timeout = setTimeout(() => {
      if (!readyRef.current) {
        setLoading(false);
        // Don't set error — iframe may still be loading WASM
      }
    }, 15000);
    return () => clearTimeout(timeout);
  }, []);

  // --- Handle iframe load error ---
  const handleError = useCallback(() => {
    setError('Failed to load Godot scene. Ensure the HTML5 export is built and served.');
    setLoading(false);
  }, []);

  // --- Keyboard: Escape closes fullscreen ---
  useEffect(() => {
    if (mode !== 'fullscreen') return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose?.();
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [mode, onClose]);

  // Panel tab state
  const [panelTab, setPanelTab] = useState<'viewer' | 'builder'>('builder');

  // ------------------------------------------------------------------
  // Panel mode — tabbed: Viewer (iframe) | Builder (scene commands)
  // ------------------------------------------------------------------
  if (mode === 'panel') {
    return (
      <div className="godot-scene godot-scene--panel">
        <div className="godot-scene__header">
          <div className="godot-scene__tabs">
            <button
              className={`godot-scene__tab ${panelTab === 'builder' ? 'godot-scene__tab--active' : ''}`}
              onClick={() => setPanelTab('builder')}
            >Builder</button>
            <button
              className={`godot-scene__tab ${panelTab === 'viewer' ? 'godot-scene__tab--active' : ''}`}
              onClick={() => setPanelTab('viewer')}
            >Viewer</button>
          </div>
          <div className="godot-scene__header-actions">
            <button
              className="godot-scene__expand-btn"
              onClick={onExpand}
              aria-label="Expand to fullscreen"
              title="Fullscreen"
            >
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="15 3 21 3 21 9" />
                <polyline points="9 21 3 21 3 15" />
                <line x1="21" y1="3" x2="14" y2="10" />
                <line x1="3" y1="21" x2="10" y2="14" />
              </svg>
            </button>
          </div>
        </div>

        {panelTab === 'builder' && <GodotSceneBuilder />}

        {panelTab === 'viewer' && (
          <>
            {error ? (
              <div className="godot-scene__error">
                <p>{error}</p>
                <p className="godot-scene__hint">
                  Run: <code>godot --headless --export-release "Web" ./export/web/index.html</code>
                </p>
              </div>
            ) : (
              <>
                {loading && (
                  <div className="godot-scene__loading">
                    <div className="godot-scene__spinner" />
                    <span>Loading Godot engine...</span>
                  </div>
                )}
                <iframe
                  ref={iframeRef}
                  className="godot-scene__iframe"
                  src={src}
                  onError={handleError}
                  allow="autoplay; fullscreen; cross-origin-isolated"
                  sandbox="allow-scripts allow-same-origin allow-popups"
                  title="Prime Radiant — Godot 3D"
                />
              </>
            )}
          </>
        )}
      </div>
    );
  }

  // ------------------------------------------------------------------
  // Fullscreen overlay mode
  // ------------------------------------------------------------------
  return (
    <div className="godot-scene__overlay" onClick={(e) => { if (e.target === e.currentTarget) onClose?.(); }}>
      <div className="godot-scene godot-scene--fullscreen">
        <div className="godot-scene__fs-toolbar">
          <span className="godot-scene__fs-title">Prime Radiant — Godot 3D</span>
          <button
            className="godot-scene__fs-close"
            onClick={onClose}
            aria-label="Close fullscreen"
          >
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
              <line x1="18" y1="6" x2="6" y2="18" />
              <line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </button>
        </div>

        {error ? (
          <div className="godot-scene__error">
            <p>{error}</p>
            <p className="godot-scene__hint">
              Run: <code>godot --headless --export-release "Web" ./export/web/index.html</code>
            </p>
          </div>
        ) : (
          <>
            {loading && (
              <div className="godot-scene__loading godot-scene__loading--fullscreen">
                <div className="godot-scene__spinner" />
                <span>Loading Godot engine...</span>
              </div>
            )}
            <iframe
              ref={iframeRef}
              className="godot-scene__iframe"
              src={src}
              onError={handleError}
              allow="autoplay; fullscreen; cross-origin-isolated"
              sandbox="allow-scripts allow-same-origin allow-popups"
              title="Prime Radiant — Godot 3D"
            />
          </>
        )}
      </div>
    </div>
  );
};
