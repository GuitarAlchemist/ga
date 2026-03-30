// src/components/PrimeRadiant/ScreenshotPreview.tsx
// Thumbnail overlay showing the last screenshot capture.
// Auto-hides after 5 seconds. Click to expand, download button included.

import React, { useEffect, useState, useCallback, useRef } from 'react';

export interface ScreenshotPreviewProps {
  /** Base64 data URL of the captured image. */
  image: string | null;
  /** Called when the preview dismisses itself. */
  onDismiss: () => void;
  /** Auto-hide delay in ms. Default: 5000. */
  autoHideMs?: number;
}

export const ScreenshotPreview: React.FC<ScreenshotPreviewProps> = ({
  image,
  onDismiss,
  autoHideMs = 5000,
}) => {
  const [expanded, setExpanded] = useState(false);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Auto-hide after delay (only when not expanded)
  useEffect(() => {
    if (!image || expanded) return;
    timerRef.current = setTimeout(() => {
      onDismiss();
    }, autoHideMs);
    return () => {
      if (timerRef.current) clearTimeout(timerRef.current);
    };
  }, [image, expanded, autoHideMs, onDismiss]);

  const handleDownload = useCallback(() => {
    if (!image) return;
    const link = document.createElement('a');
    link.href = image;
    link.download = `prime-radiant-${new Date().toISOString().replace(/[:.]/g, '-')}.png`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }, [image]);

  const handleToggleExpand = useCallback(() => {
    setExpanded(prev => !prev);
  }, []);

  const handleClose = useCallback(() => {
    setExpanded(false);
    onDismiss();
  }, [onDismiss]);

  if (!image) return null;

  return (
    <>
      {/* Backdrop when expanded */}
      {expanded && (
        <div className="screenshot__backdrop" onClick={handleClose} />
      )}

      <div className={`screenshot__preview ${expanded ? 'screenshot__preview--expanded' : ''}`}>
        <img
          className="screenshot__image"
          src={image}
          alt="Prime Radiant screenshot"
          onClick={handleToggleExpand}
        />
        <div className="screenshot__controls">
          {/* Download button */}
          <button
            className="screenshot__btn"
            onClick={handleDownload}
            title="Download screenshot"
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
              <polyline points="7 10 12 15 17 10" />
              <line x1="12" y1="15" x2="12" y2="3" />
            </svg>
          </button>
          {/* Close button */}
          <button
            className="screenshot__btn"
            onClick={handleClose}
            title="Dismiss"
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <line x1="18" y1="6" x2="6" y2="18" />
              <line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </button>
        </div>
      </div>
    </>
  );
};
