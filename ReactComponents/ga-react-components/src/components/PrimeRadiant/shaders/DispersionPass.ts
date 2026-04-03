// src/components/PrimeRadiant/shaders/DispersionPass.ts
// GLSL post-processing pass for hexavalent chromatic dispersion.
//
// 6-channel spectral split mapping 1:1 to hexavalent logic:
//   T (True)          → Gold    — IOR ~1.0 (passes straight, minimal offset)
//   P (Probable)      → Amber   — slight offset toward True
//   U (Unknown)       → Gray    — medium scatter, diffuse offset
//   D (Doubtful)      → Russet  — slight offset toward False
//   F (False)         → Red     — maximum offset (bends furthest from truth)
//   C (Contradictory) → Purple  — NEGATIVE offset (bends backward)
//
// "Governance Spectroscopy" — shoot a directive through governance and
// read the hexavalent output spectrum as chromatic dispersion.
//
// Based on Maxime Heckel's refraction/dispersion technique.

import * as THREE from 'three';

export const DispersionShader = {
  uniforms: {
    tDiffuse: { value: null as THREE.Texture | null },
    uSpread: { value: 0.0 },       // 0 = off, 1 = maximum dispersion
    uTime: { value: 0 },
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
    uniform float uSpread;
    uniform float uTime;
    uniform vec2 uResolution;
    varying vec2 vUv;

    // Hexavalent channel offsets (normalized UV displacement)
    // True = no offset, False = max offset, Contradictory = reversed
    vec2 channelOffset(int channel, float spread) {
      float angle;
      float magnitude;

      if (channel == 0) {
        // T (True) — gold — minimal offset (straight through)
        angle = 0.0;
        magnitude = 0.0;
      } else if (channel == 1) {
        // P (Probable) — amber — slight offset
        angle = 0.5236; // 30 degrees
        magnitude = spread * 0.2;
      } else if (channel == 2) {
        // U (Unknown) — gray — medium scatter
        angle = 1.5708; // 90 degrees (perpendicular — uncertainty)
        magnitude = spread * 0.4;
      } else if (channel == 3) {
        // D (Doubtful) — russet — toward False
        angle = 2.618; // 150 degrees
        magnitude = spread * 0.6;
      } else if (channel == 4) {
        // F (False) — red — maximum offset
        angle = 3.1416; // 180 degrees (opposite of True)
        magnitude = spread * 1.0;
      } else {
        // C (Contradictory) — purple — NEGATIVE/reversed
        angle = -0.7854; // -45 degrees (impossible direction)
        magnitude = spread * 0.8;
      }

      // Subtle animation — channels drift slightly
      float drift = sin(uTime * 0.3 + float(channel) * 1.047) * spread * 0.05;

      return vec2(cos(angle), sin(angle)) * (magnitude + drift) * 0.01;
    }

    // Hexavalent channel colors
    vec3 channelColor(int channel) {
      if (channel == 0) return vec3(1.0, 0.85, 0.3);   // T — gold
      if (channel == 1) return vec3(1.0, 0.7, 0.2);    // P — amber
      if (channel == 2) return vec3(0.6, 0.6, 0.6);    // U — gray
      if (channel == 3) return vec3(0.7, 0.4, 0.2);    // D — russet
      if (channel == 4) return vec3(0.9, 0.2, 0.15);   // F — red
      return vec3(0.6, 0.3, 0.8);                       // C — purple
    }

    void main() {
      if (uSpread <= 0.001) {
        gl_FragColor = texture2D(tDiffuse, vUv);
        return;
      }

      // Sample scene at 6 hexavalent offsets
      vec3 result = vec3(0.0);
      float totalWeight = 0.0;

      for (int i = 0; i < 6; i++) {
        vec2 offset = channelOffset(i, uSpread);
        vec3 sample = texture2D(tDiffuse, vUv + offset).rgb;
        vec3 tint = channelColor(i);

        // Weight: True channel gets most weight, others less
        float weight = (i == 0) ? 2.0 : 1.0;

        result += sample * tint * weight;
        totalWeight += weight;
      }

      result /= totalWeight;

      // Blend with original scene based on spread amount
      vec3 original = texture2D(tDiffuse, vUv).rgb;
      vec3 final = mix(original, result, uSpread * 0.8);

      gl_FragColor = vec4(final, 1.0);
    }
  `,
};
