/**
 * FluffyAnimals — bezier-blade fur on animal-shaped ellipsoid clusters.
 *
 * Reuses the fluffy-grass blade idiom (PlaneGeometry segmented into 6 strips,
 * vertex-shader bend curve, alpha-test discard for the silhouette) and
 * applies it to fur instead of grass. Each animal is built from a small
 * collection of ellipsoid "parts" (body, head, ears, tail). Fur instances
 * are sampled on the ellipsoid surfaces and oriented so each blade's local
 * +Y matches the surface normal — so the fur sticks out radially.
 *
 * Animals: Bear, Sheep, Fox, Bunny, Cat. Differ in body shape, fur length,
 * fur color, and which parts get fur (a fox's nose stays smooth, a sheep's
 * head stays smooth, etc.).
 */

import React, { useEffect, useRef } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { EffectComposer } from 'three/examples/jsm/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/examples/jsm/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/examples/jsm/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/examples/jsm/postprocessing/OutputPass.js';
import { Box } from '@mui/material';

export interface FluffyAnimalsProps {
  width?: number;
  height?: number;
  /** Multiplier applied to every animal's fur density (0.3..2). */
  furDensity?: number;
  /** Multiplier on fur blade length. */
  furLength?: number;
  /** Wind sway strength on fur tips. */
  windStrength?: number;
  autoRotate?: boolean;
}

const NOISE_GLSL = /* glsl */ `
  vec2 hash2(vec2 p) {
    p = vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)));
    return -1.0 + 2.0 * fract(sin(p) * 43758.5453123);
  }
  float vnoise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    vec2 a = hash2(i);
    vec2 b = hash2(i + vec2(1.0, 0.0));
    vec2 c = hash2(i + vec2(0.0, 1.0));
    vec2 d = hash2(i + vec2(1.0, 1.0));
    return mix(
      mix(dot(a, f), dot(b, f - vec2(1.0, 0.0)), u.x),
      mix(dot(c, f - vec2(0.0, 1.0)), dot(d, f - vec2(1.0, 1.0)), u.x),
      u.y);
  }
`;

// ─── Fur shader ─────────────────────────────────────────────────────────
// One material shared by every fur InstancedMesh; per-animal color tone
// flows through aBaseColor / aTipColor instance attributes.

const FUR_VERTEX = /* glsl */ `
  ${NOISE_GLSL}

  uniform float uTime;
  uniform float uWindStrength;

  attribute float aRandom;
  attribute float aTwist;
  attribute float aLength;     // 0..1 multiplier on blade length
  attribute vec3  aBaseColor;
  attribute vec3  aTipColor;

  varying vec2 vUv;
  varying vec3 vBaseColor;
  varying vec3 vTipColor;
  varying vec3 vNormalW;
  varying float vRandom;

  void main() {
    vUv = uv;
    vRandom = aRandom;
    vBaseColor = aBaseColor;
    vTipColor = aTipColor;

    // Base anchor in world space (instanceMatrix already places + orients
    // the blade so local +Y matches the surface normal).
    vec4 anchor4 = modelMatrix * instanceMatrix * vec4(0.0, 0.0, 0.0, 1.0);
    vec3 anchor = anchor4.xyz;

    // Stretch the blade by aLength (per-instance variation).
    vec3 p = position;
    p.y *= aLength;

    // Per-blade twist around local Y (the surface normal axis).
    float c = cos(aTwist);
    float s = sin(aTwist);
    vec3 rotated = vec3(c * p.x - s * p.z, p.y, s * p.x + c * p.z);

    // Local-frame curl: the blade arcs forward in local +X for natural
    // "lay" — like fur that's been lightly groomed.
    float t = clamp(uv.y, 0.0, 1.0);
    float curve = t * t;
    rotated.x += curve * 0.18 * (0.5 + aRandom * 0.5);
    rotated.y -= curve * 0.05;

    vec4 worldPos = modelMatrix * instanceMatrix * vec4(rotated, 1.0);

    // World-space wind sway on the tip — small breeze that varies
    // spatially via vnoise so neighbouring blades sway coherently.
    float gust = vnoise(anchor.xz * 0.6 + vec2(uTime * 0.4, uTime * 0.25));
    worldPos.x += (gust * 0.6 + 0.4) * uWindStrength * curve * 0.16;
    worldPos.z += sin(uTime * 0.7 + anchor.y * 1.4 + aRandom * 6.28) * uWindStrength * curve * 0.10;

    // Normal: blade plane normal rotated by twist + transformed to world.
    vec3 nLocal = normalize(vec3(s, 0.4, c));
    vNormalW = normalize((modelMatrix * instanceMatrix * vec4(nLocal, 0.0)).xyz);

    gl_Position = projectionMatrix * viewMatrix * worldPos;
  }
`;

