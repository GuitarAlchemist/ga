/**
 * Mandelbulb
 *
 * True raymarched Mandelbulb distance-field shader. Unlike the splat demo,
 * this renders the fractal surface directly with orbit-trap coloring,
 * finite-difference normals, ambient occlusion, and soft shadows.
 */

import React, { useEffect, useRef } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { EffectComposer } from 'three/examples/jsm/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/examples/jsm/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/examples/jsm/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/examples/jsm/postprocessing/OutputPass.js';

export interface MandelbulbProps {
  width?: number;
  height?: number;
  power?: number;
  iterations?: number;
  quality?: number;
  autoRotate?: boolean;
  /** "crystal" enables glass refraction + chromatic dispersion + iridescence. */
  colorMode?: 'obsidian' | 'gold' | 'ice' | 'spectral' | 'crystal';
  /** Index of refraction for crystal mode. */
  ior?: number;
  /** RGB dispersion strength for crystal mode (chromatic aberration). */
  dispersion?: number;
  /** Bloom strength for the post-pass. */
  bloom?: number;
  /** Filament / fur intensity (0 = off, 1 = dense fuzz). Works in any color mode. */
  fur?: number;
}

const vertexShader = /* glsl */ `
  varying vec2 vUv;

  void main() {
    vUv = uv;
    gl_Position = vec4(position.xy, 0.0, 1.0);
  }
`;

