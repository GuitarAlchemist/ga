/**
 * Tonal Orbit test page.
 *
 * Mounts the TonalOrbit 3D scene full-screen, wires up:
 *   - A breadcrumb HUD (top-left) showing the focus path; clicking a
 *     crumb pops focus back to that depth.
 *   - A "Now playing" pill (bottom-left) — current pitch / chord / scale,
 *     audio-on toggle, orbiting-on toggle.
 *   - A title chip (bottom-right) — version + perf tier.
 *   - The shared `CastButton` (top-right) for Chromecast tab-mirror.
 *
 * URL toggles
 * ───────────
 *   ?perf=high|medium|low  — overrides the default (high on desktop,
 *                            low on small touch screens)
 *   ?tour=auto             — TV-friendly auto-tour: camera cycles through
 *                            pitch → chord → chord → next pitch on a
 *                            fixed schedule. Any tap cancels the tour.
 *
 * The page is intentionally light on chrome: the value of the demo is
 * the 3D scene itself, and the existing demos all use this same restraint.
 */
import React, { useCallback, useMemo, useState } from 'react';
import { Container, Box, Paper, Typography, Breadcrumbs, Link, Stack, Switch, FormControlLabel } from '@mui/material';
import HomeIcon from '@mui/icons-material/Home';
import { DemoErrorBoundary } from '../components/Common/DemoErrorBoundary';
import CastButton from '../components/Common/CastButton';
import {
  TonalOrbit,
  focusPath,
  type FocusState,
  type TonalOrbitApi,
  type PerfMode,
  type ChordMode,
} from '../components/TonalOrbit';

const detectDefaultPerf = (): PerfMode => {
  if (typeof window === 'undefined') return 'high';
  // Crude but effective: small viewport + coarse pointer → mobile → low.
  const isCoarse = window.matchMedia?.('(pointer: coarse)').matches ?? false;
  const smallViewport = Math.min(window.innerWidth, window.innerHeight) < 700;
  if (isCoarse && smallViewport) return 'low';
  if (isCoarse) return 'medium';
  return 'high';
};

const readPerfFromUrl = (): PerfMode => {
  if (typeof window === 'undefined') return 'high';
  const q = new URLSearchParams(window.location.search).get('perf');
  if (q === 'low' || q === 'medium' || q === 'high') return q;
  return detectDefaultPerf();
};

const readTourFromUrl = (): boolean => {
  if (typeof window === 'undefined') return false;
  const q = new URLSearchParams(window.location.search).get('tour');
  return q === 'auto' || q === '1' || q === 'true';
};

const readChordModeFromUrl = (): ChordMode => {
  if (typeof window === 'undefined') return 'root';
  const q = new URLSearchParams(window.location.search).get('chord-mode');
  return q === 'key' ? 'key' : 'root'; // default is Mode A (root chords)
};

// Pitch-class name → PC lookup for the auto-focus harness URL syntax.
const PC_BY_NAME: Record<string, number> = {
  C: 0, 'C#': 1, Db: 1, D: 2, 'D#': 3, Eb: 3, E: 4, F: 5,
  'F#': 6, Gb: 6, G: 7, 'G#': 8, Ab: 8, A: 9, 'A#': 10, Bb: 10, B: 11,
};

/**
 * Optional auto-focus harness — when URL has `?auto-focus=<pitchName>`
 * (e.g. `?auto-focus=C`), programmatically focuses that pitch 1.5s after
 * mount via the imperative API. Lets a headless screenshot capture the
 * focused state (chord + optionally scale orbits visible) without
 * having to guess pitch-planet screen coordinates. Records the chosen
 * pitch on `<body data-auto-focus="...">` so a dump-dom probe can tell
 * whether the harness ran.
 */
const armAutoFocus = (): void => {
  if (typeof window === 'undefined') return;
  const pitchName = new URLSearchParams(window.location.search).get('auto-focus');
  if (!pitchName) return;
  const pc = PC_BY_NAME[pitchName];
  if (pc === undefined) return;
  setTimeout(() => {
    const api = (window as unknown as { __tonalOrbitApi?: TonalOrbitApi }).__tonalOrbitApi;
    if (!api) {
      document.body.setAttribute('data-auto-focus', `FAIL_NO_API:${pitchName}`);
      return;
    }
    api.focusPitchByPc(pc);
    document.body.setAttribute('data-auto-focus', `${pitchName}:pc=${pc}`);
  }, 1500);
};

