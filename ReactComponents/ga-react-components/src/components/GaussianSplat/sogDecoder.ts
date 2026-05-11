/**
 * SuperSplat / PlayCanvas SOG decoder.
 *
 * Decodes SOG-format scenes (WebP textures + meta.json) into the older
 * Antimatter15 .splat binary layout so @mkkellogg/gaussian-splats-3d 0.4.7
 * — which doesn't speak SOG natively yet — can render them. The published
 * .splat format carries DC color only (no higher-order SH), so view-
 * dependent specular detail is lost here; the rest of the splat data
 * (positions, scales, quats, base color) is preserved exactly.
 *
 * Decode math ported from upstream PR #478 (querielo/kirill/sog):
 *   https://github.com/mkkellogg/GaussianSplats3D/pull/478
 *
 * .splat layout per splat (32 bytes):
 *   0..11   float32 position.xyz
 *   12..23  float32 scale.xyz
 *   24..27  uint8   rgba
 *   28..31  uint8   quaternion.xyzw  (linear remap of -1..1 → 0..255)
 */

interface SogMeta {
  version: number;
  count: number;
  means: {
    mins: [number, number, number];
    maxs: [number, number, number];
    files: [string, string]; // [low_byte, high_byte] WebPs
  };
  scales: { codebook: number[]; files: [string] };
  quats:  { files: [string] };
  sh0:    { codebook: number[]; files: [string] };
  // shN block is ignored for .splat output — that format can't carry SH.
  shN?: unknown;
}

interface DecodedImage {
  data: Uint8ClampedArray;
  width: number;
  height: number;
}

async function loadImagePixels(url: string): Promise<DecodedImage> {
  const resp = await fetch(url);
  if (!resp.ok) throw new Error(`Failed to fetch ${url}: HTTP ${resp.status}`);
  const blob = await resp.blob();

  // Prefer ImageDecoder (Chromium) — cheaper than canvas drawImage.
  // The type-check below is the same pattern PR #478 uses.
  type ImageDecoderCtor = new (init: { data: Blob; type: string }) => {
    decode: () => Promise<{ image: {
      displayWidth?: number; displayHeight?: number; codedWidth: number; codedHeight: number;
      copyTo: (dest: Uint8ClampedArray, opts: { format: string }) => Promise<void>;
      close: () => void;
    } }>;
  };
  const ImageDecoderRef = (globalThis as unknown as { ImageDecoder?: ImageDecoderCtor }).ImageDecoder;
  if (ImageDecoderRef) {
    try {
      const decoder = new ImageDecoderRef({ data: blob, type: blob.type || 'image/webp' });
      const { image } = await decoder.decode();
      const width  = image.displayWidth  ?? image.codedWidth;
      const height = image.displayHeight ?? image.codedHeight;
      const data = new Uint8ClampedArray(width * height * 4);
      await image.copyTo(data, { format: 'RGBA' });
      image.close();
      return { data, width, height };
    } catch {
      // fall through to canvas path
    }
  }

  const bitmap = await createImageBitmap(blob);
  const { width, height } = bitmap;
  const useOffscreen = typeof OffscreenCanvas !== 'undefined';
  const canvas: HTMLCanvasElement | OffscreenCanvas = useOffscreen
    ? new OffscreenCanvas(width, height)
    : Object.assign(document.createElement('canvas'), { width, height });
  const ctx = canvas.getContext('2d') as
    | CanvasRenderingContext2D
    | OffscreenCanvasRenderingContext2D
    | null;
  if (!ctx) throw new Error('Could not get 2d context for SOG image decode');
  ctx.drawImage(bitmap, 0, 0);
  const imageData = ctx.getImageData(0, 0, width, height);
  return { data: imageData.data, width, height };
}

function lerp(a: number, b: number, t: number): number {
  return a + (b - a) * t;
}

// Inverse of the signed-log encoding SuperSplat uses on position channels.
function unlog(n: number): number {
  return Math.sign(n) * (Math.exp(Math.abs(n)) - 1);
}

