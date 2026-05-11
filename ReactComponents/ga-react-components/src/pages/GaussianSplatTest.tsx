/**
 * Gaussian Splat viewer — native Three.js renderer (no iframe, no PlayCanvas).
 *
 * Three source kinds:
 *   - superspl-id: probes SuperSplat CDN /v1../v9 for compressed PLY / SOG.
 *   - direct-url:  any http(s) URL pointing at .ply / .compressed.ply /
 *                  .splat / .ksplat; format detected from extension.
 *   - local-file:  user-picked file (any of the above) loaded via blob URL.
 *
 * Renders via @mkkellogg/gaussian-splats-3d. SuperSplat's newer SOG format
 * (WebP textures + meta.json) isn't decodable by the lib yet, so SOG scenes
 * surface a friendly fallback — see PR #478 upstream.
 */

import React, { useCallback, useEffect, useRef, useState } from 'react';
import * as GaussianSplats3D from '@mkkellogg/gaussian-splats-3d';
import {
  Box,
  Button,
  Chip,
  IconButton,
  LinearProgress,
  Paper,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import UploadFileIcon from '@mui/icons-material/UploadFile';
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
  { id: 'a8926fc4', label: 'Reflective Surfaces (SOG)' },
];
const DEFAULT_SCENE_ID = PRESETS[0].id;
const SUPERSPLAT_CDN = 'https://d28zzqy0iyovbz.cloudfront.net';
const VERSION_PATHS = ['v1', 'v7', 'v2', 'v3', 'v4', 'v5', 'v6', 'v8', 'v9'];

// ───────────────────────────── source types ─────────────────────────────

type SplatSource =
  | { kind: 'superspl-id'; sceneId: string }
  | { kind: 'direct-url';  url: string; label: string }
  | { kind: 'local-file';  file: File; objectUrl: string; label: string };

function sourceKey(s: SplatSource): string {
  switch (s.kind) {
    case 'superspl-id': return `superspl:${s.sceneId}`;
    case 'direct-url':  return `url:${s.url}`;
    case 'local-file':  return `file:${s.objectUrl}`;
  }
}

function sourceLabel(s: SplatSource): string {
  switch (s.kind) {
    case 'superspl-id': return s.sceneId;
    case 'direct-url':  return s.label;
    case 'local-file':  return s.label;
  }
}

function parseInputToSource(input: string): SplatSource {
  const trimmed = input.trim();
  if (!trimmed) return { kind: 'superspl-id', sceneId: DEFAULT_SCENE_ID };

  // SuperSplat scene URL: https://superspl.at/scene/<id>
  const m = trimmed.match(/superspl\.at\/scene\/([a-zA-Z0-9-]+)/i);
  if (m) return { kind: 'superspl-id', sceneId: m[1] };

  // Direct URL — let the lib fetch it.
  if (/^https?:\/\//i.test(trimmed)) {
    const filename = trimmed.split('/').pop()?.split('?')[0] || trimmed;
    return { kind: 'direct-url', url: trimmed, label: filename };
  }

  // Bare alphanumeric: treat as a SuperSplat scene id.
  if (/^[a-zA-Z0-9-]+$/.test(trimmed)) {
    return { kind: 'superspl-id', sceneId: trimmed };
  }

  // Anything else: assume it's a URL fragment / unusual scheme.
  return { kind: 'direct-url', url: trimmed, label: trimmed };
}

// Format hint for the lib; for non-SuperSplat blob/data URLs we have to be
// explicit because the lib can't sniff format without an extension.
type LibFormatKey = 'Ply' | 'KSplat' | 'Splat';
function detectFormatFromName(name: string): LibFormatKey | undefined {
  const n = name.toLowerCase();
  if (n.endsWith('.compressed.ply') || n.endsWith('.ply')) return 'Ply';
  if (n.endsWith('.ksplat')) return 'KSplat';
  if (n.endsWith('.splat'))  return 'Splat';
  return undefined;
}

interface SuperSplatResolution {
  format: 'ply' | 'sog';
  version: string;
  url: string; // for ply only
}

async function probe(url: string): Promise<boolean> {
  try {
    const resp = await fetch(url, { method: 'HEAD' });
    return resp.ok;
  } catch {
    return false;
  }
}

async function resolveSuperSplatScene(sceneId: string): Promise<SuperSplatResolution | null> {
  for (const v of VERSION_PATHS) {
    const plyUrl = `${SUPERSPLAT_CDN}/${sceneId}/${v}/scene.compressed.ply`;
    const sogMetaUrl = `${SUPERSPLAT_CDN}/${sceneId}/${v}/meta.json`;
    const [hasPly, hasSog] = await Promise.all([probe(plyUrl), probe(sogMetaUrl)]);
    if (hasPly) return { format: 'ply', version: v, url: plyUrl };
    if (hasSog) return { format: 'sog', version: v, url: sogMetaUrl };
  }
  return null;
}

// ───────────────────────────── component ─────────────────────────────