/**
 * Test harness — only active when URL has `?test=zoom`. Tags the canvas
 * with `data-test=zoom` so the TonalOrbit render loop exposes camera
 * state as DOM attributes (zero overhead in production), then simulates
 * a user drag at +1.5s and samples the camera distance immediately and
 * 1s later. Records `data-zoom-test="initial=… afterWheel=… afterWait1s=…"`
 * on the body so a headless --dump-dom probe can verify the camera
 * position persists after user interaction (i.e. doesn't snap back).
 *
 * This is the regression test for the zoom-stays fix: the loop only
 * lerps the camera toward the focus target while a `cameraAnimating`
 * flag is true; OrbitControls' 'start' event clears the flag so user
 * wheel / pinch / drag wins.
 */
const armZoomTest = (): void => {
  if (typeof window === 'undefined') return;
  if (new URLSearchParams(window.location.search).get('test') !== 'zoom') return;
  setTimeout(() => {
    const canvas = document.querySelector('canvas') as HTMLCanvasElement | null;
    if (!canvas) { document.body.setAttribute('data-zoom-test', 'FAIL_NO_CANVAS'); return; }
    canvas.dataset.test = 'zoom';
    // One animation frame later the render loop populates data-camera-dist.
    requestAnimationFrame(() => requestAnimationFrame(() => {
      const initial = canvas.getAttribute('data-camera-dist');
      const rect = canvas.getBoundingClientRect();
      const cx = rect.left + rect.width / 2;
      const cy = rect.top + rect.height / 2;
      // Simulate a drag-rotate — the most reliable user interaction in
      // headless Chrome (synthetic WheelEvents don't always surface
      // deltaY to OrbitControls' wheel handler).
      canvas.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, cancelable: true, pointerId: 1, clientX: cx, clientY: cy, button: 0, buttons: 1, pointerType: 'mouse' }));
      canvas.dispatchEvent(new PointerEvent('pointermove', { bubbles: true, cancelable: true, pointerId: 1, clientX: cx + 80, clientY: cy + 40, button: 0, buttons: 1, pointerType: 'mouse' }));
      canvas.dispatchEvent(new PointerEvent('pointerup',   { bubbles: true, cancelable: true, pointerId: 1, clientX: cx + 80, clientY: cy + 40, button: 0, buttons: 0, pointerType: 'mouse' }));
      setTimeout(() => {
        const afterAction = canvas.getAttribute('data-camera-dist');
        setTimeout(() => {
          const afterWait = canvas.getAttribute('data-camera-dist');
          document.body.setAttribute('data-zoom-test',
            `initial=${initial} afterAction=${afterAction} afterWait1s=${afterWait}`);
        }, 1000);
      }, 200);
    }));
  }, 1500);
};