// Reconstruct a unit quaternion from the SOG-packed RGBA byte tuple.
// Three channels carry the three "smallest" components in (-1/√2 … 1/√2);
// the alpha byte encodes which axis was dropped (largest-magnitude one),
// with the valid range 252..255 representing modes 0..3.
//
// WebP is lossy and can perturb the alpha by ±1–2 around the encoded value.
// If an `a` drifts out of 252..255 and we fall into a "default" branch,
// the splat collapses to an identity orientation — combined with an
// authored anisotropic scale, that renders as a long world-axis-aligned
// needle. Clamping the alpha to the valid range eliminates those artifacts.
function reconstructQuaternion(r: number, g: number, b: number, a: number): [number, number, number, number] {
  const comp = (c: number) => ((c / 255 - 0.5) * 2.0) / Math.SQRT2;
  const A = comp(r);
  const B = comp(g);
  const C = comp(b);
  const clampedA = a < 252 ? 252 : (a > 255 ? 255 : a);
  const mode = clampedA - 252;
  const t = A * A + B * B + C * C;
  const D = Math.sqrt(Math.max(0, 1 - t));
  let qx: number, qy: number, qz: number, qw: number;
  switch (mode) {
    case 0:  qx = D; qy = A; qz = B; qw = C; break;
    case 1:  qx = A; qy = D; qz = B; qw = C; break;
    case 2:  qx = A; qy = B; qz = D; qw = C; break;
    default: qx = A; qy = B; qz = C; qw = D; break; // mode 3
  }
  // Hemisphere flip + normalize.
  if (qw < 0) { qx = -qx; qy = -qy; qz = -qz; qw = -qw; }
  const len = Math.hypot(qx, qy, qz, qw) || 1;
  return [qx / len, qy / len, qz / len, qw / len];
}

function clampByte(v: number): number {
  return v < 0 ? 0 : v > 255 ? 255 : v | 0;
}

export interface SogDecodeResult {
  /** Blob URL containing a .splat-format buffer; revoke when done. */
  url: string;
  /** Number of splats actually decoded. */
  count: number;
  /** True when the meta declared higher-order SH that we had to drop. */
  shDropped: boolean;
  /** Bounding box of the decoded splats in scene-local coordinates. */
  bbox: {
    min: [number, number, number];
    max: [number, number, number];
    center: [number, number, number];
    radius: number;
  };
}

/**
 * Decode a SOG scene living at `<baseUrl>/<filename>` (meta + WebPs all
 * under the same prefix) into an in-memory .splat buffer; return a blob URL
 * the caller hands to `viewer.addSplatScene(url, { format: Splat })`.
 */
