// src/components/PrimeRadiant/ScreenshotCapture.ts
// Standalone screenshot capture utility for the Prime Radiant.
// Used by Demerzel governance for visual reports and dashboards.

import { useState, useCallback, useRef } from 'react';

// ─── Core capture functions ──────────────────────────────────────────────────

/**
 * Capture the Three.js / force-graph canvas as a base64 PNG string.
 * Returns the full data URL (data:image/png;base64,...).
 *
 * WebGPURenderer (and WebGL without preserveDrawingBuffer) clears the
 * back-buffer after each frame, so canvas.toDataURL() returns blank.
 * Fix: render one more frame via the force-graph tick, then capture
 * immediately within the same task. If still blank, fall back to
 * reading pixels from an OffscreenCanvas via drawImage.
 */
export async function captureCanvas(): Promise<string> {
  const canvas = document.querySelector('.prime-radiant__canvas-area canvas') as HTMLCanvasElement | null;
  if (!canvas) {
    throw new Error('No canvas element found in .prime-radiant__canvas-area');
  }

  // Strategy 1: Try direct toDataURL (works with preserveDrawingBuffer)
  let dataUrl = canvas.toDataURL('image/png');
  if (!isBlankDataUrl(dataUrl)) return dataUrl;

  // Strategy 2: Copy via drawImage to an OffscreenCanvas (captures current
  // framebuffer content even without preserveDrawingBuffer on some browsers)
  try {
    const offscreen = document.createElement('canvas');
    offscreen.width = canvas.width;
    offscreen.height = canvas.height;
    const ctx = offscreen.getContext('2d');
    if (ctx) {
      ctx.drawImage(canvas, 0, 0);
      dataUrl = offscreen.toDataURL('image/png');
      if (!isBlankDataUrl(dataUrl)) return dataUrl;
    }
  } catch {
    // drawImage can throw on tainted canvases — fall through
  }

  // Strategy 3: Request one animation frame then capture immediately
  // (renderer writes to back-buffer during rAF, we read before browser
  // clears it for the next present)
  return new Promise((resolve) => {
    requestAnimationFrame(() => {
      const result = canvas.toDataURL('image/png');
      resolve(result);
    });
  });
}

function isBlankDataUrl(dataUrl: string): boolean {
  // A blank PNG toDataURL is typically very short (<200 chars for any resolution)
  // or exactly the same as an empty canvas
  if (!dataUrl || dataUrl.length < 100) return true;
  // Check for all-transparent/all-black by sampling the base64 length
  // A 1920x1080 blank PNG is ~6KB; a real scene is typically >50KB
  const base64 = dataUrl.split(',')[1];
  if (!base64) return true;
  return base64.length < 1000;
}

/**
 * Capture the entire Prime Radiant viewport.
 * Draws the main container onto an offscreen canvas using drawImage on the 3D canvas
 * plus overlaying DOM text via a foreignObject SVG rasterization.
 * Falls back to just the 3D canvas if the full container capture fails.
 */
export async function captureFullPage(): Promise<string> {
  try {
    const container = document.querySelector('.prime-radiant') as HTMLElement | null;
    if (!container) {
      // Fallback: just capture the 3D canvas
      return captureCanvas();
    }

    const canvas3D = container.querySelector('canvas') as HTMLCanvasElement | null;
    if (!canvas3D) {
      throw new Error('No canvas found in .prime-radiant container');
    }

    const { width, height } = container.getBoundingClientRect();
    const offscreen = document.createElement('canvas');
    offscreen.width = Math.round(width * window.devicePixelRatio);
    offscreen.height = Math.round(height * window.devicePixelRatio);
    const ctx = offscreen.getContext('2d');
    if (!ctx) {
      return captureCanvas();
    }

    ctx.scale(window.devicePixelRatio, window.devicePixelRatio);

    // Draw the 3D canvas scaled to fill the container
    ctx.drawImage(canvas3D, 0, 0, width, height);

    // Overlay the HUD elements via SVG foreignObject
    try {
      const hudElements = container.querySelectorAll('.prime-radiant__health, .prime-radiant__galactic-clock');
      for (const hud of hudElements) {
        const rect = (hud as HTMLElement).getBoundingClientRect();
        const containerRect = container.getBoundingClientRect();
        const x = rect.left - containerRect.left;
        const y = rect.top - containerRect.top;

        // Create SVG with foreignObject for DOM content
        const svgData = `
          <svg xmlns="http://www.w3.org/2000/svg" width="${rect.width}" height="${rect.height}">
            <foreignObject width="100%" height="100%">
              <div xmlns="http://www.w3.org/1999/xhtml">${(hud as HTMLElement).outerHTML}</div>
            </foreignObject>
          </svg>`;
        const svgBlob = new Blob([svgData], { type: 'image/svg+xml;charset=utf-8' });
        const svgUrl = URL.createObjectURL(svgBlob);

        const img = new Image();
        await new Promise<void>((resolve) => {
          img.onload = () => {
            ctx.drawImage(img, x, y, rect.width, rect.height);
            URL.revokeObjectURL(svgUrl);
            resolve();
          };
          img.onerror = () => {
            URL.revokeObjectURL(svgUrl);
            resolve(); // Skip this HUD element on error
          };
          img.src = svgUrl;
        });
      }
    } catch {
      // If HUD overlay fails, we still have the 3D canvas content
    }

    return offscreen.toDataURL('image/png');
  } catch {
    // Final fallback: just capture the 3D canvas
    return captureCanvas();
  }
}

// ─── POST to backend ─────────────────────────────────────────────────────────

export interface ScreenshotPayload {
  image: string;
  timestamp: string;
  viewport: { width: number; height: number };
}

/**
 * Capture the canvas and POST the screenshot to the backend.
 * Fire-and-forget — errors are logged but not thrown.
 */
export async function captureAndPost(endpoint?: string): Promise<void> {
  const url = endpoint ?? '/api/governance/screenshots';
  try {
    const image = await captureCanvas();
    const canvas = document.querySelector('.prime-radiant__canvas-area canvas') as HTMLCanvasElement | null;
    const payload: ScreenshotPayload = {
      image,
      timestamp: new Date().toISOString(),
      viewport: {
        width: canvas?.width ?? 0,
        height: canvas?.height ?? 0,
      },
    };
    await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
  } catch (err) {
    console.warn('[ScreenshotCapture] POST failed:', err);
  }
}

// ─── React hook ──────────────────────────────────────────────────────────────

export interface UseScreenshotCaptureResult {
  /** Trigger a canvas capture. Returns the base64 data URL. */
  capture: () => Promise<string | null>;
  /** True while a capture is in progress. */
  capturing: boolean;
  /** The most recent capture as a base64 data URL, or null. */
  lastCapture: string | null;
  /** Clear the last capture. */
  clearCapture: () => void;
}

export function useScreenshotCapture(): UseScreenshotCaptureResult {
  const [capturing, setCapturing] = useState(false);
  const [lastCapture, setLastCapture] = useState<string | null>(null);
  const lockRef = useRef(false);

  const capture = useCallback(async (): Promise<string | null> => {
    if (lockRef.current) return null;
    lockRef.current = true;
    setCapturing(true);
    try {
      const result = await captureCanvas();
      setLastCapture(result);
      return result;
    } catch (err) {
      console.warn('[ScreenshotCapture] Capture failed:', err);
      return null;
    } finally {
      setCapturing(false);
      lockRef.current = false;
    }
  }, []);

  const clearCapture = useCallback(() => {
    setLastCapture(null);
  }, []);

  return { capture, capturing, lastCapture, clearCapture };
}
