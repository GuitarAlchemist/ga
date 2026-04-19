// src/components/Ocean/OceanPresets.ts
// Visual presets for the ocean demo — sun angle, exposure, sky colors.
// These don't regenerate the FFT spectrum (expensive), they just swap
// lighting and atmosphere parameters.

import * as THREE from 'three';

export type OceanPresetName = 'calm' | 'stormy' | 'sunset' | 'night';

export interface OceanPreset {
  name: OceanPresetName;
  label: string;
  sunElevation: number;   // degrees
  sunAzimuth: number;     // degrees
  exposure: number;       // renderer toneMappingExposure
  skyGradient: string[];  // CSS colors, top → bottom (6 stops)
  sunColor: THREE.Color;
}

export const OCEAN_PRESETS: Record<OceanPresetName, OceanPreset> = {
  calm: {
    name: 'calm',
    label: 'Calm',
    sunElevation: 35,
    sunAzimuth: 210,
    exposure: 1.0,
    // Clear-day sky: deep blue zenith → lighter blue upper → pale horizon
    // haze → warmer reflected band below horizon.
    skyGradient: ['#1e4d8c', '#3d7cc9', '#6aaeda', '#a0cbe4', '#d5deea', '#8fa5b8'],
    sunColor: new THREE.Color(0xfff8e8),
  },
  stormy: {
    name: 'stormy',
    label: 'Stormy',
    sunElevation: 12,
    sunAzimuth: 200,
    exposure: 0.75,
    skyGradient: ['#3a4050', '#4a5260', '#5a6270', '#606870', '#4a5260', '#3a4050'],
    sunColor: new THREE.Color(0xccd5e0),
  },
  sunset: {
    name: 'sunset',
    label: 'Sunset',
    // Sun parked right on the horizon (elevation ≈ 1°). The gradient stop
    // positions below place the brightest orange band at the horizon line
    // (v ≈ 0.52 in equirect coords) so the sun reads as "on the water".
    sunElevation: 1,
    sunAzimuth: 270,
    exposure: 1.15,
    // 8 stops: deep violet zenith → purple transition → peach above
    // horizon → bright orange horizon glow → darker water reflection
    // → very dark nadir. Clouds are layered on top in buildSkyTexture.
    skyGradient: [
      '#1a2036', '#3d4580', '#8a5a7a', '#d68570',
      '#ff9555', '#a0583a', '#3d1e28', '#0a0510',
    ],
    sunColor: new THREE.Color(0xffb070),
  },
  night: {
    name: 'night',
    label: 'Night',
    // Moon position — above horizon, slightly off-centre forward, so the
    // default camera (looking down -Z = az 180°) sees it on first load.
    sunElevation: 26,
    sunAzimuth: 200,
    exposure: 0.55,
    skyGradient: ['#040611', '#0a1428', '#162244', '#1f2c52', '#162236', '#0a1322'],
    sunColor: new THREE.Color(0xb8c8e8),  // cool moonlight
  },
};

// Base-relative so the same component works when deployed at '/' (main
// app) or at a sub-path like '/ga/' (GitHub Pages banner).
const BASE = import.meta.env.BASE_URL;
export const NIGHT_SKY_URL = `${BASE}textures/8k_stars_milky_way.jpg`;
export const MOON_TEX_URL = `${BASE}textures/planets/8k_moon.jpg`;
export const MOON_DISP_URL = `${BASE}textures/planets/2k_moon_displacement.jpg`;

/**
 * Returns an immediately-usable gradient texture, plus (for night) a Promise
 * resolving to a fresh Texture once the Milky Way panorama has loaded.
 *
 * Why this shape: the WebGPU renderer caches the equirect→cube conversion
 * against the Texture object identity. Mutating tex.image after the
 * conversion has run does not re-trigger conversion — we must swap the
 * whole Texture instance.
 */
