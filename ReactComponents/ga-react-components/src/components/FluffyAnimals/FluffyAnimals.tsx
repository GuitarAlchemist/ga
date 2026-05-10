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
  /** Enables body, head, tail, ear, and gait animation. */
  motionEnabled?: boolean;
  /** Multiplier for idle and walking animation speed. */
  motionSpeed?: number;
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
  attribute vec3  aFlow;       // part-local lay direction

  varying vec2 vUv;
  varying vec3 vBaseColor;
  varying vec3 vTipColor;
  varying vec3 vNormalW;
  varying vec3 vTangentW;      // world-space hair-strand direction (Kajiya-Kay)
  varying vec3 vWorldPos;
  varying float vRandom;

  void main() {
    vUv = uv;
    vRandom = aRandom;
    vBaseColor = aBaseColor;
    vTipColor = aTipColor;

    // Base anchor in world space.
    vec4 anchor4 = modelMatrix * instanceMatrix * vec4(0.0, 0.0, 0.0, 1.0);
    vec3 anchor = anchor4.xyz;

    // Stretch the blade by aLength (per-instance variation).
    vec3 p = position;
    p.y *= aLength;

    // Per-blade twist around local Y (random rotation around the strand
    // axis so faces don't all align — kept small here because the lay
    // direction is now driven coherently in world-space below).
    float c = cos(aTwist);
    float s = sin(aTwist);
    vec3 rotated = vec3(c * p.x - s * p.z, p.y, s * p.x + c * p.z);

    // Push to world space.
    vec4 worldPos = modelMatrix * instanceMatrix * vec4(rotated, 1.0);

    // Surface normal in world space.
    vec3 normalW = normalize((modelMatrix * instanceMatrix * vec4(0.0, 1.0, 0.0, 0.0)).xyz);

    // World-space lay curl — project aFlow onto the tangent plane and
    // bend the tip in that direction. This is the "groomed in one
    // direction" effect that makes fur read as fur instead of grass.
    // Curve is t² so base barely moves.
    vec3 flowW = normalize((modelMatrix * vec4(aFlow, 0.0)).xyz);
    vec3 flow = flowW - normalW * dot(flowW, normalW);
    float flowMag = length(flow) + 1e-6;
    vec3 flowDir = flow / flowMag;
    float t = clamp(uv.y, 0.0, 1.0);
    float curve = t * t;
    float curlMag = 0.50 * (0.7 + aRandom * 0.5);  // strong lay
    worldPos.xyz += flowDir * curlMag * curve;
    // Subtle gravity sag — pull the tip down slightly.
    worldPos.y -= curve * 0.06 * aLength;

    // Wind ripple on the tip, in the flow direction (so wind ruffles fur
    // along its lay rather than buffeting it sideways).
    float gust = vnoise(anchor.xz * 0.6 + vec2(uTime * 0.4, uTime * 0.25));
    float windT = curve * uWindStrength;
    worldPos.xyz += flowDir * (gust * 0.6 - 0.3) * windT * 0.12;
    worldPos.y += sin(uTime * 1.5 + aRandom * 6.28) * windT * 0.04;

    // Hair tangent in world space — direction along the strand from base
    // to tip. Used by the fragment shader for Kajiya-Kay anisotropic
    // sheen. Effective strand direction blends the surface normal (root
    // points outward) with the flow direction (tip lays back).
    vec3 strandTip = normalW * 0.5 + flowDir * 0.6;
    vTangentW = normalize(strandTip);

    vNormalW = normalW;
    vWorldPos = worldPos.xyz;
    gl_Position = projectionMatrix * viewMatrix * worldPos;
  }
