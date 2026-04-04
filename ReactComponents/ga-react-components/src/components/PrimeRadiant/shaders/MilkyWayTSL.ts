// src/components/PrimeRadiant/shaders/MilkyWayTSL.ts
// Galactic plane band — multi-layer procedural Milky Way shader.
// Coordinate transforms, spiral arms, dust lanes, HII regions, star density.
// Renderer-agnostic: auto-compiles to GLSL (WebGL2) or WGSL (WebGPU).

import * as THREE from 'three';
import { MeshBasicNodeMaterial } from 'three/webgpu';
import {
  Fn, float, vec3, vec2,
  normalWorld, positionWorld,
  sin, cos, pow, abs, mix, smoothstep, fract, floor, dot,
  min, max, sqrt, atan2, asin, exp,
} from 'three/tsl';
import { noise3, fbm6, fbm3 } from './TSLNoiseLib';

// ── Inline hash33: vec3 -> vec3 for star generation ──

const hash33 = Fn(([p_immutable]: [ReturnType<typeof vec3>]) => {
  const p = vec3(p_immutable).toVar();
  p.assign(vec3(
    dot(p, vec3(127.1, 311.7, 74.7)),
    dot(p, vec3(269.5, 183.3, 246.1)),
    dot(p, vec3(113.5, 271.9, 124.6)),
  ));
  return fract(sin(p).mul(43758.5453123));
});

/**
 * Create the Milky Way galactic band material.
 * Renders on a BackSide sphere — procedural galactic plane with spiral arms,
 * dust lanes, HII nebulae, and embedded stars.
 */
export function createMilkyWayMaterialTSL(): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();

  material.colorNode = Fn(() => {
    // World-space direction on sky sphere
    const dir = positionWorld.normalize().toVar();

    // ── Galactic coordinate transform ──
    // Tilt 60deg around X axis
    const tiltAngle = float(1.0472); // 60 * PI/180
    const ct = cos(tiltAngle);
    const st = sin(tiltAngle);
    const g = vec3(
      dir.x,
      dir.y.mul(ct).sub(dir.z.mul(st)),
      dir.y.mul(st).add(dir.z.mul(ct)),
    ).toVar();

    // Roll 0.35 rad around Z axis
    const rollAngle = float(0.35);
    const cr = cos(rollAngle);
    const sr = sin(rollAngle);
    const gRolled = vec3(
      g.x.mul(cr).sub(g.y.mul(sr)),
      g.x.mul(sr).add(g.y.mul(cr)),
      g.z,
    );
    g.assign(gRolled);

    // Galactic latitude / longitude
    const galLat = asin(g.y);
    const galLon = atan2(g.z, g.x);

    // ── Core brightness: Gaussian falloff from galactic plane ──
    const bandWidth = float(0.28);
    const coreBrightness = exp(galLat.mul(galLat).negate().div(bandWidth.mul(bandWidth).mul(2.0)));

    // ── Core bulge near Sagittarius (galLon ~ 0) ──
    const bulge = exp(galLon.mul(galLon).negate().div(float(0.8)));
    const bulgeWidening = float(1.0).add(bulge.mul(0.8));
    const wideBrightness = exp(
      galLat.mul(galLat).negate().div(bandWidth.mul(bulgeWidening).mul(bandWidth.mul(bulgeWidening)).mul(2.0)),
    );
    const brightness = max(coreBrightness, wideBrightness);

    // ── Spiral arm structure ──
    const armAngle = galLon;
    const arm1 = sin(armAngle.mul(2.0).add(1.2)).mul(0.5).add(0.5);
    const arm2 = sin(armAngle.mul(2.0).add(4.4)).mul(0.5).add(0.5);

    // FBM modulation along arms
    const armNoise = fbm6(vec3(galLon.mul(3.0), galLat.mul(8.0), float(0.0)));
    const armStrength = max(arm1, arm2).mul(float(0.6).add(armNoise.mul(0.4)));

    // Combine brightness with arms
    const galMask = brightness.mul(float(0.4).add(armStrength.mul(0.6)));

    // ── Dust lanes ──
    const dustCoord1 = vec3(galLon.mul(8.0), galLat.mul(20.0), float(1.5));
    const dustCoord2 = vec3(galLon.mul(12.0), galLat.mul(30.0), float(3.7));
    const dust1 = fbm6(dustCoord1);
    const dust2 = fbm3(dustCoord2);
    const dustLane = dust1.mul(0.7).add(dust2.mul(0.3));
    const dustMask = smoothstep(0.35, 0.65, dustLane);
    const dustAttenuation = float(1.0).sub(dustMask.mul(0.65).mul(brightness));

    // ── Color mixing ──
    // Warm cream core, cool blue-white arms
    const coreColor = vec3(1.0, 0.92, 0.75);
    const armColor = vec3(0.75, 0.82, 1.0);
    const baseColor = mix(armColor, coreColor, bulge.mul(0.7)).toVar();

    // ── HII nebulae (pink emission spots) ──
    const hiiCoord1 = vec3(galLon.mul(15.0), galLat.mul(25.0), float(7.3));
    const hiiCoord2 = vec3(galLon.mul(18.0), galLat.mul(28.0), float(11.1));
    const hiiRaw = fbm3(hiiCoord1).mul(0.6).add(fbm3(hiiCoord2).mul(0.4));
    const hiiSpots = smoothstep(0.62, 0.78, hiiRaw).mul(brightness);
    const hiiColor = vec3(0.9, 0.3, 0.5);
    baseColor.addAssign(hiiColor.mul(hiiSpots.mul(0.35)));

    // ── Star density along galactic plane ──
    const starCoord = vec3(galLon.mul(200.0), galLat.mul(200.0), float(0.0));
    const starCell = floor(starCoord);
    const starHash = hash33(starCell);
    const starFrac = fract(starCoord);
    const starDist = sqrt(
      starFrac.sub(starHash).dot(starFrac.sub(starHash)),
    );
    const starPoint = smoothstep(0.08, 0.0, starDist).mul(brightness).mul(0.8);

    // ── Final composition ──
    // Reduce overall brightness so the galactic band reads as a FAINT band of
    // light (like real night sky) instead of dominant cream clouds. Space is
    // dark — the MilkyWay should whisper, not shout.
    const col = baseColor.mul(galMask).mul(dustAttenuation).mul(0.35).toVar();
    col.addAssign(vec3(1.0, 0.98, 0.9).mul(starPoint).mul(0.6));

    // ── Outer glow: tight Gaussian, very faint, stays near the plane ──
    // Previous: width 0.6, intensity 0.15 → purple haze over the whole sky.
    // Now: width 0.25, intensity 0.04 → only a hint of glow bordering the band.
    const outerGlow = exp(galLat.mul(galLat).negate().div(float(0.25)));
    const glowNoise = fbm3(vec3(galLon.mul(2.0), galLat.mul(4.0), float(5.0)));
    col.addAssign(vec3(0.04, 0.03, 0.05).mul(outerGlow.mul(float(0.3).add(glowNoise.mul(0.15)))));

    return col;
  })();

  material.transparent = true;
  material.side = THREE.BackSide;
  material.depthWrite = false;
  material.blending = THREE.AdditiveBlending;

  return material;
}