export function buildSkyTexture(preset: OceanPreset): {
  texture: THREE.Texture;
  ready: Promise<THREE.Texture>;
} {
  // Sunset uses a wider canvas so we can paint cloud streaks at varying
  // azimuths; other presets only need the vertical gradient (uniform in
  // azimuth) so the 2-px-wide canvas stays cheap.
  const isSunset = preset.name === 'sunset';
  const w = isSunset ? 1024 : 2;
  const h = 512;
  const canvas = document.createElement('canvas');
  canvas.width = w;
  canvas.height = h;
  const ctx = canvas.getContext('2d')!;
  const grad = ctx.createLinearGradient(0, 0, 0, h);
  preset.skyGradient.forEach((color, i) => {
    grad.addColorStop(i / (preset.skyGradient.length - 1), color);
  });
  ctx.fillStyle = grad;
  ctx.fillRect(0, 0, w, h);

  if (isSunset) paintSunsetClouds(ctx, w, h);

  const fallback = new THREE.CanvasTexture(canvas);
  fallback.mapping = THREE.EquirectangularReflectionMapping;
  fallback.colorSpace = THREE.SRGBColorSpace;
  fallback.anisotropy = 8;

  if (preset.name !== 'night') {
    return { texture: fallback, ready: Promise.resolve(fallback) };
  }

  // Same-origin: don't set crossOrigin (avoids dev-server CORS edge cases).
  // Single retry on error to dodge transient HMR / StrictMode races.
  const loadOnce = (attempt: number) => new Promise<THREE.Texture>((resolve, reject) => {
    const img = new Image();
    img.onload = () => {
      const real = new THREE.Texture(img);
      real.mapping = THREE.EquirectangularReflectionMapping;
      real.colorSpace = THREE.SRGBColorSpace;
      real.anisotropy = 8;
      real.needsUpdate = true;
      console.info(`[Ocean] Milky Way panorama loaded (${img.naturalWidth}×${img.naturalHeight}, attempt ${attempt})`);
      resolve(real);
    };
    img.onerror = () => reject(new Error(`night sky load attempt ${attempt} failed`));
    img.src = NIGHT_SKY_URL;
  });

  const ready = loadOnce(1).catch(() => loadOnce(2)).catch(err => {
    console.warn(`[Ocean] Failed to load night sky at ${NIGHT_SKY_URL} — gradient fallback only.`, err);
    throw err;
  });

  return { texture: fallback, ready };
}

/**
 * Paint cirrus / altocumulus cloud streaks onto the sunset canvas.
 * Canvas is equirectangular (x=azimuth, y=altitude). Horizon sits at
 * y ≈ 0.52 based on the sunset gradient. Lower clouds = warmer tint
 * (catch horizon light), higher cirrus = cooler pink-purple.
 */
function paintSunsetClouds(ctx: CanvasRenderingContext2D, w: number, h: number): void {
  type Cloud = { xf: number; yf: number; rx: number; ry: number; alpha: number; rgb: string };
  const clouds: Cloud[] = [
    { xf: 0.30, yf: 0.32, rx: 140, ry: 6,  alpha: 0.28, rgb: '220 200 230' }, // high thin pink
    { xf: 0.15, yf: 0.40, rx: 210, ry: 11, alpha: 0.45, rgb: '240 180 200' }, // cirrus
    { xf: 0.55, yf: 0.36, rx: 260, ry: 9,  alpha: 0.38, rgb: '230 170 210' },
    { xf: 0.82, yf: 0.42, rx: 180, ry: 10, alpha: 0.52, rgb: '255 180 160' }, // altocumulus
    { xf: 0.10, yf: 0.48, rx: 240, ry: 14, alpha: 0.65, rgb: '255 160 120' }, // mid warm
    { xf: 0.40, yf: 0.50, rx: 220, ry: 12, alpha: 0.60, rgb: '255 170 110' },
    { xf: 0.70, yf: 0.54, rx: 200, ry: 13, alpha: 0.70, rgb: '255 150 100' }, // near horizon
    { xf: 0.92, yf: 0.58, rx: 170, ry: 11, alpha: 0.55, rgb: '240 130 90'  },
  ];
  for (const c of clouds) {
    const cx = c.xf * w;
    const cy = c.yf * h;
    const g = ctx.createRadialGradient(cx, cy, 0, cx, cy, c.rx);
    g.addColorStop(0, `rgba(${c.rgb}, ${c.alpha})`);
    g.addColorStop(1, `rgba(${c.rgb}, 0)`);
    ctx.save();
    ctx.translate(cx, cy);
    ctx.scale(1, c.ry / c.rx);
    ctx.translate(-cx, -cy);
    ctx.fillStyle = g;
    ctx.beginPath();
    ctx.arc(cx, cy, c.rx, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
  }
}

/**
 * Compute sun direction vector from preset angles.
 */
export function sunDirectionFromPreset(preset: OceanPreset): THREE.Vector3 {
  const phi = THREE.MathUtils.degToRad(preset.sunElevation);
  const theta = THREE.MathUtils.degToRad(preset.sunAzimuth);
  return new THREE.Vector3().setFromSphericalCoords(
    1,
    Math.PI / 2 - phi,
    theta,
  );
}