`;

const FUR_FRAGMENT = /* glsl */ `
  uniform vec3 uSunDir;
  uniform vec3 uSunColor;
  uniform vec3 uAmbient;
  uniform vec3 uCameraPos;

  varying vec2 vUv;
  varying vec3 vBaseColor;
  varying vec3 vTipColor;
  varying vec3 vNormalW;
  varying vec3 vTangentW;
  varying vec3 vWorldPos;
  varying float vRandom;

  void main() {
    // Soft edge falloff instead of a hard silhouette — strands have
    // a feathered alpha so the rim of each blade fades into the
    // neighbours' tips. Reads as fluff rather than stamped sticks.
    float halfW = abs(vUv.x - 0.5);
    float taper = 1.0 - vUv.y * 0.92;
    float edgeT = (taper * 0.5 - halfW) / max(taper * 0.5, 1e-4);
    if (edgeT < 0.02) discard;
    float edgeAlpha = smoothstep(0.0, 0.45, edgeT);

    // Color from base to tip with stronger per-blade jitter — real fur
    // shows lots of micro variation so strands don't look stamped.
    float ao = pow(vUv.y, 1.3);
    float jitter = (vRandom - 0.5) * 0.22;
    vec3 col = mix(vBaseColor, vTipColor, clamp(ao + jitter, 0.0, 1.0));
    col *= 0.82 + vRandom * 0.36;   // wider overall brightness jitter

    // Per-fragment fuzz noise anchored to the blade's UV so neighbouring
    // blades show uncorrelated speckle — adds the granular soft-fur look
    // and prevents the gradient from reading as a glossy plastic strip.
    float fuzz = fract(sin(vUv.x * 234.0 + vUv.y * 567.0 + vRandom * 13.0) * 43758.55);
    col *= 0.92 + fuzz * 0.16;

    // Lambert wrap shading on the surface normal.
    vec3 N = normalize(vNormalW);
    vec3 L = normalize(uSunDir);
    float lit = max(dot(N, L), 0.0);
    float wrap = lit * 0.55 + 0.45;

    // Softer Kajiya-Kay — broader, dimmer highlights so fur reads as
    // diffuse fluff rather than waxed plastic. Primary specular halved
    // and broadened (pow 90 → 35); secondary kept low and warmed by
    // ambient instead of tip-bright.
    vec3 V = normalize(uCameraPos - vWorldPos);
    vec3 H = normalize(V + L);
    vec3 T = normalize(vTangentW);
    float TdotH = dot(T, H);
    float aniso = sqrt(max(1.0 - TdotH * TdotH, 0.0));
    float primary   = pow(aniso, 35.0) * 0.18;
    float secondary = pow(aniso, 12.0) * 0.10;
    vec3 sheen = vec3(1.0, 0.97, 0.92) * primary + uAmbient * secondary;

    // Silhouette boost — softer Fresnel for that fluffy-halo look.
    float fres = pow(1.0 - max(dot(N, V), 0.0), 2.0);
    float silhouette = fres * ao;

    // Backlit translucency at low sun angles for that fluffy halo.
    float backLit = pow(1.0 - lit, 2.0) * smoothstep(0.0, 0.4, uSunDir.y);
    col += backLit * vTipColor * 0.40 * ao;
    col += silhouette * vTipColor * 0.55;
    col += sheen * smoothstep(0.0, 0.3, uSunDir.y);

    // Apply the soft edge alpha as a fade — keeps depth-write working
    // while rim pixels darken and blend with what's behind them.
    col *= 0.45 + 0.55 * edgeAlpha;

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
  /**
   * Hair-lay direction in animal-local space. Defaults to [0, 0, -1]
   * (back-swept toward the tail). Override per part — legs flow [0,-1,0]
   * (downward), upright ears [0, 1, 0], tails along their long axis.
   */
  flow?: [number, number, number];
}

interface AnimalDef {
  name: string;
  parts: Part[];
  pos: [number, number, number];
  yaw?: number;
}

type PartRole = 'body' | 'head' | 'ear' | 'tail' | 'leg' | 'face' | 'other';

interface AnimatedPart {
  group: THREE.Group;
  basePosition: THREE.Vector3;
  role: PartRole;
  side: number;
  front: number;
}

