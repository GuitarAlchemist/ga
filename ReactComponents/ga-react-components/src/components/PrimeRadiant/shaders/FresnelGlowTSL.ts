// src/components/PrimeRadiant/shaders/FresnelGlowTSL.ts
// TSL Fresnel glow materials — shared factory for corona, atmosphere, titan, markers.
// Renderer-agnostic: auto-compiles to GLSL (WebGL2) or WGSL (WebGPU).

import * as THREE from 'three';
import { MeshBasicNodeMaterial } from 'three/webgpu';
import {
  Fn, float, vec3, uniform,
  normalWorld, cameraPosition, positionWorld,
  pow, abs, sin, mix, time,
} from 'three/tsl';

// ── Sun Corona Glow ──

/**
 * Subtle limb glow around the sun. Fresnel-based, additive blending, backside.
 * Replaces the inline GLSL corona ShaderMaterial in SolarSystem.ts.
 */
export function createCoronaMaterialTSL(): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();

  material.colorNode = Fn(() => {
    const viewDir = cameraPosition.sub(positionWorld).normalize();
    const f = float(1.0).sub(abs(normalWorld.dot(viewDir)));
    const intensity = pow(f, float(3.5)).mul(0.15);
    return vec3(intensity.mul(1.0), intensity.mul(0.7), intensity.mul(0.2));
  })();

  material.transparent = true;
  material.side = THREE.BackSide;
  material.depthWrite = false;
  material.blending = THREE.AdditiveBlending;

  return material;
}

// ── Atmosphere Fresnel ──

export interface AtmosphereOptions {
  color: [number, number, number];
  intensity: number;
  power: number;
}

/**
 * Atmospheric Fresnel glow for planets. Parameterized color, intensity, power.
 * Replaces the inline GLSL atmosphere ShaderMaterial in SolarSystem.ts.
 */
export function createAtmosphereMaterialTSL(opts: AtmosphereOptions): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();

  const uColor = uniform(new THREE.Color(opts.color[0], opts.color[1], opts.color[2]));
  const uIntensity = uniform(opts.intensity);
  const uPower = uniform(opts.power);

  material.colorNode = Fn(() => {
    const viewDir = cameraPosition.sub(positionWorld).normalize();
    const fresnel = float(1.0).sub(abs(normalWorld.dot(viewDir)));
    const atmo = pow(fresnel, uPower).mul(uIntensity);
    return vec3(uColor).mul(atmo);
  })();

  // No opacityNode needed — AdditiveBlending on BackSide makes opacity redundant
  // (RGB intensity already encodes the Fresnel falloff)

  material.transparent = true;
  material.side = THREE.BackSide;
  material.depthWrite = false;
  material.blending = THREE.AdditiveBlending;

  return material;
}

// ── Titan Atmosphere ──

/**
 * Titan's orange haze. Fixed Fresnel params — no uniforms needed.
 * Replaces the inline GLSL titan atmosphere ShaderMaterial in SolarSystem.ts.
 */
export function createTitanAtmosphereMaterialTSL(): MeshBasicNodeMaterial {
  return createAtmosphereMaterialTSL({
    color: [0.85, 0.6, 0.2],
    intensity: 0.35,
    power: 3.0,
  });
}

// ── Location Marker Pin ──

/**
 * Pulsing Fresnel marker for Earth location pins.
 * Replaces the inline GLSL marker ShaderMaterial in SolarSystem.ts.
 */
export function createMarkerMaterialTSL(): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();

  material.colorNode = Fn(() => {
    const viewDir = cameraPosition.sub(positionWorld).normalize();
    const pulse = float(0.7).add(sin(time.mul(3.0)).mul(0.3));
    const fresnel = pow(float(1.0).sub(abs(normalWorld.dot(viewDir))), float(2.0));
    const color = mix(vec3(0.2, 0.8, 1.0), vec3(1.0, 1.0, 1.0), fresnel);
    return color.mul(pulse);
  })();

  material.transparent = false;

  return material;
}
