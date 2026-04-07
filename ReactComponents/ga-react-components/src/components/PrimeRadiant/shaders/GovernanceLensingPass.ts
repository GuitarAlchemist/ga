// src/components/PrimeRadiant/shaders/GovernanceLensingPass.ts
// GLSL post-processing pass for gravitational lensing around crisis nodes.
//
// When a governance node enters crisis state (health='error' or hexavalent
// C/contradictory), the visual space around it warps — background stars and
// neighboring nodes bend toward it, like gravitational lensing near a compact
// massive object. Makes crisis states impossible to ignore without any text.
//
// Physics model: simplified Einstein ring / weak gravitational lensing.
// For each pixel, accumulate radial distortion from up to MAX_LENSES sources.
// Distortion = direction * (intensity / distance^2) with smooth falloff at
// the radius boundary (smoothstep avoids hard edges).
//
// At low intensity: subtle heat-shimmer / atmospheric refraction.
// At full intensity (prolonged health='error'): dramatic space-warp with
// chromatic fringing at the distortion edges.
//
// Integration with ForceRadiant.tsx:
// 1. In the animation loop, project crisis node world positions to screen space:
//      const ndc = node.position.clone().project(camera);
//      const screenPos = new THREE.Vector2((ndc.x + 1) * 0.5, (ndc.y + 1) * 0.5);
// 2. Filter for nodes with health='error' or hexavalent status='contradictory'
// 3. Update uniforms: pass.uniforms.uLensPositions.value = packedFloat32Array
// 4. Add this pass to the post-processing chain (after bloom/caustics/dispersion,
//    before Moebius) — lensing should warp the already-lit scene, and Moebius
//    edge detection should pick up the warped geometry.
//
// Example integration snippet (in ForceRadiant.tsx init):
//   import { GovernanceLensingShader } from './shaders/GovernanceLensingPass';
//   const lp = new ShaderPass(GovernanceLensingShader);
//   lp.uniforms.uIntensity.value = 0.0;
//   lp.enabled = false;
//   fg.postProcessingComposer().addPass(lp);
//   lensingPassRef.current = lp;
//
// Example integration snippet (in tick loop):
//   const crisisNodes = nodesArray.filter(n =>
//     n.health?.status === 'error' || n.hexavalent === 'contradictory'
//   );
//   if (crisisNodes.length > 0 && lensingPassRef.current) {
//     lensingPassRef.current.enabled = true;
//     const positions = new Float32Array(MAX_LENSES * 2);
//     const intensities = new Float32Array(MAX_LENSES);
//     const radii = new Float32Array(MAX_LENSES);
//     crisisNodes.slice(0, MAX_LENSES).forEach((n, i) => {
//       const ndc = n.group.position.clone().project(camera);
//       positions[i * 2]     = (ndc.x + 1) * 0.5;
//       positions[i * 2 + 1] = (ndc.y + 1) * 0.5;
//       intensities[i] = n.health?.status === 'error' ? 0.8 : 0.4;
//       radii[i] = 0.15; // screen-space radius
//     });
//     updateLensingUniforms(lensingPassRef.current, crisisNodes.length, positions, intensities, radii);
//   } else if (lensingPassRef.current) {
//     lensingPassRef.current.enabled = false;
//   }

import * as THREE from 'three';

/** Maximum simultaneous lensing sources */
export const MAX_LENSES = 8;

/** Descriptor for a single lensing source (crisis node projected to screen) */
export interface LensingSource {
  /** Normalized screen coordinates [0,1] — (0,0) = bottom-left */
  screenPos: THREE.Vector2;
  /** Distortion strength: 0.0 (none) to 1.0 (full black-hole warp) */
  intensity: number;
  /** Screen-space radius of the effect (0.05 = small, 0.25 = large) */
  radius: number;
}