const fragmentShader = /* glsl */ `
  precision highp float;

  varying vec2 vUv;

  uniform vec2 uResolution;
  uniform float uTime;
  uniform vec3 uCameraPos;
  uniform mat3 uCameraMatrix;
  uniform float uFov;
  uniform float uPower;
  uniform int uIterations;
  uniform float uQuality;
  uniform int uColorMode;
  uniform float uIor;          // index of refraction for crystal mode
  uniform float uDispersion;   // chromatic spread (R/B IOR offset)
  uniform float uFur;          // filament/fur strength [0,1]

  const float PI = 3.14159265359;
  const float EPSILON = 0.00085;
  const float FAR_CLIP = 28.0;

  struct DistanceInfo {
    float dist;
    float trap;
    float stripe;
    float radial;
  };

  mat2 rot(float a) {
    float c = cos(a);
    float s = sin(a);
    return mat2(c, -s, s, c);
  }

  DistanceInfo mandelbulb(vec3 p) {
    vec3 z = p;
    float dr = 1.0;
    float r = 0.0;
    float trap = 10.0;
    float stripe = 0.0;

    for (int i = 0; i < 20; i++) {
      if (i >= uIterations) break;

      r = length(z);
      trap = min(trap, abs(r - 1.0) + float(i) * 0.018);
      stripe += exp(-abs(r - 0.82) * 9.0) * 0.06;
      if (r > 4.0) break;

      float theta = acos(clamp(z.z / max(r, 0.0001), -1.0, 1.0));
      float phi = atan(z.y, z.x);
      float zr = pow(r, uPower);
      dr = pow(r, uPower - 1.0) * uPower * dr + 1.0;

      theta *= uPower;
      phi *= uPower;

      z = zr * vec3(
        sin(theta) * cos(phi),
        sin(phi) * sin(theta),
        cos(theta)
      ) + p;
    }

    float d = 0.5 * log(max(r, 0.0001)) * r / dr;
    DistanceInfo info;
    info.dist = d;
    info.trap = trap;
    info.stripe = stripe;
    info.radial = r;
    return info;
  }

  DistanceInfo mapScene(vec3 p) {
    p.xz = rot(sin(uTime * 0.11) * 0.08) * p.xz;
    p.xy = rot(cos(uTime * 0.07) * 0.035) * p.xy;
    return mandelbulb(p);
  }

  vec3 estimateNormal(vec3 p) {
    vec2 e = vec2(EPSILON, 0.0);
    return normalize(vec3(
      mapScene(p + e.xyy).dist - mapScene(p - e.xyy).dist,
      mapScene(p + e.yxy).dist - mapScene(p - e.yxy).dist,
      mapScene(p + e.yyx).dist - mapScene(p - e.yyx).dist
    ));
  }

  float ambientOcclusion(vec3 p, vec3 n) {
    float occ = 0.0;
    float scale = 1.0;
    for (int i = 1; i <= 5; i++) {
      float h = 0.035 * float(i);
      float d = mapScene(p + n * h).dist;
      occ += (h - d) * scale;
      scale *= 0.55;
    }
    return clamp(1.0 - occ * 2.1, 0.0, 1.0);
  }

  float softShadow(vec3 ro, vec3 rd) {
    float res = 1.0;
    float t = 0.035;
    for (int i = 0; i < 42; i++) {
      DistanceInfo h = mapScene(ro + rd * t);
      res = min(res, 9.0 * h.dist / t);
      t += clamp(h.dist, 0.018, 0.30);
      if (res < 0.015 || t > 9.0) break;
    }
    return clamp(res, 0.0, 1.0);
  }

  vec3 palette(float t, float trap, float stripe) {
    if (uColorMode == 1) {
      vec3 dark = vec3(0.12, 0.075, 0.030);
      vec3 mid = vec3(0.88, 0.45, 0.10);
      vec3 light = vec3(1.0, 0.84, 0.42);
      return mix(mix(dark, mid, smoothstep(0.04, 0.42, t)), light, stripe);
    }

    if (uColorMode == 2) {
      vec3 dark = vec3(0.035, 0.070, 0.12);
      vec3 mid = vec3(0.18, 0.68, 0.95);
      vec3 light = vec3(0.83, 0.98, 1.0);
      return mix(mix(dark, mid, smoothstep(0.02, 0.36, t)), light, stripe);
    }

    if (uColorMode == 3) {
      float h = fract(0.58 + t * 1.2 + trap * 0.28);
      vec3 p = abs(fract(vec3(h, h + 0.33, h + 0.67)) * 6.0 - 3.0);
      return clamp(p - 1.0, 0.0, 1.0) * (0.75 + stripe * 0.9);
    }

    vec3 dark = vec3(0.025, 0.018, 0.040);
    vec3 mid = vec3(0.22, 0.20, 0.34);
    vec3 edge = vec3(0.76, 0.57, 1.0);
    return mix(mix(dark, mid, smoothstep(0.02, 0.34, t)), edge, stripe);
  }

  vec3 background(vec3 rd) {
    float h = clamp(rd.y * 0.5 + 0.5, 0.0, 1.0);
    vec3 top = vec3(0.018, 0.025, 0.060);
    vec3 horizon = vec3(0.075, 0.060, 0.100);
    vec3 bg = mix(horizon, top, h);
    float stars = fract(sin(dot(rd.xy * 430.0 + rd.z, vec2(12.9898, 78.233))) * 43758.5453);
    bg += vec3(smoothstep(0.9975, 1.0, stars)) * smoothstep(0.24, 0.9, h) * 0.35;
    return bg;
  }

  // Bright environment for crystal-mode refraction sampling — gives the
  // refracted rays something colourful to catch instead of the murky
  // starfield. A simple two-pole gradient with a virtual sun.
  vec3 envSample(vec3 rd) {
    float h = clamp(rd.y * 0.5 + 0.5, 0.0, 1.0);
    vec3 sky = mix(vec3(0.30, 0.55, 0.92), vec3(0.10, 0.20, 0.55), h * 0.6);
    vec3 ground = vec3(0.18, 0.10, 0.16);
    vec3 col = mix(ground, sky, smoothstep(-0.05, 0.10, rd.y));
    // Virtual sun toward upper right.
    vec3 sunDir = normalize(vec3(0.55, 0.65, 0.40));
    float sd = max(dot(rd, sunDir), 0.0);
    col += vec3(1.0, 0.92, 0.70) * (pow(sd, 32.0) * 1.4 + pow(sd, 4.0) * 0.20);
    // Add ambient stars at low altitudes for sparkle.
    float stars = fract(sin(dot(rd.xy * 680.0 + rd.z, vec2(12.9898, 78.233))) * 43758.5453);
    col += vec3(smoothstep(0.992, 1.0, stars)) * 1.2;
    return col;
  }

  // Fur / filament shell field. Six exponentially-thinning shells spaced
  // along the surface normal sample a 3D hash and accumulate filament
  // density. Modulated by the silhouette factor (rim-leaning) so fuzz
  // reads strongest where the eye expects it — at the outline.
  float furHash3(vec3 p) {
    return fract(sin(dot(p, vec3(127.1, 311.7, 269.5))) * 43758.5453);
  }
  float furField(vec3 p, vec3 n, vec3 viewDir) {
    if (uFur < 0.001) return 0.0;
    float silhouette = pow(1.0 - max(dot(n, viewDir), 0.0), 1.2);
    // Filaments oriented along the normal: sample at increasing offsets,
    // each shell contributes thinner / sparser strands.
    float total = 0.0;
    for (int k = 0; k < 6; k++) {
      float kf = float(k);
      vec3 sp = p + n * (kf * 0.018);
      // High-frequency hash gives fine strand granularity. The threshold
      // tightens with distance from the surface so outer shells produce
      // sparser tips.
      float h = furHash3(floor(sp * (90.0 + kf * 22.0)));
      float strand = step(0.78 + kf * 0.025, h);
      total += strand * (1.0 - kf / 6.0);
    }
    // Anisotropic streak: blend in a sin-based rim band so neighbouring
    // pixels get coherent filament direction instead of noise dust.
    float streak = 0.5 + 0.5 * sin(p.x * 280.0 + p.y * 320.0 + p.z * 240.0);
    streak = smoothstep(0.55, 0.85, streak);
    total = total * 0.65 + streak * silhouette * 0.45;
    return total * silhouette * uFur;
  }

  // March a refraction ray INTO the fractal until it exits, accumulating
  // Beer-Lambert-style internal absorption. A cheaper approximation than
  // a full second pass: starts inside (dist negative under the surface),
  // marches forward, samples the env when the ray re-enters open space.
  vec3 refractMarchInside(vec3 ro, vec3 rd, vec3 tint) {
    float t = 0.02;
    float absorption = 0.0;
    for (int i = 0; i < 32; i++) {
      vec3 p = ro + rd * t;
      DistanceInfo h = mapScene(p);
      // Inside the surface we expect dist < ~0; once dist > small threshold
      // we've exited.
      if (h.dist > 0.012) break;
      absorption += 0.10;
      t += max(abs(h.dist), 0.04);
      if (t > 6.0) break;
    }
    vec3 exitDir = rd;
    vec3 envCol = envSample(exitDir);
    // Beer-Lambert tint: longer internal travel → more color saturation.
    vec3 transmittance = exp(-absorption * (1.0 - tint));
    return envCol * transmittance;
  }

  void main() {
    vec2 screen = (gl_FragCoord.xy * 2.0 - uResolution.xy) / uResolution.y;
    vec3 rd = normalize(uCameraMatrix * normalize(vec3(screen * tan(radians(uFov) * 0.5), -1.0)));
    vec3 ro = uCameraPos;

    float maxSteps = mix(72.0, 160.0, clamp(uQuality, 0.0, 1.0));
    float t = 0.0;
    float glow = 0.0;
    DistanceInfo hit;
    bool found = false;

    for (int i = 0; i < 170; i++) {
      if (float(i) >= maxSteps) break;
      vec3 p = ro + rd * t;
      hit = mapScene(p);
      float threshold = EPSILON * (1.0 + t * 0.20);
      glow += 0.010 / (0.018 + hit.dist * hit.dist * 36.0);
      if (hit.dist < threshold) {
        found = true;
        break;
      }
      t += hit.dist * 0.82;
      if (t > FAR_CLIP) break;
    }

    vec3 color = background(rd);

    if (found) {
      vec3 p = ro + rd * t;
      vec3 n = estimateNormal(p);
      vec3 lightDir = normalize(vec3(-0.42, 0.58, 0.70));
      vec3 fillDir = normalize(vec3(0.58, -0.25, -0.48));
      vec3 viewDir = normalize(ro - p);
      vec3 halfDir = normalize(lightDir + viewDir);

      float diff = max(dot(n, lightDir), 0.0);
      float fill = max(dot(n, fillDir), 0.0) * 0.28;
      float fresnel = pow(1.0 - max(dot(n, viewDir), 0.0), 3.2);
      float spec = pow(max(dot(n, halfDir), 0.0), 72.0);
      float ao = ambientOcclusion(p, n);
      float shadow = softShadow(p + n * 0.018, lightDir);

      float trapTone = clamp(1.0 - hit.trap * 1.55, 0.0, 1.0);
      vec3 base = palette(trapTone, hit.trap, hit.stripe);

      vec3 lit;
      if (uColorMode == 4) {
        // ─── CRYSTAL mode — refraction with chromatic dispersion ───────
        // Three refraction rays at slightly different IORs sample the env;
        // we take only the matching colour channel from each so the
        // surface acts like a prism. Reflection is mixed in via Fresnel
        // for grazing-angle metallic glints. Iridescence + bright specular
        // sit on top.
        float fres = clamp(0.04 + 0.96 * fresnel, 0.0, 1.0);

        float iorR = uIor + uDispersion;
        float iorG = uIor;
        float iorB = uIor - uDispersion;

        vec3 refrR = refract(rd, n, 1.0 / iorR);
        vec3 refrG = refract(rd, n, 1.0 / iorG);
        vec3 refrB = refract(rd, n, 1.0 / iorB);

        vec3 tint = base * 1.4 + vec3(0.10);
        vec3 cR = refractMarchInside(p - n * 0.015, refrR, tint);
        vec3 cG = refractMarchInside(p - n * 0.015, refrG, tint);
        vec3 cB = refractMarchInside(p - n * 0.015, refrB, tint);
        vec3 refracted = vec3(cR.r, cG.g, cB.b);

        // Reflection: bounce ray off the surface, sample env directly.
        vec3 reflDir = reflect(rd, n);
        vec3 reflected = envSample(reflDir);

        // Iridescence — thin-film rainbow shift driven by view angle.
        float iridT = pow(1.0 - max(dot(n, viewDir), 0.0), 1.5);
        vec3 iridescence = 0.5 + 0.5 * cos(iridT * 14.0 + vec3(0.0, 2.094, 4.188));
        iridescence = mix(vec3(1.0), iridescence, fres * 0.55);

        // Mix refraction (bulk) ↔ reflection (grazing), modulated by base
        // tone so deep-fold cavities still read as colourful.
        vec3 glass = mix(refracted, reflected, fres) * iridescence;
        glass += base * 0.15 * (1.0 - fres);          // a hint of body colour
        glass += vec3(1.0, 0.96, 0.86) * spec * shadow * 0.65;
        glass *= 0.85 + ao * 0.30;
        lit = glass;
      } else {
        lit = base * (0.16 + ao * 0.24 + diff * shadow * 1.35 + fill);
        lit += vec3(1.0, 0.88, 0.68) * spec * shadow * 0.55;
        lit += vec3(0.60, 0.76, 1.0) * fresnel * (0.42 + trapTone * 0.24);
      }

      // Filaments / fur — sits on top of the lit colour, tinted by the
      // base palette so fur stays in keeping with the colour mode.
      float fur = furField(p, n, viewDir);
      vec3 furTint = mix(vec3(1.0, 0.96, 0.85), base * 1.6 + 0.18, 0.55);
      lit += furTint * fur * (0.65 + diff * shadow * 0.35);

      float fog = 1.0 - exp(-t * t * 0.006);
      color = mix(lit, background(rd), fog);
    } else {
      color += vec3(0.10, 0.12, 0.28) * min(glow * 0.25, 0.55);
    }

    color += vec3(0.08, 0.10, 0.22) * min(glow * 0.035, 0.30);
    color = pow(color, vec3(0.4545));

    vec2 uv = vUv * 2.0 - 1.0;
    color *= 1.0 - dot(uv, uv) * 0.16;

    gl_FragColor = vec4(color, 1.0);
  }
`;

