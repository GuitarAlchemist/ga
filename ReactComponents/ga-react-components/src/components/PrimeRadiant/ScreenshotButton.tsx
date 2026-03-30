// src/components/PrimeRadiant/ScreenshotButton.tsx
// Camera icon button for the health bar area.
// Click: capture and show preview.
// Long-press (500ms): capture and POST to backend.

import React, { useCallback, useRef, useState } from 'react';
import { captureAndPost, useScreenshotCapture } from './ScreenshotCapture';
import { ScreenshotPreview } from './ScreenshotPreview';

export interface ScreenshotButtonProps {
  /** POST endpoint override. */
  endpoint?: string;
}

export const ScreenshotButton: React.FC<ScreenshotButtonProps> = ({ endpoint }) => {
  const { capture, capturing, lastCapture, clearCapture } = useScreenshotCapture();
  const [flash, setFlash] = useState(false);
  const longPressTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const didLongPressRef = useRef(false);

  const triggerFlash = useCallback(() => {
    setFlash(true);
    setTimeout(() => setFlash(false), 300);
  }, []);

  const handlePointerDown = useCallback(() => {
    didLongPressRef.current = false;
    longPressTimerRef.current = setTimeout(() => {
      didLongPressRef.current = true;
      triggerFlash();
      void captureAndPost(endpoint);
    }, 500);
  }, [endpoint, triggerFlash]);

  const handlePointerUp = useCallback(() => {
    if (longPressTimerRef.current) {
      clearTimeout(longPressTimerRef.current);
      longPressTimerRef.current = null;
    }
    // Short click: capture and show preview
    if (!didLongPressRef.current) {
      triggerFlash();
      void capture();
    }
  }, [capture, triggerFlash]);

  const handlePointerLeave = useCallback(() => {
    if (longPressTimerRef.current) {
      clearTimeout(longPressTimerRef.current);
      longPressTimerRef.current = null;
    }
  }, []);

  return (
    <>
      {/* Flash overlay */}
      {flash && <div className="screenshot__flash" />}

      {/* Camera button */}
      <span
        className={`prime-radiant__health-metric screenshot__button ${capturing ? 'screenshot__button--capturing' : ''}`}
        title="Capture screenshot (hold to POST)"
        onPointerDown={handlePointerDown}
        onPointerUp={handlePointerUp}
        onPointerLeave={handlePointerLeave}
      >
        <svg
          width="12"
          height="12"
          viewBox="0 0 24 24"
          fill="none"
          stroke={capturing ? '#58a6ff' : '#c9d1d9'}
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
        >
          <path d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z" />
          <circle cx="12" cy="13" r="4" />
        </svg>
      </span>

      {/* Preview overlay */}
      <ScreenshotPreview image={lastCapture} onDismiss={clearCapture} />
    </>
  );
};
