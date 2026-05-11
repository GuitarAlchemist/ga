/**
 * Gaussian Splat viewer — native Three.js renderer (no iframe, no PlayCanvas).
 *
 * Loads a .compressed.ply from SuperSplat's CDN by scene id and renders it via
 * @mkkellogg/gaussian-splats-3d on top of our own Three.js canvas. SuperSplat's
 * newer SOG format (WebP textures + meta.json) isn't decodable by the lib yet,
 * so SOG scenes display a friendly fallback message — see PR #478 upstream.
 */

import React, { useCallback, useEffect, useRef, useState } from 'react';
import * as GaussianSplats3D from '@mkkellogg/gaussian-splats-3d';
import { Box, Button, Chip, LinearProgress, Paper, Stack, TextField, Typography } from '@mui/material';
import { DemoErrorBoundary } from '../components/Common/DemoErrorBoundary';

interface ScenePreset {
  id: string;
  label: string;
}

const PRESETS: ScenePreset[] = [
  { id: 'cf6ac78e', label: 'Bumblebee (PLY)' },
  { id: 'f592397a', label: 'Vegetables HQ (SOG)' },
  { id: 'ff1d0393', label: 'Queen’s Hamlet (SOG)' },
];
const DEFAULT_SCENE_ID = PRESETS[0].id;
const SUPERSPLAT_CDN = 'https://d28zzqy0iyovbz.cloudfront.net';

// SuperSplat re-encodes scenes under per-scene version paths (v1..v9). v1 and
// v7 cover the bulk of scenes I've sampled; the rest are tried as fallbacks.
const VERSION_PATHS = ['v1', 'v7', 'v2', 'v3', 'v4', 'v5', 'v6', 'v8', 'v9'];

interface ResolvedScene {
  format: 'ply' | 'sog';
  version: string;
  url: string; // populated for ply only
}

function extractSceneId(input: string): string {
  const trimmed = input.trim();
  if (!trimmed) return DEFAULT_SCENE_ID;
  const match = trimmed.match(/scene\/([a-zA-Z0-9-]+)/);
  return match ? match[1] : trimmed;
}

async function probe(url: string): Promise<boolean> {
  try {
    const resp = await fetch(url, { method: 'HEAD' });
    return resp.ok;
  } catch {
    return false;
  }
}

async function resolveScene(sceneId: string): Promise<ResolvedScene | null> {
  for (const v of VERSION_PATHS) {
    const plyUrl = `${SUPERSPLAT_CDN}/${sceneId}/${v}/scene.compressed.ply`;
    const sogUrl = `${SUPERSPLAT_CDN}/${sceneId}/${v}/meta.json`;
    const [hasPly, hasSog] = await Promise.all([probe(plyUrl), probe(sogUrl)]);
    if (hasPly) return { format: 'ply', version: v, url: plyUrl };
    if (hasSog) return { format: 'sog', version: v, url: '' };
  }
  return null;
}

