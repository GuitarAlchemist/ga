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
// These textures tile seamlessly at patchSize intervals.

import * as THREE from 'three';
import { StorageTexture, WebGPURenderer } from 'three/webgpu';
import {
  Fn, float, int, vec4,
  ivec2, uvec2,
  uniform, instanceIndex,
  storageTexture, textureStore, textureLoad,
  texture,
  sin, cos, select, shiftLeft, bitAnd,
  max, clamp, normalize,
} from 'three/tsl';

const G = 9.81;
const TAU = Math.PI * 2;

// ── Gaussian random pair (Box-Muller) for spectrum init ──────────────────────

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
  const phillips = amplitude * Math.exp(-1 / (k2 * L * L)) / (k2 * k2) * (kDotW * kDotW);
  // Suppress very small waves (prevents divergence)
  const smallWave = 0.001 * L;
  return phillips * Math.exp(-k2 * smallWave * smallWave);
}

// ── Main class ───────────────────────────────────────────────────────────────

export interface OceanFFTOptions {
  N?: number;            // FFT resolution (power of 2)
  patchSize?: number;    // World-space tile size (meters)
  windSpeed?: number;    // m/s
  windDirection?: [number, number];
  amplitude?: number;    // Phillips spectrum amplitude constant
  choppiness?: number;   // Horizontal displacement scale
}

export class OceanFFTCompute {
  readonly N: number;
  private readonly logN: number;
  private readonly patchSize: number;
  private readonly choppiness: number;

  // Output textures (bind to material)
  readonly displacementTex: StorageTexture;
  readonly normalFoamTex: StorageTexture;

  // Internal textures
  private h0Tex: THREE.DataTexture;            // Initial spectrum (CPU, read-only)
  private omegaTex: THREE.DataTexture;          // Dispersion ω(k) (CPU, read-only)
  private htHeightTex: StorageTexture;          // Evolved height spectrum H(k,t)
  private htDxTex: StorageTexture;              // Evolved Dx spectrum
  private htDzTex: StorageTexture;              // Evolved Dz spectrum
  private pingTex: StorageTexture;              // IFFT ping
  private pongTex: StorageTexture;              // IFFT pong
  private heightSpatialTex: StorageTexture;     // IFFT result: height
  private dxSpatialTex: StorageTexture;         // IFFT result: dx
  private dzSpatialTex: StorageTexture;         // IFFT result: dz

  // Compute nodes
  private evolveCompute: ReturnType<ReturnType<typeof Fn>>;
  private ifftPingToPongH: ReturnType<ReturnType<typeof Fn>>;
  private ifftPongToPingH: ReturnType<ReturnType<typeof Fn>>;
  private ifftPingToPongV: ReturnType<ReturnType<typeof Fn>>;
  private ifftPongToPingV: ReturnType<ReturnType<typeof Fn>>;
  private assembleCompute: ReturnType<ReturnType<typeof Fn>>;
  private normalCompute: ReturnType<ReturnType<typeof Fn>>;

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

    // Normalize wind direction
    const wLen = Math.sqrt(windDirection[0] ** 2 + windDirection[1] ** 2);
    const wdx = windDirection[0] / wLen;
    const wdz = windDirection[1] / wLen;

    // ── CPU-side spectrum generation ──
    this.h0Tex = this.generateH0(wdx, wdz, windSpeed, amplitude);
    this.omegaTex = this.generateOmega();

    // ── StorageTextures ──
    const mkTex = () => {
      const t = new StorageTexture(N, N);
      t.type = THREE.HalfFloatType;
      return t;
    };

    this.htHeightTex = mkTex();
    this.htDxTex = mkTex();
    this.htDzTex = mkTex();
    this.pingTex = mkTex();
    this.pongTex = mkTex();
    this.heightSpatialTex = mkTex();
    this.dxSpatialTex = mkTex();
    this.dzSpatialTex = mkTex();
    this.displacementTex = mkTex();
    this.normalFoamTex = mkTex();