interface AnimatedAnimal {
  name: string;
  group: THREE.Group;
  basePosition: THREE.Vector3;
  baseYaw: number;
  phase: number;
  speed: number;
  parts: AnimatedPart[];
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
    // Legs grow fur downward — like real fur on a leg laying toward the
    // hoof / paw.
    flow: [0, -1, 0],
    ...(furProps ?? {}),
  };
  return [
    { ...base, c: [-bodyHalfX, legY, bodyZFront] },
    { ...base, c: [ bodyHalfX, legY, bodyZFront] },
    { ...base, c: [-bodyHalfX, legY, bodyZBack] },
    { ...base, c: [ bodyHalfX, legY, bodyZBack] },
  ];
};

const classifyPart = (part: Part, partIndex: number): PartRole => {
  if (partIndex === 0) return 'body';

  const [, cy, cz] = part.c;
  const [rx, ry, rz] = part.r;

  if (cy < 0.9 && ry >= 0.25) return 'leg';
  if (cz < -0.7 && (rz > 0.35 || rx < 0.25)) return 'tail';
  if (cy > 1.65 && rx <= 0.35 && rz <= 0.2 && ry >= 0.1) return 'ear';
  if (cz > 0.65 && rx > 0.25 && ry > 0.2) return 'head';
  if (rx <= 0.12 && ry <= 0.12 && rz <= 0.12) return 'face';
  return 'other';
};

const animalPhase = (name: string): number => {
  let hash = 0;
  for (let i = 0; i < name.length; i++) hash = (hash * 31 + name.charCodeAt(i)) % 997;
  return (hash / 997) * Math.PI * 2;
};

