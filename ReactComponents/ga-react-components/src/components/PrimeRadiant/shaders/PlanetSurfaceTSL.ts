// src/components/PrimeRadiant/shaders/PlanetSurfaceTSL.ts
// TSL planet surface material — texture, displacement mapping, Earth seasonal tint.
// Uses MeshStandardNodeMaterial for proper PBR lighting on planetary surfaces.
// Renderer-agnostic: auto-compiles to GLSL (WebGL2) or WGSL (WebGPU).

import * as THREE from 'three';
import { MeshStandardNodeMaterial } from 'three/webgpu';
import {
  Fn, float, vec3, vec2, vec4,
  uniform, texture, uv,
  normalLocal, positionLocal,
  mix, smoothstep, sin, cos, abs, clamp,
} from 'three/tsl';

export interface PlanetSurfaceMaterialOptions {
  planetTexture: THREE.Texture;
  displacementMap?: THREE.Texture;
  displacementScale?: number;
  isEarth?: boolean;
}

/**
 * Create a TSL-based planet surface material.
 *
 * Features:
 * - Base texture color with PBR lighting (MeshStandardNodeMaterial)
 * - Optional displacement mapping (vertex displacement along normal)
 * - Earth-specific seasonal tinting based on uMonth uniform
 *
 * For Earth, expose `userData.monthUniform` for per-frame month updates:
 *   material.userData.monthUniform.value = currentMonth; // 1-12
 */
export function createPlanetSurfaceMaterialTSL(
  options: PlanetSurfaceMaterialOptions,
): MeshStandardNodeMaterial {
  const material = new MeshStandardNodeMaterial();
  const {
    planetTexture,
    displacementMap,
    displacementScale = 0.05,
    isEarth = false,
  } = options;

  planetTexture.colorSpace = THREE.SRGBColorSpace;
  planetTexture.minFilter = THREE.LinearMipmapLinearFilter;
  planetTexture.magFilter = THREE.LinearFilter;

  // Uniforms
  const uMonth = uniform(1.0);       // 1-12 calendar month
  const uDispScale = uniform(displacementScale);

  // Expose uniforms for per-frame updates
  material.userData.monthUniform = uMonth;
  material.userData.dispScaleUniform = uDispScale;

  const planetTex = texture(planetTexture);

  // ── Color node ──
  material.colorNode = Fn(() => {
    const uvCoord = uv();
    const baseColor = planetTex.sample(uvCoord).rgb.toVar();

    if (isEarth) {
      // Seasonal tinting for Earth
      // Summer (month 6-7): warm green boost in northern hemisphere
      // Winter (month 12-1): cool blue tint in northern hemisphere
      // UV.y < 0.5 = northern hemisphere (texture top), > 0.5 = southern

      // Seasonal phase: 0 at Jan, 1 at Jul, back to 0 at Dec
      const seasonPhase = sin(uMonth.sub(1.0).mul(Math.PI / 6.0)); // -1 to 1

      // Latitude factor: +1 at north pole, -1 at south pole
      const latitude = float(1.0).sub(uvCoord.y.mul(2.0));

      // Hemisphere-aware season: northern summer when seasonPhase > 0
      const localSeason = seasonPhase.mul(latitude);

      // Summer warmth: green-yellow tint
      const summerTint = vec3(0.02, 0.04, -0.02);
      // Winter cool: blue tint
      const winterTint = vec3(-0.02, -0.01, 0.03);

      const tint = mix(winterTint, summerTint, smoothstep(-0.5, 0.5, localSeason));

      // Apply tint only to land areas (approximate: greener pixels are land)
      // Simple heuristic: if green channel is dominant, it's likely land
      const landMask = smoothstep(0.0, 0.15, baseColor.g.sub(baseColor.b.mul(0.8)));
      baseColor.addAssign(tint.mul(landMask.mul(0.6)));
    }

    return baseColor;
  })();

  // ── Displacement node ──
  if (displacementMap) {
    displacementMap.minFilter = THREE.LinearMipmapLinearFilter;
    displacementMap.magFilter = THREE.LinearFilter;

    const dispTex = texture(displacementMap);

    material.positionNode = Fn(() => {
      const uvCoord = uv();
      const pos = positionLocal.toVar();

      // Sample displacement height (grayscale)
      const height = dispTex.sample(uvCoord).r;

      // Displace along local normal
      const displaced = pos.add(normalLocal.mul(height.mul(uDispScale)));

      return displaced;
    })();

    // Compute approximate normals from displacement for better lighting.
    // Use central-difference sampling of the height map.
    material.normalNode = Fn(() => {
      const uvCoord = uv();
      const texelSize = float(1.0 / 1024.0); // assume 1024px texture

      // Sample neighboring heights
      const hL = dispTex.sample(uvCoord.sub(vec2(texelSize, 0.0))).r;
      const hR = dispTex.sample(uvCoord.add(vec2(texelSize, 0.0))).r;
      const hD = dispTex.sample(uvCoord.sub(vec2(0.0, texelSize))).r;
      const hU = dispTex.sample(uvCoord.add(vec2(0.0, texelSize))).r;

      // Tangent-space normal from height differences
      const scale = uDispScale.mul(2.0);
      const n = vec3(
        hL.sub(hR).mul(scale),
        hD.sub(hU).mul(scale),
        float(1.0),
      ).normalize();

      return n;
    })();
  }

  // PBR properties
  material.roughness = 0.85;
  material.metalness = 0.0;

  return material;
}