const GaussianSplatTest: React.FC = () => {
  const containerRef = useRef<HTMLDivElement>(null);
  const viewerRef = useRef<GaussianSplats3D.Viewer | null>(null);

  const [draftInput, setDraftInput] = useState(`https://superspl.at/scene/${DEFAULT_SCENE_ID}`);
  const [sceneId, setSceneId] = useState(DEFAULT_SCENE_ID);
  const [loading, setLoading] = useState(true);
  const [progress, setProgress] = useState(0);
  const [error, setError] = useState<string | null>(null);

  const handleLoad = useCallback(() => {
    const next = extractSceneId(draftInput);
    if (next !== sceneId) setSceneId(next);
  }, [draftInput, sceneId]);

  const handlePreset = useCallback((preset: ScenePreset) => {
    setDraftInput(`https://superspl.at/scene/${preset.id}`);
    if (preset.id !== sceneId) setSceneId(preset.id);
  }, [sceneId]);

  // Gotchas this hook exists to work around — keep this comment, the wins
  // here cost a session of experimentation:
  //   1. Serialize loads through a chained promise (loadChainRef): React
  //      StrictMode dev fires the effect twice; the lib errors with
  //      "Cannot add splat scene while another load or unload is already
  //      in progress" on parallel addSplatScene calls.
  //   2. Update lastLoadedRef even after React-side cancellation: the
  //      next chain link skips a duplicate add only if it sees the prior
  //      load completed.
  //   3. Never dispose the viewer — the lib's worker keeps posting sorted-
  //      splat messages after dispose and throws "visitLeaves on null".
  //   4. progressiveLoad: false — progressive-load setTimeouts also race
  //      with StrictMode and spam visitLeaves errors.
  //   5. gpuAcceleratedSort: false — the GPU sort path silently no-ops
  //      without WebGPU compute and the post-sort visibility flip never
  //      fires, so the splat container stays visible:false (drawCalls=0).
  const loadChainRef = useRef<Promise<unknown>>(Promise.resolve());
  const lastLoadedRef = useRef<string | null>(null);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    const ensureViewer = (): GaussianSplats3D.Viewer => {
      if (viewerRef.current) return viewerRef.current;
      const v = new GaussianSplats3D.Viewer({
        cameraUp: [0, 1, 0],
        initialCameraPosition: [0, 0.3, 1.4],
        initialCameraLookAt: [0, 0, 0],
        sphericalHarmonicsDegree: 0,
        rootElement: container,
        useBuiltInControls: true,
        ignoreDevicePixelRatio: false,
        gpuAcceleratedSort: false,
        enableSIMDInSort: true,
        sharedMemoryForWorkers: false,
        dynamicScene: false,
        antialiased: false,
        selfDrivenMode: true,
      });
      viewerRef.current = v;
      v.start();
      return v;
    };

    let cancelled = false;
    setLoading(true);
    setProgress(0);
    setError(null);

    loadChainRef.current = loadChainRef.current
      .catch(() => undefined)
      .then(async () => {
        if (lastLoadedRef.current === sceneId) {
          if (!cancelled) setLoading(false);
          return;
        }
        const clearLoadedSplat = async () => {
          const existing = viewerRef.current;
          if (!existing || lastLoadedRef.current === null) return;
          if ((existing.getSceneCount?.() ?? 0) <= 0) return;
          try { await existing.removeSplatScene(0); } catch { /* viewer may have torn down */ }
          lastLoadedRef.current = null;
        };

        const resolved = await resolveScene(sceneId);
        if (!resolved) {
          await clearLoadedSplat();
          if (!cancelled) {
            setError(`Scene ${sceneId} not found on the SuperSplat CDN under any known version path (v1–v9).`);
            setLoading(false);
          }
          return;
        }
        if (resolved.format === 'sog') {
          await clearLoadedSplat();
          if (!cancelled) {
            setError(
              `Scene ${sceneId} (${resolved.version}) uses SuperSplat's newer SOG format (WebP textures + meta.json). ` +
              `The Three.js renderer here (@mkkellogg/gaussian-splats-3d 0.4.7) only supports the older compressed PLY. ` +
              `SOG support is in flight upstream — see github.com/mkkellogg/GaussianSplats3D/pull/478.`
            );
            setLoading(false);
          }
          return;
        }
        const v = ensureViewer();
        try {
          if (lastLoadedRef.current !== null && v.getSceneCount?.() > 0) {
            await v.removeSplatScene(0);
          }
          await v.addSplatScene(resolved.url, {
            progressiveLoad: false,
            showLoadingUI: false,
            // SuperSplat / COLMAP captures author +Y as gravity (down). Rotate
            // 180° around the X axis (quaternion x,y,z,w = 1,0,0,0) so subjects
            // render right-side-up under Three.js's Y-up convention. Applies to
            // the splat geometry itself so cached viewers also pick it up.
            rotation: [1, 0, 0, 0],
            onProgress: (percent: number) => {
              if (!cancelled) setProgress(percent);
            },
          });
          lastLoadedRef.current = sceneId;
          if (!cancelled) setLoading(false);
        } catch (err) {
          if (!cancelled) {
            const msg = err instanceof Error ? err.message : String(err);
            setError(`Failed to load scene: ${msg}`);
            setLoading(false);
          }
        }
      });

    return () => {
      cancelled = true;
    };
  }, [sceneId]);

  return (
    <DemoErrorBoundary demoName="Gaussian Splat">
      <Box
        sx={{
          width: '100%',
          // 100dvh respects mobile address-bar collapse; 100vh fallback for browsers without dvh.
          height: ['100vh', '100dvh'],
          bgcolor: '#05050d',
          display: 'flex',
          flexDirection: 'column',
          position: 'relative',
          overflow: 'hidden',
        }}
      >
        <Paper
          elevation={0}
          sx={{
            px: { xs: 1, sm: 2 },
            py: { xs: 0.75, sm: 1.25 },
            bgcolor: 'rgba(5,5,13,0.92)',
            color: '#f5f0ff',
            borderRadius: 0,
            borderBottom: '1px solid rgba(189,164,255,0.24)',
            display: 'flex',
            // Stack rows on phones, single row on tablets+: header rows are
            // title/badge, then presets, then URL+Load on narrow viewports.
            flexDirection: { xs: 'column', md: 'row' },
            alignItems: { xs: 'stretch', md: 'center' },
            gap: { xs: 0.75, md: 1.5 },
            flexWrap: 'wrap',
            zIndex: 2,
          }}
        >
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
              Gaussian Splat Viewer
            </Typography>
            <Typography variant="caption" sx={{ color: '#bda4ff', display: { xs: 'none', sm: 'inline' } }}>
              Three.js · @mkkellogg/gaussian-splats-3d
            </Typography>
          </Box>
          <Stack
            direction="row"
            spacing={0.75}
            sx={{ flexWrap: 'wrap', rowGap: 0.75, overflowX: 'auto', WebkitOverflowScrolling: 'touch' }}
          >
            {PRESETS.map((preset) => (
              <Chip
                key={preset.id}
                label={preset.label}
                clickable
                onClick={() => handlePreset(preset)}
                variant={preset.id === sceneId ? 'filled' : 'outlined'}
                sx={{
                  // Bigger touch target on phones (32px) vs desktop "small" (24px).
                  height: { xs: 32, sm: 26 },
                  fontSize: { xs: '0.8rem', sm: '0.75rem' },
                  color: preset.id === sceneId ? '#0d0d18' : '#bda4ff',
                  bgcolor: preset.id === sceneId ? '#bda4ff' : 'transparent',
                  borderColor: 'rgba(189,164,255,0.4)',
                  fontWeight: preset.id === sceneId ? 600 : 400,
                  '&:hover': {
                    bgcolor: preset.id === sceneId ? '#bda4ff' : 'rgba(189,164,255,0.12)',
                  },
                }}
              />
            ))}
          </Stack>
          <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', flex: 1, minWidth: 0 }}>
            <TextField
              size="small"
              value={draftInput}
              onChange={(e) => setDraftInput(e.target.value)}
              onKeyDown={(e) => { if (e.key === 'Enter') handleLoad(); }}
              placeholder="superspl.at scene URL or id"
              sx={{
                flex: 1,
                minWidth: 0,
                '& .MuiOutlinedInput-root': {
                  bgcolor: '#0d1117',
                  color: 'rgba(255,255,255,0.87)',
                  '& fieldset': { borderColor: '#30363d' },
                  '&:hover fieldset': { borderColor: '#58a6ff' },
                },
                '& input::placeholder': { color: 'rgba(255,255,255,0.5)', opacity: 1 },
              }}
            />
            <Button
              variant="outlined"
              size="small"
              onClick={handleLoad}
              sx={{
                color: '#bda4ff',
                borderColor: 'rgba(189,164,255,0.4)',
                minWidth: 64,
                px: 1.5,
              }}
            >
              Load
            </Button>
          </Box>
          <Typography
            variant="caption"
            sx={{
              color: '#8b949e',
              fontFamily: 'monospace',
              display: { xs: 'none', md: 'inline' },
            }}
          >
            scene: {sceneId}
          </Typography>
        </Paper>

        <Box sx={{ flex: 1, position: 'relative', minHeight: 0, bgcolor: '#000' }}>
          <Box
            ref={containerRef}
            sx={{
              position: 'absolute',
              inset: 0,
              // touch-action: none lets the lib's OrbitControls own the gesture
              // instead of the browser stealing it for scroll/zoom.
              touchAction: 'none',
              overscrollBehavior: 'contain',
              '& canvas': {
                display: 'block',
                width: '100%',
                height: '100%',
                touchAction: 'none',
              },
            }}
          />
          {loading && !error && (
            <Box
              sx={{
                position: 'absolute',
                inset: 0,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                flexDirection: 'column',
                gap: 1.5,
                bgcolor: 'rgba(0,0,0,0.55)',
                pointerEvents: 'none',
              }}
            >
              <Typography variant="body2" sx={{ color: '#f5f0ff' }}>
                Loading splat… {Math.round(progress)}%
              </Typography>
              <Box sx={{ width: { xs: '70vw', sm: 280 }, maxWidth: 320 }}>
                <LinearProgress variant="determinate" value={progress} />
              </Box>
              <Typography variant="caption" sx={{ color: '#8b949e' }}>
                Streaming compressed PLY from SuperSplat CDN.
              </Typography>
            </Box>
          )}
          {error && (
            <Box
              sx={{
                position: 'absolute',
                inset: 0,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                p: 4,
              }}
            >
              <Typography variant="body2" sx={{ color: '#ff8a80', textAlign: 'center', maxWidth: 480 }}>
                {error}
              </Typography>
            </Box>
          )}
        </Box>
      </Box>
    </DemoErrorBoundary>
  );
};

export default GaussianSplatTest;
