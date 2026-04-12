// src/components/Ocean/OceanTSLFFT.ts
// Ocean material for FFT displacement + normal textures.
//
// Tuned for Tessendorf FFT normals (more varied than Gerstner → stronger specular).
// Features:
// - Sun trail: wide + tight specular for long reflection path on water
// - Subsurface scattering: green-blue light through thin wave crests
// - Jacobian foam from FFT compute
// - Underwater camera: blue-green fog when camera below sea level
// - Micro-normal noise to add capillary-scale detail FFT can't resolve

import * as THREE from 'three';
import { MeshBasicNodeMaterial, StorageTexture } from 'three/webgpu';
import {
  Fn, float, vec2, vec3,
  uniform,
  positionLocal, positionWorld, cameraPosition,
  sin, cos, dot, normalize, mix, smoothstep, pow, exp,
  max, clamp, reflect, fract,
  texture,
} from 'three/tsl';

// ── Hash for micro-normal noise ──
const hash22 = Fn(([p_immutable]: [ReturnType<typeof vec2>]) => {
  const p = vec2(p_immutable);
  const q = vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)));
  return fract(sin(q).mul(43758.5453)).mul(2.0).sub(1.0);
});

export interface OceanFFTMaterialUniforms {
  time: { value: number };
  sunDirection: { value: THREE.Vector3 };
}

export interface OceanFFTMaterialResult {
  material: MeshBasicNodeMaterial;
  uniforms: OceanFFTMaterialUniforms;
}