export async function decodeSogToSplatBlob(metaUrl: string): Promise<SogDecodeResult> {
  const baseUrl = metaUrl.substring(0, metaUrl.lastIndexOf('/') + 1);
  const meta = (await (await fetch(metaUrl)).json()) as SogMeta;

  const resolve = (name: string) => loadImagePixels(`${baseUrl}${name}`);
  const [meansL, meansU, quats, scalesImg, sh0Img] = await Promise.all([
    resolve(meta.means.files[0]),
    resolve(meta.means.files[1]),
    resolve(meta.quats.files[0]),
    resolve(meta.scales.files[0]),
    resolve(meta.sh0.files[0]),
  ]);

  const width = meansL.width;
  const height = meansL.height;
  const capacity = width * height;
  const count = Math.min(meta.count, capacity);

  const SH_C0 = 0.28209479177387814;
  const SPLAT_SIZE = 32;
  const out = new Uint8Array(count * SPLAT_SIZE);
  const view = new DataView(out.buffer);

  const mins = meta.means.mins;
  const maxs = meta.means.maxs;
  const sh0Codebook = meta.sh0.codebook;
  const scaleCodebook = meta.scales.codebook;

  let bbMinX = Infinity, bbMinY = Infinity, bbMinZ = Infinity;
  let bbMaxX = -Infinity, bbMaxY = -Infinity, bbMaxZ = -Infinity;

  for (let i = 0; i < count; i++) {
    const idx = i * 4; // RGBA stride into the WebP pixel arrays
    const base = i * SPLAT_SIZE;

    // Position: 16-bit per-axis dequantized to [mins, maxs], then unlog.
    const qx = (meansU.data[idx + 0] << 8) | meansL.data[idx + 0];
    const qy = (meansU.data[idx + 1] << 8) | meansL.data[idx + 1];
    const qz = (meansU.data[idx + 2] << 8) | meansL.data[idx + 2];
    const px = unlog(lerp(mins[0], maxs[0], qx / 65535));
    const py = unlog(lerp(mins[1], maxs[1], qy / 65535));
    const pz = unlog(lerp(mins[2], maxs[2], qz / 65535));

    if (px < bbMinX) bbMinX = px; if (px > bbMaxX) bbMaxX = px;
    if (py < bbMinY) bbMinY = py; if (py > bbMaxY) bbMaxY = py;
    if (pz < bbMinZ) bbMinZ = pz; if (pz > bbMaxZ) bbMaxZ = pz;

    // Scale: per-axis codebook index → log-scale → linear.
    const sx = Math.exp(scaleCodebook[scalesImg.data[idx + 0]]);
    const sy = Math.exp(scaleCodebook[scalesImg.data[idx + 1]]);
    const sz = Math.exp(scaleCodebook[scalesImg.data[idx + 2]]);

    // Quaternion: 3-of-4 packing with axis indicator in alpha.
    const [qX, qY, qZ, qW] = reconstructQuaternion(
      quats.data[idx + 0], quats.data[idx + 1], quats.data[idx + 2], quats.data[idx + 3]
    );

    // DC color: SH0 codebook → SH_C0 → 0..1 → bytes.
    const r = clampByte((0.5 + sh0Codebook[sh0Img.data[idx + 0]] * SH_C0) * 255);
    const g = clampByte((0.5 + sh0Codebook[sh0Img.data[idx + 1]] * SH_C0) * 255);
    const b = clampByte((0.5 + sh0Codebook[sh0Img.data[idx + 2]] * SH_C0) * 255);
    // SOG stores alpha through SH0's alpha byte (already 0..255).
    const a = sh0Img.data[idx + 3];

    // Write .splat record (little-endian floats; bytes are byte-order-neutral).
    view.setFloat32(base + 0,  px, true);
    view.setFloat32(base + 4,  py, true);
    view.setFloat32(base + 8,  pz, true);
    view.setFloat32(base + 12, sx, true);
    view.setFloat32(base + 16, sy, true);
    view.setFloat32(base + 20, sz, true);
    out[base + 24] = r;
    out[base + 25] = g;
    out[base + 26] = b;
    out[base + 27] = a;
    out[base + 28] = clampByte(qX * 128 + 128);
    out[base + 29] = clampByte(qY * 128 + 128);
    out[base + 30] = clampByte(qZ * 128 + 128);
    out[base + 31] = clampByte(qW * 128 + 128);
  }

  const blob = new Blob([out], { type: 'application/octet-stream' });
  const url = URL.createObjectURL(blob);

  // Defaults guard against an empty/degenerate scene where every coordinate
  // collapsed to the same value — without this the radius would be zero and
  // the auto-frame would put the camera inside the splats.
  const safeMin = isFinite(bbMinX) ? [bbMinX, bbMinY, bbMinZ] : [-1, -1, -1];
  const safeMax = isFinite(bbMaxX) ? [bbMaxX, bbMaxY, bbMaxZ] : [ 1,  1,  1];
  const center: [number, number, number] = [
    (safeMin[0] + safeMax[0]) / 2,
    (safeMin[1] + safeMax[1]) / 2,
    (safeMin[2] + safeMax[2]) / 2,
  ];
  const halfExtent: [number, number, number] = [
    (safeMax[0] - safeMin[0]) / 2,
    (safeMax[1] - safeMin[1]) / 2,
    (safeMax[2] - safeMin[2]) / 2,
  ];
  const radius = Math.max(Math.hypot(halfExtent[0], halfExtent[1], halfExtent[2]), 0.1);

  return {
    url,
    count,
    shDropped: Boolean(meta.shN),
    bbox: {
      min: safeMin as [number, number, number],
      max: safeMax as [number, number, number],
      center,
      radius,
    },
  };
}
