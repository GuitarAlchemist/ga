// src/components/PrimeRadiant/ReactionDiffusionSim.ts
// CPU Gray-Scott reaction-diffusion simulation for governance crisis textures.
//
// Two chemicals U (substrate) and V (activator) react and diffuse.
// The resulting Turing patterns encode governance health:
//   - Stable spots: fragmented but functional adoption
//   - Stripes: polarized blocs
//   - Smooth saturation: institutional convergence
//   - Chaotic: governance breakdown (Seldon Crisis)
//   - Dead/uniform: stale connection, no activity

import * as THREE from 'three';
import type { GovernanceHealthStatus } from './types';

// ── Gray-Scott parameters mapped to governance health ──

interface GrayScottParams {
  f: number;  // feed rate
  k: number;  // kill rate
}

/** Health status → RD parameters. Stable patterns for healthy, chaotic for crisis. */
const HEALTH_PARAMS: Record<GovernanceHealthStatus | 'default', GrayScottParams> = {
  healthy:  { f: 0.055, k: 0.062 },  // stable spots — ordered governance
  warning:  { f: 0.04,  k: 0.06 },   // labyrinthine — complex but holding
  error:    { f: 0.025, k: 0.06 },   // chaotic mitosis — breaking down
  unknown:  { f: 0.035, k: 0.065 },  // sparse spots — uncertain
  default:  { f: 0.055, k: 0.062 },
};

// ── Diffusion rates ──
const Du = 0.2097;
const Dv = 0.105;
const DT = 1.0; // time step

// ── Grid ──

export class GrayScottGrid {
  readonly width: number;
  readonly height: number;
  private u: Float32Array;
  private v: Float32Array;
  private uNext: Float32Array;
  private vNext: Float32Array;
  private params: GrayScottParams;
  private texture: THREE.DataTexture;
  private textureData: Uint8Array;

  constructor(width: number, height: number) {
    this.width = width;
    this.height = height;
    const size = width * height;
    this.u = new Float32Array(size).fill(1.0);
    this.v = new Float32Array(size).fill(0.0);
    this.uNext = new Float32Array(size);
    this.vNext = new Float32Array(size);
    this.params = { ...HEALTH_PARAMS.healthy };

    // RGBA texture for GPU upload
    this.textureData = new Uint8Array(size * 4);
    this.texture = new THREE.DataTexture(this.textureData, width, height);
    this.texture.magFilter = THREE.LinearFilter;
    this.texture.minFilter = THREE.LinearFilter;
    this.texture.wrapS = THREE.RepeatWrapping;
    this.texture.wrapT = THREE.RepeatWrapping;

    // Seed with initial perturbation
    this.seed();
  }

  /** Seed V chemical in a few random spots to kickstart patterns */
  seed(): void {
    const w = this.width, h = this.height;
    this.u.fill(1.0);
    this.v.fill(0.0);

    // Place 3-5 random seed clusters
    const numSeeds = 3 + (Math.random() * 3) | 0;
    for (let s = 0; s < numSeeds; s++) {
      const cx = (Math.random() * w) | 0;
      const cy = (Math.random() * h) | 0;
      const radius = 3 + (Math.random() * 4) | 0;
      for (let dy = -radius; dy <= radius; dy++) {
        for (let dx = -radius; dx <= radius; dx++) {
          if (dx * dx + dy * dy > radius * radius) continue;
          const x = (cx + dx + w) % w;
          const y = (cy + dy + h) % h;
          const idx = y * w + x;
          this.u[idx] = 0.5;
          this.v[idx] = 0.25 + Math.random() * 0.1;
        }
      }
    }
  }

  /** Set health status → adjusts f/k parameters */
  setHealth(status: GovernanceHealthStatus): void {
    const target = HEALTH_PARAMS[status] ?? HEALTH_PARAMS.default;
    // Smooth transition (don't snap instantly)
    this.params.f += (target.f - this.params.f) * 0.1;
    this.params.k += (target.k - this.params.k) * 0.1;
  }

  /** Run N iterations of Gray-Scott */
  step(iterations: number): void {
    const w = this.width, h = this.height;
    const { f, k } = this.params;

    for (let iter = 0; iter < iterations; iter++) {
      for (let y = 0; y < h; y++) {
        for (let x = 0; x < w; x++) {
          const idx = y * w + x;

          // 5-point Laplacian stencil (toroidal wrapping)
          const xm = ((x - 1) + w) % w;
          const xp = (x + 1) % w;
          const ym = ((y - 1) + h) % h;
          const yp = (y + 1) % h;

          const lapU = this.u[y * w + xm] + this.u[y * w + xp]
                     + this.u[ym * w + x] + this.u[yp * w + x]
                     - 4.0 * this.u[idx];

          const lapV = this.v[y * w + xm] + this.v[y * w + xp]
                     + this.v[ym * w + x] + this.v[yp * w + x]
                     - 4.0 * this.v[idx];

          const uVal = this.u[idx];
          const vVal = this.v[idx];
          const uvv = uVal * vVal * vVal;

          this.uNext[idx] = uVal + DT * (Du * lapU - uvv + f * (1.0 - uVal));
          this.vNext[idx] = vVal + DT * (Dv * lapV + uvv - (k + f) * vVal);

          // Clamp
          this.uNext[idx] = Math.max(0, Math.min(1, this.uNext[idx]));
          this.vNext[idx] = Math.max(0, Math.min(1, this.vNext[idx]));
        }
      }

      // Swap buffers
      [this.u, this.uNext] = [this.uNext, this.u];
      [this.v, this.vNext] = [this.vNext, this.v];
    }
  }

  /** Convert current state to DataTexture (call after step()) */
  updateTexture(): THREE.DataTexture {
    const size = this.width * this.height;
    for (let i = 0; i < size; i++) {
      const i4 = i * 4;
      const vVal = this.v[i];
      // Map V chemical to color intensity
      // Low V = dark (no activity), High V = bright (active pattern)
      const intensity = Math.min(255, (vVal * 4.0 * 255) | 0);
      this.textureData[i4] = intensity;           // R: pattern intensity
      this.textureData[i4 + 1] = (intensity * 0.7) | 0; // G: slightly less (warm tint)
      this.textureData[i4 + 2] = (intensity * 0.3) | 0; // B: low (warm)
      this.textureData[i4 + 3] = 255;             // A: full
    }
    this.texture.needsUpdate = true;
    return this.texture;
  }

  /** Get the DataTexture for material sampling */
  getTexture(): THREE.DataTexture {
    return this.texture;
  }

  dispose(): void {
    this.texture.dispose();
  }
}
