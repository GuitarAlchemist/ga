// src/components/PrimeRadiant/shaders/OrbitTrailTSL.ts
// TSL orbit trail material — per-vertex alpha fading line.
// Requires WebGPURenderer (auto-compiles to GLSL on WebGL2 fallback backend).

import * as THREE from 'three';
import { LineBasicNodeMaterial } from 'three/webgpu';
import { uniform, attribute } from 'three/tsl';

/**
 * Create a TSL orbit trail line material with per-vertex alpha.
 * Replaces the inline GLSL orbit trail ShaderMaterial in SolarSystem.ts.
 *
 * @param color - Trail color (hex or THREE.Color)
 */
export function createOrbitTrailMaterialTSL(color: THREE.ColorRepresentation): LineBasicNodeMaterial {
  const material = new LineBasicNodeMaterial();

  const uColor = uniform(new THREE.Color(color));
  material.colorNode = uColor;
  material.opacityNode = attribute('aAlpha', 'float');

  material.transparent = true;
  material.depthWrite = false;

  return material;
}
