// src/components/PrimeRadiant/shaders/VoronoiShellTSL.ts
// TSL material for Voronoi jurisdiction shells — translucent membranes
// showing authority boundaries between governance clusters.
//
// Governance meaning: each cluster of nodes exists within a jurisdictional
// "territory." Where two territories meet, a glowing membrane wall appears.
// This makes visible the invisible fact that every governance artifact
// displaces the authority of its neighbors ("Jurisdictional Pressure").

import * as THREE from 'three';
import { MeshBasicNodeMaterial } from 'three/webgpu';
import {
  Fn, float, vec3, vec4,
  uniform, time,
  positionWorld, normalWorld, cameraPosition,
  mix, smoothstep, pow, abs, sin, min, length,
} from 'three/tsl';
import { noise3 } from './TSLNoiseLib';
import type { QualityTier } from './TSLUniforms';

// ── Types ──

export interface VoronoiShellOptions {
  /** Cluster color (gold for constitutional, silver for dept, etc.) */
  color: THREE.Color;
  /** Number of seed positions (governance nodes in this cluster) */
  seedCount: number;
  /** Quality tier — controls Voronoi complexity */
  quality: QualityTier;
}

// ── Constants ──

/** Max seeds per shell (WebGL2 uniform limit is 256 vec4s — 32 vec3s is safe) */
const MAX_SEEDS = 32;

// ── Cluster color palette by governance type ──

export const CLUSTER_COLORS: Record<string, THREE.Color> = {
  constitution: new THREE.Color(0xffd700), // gold
  department: new THREE.Color(0xc0c0c0),   // silver
  policy: new THREE.Color(0xcd7f32),       // bronze/copper
  persona: new THREE.Color(0x88ccff),      // ice blue
  pipeline: new THREE.Color(0x73d117),     // green
  schema: new THREE.Color(0xff6b6b),       // coral
  test: new THREE.Color(0xb388ff),         // lavender
  default: new THREE.Color(0x668899),      // slate
};

// ── Material factory ──

/**
 * Create a TSL material for a Voronoi jurisdiction shell.
 *
 * The shell is a translucent sphere with glowing boundary lines where
 * governance jurisdictions meet. Seed positions (node positions within
 * the cluster) drive the Voronoi cell pattern.
 *
 * @param options Shell configuration
 * @returns Material + seed position uniform for per-frame updates
 */
