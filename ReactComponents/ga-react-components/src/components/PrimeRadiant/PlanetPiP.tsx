// src/components/PrimeRadiant/PlanetPiP.tsx
// Picture-in-Picture YouTube overlay — appears next to a planet when zoomed in.

import React, { useEffect, useRef, useState } from 'react';

// Planet → YouTube educational course mapping
// Curated courses: NASA explainers, university lectures, documentary deep-dives
const PLANET_VIDEOS: Record<string, { videoId: string; title: string }> = {
  sun:      { videoId: 'KhOaSHcb7IY', title: 'The Sun — Crash Course Astronomy' },
  mercury:  { videoId: 'P3GkZe3nRQ0', title: 'Mercury — Crash Course Astronomy' },
  venus:    { videoId: 'BvXa1n9fjow', title: 'Venus — Crash Course Astronomy' },
  earth:    { videoId: '0BnGJ3pKeDo', title: 'Earth — Crash Course Astronomy' },
  moon:     { videoId: 'UIKmSQqp8wY', title: 'The Moon — Crash Course Astronomy' },
  mars:     { videoId: 'D8pnmwOXhoY', title: 'Mars — Crash Course Astronomy' },
  jupiter:  { videoId: 'Xwn8fQSW7-8', title: 'Jupiter — Crash Course Astronomy' },
  saturn:   { videoId: 'E6lm8KdXw8o', title: 'Saturn — Crash Course Astronomy' },
  uranus:   { videoId: 'b_hUFkKV8xk', title: 'Uranus & Neptune — Crash Course' },
  neptune:  { videoId: 'b_hUFkKV8xk', title: 'Uranus & Neptune — Crash Course' },
  titan:    { videoId: 'tsnx4GRDKKI', title: 'Titan — Huygens Descent' },
};

interface PlanetPiPProps {
  planet: string | null;
  zoomDistance: number;
  screenX: number;
  screenY: number;
  containerWidth: number;
  containerHeight: number;
  /** When true, show regardless of zoom distance (manual toggle from PlanetNav) */
  forceShow?: boolean;
  onClose?: () => void;
}

const ZOOM_THRESHOLD = 8; // show PiP when camera is closer than this

export const PlanetPiP: React.FC<PlanetPiPProps> = ({
  planet, zoomDistance, screenX, screenY, containerWidth, containerHeight,
  forceShow = false, onClose,
}) => {
  const [dismissed, setDismissed] = useState<string | null>(null);
  const iframeRef = useRef<HTMLIFrameElement>(null);

  useEffect(() => { setDismissed(null); }, [planet]);

  if (!planet) return null;
  // Show if forced (button toggle) OR if zoomed in close enough
  if (!forceShow && zoomDistance > ZOOM_THRESHOLD) return null;
  if (dismissed === planet) return null;

  const video = PLANET_VIDEOS[planet];
  if (!video) return null;

  // Position: offset to the right of the planet's screen position
  const pipWidth = 280;
  const pipHeight = 158; // 16:9
  let left = screenX + 30;
  let top = screenY - pipHeight / 2;

  // Clamp to viewport
  if (left + pipWidth > containerWidth - 20) left = screenX - pipWidth - 30;
  if (top < 10) top = 10;
  if (top + pipHeight > containerHeight - 10) top = containerHeight - pipHeight - 10;

  return (
    <div
      style={{
        position: 'absolute',
        left, top,
        width: pipWidth, height: pipHeight,
        zIndex: 100,
        borderRadius: 8,
        overflow: 'hidden',
        border: '1px solid rgba(88, 166, 255, 0.4)',
        boxShadow: '0 4px 24px rgba(0,0,0,0.8)',
        background: '#000',
        transition: 'opacity 0.3s, transform 0.3s',
        pointerEvents: 'auto',
      }}
    >
      <iframe
        ref={iframeRef}
        width={pipWidth}
        height={pipHeight}
        src={`https://www.youtube.com/embed/${video.videoId}?autoplay=1&mute=1&controls=1&modestbranding=1&rel=0`}
        title={video.title}
        allow="autoplay; encrypted-media"
        style={{ border: 'none', display: 'block' }}
      />
      <button
        onClick={() => { setDismissed(planet); onClose?.(); }}
        style={{
          position: 'absolute', top: 4, right: 4,
          background: 'rgba(0,0,0,0.7)', border: 'none', color: '#8b949e',
          fontSize: 14, cursor: 'pointer', borderRadius: 4,
          width: 22, height: 22, display: 'flex', alignItems: 'center', justifyContent: 'center',
        }}
        title="Close"
      >
        ×
      </button>
      <div style={{
        position: 'absolute', bottom: 0, left: 0, right: 0,
        background: 'linear-gradient(transparent, rgba(0,0,0,0.8))',
        padding: '12px 8px 4px', fontSize: 9, color: '#8b949e',
        letterSpacing: 0.5,
      }}>
        {video.title}
      </div>
    </div>
  );
};
