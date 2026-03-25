// src/components/PrimeRadiant/TutorialOverlay.tsx
// Floating "?" help button + full-screen tutorial overlay for Prime Radiant

import React, { useCallback, useEffect, useState } from 'react';

// TODO: Replace with actual Discord invite link
const DISCORD_INVITE = 'https://discord.gg/YOUR_INVITE';

export const TutorialOverlay: React.FC = () => {
  const [open, setOpen] = useState(false);

  const handleOpen = useCallback(() => setOpen(true), []);
  const handleClose = useCallback(() => setOpen(false), []);

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open]);

  return (
    <>
      {/* Floating help button */}
      <button
        className="prime-radiant__tutorial-btn"
        onClick={handleOpen}
        title="Help &amp; Tutorial"
        aria-label="Open tutorial"
      >
        ?
      </button>

      {/* Tutorial overlay */}
      {open && (
        <div
          className="prime-radiant__tutorial-overlay"
          onClick={handleClose}
          role="dialog"
          aria-modal="true"
        >
          <div
            className="prime-radiant__tutorial-card"
            onClick={(e) => e.stopPropagation()}
          >
            <button
              className="prime-radiant__tutorial-close"
              onClick={handleClose}
              aria-label="Close tutorial"
            >
              &times;
            </button>

            <h2 className="prime-radiant__tutorial-title">
              Prime Radiant
            </h2>
            <p className="prime-radiant__tutorial-subtitle">
              Governance Visualization Engine
            </p>

            <div className="prime-radiant__tutorial-section">
              <h3>Navigation</h3>
              <ul>
                <li><kbd>Drag</kbd> Orbit the graph</li>
                <li><kbd>Scroll</kbd> Zoom in / out</li>
                <li><kbd>Right-drag</kbd> Pan the view</li>
              </ul>
            </div>

            <div className="prime-radiant__tutorial-section">
              <h3>Nodes</h3>
              <p>
                Shapes represent artifact types. Colors indicate health:
              </p>
              <div className="prime-radiant__tutorial-colors">
                <span><span className="prime-radiant__tutorial-dot" style={{ background: '#3fb950' }} /> Healthy</span>
                <span><span className="prime-radiant__tutorial-dot" style={{ background: '#d29922' }} /> Warning</span>
                <span><span className="prime-radiant__tutorial-dot" style={{ background: '#f85149' }} /> Error</span>
                <span><span className="prime-radiant__tutorial-dot" style={{ background: '#8b949e' }} /> Unknown</span>
                <span><span className="prime-radiant__tutorial-dot" style={{ background: '#bc3fbc' }} /> Contradictory</span>
              </div>
              <p>Click a node to inspect details and file tree in the right panel.</p>
            </div>

            <div className="prime-radiant__tutorial-section">
              <h3>Companions</h3>
              <p>
                <strong>Demerzel</strong> (bottom-left face) — the governance AI.{' '}
                <strong>TARS</strong> (bottom-left robot) — the cognition engine.
              </p>
            </div>

            <div className="prime-radiant__tutorial-section">
              <h3>Chat</h3>
              <p>
                Click Demerzel's chat bubble (gold circle, bottom-left) to ask questions.
                Supports voice input (mic) and text-to-speech.
              </p>
            </div>

            <div className="prime-radiant__tutorial-section">
              <h3>HUD</h3>
              <ul>
                <li><strong>Trantor</strong> — holographic globe (top-right), capital of the Galactic Empire.</li>
                <li><strong>GST</strong> — Galactic Standard Time (top-left), Foundation Era calendar.</li>
                <li><strong>FPS</strong> — frame rate (top-left). Quality auto-adjusts for your GPU.</li>
              </ul>
            </div>

            <div className="prime-radiant__tutorial-section prime-radiant__tutorial-discord">
              {/* TODO: Replace DISCORD_INVITE with real invite link */}
              <a
                href={DISCORD_INVITE}
                target="_blank"
                rel="noopener noreferrer"
              >
                Join the Demerzel community on Discord
              </a>
            </div>
          </div>
        </div>
      )}
    </>
  );
};