export function createVoronoiShellMaterial(options: VoronoiShellOptions): {
  material: MeshBasicNodeMaterial;
  /** Update seed positions each frame (world-space positions relative to shell center) */
  seedPositions: THREE.Uniform<THREE.Vector3[]>;
  /** Update active seed count (may be less than array length) */
  activeSeedCount: THREE.Uniform<number>;
} {
  const { color, seedCount, quality } = options;
  const material = new MeshBasicNodeMaterial();
  material.transparent = true;
  material.side = THREE.DoubleSide;
  material.depthWrite = false;
  material.blending = THREE.AdditiveBlending;

  // Uniform: seed positions (governance node positions relative to shell center)
  const seedArray = new Array(MAX_SEEDS).fill(null).map(() => new THREE.Vector3());
  const seedPositionsUniform = new THREE.Uniform(seedArray);
  const activeSeedCountUniform = new THREE.Uniform(Math.min(seedCount, MAX_SEEDS));

  // TSL uniforms
  const uSeeds = uniform(seedArray);
  const uSeedCount = uniform(activeSeedCountUniform.value);
  const uColor = uniform(color);

  // ── Low quality: plain translucent shell, no Voronoi ──
  if (quality === 'low') {
    const rimLow = Fn(() => {
      const viewDir = cameraPosition.sub(positionWorld).normalize();
      const fresnel = float(1.0).sub(abs(normalWorld.dot(viewDir)));
      return pow(fresnel, float(3.0)).mul(0.08);
    });
    material.colorNode = Fn(() => vec3(uColor).mul(rimLow()))();
    material.opacityNode = Fn(() => rimLow())();
    material.transparent = true;
    material.depthWrite = false;
    return { material, seedPositions: seedPositionsUniform, activeSeedCount: activeSeedCountUniform };
  }

  // ── Medium/High quality: Voronoi boundary computation ──
  material.colorNode = Fn(() => {
    const worldPos = positionWorld;
    const viewDir = cameraPosition.sub(worldPos).normalize();
    const fresnel = float(1.0).sub(abs(normalWorld.dot(viewDir)));

    // Find nearest and second-nearest seed distances
    const nearestDist = float(1e6).toVar();
    const secondDist = float(1e6).toVar();

    // Unrolled seed search (TSL doesn't have dynamic loops over uniforms easily)
    // We check up to MAX_SEEDS but only the first `uSeedCount` are valid
    const checkSeed = (idx: number) => {
      const seedPos = (uSeeds.value as THREE.Vector3[])[idx];
      if (!seedPos) return;
      const seedUniform = uniform(seedPos);
      const diff = worldPos.sub(seedUniform);
      const d = length(diff);
      // Update nearest/second nearest
      const isNearer = d.lessThan(nearestDist);
      const isSecond = d.lessThan(secondDist).and(isNearer.not());
      secondDist.assign(mix(secondDist, min(secondDist, d), float(isSecond)));
      secondDist.assign(mix(secondDist, nearestDist, float(isNearer)));
      nearestDist.assign(min(nearestDist, d));
    };

    // Check seeds (up to 16 for medium, 32 for high)
    const maxCheck = quality === 'high' ? MAX_SEEDS : 16;
    for (let i = 0; i < maxCheck; i++) {
      checkSeed(i);
    }

    // Edge glow: where distance difference between nearest two cells is small
    const edgeDiff = secondDist.sub(nearestDist);
    const edgeWidth = quality === 'high' ? 0.3 : 0.5; // thinner edges on high quality
    const edgeGlow = smoothstep(edgeWidth, 0.0, edgeDiff);

    // Fresnel rim for depth perception
    const rim = pow(fresnel, float(2.5)).mul(0.12);

    // Animated pulse on edges (high quality only)
    const pulse = quality === 'high'
      ? float(1.0).add(sin(time.mul(1.5).add(nearestDist.mul(3.0))).mul(0.15))
      : float(1.0);

    // Noise displacement on boundary (high quality)
    const noiseDisp = quality === 'high'
      ? noise3(worldPos.mul(8.0).add(vec3(time.mul(0.2)))).mul(0.1)
      : float(0.0);

    // Combine: edge glow + rim, colored by cluster type
    const brightness = edgeGlow.mul(pulse).add(noiseDisp).add(rim);
    return vec3(uColor).mul(brightness);
  })();

  // Shell opacity — same brightness formula, scaled by 0.35 for translucency
  material.opacityNode = Fn(() => {
    const worldPos = positionWorld;
    const viewDir = cameraPosition.sub(worldPos).normalize();
    const fresnel = float(1.0).sub(abs(normalWorld.dot(viewDir)));

    const nearestDist = float(1e6).toVar();
    const secondDist = float(1e6).toVar();
    const checkSeedO = (idx: number) => {
      const seedPos = (uSeeds.value as THREE.Vector3[])[idx];
      if (!seedPos) return;
      const seedUniform = uniform(seedPos);
      const diff = worldPos.sub(seedUniform);
      const d = length(diff);
      const isNearer = d.lessThan(nearestDist);
      const isSecond = d.lessThan(secondDist).and(isNearer.not());
      secondDist.assign(mix(secondDist, min(secondDist, d), float(isSecond)));
      secondDist.assign(mix(secondDist, nearestDist, float(isNearer)));
      nearestDist.assign(min(nearestDist, d));
    };
    const maxCheckO = quality === 'high' ? MAX_SEEDS : 16;
    for (let i = 0; i < maxCheckO; i++) checkSeedO(i);
    const edgeDiff = secondDist.sub(nearestDist);
    const edgeWidth = quality === 'high' ? 0.3 : 0.5;
    const edgeGlow = smoothstep(edgeWidth, 0.0, edgeDiff);
    const rim = pow(fresnel, float(2.5)).mul(0.12);
    const pulse = quality === 'high'
      ? float(1.0).add(sin(time.mul(1.5).add(nearestDist.mul(3.0))).mul(0.15))
      : float(1.0);
    const noiseDisp = quality === 'high'
      ? noise3(worldPos.mul(8.0).add(vec3(time.mul(0.2)))).mul(0.1)
      : float(0.0);
    const brightness = edgeGlow.mul(pulse).add(noiseDisp).add(rim);
    return brightness.mul(0.35);
  })();

  material.transparent = true;
  material.depthWrite = false;

  return { material, seedPositions: seedPositionsUniform, activeSeedCount: activeSeedCountUniform };
}
