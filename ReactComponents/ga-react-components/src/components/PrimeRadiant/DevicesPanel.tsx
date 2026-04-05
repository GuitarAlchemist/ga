// src/components/PrimeRadiant/DevicesPanel.tsx
// HUD chip + popover for picking audio input/output devices and casting the tab.
// Casting uses the Presentation API (generic), which delegates to the OS/Chrome
// cast integration — works with Chromecast on Android Chrome and desktop Chrome.

import { useCallback, useEffect, useRef, useState } from 'react';
import { useDevices } from './DeviceContext';

export function DevicesPanel(): JSX.Element {
  const { outputSinkId, setOutputSinkId, outputLabel, outputDevices, inputDeviceId, setInputDeviceId, inputDevices, refreshDevices } = useDevices();
  const [open, setOpen] = useState(false);
  const [castSupported, setCastSupported] = useState(false);
  const [castActive, setCastActive] = useState(false);
  const connectionRef = useRef<PresentationConnection | null>(null);
  const rootRef = useRef<HTMLDivElement | null>(null);

  // Presentation API support check
  useEffect(() => {
    setCastSupported(typeof navigator !== 'undefined' && 'presentation' in navigator && typeof PresentationRequest !== 'undefined');
  }, []);

  // Close on outside click
  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => {
      if (rootRef.current && !rootRef.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', onDown);
    return () => document.removeEventListener('mousedown', onDown);
  }, [open]);

  const onOpen = useCallback(async () => {
    const next = !open;
    setOpen(next);
    if (next) {
      // Browsers hide device labels until mic/camera permission is granted.
      // If labels look empty, prompt once to unlock them.
      const needLabels = outputDevices.some(d => !d.label) || inputDevices.some(d => !d.label);
      if (needLabels) {
        try {
          const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
          stream.getTracks().forEach(t => t.stop());
          await refreshDevices();
        } catch {
          /* user denied — labels stay blank, deviceIds still usable */
        }
      }
    }
  }, [open, outputDevices, inputDevices, refreshDevices]);

  const startCast = useCallback(async () => {
    try {
      // Cast the current URL — Cast receiver displays the page.
      const req = new PresentationRequest([window.location.href]);
      const connection = await req.start();
      connectionRef.current = connection;
      setCastActive(connection.state === 'connected');
      connection.onconnect = () => setCastActive(true);
      connection.onclose = () => setCastActive(false);
      connection.onterminate = () => setCastActive(false);
    } catch (err) {
      console.warn('[cast] start failed:', err);
    }
  }, []);

  const stopCast = useCallback(() => {
    connectionRef.current?.terminate();
    connectionRef.current = null;
    setCastActive(false);
  }, []);

  // Trim device labels — macOS/Chrome prepend "Default - " and "Communications - "
  const labelOf = (d: MediaDeviceInfo): string => d.label.replace(/^(Default|Communications)\s*-\s*/, '') || `Device ${d.deviceId.slice(0, 6)}`;

  return (
    <div ref={rootRef} className="prime-radiant__devices">
      <button
        className="prime-radiant__devices-chip"
        onClick={onOpen}
        title={`Output: ${outputLabel}${castActive ? ' · Casting' : ''}`}
      >
        <span aria-hidden="true">🎧</span>
        <span className="prime-radiant__devices-chip-label">{outputLabel}</span>
        {castActive && <span aria-hidden="true">📺</span>}
      </button>
      {open && (
        <div className="prime-radiant__devices-panel" onClick={(e) => e.stopPropagation()}>
          <div className="prime-radiant__devices-section">
            <label className="prime-radiant__devices-label">🔊 Speaker (TTS output)</label>
            <select
              className="prime-radiant__devices-select"
              value={outputSinkId}
              onChange={(e) => setOutputSinkId(e.target.value)}
            >
              <option value="">System default</option>
              {outputDevices.map(d => (
                <option key={d.deviceId} value={d.deviceId}>{labelOf(d)}</option>
              ))}
            </select>
          </div>
          <div className="prime-radiant__devices-section">
            <label className="prime-radiant__devices-label">🎤 Microphone</label>
            <select
              className="prime-radiant__devices-select"
              value={inputDeviceId}
              onChange={(e) => setInputDeviceId(e.target.value)}
            >
              <option value="">System default</option>
              {inputDevices.map(d => (
                <option key={d.deviceId} value={d.deviceId}>{labelOf(d)}</option>
              ))}
            </select>
            <div className="prime-radiant__devices-hint">
              Voice recognition uses the system default mic — selection here is informational on most browsers.
            </div>
          </div>
          <div className="prime-radiant__devices-section">
            <label className="prime-radiant__devices-label">📺 Cast</label>
            {!castSupported && (
              <div className="prime-radiant__devices-hint">Cast not supported — use Chrome menu → Cast.</div>
            )}
            {castSupported && !castActive && (
              <button className="prime-radiant__devices-btn" onClick={() => void startCast()}>
                Cast to TV
              </button>
            )}
            {castSupported && castActive && (
              <button className="prime-radiant__devices-btn prime-radiant__devices-btn--stop" onClick={stopCast}>
                Stop casting
              </button>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
