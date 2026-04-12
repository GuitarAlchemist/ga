// src/components/Ocean/OceanFFTCompute.ts
// Tessendorf FFT ocean simulation — GPU compute pipeline via TSL.
//
// Pipeline (per frame):
//   1. spectrumEvolve: H0(k) × exp(iωt) → H(k,t), Dx(k,t), Dz(k,t)
//   2. IFFT (horizontal + vertical): frequency → spatial domain
//   3. assembleDisplacement: pack height/dx/dz into displacement texture
//   4. computeNormals: finite-difference normals + Jacobian foam
//
// Output: displacementTex (RGBA = height,dx,dz,0) + normalFoamTex (RGBA = nx,ny,nz,foam)

import * as THREE from 'three';
import { StorageTexture, WebGPURenderer } from 'three/webgpu';
import {
  Fn, float, int, vec4,
  ivec2, uvec2,
  uniform, instanceIndex,
  textureStore, textureLoad,
  sin, cos, select, shiftLeft, bitAnd,
  max, clamp, normalize,
} from 'three/tsl';

const G = 9.81;
const TAU = Math.PI * 2;

// ── Gaussian random pair (Box-Muller) ────────────────────────────────────────

function gaussianPair(): [number, number] {
  let u1: number;
  do { u1 = Math.random(); } while (u1 === 0);
  const u2 = Math.random();
  const r = Math.sqrt(-2 * Math.log(u1));
  return [r * Math.cos(TAU * u2), r * Math.sin(TAU * u2)];
}

// ── Phillips spectrum ────────────────────────────────────────────────────────

function phillipsSpectrum(
  kx: number, kz: number,
  windDirX: number, windDirZ: number,
  windSpeed: number, amplitude: number,
): number {
  const k2 = kx * kx + kz * kz;
  if (k2 < 1e-12) return 0;
  const kMag = Math.sqrt(k2);
  const L = windSpeed * windSpeed / G;
  const kDotW = (kx * windDirX + kz * windDirZ) / kMag;
  // Broader directional distribution: |dot|^1.2 instead of dot²
  // Reduces "all waves parallel" effect, produces more chaotic sea
  const directional = Math.pow(Math.abs(kDotW), 1.2);
  // Also allow some waves perpendicular to wind (0.08 base factor)
  const dirMix = 0.08 + 0.92 * directional;
  const phillips = amplitude * Math.exp(-1 / (k2 * L * L)) / (k2 * k2) * dirMix;
  const smallWave = 0.001 * L;
  return phillips * Math.exp(-k2 * smallWave * smallWave);
}

// ── Main class ───────────────────────────────────────────────────────────────

export interface OceanFFTOptions {
  N?: number;
  patchSize?: number;
  windSpeed?: number;
  windDirection?: [number, number];
  amplitude?: number;
  choppiness?: number;
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type ComputeNode = any;

export class OceanFFTCompute {
  readonly N: number;
  private readonly logN: number;
  private readonly patchSize: number;
  private readonly choppiness: number;

  // Output textures (bind to material)
  readonly displacementTex: StorageTexture;
  readonly normalFoamTex: StorageTexture;

  // Internal textures
  private h0Tex: THREE.DataTexture;
  private omegaTex: THREE.DataTexture;
  private htHeightTex: StorageTexture;
  private htDxTex: StorageTexture;
  private htDzTex: StorageTexture;
  private pingTex: StorageTexture;
  private pongTex: StorageTexture;
  private heightResultTex: StorageTexture;
  private dxResultTex: StorageTexture;
  private dzResultTex: StorageTexture;

  // Compute nodes
  private evolveNode: ComputeNode;
  private ifftH_p2p: ComputeNode;  // horizontal ping→pong
  private ifftH_p2pi: ComputeNode; // horizontal pong→ping
  private ifftV_p2p: ComputeNode;  // vertical ping→pong
  private ifftV_p2pi: ComputeNode; // vertical pong→ping
  private assembleNode: ComputeNode;
  private normalNode: ComputeNode;
  private copyNodes = new Map<string, ComputeNode>();

  // Uniforms
  private uTime = uniform(0.0);
  private uStage = uniform(0);

