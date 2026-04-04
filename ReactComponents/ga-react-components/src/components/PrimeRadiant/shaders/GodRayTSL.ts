// src/components/PrimeRadiant/shaders/GodRayTSL.ts
// TSL volumetric god ray cone — sine wave light shaft
// Used on ConeGeometry with DoubleSide for volumetric light beams.

import * as THREE from 'three';
import { MeshBasicNodeMaterial } from 'three/webgpu';
import {
  Fn, vec3,
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

  // Pre-multiply the color by the fade — works with AdditiveBlending regardless
  // of how opacityNode is handled. The final color output becomes
  //   uColor * fade * rays * 0.03
  // which with GL_ONE+GL_ONE blending adds that subtle amount per pixel.
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

    // Very subtle (0.002 at peak). The 300-unit cone is rendered from the
    // graph center outward; when the camera sits AT or near the cone apex
    // (which happens frequently since the selected node is near origin),
    // all 40+17 sine-wave ray bands converge radially on screen, stacking
    // into a dominant yellow starburst. Keeping the per-pixel contribution
    // tiny limits the maximum possible convergence brightness.
    const scale = fade.mul(rays).mul(0.002);
    return uColor.mul(scale);
  })();

  return material;
}
