/**
 * CastButton — minimal Google Cast / Chromecast overlay button.
 *
 * Loads the Cast Sender SDK on demand and surfaces a small "Cast" pill the
 * user can click to push the current demo URL to a Chromecast device on
 * the local network. Uses the Presentation API path (one-arg
 * PresentationRequest) so it works without a registered custom receiver
 * for the simple "cast this page" use case; falls back to logging a warn
 * line when neither API is available (older browsers, non-Chromium).
 *
 * Limitations to be aware of:
 *
 *  - Full visual fidelity on the Chromecast requires either (a) a custom
 *    Cast receiver registered for this origin via the Google Cast SDK
 *    Developer Console, or (b) Chrome's tab/screen mirroring (which is a
 *    user-initiated browser action, not script-callable). The button below
 *    triggers the browser's cast picker — what the device does after
 *    selection depends on the receiver registration state.
 *  - The button intentionally renders nothing until at least the
 *    Presentation API is detected, so it doesn't clutter the UI on
 *    browsers that can't cast.
 */

import React, { useEffect, useState } from 'react';
import { Box } from '@mui/material';
import CastIcon from '@mui/icons-material/Cast';
import CastConnectedIcon from '@mui/icons-material/CastConnected';

interface PresentationConnectionLike {
  state: 'connecting' | 'connected' | 'closed' | 'terminated';
  addEventListener: (type: string, listener: EventListenerOrEventListenerObject) => void;
}

interface PresentationRequestLike {
  start(): Promise<PresentationConnectionLike>;
}

declare global {
  interface Window {
    PresentationRequest?: new (urls: string[]) => PresentationRequestLike;
  }
}

export interface CastButtonProps {
  /** URL to cast. Defaults to `window.location.href`. */
  url?: string;
  /** Override label text. Defaults to "Cast" / "Casting". */
  label?: string;
  /** Absolute offset from the right edge. */
  right?: number | string;
}

const CastButton: React.FC<CastButtonProps> = ({ url, label, right = 16 }) => {
  const [casting, setCasting] = useState<boolean>(false);
  const [hint, setHint] = useState<string | null>(null);

  // Always render the button — on browsers without cast support we surface
  // a small inline hint at click time rather than hide the affordance.
  // Hiding silently was confusing: the user knows they're on Chrome, sees
  // no button, and thinks the feature is broken.

  useEffect(() => {
    if (!hint) return;
    const t = setTimeout(() => setHint(null), 4000);
    return () => clearTimeout(t);
  }, [hint]);

  const handleClick = async () => {
    const target = url ?? window.location.href;
    const Req = typeof window !== 'undefined' ? window.PresentationRequest : undefined;
    if (typeof Req !== 'function') {
      setHint('Cast requires Chrome / Edge on desktop or Android.');
      return;
    }
    try {
      const req = new Req([target]);
      const conn = await req.start();
      setCasting(true);
      conn.addEventListener('close',     () => setCasting(false));
      conn.addEventListener('terminate', () => setCasting(false));
    } catch (err) {
      // User cancelled the picker, or no compatible devices were found.
      // Surface a brief hint so the click doesn't feel like a no-op.
      setHint('No Chromecast device found, or cast cancelled.');
      console.debug('CastButton: cast cancelled or unavailable', err);
    }
  };

  const Icon = casting ? CastConnectedIcon : CastIcon;
  const text = label ?? (casting ? 'Casting' : 'Cast');
  const accent = casting ? '#4caf50' : '#9be38a';

  return (
    <Box sx={{ position: 'absolute', top: 16, right, zIndex: 10, display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 1 }}>
      <Box
        role="button"
        aria-label={casting ? 'Casting to Chromecast' : 'Cast to Chromecast'}
        onClick={handleClick}
        sx={{
          cursor: 'pointer',
          display: 'inline-flex',
          alignItems: 'center',
          gap: 1,
          px: 1.5,
          py: 0.75,
          borderRadius: 99,
          backgroundColor: 'rgba(0, 0, 0, 0.55)',
          backdropFilter: 'blur(6px)',
          border: `1px solid ${accent}`,
          color: accent,
          fontFamily: 'monospace',
          fontSize: 13,
          userSelect: 'none',
          transition: 'background-color 120ms ease',
          '&:hover': { backgroundColor: 'rgba(0, 0, 0, 0.75)' },
        }}
      >
        <Icon fontSize="small" />
        <span>{text}</span>
      </Box>
      {hint && (
        <Box
          sx={{
            px: 1.5,
            py: 0.75,
            maxWidth: 260,
            borderRadius: 1,
            backgroundColor: 'rgba(0, 0, 0, 0.7)',
            color: '#ffd58a',
            fontFamily: 'monospace',
            fontSize: 12,
            lineHeight: 1.3,
          }}
        >
          {hint}
        </Box>
      )}
    </Box>
  );
};

export default CastButton;