export const GovernanceLensingShader = {
  uniforms: {
    tDiffuse: { value: null as THREE.Texture | null },
    uResolution: { value: new THREE.Vector2(1920, 1080) },
    uTime: { value: 0 },
    // Packed lens data — flat Float32Arrays for GPU-friendly transfer
    uLensPositions: { value: new Float32Array(MAX_LENSES * 2) },   // vec2[MAX_LENSES]
    uLensIntensities: { value: new Float32Array(MAX_LENSES) },     // float[MAX_LENSES]
    uLensRadii: { value: new Float32Array(MAX_LENSES) },           // float[MAX_LENSES]
    uLensCount: { value: 0 },
    // Global intensity multiplier (0 = disabled, 1 = full effect)
    uIntensity: { value: 0.0 },
    // Chromatic fringe amount at distortion edges
    uChromaticFringe: { value: 0.3 },
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
    uniform float uLensPositions[${MAX_LENSES * 2}];
    uniform float uLensIntensities[${MAX_LENSES}];
    uniform float uLensRadii[${MAX_LENSES}];
    uniform int uLensCount;
    uniform float uIntensity;
    uniform float uChromaticFringe;

    varying vec2 vUv;

    // Simple hash for heat-shimmer noise
    float hash21(vec2 p) {
      p = fract(p * vec2(233.34, 851.73));
      p += dot(p, p + 23.45);
      return fract(p.x * p.y);
    }

    void main() {
      if (uIntensity <= 0.001 || uLensCount <= 0) {
        gl_FragColor = texture2D(tDiffuse, vUv);
        return;
      }

      // Aspect ratio correction — lensing circles should be circular, not elliptical
      float aspect = uResolution.x / uResolution.y;

      // Accumulate UV distortion from all active lens sources
      vec2 totalDistortion = vec2(0.0);
      float totalWarp = 0.0; // track warp magnitude for chromatic fringe

      for (int i = 0; i < ${MAX_LENSES}; i++) {
        if (i >= uLensCount) break;

        // Unpack lens center (normalized screen coords)
        vec2 lensCenter = vec2(uLensPositions[i * 2], uLensPositions[i * 2 + 1]);
        float intensity = uLensIntensities[i] * uIntensity;
        float radius = uLensRadii[i];

        if (intensity <= 0.001 || radius <= 0.001) continue;

        // Direction from pixel to lens center (aspect-corrected for distance calc)
        vec2 delta = vUv - lensCenter;
        vec2 deltaAspect = vec2(delta.x * aspect, delta.y);
        float dist = length(deltaAspect);

        // Normalized distance within the lens radius
        float normDist = dist / radius;

        // Smooth falloff: 1.0 at center, 0.0 beyond radius
        // Uses smoothstep for soft edges — no hard ring boundary
        float falloff = 1.0 - smoothstep(0.0, 1.0, normDist);

        // Gravitational lensing distortion:
        // Near the center: strong radial push outward (Einstein ring effect)
        // Falls off with inverse distance (clamped to avoid singularity)
        float clampedDist = max(normDist, 0.1); // prevent division by near-zero
        float lensingStrength = intensity * 0.02 / (clampedDist * clampedDist);

        // Apply falloff to keep distortion bounded within radius
        lensingStrength *= falloff;

        // Heat shimmer — subtle animated noise displacement at low intensity
        float shimmer = hash21(vUv * 200.0 + uTime * 0.5 + float(i) * 7.0);
        float shimmerAmount = intensity * falloff * 0.002;
        vec2 shimmerOffset = vec2(
          sin(shimmer * 6.2832 + uTime * 2.0),
          cos(shimmer * 6.2832 + uTime * 1.7)
        ) * shimmerAmount;

        // Direction: push pixels radially away from lens center
        vec2 direction = normalize(delta + vec2(0.0001)); // tiny epsilon avoids zero-length
        vec2 distortion = direction * lensingStrength + shimmerOffset;

        totalDistortion += distortion;
        totalWarp += lensingStrength;
      }

      // Clamp total distortion to prevent extreme warping artifacts
      float maxDistort = 0.08;
      float distortMag = length(totalDistortion);
      if (distortMag > maxDistort) {
        totalDistortion *= maxDistort / distortMag;
        distortMag = maxDistort;
      }

      // Sample with distortion
      vec2 distortedUv = vUv + totalDistortion;

      // Chromatic fringe at distortion edges — split RGB channels slightly
      // along the distortion direction for a prismatic edge effect.
      // Stronger where distortion is stronger (totalWarp).
      float fringeAmount = uChromaticFringe * min(totalWarp * 5.0, 1.0) * uIntensity;
      vec2 fringeDir = normalize(totalDistortion + vec2(0.0001));
      float fringeOffset = fringeAmount * 0.004;

      float r = texture2D(tDiffuse, distortedUv + fringeDir * fringeOffset).r;
      float g = texture2D(tDiffuse, distortedUv).g;
      float b = texture2D(tDiffuse, distortedUv - fringeDir * fringeOffset).b;
      float a = texture2D(tDiffuse, distortedUv).a;

      // Where there's no distortion, skip fringe — use straight sample
      vec4 straight = texture2D(tDiffuse, distortedUv);
      float frIngeMix = smoothstep(0.0, 0.005, distortMag);
      vec3 finalColor = mix(straight.rgb, vec3(r, g, b), fringeAmount > 0.001 ? frIngeMix : 0.0);

      gl_FragColor = vec4(finalColor, a);
    }
  `,
};

/**
 * Helper: pack LensingSource[] into the flat uniform arrays.
 * Call this per-frame in the tick loop when crisis nodes are active.
 */
export function updateLensingUniforms(
  pass: { uniforms: Record<string, { value: unknown }> },
  count: number,
  positions: Float32Array,
  intensities: Float32Array,
  radii: Float32Array,
): void {
  pass.uniforms.uLensCount.value = Math.min(count, MAX_LENSES);
  pass.uniforms.uLensPositions.value = positions;
  pass.uniforms.uLensIntensities.value = intensities;
  pass.uniforms.uLensRadii.value = radii;
}

/**
 * Helper: convert LensingSource[] descriptors to flat arrays and update the pass.
 * Convenience wrapper when you have structured source objects.
 */
export function updateLensingSources(
  pass: { uniforms: Record<string, { value: unknown }> },
  sources: LensingSource[],
): void {
  const n = Math.min(sources.length, MAX_LENSES);
  const positions = new Float32Array(MAX_LENSES * 2);
  const intensities = new Float32Array(MAX_LENSES);
  const radii = new Float32Array(MAX_LENSES);

  for (let i = 0; i < n; i++) {
    positions[i * 2] = sources[i].screenPos.x;
    positions[i * 2 + 1] = sources[i].screenPos.y;
    intensities[i] = sources[i].intensity;
    radii[i] = sources[i].radius;
  }

  updateLensingUniforms(pass, n, positions, intensities, radii);
}