const colorModeIndex = (mode: MandelbulbProps['colorMode']) => {
  if (mode === 'gold') return 1;
  if (mode === 'ice') return 2;
  if (mode === 'spectral') return 3;
  if (mode === 'crystal') return 4;
  return 0;
};

const Mandelbulb: React.FC<MandelbulbProps> = ({
  width,
  height,
  power = 8,
  iterations = 12,
  quality = 0.72,
  autoRotate = true,
  colorMode = 'obsidian',
  ior = 1.45,
  dispersion = 0.04,
  bloom = 0.55,
  fur = 0,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    const initialWidth = width ?? container.clientWidth ?? 1280;
    const initialHeight = height ?? container.clientHeight ?? 720;

    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(initialWidth, initialHeight);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.4));
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    container.appendChild(renderer.domElement);

    const scene = new THREE.Scene();
    const quadCamera = new THREE.OrthographicCamera(-1, 1, 1, -1, 0, 1);
    const orbitCamera = new THREE.PerspectiveCamera(48, initialWidth / initialHeight, 0.1, 100);
    orbitCamera.position.set(0.2, 0.25, 4.15);

    const controls = new OrbitControls(orbitCamera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.055;
    controls.minDistance = 2.4;
    controls.maxDistance = 8.0;
    controls.autoRotate = autoRotate;
    controls.autoRotateSpeed = 0.42;
    controls.target.set(0, 0, 0);

    const uniforms = {
      uResolution: { value: new THREE.Vector2(initialWidth, initialHeight) },
      uTime: { value: 0 },
      uCameraPos: { value: orbitCamera.position.clone() },
      uCameraMatrix: { value: new THREE.Matrix3() },
      uFov: { value: orbitCamera.fov },
      uPower: { value: power },
      uIterations: { value: iterations },
      uQuality: { value: quality },
      uColorMode: { value: colorModeIndex(colorMode) },
      uIor: { value: ior },
      uDispersion: { value: dispersion },
      uFur: { value: fur },
    };

    const material = new THREE.ShaderMaterial({
      uniforms,
      vertexShader,
      fragmentShader,
    });
    const quad = new THREE.Mesh(new THREE.PlaneGeometry(2, 2), material);
    scene.add(quad);

    // Post-processing — UnrealBloom amplifies the dispersion glints and
    // crystal highlights without washing out the surface lighting in the
    // non-crystal modes (high threshold so only true highlights bloom).
    const composer = new EffectComposer(renderer);
    composer.setSize(initialWidth, initialHeight);
    composer.setPixelRatio(Math.min(window.devicePixelRatio, 1.4));
    composer.addPass(new RenderPass(scene, quadCamera));
    const bloomPass = new UnrealBloomPass(
      new THREE.Vector2(initialWidth, initialHeight),
      bloom, // strength
      0.65,  // radius
      0.86,  // threshold
    );
    composer.addPass(bloomPass);
    composer.addPass(new OutputPass());

    const cameraMatrix = new THREE.Matrix3();
    const clock = new THREE.Clock();
    let frame = 0;

    const animate = () => {
      const elapsed = clock.getElapsedTime();
      controls.autoRotate = autoRotate;
      controls.update();

      cameraMatrix.setFromMatrix4(orbitCamera.matrixWorld);
      uniforms.uTime.value = elapsed;
      uniforms.uCameraPos.value.copy(orbitCamera.position);
      uniforms.uCameraMatrix.value.copy(cameraMatrix);
      uniforms.uFov.value = orbitCamera.fov;
      uniforms.uPower.value = power;
      uniforms.uIterations.value = iterations;
      uniforms.uQuality.value = quality;
      uniforms.uColorMode.value = colorModeIndex(colorMode);
      uniforms.uIor.value = ior;
      uniforms.uDispersion.value = dispersion;
      uniforms.uFur.value = fur;
      bloomPass.strength = bloom;

      composer.render();
      frame = requestAnimationFrame(animate);
    };
    animate();

    const onResize = () => {
      const currentWidth = container.clientWidth;
      const currentHeight = container.clientHeight;
      if (currentWidth === 0 || currentHeight === 0) return;
      renderer.setSize(currentWidth, currentHeight, false);
      composer.setSize(currentWidth, currentHeight);
      bloomPass.setSize(currentWidth, currentHeight);
      uniforms.uResolution.value.set(currentWidth, currentHeight);
      orbitCamera.aspect = currentWidth / currentHeight;
      orbitCamera.updateProjectionMatrix();
    };
    const resizeObserver = new ResizeObserver(onResize);
    resizeObserver.observe(container);

    return () => {
      cancelAnimationFrame(frame);
      resizeObserver.disconnect();
      controls.dispose();
      composer.dispose();
      quad.geometry.dispose();
      material.dispose();
      renderer.dispose();
      if (renderer.domElement.parentNode === container) {
        container.removeChild(renderer.domElement);
      }
    };
  }, [autoRotate, colorMode, height, iterations, power, quality, width, ior, dispersion, bloom, fur]);

  const style: React.CSSProperties = width !== undefined && height !== undefined
    ? { width, height, overflow: 'hidden' }
    : { width: '100%', height: '100%', overflow: 'hidden' };

  return <div ref={containerRef} style={style} />;
};

export default Mandelbulb;