    // ── Build compute nodes ──
    this.evolveCompute = this.buildEvolve();
    const ifft = this.buildIFFT();
    this.ifftPingToPongH = ifft.p2pH;
    this.ifftPongToPingH = ifft.p2piH;
    this.ifftPingToPongV = ifft.p2pV;
    this.ifftPongToPingV = ifft.p2piV;
    this.assembleCompute = this.buildAssemble();
    this.normalCompute = this.buildNormals();
  }

  // ── Generate initial spectrum H0(k) on CPU ──

  private generateH0(
    wdx: number, wdz: number, windSpeed: number, amplitude: number,
  ): THREE.DataTexture {
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
        data[idx + 0] = g1r * Math.sqrt(p1) / Math.SQRT2;  // Re(H0(k))
        data[idx + 1] = g1i * Math.sqrt(p1) / Math.SQRT2;  // Im(H0(k))
        data[idx + 2] = g2r * Math.sqrt(p2) / Math.SQRT2;  // Re(H0*(-k))
        data[idx + 3] = -(g2i * Math.sqrt(p2) / Math.SQRT2); // Im(H0*(-k)) conjugate
      }
    }

    const tex = new THREE.DataTexture(data, N, N, THREE.RGBAFormat, THREE.FloatType);
    tex.needsUpdate = true;
    tex.minFilter = THREE.NearestFilter;
    tex.magFilter = THREE.NearestFilter;
    return tex;
  }

  // ── Generate dispersion relation ω(k) = sqrt(g|k|) on CPU ──

  private generateOmega(): THREE.DataTexture {
    const N = this.N;
    const L = this.patchSize;
    const data = new Float32Array(N * N * 4);

    for (let y = 0; y < N; y++) {
      for (let x = 0; x < N; x++) {
        const kx = (x - N / 2) * TAU / L;
        const kz = (y - N / 2) * TAU / L;
        const kMag = Math.sqrt(kx * kx + kz * kz);
        const omega = Math.sqrt(G * kMag);

        const idx = (y * N + x) * 4;
        data[idx + 0] = omega;   // ω
        data[idx + 1] = kx;      // kx
        data[idx + 2] = kz;      // kz
        data[idx + 3] = kMag;    // |k|
      }
    }

    const tex = new THREE.DataTexture(data, N, N, THREE.RGBAFormat, THREE.FloatType);
    tex.needsUpdate = true;
    tex.minFilter = THREE.NearestFilter;
    tex.magFilter = THREE.NearestFilter;
    return tex;
  }

  // ── Spectrum evolution compute ──
  // H(k,t) = H0(k)*exp(iωt) + H0*(-k)*exp(-iωt)
  // Dx(k,t) = -i * kx/|k| * H(k,t)
  // Dz(k,t) = -i * kz/|k| * H(k,t)

  private buildEvolve() {
    const N = this.N;
    const h0TexNode = texture(this.h0Tex);
    const omegaTexNode = texture(this.omegaTex);
    const htH = storageTexture(this.htHeightTex).toWriteOnly();
    const htDx = storageTexture(this.htDxTex).toWriteOnly();
    const htDz = storageTexture(this.htDzTex).toWriteOnly();
    const uTime = this.uTime;

    const kernel = Fn(() => {
      const x = instanceIndex.remainder(N);
      const y = instanceIndex.div(N);
      const coord = ivec2(int(x), int(y));

      // Read H0 and omega
      const h0 = textureLoad(h0TexNode, coord);
      const omegaData = textureLoad(omegaTexNode, coord);
      const omega = omegaData.x;
      const kx = omegaData.y;
      const kz = omegaData.z;
      const kMag = omegaData.w;

      // exp(iωt) = (cos(ωt), sin(ωt))
      const wt = omega.mul(uTime);
      const cosWt = cos(wt);
      const sinWt = sin(wt);

      // H0(k) = (h0.r, h0.g), H0*(-k) = (h0.b, h0.a)
      // H(k,t) = H0 * exp(iωt) + H0*(-k) * exp(-iωt)
      // Complex mul: (a+bi)(c+di) = (ac-bd) + (ad+bc)i
      const hr = h0.x.mul(cosWt).sub(h0.y.mul(sinWt))
        .add(h0.z.mul(cosWt).add(h0.w.mul(sinWt)));
      const hi = h0.x.mul(sinWt).add(h0.y.mul(cosWt))
        .add(h0.z.mul(sinWt).negate().add(h0.w.mul(cosWt)));

      // Store height spectrum (complex: rg)
      textureStore(htH, uvec2(x, y), vec4(hr, hi, 0, 0));

      // Dx(k,t) = -i * kx/|k| * H(k,t) = (kx/|k| * hi, -kx/|k| * hr)
      // Dz(k,t) = -i * kz/|k| * H(k,t) = (kz/|k| * hi, -kz/|k| * hr)
      const safeKMag = max(kMag, float(0.0001));
      const kxNorm = kx.div(safeKMag);
      const kzNorm = kz.div(safeKMag);

      textureStore(htDx, uvec2(x, y), vec4(kxNorm.mul(hi), kxNorm.negate().mul(hr), 0, 0));
      textureStore(htDz, uvec2(x, y), vec4(kzNorm.mul(hi), kzNorm.negate().mul(hr), 0, 0));
    })().compute(N * N);

    return kernel;
  }

  // ── IFFT butterfly compute ──
  // Cooley-Tukey radix-2 DIT butterfly, one stage at a time.
  // Positive twiddle angles for inverse FFT. Final 1/N scaling in assemble.

  private buildIFFT() {
    const N = this.N;
    const uStage = this.uStage;

    const pingRead = storageTexture(this.pingTex).toReadOnly();
    const pongRead = storageTexture(this.pongTex).toReadOnly();
    const pingWrite = storageTexture(this.pingTex).toWriteOnly();
    const pongWrite = storageTexture(this.pongTex).toWriteOnly();

    // Horizontal butterfly: FFT along x-axis, y stays fixed
    const makeHorizKernel = (readNode: ReturnType<typeof storageTexture>, writeNode: ReturnType<typeof storageTexture>) =>
      Fn(() => {
        const x = instanceIndex.remainder(N);
        const y = instanceIndex.div(N);

        const halfSize = shiftLeft(int(1), uStage);
        const fullSize = shiftLeft(int(1), int(uStage).add(1));
        const posInGroup = bitAnd(int(x), fullSize.sub(1));
        const isTop = posInGroup.lessThan(halfSize);

        const k = select(posInGroup.sub(halfSize), posInGroup, isTop);
        const partner = select(int(x).sub(halfSize), int(x).add(halfSize), isTop);

        // Read this element and partner
        const a = textureLoad(readNode, ivec2(int(x), int(y)));
        const b = textureLoad(readNode, ivec2(partner, int(y)));

        // Twiddle factor: exp(+2πi * k / fullSize) for IFFT
        const angle = float(TAU).mul(float(k)).div(float(fullSize));
        const twR = cos(angle);
        const twI = sin(angle);

        // Complex multiply: b * twiddle
        const btR = b.x.mul(twR).sub(b.y.mul(twI));
        const btI = b.x.mul(twI).add(b.y.mul(twR));

        // Butterfly: top = a + bt, bottom = a - bt
        const outR = select(a.x.sub(btR), a.x.add(btR), isTop);
        const outI = select(a.y.sub(btI), a.y.add(btI), isTop);

        textureStore(writeNode, uvec2(x, y), vec4(outR, outI, 0, 0));
      })().compute(N * N);

    // Vertical butterfly: FFT along y-axis, x stays fixed
    const makeVertKernel = (readNode: ReturnType<typeof storageTexture>, writeNode: ReturnType<typeof storageTexture>) =>
      Fn(() => {
        const x = instanceIndex.remainder(N);
        const y = instanceIndex.div(N);

        const halfSize = shiftLeft(int(1), uStage);
        const fullSize = shiftLeft(int(1), int(uStage).add(1));
        const posInGroup = bitAnd(int(y), fullSize.sub(1));
        const isTop = posInGroup.lessThan(halfSize);

        const k = select(posInGroup.sub(halfSize), posInGroup, isTop);
        const partner = select(int(y).sub(halfSize), int(y).add(halfSize), isTop);

        const a = textureLoad(readNode, ivec2(int(x), int(y)));
        const b = textureLoad(readNode, ivec2(int(x), partner));

        const angle = float(TAU).mul(float(k)).div(float(fullSize));
        const twR = cos(angle);
        const twI = sin(angle);

        const btR = b.x.mul(twR).sub(b.y.mul(twI));
        const btI = b.x.mul(twI).add(b.y.mul(twR));

        const outR = select(a.x.sub(btR), a.x.add(btR), isTop);
        const outI = select(a.y.sub(btI), a.y.add(btI), isTop);

        textureStore(writeNode, uvec2(x, y), vec4(outR, outI, 0, 0));
      })().compute(N * N);

    return {
      p2pH: makeHorizKernel(pingRead, pongWrite),
      p2piH: makeHorizKernel(pongRead, pingWrite),
      p2pV: makeVertKernel(pingRead, pongWrite),
      p2piV: makeVertKernel(pongRead, pingWrite),
    };
  }

  // ── Assemble displacement ──
  // Reads IFFT spatial results, applies sign correction ((-1)^(x+y) for
  // centered spectrum), 1/N² normalization, and packs into displacement texture.

  private buildAssemble() {
    const N = this.N;
    const chop = this.choppiness;

    const heightRead = storageTexture(this.heightSpatialTex).toReadOnly();
    const dxRead = storageTexture(this.dxSpatialTex).toReadOnly();
    const dzRead = storageTexture(this.dzSpatialTex).toReadOnly();
    const dispWrite = storageTexture(this.displacementTex).toWriteOnly();

    return Fn(() => {
      const x = instanceIndex.remainder(N);
      const y = instanceIndex.div(N);
      const coord = ivec2(int(x), int(y));

      const h = textureLoad(heightRead, coord);
      const dx = textureLoad(dxRead, coord);
      const dz = textureLoad(dzRead, coord);

      // Sign correction: (-1)^(x+y) for frequency-centered spectrum
      const sign = select(float(-1.0), float(1.0), bitAnd(int(x).add(int(y)), int(1)).equal(int(0)));

      // 1/N² normalization for 2D IFFT
      const invN2 = float(1.0 / (N * N));

      const height = h.x.mul(sign).mul(invN2);
      const dispX = dx.x.mul(sign).mul(invN2).mul(chop);
      const dispZ = dz.x.mul(sign).mul(invN2).mul(chop);

      textureStore(dispWrite, uvec2(x, y), vec4(height, dispX, dispZ, 0));
    })().compute(N * N);
  }

  // ── Normal + foam computation ──
  // Finite-difference normals from displacement texture.
  // Jacobian determinant approximation for foam mask.

  private buildNormals() {
    const N = this.N;
    const texelSize = this.patchSize / N;

    const dispRead = storageTexture(this.displacementTex).toReadOnly();
    const normalWrite = storageTexture(this.normalFoamTex).toWriteOnly();

    return Fn(() => {
      const x = instanceIndex.remainder(N);
      const y = instanceIndex.div(N);

      // Wrap-around neighbors for seamless tiling
      const xp = int(x).add(1).remainder(N);
      const xm = int(x).add(N - 1).remainder(N);
      const yp = int(y).add(1).remainder(N);
      const ym = int(y).add(N - 1).remainder(N);

      const _c = textureLoad(dispRead, ivec2(int(x), int(y)));
      const r  = textureLoad(dispRead, ivec2(xp, int(y)));
      const l  = textureLoad(dispRead, ivec2(xm, int(y)));
      const u  = textureLoad(dispRead, ivec2(int(x), yp));
      const d  = textureLoad(dispRead, ivec2(int(x), ym));

      // Central differences for height gradient
      const dhdx = r.x.sub(l.x).div(2.0 * texelSize);
      const dhdz = u.x.sub(d.x).div(2.0 * texelSize);

      // Normal from height gradient
      const normal = normalize(vec4(dhdx.negate(), float(1.0), dhdz.negate(), 0).xyz);

      // Jacobian determinant for foam (approximation from displacement divergence)
      // J = (1 + dDx/dx) * (1 + dDz/dz) - (dDx/dz) * (dDz/dx)
      const dDxdx = r.y.sub(l.y).div(2.0 * texelSize);
      const dDzdz = u.z.sub(d.z).div(2.0 * texelSize);
      const jacobian = float(1.0).add(dDxdx).mul(float(1.0).add(dDzdz));
      // Foam where Jacobian < 0 (wave folding)
      const foam = clamp(float(1.0).sub(jacobian).mul(2.0), 0.0, 1.0);

      textureStore(normalWrite, uvec2(x, y), vec4(normal.x, normal.y, normal.z, foam));
    })().compute(N * N);
  }

  // ── Run one IFFT on a spectrum texture → spatial result texture ──

  private runIFFT(
    renderer: WebGPURenderer,
    spectrumTex: StorageTexture,
    resultTex: StorageTexture,
  ): void {
    // Copy spectrum into ping texture first
    // We'll use a simple copy by running a trivial compute
    // Actually, we need to copy spectrumTex → pingTex before starting IFFT.
    // For simplicity, we'll set up the evolve to write directly to pingTex
    // for the first spectrum, or we do a manual copy here.
    //
    // For now, we assume pingTex already contains the spectrum data.

    // Horizontal passes
    for (let s = 0; s < this.logN; s++) {
      this.uStage.value = s;
      if (s % 2 === 0) {
        renderer.compute(this.ifftPingToPongH);
      } else {
        renderer.compute(this.ifftPongToPingH);
      }
    }

    // After horizontal: result is in ping (if logN even) or pong (if logN odd)
    // Vertical passes continue from where horizontal left off
    for (let s = 0; s < this.logN; s++) {
      this.uStage.value = s;
      const totalStages = this.logN + s;
      if (totalStages % 2 === 0) {
        renderer.compute(this.ifftPingToPongV);
      } else {
        renderer.compute(this.ifftPongToPingV);
      }
    }

    // Result is in ping or pong depending on total stages parity
    // We need to copy it to resultTex.
    // Total stages = 2 * logN. If logN=8, total=16 (even) → result in ping.
    // For simplicity, we'll always copy from ping.
    // TODO: handle odd logN if needed (logN=8 for N=256 → even, OK)
    this.copyTexture(renderer, this.pingTex, resultTex);
  }

  // ── Copy one StorageTexture to another ──

  private copyComputes = new Map<string, ReturnType<ReturnType<typeof Fn>>>();

  private copyTexture(
    renderer: WebGPURenderer,
    src: StorageTexture,
    dst: StorageTexture,
  ): void {
    const key = `${src.id}_${dst.id}`;
    if (!this.copyComputes.has(key)) {
      const srcRead = storageTexture(src).toReadOnly();
      const dstWrite = storageTexture(dst).toWriteOnly();
      const node = Fn(() => {
        const x = instanceIndex.remainder(this.N);
        const y = instanceIndex.div(this.N);
        const v = textureLoad(srcRead, ivec2(int(x), int(y)));
        textureStore(dstWrite, uvec2(x, y), v);
      })().compute(this.N * this.N);
      this.copyComputes.set(key, node);
    }
    renderer.compute(this.copyComputes.get(key)!);
  }

  // ── Per-frame update ──

  update(renderer: WebGPURenderer, time: number): void {
    this.uTime.value = time;

    // 1. Evolve spectrum: H0 → H(t), Dx(t), Dz(t)
    renderer.compute(this.evolveCompute);

    // 2. IFFT height spectrum
    this.copyTexture(renderer, this.htHeightTex, this.pingTex);
    this.runIFFT(renderer, this.htHeightTex, this.heightSpatialTex);

    // 3. IFFT Dx spectrum
    this.copyTexture(renderer, this.htDxTex, this.pingTex);
    this.runIFFT(renderer, this.htDxTex, this.dxSpatialTex);

    // 4. IFFT Dz spectrum
    this.copyTexture(renderer, this.htDzTex, this.pingTex);
    this.runIFFT(renderer, this.htDzTex, this.dzSpatialTex);

    // 5. Assemble displacement
    renderer.compute(this.assembleCompute);

    // 6. Compute normals + foam
    renderer.compute(this.normalCompute);
  }

  // ── Cleanup ──

  dispose(): void {
    this.h0Tex.dispose();
    this.omegaTex.dispose();
    this.htHeightTex.dispose();
    this.htDxTex.dispose();
    this.htDzTex.dispose();
    this.pingTex.dispose();
    this.pongTex.dispose();
    this.heightSpatialTex.dispose();
    this.dxSpatialTex.dispose();
    this.dzSpatialTex.dispose();
    this.displacementTex.dispose();
    this.normalFoamTex.dispose();
  }
}