const FUR_FRAGMENT = /* glsl */ `
  uniform vec3 uSunDir;
  uniform vec3 uSunColor;
  uniform vec3 uAmbient;

  varying vec2 vUv;
  varying vec3 vBaseColor;
  varying vec3 vTipColor;
  varying vec3 vNormalW;
  varying float vRandom;

  void main() {
    // Blade taper — silhouette via discard so depth-write stays correct.
    float halfW = abs(vUv.x - 0.5);
    float taper = 1.0 - vUv.y * 0.85;
    if (halfW > taper * 0.5) discard;

    // Color from base to tip with slight per-blade variation.
    float ao = pow(vUv.y, 1.3);
    vec3 col = mix(vBaseColor, vTipColor, ao + (vRandom - 0.5) * 0.10);

    // Lambert shading using a wrap term so backsides aren't black.
    vec3 N = normalize(vNormalW);
    vec3 L = normalize(uSunDir);
    float lit = max(dot(N, L), 0.0);
    float wrap = lit * 0.55 + 0.45;

    // Backlit translucency at low sun angles for that fluffy halo.
    float backLit = pow(1.0 - lit, 2.0) * smoothstep(0.0, 0.4, uSunDir.y);
    col += backLit * vTipColor * 0.30 * ao;

    col = col * (uAmbient * 0.55 + uSunColor * wrap);
    gl_FragColor = vec4(col, 1.0);
  }
`;

// ─── Helpers ────────────────────────────────────────────────────────────

const yAxis = new THREE.Vector3(0, 1, 0);

// ─── Fur pattern motifs ─────────────────────────────────────────────────
// Each pattern is evaluated at a fur instance's *local* position (relative
// to the part centre, normalised by the radii) and returns a 0..1 mix
// factor between the part's primary colours (body/tip) and the pattern's
// secondary colours (bodyB/tipB). Result is baked into the per-instance
// aBaseColor / aTipColor attributes so no per-fragment sampling is
// needed at render time.

type Pattern =
  | { kind: 'solid' }
  | { kind: 'belly';   threshold: number; bodyB: number; tipB: number }                          // white-belly fox
  | { kind: 'stripes'; freq: number;     axis: 'x' | 'y' | 'z'; jitter?: number; sharp?: number; bodyB: number; tipB: number }  // tiger / zebra
  | { kind: 'patches'; scale: number;    threshold: number;     bodyB: number; tipB: number }    // cow / dalmatian
  | { kind: 'sock';    threshold: number; axis: 'y';            bodyB: number; tipB: number };  // dark feet/socks

interface Part {
  c: [number, number, number];
  r: [number, number, number];
  body: number;
  tip?: number;
  fur?: boolean;
  furLen?: number;
  furDen?: number;
  pattern?: Pattern;
}

interface AnimalDef {
  name: string;
  parts: Part[];
  pos: [number, number, number];
  yaw?: number;
}

// CPU 3D hash for the patches pattern.
const hash3D = (x: number, y: number, z: number): number => {
  const v = Math.sin(x * 12.9898 + y * 78.233 + z * 37.719) * 43758.5453;
  return v - Math.floor(v);
};

// Smoothed 3D noise so patches blob rather than hard-grid.
const blob3D = (x: number, y: number, z: number): number => {
  const ix = Math.floor(x), iy = Math.floor(y), iz = Math.floor(z);
  const fx = x - ix, fy = y - iy, fz = z - iz;
  const ux = fx * fx * (3 - 2 * fx);
  const uy = fy * fy * (3 - 2 * fy);
  const uz = fz * fz * (3 - 2 * fz);
  let s = 0;
  s += hash3D(ix,     iy,     iz)     * (1 - ux) * (1 - uy) * (1 - uz);
  s += hash3D(ix + 1, iy,     iz)     * ux       * (1 - uy) * (1 - uz);
  s += hash3D(ix,     iy + 1, iz)     * (1 - ux) * uy       * (1 - uz);
  s += hash3D(ix + 1, iy + 1, iz)     * ux       * uy       * (1 - uz);
  s += hash3D(ix,     iy,     iz + 1) * (1 - ux) * (1 - uy) * uz;
  s += hash3D(ix + 1, iy,     iz + 1) * ux       * (1 - uy) * uz;
  s += hash3D(ix,     iy + 1, iz + 1) * (1 - ux) * uy       * uz;
  s += hash3D(ix + 1, iy + 1, iz + 1) * ux       * uy       * uz;
  return s;
};

// Evaluate a pattern at a local point on the part's surface. lx/ly/lz are
// in local space (unrotated) relative to the part centre, in WORLD units
// (not normalised), so jitter and freq are in metres.
function evalPattern(p: Pattern | undefined, lx: number, ly: number, lz: number, ry: number): number {
  if (!p || p.kind === 'solid') return 0;
  if (p.kind === 'belly') {
    // Below the part's lower threshold (in normalised Y) gets the
    // secondary colour. Smoothstep across a small band so the seam
    // isn't a hard line.
    const yNorm = ly / ry;
    const t = (p.threshold - yNorm) * 4 + 0.5;
    return Math.max(0, Math.min(1, t));
  }
  if (p.kind === 'sock') {
    // Like belly but reversed sense — secondary colour appears below
    // threshold (feet) AND blends quickly. Used on legs.
    const yNorm = ly / ry;
    return yNorm < p.threshold ? 1 : 0;
  }
  if (p.kind === 'stripes') {
    // Sin-wave along the chosen axis, jittered by a perpendicular noise
    // so stripes wobble like real tiger fur instead of perfect bars.
    const v = p.axis === 'x' ? lx : p.axis === 'y' ? ly : lz;
    const jitter = p.jitter ?? 0;
    const wobble = jitter * Math.sin(ly * 3.5 + lx * 2.5);
    const wave = Math.sin(v * p.freq + wobble);
    const sharp = p.sharp ?? 4.0;
    // Push toward 0 / 1 using sharp scale.
    return Math.max(0, Math.min(1, (wave + 1) * 0.5 * sharp - (sharp - 1) * 0.5));
  }
  if (p.kind === 'patches') {
    const n = blob3D(lx * p.scale, ly * p.scale, lz * p.scale);
    return n > p.threshold ? 1 : 0;
  }
  return 0;
}