const TonalOrbitTest: React.FC = () => {
  // Read once on mount — perf tier change requires remount of the scene.
  const perf = useMemo<PerfMode>(readPerfFromUrl, []);
  const tour = useMemo<boolean>(readTourFromUrl, []);
  React.useEffect(() => { armZoomTest(); armAutoFocus(); }, []);

  const [focus, setFocus] = useState<FocusState>({ pitch: null, chord: null, scale: null });
  const [audioOn, setAudioOn] = useState<boolean>(true);
  const [orbitingOn, setOrbitingOn] = useState<boolean>(true);
  // Chord mode: 'root' (Mode A — chord families on the focused pitch as
  // root) or 'key' (Mode B — diatonic chords of the focused pitch's
  // major key, with the 7 modes of that key in the outer ring).
  const [chordMode, setChordMode] = useState<ChordMode>(readChordModeFromUrl);
  const apiRef = React.useRef<TonalOrbitApi | null>(null);

  const handleFocus = useCallback((f: FocusState) => setFocus(f), []);
  const handleReady = useCallback((api: TonalOrbitApi) => {
    apiRef.current = api;
    // Push the URL-driven mode into the scene the moment the API hooks up.
    api.setChordMode(chordMode);
    // Expose the API on window for headless-screenshot probes so they
    // don't have to guess pitch-planet screen coordinates. Gated behind
    // the same `?auto-focus=` URL param the rest of the harness uses.
    if (typeof window !== 'undefined' && new URLSearchParams(window.location.search).has('auto-focus')) {
      (window as unknown as { __tonalOrbitApi?: TonalOrbitApi }).__tonalOrbitApi = api;
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const path = focusPath(focus);

  const popTo = (depth: 0 | 1 | 2) => apiRef.current?.popTo(depth);
  const toggleAudio = (on: boolean) => {
    setAudioOn(on);
    apiRef.current?.setAudioEnabled(on);
  };
  const toggleOrbit = (on: boolean) => {
    setOrbitingOn(on);
    apiRef.current?.setOrbiting(on);
  };
  const toggleChordMode = (next: ChordMode) => {
    if (next === chordMode) return;
    setChordMode(next);
    apiRef.current?.setChordMode(next);
    // Reflect the new mode in the URL so it survives page reloads / shares.
    if (typeof window !== 'undefined') {
      const url = new URL(window.location.href);
      if (next === 'root') url.searchParams.delete('chord-mode'); // keep default URL clean
      else url.searchParams.set('chord-mode', next);
      window.history.replaceState({}, '', url.toString());
    }
  };

  return (
    <DemoErrorBoundary demoName="Tonal Orbit">
      {/* `height: 100%` (not 100vh) so we fill the App's `.mainContent`
          flex pane — using 100vh overflowed the parent by the height of
          the sticky breadcrumb bar, leaving a white strip at the bottom
          of the viewport and cutting ~30px off the canvas. */}
      <Container
        maxWidth={false}
        disableGutters
        sx={{ width: '100%', height: '100%', overflow: 'hidden', position: 'relative', backgroundColor: '#000' }}
      >
        <TonalOrbit perf={perf} tour={tour} chordMode={chordMode} onFocusChange={handleFocus} onReady={handleReady} />
        <CastButton label="Cast" />

        {/* Chord-mode pill. Switches the chord/scale orbit between
            "Root chords" (Mode A — chord qualities on the focused pitch
            as root) and "Key chords" (Mode B — diatonic chords of the
            focused pitch's major key + the 7 modes of that key). The
            mode change rebuilds only the chord/scale orbit, not the
            renderer or starfield.

            Placement: top-centre on tablet+ (sm and up); on mobile (xs)
            the breadcrumb + Cast button squeeze the centre, so the
            pill drops below them (top: 60). The "MODE" label is
            collapsed on mobile to keep the pill narrow. */}
        <Paper
          elevation={3}
          sx={{
            position: 'absolute',
            top: { xs: 60, sm: 16 },
            left: '50%',
            transform: 'translateX(-50%)',
            display: 'flex',
            alignItems: 'center',
            gap: 0.5,
            px: 1,
            py: 0.5,
            backgroundColor: 'rgba(0, 0, 0, 0.55)',
            backdropFilter: 'blur(8px)',
            border: '1px solid rgba(155, 195, 255, 0.35)',
            borderRadius: 1.5,
            pointerEvents: 'auto',
            zIndex: 5,
            whiteSpace: 'nowrap',
          }}
        >
          <Typography
            sx={{
              color: '#7da8cf', fontFamily: 'monospace', fontSize: 11, letterSpacing: 1,
              mr: 1,
              display: { xs: 'none', sm: 'inline' }, // MODE label takes too much room on mobile
            }}
          >
            MODE
          </Typography>
          <Box
            role="button"
            tabIndex={0}
            aria-pressed={chordMode === 'root'}
            onClick={() => toggleChordMode('root')}
            sx={{
              cursor: 'pointer',
              px: 1.25,
              py: 0.5,
              borderRadius: 1,
              fontFamily: 'monospace',
              fontSize: 12,
              color: chordMode === 'root' ? '#0a0e2a' : '#cfe7ff',
              backgroundColor: chordMode === 'root' ? '#9bc3ff' : 'transparent',
              userSelect: 'none',
              transition: 'background-color 120ms ease, color 120ms ease',
            }}
          >
            Root chords
          </Box>
          <Box
            role="button"
            tabIndex={0}
            aria-pressed={chordMode === 'key'}
            onClick={() => toggleChordMode('key')}
            sx={{
              cursor: 'pointer',
              px: 1.25,
              py: 0.5,
              borderRadius: 1,
              fontFamily: 'monospace',
              fontSize: 12,
              color: chordMode === 'key' ? '#0a0e2a' : '#cfe7ff',
              backgroundColor: chordMode === 'key' ? '#9bc3ff' : 'transparent',
              userSelect: 'none',
              transition: 'background-color 120ms ease, color 120ms ease',
            }}
          >
            Key chords
          </Box>
        </Paper>

        {/* Breadcrumb — top-left. Clicking pops focus. Right-side max-width
            leaves a 120px gutter for the CastButton (top-right) so the two
            never overlap on narrow viewports. */}
        <Paper
          elevation={3}
          sx={{
            position: 'absolute',
            top: 16,
            left: 16,
            px: 1.5,
            py: 0.75,
            backgroundColor: 'rgba(0, 0, 0, 0.55)',
            backdropFilter: 'blur(8px)',
            border: '1px solid rgba(155, 195, 255, 0.35)',
            borderRadius: 1.5,
            maxWidth: 'calc(100% - 160px)',
            overflowX: 'auto',
            pointerEvents: 'auto',
          }}
        >
          <Breadcrumbs separator="›" sx={{ '& .MuiBreadcrumbs-separator': { color: '#9bc3ff' } }}>
            <Link
              component="button"
              onClick={() => popTo(0)}
              sx={{
                color: path.length === 1 ? '#cfe7ff' : '#9bc3ff',
                display: 'flex', alignItems: 'center', gap: 0.5,
                fontFamily: 'monospace', fontSize: 13, textDecoration: 'none',
              }}
            >
              <HomeIcon sx={{ fontSize: 16 }} /> Tonal Orbit
            </Link>
            {path.slice(1).map((segment, i) => {
              // i = 0 → pitch (popTo 0 to clear), i = 1 → chord (popTo 1 to clear scale), i = 2 → scale (popTo 2)
              const targetDepth = (i === 0 ? 0 : i === 1 ? 1 : 2) as 0 | 1 | 2;
              const isLeaf = i === path.length - 2;
              return (
                <Link
                  key={`${i}-${segment}`}
                  component="button"
                  onClick={() => popTo(targetDepth)}
                  sx={{
                    color: isLeaf ? '#cfe7ff' : '#9bc3ff',
                    fontFamily: 'monospace', fontSize: 13, textDecoration: 'none',
                  }}
                >
                  {segment}
                </Link>
              );
            })}
          </Breadcrumbs>
        </Paper>

        {/* "Now playing" pill — bottom-left. On narrow viewports we shed
            the right-side margin reserved for the title chip (which is
            hidden below xs) and let the pill take the full width. */}
        <Paper
          elevation={3}
          sx={{
            position: 'absolute',
            bottom: 20,
            left: 20,
            px: 2,
            py: 1.25,
            // On wide screens reserve room for the title chip on the right;
            // on narrow screens the chip is hidden and the pill can stretch.
            maxWidth: { xs: 'calc(100% - 40px)', sm: 'calc(100% - 260px)' },
            backgroundColor: 'rgba(6, 10, 24, 0.78)',
            color: '#cfe7ff',
            fontFamily: 'monospace',
            borderLeft: '3px solid #9bc3ff',
          }}
        >
          <Typography variant="caption" sx={{ color: '#7da8cf', display: 'block', letterSpacing: 1, fontSize: 10 }}>
            FOCUS
          </Typography>
          <Typography variant="body1" sx={{ color: '#cfe7ff', fontFamily: 'monospace', fontSize: 14, mb: 0.5 }}>
            {focus.scale?.displayName ?? focus.chord?.displayName ?? focus.pitch?.name ?? 'Tap a planet'}
          </Typography>
          <Stack direction="row" spacing={2} sx={{ mt: 0.5 }}>
            <FormControlLabel
              control={
                <Switch
                  size="small"
                  checked={audioOn}
                  onChange={(e) => toggleAudio(e.target.checked)}
                  sx={{
                    '& .MuiSwitch-switchBase.Mui-checked': { color: '#9bc3ff' },
                    '& .MuiSwitch-switchBase.Mui-checked + .MuiSwitch-track': { backgroundColor: '#9bc3ff' },
                  }}
                />
              }
              label={<Typography sx={{ color: '#cfe7ff', fontSize: 11 }}>Drone</Typography>}
              sx={{ m: 0 }}
            />
            <FormControlLabel
              control={
                <Switch
                  size="small"
                  checked={orbitingOn}
                  onChange={(e) => toggleOrbit(e.target.checked)}
                  sx={{
                    '& .MuiSwitch-switchBase.Mui-checked': { color: '#9bc3ff' },
                    '& .MuiSwitch-switchBase.Mui-checked + .MuiSwitch-track': { backgroundColor: '#9bc3ff' },
                  }}
                />
              }
              label={<Typography sx={{ color: '#cfe7ff', fontSize: 11 }}>Orbit</Typography>}
              sx={{ m: 0 }}
            />
          </Stack>
        </Paper>

        {/* Title chip — bottom-right. Hidden below the "sm" breakpoint
            (~600px) because the FOCUS pill needs the full bottom row on
            phone-sized viewports. */}
        <Box
          sx={{
            display: { xs: 'none', sm: 'block' },
            position: 'absolute',
            bottom: 20,
            right: 20,
            px: 1.5,
            py: 0.75,
            backgroundColor: 'rgba(0, 0, 0, 0.55)',
            color: '#9bc3ff',
            fontFamily: 'monospace',
            fontSize: 11,
            pointerEvents: 'none',
            letterSpacing: 1,
            borderRadius: 1,
          }}
        >
          TONAL ORBIT · v1 · perf={perf}{tour ? ' · tour' : ''}
        </Box>
      </Container>
    </DemoErrorBoundary>
  );
};

export default TonalOrbitTest;
