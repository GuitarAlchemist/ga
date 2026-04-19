// src/components/Ocean/OceanTSL.ts
// Advanced ocean material using Three.js Shading Language (TSL).
//
// Visual target: WebTide (barthpaleologue.github.io/WebTide)
// - Very dark water body when looking down
// - Smooth Fresnel gradient: dark foreground → bright reflective horizon
// - Small, scattered specular highlights on wave facets (not chrome blobs)
// - Heavy atmospheric fog blending ocean seamlessly into sky
// - Choppy horizontal displacement from 8 Gerstner waves

import * as THREE from 'three';
import { MeshBasicNodeMaterial } from 'three/webgpu';
import {
  Fn, float, vec2, vec3,
  uniform,
  positionLocal, positionWorld, cameraPosition,
  sin, cos, dot, normalize, mix, smoothstep, pow, exp,
  max, clamp, reflect, fract,
} from 'three/tsl';

// ── Procedural hash for micro-normal perturbation ────────────────────────────
// Breaks up the regular grid pattern visible at grazing angles.
// Fast 2D→2D hash: no textures, no noise LUTs, purely arithmetic.

const hash22 = Fn(([p_immutable]: [ReturnType<typeof vec2>]) => {
  const p = vec2(p_immutable);
  const q = vec2(
    dot(p, vec2(127.1, 311.7)),
    dot(p, vec2(269.5, 183.3)),
  );
  return fract(sin(q).mul(43758.5453)).mul(2.0).sub(1.0); // range [-1, 1]
});

// ── Precomputed wave parameters ──────────────────────────────────────────────

interface PrecomputedWave {
  dx: number; dz: number;
  amp: number; k: number; omega: number; Q: number;
  kAmp: number; QkAmp: number;
}

const G = 9.81;
const TAU = Math.PI * 2;

// Wave set tuned for realistic open-ocean look.
// CRITICAL: sum of per-wave steepness must stay below 1.0 to avoid Gerstner
// fold-back — when Σ(Q·k·A) > 1 at a point, the parameterization self-
// intersects and produces the characteristic flat-top plateaus with pinched
// ring rims. 8 waves × 0.12 avg ≈ 0.96 — just under the limit.
const WAVE_DEFS = [
  // Long swells (dominant — set the overall shape)
  { dir: [1.0, 0.15],  amp: 2.0,  len: 120, steep: 0.14 },
  { dir: [0.8, 0.55],  amp: 1.4,  len: 75,  steep: 0.14 },
  // Medium cross-sea
  { dir: [0.15, 1.0],  amp: 0.8,  len: 40,  steep: 0.15 },
  { dir: [-0.6, 0.8],  amp: 0.5,  len: 25,  steep: 0.15 },
  // Short chop (wind-driven)
  { dir: [-0.85, 0.15], amp: 0.25, len: 13, steep: 0.14 },
  { dir: [0.3, -0.95],  amp: 0.16, len: 8,  steep: 0.12 },
  // Capillary detail
  { dir: [0.7, 0.7],    amp: 0.08, len: 4.5, steep: 0.10 },
  { dir: [-0.5, -0.5],  amp: 0.05, len: 3.0, steep: 0.09 },
];

const WAVES: PrecomputedWave[] = WAVE_DEFS.map(w => {
  const mag = Math.sqrt(w.dir[0] ** 2 + w.dir[1] ** 2);
  const dx = w.dir[0] / mag;
  const dz = w.dir[1] / mag;
  const k = TAU / w.len;
  const omega = Math.sqrt(G * k);
  const Q = w.steep / (k * w.amp);
  return { dx, dz, amp: w.amp, k, omega, Q, kAmp: k * w.amp, QkAmp: Q * k * w.amp };
});

// ── Public interface ─────────────────────────────────────────────────────────

export interface OceanTSLUniforms {
  time: { value: number };
  sunDirection: { value: THREE.Vector3 };
}

export interface OceanTSLResult {
  material: MeshBasicNodeMaterial;
  uniforms: OceanTSLUniforms;
}

export interface OceanTSLOptions {
  waveCount?: 4 | 6 | 8;
  fogDensity?: number;
  sunSpecExponent?: number;
  sunSpecMultiplier?: number;
}

