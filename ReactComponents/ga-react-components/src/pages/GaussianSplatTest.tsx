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
import { Box, Chip, LinearProgress, Paper, Stack, Typography } from '@mui/material';
import { DemoErrorBoundary } from '../components/Common/DemoErrorBoundary';
import { decodeSogToSplatBlob } from '../components/GaussianSplat/sogDecoder';

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
    const metaUrl = `${SUPERSPLAT_CDN}/${sceneId}/${v}/meta.json`;
    const [hasPly, hasSog] = await Promise.all([probe(plyUrl), probe(metaUrl)]);
    if (hasPly) return { format: 'ply', version: v, url: plyUrl };
    if (hasSog) return { format: 'sog', version: v, url: metaUrl };
  }
  return null;
}

const GaussianSplatTest: React.FC = () => {
  const containerRef = useRef<HTMLDivElement>(null);
  const viewerRef = useRef<GaussianSplats3D.Viewer | null>(null);

  const [sceneId, setSceneId] = useState(DEFAULT_SCENE_ID);
  const [loading, setLoading] = useState(true);
  const [progress, setProgress] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [statusLine, setStatusLine] = useState<string | null>(null);

  // Blob URL of the most recent SOG decode so we can revoke it on scene change
  // or unmount. PLY scenes load straight from the CDN — nothing to revoke.
  const lastBlobUrlRef = useRef<string | null>(null);

  const handlePreset = useCallback((preset: ScenePreset) => {
    if (preset.id !== sceneId) setSceneId(preset.id);
  }, [sceneId]);

  useEffect(() => () => {
    if (lastBlobUrlRef.current) {
      try { URL.revokeObjectURL(lastBlobUrlRef.current); } catch { /* already revoked */ }
      lastBlobUrlRef.current = null;
    }
  }, []);

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

        // Pick a load URL + lib format key per scene format. SOG is decoded
        // in-browser into the older .splat layout (sogDecoder.ts) because
        // @mkkellogg/gaussian-splats-3d 0.4.7 doesn't natively decode SOG.
        let url: string;
        let formatKey: 'Ply' | 'Splat';
        let bbox: { center: [number, number, number]; radius: number } | null = null;

        if (resolved.format === 'ply') {
          url = resolved.url;
          formatKey = 'Ply';
          setStatusLine(`PLY · ${resolved.version}`);
        } else {
          if (!cancelled) setStatusLine(`Decoding SOG (${resolved.version})…`);
          try {
            const decoded = await decodeSogToSplatBlob(resolved.url);
            if (cancelled) { URL.revokeObjectURL(decoded.url); return; }
            if (lastBlobUrlRef.current) {
              try { URL.revokeObjectURL(lastBlobUrlRef.current); } catch { /* ignore */ }
            }
            lastBlobUrlRef.current = decoded.url;
            url = decoded.url;
            formatKey = 'Splat';
            bbox = { center: decoded.bbox.center, radius: decoded.bbox.radius };
            const shNote = decoded.shDropped ? ' (DC color only)' : '';
            setStatusLine(`SOG · ${resolved.version} · ${decoded.count.toLocaleString()} splats${shNote}`);
          } catch (decodeErr) {
            await clearLoadedSplat();
            if (!cancelled) {
              const msg = decodeErr instanceof Error ? decodeErr.message : String(decodeErr);
              setError(`SOG decode failed: ${msg}`);
              setLoading(false);
            }
            return;
          }
        }

        const v = ensureViewer();
        try {
          if (lastLoadedRef.current !== null && v.getSceneCount?.() > 0) {
            await v.removeSplatScene(0);
          }
          const sceneOptions: Record<string, unknown> = {
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
          };
          // Center SOG scenes at the origin using their bbox so the camera
          // (parked near 0,0,0) isn't buried inside the splat cloud.
          if (bbox) {
            sceneOptions.position = [-bbox.center[0], -bbox.center[1], -bbox.center[2]];
          }
          const fmtEnum = (GaussianSplats3D as unknown as { SceneFormat?: Record<string, unknown> }).SceneFormat;
          if (fmtEnum && fmtEnum[formatKey] !== undefined) {
            sceneOptions.format = fmtEnum[formatKey];
          }
          await v.addSplatScene(url, sceneOptions);

          // Pull the camera back to fit the bbox. The lib doesn't expose an
          // auto-frame helper, so we tweak camera.position directly. Bbox is
          // post-rotation 180° around X, so flip Y when picking a camera
          // height. radius * 2.4 is a comfortable default for SuperSplat
          // captures (covers the subject without clipping into it).
          if (bbox && v.camera) {
            const dist = Math.max(bbox.radius * 2.4, 1.5);
            v.camera.position.set(0, dist * 0.25, dist);
            v.camera.lookAt(0, 0, 0);
            if (v.controls?.target) {
              v.controls.target.set(0, 0, 0);
              v.controls.update?.();
            }
          }

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
      <Box sx={{ width: '100%', height: '100vh', bgcolor: '#05050d', display: 'flex', flexDirection: 'column', position: 'relative' }}>
        <Paper
          elevation={0}
          sx={{
            px: 2,
            py: 1.25,
            bgcolor: 'rgba(5,5,13,0.92)',
            color: '#f5f0ff',
            borderRadius: 0,
            borderBottom: '1px solid rgba(189,164,255,0.24)',
            display: 'flex',
            alignItems: 'center',
            gap: 1.5,
            flexWrap: 'wrap',
            zIndex: 2,
          }}
        >
          <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
            Gaussian Splat Viewer
          </Typography>
          <Typography variant="caption" sx={{ color: '#bda4ff' }}>
            Three.js · @mkkellogg/gaussian-splats-3d
          </Typography>
          <Stack direction="row" spacing={0.75}>
            {PRESETS.map((preset) => (
              <Chip
                key={preset.id}
                label={preset.label}
                size="small"
                clickable
                onClick={() => handlePreset(preset)}
                variant={preset.id === sceneId ? 'filled' : 'outlined'}
                sx={{
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
          <Box sx={{ flex: 1 }} />
          <Typography
            variant="caption"
            sx={{
              color: '#8b949e',
              fontFamily: 'monospace',
              maxWidth: { xs: '100%', md: 380 },
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap',
            }}
            title={statusLine ?? `scene: ${sceneId}`}
          >
            {statusLine ?? `scene: ${sceneId}`}
          </Typography>
        </Paper>

        <Box sx={{ flex: 1, position: 'relative', minHeight: 0, bgcolor: '#000' }}>
          <Box
            ref={containerRef}
            sx={{
              position: 'absolute',
              inset: 0,
              '& canvas': { display: 'block', width: '100%', height: '100%' },
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
              <Box sx={{ width: 280 }}>
                <LinearProgress variant="determinate" value={progress} />
              </Box>
              <Typography variant="caption" sx={{ color: '#8b949e' }}>
                {statusLine ?? 'Streaming splat data.'}
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
