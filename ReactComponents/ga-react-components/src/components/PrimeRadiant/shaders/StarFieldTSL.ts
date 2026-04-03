// src/components/PrimeRadiant/shaders/StarFieldTSL.ts
// TSL star field material — per-vertex colored point sprites with circular disc.
// Requires WebGPURenderer (auto-compiles to GLSL on WebGL2 fallback backend).

import * as THREE from 'three';
import { PointsNodeMaterial } from 'three/webgpu';
import {
  Fn, float, vec2, vec4,
  attribute, vertexColor,
  pointUV, Discard, dot, smoothstep,
} from 'three/tsl';

/**
 * Create a TSL star field point material.
 * Per-vertex color, per-vertex size, circular soft disc via pointUV.
 * Replaces the inline GLSL star field ShaderMaterial in LunarLanderEngine.ts.
 *
 * @param pixelRatio - Device pixel ratio (clamped)
 */
export function createStarFieldMaterialTSL(pixelRatio: number): PointsNodeMaterial {
  const material = new PointsNodeMaterial();

  // Point size from per-vertex 'size' attribute, scaled by pixel ratio
  const sizeAttr = attribute('size', 'float');
  material.sizeNode = sizeAttr.mul(pixelRatio);

  // Circular disc + premultiplied alpha in colorNode (Discard must be in colorNode, not opacityNode)
  material.colorNode = Fn(() => {
    const c = pointUV.sub(vec2(0.5, 0.5));
    const d = dot(c, c);
    Discard(d.greaterThan(0.25));
    const alpha = float(1.0).sub(smoothstep(float(0.1), float(0.25), d)).mul(0.9);
    // Premultiply vertex color by alpha for AdditiveBlending
    return vertexColor().mul(alpha);
  })();

  material.vertexColors = true;
  material.transparent = true;
  material.depthWrite = false;
  material.blending = THREE.AdditiveBlending;

  return material;
}