  constructor(options: OceanFFTOptions = {}) {
    const {
      N = 256,
      patchSize = 500,
      windSpeed = 10,
      windDirection = [1, 0.3],
      amplitude = 0.0003,
      choppiness = 1.6,
    } = options;

    this.N = N;
    this.logN = Math.log2(N);
    this.patchSize = patchSize;
    this.choppiness = choppiness;

    const wLen = Math.sqrt(windDirection[0] ** 2 + windDirection[1] ** 2);
    const wdx = windDirection[0] / wLen;
    const wdz = windDirection[1] / wLen;

    // CPU-side spectrum data
    this.h0Tex = this.generateH0(wdx, wdz, windSpeed, amplitude);
    this.omegaTex = this.generateOmega();

    // StorageTextures (all HalfFloat for perf)
    const mk = () => { const t = new StorageTexture(N, N); t.type = THREE.HalfFloatType; return t; };
    this.htHeightTex = mk();
    this.htDxTex = mk();
    this.htDzTex = mk();
    this.pingTex = mk();
    this.pongTex = mk();
    this.heightResultTex = mk();
    this.dxResultTex = mk();
    this.dzResultTex = mk();
    this.displacementTex = mk();
    this.normalFoamTex = mk();

    // Build all compute nodes
    this.evolveNode = this.buildEvolve();
    this.ifftH_p2p = this.buildButterfly(this.pingTex, this.pongTex, false);
    this.ifftH_p2pi = this.buildButterfly(this.pongTex, this.pingTex, false);
    this.ifftV_p2p = this.buildButterfly(this.pingTex, this.pongTex, true);
    this.ifftV_p2pi = this.buildButterfly(this.pongTex, this.pingTex, true);
    this.assembleNode = this.buildAssemble();
    this.normalNode = this.buildNormals();
  }

  // ── Generate H0 spectrum on CPU ──

  private generateH0(wdx: number, wdz: number, windSpeed: number, amplitude: number): THREE.DataTexture {
    const N = this.N;
    const L = this.patchSize;
    const data = new Float32Array(N * N * 4);

    for (let y = 0; y < N; y++) {
      for (let x = 0; x < N; x++) {
        const kx = (x - N / 2) * TAU / L;
        const kz = (y - N / 2) * TAU / L;
        const p1 = phillipsSpectrum(kx, kz, wdx, wdz, windSpeed, amplitude);
        const p2 = phillipsSpectrum(-kx, -kz, wdx, wdz, windSpeed, amplitude);
        const [g1r, g1i] = gaussianPair();
        const [g2r, g2i] = gaussianPair();
        const idx = (y * N + x) * 4;
        data[idx + 0] = g1r * Math.sqrt(p1) / Math.SQRT2;
        data[idx + 1] = g1i * Math.sqrt(p1) / Math.SQRT2;
        data[idx + 2] = g2r * Math.sqrt(p2) / Math.SQRT2;
        data[idx + 3] = -(g2i * Math.sqrt(p2) / Math.SQRT2);
      }
    }

    const tex = new THREE.DataTexture(data, N, N, THREE.RGBAFormat, THREE.FloatType);
    tex.needsUpdate = true;
    tex.minFilter = THREE.NearestFilter;
    tex.magFilter = THREE.NearestFilter;
    return tex;
  }

  // ── Generate ω(k) dispersion on CPU ──

  private generateOmega(): THREE.DataTexture {
    const N = this.N;
    const L = this.patchSize;
    const data = new Float32Array(N * N * 4);

    for (let y = 0; y < N; y++) {
      for (let x = 0; x < N; x++) {
        const kx = (x - N / 2) * TAU / L;
        const kz = (y - N / 2) * TAU / L;
        const kMag = Math.sqrt(kx * kx + kz * kz);
        const idx = (y * N + x) * 4;
        data[idx + 0] = Math.sqrt(G * kMag); // ω
        data[idx + 1] = kx;
        data[idx + 2] = kz;
        data[idx + 3] = kMag;
      }
    }

    const tex = new THREE.DataTexture(data, N, N, THREE.RGBAFormat, THREE.FloatType);
    tex.needsUpdate = true;
    tex.minFilter = THREE.NearestFilter;
    tex.magFilter = THREE.NearestFilter;
    return tex;
  }

  // ── Spectrum evolution compute ──
  // KEY FIX: pass raw textures to textureLoad/textureStore, not pre-wrapped nodes

