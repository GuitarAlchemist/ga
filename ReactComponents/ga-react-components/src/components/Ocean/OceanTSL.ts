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
  max, clamp, reflect,
} from 'three/tsl';

// ── Precomputed wave parameters ──────────────────────────────────────────────

interface PrecomputedWave {
  dx: number; dz: number;
  amp: number; k: number; omega: number; Q: number;
  kAmp: number; QkAmp: number;
}

const G = 9.81;
const TAU = Math.PI * 2;

// Wave set tuned for realistic open-ocean look.
// Lower steepness on short waves to prevent circular blob artifacts.
const WAVE_DEFS = [
  // Long swells (dominant — set the overall shape)
  { dir: [1.0, 0.15],  amp: 2.0,  len: 120, steep: 0.28 },
  { dir: [0.8, 0.55],  amp: 1.4,  len: 75,  steep: 0.28 },
  // Medium cross-sea
  { dir: [0.15, 1.0],  amp: 0.8,  len: 40,  steep: 0.32 },
  { dir: [-0.6, 0.8],  amp: 0.5,  len: 25,  steep: 0.32 },
  // Short chop (wind-driven — moderate steepness)
  { dir: [-0.85, 0.15], amp: 0.25, len: 13, steep: 0.35 },
  { dir: [0.3, -0.95],  amp: 0.16, len: 8,  steep: 0.35 },
  // Capillary detail (LOW steepness to avoid blobs)
  { dir: [0.7, 0.7],    amp: 0.08, len: 4.5, steep: 0.25 },
  { dir: [-0.5, -0.5],  amp: 0.05, len: 3.0, steep: 0.22 },
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

export function createOceanTSLMaterial(): OceanTSLResult {
  const material = new MeshBasicNodeMaterial();
  material.side = THREE.FrontSide;

  const uTime = uniform(0.0);
  const uSunDir = uniform(new THREE.Vector3(0.5, 0.85, 0.3).normalize());

  // ── Vertex: 8-wave Gerstner with choppy horizontal displacement ────────

  material.positionNode = Fn(() => {
    const pos = positionLocal;
    const xz = vec2(pos.x, pos.z);

    const dx = float(0.0).toVar();
    const dy = float(0.0).toVar();
    const dz = float(0.0).toVar();

    for (const w of WAVES) {
      const phase = dot(vec2(w.dx, w.dz), xz).mul(w.k).sub(uTime.mul(w.omega));
      const c = cos(phase);
      const s = sin(phase);

      dx.addAssign(float(w.Q * w.amp * w.dx).mul(c));
      dz.addAssign(float(w.Q * w.amp * w.dz).mul(c));
      dy.addAssign(float(w.amp).mul(s));
    }

    return vec3(pos.x.add(dx), pos.y.add(dy), pos.z.add(dz));
  })();

  // ── Fragment: full custom lighting ─────────────────────────────────────

  material.colorNode = Fn(() => {
    const origXZ = vec2(positionLocal.x, positionLocal.z);
    const V = normalize(cameraPosition.sub(positionWorld));

    // ── Gerstner Normal (per-fragment) ──
    const nx = float(0.0).toVar();
    const ny = float(1.0).toVar();
    const nz = float(0.0).toVar();

    for (const w of WAVES) {
      const phase = dot(vec2(w.dx, w.dz), origXZ).mul(w.k).sub(uTime.mul(w.omega));
      const c = cos(phase);
      const s = sin(phase);

      nx.subAssign(float(w.dx * w.kAmp).mul(c));
      nz.subAssign(float(w.dz * w.kAmp).mul(c));
      ny.subAssign(float(w.QkAmp).mul(s));
    }

    const N = normalize(vec3(nx, ny, nz));

    // ── Fresnel (Schlick, F0 = 0.02) ──
    const NoV = clamp(dot(N, V), 0.0, 1.0);
    const fresnel = float(0.02).add(float(0.98).mul(pow(float(1.0).sub(NoV), 5.0)));

    // ── Reflected sky color ──
    // Overcast grey sky matching WebTide's muted atmosphere
    const R = reflect(V.negate(), N);
    const skyT = clamp(R.y, 0.0, 1.0);

    const skyHorizon = vec3(0.62, 0.65, 0.70);     // bright overcast horizon
    const skyZenith  = vec3(0.35, 0.42, 0.55);      // muted blue-grey
    const skyBelow   = vec3(0.015, 0.025, 0.04);    // dark water self-reflection
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
    const sunSpec = pow(NdH, float(1024.0)).mul(60.0);
    const specContrib = sunCol.mul(sunSpec);

    // ── Water body color (very dark, near-black) ──
    const baseWater = vec3(0.003, 0.014, 0.025);

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
    const fogStrength = float(0.00022).add(float(0.00010).mul(viewHoriz));
    const fogFactor = clamp(exp(fogStrength.negate().mul(fogStrength).mul(distSq)), 0.0, 1.0);

    // Fog color matches sky horizon for seamless blend
    const fogCol = vec3(0.60, 0.64, 0.70);

    return mix(fogCol, litColor, fogFactor);
  })();

  return {
    material,
    uniforms: { time: uTime, sunDirection: uSunDir },
  };
}
