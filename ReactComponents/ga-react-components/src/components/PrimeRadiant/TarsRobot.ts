// src/components/PrimeRadiant/TarsRobot.ts
// TARS — articulated monolith robot from Interstellar
// Four rectangular slabs that pivot, walk, and reconfigure
// Holographic wireframe aesthetic matching Prime Radiant

import * as THREE from 'three';
import type { MeshBasicNodeMaterial } from 'three/webgpu';
import { createHolographicMaterialTSL } from './shaders/HolographicTSL';

// ---------------------------------------------------------------------------
// TARS locomotion modes
// ---------------------------------------------------------------------------
export type TarsMode = 'standing' | 'walking' | 'spinning' | 'compact';

// ---------------------------------------------------------------------------
// Holographic slab material
// ---------------------------------------------------------------------------
const slabVertexShader = /* glsl */ `
  varying vec3 vNormal;
  varying vec3 vViewDir;
  varying vec3 vWorldPos;

  void main() {
    vNormal = normalize(normalMatrix * normal);
    vec4 worldPos = modelMatrix * vec4(position, 1.0);
    vWorldPos = worldPos.xyz;
    vViewDir = normalize(cameraPosition - worldPos.xyz);
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  }
`;

const slabFragmentShader = /* glsl */ `
  uniform vec3 uColor;
  uniform float uTime;
  uniform float uOpacity;

  varying vec3 vNormal;
  varying vec3 vViewDir;
  varying vec3 vWorldPos;

  void main() {
    float fresnel = 1.0 - abs(dot(vNormal, vViewDir));
    fresnel = pow(fresnel, 2.0);

    // Edge glow — bright wireframe-like edges
    float edgeGlow = pow(fresnel, 1.5) * 0.8;

    // Horizontal scan lines
    float scan = sin(vWorldPos.y * 15.0 - uTime * 3.0) * 0.5 + 0.5;
    scan = pow(scan, 6.0) * 0.2;

    // Data stream — vertical lines flowing down the slab
    float dataStream = sin(vWorldPos.x * 30.0 + uTime * 2.0) * 0.5 + 0.5;
    dataStream *= sin(vWorldPos.y * 5.0 - uTime * 8.0) * 0.5 + 0.5;
    dataStream = pow(dataStream, 4.0) * 0.15;

    // Holographic flicker
    float flicker = 1.0;
    float seed = fract(sin(floor(uTime * 10.0) * 43758.5453));
    if (seed > 0.94) flicker = 0.4;

    float alpha = (fresnel * 0.5 + 0.15 + edgeGlow + scan + dataStream) * uOpacity * flicker;
    alpha = clamp(alpha, 0.0, 1.0);

    vec3 col = uColor * (0.4 + fresnel * 0.8 + scan + dataStream * 2.0);
    // Cool blue-white tint for TARS (different from Demerzel's gold)
    col += vec3(0.05, 0.1, 0.2) * fresnel;

    gl_FragColor = vec4(col, alpha);
  }
`;

function createSlabMaterial(opacity: number = 0.5): MeshBasicNodeMaterial {
  return createHolographicMaterialTSL({
    color: '#B0C4DE', // light steel blue
    opacity,
    wireframe: false,
    dataStream: true,
  });
}

// ---------------------------------------------------------------------------
// Create TARS robot
// ---------------------------------------------------------------------------
export function createTarsRobot(scale: number = 1): THREE.Group {
  const group = new THREE.Group();
  group.userData.isTarsRobot = true;
  const s = scale;

  // TARS is 4 rectangular slabs that pivot at joints
  // Each slab: tall, thin rectangle
  const slabWidth = 0.8 * s;
  const slabHeight = 4 * s;
  const slabDepth = 0.15 * s;

  const slabs: THREE.Group[] = [];

  for (let i = 0; i < 4; i++) {
    const slabGroup = new THREE.Group();
    slabGroup.name = `tars-slab-${i}`;

    const geo = new THREE.BoxGeometry(slabWidth, slabHeight, slabDepth);
    const mat = createSlabMaterial(0.45 + i * 0.05);

    // Wireframe overlay for that monolith look
    const solid = new THREE.Mesh(geo, mat);
    slabGroup.add(solid);

    const wireGeo = new THREE.EdgesGeometry(geo);
    const wireMat = new THREE.LineBasicMaterial({
      color: new THREE.Color('#B0C4DE'),
      transparent: true,
      opacity: 0.6,
      blending: THREE.AdditiveBlending,
    });
    const wireframe = new THREE.LineSegments(wireGeo, wireMat);
    slabGroup.add(wireframe);

    // Tiny screen/display on front face of each slab
    const screenGeo = new THREE.PlaneGeometry(slabWidth * 0.6, slabHeight * 0.15);
    const screenMat = new THREE.MeshBasicMaterial({
      color: new THREE.Color('#4FC3F7'),
      transparent: true,
      opacity: 0.3,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    });
    const screen = new THREE.Mesh(screenGeo, screenMat);
    screen.position.set(0, slabHeight * 0.15, slabDepth * 0.51);
    screen.name = `tars-screen-${i}`;
    slabGroup.add(screen);

    // Position slabs side by side
    slabGroup.position.x = (i - 1.5) * (slabWidth + 0.02 * s);

    // Pivot point at bottom of slab (for walking rotation)
    geo.translate(0, slabHeight / 2, 0);
    wireGeo.translate(0, slabHeight / 2, 0);

    slabs.push(slabGroup);
    group.add(slabGroup);
  }

  // Center vertically
  group.position.y = -slabHeight / 2;

  // "Eye" — a small light on the top of the second slab
  const eyeGeo = new THREE.SphereGeometry(0.1 * s, 8, 8);
  const eyeMat = new THREE.MeshBasicMaterial({
    color: new THREE.Color('#4FC3F7'),
    transparent: true,
    opacity: 0.9,
    blending: THREE.AdditiveBlending,
  });
  const eye = new THREE.Mesh(eyeGeo, eyeMat);
  eye.position.set(slabs[1].position.x, slabHeight + 0.15 * s, slabDepth * 0.6);
  eye.name = 'tars-eye';
  group.add(eye);

  group.userData.parts = { slabs, eye, scale: s, slabHeight };
  group.userData.mode = 'standing' as TarsMode;
  group.userData.walkPhase = 0;
  group.userData.modeTimer = 0;
  group.userData.lastModeSwitch = 0;

  return group;
}

