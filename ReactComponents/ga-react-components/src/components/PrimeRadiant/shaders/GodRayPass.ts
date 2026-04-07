// src/components/PrimeRadiant/shaders/GodRayPass.ts
// GLSL post-processing pass for screen-space volumetric god rays from the Sun.
//
// GPU Gems 3 radial-blur approach: for each pixel, march from the pixel
// toward the sun's screen position, accumulating light samples with
// exponential decay. The result is blended additively over the scene,
// creating volumetric light shafts that stream outward from the sun
// through occluding geometry (planets, rings, nodes).
//
// Governance meaning: the Sun represents the constitutional core — the
// Asimov constitution radiating governance authority outward. God rays
// are the visible propagation of that authority through the institutional
// structure; they bend around opaque governance bodies (nodes) and
// illuminate the spaces between, making the hierarchy's light tangible.
//
// Integration with ForceRadiant.tsx:
// 1. Import and create the pass:
//      import { GodRayShader, updateGodRayUniforms } from './shaders/GodRayPass';
//      const godRayPass = new ShaderPass(GodRayShader);
//      fg.postProcessingComposer().addPass(godRayPass);
//      godRayPassRef.current = godRayPass;
//
// 2. In the tick loop, update sun screen position every frame:
//      const sunWorldPos = sunMeshRef.current.position; // or sunGroup world pos
//      updateGodRayUniforms(
//        godRayPassRef.current,
//        sunWorldPos,
//        camera,
//        effectsEnabled,  // boolean toggle
//      );
//
// Pass ordering: add AFTER bloom (god rays should pick up the bloom
// glow around the sun) and BEFORE Moebius/dispersion (edge detection
// should capture the ray streaks, dispersion should split them).

import * as THREE from 'three';

export const GodRayShader = {
  uniforms: {
    tDiffuse: { value: null as THREE.Texture | null },
    uSunScreenPos: { value: new THREE.Vector2(0.5, 0.5) },
    uDensity: { value: 0.5 },       // ray density — how far each step advances
    uWeight: { value: 0.02 },       // per-sample contribution weight
    uDecay: { value: 0.97 },        // exponential decay per step (< 1.0)
    uExposure: { value: 0.25 },     // final brightness multiplier
    uSamples: { value: 60 },        // number of ray-march steps
    uEnabled: { value: 1.0 },       // 0 = bypass, 1 = active
  },

  vertexShader: /* glsl */ `
    varying vec2 vUv;
    void main() {
      vUv = uv;
      gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
    }
  `,

  fragmentShader: /* glsl */ `
    uniform sampler2D tDiffuse;
    uniform vec2 uSunScreenPos;
    uniform float uDensity;
    uniform float uWeight;
    uniform float uDecay;
    uniform float uExposure;
    uniform int uSamples;
    uniform float uEnabled;
    varying vec2 vUv;

    void main() {
      vec4 scene = texture2D(tDiffuse, vUv);

      if (uEnabled < 0.5) {
        gl_FragColor = scene;
        return;
      }

      // Delta from this pixel toward the sun, scaled by density / sample count
      vec2 deltaTexCoord = (vUv - uSunScreenPos) * uDensity / float(uSamples);

      float illuminationDecay = 1.0;
      vec3 godRay = vec3(0.0);
      vec2 sampleCoord = vUv;

      // March toward the sun, accumulating light with exponential decay
      for (int i = 0; i < 60; i++) {
        if (i >= uSamples) break;
        sampleCoord -= deltaTexCoord;
        vec3 s = texture2D(tDiffuse, clamp(sampleCoord, 0.0, 1.0)).rgb;
        s *= illuminationDecay * uWeight;
        godRay += s;
        illuminationDecay *= uDecay;
      }

      // Warm sun tint — constitutional gold
      godRay *= vec3(1.0, 0.9, 0.7);

      gl_FragColor = scene + vec4(godRay * uExposure, 0.0);
    }
  `,
};

/**
 * Project sun world position to screen space and update the god ray pass
 * uniforms. Call once per frame in the tick loop.
 *
 * Handles:
 * - NDC → UV conversion for uSunScreenPos
 * - Disabling when sun is behind the camera (z >= 1.0 in clip space)
 * - Distance-based exposure fade (rays dim as sun moves off-screen)
 */
export function updateGodRayUniforms(
  pass: { uniforms: Record<string, { value: unknown }> },
  sunWorldPos: THREE.Vector3,
  camera: THREE.Camera,
  enabled: boolean,
): void {
  const sunScreen = sunWorldPos.clone().project(camera);
  const inFront = sunScreen.z < 1.0;

  // NDC [-1,1] → UV [0,1]
  (pass.uniforms.uSunScreenPos.value as THREE.Vector2).set(
    (sunScreen.x + 1) / 2,
    (sunScreen.y + 1) / 2,
  );

  pass.uniforms.uEnabled.value = enabled && inFront ? 1.0 : 0.0;

  // Fade exposure as the sun drifts toward screen edges — prevents
  // over-bright streaks when the sun is barely in frame
  const centerDist = Math.sqrt(
    sunScreen.x * sunScreen.x + sunScreen.y * sunScreen.y,
  );
  pass.uniforms.uExposure.value = Math.max(0, 0.25 * (1 - centerDist * 0.5));
}
