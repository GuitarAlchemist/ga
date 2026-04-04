// src/components/PrimeRadiant/shaders/SaturnRingsTSL.ts
// TSL Saturn rings material — Cassini division, Encke gap, density variation,
// sun-lit front / warm backlit rendering.
// Renderer-agnostic: auto-compiles to GLSL (WebGL2) or WGSL (WebGPU).

import * as THREE from 'three';
import { MeshBasicNodeMaterial } from 'three/webgpu';
import {
  Fn, float, vec3, vec2,
  uniform, texture, uv,
  normalWorld, positionWorld,
  smoothstep, mix, max, dot, normalize, sqrt,
  Discard,
} from 'three/tsl';

export interface SaturnRingsMaterialOptions {
  ringTexture: THREE.Texture;
}

/**
 * Create a TSL-based Saturn rings material.
 *
 * Transparent ring plane with:
 * - Cassini division (gap at r 0.53-0.60)
 * - Encke gap (thin gap at r 0.83-0.87)
 * - Radial density falloff (B ring dense, C ring thin)
 * - Front-lit / warm back-lit illumination from uSunPos
 *
 * The returned material exposes `userData.sunPosUniform` for per-frame updates:
 *   material.userData.sunPosUniform.value.copy(sunWorldPosition);
 */
export function createSaturnRingsMaterialTSL(
  options: SaturnRingsMaterialOptions,
): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();
  const { ringTexture } = options;

  ringTexture.colorSpace = THREE.SRGBColorSpace;
  ringTexture.minFilter = THREE.LinearMipmapLinearFilter;
  ringTexture.magFilter = THREE.LinearFilter;

  // Uniforms
  const uSunPos = uniform(new THREE.Vector3(0, 0, 0));

  // Expose for per-frame update
  material.userData.sunPosUniform = uSunPos;

  const ringTex = texture(ringTexture);

  // ── Color node ──
  material.colorNode = Fn(() => {
    const r = uv().x; // 0 = inner edge, 1 = outer edge

    // Sample ring texture at radial coordinate
    const tex = ringTex.sample(vec2(r, float(0.5)));

    // ── Gaps ──
    // Cassini division: broad gap at 0.53-0.60
    const cassini = smoothstep(0.53, 0.55, r).mul(
      float(1.0).sub(smoothstep(0.58, 0.60, r)),
    );
    // Encke gap: thin gap at 0.83-0.87
    const encke = smoothstep(0.83, 0.84, r).mul(
      float(1.0).sub(smoothstep(0.86, 0.87, r)),
    );
    const gapAlpha = float(1.0).sub(cassini.mul(0.9)).sub(encke.mul(0.7));

    // ── Radial density ──
    // Fade in at inner edge, fade out at outer edge
    // B ring (0.3-0.5) is densest, C ring (0-0.2) is thin
    const density = smoothstep(0.0, 0.15, r)
      .mul(float(1.0).sub(smoothstep(0.95, 1.0, r)))
      .mul(mix(0.4, 1.0, smoothstep(0.2, 0.35, r)));

    // ── Sun illumination ──
    const sunDir = normalize(uSunPos.sub(positionWorld));
    const NdotL = dot(normalWorld, sunDir);

    // Front-lit: direct sunlight
    const frontColor = tex.rgb.mul(max(NdotL, 0.0)).mul(1.2);
    // Back-lit: warm transmitted light through thin ice particles
    const backColor = tex.rgb
      .mul(max(NdotL.negate(), 0.0))
      .mul(0.4)
      .mul(vec3(1.0, 0.85, 0.6));
    // Ambient fill
    const ambient = tex.rgb.mul(0.05);

    const ringColor = frontColor.add(backColor).add(ambient);

    // ── Alpha ──
    const alpha = tex.a.mul(density).mul(gapAlpha);
    Discard(alpha.lessThan(0.01));

    // Store alpha for opacityNode (return color here)
    return ringColor;
  })();

  // ── Opacity node ──
  material.opacityNode = Fn(() => {
    const r = uv().x;

    const tex = ringTex.sample(vec2(r, float(0.5)));

    const cassini = smoothstep(0.53, 0.55, r).mul(
      float(1.0).sub(smoothstep(0.58, 0.60, r)),
    );
    const encke = smoothstep(0.83, 0.84, r).mul(
      float(1.0).sub(smoothstep(0.86, 0.87, r)),
    );
    const gapAlpha = float(1.0).sub(cassini.mul(0.9)).sub(encke.mul(0.7));

    const density = smoothstep(0.0, 0.15, r)
      .mul(float(1.0).sub(smoothstep(0.95, 1.0, r)))
      .mul(mix(0.4, 1.0, smoothstep(0.2, 0.35, r)));

    const alpha = tex.a.mul(density).mul(gapAlpha);
    return alpha.mul(0.85);
  })();

  // Material settings
  material.transparent = true;
  material.depthWrite = false;
  material.side = THREE.DoubleSide;

  return material;
}