  private buildEvolve(): ComputeNode {
    const N = this.N;
    const h0 = this.h0Tex;
    const omega = this.omegaTex;
    const htH = this.htHeightTex;
    const htDx = this.htDxTex;
    const htDz = this.htDzTex;
    const uTime = this.uTime;

    return Fn(() => {
      const x = instanceIndex.mod(N);
      const y = instanceIndex.div(N);
      const coord = ivec2(int(x), int(y));

      // Read H0 and omega — pass RAW textures to textureLoad
      const h0Val = textureLoad(h0, coord);
      const omVal = textureLoad(omega, coord);
      const w = omVal.x;
      const kx = omVal.y;
      const kz = omVal.z;
      const kMag = omVal.w;

      const wt = w.mul(uTime);
      const cw = cos(wt);
      const sw = sin(wt);

      // H(k,t) = H0(k) * exp(iωt) + H0*(-k) * exp(-iωt)
      const hr = h0Val.x.mul(cw).sub(h0Val.y.mul(sw))
        .add(h0Val.z.mul(cw).add(h0Val.w.mul(sw)));
      const hi = h0Val.x.mul(sw).add(h0Val.y.mul(cw))
        .add(h0Val.z.mul(sw).negate().add(h0Val.w.mul(cw)));

      const uv = uvec2(x, y);

      // Write — pass RAW StorageTexture, chain .toWriteOnly()
      textureStore(htH, uv, vec4(hr, hi, 0, 0)).toWriteOnly();

      // Dx = -i * kx/|k| * H, Dz = -i * kz/|k| * H
      const safeK = max(kMag, float(0.0001));
      const knx = kx.div(safeK);
      const knz = kz.div(safeK);
      textureStore(htDx, uv, vec4(knx.mul(hi), knx.negate().mul(hr), 0, 0)).toWriteOnly();
      textureStore(htDz, uv, vec4(knz.mul(hi), knz.negate().mul(hr), 0, 0)).toWriteOnly();
    })().compute(N * N);
  }

  // ── IFFT butterfly (one stage, one direction) ──
  // KEY FIX: pass raw StorageTextures directly

  private buildButterfly(srcTex: StorageTexture, dstTex: StorageTexture, vertical: boolean): ComputeNode {
    const N = this.N;
    const uStage = this.uStage;

    return Fn(() => {
      const x = instanceIndex.mod(N);
      const y = instanceIndex.div(N);

      const j = vertical ? int(y) : int(x);

      const halfSize = shiftLeft(int(1), uStage);
      const fullSize = shiftLeft(int(1), int(uStage).add(1));
      const posInGroup = bitAnd(j, fullSize.sub(1));
      const isTop = posInGroup.lessThan(halfSize);

      const k = select(posInGroup.sub(halfSize), posInGroup, isTop);
      const partner = select(j.sub(halfSize), j.add(halfSize), isTop);

      // Read from source — raw StorageTexture
      const coordA = vertical ? ivec2(int(x), j) : ivec2(j, int(y));
      const coordB = vertical ? ivec2(int(x), partner) : ivec2(partner, int(y));
      const a = textureLoad(srcTex, coordA);
      const b = textureLoad(srcTex, coordB);

      // Twiddle factor (positive angle for IFFT)
      const angle = float(TAU).mul(float(k)).div(float(fullSize));
      const twR = cos(angle);
      const twI = sin(angle);

      // Complex multiply: b * twiddle
      const btR = b.x.mul(twR).sub(b.y.mul(twI));
      const btI = b.x.mul(twI).add(b.y.mul(twR));

      const outR = select(a.x.sub(btR), a.x.add(btR), isTop);
      const outI = select(a.y.sub(btI), a.y.add(btI), isTop);

      // Write to destination — raw StorageTexture
      textureStore(dstTex, uvec2(x, y), vec4(outR, outI, 0, 0)).toWriteOnly();
    })().compute(N * N);
  }

  // ── Assemble displacement from IFFT results ──

  private buildAssemble(): ComputeNode {
    const N = this.N;
    const chop = this.choppiness;
    const hTex = this.heightResultTex;
    const dxTex = this.dxResultTex;
    const dzTex = this.dzResultTex;
    const outTex = this.displacementTex;

    return Fn(() => {
      const x = instanceIndex.mod(N);
      const y = instanceIndex.div(N);
      const coord = ivec2(int(x), int(y));

      const h = textureLoad(hTex, coord);
      const dx = textureLoad(dxTex, coord);
      const dz = textureLoad(dzTex, coord);

      // Sign correction: (-1)^(x+y) for centered spectrum
      const parity = bitAnd(int(x).add(int(y)), int(1));
      const sign = select(float(-1.0), float(1.0), parity.equal(int(0)));

      const invN2 = float(1.0 / (N * N));
      const height = h.x.mul(sign).mul(invN2);
      const dispX = dx.x.mul(sign).mul(invN2).mul(chop);
      const dispZ = dz.x.mul(sign).mul(invN2).mul(chop);

      textureStore(outTex, uvec2(x, y), vec4(height, dispX, dispZ, 0)).toWriteOnly();
    })().compute(N * N);
  }