export function createOceanFFTMaterial(
  displacementTex: StorageTexture,
  normalFoamTex: StorageTexture,
  patchSize: number,
): OceanFFTMaterialResult {
  const material = new MeshBasicNodeMaterial();
  material.side = THREE.FrontSide;

  const uTime = uniform(0.0);
  const uSunDir = uniform(new THREE.Vector3(0.5, 0.85, 0.3).normalize());

  displacementTex.wrapS = THREE.RepeatWrapping;
  displacementTex.wrapT = THREE.RepeatWrapping;
  normalFoamTex.wrapS = THREE.RepeatWrapping;
  normalFoamTex.wrapT = THREE.RepeatWrapping;

  const dispTex = texture(displacementTex);
  const nfTex = texture(normalFoamTex);

  // ── Vertex: FFT displacement ──

  material.positionNode = Fn(() => {
    const pos = positionLocal;
    const tileUV = vec2(pos.x.div(patchSize), pos.z.div(patchSize));
    const disp = dispTex.sample(tileUV);

    return vec3(
      pos.x.add(disp.y),
      pos.y.add(disp.x),
      pos.z.add(disp.z),
    );
  })();

  // ── Fragment: full custom lighting with FFT normals ──

  material.colorNode = Fn(() => {
    const V = normalize(cameraPosition.sub(positionWorld));
    const tileUV = vec2(positionLocal.x.div(patchSize), positionLocal.z.div(patchSize));

    // Read FFT normal + foam
    const nfSample = nfTex.sample(tileUV);
    const fftNx = nfSample.x;
    const fftNy = nfSample.y;
    const fftNz = nfSample.z;
    const foam = clamp(nfSample.w, 0.0, 1.0);

    // ── Micro-normal noise (3 octaves, capillary-scale detail) ──
    // Scale by world position for true cm-scale grain regardless of patch size.
    // Slightly stronger than before to add visible detail at close range where
    // the FFT resolution (~1m texel) can't resolve capillary waves.
    const worldXZ = vec2(positionLocal.x, positionLocal.z);
    const microUV1 = worldXZ.mul(1.5).add(uTime.mul(vec2(0.3, 0.2)));
    const microUV2 = worldXZ.mul(4.0).add(uTime.mul(vec2(-0.5, 0.4)));
    const microUV3 = worldXZ.mul(12.0).add(uTime.mul(vec2(0.8, -0.6)));
    const n1 = hash22(microUV1);
    const n2 = hash22(microUV2);
    const n3 = hash22(microUV3);
    const microX = n1.x.mul(0.025).add(n2.x.mul(0.015)).add(n3.x.mul(0.008));
    const microZ = n1.y.mul(0.025).add(n2.y.mul(0.015)).add(n3.y.mul(0.008));

    const N = normalize(vec3(fftNx.add(microX), fftNy, fftNz.add(microZ)));

    // ── Underwater detection ──
    const camY = cameraPosition.y;
    const isUnderwater = smoothstep(float(0.5), float(-0.5), camY);

    // ── Fresnel (Schlick, F0 = 0.02) ──
    const NoV = clamp(dot(N, V), 0.0, 1.0);
    const fresnel = float(0.02).add(float(0.98).mul(pow(float(1.0).sub(NoV), 5.0)));

    // ── Sky reflection ──
    const R = reflect(V.negate(), N);
    const skyT = clamp(R.y, 0.0, 1.0);
    const skyHorizon = vec3(0.62, 0.65, 0.70);
    const skyZenith  = vec3(0.35, 0.42, 0.55);
    const skyBelow   = vec3(0.015, 0.025, 0.04);
    const skyAbove = mix(skyHorizon, skyZenith, smoothstep(0.0, 0.4, skyT));
    const belowFactor = smoothstep(float(0.02), float(-0.12), R.y);
    const skyBase = mix(skyAbove, skyBelow, belowFactor);

    // Sun glow in reflection (subtle — specular handles the brightness)
    const sunDot = max(dot(R, uSunDir), 0.0);
    const sunGlowWide = pow(sunDot, float(32.0)).mul(0.15);
    const sunGlowTight = pow(sunDot, float(256.0)).mul(0.8);
    const sunCol = vec3(1.0, 0.96, 0.90);
    const skyColor = skyBase.add(sunCol.mul(sunGlowWide.add(sunGlowTight)));

    // ── Sun Specular (Blinn-Phong, moderate) ──
    // Two layers: wide for sun trail visibility, tight for sharp glints.
    // Lower multipliers — bloom amplifies anything >1 into HDR glow.
    const H = normalize(V.add(uSunDir));
    const NdH = max(dot(N, H), 0.0);
    const specWide = pow(NdH, float(256.0)).mul(2.0);
    const specTight = pow(NdH, float(1024.0)).mul(40.0);
    const specContrib = sunCol.mul(specWide.add(specTight));

    // ── Water body color ──
    const baseWater = vec3(0.003, 0.014, 0.025);

    // Subsurface scattering (stronger than Gerstner version)
    const NdL = max(dot(N, uSunDir), 0.0);
    const waveHeight = clamp(positionWorld.y.mul(0.15), 0.0, 1.0);
    const sss = NdL.mul(waveHeight).mul(0.12);
    const sssCol = vec3(0.0, 0.08, 0.06);
    const waterColor = baseWater.add(sssCol.mul(sss));

    // ── Foam (Jacobian) ──
    const foamCol = vec3(0.55, 0.58, 0.62);
    const foamAmount = pow(foam, float(0.8)).mul(0.30);

    // ── Compose (above water) ──
    const reflected = skyColor.add(specContrib);
    const refracted = mix(waterColor, foamCol, foamAmount);
    const aboveColor = mix(refracted, reflected, fresnel);

    // ── Distance fog (above water) ──
    const toCamera = positionWorld.sub(cameraPosition);
    const distSq = dot(toCamera, toCamera);
    const fogDensity = float(0.00020);
    const viewHoriz = float(1.0).sub(clamp(V.y.mul(2.0), 0.0, 1.0));
    const fogStrength = fogDensity.add(fogDensity.mul(0.45).mul(viewHoriz));
    const fogFactor = clamp(exp(fogStrength.negate().mul(fogStrength).mul(distSq)), 0.0, 1.0);
    const fogCol = vec3(0.60, 0.64, 0.70);
    const aboveResult = mix(fogCol, aboveColor, fogFactor);

    // ── Underwater rendering ──
    const uwFogCol = vec3(0.005, 0.06, 0.08);  // dark blue-green
    const uwDist = clamp(float(1.0).sub(exp(float(-0.08).mul(distSq.mul(0.0001)))), 0.0, 1.0);
    // Flip normal for underwater view, invert Fresnel
    const uwColor = mix(vec3(0.003, 0.04, 0.05), uwFogCol, uwDist);
    // Caustics approximation: animated bright spots
    const caustUV = positionWorld.xz.mul(0.15).add(uTime.mul(vec2(0.2, 0.15)));
    const caust = pow(max(sin(caustUV.x.mul(3.0).add(cos(caustUV.y.mul(2.5)))).mul(0.5).add(0.5), 0.0), float(3.0));
    const uwLit = uwColor.add(vec3(0.01, 0.04, 0.03).mul(caust.mul(NdL)));

    // ── Blend above/underwater ──
    return mix(aboveResult, uwLit, isUnderwater);
  })();

  return {
    material,
    uniforms: { time: uTime, sunDirection: uSunDir },
  };
}
