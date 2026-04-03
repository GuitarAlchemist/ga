// src/components/PrimeRadiant/shaders/StormVortexTSL.ts
// Jupiter Great Red Spot — rotating spiral vortex material.
// Renderer-agnostic: auto-compiles to GLSL (WebGL2) or WGSL (WebGPU).

import * as THREE from 'three';
import { MeshBasicNodeMaterial } from 'three/webgpu';
import {
  Fn, float, vec2, vec3,
  uv, time,
  sin, pow, abs, mix, smoothstep, sqrt, atan2,
} from 'three/tsl';

/**
 * Create a storm vortex material for Jupiter's Great Red Spot.
 * Rotating spiral with red-brown storm colors and distance-based falloff.
 */
export function createStormVortexMaterialTSL(): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();

  material.colorNode = Fn(() => {
    const uvCoord = uv();

    // Center offset
    const c = uvCoord.sub(vec2(0.5, 0.5));
    const dist = sqrt(c.dot(c));
    const angle = atan2(c.y, c.x);

    // Spiral pattern
    const spiral = sin(angle.mul(3.0).sub(dist.mul(20.0)).add(time.mul(1.5))).mul(0.5).add(0.5);

    // Secondary spiral for turbulence
    const spiral2 = sin(angle.mul(5.0).add(dist.mul(15.0)).sub(time.mul(1.0))).mul(0.5).add(0.5);
    const turbulence = spiral.mul(0.7).add(spiral2.mul(0.3));

    // Storm colors — deep red core to brown edges
    const deepRed = vec3(0.7, 0.15, 0.05);
    const warmBrown = vec3(0.85, 0.45, 0.15);
    const paleEdge = vec3(0.95, 0.75, 0.5);

    // Color mix based on distance and spiral
    const col = mix(deepRed, warmBrown, turbulence.mul(0.6)).toVar();
    col.assign(mix(col, paleEdge, dist.mul(1.5)));

    // Bright eye at center
    const eyeBrightness = smoothstep(0.15, 0.0, dist);
    col.addAssign(vec3(0.3, 0.1, 0.02).mul(eyeBrightness));

    return col;
  })();

  material.opacityNode = Fn(() => {
    const uvCoord = uv();
    const c = uvCoord.sub(vec2(0.5, 0.5));
    const dist = sqrt(c.dot(c));

    // Radial falloff — fully opaque in center, fades at edges
    return smoothstep(0.5, 0.15, dist).mul(0.85);
  })();

  material.transparent = true;
  material.depthWrite = false;
  material.side = THREE.DoubleSide;

  return material;
}