const GaussianSplatTest: React.FC = () => {
  const containerRef = useRef<HTMLDivElement>(null);
  const viewerRef = useRef<GaussianSplats3D.Viewer | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  // Tracks the object URL of the currently displayed local file so we can
  // revoke it when a new source replaces it. URLs from earlier files would
  // otherwise leak the file's memory until the page is closed.
  const lastObjectUrlRef = useRef<string | null>(null);
  // Same idea but for SOG-decoded blob URLs: revoke on scene change / unmount.
  const lastBlobUrlRef = useRef<string | null>(null);

  const [draftInput, setDraftInput] = useState(`https://superspl.at/scene/${DEFAULT_SCENE_ID}`);
  const [source, setSource] = useState<SplatSource>({ kind: 'superspl-id', sceneId: DEFAULT_SCENE_ID });
  const [loading, setLoading] = useState(true);
  const [progress, setProgress] = useState(0);
  const [error, setError] = useState<string | null>(null);

  const replaceSource = useCallback((next: SplatSource) => {
    setSource((prev) => {
      // Revoke any previous blob URL we created; ignore URLs we didn't make.
      if (lastObjectUrlRef.current && lastObjectUrlRef.current !== (next.kind === 'local-file' ? next.objectUrl : null)) {
        try { URL.revokeObjectURL(lastObjectUrlRef.current); } catch { /* already revoked */ }
        lastObjectUrlRef.current = null;
      }
      if (next.kind === 'local-file') lastObjectUrlRef.current = next.objectUrl;
      // Avoid no-op state updates that would refire effects unnecessarily.
      if (sourceKey(prev) === sourceKey(next)) return prev;
      return next;
    });
  }, []);

  const handleLoad = useCallback(() => {
    replaceSource(parseInputToSource(draftInput));
  }, [draftInput, replaceSource]);

  const handlePreset = useCallback((preset: ScenePreset) => {
    setDraftInput(`https://superspl.at/scene/${preset.id}`);
    replaceSource({ kind: 'superspl-id', sceneId: preset.id });
  }, [replaceSource]);

  const handleFile = useCallback((file: File) => {
    const objectUrl = URL.createObjectURL(file);
    setDraftInput(file.name);
    replaceSource({ kind: 'local-file', file, objectUrl, label: file.name });
  }, [replaceSource]);

  const onFileInputChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) handleFile(file);
    // Allow re-picking the same file later.
    e.target.value = '';
  }, [handleFile]);

  // Revoke any outstanding blob URLs when the page unmounts — both the
  // user-picked-file URL and any SOG-decoded transcode blob.
  useEffect(() => () => {
    if (lastObjectUrlRef.current) {
      try { URL.revokeObjectURL(lastObjectUrlRef.current); } catch { /* already revoked */ }
      lastObjectUrlRef.current = null;
    }
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
    const key = sourceKey(source);
    setLoading(true);
    setProgress(0);
    setError(null);

    loadChainRef.current = loadChainRef.current
      .catch(() => undefined)
      .then(async () => {
        if (lastLoadedRef.current === key) {
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

        // Resolve to a concrete URL + format hint per source kind.
        let url: string;
        let formatKey: LibFormatKey | undefined;
        // Populated only for SOG scenes; used to center the splat at origin.
        let sogDecodedBbox: { center: [number, number, number]; radius: number } | null = null;

        if (source.kind === 'superspl-id') {
          const resolved = await resolveSuperSplatScene(source.sceneId);
          if (!resolved) {
            await clearLoadedSplat();
            if (!cancelled) {
              setError(`Scene ${source.sceneId} not found on the SuperSplat CDN under any known version path (v1–v9).`);
              setLoading(false);
            }
            return;
          }
          if (resolved.format === 'sog') {
            // Decode SOG in-browser into the older Antimatter15 .splat layout
            // and hand the lib a blob URL. Higher-order SH is dropped (the
            // .splat format can't carry it) but positions, scales, rotations,
            // and DC color are exact. See components/GaussianSplat/sogDecoder.
            try {
              const decoded = await decodeSogToSplatBlob(resolved.url);
              if (cancelled) { URL.revokeObjectURL(decoded.url); return; }
              if (lastBlobUrlRef.current) {
                try { URL.revokeObjectURL(lastBlobUrlRef.current); } catch { /* ignore */ }
              }
              lastBlobUrlRef.current = decoded.url;
              url = decoded.url;
              formatKey = 'Splat';
              sogDecodedBbox = decoded.bbox;
            } catch (sogErr) {
              await clearLoadedSplat();
              if (!cancelled) {
                const msg = sogErr instanceof Error ? sogErr.message : String(sogErr);
                setError(`SOG decode failed for ${source.sceneId} (${resolved.version}): ${msg}`);
                setLoading(false);
              }
              return;
            }
          } else {
            url = resolved.url;
            formatKey = 'Ply';
          }
        } else if (source.kind === 'direct-url') {
          url = source.url;
          formatKey = detectFormatFromName(source.label) ?? detectFormatFromName(source.url);
        } else {
          url = source.objectUrl;
          formatKey = detectFormatFromName(source.label);
          if (!formatKey) {
            await clearLoadedSplat();
            if (!cancelled) {
              setError(
                `Couldn't detect splat format from "${source.label}". Supported extensions: .ply, .compressed.ply, .splat, .ksplat.`
              );
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
            // render right-side-up under Three.js's Y-up convention.
            rotation: [1, 0, 0, 0],
            onProgress: (percent: number) => {
              if (!cancelled) setProgress(percent);
            },
          };
          // Size-aware centering: small studio-scale SOG scenes (Vegetables
          // HQ, table-top captures) need translation to origin so the default
          // camera at (0, 0.3, 1.4) doesn't sit inside the cloud. Large
          // outdoor captures (Queen's Hamlet) already have a sensible scene
          // origin from SuperSplat — centering them puts the default camera
          // 1.4 units from a 30-unit-radius bbox center, i.e. deep inside.
          if (sogDecodedBbox && sogDecodedBbox.radius < 5) {
            sceneOptions.position = [
              -sogDecodedBbox.center[0],
              -sogDecodedBbox.center[1],
              -sogDecodedBbox.center[2],
            ];
          }
          if (formatKey) {
            const fmtEnum = (GaussianSplats3D as unknown as { SceneFormat?: Record<string, unknown> }).SceneFormat;
            if (fmtEnum && fmtEnum[formatKey] !== undefined) {
              sceneOptions.format = fmtEnum[formatKey];
            }
          }
          await v.addSplatScene(url, sceneOptions);
          // No camera repositioning: Vegetables HQ frames well with the
          // bbox-centered scene at default camera, and Queen's Hamlet is
          // a large outdoor scene where "inside the bbox looking at content"
          // is the natural framing. Auto-frame from outside the bbox put
          // the camera over empty sky/ground for Queen's.
          lastLoadedRef.current = key;
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
  }, [source]);

  const currentLabel = sourceLabel(source);
  const isPresetSelected = (preset: ScenePreset) =>
    source.kind === 'superspl-id' && source.sceneId === preset.id;

  return (
    <DemoErrorBoundary demoName="Gaussian Splat">
      <Box
        sx={{
          width: '100%',
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
            {PRESETS.map((preset) => {
              const selected = isPresetSelected(preset);
              return (
                <Chip
                  key={preset.id}
                  label={preset.label}
                  clickable
                  onClick={() => handlePreset(preset)}
                  variant={selected ? 'filled' : 'outlined'}
                  sx={{
                    height: { xs: 32, sm: 26 },
                    fontSize: { xs: '0.8rem', sm: '0.75rem' },
                    color: selected ? '#0d0d18' : '#bda4ff',
                    bgcolor: selected ? '#bda4ff' : 'transparent',
                    borderColor: 'rgba(189,164,255,0.4)',
                    fontWeight: selected ? 600 : 400,
                    '&:hover': {
                      bgcolor: selected ? '#bda4ff' : 'rgba(189,164,255,0.12)',
                    },
                  }}
                />
              );
            })}
          </Stack>
          <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', flex: 1, minWidth: 0 }}>
            <TextField
              size="small"
              value={draftInput}
              onChange={(e) => setDraftInput(e.target.value)}
              onKeyDown={(e) => { if (e.key === 'Enter') handleLoad(); }}
              placeholder="superspl.at URL, scene id, or direct .ply/.splat/.ksplat URL"
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
            <Tooltip title="Open a local .ply, .compressed.ply, .splat, or .ksplat file">
              <IconButton
                size="small"
                onClick={() => fileInputRef.current?.click()}
                sx={{
                  color: '#bda4ff',
                  border: '1px solid rgba(189,164,255,0.4)',
                  borderRadius: 1,
                  width: 36,
                  height: 36,
                }}
                aria-label="Open local splat file"
              >
                <UploadFileIcon fontSize="small" />
              </IconButton>
            </Tooltip>
            <input
              ref={fileInputRef}
              type="file"
              accept=".ply,.splat,.ksplat"
              onChange={onFileInputChange}
              style={{ display: 'none' }}
            />
          </Box>
          <Typography
            variant="caption"
            sx={{
              color: '#8b949e',
              fontFamily: 'monospace',
              display: { xs: 'none', md: 'inline' },
              maxWidth: 220,
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap',
            }}
            title={currentLabel}
          >
            {source.kind === 'superspl-id' && `scene: ${source.sceneId}`}
            {source.kind === 'direct-url'  && `url: ${currentLabel}`}
            {source.kind === 'local-file'  && `file: ${currentLabel}`}
          </Typography>
        </Paper>

        <Box sx={{ flex: 1, position: 'relative', minHeight: 0, bgcolor: '#000' }}>
          <Box
            ref={containerRef}
            sx={{
              position: 'absolute',
              inset: 0,
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
                {source.kind === 'local-file' ? 'Decoding local splat file.' : 'Streaming splat data.'}
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
