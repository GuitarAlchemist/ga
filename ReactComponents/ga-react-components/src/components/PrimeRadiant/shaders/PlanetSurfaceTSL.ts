// src/components/PrimeRadiant/shaders/PlanetSurfaceTSL.ts
// TSL planet surface material — full port of the legacy PLANET_VERT/PLANET_FRAG
// GLSL shaders from SolarSystem.ts.
//
// Features (match legacy GLSL 1:1):
//  - Texture-derived bump normals (3x3 Sobel over luminance)
//  - Day/night blend via manual NdotL using uSunPosWorld (world-space sun)
//  - Optional night map (Earth city lights) with emissive blend
//  - Optional specular map (Earth ocean shimmer) with Blinn-Phong
//  - Seasonal snow/ice coverage for Earth (uMonth 1-12)
//  - Sunrise/sunset terminator glow (golden hour band)
//  - Atmosphere Fresnel rim (Earth blue, Venus orange)
//  - Limb darkening (quadratic, stable)
//  - Atmospheric in-scattering with Rayleigh phase
//  - Optional vertex displacement via height map
//
// Renderer: MeshBasicNodeMaterial — fully self-lit, manual lighting computed in
// fragment shader from uSunPosWorld. Does NOT respond to scene lights. Matches
// legacy behavior exactly.
//
// Renderer-agnostic: auto-compiles to GLSL (WebGL2) or WGSL (WebGPU).

import * as THREE from 'three';
import { MeshBasicNodeMaterial } from 'three/webgpu';
import {
  Fn, float, vec2, vec3,
  uniform, texture, uv,
  positionLocal, normalLocal,
  positionWorld, normalWorld, cameraPosition,
  mix, smoothstep, cos, abs, max, pow, exp, dot, normalize, cross,
} from 'three/tsl';

export type AtmosphereType = 'none' | 'blue' | 'orange';

export interface PlanetSurfaceMaterialOptions {
  map: THREE.Texture;
  nightMap?: THREE.Texture;
  specularMap?: THREE.Texture;
  displacementMap?: THREE.Texture;
  displacementScale?: number;
  isEarth?: boolean;
  atmosphereType?: AtmosphereType;
  roughness?: number;
  textureSize?: number; // for bump-map pixel size; default 2048
}

/**
 * Create a TSL-based planet surface material that matches the legacy
 * PLANET_FRAG GLSL shader exactly.
 *
 * Exposes these uniforms for per-frame updates via `material.userData`:
 *   - sunPosUniform: THREE.Vector3  (sun position in WORLD space — update each frame)
 *   - monthUniform: number          (1-12, for Earth seasonal snow)
 *   - dispScaleUniform: number      (displacement scale multiplier)
 */