export function createOceanTSLMaterial(options: OceanTSLOptions = {}): OceanTSLResult {
  const {
    waveCount = 8,
    fogDensity = 0.00022,
    sunSpecExponent = 1024,
    sunSpecMultiplier = 60,
  } = options;
  const activeWaves = WAVES.slice(0, waveCount);
  const material = new MeshBasicNodeMaterial();
  material.side = THREE.FrontSide;

  const uTime = uniform(0.0);
  const uSunDir = uniform(new THREE.Vector3(0.5, 0.85, 0.3).normalize());

  // Earth-curvature drop: subtract d²/(2R) per vertex, where d is horizontal
  // distance from the camera. R smaller than Earth gives visible curvature on
  // a 6km-mesh demo (real Earth would barely curve over that range). Default
  // ≈2000 km — gives a clear horizon dip at ~3-5 km without absurdity.
  const uPlanetRadius = uniform(2_000_000);

  // ── Vertex: 8-wave Gerstner + earth-curvature drop ─────────────────────

  material.positionNode = Fn(() => {
    const pos = positionLocal;
    const xz = vec2(pos.x, pos.z);

    const dx = float(0.0).toVar();
    const dy = float(0.0).toVar();
    const dz = float(0.0).toVar();

    for (const w of activeWaves) {
      const phase = dot(vec2(w.dx, w.dz), xz).mul(w.k).sub(uTime.mul(w.omega));
      const c = cos(phase);
      const s = sin(phase);

      dx.addAssign(float(w.Q * w.amp * w.dx).mul(c));
      dz.addAssign(float(w.Q * w.amp * w.dz).mul(c));
      dy.addAssign(float(w.amp).mul(s));
    }

    // Earth-curvature drop relative to camera ground position.
    // Horizontal distance² from camera in world XZ plane; subtract d²/(2R)
    // from Y so distant water visibly curves below the visible horizon.
    const wx = pos.x.add(dx);
    const wz = pos.z.add(dz);
    const camDx = wx.sub(cameraPosition.x);
    const camDz = wz.sub(cameraPosition.z);
    const distSq = camDx.mul(camDx).add(camDz.mul(camDz));
    const drop = distSq.mul(float(0.5).div(uPlanetRadius));

    return vec3(wx, pos.y.add(dy).sub(drop), wz);
  })();

  // ── Fragment: full custom lighting ─────────────────────────────────────

  material.colorNode = Fn(() => {
    const origXZ = vec2(positionLocal.x, positionLocal.z);
    const V = normalize(cameraPosition.sub(positionWorld));

    // ── Gerstner Normal (per-fragment) ──
    const nx = float(0.0).toVar();
    const ny = float(1.0).toVar();
    const nz = float(0.0).toVar();

    for (const w of activeWaves) {
      const phase = dot(vec2(w.dx, w.dz), origXZ).mul(w.k).sub(uTime.mul(w.omega));
      const c = cos(phase);
      const s = sin(phase);

      nx.subAssign(float(w.dx * w.kAmp).mul(c));
      nz.subAssign(float(w.dz * w.kAmp).mul(c));
      ny.subAssign(float(w.QkAmp).mul(s));
    }

    // ── Micro-normal perturbation (breaks grid regularity) ──
    // Two octaves of hash noise at different scales, animated with time.
    // Small amplitude (0.04-0.06) — just enough to scatter specular catches
    // without visibly changing the wave shape.
    const microUV1 = origXZ.mul(0.8).add(uTime.mul(vec2(0.3, 0.2)));
    const microUV2 = origXZ.mul(2.5).add(uTime.mul(vec2(-0.5, 0.4)));
    const noise1 = hash22(microUV1);
    const noise2 = hash22(microUV2);
    const microPerturb = noise1.mul(0.04).add(noise2.mul(0.025));
    nx.addAssign(microPerturb.x);
    nz.addAssign(microPerturb.y);

    const N = normalize(vec3(nx, ny, nz));

    // ── Fresnel (Schlick, F0 = 0.02) ──
    const NoV = clamp(dot(N, V), 0.0, 1.0);
    const fresnel = float(0.02).add(float(0.98).mul(pow(float(1.0).sub(NoV), 5.0)));

    // ── Reflected sky color ──
    // Overcast grey sky matching WebTide's muted atmosphere
    const R = reflect(V.negate(), N);
    const skyT = clamp(R.y, 0.0, 1.0);

    const skyHorizon = vec3(0.62, 0.72, 0.86);     // bright blue-white horizon
    const skyZenith  = vec3(0.24, 0.48, 0.78);      // saturated blue zenith
    const skyBelow   = vec3(0.04, 0.08, 0.14);      // deep-water self-reflection
    const skyAbove = mix(skyHorizon, skyZenith, smoothstep(0.0, 0.4, skyT));
    const belowFactor = smoothstep(float(0.02), float(-0.12), R.y);
    const skyBase = mix(skyAbove, skyBelow, belowFactor);

    // Sun: soft broad glow only (the specular handles the tight highlight)
    const sunDot = max(dot(R, uSunDir), 0.0);
    const sunGlow = pow(sunDot, float(48.0)).mul(0.25);
    const sunCol = vec3(1.0, 0.96, 0.90);
    const skyColor = skyBase.add(sunCol.mul(sunGlow));

    // ── Sun Specular (very tight, moderate intensity) ──
    // WebTide: pow(720) * 210. But with 8 Gerstner waves (smoother normals
    // than FFT), we need LOWER intensity to avoid chrome blobs.
    // Tight exponent + modest multiplier = small scattered glints.
    const H = normalize(V.add(uSunDir));
    const NdH = max(dot(N, H), 0.0);
    const sunSpec = pow(NdH, float(sunSpecExponent)).mul(sunSpecMultiplier);
    const specContrib = sunCol.mul(sunSpec);

    // ── Water body color (deep ocean blue, not pure black) ──
    // Was (0.003, 0.014, 0.025) — too black for sunlit presets (calm/sunset).
    // Lifting to a deep-teal base keeps foreground water visibly water-coloured
    // even at steep view angles where Fresnel gives little sky reflection.
    const baseWater = vec3(0.02, 0.07, 0.12);

    // Subtle subsurface scattering on sun-facing thin crests
    const NdL = max(dot(N, uSunDir), 0.0);
    const waveHeight = clamp(positionWorld.y.mul(0.2), 0.0, 1.0);
    const sss = NdL.mul(waveHeight).mul(0.06);
    const sssCol = vec3(0.0, 0.04, 0.035);
    const waterColor = baseWater.add(sssCol.mul(sss));

    // ── Foam (only at very steep crests, subtle) ──
    const upDot = clamp(dot(N, vec3(0.0, 1.0, 0.0)), 0.0, 1.0);
    const slope = float(1.0).sub(upDot);
    const foam = pow(smoothstep(0.35, 0.65, slope), float(2.5)).mul(0.15);
    const foamCol = vec3(0.45, 0.48, 0.52);

    // ── Compose ──
    const reflected = skyColor.add(specContrib);
    const refracted = mix(waterColor, foamCol, foam);
    const litColor = mix(refracted, reflected, fresnel);

    // ── Distance fog (heavy — seamless ocean-to-sky blend) ──
    const toCamera = positionWorld.sub(cameraPosition);
    const distSq = dot(toCamera, toCamera);
    // Directional fog: stronger looking toward horizon, less looking down
    const viewHoriz = float(1.0).sub(clamp(V.y.mul(2.0), 0.0, 1.0));
    const fogStrength = float(fogDensity).add(float(fogDensity * 0.45).mul(viewHoriz));
    const fogFactor = clamp(exp(fogStrength.negate().mul(fogStrength).mul(distSq)), 0.0, 1.0);

    // Fog colour: match the (new) bluer horizon so the seamless horizon
    // blend stays seamless across calm/stormy/sunset.
    const fogCol = vec3(0.62, 0.72, 0.84);

    return mix(fogCol, litColor, fogFactor);
  })();

  return {
    material,
    uniforms: { time: uTime, sunDirection: uSunDir },
  };
}
