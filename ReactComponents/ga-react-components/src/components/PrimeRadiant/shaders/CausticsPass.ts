// src/components/PrimeRadiant/shaders/CausticsPass.ts
// GLSL post-processing pass for animated caustic light patterns.
//
// Governance meaning: constitutional authority concentrates as bright
// caustic lines — where governance light focuses through refractive
// institutional structures. Based on Maxime Heckel's caustics technique.
//
// The caustics are computed as overlapping sine-wave interference patterns
// projected onto the scene, modulated by scene luminance (bright areas
// receive more caustics, dark areas less — authority concentrates on
// visible/active governance artifacts).

import * as THREE from 'three';

export const CausticsShader = {
  uniforms: {
    tDiffuse: { value: null as THREE.Texture | null },
    uTime: { value: 0 },
    uIntensity: { value: 0.0 },  // 0 = off, 1 = full caustics
    uScale: { value: 3.0 },      // caustic pattern scale
    uSpeed: { value: 0.8 },      // animation speed
    uWarmth: { value: 0.7 },     // 0 = white caustics, 1 = gold (constitutional)
    uResolution: { value: new THREE.Vector2(1920, 1080) },
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
    uniform float uTime;
    uniform float uIntensity;
    uniform float uScale;
    uniform float uSpeed;
    uniform float uWarmth;
    uniform vec2 uResolution;
    varying vec2 vUv;

    // Caustic pattern — overlapping sine wave interference
    // Two wave grids at different angles create Voronoi-like caustic cells
    float causticPattern(vec2 uv, float time) {
      float s = uScale;
      float t = time * uSpeed;

      // Layer 1: diagonal wave grid
      float w1 = sin(uv.x * s * 3.7 + t) * cos(uv.y * s * 2.3 - t * 0.7);
      float w2 = sin(uv.y * s * 4.1 - t * 0.5) * cos(uv.x * s * 1.9 + t * 0.3);

      // Layer 2: rotated grid for interference
      vec2 ruv = vec2(
        uv.x * 0.866 - uv.y * 0.5,
        uv.x * 0.5 + uv.y * 0.866
      );
      float w3 = sin(ruv.x * s * 3.3 + t * 1.1) * cos(ruv.y * s * 2.7 - t * 0.4);
      float w4 = sin(ruv.y * s * 3.9 - t * 0.8) * cos(ruv.x * s * 2.1 + t * 0.6);

      // Combine: interference creates bright caustic lines at wave peaks
      float combined = (w1 + w2 + w3 + w4) * 0.25; // normalize to [-1, 1]

      // Sharpen: caustics are bright lines, not smooth gradients
      float caustic = pow(max(combined, 0.0), 2.0);

      // Add fine detail ripples
      float detail = sin(uv.x * s * 12.0 + t * 2.0) * sin(uv.y * s * 11.3 - t * 1.5) * 0.15;
      caustic += max(detail, 0.0);

      return caustic;
    }

    void main() {
      vec4 scene = texture2D(tDiffuse, vUv);

      if (uIntensity <= 0.001) {
        gl_FragColor = scene;
        return;
      }

      // Aspect-corrected UVs for caustic pattern
      vec2 aspect = vec2(uResolution.x / uResolution.y, 1.0);
      float caustic = causticPattern(vUv * aspect, uTime);

      // Modulate by scene luminance — caustics appear more on brighter areas
      float luma = dot(scene.rgb, vec3(0.299, 0.587, 0.114));
      float lumaFactor = smoothstep(0.05, 0.4, luma); // dark areas get less

      // Caustic color: warm gold (constitutional) to white
      vec3 causticColor = mix(
        vec3(1.0, 1.0, 1.0),                    // white
        vec3(1.0, 0.85, 0.4),                    // warm gold
        uWarmth
      );

      // Apply caustics additively, modulated by intensity and scene brightness
      vec3 result = scene.rgb + causticColor * caustic * uIntensity * lumaFactor;

      gl_FragColor = vec4(result, scene.a);
    }
  `,
};
