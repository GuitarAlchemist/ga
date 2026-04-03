// src/components/PrimeRadiant/shaders/SkyboxNebulaTSL.ts
// TSL deep space nebula skybox — procedural background sphere
// Orion-like and Carina-like nebulae with hash-based starfield.

import { MeshBasicNodeMaterial } from 'three/webgpu';
import * as THREE from 'three';
import {
  Fn, float, vec3, vec4,
  positionLocal,
  mix, smoothstep, pow, abs, max, step,
  dot, normalize, length,
} from 'three/tsl';
import { fbm6, hash3 } from './TSLNoiseLib';

/**
 * Create a TSL-based deep space nebula skybox material.
 * Renders on BackSide of a large sphere. All procedural, no textures.
 */
export function createSkyboxNebulaMaterialTSL(): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();
  material.side = THREE.BackSide;
  material.depthWrite = false;

  material.colorNode = Fn(() => {
    // Direction from local position (skybox sphere)
    const dir = normalize(positionLocal);

    // Base gradient: near-black space, slight blue-purple tint varies by y
    const baseColor = mix(
      vec3(0.002, 0.002, 0.008),
      vec3(0.005, 0.003, 0.012),
      dir.y.mul(0.5).add(0.5),
    ).toVar();

    // ─── Orion-like nebula (reddish-pink) ───
    const orionCenter = normalize(vec3(0.5, 0.2, -0.8));
    const orionDist = length(dir.sub(orionCenter));
    const orionMask = smoothstep(1.2, 0.2, orionDist);
    const orionNoise = fbm6(dir.mul(4.0).add(vec3(1.3, 2.7, 0.5)));
    const orionColor = vec3(0.12, 0.02, 0.03);
    baseColor.addAssign(orionColor.mul(orionMask).mul(orionNoise));

    // ─── Carina-like nebula (blue-teal) ───
    const carinaCenter = normalize(vec3(-0.6, -0.3, 0.7));
    const carinaDist = length(dir.sub(carinaCenter));
    const carinaMask = smoothstep(1.0, 0.15, carinaDist);
    const carinaNoise = fbm6(dir.mul(5.0).add(vec3(3.1, 0.4, 1.8)));
    const carinaColor = vec3(0.02, 0.05, 0.08);
    baseColor.addAssign(carinaColor.mul(carinaMask).mul(carinaNoise));

    // ─── Faint dust wisps ───
    const dust = fbm6(dir.mul(2.5).add(vec3(7.3, 3.1, 5.7)));
    baseColor.addAssign(vec3(0.003, 0.002, 0.004).mul(dust));

    // ─── Starfield (hash-based points) ───
    // Use high-frequency hash to create star points
    const starGrid = dir.mul(300.0);
    const starHash = hash3(vec3(
      starGrid.x.floor(),
      starGrid.y.floor(),
      starGrid.z.floor(),
    ));
    // Step function: only very high hash values become stars
    const starBright = step(0.997, starHash).mul(starHash);
    // Vary star color slightly
    const starColor = mix(
      vec3(0.8, 0.85, 1.0),
      vec3(1.0, 0.9, 0.7),
      hash3(vec3(starGrid.x.floor().add(100.0), starGrid.y.floor(), starGrid.z.floor())),
    );
    baseColor.addAssign(starColor.mul(starBright).mul(0.08));

    // ─── Dim secondary star layer ───
    const starGrid2 = dir.mul(150.0);
    const starHash2 = hash3(vec3(
      starGrid2.x.floor(),
      starGrid2.y.floor(),
      starGrid2.z.floor(),
    ));
    const starBright2 = step(0.998, starHash2).mul(starHash2);
    baseColor.addAssign(vec3(0.7, 0.75, 0.9).mul(starBright2).mul(0.04));

    return vec4(baseColor, float(1.0));
  })();

  return material;
}
