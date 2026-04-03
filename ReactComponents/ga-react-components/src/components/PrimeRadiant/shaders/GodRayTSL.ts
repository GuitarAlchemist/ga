// src/components/PrimeRadiant/shaders/GodRayTSL.ts
// TSL volumetric god ray cone — sine wave light shaft
// Used on ConeGeometry with DoubleSide for volumetric light beams.

import * as THREE from 'three';
import { MeshBasicNodeMaterial } from 'three/webgpu';
import {
  Fn, float, vec3, vec4,
  uv, time, sin, smoothstep,
} from 'three/tsl';

/**
 * Create a TSL-based god ray material for volumetric light shafts.
 * Renders on a ConeGeometry with additive blending.
 */
export function createGodRayMaterialTSL(
  color: THREE.ColorRepresentation = 0xff8800,
): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();
  material.transparent = true;
  material.side = THREE.DoubleSide;
  material.depthWrite = false;
  material.blending = THREE.AdditiveBlending;

  const c = new THREE.Color(color);
  const uColor = vec3(c.r, c.g, c.b);

  material.colorNode = Fn(() => {
    const uvCoord = uv();
    const t = time;

    // Vertical fade — peak at center of cone height
    const fade = smoothstep(0.0, 0.5, uvCoord.y).mul(
      smoothstep(1.0, 0.5, uvCoord.y),
    );

    // Layered sine wave rays
    const rays1 = sin(uvCoord.x.mul(40.0).add(t.mul(0.5))).mul(0.5).add(0.5);
    const rays2 = sin(uvCoord.x.mul(17.0).sub(t.mul(0.3))).mul(0.5).add(0.5);
    const rays = rays1.mul(rays2);

    // Final alpha — very subtle
    const alpha = fade.mul(rays).mul(0.03);

    return vec4(uColor, alpha);
  })();

  return material;
}