  // ── Normal + foam from displacement ──

  private buildNormals(): ComputeNode {
    const N = this.N;
    const texelSize = this.patchSize / N;
    const dispTex = this.displacementTex;
    const outTex = this.normalFoamTex;

    return Fn(() => {
      const x = instanceIndex.mod(N);
      const y = instanceIndex.div(N);

      const xp = int(x).add(1).mod(N);
      const xm = int(x).add(N - 1).mod(N);
      const yp = int(y).add(1).mod(N);
      const ym = int(y).add(N - 1).mod(N);

      const r = textureLoad(dispTex, ivec2(xp, int(y)));
      const l = textureLoad(dispTex, ivec2(xm, int(y)));
      const u = textureLoad(dispTex, ivec2(int(x), yp));
      const d = textureLoad(dispTex, ivec2(int(x), ym));

      const dhdx = r.x.sub(l.x).div(2.0 * texelSize);
      const dhdz = u.x.sub(d.x).div(2.0 * texelSize);
      const normal = normalize(vec4(dhdx.negate(), float(1.0), dhdz.negate(), 0).xyz);

      // Jacobian foam
      const dDxdx = r.y.sub(l.y).div(2.0 * texelSize);
      const dDzdz = u.z.sub(d.z).div(2.0 * texelSize);
      const jacobian = float(1.0).add(dDxdx).mul(float(1.0).add(dDzdz));
      const foam = clamp(float(1.0).sub(jacobian).mul(2.0), 0.0, 1.0);

      textureStore(outTex, uvec2(x, y), vec4(normal.x, normal.y, normal.z, foam)).toWriteOnly();
    })().compute(N * N);
  }

  // ── Copy StorageTexture → StorageTexture ──

  private copy(renderer: WebGPURenderer, src: StorageTexture, dst: StorageTexture): void {
    const key = `${src.id}_${dst.id}`;
    if (!this.copyNodes.has(key)) {
      const N = this.N;
      this.copyNodes.set(key, Fn(() => {
        const x = instanceIndex.mod(N);
        const y = instanceIndex.div(N);
        const v = textureLoad(src, ivec2(int(x), int(y)));
        textureStore(dst, uvec2(x, y), v).toWriteOnly();
      })().compute(N * N));
    }
    renderer.compute(this.copyNodes.get(key)!);
  }

  // ── Run 1D IFFT (horiz + vert) on a spectrum → result ──

  private runIFFT(renderer: WebGPURenderer, resultTex: StorageTexture): void {
    // Horizontal passes
    for (let s = 0; s < this.logN; s++) {
      this.uStage.value = s;
      renderer.compute(s % 2 === 0 ? this.ifftH_p2p : this.ifftH_p2pi);
    }

    // Vertical passes (continue ping-pong state from horizontal)
    for (let s = 0; s < this.logN; s++) {
      this.uStage.value = s;
      const total = this.logN + s;
      renderer.compute(total % 2 === 0 ? this.ifftV_p2p : this.ifftV_p2pi);
    }

    // Result in ping (total stages = 2*logN = even for N=256 → logN=8)
    this.copy(renderer, this.pingTex, resultTex);
  }

  // ── Per-frame update ──

  update(renderer: WebGPURenderer, time: number): void {
    this.uTime.value = time;

    // 1. Evolve spectrum
    renderer.compute(this.evolveNode);

    // 2. IFFT height
    this.copy(renderer, this.htHeightTex, this.pingTex);
    this.runIFFT(renderer, this.heightResultTex);

    // 3. IFFT Dx
    this.copy(renderer, this.htDxTex, this.pingTex);
    this.runIFFT(renderer, this.dxResultTex);

    // 4. IFFT Dz
    this.copy(renderer, this.htDzTex, this.pingTex);
    this.runIFFT(renderer, this.dzResultTex);

    // 5. Assemble displacement
    renderer.compute(this.assembleNode);

    // 6. Normals + foam
    renderer.compute(this.normalNode);
  }

  dispose(): void {
    this.h0Tex.dispose();
    this.omegaTex.dispose();
    [this.htHeightTex, this.htDxTex, this.htDzTex,
     this.pingTex, this.pongTex,
     this.heightResultTex, this.dxResultTex, this.dzResultTex,
     this.displacementTex, this.normalFoamTex].forEach(t => t.dispose());
  }
}
