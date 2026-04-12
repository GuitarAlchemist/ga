// src/components/Ocean/OceanTSLFFT.ts
// Ocean material variant that samples FFT displacement and normal textures
// instead of computing Gerstner waves in-shader.
//
// The shading pipeline (Fresnel, sky reflection, specular, fog) is identical
// to OceanTSL.ts — only the displacement/normal source changes.

import * as THREE from 'three';
import { MeshBasicNodeMaterial, StorageTexture } from 'three/webgpu';
import {
  Fn, float, vec2, vec3,
  uniform,
  positionLocal, positionWorld, cameraPosition,
  dot, normalize, mix, smoothstep, pow, exp,
  max, clamp, reflect,
  texture,
} from 'three/tsl';

export interface OceanFFTMaterialUniforms {
  time: { value: number };
  sunDirection: { value: THREE.Vector3 };
}

export interface OceanFFTMaterialResult {
  material: MeshBasicNodeMaterial;
  uniforms: OceanFFTMaterialUniforms;
}

/**
 * Create an ocean material that reads displacement from FFT compute textures.
 *
 * @param displacementTex - StorageTexture with RGBA = (height, dx, dz, 0)
 * @param normalFoamTex - StorageTexture with RGBA = (nx, ny, nz, foam)
 * @param patchSize - World-space size of one FFT tile (for UV → world mapping)
 */
export function createOceanFFTMaterial(
  displacementTex: StorageTexture,
  normalFoamTex: StorageTexture,
  patchSize: number,
): OceanFFTMaterialResult {
  const material = new MeshBasicNodeMaterial();
  material.side = THREE.FrontSide;

  const uTime = uniform(0.0);
  const uSunDir = uniform(new THREE.Vector3(0.5, 0.85, 0.3).normalize());

  // Wrap displacement texture for tiling
  displacementTex.wrapS = THREE.RepeatWrapping;
  displacementTex.wrapT = THREE.RepeatWrapping;
  normalFoamTex.wrapS = THREE.RepeatWrapping;
  normalFoamTex.wrapT = THREE.RepeatWrapping;

  const dispTex = texture(displacementTex);
  const nfTex = texture(normalFoamTex);

  // ── Vertex: sample FFT displacement texture ──

  material.positionNode = Fn(() => {
    const pos = positionLocal;
    // Map world XZ to tiling UV (wrap at patchSize boundaries)
    const tileUV = vec2(pos.x.div(patchSize), pos.z.div(patchSize));
    const disp = dispTex.sample(tileUV);

    return vec3(
      pos.x.add(disp.y),   // x + dx (choppy horizontal)
      pos.y.add(disp.x),   // y + height
      pos.z.add(disp.z),   // z + dz (choppy horizontal)
    );
  })();

  // ── Fragment: same shading as OceanTSL but reads FFT normal ──

  material.colorNode = Fn(() => {
    const V = normalize(cameraPosition.sub(positionWorld));

    // Read normal and foam from FFT compute texture
    const tileUV = vec2(positionLocal.x.div(patchSize), positionLocal.z.div(patchSize));
    const nfSample = nfTex.sample(tileUV);
    const N = normalize(vec3(nfSample.x, nfSample.y, nfSample.z));
    const foam = clamp(nfSample.w, 0.0, 1.0);

    // ── Fresnel (Schlick, F0 = 0.02) ──
    const NoV = clamp(dot(N, V), 0.0, 1.0);
    const fresnel = float(0.02).add(float(0.98).mul(pow(float(1.0).sub(NoV), 5.0)));

    // ── Reflected sky color ──
    const R = reflect(V.negate(), N);
    const skyT = clamp(R.y, 0.0, 1.0);
    const skyHorizon = vec3(0.62, 0.65, 0.70);
    const skyZenith  = vec3(0.35, 0.42, 0.55);
    const skyBelow   = vec3(0.015, 0.025, 0.04);
    const skyAbove = mix(skyHorizon, skyZenith, smoothstep(0.0, 0.4, skyT));
    const belowFactor = smoothstep(float(0.02), float(-0.12), R.y);
    const skyBase = mix(skyAbove, skyBelow, belowFactor);

    const sunDot = max(dot(R, uSunDir), 0.0);
    const sunGlow = pow(sunDot, float(64.0)).mul(0.8)
      .add(pow(sunDot, float(256.0)).mul(2.0));
    const sunCol = vec3(1.0, 0.96, 0.90);
    const skyColor = skyBase.add(sunCol.mul(sunGlow));

    // ── Sun Specular ──
    const H = normalize(V.add(uSunDir));
    const NdH = max(dot(N, H), 0.0);
    const sunSpec = pow(NdH, float(720.0)).mul(120.0);
    const specContrib = sunCol.mul(sunSpec);

    // ── Water body color ──
    const baseWater = vec3(0.003, 0.014, 0.025);

    // Subsurface scattering
    const NdL = max(dot(N, uSunDir), 0.0);
    const waveHeight = clamp(positionWorld.y.mul(0.2), 0.0, 1.0);
    const sss = NdL.mul(waveHeight).mul(0.06);
    const sssCol = vec3(0.0, 0.04, 0.035);
    const waterColor = baseWater.add(sssCol.mul(sss));

    // ── Foam from Jacobian (FFT-computed) ──
    const foamCol = vec3(0.55, 0.58, 0.62);
    const foamAmount = pow(foam, float(0.8)).mul(0.4);

    // ── Compose ──
    const reflected = skyColor.add(specContrib);
    const refracted = mix(waterColor, foamCol, foamAmount);
    const litColor = mix(refracted, reflected, fresnel);

    // ── Distance fog ──
    const toCamera = positionWorld.sub(cameraPosition);
    const distSq = dot(toCamera, toCamera);
    const fogStrength = float(0.00028);
    const fogFactor = clamp(exp(fogStrength.negate().mul(fogStrength).mul(distSq)), 0.0, 1.0);
    const fogCol = vec3(0.60, 0.64, 0.70);

    return mix(fogCol, litColor, fogFactor);
  })();

  return {
    material,
    uniforms: { time: uTime, sunDirection: uSunDir },
  };
}
