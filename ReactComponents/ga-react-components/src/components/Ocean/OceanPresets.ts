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
    skyGradient: ['#6a7a90', '#8a94a2', '#a0a6ae', '#9aa0aa', '#808890', '#606870'],
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
    sunElevation: 4,
    sunAzimuth: 270,
    exposure: 1.15,
    skyGradient: ['#2a3850', '#5a4860', '#a06050', '#e08a50', '#c0603a', '#3a2030'],
    sunColor: new THREE.Color(0xffb070),
  },
  night: {
    name: 'night',
    label: 'Night',
    sunElevation: -8,
    sunAzimuth: 240,
    exposure: 0.5,
    skyGradient: ['#050812', '#0a1020', '#151e35', '#1a253f', '#0f1828', '#050810'],
    sunColor: new THREE.Color(0x4a6090),
  },
};

/**
 * Build a sky gradient CanvasTexture from a preset.
 */
export function buildSkyTexture(preset: OceanPreset): THREE.CanvasTexture {
  const canvas = document.createElement('canvas');
  canvas.width = 1;
  canvas.height = 512;
  const ctx = canvas.getContext('2d')!;
  const grad = ctx.createLinearGradient(0, 0, 0, 512);
  const stops = preset.skyGradient;
  stops.forEach((color, i) => {
    grad.addColorStop(i / (stops.length - 1), color);
  });
  ctx.fillStyle = grad;
  ctx.fillRect(0, 0, 1, 512);
  const tex = new THREE.CanvasTexture(canvas);
  tex.mapping = THREE.EquirectangularReflectionMapping;
  tex.colorSpace = THREE.SRGBColorSpace;
  return tex;
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
