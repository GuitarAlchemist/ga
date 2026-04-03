// src/components/PrimeRadiant/shaders/CymaticsTSL.ts
// TSL material for wave cymatics on governance filaments.
//
// Governance meaning:
//   - Clean standing waves (constructive interference) = aligned governance
//   - Discordant noise (destructive interference) = conflict/misalignment
//   - Wave amplitude = governance activity level
//   - Wave frequency = governance specificity (detailed = high freq)
//
// Applied to TubeGeometry filaments replacing Line geometry.
// Vertex displacement creates physical wave motion visible from any angle.

import * as THREE from 'three';
import { MeshBasicNodeMaterial } from 'three/webgpu';
import {
  Fn, float, vec3,
  uniform, time,
  positionLocal, normalLocal,
  sin, cos, mix, abs, pow, smoothstep,
} from 'three/tsl';
import { noise3 } from './TSLNoiseLib';
import type { QualityTier } from './TSLUniforms';

// ── Types ──

export interface CymaticsMaterialOptions {
  /** 0.0 = full conflict, 1.0 = perfect alignment */
  alignment: number;
  /** Base color for the filament */
  color: THREE.Color;
  /** Quality tier */
  quality: QualityTier;
}

// ── Color palette ──
const ALIGNED_COLOR = new THREE.Color(0x88ddff); // ice blue — harmonious
const CONFLICT_COLOR = new THREE.Color(0xff4422); // red-orange — discordant

// ── Material factory ──

/**
 * Create a TSL material for cymatics wave filaments.
 *
 * Returns material + alignment uniform for per-frame updates.
 */
export function createCymaticsMaterial(options: CymaticsMaterialOptions): {
  material: MeshBasicNodeMaterial;
  /** Update alignment per frame (0=conflict, 1=aligned) */
  alignmentUniform: ReturnType<typeof uniform>;
} {
  const { alignment, quality } = options;
  const material = new MeshBasicNodeMaterial();
  material.transparent = true;
  material.depthWrite = false;
  material.side = THREE.DoubleSide;

  const uAlignment = uniform(alignment);

  // ── Vertex displacement: standing wave + conflict noise ──
  material.positionNode = Fn(() => {
    const pos = positionLocal.toVar();
    const normal = normalLocal;
    const t = time;

    // Wave parameters driven by alignment
    // High alignment = clean sine wave, low = noisy interference
    const waveFreq = float(8.0); // base frequency
    const waveAmp = float(0.03); // base amplitude

    // Primary standing wave (always present)
    const primaryWave = sin(pos.x.mul(waveFreq).add(t.mul(2.0))).mul(waveAmp);

    // Secondary wave (creates interference when alignment < 1)
    const secondaryFreq = waveFreq.mul(1.618); // golden ratio offset
    const secondaryPhase = t.mul(3.1); // different speed = beating pattern
    const secondaryWave = sin(pos.x.mul(secondaryFreq).add(secondaryPhase)).mul(waveAmp.mul(0.7));

    // Conflict noise (high freq, chaotic — only when alignment is low)
    const conflictAmount = float(1.0).sub(uAlignment);
    let conflictDisp = float(0.0);
    if (quality !== 'low') {
      conflictDisp = noise3(pos.mul(20.0).add(vec3(t.mul(4.0)))).sub(0.5).mul(0.04).mul(conflictAmount);
    }

    // Blend: at alignment=1, pure standing wave. At alignment=0, interference + noise
    const displacement = primaryWave.add(secondaryWave.mul(conflictAmount)).add(conflictDisp);

    // Displace along normal
    return pos.add(normal.mul(displacement));
  })();

  // ── Color: alignment gradient + wave brightness modulation ──
  material.colorNode = Fn(() => {
    const t = time;
    const pos = positionLocal;

    // Base color: aligned=blue, conflict=red
    const alignedCol = vec3(ALIGNED_COLOR.r, ALIGNED_COLOR.g, ALIGNED_COLOR.b);
    const conflictCol = vec3(CONFLICT_COLOR.r, CONFLICT_COLOR.g, CONFLICT_COLOR.b);
    const baseCol = mix(conflictCol, alignedCol, uAlignment).toVar();

    // Wave brightness modulation (bright at wave peaks)
    const waveBrightness = sin(pos.x.mul(8.0).add(t.mul(2.0))).mul(0.15).add(1.0);
    baseCol.mulAssign(waveBrightness);

    // High quality: subtle shimmer from secondary interference
    if (quality === 'high') {
      const interferenceShimmer = cos(pos.x.mul(12.944).add(t.mul(3.1))).mul(0.1)
        .mul(float(1.0).sub(uAlignment));
      baseCol.addAssign(vec3(1.0, 0.5, 0.2).mul(interferenceShimmer));
    }

    return baseCol;
  })();

  // ── Opacity: fade at filament ends, brighter in middle ──
  material.opacityNode = Fn(() => {
    const waveEnvelope = smoothstep(0.0, 0.2, abs(positionLocal.x))
      .mul(smoothstep(1.0, 0.8, abs(positionLocal.x)));
    return float(0.6).mul(waveEnvelope);
  })();

  return { material, alignmentUniform: uAlignment };
}
