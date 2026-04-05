// src/components/PrimeRadiant/shaders/MilkyWayTextureTSL.ts
// Photographic Milky Way panorama — samples an equirectangular astrophotography
// texture instead of procedurally generating the galactic band.
//
// Source: Solar System Scope (https://www.solarsystemscope.com/textures/)
// Their Milky Way panoramas are composites of real ESO/NASA imagery,
// CC-BY 4.0 licensed, standard 2:1 equirectangular format.
//
// Rendered on a BackSide sphere — we're inside it, looking out. SphereGeometry's
// built-in UVs are equirectangular, so we sample with `uv()` directly.

import * as THREE from 'three';
import { MeshBasicNodeMaterial } from 'three/webgpu';
import {
  Fn, float, vec2, vec3, dot, mix,
  uniform, uv, texture as textureNode,
} from 'three/tsl';

export interface MilkyWayTextureOptions {
  /** Brightness multiplier (0..1). Real Milky Way is faint — 0.6-0.9 works. */
  brightness?: number;
  /** Saturation multiplier (0..2). 1.0 = original, <1 = desaturated. */
  saturation?: number;
  /** Flip horizontally — set true if texture appears mirrored on BackSide. */
  flipU?: boolean;
}

/**
 * Create a TSL material that samples a Milky Way panorama texture.
 * Attach to a BackSide SphereGeometry behind the starfield.
 *
 * @param tex Equirectangular Milky Way texture (2:1 aspect)
 */
export function createMilkyWayTextureMaterial(
  tex: THREE.Texture,
  options: MilkyWayTextureOptions = {},
): MeshBasicNodeMaterial {
  const { brightness = 0.7, saturation = 0.85, flipU = true } = options;

  // Texture must use SRGB color space for correct brightness display
  tex.colorSpace = THREE.SRGBColorSpace;
  // Equirectangular skyboxes are seamless horizontally
  tex.wrapS = THREE.RepeatWrapping;
  tex.wrapT = THREE.ClampToEdgeWrapping;

  const material = new MeshBasicNodeMaterial();
  material.side = THREE.BackSide;
  material.depthWrite = false;
  material.transparent = false; // opaque skybox layer
  // NormalBlending not Additive: this IS the background, not an overlay on it
  material.blending = THREE.NormalBlending;

  const uTex = textureNode(tex);
  const uBright = uniform(brightness);
  const uSat = uniform(saturation);

  material.colorNode = Fn(() => {
    const baseUV = uv();
    // BackSide sphere viewed from inside flips horizontal handedness.
    // Offsetting u by 0.5 rotates the texture 180° — puts galactic center
    // (Sagittarius) behind the camera by default, like a real night sky.
    // Flip u too (1 - u) so galactic center reads correctly from inside.
    const u = flipU ? float(1.0).sub(baseUV.x).add(0.5) : baseUV.x;
    const sampled = uTex.sample(vec2(u, baseUV.y));

    // Desaturate toward luminance (space photography often looks over-saturated)
    const luma = dot(sampled.rgb, vec3(0.299, 0.587, 0.114));
    const gray = vec3(luma, luma, luma);
    const tinted = mix(gray, sampled.rgb, uSat);

    return tinted.mul(uBright);
  })();

  return material;
}