// Helper for the leg quad (4 ellipsoids at corners of a body).
const legQuad = (
  bodyHalfX: number,
  bodyZFront: number,
  bodyZBack: number,
  legY: number,
  legR: [number, number, number],
  body: number,
  pattern?: Pattern,
  furProps?: Partial<Pick<Part, 'fur' | 'furLen' | 'furDen' | 'tip'>>,
): Part[] => {
  const base: Omit<Part, 'c'> = {
    r: legR,
    body,
    pattern,
    ...(furProps ?? {}),
  };
  return [
    { ...base, c: [-bodyHalfX, legY, bodyZFront] },
    { ...base, c: [ bodyHalfX, legY, bodyZFront] },
    { ...base, c: [-bodyHalfX, legY, bodyZBack] },
    { ...base, c: [ bodyHalfX, legY, bodyZBack] },
  ];
};

const ANIMALS: AnimalDef[] = [
  // ─── Bear — solid brown, lighter underbelly, four stocky legs. ─────────
  {
    name: 'bear',
    pos: [-12.5, 0, 0],
    yaw: 0.2,
    parts: [
      // Body with a lighter belly via 'belly' pattern.
      {
        c: [0, 1.45, 0], r: [1.05, 0.85, 1.40],
        body: 0x6b4423, tip: 0xa67849, fur: true, furLen: 1.1, furDen: 1.0,
        pattern: { kind: 'belly', threshold: -0.10, bodyB: 0xa07a52, tipB: 0xc99770 },
      },
      // Head.
      {
        c: [0, 1.95, 1.10], r: [0.65, 0.60, 0.70],
        body: 0x6b4423, tip: 0xa67849, fur: true, furLen: 0.7,
      },
      // Ears.
      { c: [-0.34, 2.50, 1.05], r: [0.18, 0.18, 0.18], body: 0x4a2f17, tip: 0x6b4423, fur: true, furLen: 0.35 },
      { c: [ 0.34, 2.50, 1.05], r: [0.18, 0.18, 0.18], body: 0x4a2f17, tip: 0x6b4423, fur: true, furLen: 0.35 },
      // Snout, nose, eyes (no fur).
      { c: [0, 1.80, 1.65], r: [0.30, 0.22, 0.25], body: 0x4a2f17 },
      { c: [0, 1.85, 1.92], r: [0.10, 0.08, 0.07], body: 0x111111 },
      { c: [-0.22, 2.07, 1.55], r: [0.07, 0.07, 0.06], body: 0x111111 },
      { c: [ 0.22, 2.07, 1.55], r: [0.07, 0.07, 0.06], body: 0x111111 },
      // Legs — thicker than other animals, slightly darker than body.
      ...legQuad(0.65, 1.05, -1.05, 0.55, [0.30, 0.55, 0.30], 0x4a2f17,
        undefined, { fur: true, furLen: 0.45, tip: 0x6b4423 }),
    ],
  },

  // ─── Sheep — solid white wool, dark face/legs, fluffy as ever. ─────────
  {
    name: 'sheep',
    pos: [-7.5, 0, 0],
    yaw: 0.0,
    parts: [
      {
        c: [0, 1.40, 0], r: [0.95, 0.95, 1.20],
        body: 0xf0e8d4, tip: 0xffffff, fur: true, furLen: 1.5, furDen: 1.6,
      },
      // Dark head + ears.
      { c: [0, 1.65, 1.05], r: [0.40, 0.45, 0.50], body: 0x2a2018 },
      { c: [-0.32, 2.00, 0.90], r: [0.16, 0.18, 0.10], body: 0x2a2018, tip: 0xf0e8d4, fur: true, furLen: 0.30 },
      { c: [ 0.32, 2.00, 0.90], r: [0.16, 0.18, 0.10], body: 0x2a2018, tip: 0xf0e8d4, fur: true, furLen: 0.30 },
      { c: [-0.16, 1.75, 1.45], r: [0.05, 0.05, 0.04], body: 0xffffff },
      { c: [ 0.16, 1.75, 1.45], r: [0.05, 0.05, 0.04], body: 0xffffff },
      { c: [0, 1.55, 1.50], r: [0.10, 0.08, 0.08], body: 0x111111 },
      // Thin dark legs (no fur, just body).
      ...legQuad(0.50, 0.85, -0.85, 0.45, [0.10, 0.45, 0.10], 0x2a2018),
    ],
  },

  // ─── Fox — orange with white belly + black socks + white-tipped tail. ──
  {
    name: 'fox',
    pos: [-2.5, 0, 0],
    yaw: -0.1,
    parts: [
      // Body — orange with white belly.
      {
        c: [0, 1.05, 0], r: [0.55, 0.50, 1.10],
        body: 0xc25c2c, tip: 0xea8054, fur: true, furLen: 0.55, furDen: 1.0,
        pattern: { kind: 'belly', threshold: -0.10, bodyB: 0xfaf0e2, tipB: 0xffffff },
      },
      // Head with white muzzle (achieved via a dedicated muzzle part).
      {
        c: [0, 1.30, 0.95], r: [0.45, 0.40, 0.50],
        body: 0xc25c2c, tip: 0xf2a36a, fur: true, furLen: 0.35,
      },
      { c: [0, 1.20, 1.32], r: [0.20, 0.18, 0.22], body: 0xfaf0e2, tip: 0xffffff, fur: true, furLen: 0.18 },
      { c: [-0.27, 1.75, 0.90], r: [0.10, 0.30, 0.08], body: 0xc25c2c, tip: 0x222222, fur: true, furLen: 0.18 },
      { c: [ 0.27, 1.75, 0.90], r: [0.10, 0.30, 0.08], body: 0xc25c2c, tip: 0x222222, fur: true, furLen: 0.18 },
      // Tail — fox tails are bushy; white tip via belly pattern flipped along Z.
      {
        c: [0, 1.10, -1.20], r: [0.30, 0.30, 0.85],
        body: 0xc25c2c, tip: 0xea8054, fur: true, furLen: 0.95, furDen: 1.4,
        pattern: { kind: 'belly', threshold: -0.55, bodyB: 0xffffff, tipB: 0xffffff },
      },
      // Eyes + nose.
      { c: [-0.18, 1.40, 1.30], r: [0.05, 0.05, 0.04], body: 0x111111 },
      { c: [ 0.18, 1.40, 1.30], r: [0.05, 0.05, 0.04], body: 0x111111 },
      { c: [0, 1.20, 1.55], r: [0.05, 0.04, 0.04], body: 0x111111 },
      // Legs with black sock pattern.
      ...legQuad(0.40, 0.85, -0.85, 0.40, [0.10, 0.40, 0.10], 0xc25c2c,
        { kind: 'sock', axis: 'y', threshold: -0.20, bodyB: 0x1a1a1a, tipB: 0x2a2a2a },
        { fur: true, furLen: 0.18, tip: 0xea8054 }),
    ],
  },

  // ─── Bunny — long upright ears, pom tail, hopper hindlegs. ─────────────
  {
    name: 'bunny',
    pos: [2.5, 0, 0],
    yaw: 0.1,
    parts: [
      {
        c: [0, 0.95, 0], r: [0.55, 0.60, 0.80],
        body: 0xf2efe8, tip: 0xffffff, fur: true, furLen: 0.65, furDen: 1.2,
      },
      { c: [0, 1.35, 0.55], r: [0.40, 0.40, 0.45], body: 0xf2efe8, tip: 0xffffff, fur: true, furLen: 0.50 },
      // Long upright ears with pink interiors via belly pattern.
      {
        c: [-0.20, 2.20, 0.40], r: [0.10, 0.55, 0.10],
        body: 0xf2efe8, tip: 0xfdd6cf, fur: true, furLen: 0.20,
      },
      {
        c: [ 0.20, 2.20, 0.40], r: [0.10, 0.55, 0.10],
        body: 0xf2efe8, tip: 0xfdd6cf, fur: true, furLen: 0.20,
      },
      { c: [0, 0.90, -0.85], r: [0.22, 0.22, 0.22], body: 0xffffff, tip: 0xffffff, fur: true, furLen: 0.75, furDen: 2.0 },
      { c: [-0.14, 1.35, 0.95], r: [0.05, 0.05, 0.05], body: 0x111111 },
      { c: [ 0.14, 1.35, 0.95], r: [0.05, 0.05, 0.05], body: 0x111111 },
      { c: [0, 1.20, 1.00], r: [0.05, 0.04, 0.04], body: 0xff8a8a },
      // Front legs (small) + hindlegs (larger, hopper-style).
      { c: [-0.30, 0.30, 0.55], r: [0.10, 0.30, 0.10], body: 0xf2efe8, tip: 0xffffff, fur: true, furLen: 0.30 },
      { c: [ 0.30, 0.30, 0.55], r: [0.10, 0.30, 0.10], body: 0xf2efe8, tip: 0xffffff, fur: true, furLen: 0.30 },
      { c: [-0.40, 0.40, -0.35], r: [0.18, 0.40, 0.30], body: 0xf2efe8, tip: 0xffffff, fur: true, furLen: 0.45, furDen: 1.2 },
      { c: [ 0.40, 0.40, -0.35], r: [0.18, 0.40, 0.30], body: 0xf2efe8, tip: 0xffffff, fur: true, furLen: 0.45, furDen: 1.2 },
    ],
  },

  // ─── Tiger — orange + bold black stripes + white belly. ────────────────
  {
    name: 'tiger',
    pos: [7.5, 0, 0],
    yaw: -0.15,
    parts: [
      {
        c: [0, 1.10, 0], r: [0.60, 0.60, 1.30],
        body: 0xe2701a, tip: 0xf2933e, fur: true, furLen: 0.55, furDen: 1.1,
        pattern: { kind: 'stripes', freq: 4.0, axis: 'z', jitter: 1.4, sharp: 6.0,
                   bodyB: 0x141008, tipB: 0x2a1d10 },
      },
      // Head — also striped (lighter freq).
      {
        c: [0, 1.40, 1.10], r: [0.50, 0.45, 0.55],
        body: 0xe2701a, tip: 0xf2933e, fur: true, furLen: 0.40,
        pattern: { kind: 'stripes', freq: 8.0, axis: 'x', jitter: 0.8, sharp: 5.0,
                   bodyB: 0x141008, tipB: 0x2a1d10 },
      },
      // White muzzle.
      { c: [0, 1.25, 1.50], r: [0.22, 0.18, 0.22], body: 0xfaf0e2, tip: 0xffffff, fur: true, furLen: 0.20 },
      // Ears — rounded with dark backs.
      { c: [-0.32, 1.85, 0.95], r: [0.13, 0.16, 0.07], body: 0xe2701a, tip: 0x141008, fur: true, furLen: 0.18 },
      { c: [ 0.32, 1.85, 0.95], r: [0.13, 0.16, 0.07], body: 0xe2701a, tip: 0x141008, fur: true, furLen: 0.18 },
      // Tail — striped.
      {
        c: [0, 1.10, -1.40], r: [0.16, 0.16, 0.85],
        body: 0xe2701a, tip: 0xf2933e, fur: true, furLen: 0.45, furDen: 1.0,
        pattern: { kind: 'stripes', freq: 6.0, axis: 'z', jitter: 0.4, sharp: 6.0,
                   bodyB: 0x141008, tipB: 0x2a1d10 },
      },
      // Eyes + nose.
      { c: [-0.20, 1.55, 1.45], r: [0.06, 0.05, 0.05], body: 0xc6b06a }, // amber eyes
      { c: [ 0.20, 1.55, 1.45], r: [0.06, 0.05, 0.05], body: 0xc6b06a },
      { c: [0, 1.30, 1.70], r: [0.06, 0.05, 0.05], body: 0x4a1a14 }, // dark nose
      // Striped legs.
      ...legQuad(0.45, 1.00, -1.00, 0.50, [0.13, 0.50, 0.13], 0xe2701a,
        { kind: 'stripes', freq: 10.0, axis: 'y', jitter: 0.5, sharp: 5.0,
          bodyB: 0x141008, tipB: 0x2a1d10 },
        { fur: true, furLen: 0.22, tip: 0xf2933e }),
    ],
  },

  // ─── Cow — white with black blobby patches. ────────────────────────────
  {
    name: 'cow',
    pos: [12.5, 0, 0],
    yaw: 0.05,
    parts: [
      {
        c: [0, 1.30, 0], r: [0.75, 0.75, 1.55],
        body: 0xfafafa, tip: 0xffffff, fur: true, furLen: 0.50, furDen: 0.9,
        pattern: { kind: 'patches', scale: 1.7, threshold: 0.5, bodyB: 0x141414, tipB: 0x2a2a2a },
      },
      // Head — white with patches.
      {
        c: [0, 1.55, 1.30], r: [0.45, 0.45, 0.55],
        body: 0xfafafa, tip: 0xffffff, fur: true, furLen: 0.30,
        pattern: { kind: 'patches', scale: 2.6, threshold: 0.55, bodyB: 0x141414, tipB: 0x2a2a2a },
      },
      // Pink muzzle.
      { c: [0, 1.40, 1.70], r: [0.25, 0.18, 0.22], body: 0xf0a8a0 },
      // Black nostrils.
      { c: [-0.10, 1.40, 1.86], r: [0.05, 0.04, 0.04], body: 0x141414 },
      { c: [ 0.10, 1.40, 1.86], r: [0.05, 0.04, 0.04], body: 0x141414 },
      // Eyes.
      { c: [-0.22, 1.70, 1.55], r: [0.06, 0.05, 0.05], body: 0x141414 },
      { c: [ 0.22, 1.70, 1.55], r: [0.06, 0.05, 0.05], body: 0x141414 },
      // Two short curved horns (no fur).
      { c: [-0.30, 2.00, 1.05], r: [0.07, 0.18, 0.07], body: 0xece2c4 },
      { c: [ 0.30, 2.00, 1.05], r: [0.07, 0.18, 0.07], body: 0xece2c4 },
      // Pink ears.
      { c: [-0.50, 1.70, 1.15], r: [0.18, 0.10, 0.10], body: 0xfafafa, tip: 0xfdd6cf, fur: true, furLen: 0.20 },
      { c: [ 0.50, 1.70, 1.15], r: [0.18, 0.10, 0.10], body: 0xfafafa, tip: 0xfdd6cf, fur: true, furLen: 0.20 },
      // Tail with tuft.
      { c: [0, 1.20, -1.55], r: [0.09, 0.09, 0.50], body: 0xfafafa, tip: 0xffffff, fur: true, furLen: 0.18 },
      { c: [0, 0.80, -1.95], r: [0.18, 0.18, 0.18], body: 0x141414, tip: 0x2a2a2a, fur: true, furLen: 0.40, furDen: 1.5 },
      // Small udder (pink, no fur).
      { c: [0, 0.75, -0.30], r: [0.18, 0.18, 0.20], body: 0xf0a8a0 },
      // Legs (white with black hooves via sock pattern).
      ...legQuad(0.55, 1.20, -1.20, 0.55, [0.13, 0.55, 0.13], 0xfafafa,
        { kind: 'sock', axis: 'y', threshold: -0.55, bodyB: 0x141414, tipB: 0x2a2a2a },
        { fur: true, furLen: 0.22, tip: 0xffffff }),
    ],
  },
];