const ANIMALS: AnimalDef[] = [
  // ─── Bear — solid brown, lighter underbelly, four stocky legs. ─────────
  {
    name: 'bear',
    pos: [-4.5, 0, 0.2],
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
    pos: [-2.7, 0, -0.35],
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
    pos: [-0.9, 0, 0.45],
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
    pos: [0.9, 0, -0.15],
    yaw: 0.1,
    parts: [
      {
        c: [0, 0.95, 0], r: [0.55, 0.60, 0.80],
        body: 0xf2efe8, tip: 0xffffff, fur: true, furLen: 0.65, furDen: 1.2,
      },
      { c: [0, 1.35, 0.55], r: [0.40, 0.40, 0.45], body: 0xf2efe8, tip: 0xffffff, fur: true, furLen: 0.50 },
      // Long upright ears with pink interiors via belly pattern.
      // Fur flows UPWARD toward the ear tip — ears should look groomed,
      // not exploded radially.
      {
        c: [-0.20, 2.20, 0.40], r: [0.10, 0.55, 0.10],
        body: 0xf2efe8, tip: 0xfdd6cf, fur: true, furLen: 0.20,
        flow: [0, 1, 0],
      },
      {
        c: [ 0.20, 2.20, 0.40], r: [0.10, 0.55, 0.10],
        body: 0xf2efe8, tip: 0xfdd6cf, fur: true, furLen: 0.20,
        flow: [0, 1, 0],
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
    pos: [2.8, 0, 0.35],
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
    pos: [4.7, 0, -0.25],
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
  motionEnabled = true,
  motionSpeed = 0.8,
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

    const camera = new THREE.PerspectiveCamera(44, W0 / H0, 0.1, 200);
    camera.position.set(0, 3.35, 13);
    camera.lookAt(0, 1.1, 0.1);

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
    controls.minDistance = 4;
    controls.maxDistance = 28;
    controls.maxPolarAngle = Math.PI * 0.49;
    controls.target.set(0, 1.1, 0.1);
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

    // ─── Meadow grass — fluffy-grass-style bezier blades ─────────────────
    // Same idiom as /test/fluffy-grass: instanced curved planes with a
    // vertex bend + wind gust, alpha-test silhouette via discard. Placed
    // in a chunked grid around the animals; rejection-sampled against
    // each animal's footprint so blades don't poke through bodies.

    // Animal exclusion footprints in world space (centre + radius).
    const animalExclusions: Array<{ x: number; z: number; r: number }> = ANIMALS.map((a) => ({
      x: a.pos[0],
      z: a.pos[2],
      // Use the body part's xz radii plus a small turf gap.
      r: Math.max(a.parts[0].r[0], a.parts[0].r[2]) + 0.6,
    }));
    const insideAnimal = (x: number, z: number): boolean => {
      for (const ex of animalExclusions) {
        const dx = x - ex.x;
        const dz = z - ex.z;
        if (dx * dx + dz * dz < ex.r * ex.r) return true;
      }
      return false;
    };

    const grassUniforms = {
      uTime:      { value: 0 },
      uSunDir:    { value: sunDir.clone() },
      uSunColor:  { value: new THREE.Color(0xfff1d8) },
      uAmbient:   { value: new THREE.Color(0x4f6a8c) },
      // Brighter, more saturated green so the strands stand out against
      // the painterly ground plane underneath them. Same gradient idea
      // as fluffy-grass but pushed for visibility at this camera scale.
      uBaseColor: { value: new THREE.Color(0x274d20) },
      uTipColor:  { value: new THREE.Color(0xb8d666) },
      uTipColor2: { value: new THREE.Color(0xd6e08a) },
      uWindStrength: { value: 0.30 },
    };

    const grassMaterial = new THREE.ShaderMaterial({
      uniforms: grassUniforms,
      side: THREE.DoubleSide,
      vertexShader: /* glsl */ `
        ${NOISE_GLSL}

        uniform float uTime;
        uniform float uWindStrength;

        attribute float gRandom;
        attribute float gBend;
        attribute float gTwist;

        varying vec2 vUv;
        varying float vGust;
        varying vec3 vNormalW;
        varying float vRandom;

        void main() {
          vUv = uv;
          vRandom = gRandom;

          vec4 ip = instanceMatrix * vec4(0.0, 0.0, 0.0, 1.0);
          vec3 anchor = (modelMatrix * ip).xyz;

          // Large-scale gust band sweeping diagonally.
          vec2 gustUV = anchor.xz * 0.05 + vec2(uTime * 0.18, uTime * 0.10);
          float gustRaw = vnoise(gustUV) * 0.5 + 0.5;
          float gust = pow(gustRaw, 1.6);
          vGust = gust;

          float flutter = vnoise(anchor.xz * 0.6 + vec2(uTime * 1.0, gRandom * 13.0));

          vec3 p = position;
          float t = clamp(uv.y, 0.0, 1.0);
          float curve = t * t;
          float windAmp = (gust * 1.0 + 0.2) * uWindStrength + flutter * 0.07;
          float bendAmt = gBend + windAmp;

          p.x += curve * bendAmt;
          p.y -= curve * bendAmt * 0.25;

          float c = cos(gTwist);
          float s = sin(gTwist);
          vec3 rotated = vec3(c * p.x - s * p.z, p.y, s * p.x + c * p.z);

          vec4 worldPos = modelMatrix * instanceMatrix * vec4(rotated, 1.0);

          vec3 nLocal = normalize(vec3(-bendAmt * (1.0 - t) * 2.0, 0.5, 1.0));
          vec3 nRot   = vec3(c * nLocal.x - s * nLocal.z, nLocal.y, s * nLocal.x + c * nLocal.z);
          vNormalW    = normalize((modelMatrix * vec4(nRot, 0.0)).xyz);

          gl_Position = projectionMatrix * viewMatrix * worldPos;
        }
      `,
      fragmentShader: /* glsl */ `
        uniform vec3 uBaseColor;
        uniform vec3 uTipColor;
        uniform vec3 uTipColor2;
        uniform vec3 uSunDir;
        uniform vec3 uSunColor;
        uniform vec3 uAmbient;

        varying vec2 vUv;
        varying float vGust;
        varying vec3 vNormalW;
        varying float vRandom;

        void main() {
          float halfW = abs(vUv.x - 0.5);
          float taper = 1.0 - vUv.y * 0.85;
          if (halfW > taper * 0.5) discard;

          float ao = pow(vUv.y, 1.4);
          vec3 tipMix = mix(uTipColor, uTipColor2, vRandom);
          vec3 col = mix(uBaseColor, tipMix, ao);

          col += (vGust - 0.45) * 0.28 * vec3(0.9, 1.05, 0.7);

          float sunDot = max(dot(vNormalW, normalize(uSunDir)), 0.0);
          float backLit = pow(1.0 - sunDot, 2.0) * smoothstep(0.0, 0.3, uSunDir.y);
          col += backLit * vec3(0.30, 0.40, 0.20) * ao;

          float wrap = sunDot * 0.5 + 0.5;
          col = col * (uAmbient * 0.4 + uSunColor * wrap);

          gl_FragColor = vec4(col, 1.0);
        }
      `,
    });

    // Wider + taller blades than fluffy-grass uses, because the animals
    // sit further from the camera here and thin blades pixelate to
    // invisible green dots at this distance.
    const grassBladeBase = new THREE.PlaneGeometry(0.12, 0.50, 1, 6);
    grassBladeBase.translate(0, 0.25, 0);

    const phoneGrass = window.matchMedia('(max-width: 900px), (pointer: coarse)').matches;
    const grassChunkSize  = 1.5;
    const grassChunkCount = phoneGrass ? 12 : 18;            // 18m or 27m square
    const grassDensity    = phoneGrass ? 80 : 160;
    const grassPlaneAngles = [0, Math.PI / 3, (2 * Math.PI) / 3];
    const grassHalfCount  = grassChunkCount / 2;
    const grassMeshes: THREE.InstancedMesh[] = [];
    const grassGeoms: THREE.BufferGeometry[] = [];

    const grassDummy = new THREE.Object3D();
    for (let cx = 0; cx < grassChunkCount; cx++) {
      for (let cz = 0; cz < grassChunkCount; cz++) {
        const baseX = (cx - grassHalfCount) * grassChunkSize;
        const baseZ = (cz - grassHalfCount) * grassChunkSize;

        for (const baseAngle of grassPlaneAngles) {
          const geo = grassBladeBase.clone();
          const gRandomArr = new Float32Array(grassDensity);
          const gBendArr   = new Float32Array(grassDensity);
          const gTwistArr  = new Float32Array(grassDensity);
          const inst = new THREE.InstancedMesh(geo, grassMaterial, grassDensity);
          inst.castShadow = false;
          inst.receiveShadow = false;
          inst.frustumCulled = false;

          for (let i = 0; i < grassDensity; i++) {
            let x = 0, z = 0, ok = false;
            for (let tries = 0; tries < 6; tries++) {
              const tx = baseX + Math.random() * grassChunkSize;
              const tz = baseZ + Math.random() * grassChunkSize;
              if (!insideAnimal(tx, tz)) { x = tx; z = tz; ok = true; break; }
            }
            const sH = ok ? 0.55 + Math.random() * 0.85 : 0;
            const sW = ok ? 0.85 + Math.random() * 0.5  : 0;
            grassDummy.position.set(x, 0, z);
            grassDummy.rotation.set(0, 0, 0);
            grassDummy.scale.set(sW, sH, 1);
            grassDummy.updateMatrix();
            inst.setMatrixAt(i, grassDummy.matrix);
            gRandomArr[i] = Math.random();
            gBendArr[i]   = (Math.random() - 0.5) * 0.10;
            gTwistArr[i]  = baseAngle + (Math.random() - 0.5) * 0.5;
          }
          inst.instanceMatrix.needsUpdate = true;
          geo.setAttribute('gRandom', new THREE.InstancedBufferAttribute(gRandomArr, 1));
          geo.setAttribute('gBend',   new THREE.InstancedBufferAttribute(gBendArr, 1));
          geo.setAttribute('gTwist',  new THREE.InstancedBufferAttribute(gTwistArr, 1));
          scene.add(inst);
          grassMeshes.push(inst);
          grassGeoms.push(geo);
        }
      }
    }

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
      uCameraPos: { value: camera.position.clone() },
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
    const animatedAnimals: AnimatedAnimal[] = [];
    const shadowGeo = new THREE.CircleGeometry(1, 40);
    shadowGeo.rotateX(-Math.PI / 2);
    const shadowMat = new THREE.MeshBasicMaterial({
      color: 0x1b2414,
      transparent: true,
      opacity: 0.28,
      depthWrite: false,
    });

    const dummy = new THREE.Object3D();

    for (const animal of ANIMALS) {
      const group = new THREE.Group();
      group.position.set(...animal.pos);
      if (animal.yaw) group.rotation.y = animal.yaw;
      scene.add(group);

      const body = animal.parts[0];
      const shadow = new THREE.Mesh(shadowGeo, shadowMat);
      shadow.position.set(0, 0.015, 0);
      shadow.scale.set(Math.max(body.r[0] * 1.7, 0.9), Math.max(body.r[2] * 1.55, 1.0), 1);
      shadow.renderOrder = -1;
      group.add(shadow);

      const animatedParts: AnimatedPart[] = [];

      for (let partIndex = 0; partIndex < animal.parts.length; partIndex++) {
        const part = animal.parts[partIndex];
        const partGroup = new THREE.Group();
        partGroup.position.set(...part.c);
        group.add(partGroup);

        const role = classifyPart(part, partIndex);
        animatedParts.push({
          group: partGroup,
          basePosition: partGroup.position.clone(),
          role,
          side: part.c[0] < 0 ? -1 : 1,
          front: part.c[2] >= 0 ? 1 : -1,
        });

        // Body part (visible underneath the fur — gives the animal weight).
        const geom = new THREE.SphereGeometry(1, 24, 16);
        const mat = new THREE.MeshStandardMaterial({
          color: part.body,
          roughness: 0.86,
          metalness: 0.0,
        });
        const m = new THREE.Mesh(geom, mat);
        m.scale.set(...part.r);
        m.castShadow = true;
        m.receiveShadow = true;
        partGroup.add(m);
        bodyMeshes.push(m);
        bodyMats.push(mat);

        if (!part.fur) continue;

        const [rx, ry, rz] = part.r;
        const area = 4 * Math.PI * Math.max(rx * ry + ry * rz + rx * rz, 0.05) / 3;
        const totalCount = Math.max(20, Math.round(area * 3200 * (part.furDen ?? 1.0) * densityScale));

        // Thinner blade — 0.030 wide vs 0.05 — strands read as hair, not
        // grass, especially at silhouette. One instanced mesh per part lets
        // articulated motion carry fur with the body part it belongs to.
        const blade = new THREE.PlaneGeometry(0.030, 0.40, 1, 6);
        blade.translate(0, 0.20, 0);

        const aRandomArr = new Float32Array(totalCount);
        const aTwistArr  = new Float32Array(totalCount);
        const aLengthArr = new Float32Array(totalCount);
        const aBaseArr   = new Float32Array(totalCount * 3);
        const aTipArr    = new Float32Array(totalCount * 3);
        const aFlowArr   = new Float32Array(totalCount * 3);

        const inst = new THREE.InstancedMesh(blade, furMaterial, totalCount);
        inst.castShadow = false;
        inst.receiveShadow = false;
        inst.frustumCulled = false;

        const baseA = new THREE.Color();
        const tipA  = new THREE.Color();
        const baseB = new THREE.Color();
        const tipB  = new THREE.Color();
        const baseMixed = new THREE.Color();
        const tipMixed  = new THREE.Color();
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

        // Part-local lay direction; the shader transforms it with modelMatrix
        // so animated ears, tails, heads, and legs keep coherent grooming.
        const [flx, fly, flz] = part.flow ?? [0, 0, -1];

        for (let idx = 0; idx < totalCount; idx++) {
          // Uniform random direction on unit sphere → ellipsoid surface.
          const u = Math.random();
          const v = Math.random();
          const phi   = Math.acos(2 * u - 1);
          const theta = 2 * Math.PI * v;
          const sinPhi = Math.sin(phi);
          const dx = sinPhi * Math.cos(theta);
          const dy = Math.cos(phi);
          const dz = sinPhi * Math.sin(theta);

          // Position on this part's ellipsoid surface, in part-local units.
          const px = dx * rx;
          const py = dy * ry;
          const pz = dz * rz;

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
          // Wider width jitter — most strands thin, a few thicker. Real
          // fur has a heavy mix of guard hairs (thicker, sparse) and
          // undercoat (fine, dense). Approximated with a quartic taper:
          // mostly 0.5–0.9 width, occasional 1.0–1.5.
          const w = 0.5 + Math.pow(Math.random(), 2) * 1.0;
          dummy.scale.set(w, 1, 1);
          dummy.updateMatrix();
          inst.setMatrixAt(idx, dummy.matrix);

          // Evaluate the pattern at this local surface point and mix per-blade
          // colours between primary and secondary based on the result.
          const mix = evalPattern(pattern, px, py, pz, ry);
          baseMixed.copy(baseA).lerp(baseB, mix);
          tipMixed.copy(tipA).lerp(tipB, mix);

          aRandomArr[idx] = Math.random();
          aTwistArr[idx]  = Math.random() * Math.PI * 2;
          // Wider length variation — 0.55× to 1.5× of the part's nominal
          // fur length. Mixes short undercoat with longer guard hairs.
          aLengthArr[idx] = (0.55 + Math.random() * 0.95) * partLen;
          aBaseArr[idx * 3 + 0] = baseMixed.r;
          aBaseArr[idx * 3 + 1] = baseMixed.g;
          aBaseArr[idx * 3 + 2] = baseMixed.b;
          aTipArr[idx * 3 + 0] = tipMixed.r;
          aTipArr[idx * 3 + 1] = tipMixed.g;
          aTipArr[idx * 3 + 2] = tipMixed.b;
          aFlowArr[idx * 3 + 0] = flx;
          aFlowArr[idx * 3 + 1] = fly;
          aFlowArr[idx * 3 + 2] = flz;
        }

        inst.instanceMatrix.needsUpdate = true;
        blade.setAttribute('aRandom', new THREE.InstancedBufferAttribute(aRandomArr, 1));
        blade.setAttribute('aTwist',  new THREE.InstancedBufferAttribute(aTwistArr, 1));
        blade.setAttribute('aLength', new THREE.InstancedBufferAttribute(aLengthArr, 1));
        blade.setAttribute('aBaseColor', new THREE.InstancedBufferAttribute(aBaseArr, 3));
        blade.setAttribute('aTipColor',  new THREE.InstancedBufferAttribute(aTipArr, 3));
        blade.setAttribute('aFlow',      new THREE.InstancedBufferAttribute(aFlowArr, 3));

        partGroup.add(inst);
        furMeshes.push(inst);
        furGeoms.push(blade);
      }

      const phase = animalPhase(animal.name);
      animatedAnimals.push({
        name: animal.name,
        group,
        basePosition: group.position.clone(),
        baseYaw: animal.yaw ?? 0,
        phase,
        speed: 0.75 + ((phase / (Math.PI * 2)) % 1.0) * 0.45,
        parts: animatedParts,
      });
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
    const updateAnimalMotion = (elapsed: number) => {
      const speedScale = Math.max(0, Math.min(1.8, motionSpeed));

      for (const animal of animatedAnimals) {
        animal.group.position.copy(animal.basePosition);
        animal.group.rotation.set(0, animal.baseYaw, 0);

        if (!motionEnabled || speedScale <= 0) {
          for (const part of animal.parts) {
            part.group.position.copy(part.basePosition);
            part.group.rotation.set(0, 0, 0);
            part.group.scale.set(1, 1, 1);
          }
          continue;
        }

        const idleT = elapsed * animal.speed * speedScale + animal.phase;
        const gaitT = elapsed * (2.1 + animal.speed * 0.55) * speedScale + animal.phase;
        const wanderX = Math.sin(idleT * 0.24) * 0.18;
        const wanderZ = Math.cos(idleT * 0.19) * 0.12;
        animal.group.position.x += wanderX;
        animal.group.position.z += wanderZ;
        animal.group.rotation.y += Math.sin(idleT * 0.18) * 0.075;

        const breathe = Math.sin(elapsed * 1.35 * speedScale + animal.phase) * 0.026;
        const gaitBob = Math.abs(Math.sin(gaitT * 2.0)) * 0.025;
        const grazingRaw = animal.name === 'sheep' || animal.name === 'cow'
          ? Math.sin(elapsed * 0.36 * speedScale + animal.phase)
          : -1;
        const graze = Math.max(0, Math.min(1, (grazingRaw - 0.15) / 0.85));

        for (const part of animal.parts) {
          part.group.position.copy(part.basePosition);
          part.group.rotation.set(0, 0, 0);
          part.group.scale.set(1, 1, 1);

          if (part.role === 'body') {
            part.group.position.y += breathe + gaitBob;
            part.group.rotation.z = Math.sin(gaitT) * 0.018;
            part.group.scale.set(1 + breathe * 0.45, 1 - breathe * 0.35, 1 + breathe * 0.25);
          } else if (part.role === 'head') {
            const look = Math.sin(idleT * 0.62);
            part.group.position.y += breathe * 0.45 - graze * 0.28;
            part.group.position.z += graze * 0.22;
            part.group.rotation.x = Math.sin(idleT * 0.78) * 0.06 - graze * 0.62;
            part.group.rotation.y = look * 0.16;
          } else if (part.role === 'ear') {
            const twitch = Math.max(0, Math.sin(elapsed * 2.7 * speedScale + animal.phase + part.side * 1.4));
            part.group.rotation.x = Math.sin(idleT * 1.1 + part.side) * 0.08 + twitch * 0.16;
            part.group.rotation.z = part.side * (0.12 + Math.sin(idleT * 0.9) * 0.08);
          } else if (part.role === 'tail') {
            const wag = Math.sin(elapsed * 2.8 * speedScale + animal.phase);
            part.group.rotation.x = -0.08 + Math.sin(idleT * 0.8) * 0.06;
            part.group.rotation.y = wag * 0.28;
            part.group.rotation.z = Math.sin(elapsed * 1.7 * speedScale + animal.phase) * 0.12;
          } else if (part.role === 'leg') {
            const diagonalOffset = part.side * part.front > 0 ? 0 : Math.PI;
            const step = Math.sin(gaitT + diagonalOffset);
            const lift = Math.max(0, step) * 0.075;
            part.group.position.y += lift;
            part.group.position.z += Math.cos(gaitT + diagonalOffset) * 0.025 * part.front;
            part.group.rotation.x = step * 0.18 * part.front;
            part.group.rotation.z = Math.sin(gaitT + diagonalOffset) * 0.025 * part.side;
          } else if (part.role === 'face') {
            part.group.position.y += breathe * 0.35 - graze * 0.28;
          }
        }
      }
    };

    const animate = () => {
      const elapsed = clock.getElapsedTime();
      furUniforms.uTime.value = elapsed;
      grassUniforms.uTime.value = elapsed;
      grassUniforms.uWindStrength.value = 0.30 + windStrength * 0.20;
      furUniforms.uWindStrength.value = windStrength;
      furUniforms.uCameraPos.value.copy(camera.position);
      updateAnimalMotion(elapsed);
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
      shadowGeo.dispose();
      shadowMat.dispose();
      groundGeo.dispose();
      groundMat.dispose();
      grassMaterial.dispose();
      grassBladeBase.dispose();
      grassGeoms.forEach((g) => g.dispose());
      sky.geometry.dispose();
      skyMat.dispose();
      renderer.dispose();
      if (renderer.domElement.parentNode === container) {
        container.removeChild(renderer.domElement);
      }
    };
  }, [width, height, furDensity, furLength, windStrength, motionEnabled, motionSpeed, autoRotate]);

  const sx = (width !== undefined && height !== undefined)
    ? { width, height, overflow: 'hidden' }
    : { width: '100%', height: '100%', overflow: 'hidden' };
  return <Box ref={containerRef} sx={sx} />;
};

export default FluffyAnimals;
