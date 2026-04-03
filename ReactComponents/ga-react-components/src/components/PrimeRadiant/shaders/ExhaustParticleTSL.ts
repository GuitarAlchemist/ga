// src/components/PrimeRadiant/shaders/ExhaustParticleTSL.ts
// TSL exhaust particle material for Lunar Lander engine plume.
// Orange-to-red fading circular point sprites with additive blending.
// Renderer-agnostic: auto-compiles to GLSL (WebGL2) or WGSL (WebGPU).

import * as THREE from 'three';
import { PointsNodeMaterial } from 'three/webgpu';
import {
  Fn, float, vec2, vec3,
  attribute,
  pointUV, Discard, dot, smoothstep, sqrt, mix,
} from 'three/tsl';

/**
 * Create a TSL-based exhaust particle material for Lunar Lander.
 *
 * Expects per-particle attributes on the BufferGeometry:
 *   - aAge: float — current particle age (seconds)
 *   - aMaxLife: float — maximum particle lifetime (seconds)
 *
 * Point sprites render as circular discs that shrink and fade
 * from bright orange to deep red over their lifetime.
 */
export function createExhaustParticleMaterialTSL(): PointsNodeMaterial {
  const material = new PointsNodeMaterial();

  // Per-particle attributes
  const aAge = attribute('aAge', 'float');
  const aMaxLife = attribute('aMaxLife', 'float');

  // Normalized age: 0 = birth, 1 = death
  const t = aAge.div(aMaxLife);

  // ── Size: shrink quadratically over lifetime ──
  // size = 8 * (1 - t^2)
  material.sizeNode = float(8.0).mul(float(1.0).sub(t.mul(t)));

  // ── Color: orange to red fade ──
  material.colorNode = Fn(() => {
    const orange = vec3(1.0, 0.8, 0.4);
    const red = vec3(1.0, 0.4, 0.0);
    return mix(orange, red, t);
  })();

  // ── Opacity: circular disc + age fade ──
  material.opacityNode = Fn(() => {
    // Point UV centered: (0,0) at center, disc radius = 0.5
    const c = pointUV.sub(vec2(0.5, 0.5));
    const dist = dot(c, c);

    // Discard outside circular radius
    Discard(dist.greaterThan(0.25));

    // Soft disc edge
    const discAlpha = smoothstep(0.5, 0.1, sqrt(dist));

    // Fade out over lifetime
    const fadeOut = float(1.0).sub(t);

    return discAlpha.mul(fadeOut).mul(0.8);
  })();

  // Material settings
  material.transparent = true;
  material.depthWrite = false;
  material.blending = THREE.AdditiveBlending;
  material.sizeAttenuation = true;

  return material;
}

/**
 * Create a BufferGeometry for exhaust particles with required attributes.
 *
 * @param maxParticles Maximum particle count
 * @returns Geometry + typed arrays for CPU-side updates each frame
 */
export function createExhaustParticleGeometry(maxParticles: number): {
  geometry: THREE.BufferGeometry;
  positions: Float32Array;
  ages: Float32Array;
  maxLives: Float32Array;
} {
  const positions = new Float32Array(maxParticles * 3);
  const ages = new Float32Array(maxParticles);
  const maxLives = new Float32Array(maxParticles);

  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3).setUsage(THREE.DynamicDrawUsage));
  geometry.setAttribute('aAge', new THREE.BufferAttribute(ages, 1).setUsage(THREE.DynamicDrawUsage));
  geometry.setAttribute('aMaxLife', new THREE.BufferAttribute(maxLives, 1).setUsage(THREE.DynamicDrawUsage));

  // Start with zero visible particles
  geometry.setDrawRange(0, 0);

  return { geometry, positions, ages, maxLives };
}
