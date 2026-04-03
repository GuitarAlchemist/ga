// src/components/PrimeRadiant/shaders/MoebiusPassTSL.ts
// Moebius-style post-processing pass for "Audit Mode" governance visualization.
//
// Strips PBR materials and volumetric glow down to pure structure:
// - Sobel edge detection on depth + luminance for structural outlines
// - Cross-hatching shadows where luminance thresholds encode governance health
// - Noise-displaced outlines for hand-drawn feel
//
// Governance meaning:
//   Clean outlines = stable, healthy governance
//   Dense cross-hatching = crisis, uncertainty
//   Thick edges = high-hierarchy (constitutional) nodes
//   Thin edges = low-hierarchy (persona) nodes
//
// Based on Maxime Heckel's "Moebius-style post-processing" technique.
// Implemented as a Three.js ShaderPass for the existing EffectComposer.

import * as THREE from 'three';

// ── Moebius shader definition (for use with ShaderPass) ──

export const MoebiusShader = {
  uniforms: {
    tDiffuse: { value: null as THREE.Texture | null },
    uResolution: { value: new THREE.Vector2(1920, 1080) },
    uTime: { value: 0 },
    uEnabled: { value: 1.0 }, // 0.0 = passthrough, 1.0 = full Moebius
    uEdgeThreshold: { value: 0.08 }, // Sobel edge sensitivity
    uHatchDensity: { value: 1.0 }, // cross-hatch line density
    uNoiseAmount: { value: 0.003 }, // outline displacement noise
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
    uniform vec2 uResolution;
    uniform float uTime;
    uniform float uEnabled;
    uniform float uEdgeThreshold;
    uniform float uHatchDensity;
    uniform float uNoiseAmount;

    varying vec2 vUv;

    // Simple hash for noise
    float hash21(vec2 p) {
      p = fract(p * vec2(233.34, 851.73));
      p += dot(p, p + 23.45);
      return fract(p.x * p.y);
    }

    // Luminance
    float luma(vec3 c) {
      return dot(c, vec3(0.299, 0.587, 0.114));
    }

    // Sobel edge detection on luminance
    float sobelEdge(vec2 uv, vec2 texel) {
      float tl = luma(texture2D(tDiffuse, uv + vec2(-texel.x, texel.y)).rgb);
      float t  = luma(texture2D(tDiffuse, uv + vec2(0.0, texel.y)).rgb);
      float tr = luma(texture2D(tDiffuse, uv + vec2(texel.x, texel.y)).rgb);
      float l  = luma(texture2D(tDiffuse, uv + vec2(-texel.x, 0.0)).rgb);
      float r  = luma(texture2D(tDiffuse, uv + vec2(texel.x, 0.0)).rgb);
      float bl = luma(texture2D(tDiffuse, uv + vec2(-texel.x, -texel.y)).rgb);
      float b  = luma(texture2D(tDiffuse, uv + vec2(0.0, -texel.y)).rgb);
      float br = luma(texture2D(tDiffuse, uv + vec2(texel.x, -texel.y)).rgb);

      float gx = (tr + 2.0*r + br) - (tl + 2.0*l + bl);
      float gy = (bl + 2.0*b + br) - (tl + 2.0*t + tr);

      return sqrt(gx*gx + gy*gy);
    }

    // Cross-hatching based on luminance thresholds
    // Returns 0.0 (dark hatch) to 1.0 (no hatch)
    float crossHatch(vec2 uv, float luminance) {
      float scale = uHatchDensity * 800.0;
      float hatch = 1.0;

      // Diagonal hatch (darkest shadows)
      if (luminance <= 0.35) {
        float d = mod(uv.x * scale + uv.y * scale, 10.0);
        if (d < 3.0) hatch = 0.0;
      }

      // Vertical hatch (medium shadows)
      if (luminance <= 0.55) {
        float d = mod(uv.x * scale, 10.0);
        if (d < 2.5) hatch *= 0.2;
      }

      // Horizontal hatch (light shadows)
      if (luminance <= 0.75) {
        float d = mod(uv.y * scale * 0.8, 10.0);
        if (d < 2.0) hatch *= 0.4;
      }

      return hatch;
    }

    void main() {
      vec2 texel = 1.0 / uResolution;

      // Noise displacement for hand-drawn feel
      float noise = hash21(vUv * 500.0 + uTime * 0.1);
      vec2 displacedUv = vUv + (noise - 0.5) * uNoiseAmount * uEnabled;

      // Original scene color
      vec4 sceneColor = texture2D(tDiffuse, displacedUv);
      float luminance = luma(sceneColor.rgb);

      // Sobel edge detection
      float edge = sobelEdge(displacedUv, texel);
      float edgeMask = step(uEdgeThreshold, edge);

      // Cross-hatching
      float hatch = crossHatch(displacedUv, luminance);

      // Moebius composite:
      // - Background: warm parchment tint based on luminance
      // - Edges: dark ink lines
      // - Shadows: cross-hatched
      vec3 parchment = vec3(0.95, 0.92, 0.85); // warm paper
      vec3 ink = vec3(0.08, 0.06, 0.04); // dark ink

      // Base: luminance-mapped parchment (brighter areas = cleaner paper)
      vec3 moebiusColor = mix(ink, parchment, luminance * 0.8 + 0.2);

      // Apply cross-hatching (multiply with hatch pattern)
      moebiusColor *= mix(0.3, 1.0, hatch);

      // Apply edge lines (darken where edges detected)
      moebiusColor = mix(moebiusColor, ink, edgeMask * 0.9);

      // Tint edges with original scene color for governance type identification
      // (constitutional = gold edges, policy = copper, persona = blue)
      vec3 tintedEdge = mix(ink, sceneColor.rgb * 0.5, 0.3);
      moebiusColor = mix(moebiusColor, tintedEdge, edgeMask * 0.4);

      // Blend between original scene and Moebius based on uEnabled
      vec3 finalColor = mix(sceneColor.rgb, moebiusColor, uEnabled);

      gl_FragColor = vec4(finalColor, 1.0);
    }
  `,
};

/**
 * Create a Moebius Audit Mode ShaderPass.
 *
 * @returns The ShaderPass + control functions
 */
export function createMoebiusPass(): {
  pass: THREE.ShaderMaterial;
  /** Set enabled amount (0.0 = off, 1.0 = full Moebius) */
  setEnabled: (amount: number) => void;
  /** Update resolution (call on resize) */
  setResolution: (width: number, height: number) => void;
  /** Update time (call per frame) */
  setTime: (time: number) => void;
  /** Get the shader definition for ShaderPass construction */
  shaderDef: typeof MoebiusShader;
} {
  const uniforms = THREE.UniformsUtils.clone(MoebiusShader.uniforms);

  return {
    pass: new THREE.ShaderMaterial({
      uniforms,
      vertexShader: MoebiusShader.vertexShader,
      fragmentShader: MoebiusShader.fragmentShader,
    }),
    shaderDef: MoebiusShader,
    setEnabled: (amount: number) => { uniforms.uEnabled.value = amount; },
    setResolution: (w: number, h: number) => { (uniforms.uResolution.value as THREE.Vector2).set(w, h); },
    setTime: (time: number) => { uniforms.uTime.value = time; },
  };
}