// ---------------------------------------------------------------------------
// Animate TARS each frame
// ---------------------------------------------------------------------------
export function updateTarsRobot(
  group: THREE.Group,
  time: number,
): void {
  const ud = group.userData;
  const parts = ud.parts as {
    slabs: THREE.Group[];
    eye: THREE.Mesh;
    scale: number;
    slabHeight: number;
  } | undefined;
  if (!parts) return;

  const s = parts.scale;
  const { slabs, eye } = parts;

  // ── Auto-cycle modes ──
  if (time - ud.lastModeSwitch > 6 + Math.random() * 8) {
    const modes: TarsMode[] = ['standing', 'walking', 'spinning', 'compact'];
    ud.mode = modes[Math.floor(Math.random() * modes.length)];
    ud.lastModeSwitch = time;
  }

  const mode = ud.mode as TarsMode;

  // ── Slab animation by mode ──
  if (mode === 'standing') {
    // Gentle idle sway
    slabs.forEach((slab, i) => {
      slab.rotation.z = Math.sin(time * 0.5 + i * 0.8) * 0.02;
      slab.rotation.x = 0;
      slab.position.y = 0;
    });
    group.rotation.y = Math.sin(time * 0.3) * 0.1;

  } else if (mode === 'walking') {
    // TARS walking — slabs pivot alternately like legs
    const walkSpeed = 3;
    slabs.forEach((slab, i) => {
      const phase = (i % 2 === 0) ? 0 : Math.PI;
      const swing = Math.sin(time * walkSpeed + phase) * 0.25;
      slab.rotation.x = swing;
    });
    // Bob up and down
    group.position.y = -parts.slabHeight / 2 + Math.abs(Math.sin(time * walkSpeed)) * 0.3 * s;
    group.rotation.y += 0.005; // slow turn while walking

  } else if (mode === 'spinning') {
    // TARS wheel mode — rapid rotation
    group.rotation.z = time * 4;
    slabs.forEach((slab) => {
      slab.rotation.x = 0;
      slab.rotation.z = 0;
    });

  } else if (mode === 'compact') {
    // Slabs fold together
    slabs.forEach((slab, i) => {
      const target = (i - 1.5) * 0.05 * s;
      slab.position.x += (target - slab.position.x) * 0.05;
      slab.rotation.z = 0;
      slab.rotation.x = 0;
    });
    group.rotation.y = Math.sin(time * 0.2) * 0.05;
    group.rotation.z *= 0.95; // dampen any spin
  }

  // ── Eye pulse ──
  const eyePulse = 0.7 + Math.sin(time * 2) * 0.3;
  (eye.material as THREE.MeshBasicMaterial).opacity = eyePulse;
  // Eye tracks rotation
  eye.position.y = parts.slabHeight + 0.15 * s + Math.sin(time * 1.5) * 0.05 * s;

  // ── Screen flicker on each slab ──
  slabs.forEach((slab, i) => {
    const screen = slab.getObjectByName(`tars-screen-${i}`);
    if (screen) {
      const mat = (screen as THREE.Mesh).material as THREE.MeshBasicMaterial;
      const flicker = 0.2 + Math.sin(time * 4 + i * 2) * 0.15;
      mat.opacity = mode === 'spinning' ? 0.6 : flicker;
    }
  });

  // TSL holographic slabs use built-in `time` node — no manual update needed.

  // ── Holographic glitch ──
  if (Math.sin(Math.floor(time * 7) * 99999.1) > 0.96) {
    group.position.x += (Math.random() - 0.5) * 0.15 * s;
  }
}