const FluffyAnimals: React.FC<FluffyAnimalsProps> = ({
  width,
  height,
  furDensity = 1,
  furLength = 1,
  windStrength = 0.5,
  autoRotate = true,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    const W0 = width ?? container.clientWidth ?? 1280;
    const H0 = height ?? container.clientHeight ?? 720;

    const scene = new THREE.Scene();
    scene.fog = new THREE.FogExp2(0xc8d8e8, 0.008);

    const camera = new THREE.PerspectiveCamera(45, W0 / H0, 0.1, 200);
    camera.position.set(0, 4, 14);
    camera.lookAt(0, 1.2, 0);

    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(W0, H0);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.5));
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.05;
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    container.appendChild(renderer.domElement);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.06;
    controls.minDistance = 5;
    controls.maxDistance = 40;
    controls.maxPolarAngle = Math.PI * 0.49;
    controls.target.set(0, 1.2, 0);
    controls.autoRotate = autoRotate;
    controls.autoRotateSpeed = 0.4;

    // ─── Sky + lighting ──────────────────────────────────────────────────
    const skyMat = new THREE.ShaderMaterial({
      side: THREE.BackSide,
      depthWrite: false,
      uniforms: { uSunDir: { value: new THREE.Vector3(0.4, 0.7, 0.6).normalize() } },
      vertexShader: `varying vec3 vDir; void main() { vec4 wp = modelMatrix*vec4(position,1); vDir = normalize(wp.xyz); gl_Position = projectionMatrix*viewMatrix*wp; }`,
      fragmentShader: `uniform vec3 uSunDir; varying vec3 vDir;
        void main() {
          vec3 d = normalize(vDir);
          float h = clamp(d.y*0.5+0.5, 0.0, 1.0);
          vec3 col = mix(vec3(0.95, 0.85, 0.74), vec3(0.45, 0.66, 0.92), pow(h, 0.7));
          float sd = max(dot(d, normalize(uSunDir)), 0.0);
          col += vec3(1.0, 0.95, 0.78) * (pow(sd, 32.0) * 1.2 + pow(sd, 4.0) * 0.10);
          gl_FragColor = vec4(col, 1.0);
        }`,
    });
    const sky = new THREE.Mesh(new THREE.SphereGeometry(120, 32, 16), skyMat);
    scene.add(sky);

    const ambient = new THREE.HemisphereLight(0xc6dbff, 0x4a3920, 0.7);
    scene.add(ambient);

    const sun = new THREE.DirectionalLight(0xfff1d8, 1.6);
    sun.position.set(8, 14, 10);
    sun.castShadow = true;
    sun.shadow.mapSize.set(1024, 1024);
    sun.shadow.camera.left = -16;
    sun.shadow.camera.right = 16;
    sun.shadow.camera.top = 16;
    sun.shadow.camera.bottom = -16;
    sun.shadow.camera.near = 1;
    sun.shadow.camera.far = 50;
    sun.shadow.bias = -0.0006;
    scene.add(sun);

    const sunDir = new THREE.Vector3().subVectors(sun.position, sun.target.position).normalize();

    // ─── Ground (simple painterly turf) ──────────────────────────────────
    const groundGeo = new THREE.PlaneGeometry(80, 80, 64, 64);
    groundGeo.rotateX(-Math.PI / 2);
    const groundMat = new THREE.ShaderMaterial({
      uniforms: {
        uSunDir: { value: sunDir.clone() },
        uSunColor: { value: new THREE.Color(0xfff1d8) },
        uAmbient: { value: new THREE.Color(0x506890) },
      },
      vertexShader: `
        varying vec3 vWorld;
        varying vec3 vNormal;
        void main() {
          vec4 wp = modelMatrix * vec4(position, 1.0);
          vWorld = wp.xyz;
          vNormal = normalize(normalMatrix * normal);
          gl_Position = projectionMatrix * viewMatrix * wp;
        }
      `,
      fragmentShader: `
        uniform vec3 uSunDir, uSunColor, uAmbient;
        varying vec3 vWorld;
        varying vec3 vNormal;
        ${NOISE_GLSL}
        void main() {
          float n = vnoise(vWorld.xz * 0.5);
          vec3 grass = mix(vec3(0.36, 0.50, 0.24), vec3(0.20, 0.34, 0.16), n * 0.5 + 0.5);
          // Soft circular vignette so the row of animals reads as the
          // subject and the periphery falls into haze.
          float r = length(vWorld.xz);
          grass = mix(grass, vec3(0.36, 0.45, 0.34), smoothstep(15.0, 35.0, r));
          float lit = max(dot(vNormal, normalize(uSunDir)), 0.0);
          grass = grass * (uAmbient * 0.4 + uSunColor * (lit * 0.7 + 0.4));
          gl_FragColor = vec4(grass, 1.0);
        }
      `,
    });
    const ground = new THREE.Mesh(groundGeo, groundMat);
    ground.receiveShadow = true;
    scene.add(ground);

    // ─── Build animals ───────────────────────────────────────────────────
    const phoneish = window.matchMedia('(max-width: 900px), (pointer: coarse)').matches;
    const densityScale = (phoneish ? 0.45 : 1.0) * furDensity;
    const lengthScale  = furLength;

    const furUniforms = {
      uTime: { value: 0 },
      uWindStrength: { value: windStrength },
      uSunDir: { value: sunDir.clone() },
      uSunColor: { value: new THREE.Color(0xfff1d8) },
      uAmbient: { value: new THREE.Color(0x4f6a8c) },
    };

    const furMaterial = new THREE.ShaderMaterial({
      uniforms: furUniforms,
      vertexShader: FUR_VERTEX,
      fragmentShader: FUR_FRAGMENT,
      side: THREE.DoubleSide,
      transparent: false,
      depthWrite: true,
    });

    const furMeshes: THREE.InstancedMesh[] = [];
    const furGeoms: THREE.BufferGeometry[] = [];
    const bodyMeshes: THREE.Mesh[] = [];
    const bodyMats: THREE.Material[] = [];

    const dummy = new THREE.Object3D();

    for (const animal of ANIMALS) {
      const group = new THREE.Group();
      group.position.set(...animal.pos);
      if (animal.yaw) group.rotation.y = animal.yaw;
      scene.add(group);

      // Total fur instance count for this animal — sum across furred parts.
      const furParts = animal.parts.filter((p) => p.fur);
      const partInstanceCounts: number[] = furParts.map((p) => {
        // Surface area ≈ 4π * mean(r1*r2). Density scaled by part's density.
        const [rx, ry, rz] = p.r;
        const area = 4 * Math.PI * Math.max(rx * ry + ry * rz + rx * rz, 0.05) / 3;
        const baseN = Math.round(area * 2200 * (p.furDen ?? 1.0) * densityScale);
        return Math.max(20, baseN);
      });
      const totalCount = partInstanceCounts.reduce((s, n) => s + n, 0);

      // One InstancedMesh per animal (cheaper than per-part). A short
      // base blade geometry — 0.35m at full length — is the canvas;
      // per-instance aLength scales it.
      const blade = new THREE.PlaneGeometry(0.05, 0.35, 1, 6);
      blade.translate(0, 0.175, 0);

      const aRandomArr = new Float32Array(totalCount);
      const aTwistArr  = new Float32Array(totalCount);
      const aLengthArr = new Float32Array(totalCount);
      const aBaseArr   = new Float32Array(totalCount * 3);
      const aTipArr    = new Float32Array(totalCount * 3);

      const inst = new THREE.InstancedMesh(blade, furMaterial, totalCount);
      inst.castShadow = false;
      inst.receiveShadow = false;
      inst.frustumCulled = false;

      const baseColor = new THREE.Color();
      const tipColor  = new THREE.Color();

      const baseA = new THREE.Color();
      const tipA  = new THREE.Color();
      const baseB = new THREE.Color();
      const tipB  = new THREE.Color();
      const baseMixed = new THREE.Color();
      const tipMixed  = new THREE.Color();

      let idx = 0;
      for (let pi = 0; pi < furParts.length; pi++) {
        const part = furParts[pi];
        const n = partInstanceCounts[pi];
        baseA.set(part.body);
        tipA.set(part.tip ?? part.body);
        // Pattern's secondary colours (if any).
        const pattern = part.pattern;
        if (pattern && pattern.kind !== 'solid') {
          baseB.set(pattern.bodyB);
          tipB.set(pattern.tipB);
        } else {
          baseB.copy(baseA);
          tipB.copy(tipA);
        }
        const partLen = (part.furLen ?? 1.0) * lengthScale;
        const [cx, cy, cz] = part.c;
        const [rx, ry, rz] = part.r;

        for (let i = 0; i < n; i++) {
          // Uniform random direction on unit sphere → ellipsoid surface.
          const u = Math.random();
          const v = Math.random();
          const phi   = Math.acos(2 * u - 1);
          const theta = 2 * Math.PI * v;
          const sinPhi = Math.sin(phi);
          const dx = sinPhi * Math.cos(theta);
          const dy = Math.cos(phi);
          const dz = sinPhi * Math.sin(theta);

          // Position on ellipsoid surface (world units).
          const px = cx + dx * rx;
          const py = cy + dy * ry;
          const pz = cz + dz * rz;
          // Local position relative to the part centre — used by the
          // pattern evaluator. lx/ly/lz are in world units.
          const lx = px - cx;
          const ly = py - cy;
          const lz = pz - cz;

          // Normal at that point: gradient of ellipsoid equation
          // (x/rx², y/ry², z/rz²), normalized.
          const nxRaw = dx / rx;
          const nyRaw = dy / ry;
          const nzRaw = dz / rz;
          const nLen = Math.sqrt(nxRaw * nxRaw + nyRaw * nyRaw + nzRaw * nzRaw);
          const nx = nxRaw / nLen;
          const ny = nyRaw / nLen;
          const nz = nzRaw / nLen;

          dummy.position.set(px, py, pz);
          dummy.quaternion.setFromUnitVectors(yAxis, new THREE.Vector3(nx, ny, nz));
          dummy.scale.set(0.85 + Math.random() * 0.4, 1, 1);
          dummy.updateMatrix();
          inst.setMatrixAt(idx, dummy.matrix);

          // Evaluate the pattern at this surface point and mix per-blade
          // colours between primary and secondary based on the result.
          const mix = evalPattern(pattern, lx, ly, lz, ry);
          baseMixed.copy(baseA).lerp(baseB, mix);
          tipMixed.copy(tipA).lerp(tipB, mix);

          aRandomArr[idx] = Math.random();
          aTwistArr[idx]  = Math.random() * Math.PI * 2;
          aLengthArr[idx] = (0.7 + Math.random() * 0.6) * partLen;
          aBaseArr[idx * 3 + 0] = baseMixed.r;
          aBaseArr[idx * 3 + 1] = baseMixed.g;
          aBaseArr[idx * 3 + 2] = baseMixed.b;
          aTipArr[idx * 3 + 0] = tipMixed.r;
          aTipArr[idx * 3 + 1] = tipMixed.g;
          aTipArr[idx * 3 + 2] = tipMixed.b;
          idx++;
        }
      }
      inst.instanceMatrix.needsUpdate = true;
      blade.setAttribute('aRandom', new THREE.InstancedBufferAttribute(aRandomArr, 1));
      blade.setAttribute('aTwist',  new THREE.InstancedBufferAttribute(aTwistArr, 1));
      blade.setAttribute('aLength', new THREE.InstancedBufferAttribute(aLengthArr, 1));
      blade.setAttribute('aBaseColor', new THREE.InstancedBufferAttribute(aBaseArr, 3));
      blade.setAttribute('aTipColor',  new THREE.InstancedBufferAttribute(aTipArr, 3));

      group.add(inst);
      furMeshes.push(inst);
      furGeoms.push(blade);

      // Body parts (visible underneath the fur — gives the animal weight).
      for (const part of animal.parts) {
        const geom = new THREE.SphereGeometry(1, 24, 16);
        const mat = new THREE.MeshStandardMaterial({
          color: part.body,
          roughness: 0.9,
          metalness: 0.0,
        });
        const m = new THREE.Mesh(geom, mat);
        m.position.set(...part.c);
        m.scale.set(...part.r);
        m.castShadow = true;
        m.receiveShadow = true;
        group.add(m);
        bodyMeshes.push(m);
        bodyMats.push(mat);
      }
    }

    // ─── Bloom post-pass ─────────────────────────────────────────────────
    const composer = new EffectComposer(renderer);
    composer.setSize(W0, H0);
    composer.setPixelRatio(Math.min(window.devicePixelRatio, 1.5));
    composer.addPass(new RenderPass(scene, camera));
    const bloomPass = new UnrealBloomPass(new THREE.Vector2(W0, H0), 0.30, 0.45, 0.92);
    composer.addPass(bloomPass);
    composer.addPass(new OutputPass());

    // ─── Animate ─────────────────────────────────────────────────────────
    const clock = new THREE.Clock();
    let raf = 0;
    const animate = () => {
      const elapsed = clock.getElapsedTime();
      furUniforms.uTime.value = elapsed;
      furUniforms.uWindStrength.value = windStrength;
      controls.autoRotate = autoRotate;
      controls.update();
      composer.render();
      raf = requestAnimationFrame(animate);
    };
    animate();

    // ─── Resize ──────────────────────────────────────────────────────────
    const onResize = () => {
      const w = container.clientWidth;
      const h = container.clientHeight;
      if (w === 0 || h === 0) return;
      camera.aspect = w / h;
      camera.updateProjectionMatrix();
      renderer.setSize(w, h, false);
      composer.setSize(w, h);
      bloomPass.setSize(w, h);
    };
    const ro = new ResizeObserver(onResize);
    ro.observe(container);

    return () => {
      cancelAnimationFrame(raf);
      ro.disconnect();
      controls.dispose();
      composer.dispose();
      furMaterial.dispose();
      furGeoms.forEach((g) => g.dispose());
      furMeshes.forEach((m) => { /* geom already in furGeoms */ void m; });
      bodyMeshes.forEach((m) => m.geometry.dispose());
      bodyMats.forEach((m) => m.dispose());
      groundGeo.dispose();
      groundMat.dispose();
      sky.geometry.dispose();
      skyMat.dispose();
      renderer.dispose();
      if (renderer.domElement.parentNode === container) {
        container.removeChild(renderer.domElement);
      }
    };
  }, [width, height, furDensity, furLength, windStrength, autoRotate]);

  const sx = (width !== undefined && height !== undefined)
    ? { width, height, overflow: 'hidden' }
    : { width: '100%', height: '100%', overflow: 'hidden' };
  return <Box ref={containerRef} sx={sx} />;
};

export default FluffyAnimals;
