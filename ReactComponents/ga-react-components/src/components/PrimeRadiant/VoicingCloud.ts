// src/components/PrimeRadiant/VoicingCloud.ts
//
// Phase 2 of the ix → GA voicings-in-Prime-Radiant integration plan
// (see ix:docs/plans/2026-05-02-voicings-in-prime-radiant.md). Adds a
// THREE.Group containing a single THREE.Points object covering all
// 688K voicings from the OPTIC-K corpus, fetched from ix's `serve_viz`
// over plain HTTP.
//
// Self-contained: no React, no force-graph dependency. Caller hands us
// the scene; we add the group, return a handle with `dispose()`.
//
// Activated by URL param `?voicings=1` in ForceRadiant. Override the ix
// host with `?voicings_url=http://other-host:8765`.

import * as THREE from 'three';

const COLORS: Record<string, [number, number, number]> = {
  guitar:  [0.306, 0.631, 1.000],
  bass:    [0.482, 0.847, 0.561],
  ukulele: [0.961, 0.627, 0.290],
};

interface InstrumentBlock {
  name: string;
  offset: number;
  count: number;
}

interface PositionMeta {
  total: number;
  instruments: InstrumentBlock[];
  bounds: { min: [number, number, number]; max: [number, number, number] };
}

export interface VoicingCloudHandle {
  group: THREE.Group;
  dispose: () => void;
  // Spread reapplies triangular-noise jitter; matches the local /3d
  // viewer's slider. Useful because the OPTIC-K precompute packs ~99%
  // of each instrument into a sub-1-unit cluster knot — without spread
  // they collapse to sub-pixel blobs at any reasonable zoom.
  setSpread: (amount: number) => void;
  setPointSize: (size: number) => void;
}

export interface VoicingCloudOptions {
  /// Base URL of ix's serve_viz. Default `http://127.0.0.1:8765`.
  serveUrl?: string;
  /// Initial per-axis jitter. Default 1.5 (matches the plan doc default).
  spread?: number;
  /// Initial Three.js PointsMaterial.size. Default 0.3.
  pointSize?: number;
}

/**
 * Fetches the binary positions buffer + metadata sidecar from ix and
 * builds a Points object. Async because the binary is ~8 MB. Returns a
 * handle the caller can dispose to free GPU memory.
 *
 * Errors surface via the returned promise — the caller decides how to
 * report them (HUD toast, console, etc.). We never throw into the
 * scene, only return.
 */
export async function createVoicingCloud(
  scene: THREE.Scene,
  options: VoicingCloudOptions = {},
): Promise<VoicingCloudHandle> {
  const serveUrl = (options.serveUrl ?? 'http://127.0.0.1:8765').replace(/\/$/, '');
  const initialSpread = options.spread ?? 1.5;
  const initialSize = options.pointSize ?? 0.3;

  const [meta, basePositions] = await Promise.all([
    fetchMeta(`${serveUrl}/data/voicing-positions.meta.json`),
    fetchPositions(`${serveUrl}/data/voicing-positions.bin`),
  ]);
  if (basePositions.length !== meta.total * 3) {
    throw new Error(
      `voicing-positions size mismatch: bin has ${basePositions.length / 3} points, meta says ${meta.total}`,
    );
  }

  const group = new THREE.Group();
  group.name = 'voicing-cloud';

  let geometry = new THREE.BufferGeometry();
  let material = new THREE.PointsMaterial({
    size: initialSize,
    vertexColors: true,
    sizeAttenuation: true,
    transparent: true,
    opacity: 0.85,
    depthWrite: false,
  });
  let pointsObject = new THREE.Points(geometry, material);
  group.add(pointsObject);
  scene.add(group);

  let spread = initialSpread;

  function rebuildGeometry(): void {
    const positions = new Float32Array(meta.total * 3);
    const colors = new Float32Array(meta.total * 3);
    let dst = 0;
    for (const block of meta.instruments) {
      const colour = COLORS[block.name] ?? [1, 1, 1];
      const srcStart = block.offset * 3;
      const srcEnd = srcStart + block.count * 3;
      positions.set(basePositions.subarray(srcStart, srcEnd), dst * 3);
      if (spread > 0) {
        // Triangular distribution (avg of two uniforms) per axis.
        for (let i = dst * 3, end = (dst + block.count) * 3; i < end; i++) {
          positions[i] += (Math.random() + Math.random() - 1) * spread;
        }
      }
      for (let i = 0; i < block.count; i++) {
        colors[(dst + i) * 3]     = colour[0];
        colors[(dst + i) * 3 + 1] = colour[1];
        colors[(dst + i) * 3 + 2] = colour[2];
      }
      dst += block.count;
    }
    const next = new THREE.BufferGeometry();
    next.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    next.setAttribute('color', new THREE.BufferAttribute(colors, 3));
    pointsObject.geometry.dispose();
    pointsObject.geometry = next;
    geometry = next;
  }

  rebuildGeometry();

  return {
    group,
    dispose: () => {
      scene.remove(group);
      pointsObject.geometry.dispose();
      pointsObject.material.dispose();
      group.clear();
    },
    setSpread: (amount: number) => {
      spread = amount;
      rebuildGeometry();
    },
    setPointSize: (size: number) => {
      material.size = size;
    },
  };
}

async function fetchMeta(url: string): Promise<PositionMeta> {
  const res = await fetch(url);
  if (!res.ok) throw new Error(`${url}: HTTP ${res.status}`);
  const data = await res.json();
  // Defensive validation — the schema is a public contract, but failing
  // loud in the browser beats failing silent in the renderer.
  if (typeof data?.total !== 'number' || !Array.isArray(data?.instruments) || !data?.bounds) {
    throw new Error(`${url}: payload does not match voicings.payload.v1 meta shape`);
  }
  return data as PositionMeta;
}

async function fetchPositions(url: string): Promise<Float32Array> {
  const res = await fetch(url);
  if (!res.ok) throw new Error(`${url}: HTTP ${res.status}`);
  const buf = await res.arrayBuffer();
  return new Float32Array(buf);
}
