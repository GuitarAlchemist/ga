// src/components/PrimeRadiant/shaders/HolographicTSL.ts
// Shared holographic material for DemerzelFace, DemerzelRiggedFace, and TarsRobot.
// Fresnel + scanlines + flicker + optional speaking pulse.
// Renderer-agnostic: auto-compiles to GLSL (WebGL2) or WGSL (WebGPU).

import * as THREE from 'three';
import { MeshBasicNodeMaterial } from 'three/webgpu';
import {
  Fn, float, vec3, uniform,
  normalWorld, cameraPosition, positionWorld,
  sin, pow, abs, dot, fract, floor, mix, time,
} from 'three/tsl';

export interface HolographicOptions {
  color?: THREE.ColorRepresentation;
  opacity?: number;
  wireframe?: boolean;
  /** Enable TARS data-stream overlay */
  dataStream?: boolean;
}

/**
 * Create a holographic TSL material with Fresnel glow, scanlines, flicker,
 * and speaking pulse support.
 *
 * The returned material exposes `userData.speakingUniform` so callers can
 * drive the speaking pulse: `material.userData.speakingUniform.value = 1.0`.
 */
export function createHolographicMaterialTSL(options: HolographicOptions = {}): MeshBasicNodeMaterial {
  const {
    color = 0xffaa33,
    opacity = 0.6,
    wireframe = false,
    dataStream = false,
  } = options;

  const material = new MeshBasicNodeMaterial();

  const uColor = uniform(new THREE.Color(color));
  const uOpacity = uniform(opacity);
  const uSpeaking = uniform(0.0);

  // Warm rim tint — gold-amber accent on Fresnel edges
  const rimTint = vec3(1.0, 0.85, 0.5);

  material.colorNode = Fn(() => {
    const viewDir = cameraPosition.sub(positionWorld).normalize();
    const fresnel = pow(float(1.0).sub(abs(normalWorld.dot(viewDir))), float(2.1));
    const worldY = positionWorld.y;

    // Scanlines
    const scan = pow(sin(worldY.mul(10.0).sub(time.mul(2.2))).mul(0.5).add(0.5), float(4.0)).mul(0.25);
    const fineScan = pow(sin(worldY.mul(45.0).sub(time.mul(4.5))).mul(0.5).add(0.5), float(8.0)).mul(0.12);

    // Flicker — pseudo-random per time step
    const flickerSeed = fract(sin(floor(time.mul(9.0)).mul(43758.5453)));
    const flickerRaw = float(1.0).sub(
      pow(float(1.0).sub(flickerSeed), float(8.0)).mul(0.65),
    );
    // When seed > 0.9 produce a dip; otherwise stay at 1.0
    // Simplified: smoothstep gives gradual falloff
    const flicker = mix(float(1.0), float(0.35), flickerSeed.step(0.9).oneMinus());

    // Speaking pulse
    const speakPulse = float(1.0).add(uSpeaking.mul(sin(time.mul(12.0)).mul(0.28)));

    // Color assembly
    const col = vec3(uColor).mul(
      float(0.5).add(fresnel.mul(0.7)).add(scan.mul(0.45)),
    ).mul(speakPulse).toVar();

    // Warm rim
    col.addAssign(rimTint.mul(fresnel.mul(0.4)));

    // TARS data stream overlay
    if (dataStream) {
      const worldZ = positionWorld.z;
      const worldX = positionWorld.x;
      const stream = sin(worldZ.mul(20.0).add(time.mul(3.0)))
        .mul(sin(worldX.mul(15.0).sub(time.mul(2.0))))
        .mul(0.15);
      col.addAssign(vec3(uColor).mul(stream).mul(2.0));
    }

    return col;
  })();

  material.opacityNode = Fn(() => {
    const viewDir = cameraPosition.sub(positionWorld).normalize();
    const fresnel = pow(float(1.0).sub(abs(normalWorld.dot(viewDir))), float(2.1));
    const worldY = positionWorld.y;

    const scan = pow(sin(worldY.mul(10.0).sub(time.mul(2.2))).mul(0.5).add(0.5), float(4.0)).mul(0.25);
    const fineScan = pow(sin(worldY.mul(45.0).sub(time.mul(4.5))).mul(0.5).add(0.5), float(8.0)).mul(0.12);

    const flickerSeed = fract(sin(floor(time.mul(9.0)).mul(43758.5453)));
    const flicker = mix(float(1.0), float(0.35), flickerSeed.step(0.9).oneMinus());

    return fresnel.mul(0.6).add(0.2).mul(uOpacity).mul(flicker).add(scan).add(fineScan);
  })();

  material.transparent = true;
  material.blending = THREE.AdditiveBlending;
  material.depthWrite = false;
  material.side = THREE.DoubleSide;
  material.wireframe = wireframe;

  // Expose speaking uniform for callers
  material.userData.speakingUniform = uSpeaking;

  return material;
}
