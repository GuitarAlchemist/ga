// src/components/PrimeRadiant/shaders/FlowFieldParticleTSL.ts
// TSL material for flow field compliance particles.
//
// Governance meaning: particles flow along governance directive paths.
// Smooth laminar flow = healthy compliance. Vortices = bureaucratic churn.
// Dead zones = blocked governance pathways.
//
// Per-particle attributes:
//   - health: 0.0 (violation) to 1.0 (compliant) → red-to-green color
//   - speed: particle velocity magnitude → size scaling
//   - phase: lifecycle progress 0-1 → opacity fade in/out

import * as THREE from 'three';
import { PointsNodeMaterial } from 'three/webgpu';
import {
  Fn, float, vec3, vec4,
  attribute, time,
  mix, smoothstep, pow, sin, abs,
} from 'three/tsl';
import type { QualityTier } from './TSLUniforms';

// ── Types ──

export interface FlowParticleMaterialOptions {
  quality: QualityTier;
}

// ── Color palette ──

const COMPLIANT_COLOR = new THREE.Color(0x44ff88);   // green
const WARNING_COLOR = new THREE.Color(0xffaa22);     // amber
const VIOLATION_COLOR = new THREE.Color(0xff3344);   // red

// ── Material factory ──

/**
 * Create a TSL PointsNodeMaterial for compliance flow particles.
 *
 * Expects per-particle attributes on the BufferGeometry:
 *   - aHealth: float [0-1] compliance health
 *   - aPhase: float [0-1] lifecycle phase (for fade in/out)
 *   - aSpeed: float [0+] velocity magnitude (for size scaling)
 */
export function createFlowParticleMaterial(options: FlowParticleMaterialOptions): PointsNodeMaterial {
  const { quality } = options;
  const material = new PointsNodeMaterial();
  material.transparent = true;
  material.depthWrite = false;
  material.blending = THREE.AdditiveBlending;
  material.sizeAttenuation = true;

  // Per-particle attributes (set on the BufferGeometry, read here)
  const aHealth = attribute('aHealth', 'float');
  const aPhase = attribute('aPhase', 'float');
  const aSpeed = attribute('aSpeed', 'float');

  // ── Size: larger for faster/more-active particles ──
  const baseSize = quality === 'high' ? 3.0 : quality === 'medium' ? 2.5 : 2.0;
  material.sizeNode = float(baseSize).add(aSpeed.mul(1.5));

  // ── Color: health-based gradient ──
  material.colorNode = Fn(() => {
    // Two-stop gradient: violation (0) → warning (0.5) → compliant (1.0)
    const violationToWarning = mix(
      vec3(VIOLATION_COLOR.r, VIOLATION_COLOR.g, VIOLATION_COLOR.b),
      vec3(WARNING_COLOR.r, WARNING_COLOR.g, WARNING_COLOR.b),
      smoothstep(0.0, 0.5, aHealth),
    );
    const col = mix(
      violationToWarning,
      vec3(COMPLIANT_COLOR.r, COMPLIANT_COLOR.g, COMPLIANT_COLOR.b),
      smoothstep(0.5, 1.0, aHealth),
    ).toVar();

    // High quality: subtle shimmer
    if (quality === 'high') {
      const shimmer = sin(time.mul(3.0).add(aPhase.mul(6.28))).mul(0.08).add(1.0);
      col.mulAssign(shimmer);
    }

    return col;
  })();

  // ── Opacity: fade in at birth (phase 0), fade out at death (phase 1) ──
  material.opacityNode = Fn(() => {
    const fadeIn = smoothstep(0.0, 0.1, aPhase);
    const fadeOut = smoothstep(1.0, 0.85, aPhase);
    const baseOpacity = quality === 'high' ? 0.7 : 0.5;
    return float(baseOpacity).mul(fadeIn).mul(fadeOut);
  })();

  return material;
}

/**
 * Create a BufferGeometry for flow particles with the required attributes.
 *
 * @param maxParticles Maximum particle count
 * @returns Geometry + typed arrays for CPU updates
 */
export function createFlowParticleGeometry(maxParticles: number): {
  geometry: THREE.BufferGeometry;
  positions: Float32Array;
  healths: Float32Array;
  phases: Float32Array;
  speeds: Float32Array;
} {
  const positions = new Float32Array(maxParticles * 3);
  const healths = new Float32Array(maxParticles);
  const phases = new Float32Array(maxParticles);
  const speeds = new Float32Array(maxParticles);

  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3).setUsage(THREE.DynamicDrawUsage));
  geometry.setAttribute('aHealth', new THREE.BufferAttribute(healths, 1).setUsage(THREE.DynamicDrawUsage));
  geometry.setAttribute('aPhase', new THREE.BufferAttribute(phases, 1).setUsage(THREE.DynamicDrawUsage));
  geometry.setAttribute('aSpeed', new THREE.BufferAttribute(speeds, 1).setUsage(THREE.DynamicDrawUsage));

  // Set draw range to 0 initially (populated by engine)
  geometry.setDrawRange(0, 0);

  return { geometry, positions, healths, phases, speeds };
}