export function createPlanetSurfaceMaterialTSL(
  options: PlanetSurfaceMaterialOptions,
): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();
  const {
    map,
    nightMap,
    specularMap,
    displacementMap,
    displacementScale = 0.05,
    isEarth = false,
    atmosphereType = 'none',
    roughness = 0.85,
    textureSize = 2048,
  } = options;

  map.colorSpace = THREE.SRGBColorSpace;
  map.minFilter = THREE.LinearMipmapLinearFilter;
  map.magFilter = THREE.LinearFilter;

  // ── Uniforms ──
  const uSunPosWorld = uniform(new THREE.Vector3(0, 0, 0));
  const uMonth = uniform(1.0);
  const uDispScale = uniform(displacementScale);
  const uTexelSize = uniform(new THREE.Vector2(1 / textureSize, 1 / textureSize));

  // ── Bump strength (uniform, altitude-driven) ──
  // Turned from a hardcoded float into a uniform so the scene
  // update loop can attenuate it when the camera is close to the
  // planet. At close zoom the Sobel-from-luminance bump normals
  // generate exaggerated fake 3D from what is really just JPEG
  // artifacts and vegetation variation in the daymap, producing
  // visible "mosaic tile" shading over flat regions (e.g. the
  // Amazon basin). The default value matches the legacy hardcoded
  // 0.4 so existing behavior is preserved when the uniform is not
  // driven.
  const uBumpStrength = uniform(0.4);

  material.userData.sunPosUniform = uSunPosWorld;
  material.userData.monthUniform = uMonth;
  material.userData.dispScaleUniform = uDispScale;
  material.userData.bumpStrengthUniform = uBumpStrength;

  const atmoCode =
    atmosphereType === 'blue' ? 1.0 : atmosphereType === 'orange' ? 2.0 : 0.0;
  const uAtmoColor = float(atmoCode);
  const uRoughness = float(roughness);
  const uIsEarth = float(isEarth ? 1.0 : 0.0);
  const uHasNight = float(nightMap ? 1.0 : 0.0);
  const uHasSpec = float(specularMap ? 1.0 : 0.0);

  const mapTex = texture(map);
  const nightTex = nightMap ? texture(nightMap) : null;
  const specTex = specularMap ? texture(specularMap) : null;

  // ── Vertex displacement (optional) ──
  if (displacementMap) {
    displacementMap.minFilter = THREE.LinearMipmapLinearFilter;
    displacementMap.magFilter = THREE.LinearFilter;
    const dispTex = texture(displacementMap);
    material.positionNode = Fn(() => {
      const uvCoord = uv();
      const height = dispTex.sample(uvCoord).r;
      // Legacy: (height - 0.3) * scale
      return positionLocal.add(normalLocal.mul(height.sub(0.3).mul(uDispScale)));
    })();
  }

  // ── Luminance helper ──
  const luma = (rgb: ReturnType<typeof vec3>) =>
    rgb.dot(vec3(0.299, 0.587, 0.114));

  // ── Bump normal from texture luminance (3x3 Sobel) ──
  const getBumpNormal = Fn(() => {
    const uvCoord = uv();
    const tx = uTexelSize.x;
    const ty = uTexelSize.y;

    const tl = luma(mapTex.sample(uvCoord.add(vec2(tx.negate(), ty))).rgb);
    const t = luma(mapTex.sample(uvCoord.add(vec2(float(0.0), ty))).rgb);
    const tr = luma(mapTex.sample(uvCoord.add(vec2(tx, ty))).rgb);
    const l = luma(mapTex.sample(uvCoord.add(vec2(tx.negate(), float(0.0)))).rgb);
    const r = luma(mapTex.sample(uvCoord.add(vec2(tx, float(0.0)))).rgb);
    const bl = luma(mapTex.sample(uvCoord.add(vec2(tx.negate(), ty.negate()))).rgb);
    const b = luma(mapTex.sample(uvCoord.add(vec2(float(0.0), ty.negate()))).rgb);
    const br = luma(mapTex.sample(uvCoord.add(vec2(tx, ty.negate()))).rgb);

    // Sobel
    const dX = tr.add(r.mul(2.0)).add(br).sub(tl.add(l.mul(2.0)).add(bl));
    const dY = bl.add(b.mul(2.0)).add(br).sub(tl.add(t.mul(2.0)).add(tr));

    // Bump strength is now driven by uBumpStrength (was a hardcoded
    // float(0.4)). The update loop in SolarSystem.ts attenuates it
    // with camera altitude so close zoom does not show Sobel
    // artifacts from the daymap's JPEG compression.
    const N = normalize(normalWorld);
    const T = normalize(cross(N, vec3(0.0, 1.0, 0.0001)));
    const B = cross(N, T);
    return normalize(N.add(T.mul(dX).add(B.mul(dY)).mul(uBumpStrength)));
  });

  // ── Color node: full lighting pipeline ──
  material.colorNode = Fn(() => {
    const uvCoord = uv();

    // World-space vectors
    const worldPos = positionWorld;
    const viewDir = normalize(cameraPosition.sub(worldPos));
    const sunDir = normalize(uSunPosWorld.sub(worldPos));

    const N = getBumpNormal();
    const NdotL = dot(N, sunDir);
    // Macro-normal version for effects that should NOT be
    // perturbed by per-texel bump normals. Using the bumped
    // normal everywhere drags NdotL near zero over rough terrain
    // (e.g. the Himalayas), which makes the terminator-glow
    // Gaussian fire at mid-day and sprays red-brown "sunrise"
    // color onto mountain ranges. The terminator should follow
    // the macro sphere normal, not the local bump.
    const NMacro = normalize(normalWorld);
    const NdotLMacro = dot(NMacro, sunDir);
    // Wider terminator for visible day/night transition from all camera angles.
    // Range ±0.15 gives ~30° twilight zone.
    const dayFactor = smoothstep(-0.15, 0.15, NdotL);

    // Day color
    const dayColorBase = mapTex.sample(uvCoord).rgb.toVar();

    // ── Seasonal snow (Earth only) ──
    if (isEarth) {
      const lat = uvCoord.y.sub(0.5).mul(2.0); // -1 (south) to +1 (north)
      const absLat = abs(lat);

      const monthRad = uMonth.sub(1.0).div(12.0).mul(6.28318);
      const winterFactorN = cos(monthRad).add(1.0).mul(0.5);
      const winterFactorS = cos(monthRad.add(3.14159)).add(1.0).mul(0.5);

      const snowLineN = winterFactorN.mul(0.25).add(0.55);
      const snowLineS = winterFactorS.mul(0.25).add(0.55);

      const snowNorth = smoothstep(snowLineN.sub(0.15), snowLineN.add(0.05), absLat);
      const snowSouth = smoothstep(snowLineS.sub(0.15), snowLineS.add(0.05), absLat);
      const latIsNorth = smoothstep(-0.01, 0.01, lat);
      const snowAmount = mix(snowSouth, snowNorth, latIsNorth);

      const iceCap = smoothstep(0.78, 0.88, absLat);
      const finalSnow = max(snowAmount, iceCap);

      const snowColor = vec3(0.92, 0.95, 1.0);
      dayColorBase.assign(mix(dayColorBase, snowColor, finalSnow.mul(0.7)));
    }

    // ── Diffuse + Venus albedo boost ──
    const diffuse = max(NdotL, 0.0);
    const isVenus = smoothstep(1.5, 1.51, uAtmoColor); // 1.0 if uAtmoColor > 1.5
    const albedoBoost = mix(float(1.0), float(1.5), isVenus);
    const litDay = dayColorBase.mul(diffuse.mul(0.97).add(0.03)).mul(albedoBoost).toVar();

    // ── Specular (Blinn-Phong) ──
    const halfDir = normalize(sunDir.add(viewDir));
    const specAngle = max(dot(N, halfDir), 0.0);
    const specPower = mix(float(16.0), float(64.0), float(1.0).sub(uRoughness));
    const specBase = pow(specAngle, specPower).mul(float(1.0).sub(uRoughness)).mul(0.35).toVar();
    if (specTex) {
      const specMask = specTex.sample(uvCoord).r;
      specBase.assign(specBase.mul(specMask));
    }
    litDay.assign(litDay.add(vec3(1.0, 0.95, 0.9).mul(specBase).mul(dayFactor)));

    // ── Night side ──
    // Earth: city lights from nightMap. Others: faint blue so dark side is visible.
    const nightColor = nightTex
      ? nightTex.sample(uvCoord).rgb.mul(0.8)
      : vec3(0.005, 0.008, 0.02);

    // ── Day/night blend across terminator ──
    const surfaceColor = mix(nightColor, litDay, dayFactor).toVar();

    // ── Sunrise/sunset terminator glow ──
    // Uses NdotLMacro (geometric sphere normal) NOT the bump-
    // perturbed N — otherwise rough terrain can fake the
    // terminator at mid-day and spray red sunrise color onto
    // mountain ranges. See the normal-computation block above.
    const hasAtmo = smoothstep(0.49, 0.51, uAtmoColor); // 1.0 if uAtmoColor > 0.5
    const terminatorBand = exp(NdotLMacro.mul(NdotLMacro).negate().div(0.03));
    const sunriseColorDeep = vec3(0.8, 0.2, 0.05);
    const sunriseColorWarm = vec3(1.0, 0.6, 0.2);
    const warmBlend = smoothstep(-0.08, 0.08, NdotLMacro);
    const sunriseColor = mix(sunriseColorDeep, sunriseColorWarm, warmBlend);
    // Intensity: Earth (1) = 0.35, Venus (2) = 0.2, Mars (handled separately) = 0.1
    const isEarthAtmo = float(1.0).sub(smoothstep(1.49, 1.51, uAtmoColor)).mul(hasAtmo);
    const isVenusAtmo = smoothstep(1.49, 1.51, uAtmoColor);
    const atmoStrength = isEarthAtmo.mul(0.35).add(isVenusAtmo.mul(0.2));
    surfaceColor.assign(
      surfaceColor.add(sunriseColor.mul(terminatorBand).mul(atmoStrength).mul(0.5).mul(hasAtmo)),
    );

    // ── Atmosphere Fresnel rim glow ──
    const viewN = normalize(normalWorld);
    const fresnel = pow(float(1.0).sub(abs(dot(viewN, viewDir))), float(3.0));

    // Earth (blue) atmosphere
    const earthDayRim = vec3(0.25, 0.45, 1.0).mul(fresnel).mul(0.35).mul(dayFactor);
    const earthNightRim = vec3(0.05, 0.1, 0.3).mul(fresnel).mul(0.2).mul(float(1.0).sub(dayFactor));
    // Earth terminator Fresnel — uses NdotLMacro for the same
    // reason as the main terminator glow: bump-perturbed normals
    // over rough terrain would misfire this warm rim.
    const earthTerminatorFresnel = fresnel.mul(exp(NdotLMacro.mul(NdotLMacro).negate().div(0.02)));
    const earthWarmRim = vec3(1.0, 0.5, 0.15).mul(earthTerminatorFresnel).mul(0.4);
    surfaceColor.assign(
      surfaceColor.add(earthDayRim.add(earthNightRim).add(earthWarmRim).mul(isEarthAtmo)),
    );

    // Venus (orange) atmosphere
    const venusHaze = vec3(1.0, 0.7, 0.2).mul(fresnel).mul(0.3);
    // Venus terminator Fresnel — same fix as Earth.
    const venusTerminatorFresnel = fresnel.mul(exp(NdotLMacro.mul(NdotLMacro).negate().div(0.02)));
    const venusWarmRim = vec3(1.0, 0.5, 0.1).mul(venusTerminatorFresnel).mul(0.25);
    surfaceColor.assign(surfaceColor.add(venusHaze.add(venusWarmRim).mul(isVenusAtmo)));

    // ── Limb darkening (quadratic — stable, no flicker) ──
    const NdotV_limb = max(dot(viewN, viewDir), 0.0);
    const limbDarken = mix(float(0.35), float(1.0), NdotV_limb.mul(NdotV_limb));
    surfaceColor.assign(surfaceColor.mul(limbDarken));

    // ── Atmospheric in-scattering (Rayleigh) ──
    const inScatter = pow(float(1.0).sub(NdotV_limb), float(2.0)).mul(0.5);
    const cosTheta = dot(viewDir, sunDir);
    const rayleigh = cosTheta.mul(cosTheta).add(1.0).mul(0.75);
    const atmoTintEarth = vec3(0.3, 0.5, 1.0).mul(isEarthAtmo);
    const atmoTintVenus = vec3(0.9, 0.6, 0.2).mul(isVenusAtmo);
    const atmoTint = atmoTintEarth.add(atmoTintVenus);
    surfaceColor.assign(
      surfaceColor.add(atmoTint.mul(inScatter).mul(rayleigh).mul(0.25).mul(hasAtmo)),
    );

    return surfaceColor;
  })();

  return material;
}
