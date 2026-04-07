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

    // ── Spiral arm structure (4-arm: 2 major + 2 minor) ──
    const armAngle = galLon;
    // Major arms — tighter winding, stronger contrast
    const arm1 = sin(armAngle.mul(2.0).add(1.2)).mul(0.5).add(0.5);
    const arm2 = sin(armAngle.mul(2.0).add(4.4)).mul(0.5).add(0.5);
    const majorArms = max(arm1, arm2);
    // Minor arms — 45deg offset, 60% intensity
    const arm3 = sin(armAngle.mul(2.0).add(1.2 + 0.785)).mul(0.5).add(0.5);
    const arm4 = sin(armAngle.mul(2.0).add(4.4 + 0.785)).mul(0.5).add(0.5);
    const minorArms = max(arm3, arm4).mul(0.6);

    // FBM modulation along arms
    const armNoise = fbm6(vec3(galLon.mul(3.0), galLat.mul(8.0), float(0.0)));
    const combinedArms = max(majorArms, minorArms);
    // Raise arm contrast: pow(arms, 0.7) sharpens peaks, higher amplitude
    const armStrength = pow(combinedArms, float(0.7)).mul(float(0.5).add(armNoise.mul(0.5)));

    // Combine brightness with arms — deeper valleys between arms
    const galMask = brightness.mul(float(0.25).add(armStrength.mul(0.75)));

    // ── Dust lanes (large-scale + local dark clouds) ──
    const dustCoord1 = vec3(galLon.mul(8.0), galLat.mul(20.0), float(1.5));
    const dustCoord2 = vec3(galLon.mul(12.0), galLat.mul(30.0), float(3.7));
    const dust1 = fbm6(dustCoord1);
    const dust2 = fbm3(dustCoord2);
    const dustLane = dust1.mul(0.7).add(dust2.mul(0.3));
    // Tighter smoothstep for sharper dust lanes
    const dustMask = smoothstep(0.30, 0.55, dustLane);
    // Secondary smaller-scale dark clouds
    const localDustCoord = vec3(galLon.mul(25.0), galLat.mul(50.0), float(9.2));
    const localDust = fbm3(localDustCoord);
    const localDustMask = smoothstep(0.4, 0.6, localDust).mul(0.35);
    // Combined dust — stronger attenuation (0.75 max vs 0.65)
    const totalDust = min(dustMask.add(localDustMask), float(1.0));
    const dustAttenuation = float(1.0).sub(totalDust.mul(0.75).mul(brightness));

    // ── Color mixing ──
    // Warm golden core, cool blue arms — higher contrast
    const coreColor = vec3(1.0, 0.85, 0.55);
    const armColor = vec3(0.65, 0.75, 1.0);
    const baseColor = mix(armColor, coreColor, bulge.mul(0.7)).toVar();

    // ── HII nebulae (pink emission spots) — boosted intensity ──
    const hiiCoord1 = vec3(galLon.mul(15.0), galLat.mul(25.0), float(7.3));
    const hiiCoord2 = vec3(galLon.mul(18.0), galLat.mul(28.0), float(11.1));
    const hiiRaw = fbm3(hiiCoord1).mul(0.6).add(fbm3(hiiCoord2).mul(0.4));
    const hiiSpots = smoothstep(0.58, 0.75, hiiRaw).mul(brightness);
    const hiiColor = vec3(0.95, 0.3, 0.5);
    baseColor.addAssign(hiiColor.mul(hiiSpots.mul(0.45)));

    // ── Blue reflection nebulae (near bright star regions) ──
    const reflCoord = vec3(galLon.mul(20.0), galLat.mul(35.0), float(13.7));
    const reflRaw = fbm3(reflCoord);
    const reflSpots = smoothstep(0.65, 0.80, reflRaw).mul(brightness).mul(armStrength);
    const reflColor = vec3(0.4, 0.55, 1.0);
    baseColor.addAssign(reflColor.mul(reflSpots.mul(0.25)));

    // ── Dark nebula silhouettes (Coalsack-style patches) ──
    const darkNebCoord = vec3(galLon.mul(10.0), galLat.mul(18.0), float(17.3));
    const darkNebRaw = fbm3(darkNebCoord);
    const darkNebMask = smoothstep(0.68, 0.82, darkNebRaw).mul(brightness);
    baseColor.mulAssign(float(1.0).sub(darkNebMask.mul(0.5)));

    // ── Star density along galactic plane ──
    // Stars 3x denser inside spiral arms, denser near core
    const armDensityBoost = float(1.0).add(armStrength.mul(2.0));
    const coreDensityBoost = float(1.0).add(bulge.mul(1.5));
    const starDensity = armDensityBoost.mul(coreDensityBoost);

    const starCoord = vec3(galLon.mul(200.0), galLat.mul(200.0), float(0.0));
    const starCell = floor(starCoord);
    const starHash = hash33(starCell);
    const starFrac = fract(starCoord);
    const starDist = sqrt(
      starFrac.sub(starHash).dot(starFrac.sub(starHash)),
    );
    // Scale point size threshold by density — more stars resolve in dense regions
    const starThreshold = float(0.08).mul(sqrt(starDensity));
    const starPoint = smoothstep(starThreshold, float(0.0), starDist).mul(brightness).mul(0.8);

    // Warm-tint stars near core (gold) vs blue in arms
    const starWarmth = bulge.mul(0.6);
    const starTint = mix(vec3(0.9, 0.95, 1.0), vec3(1.0, 0.9, 0.7), starWarmth);

    // ── Extra-bright resolved stars (Sirius/Canopus analogs) ──
    const brightStarCoord = vec3(galLon.mul(50.0), galLat.mul(50.0), float(42.0));
    const brightCell = floor(brightStarCoord);
    const brightHash = hash33(brightCell);
    const brightFrac = fract(brightStarCoord);
    const brightDist = sqrt(
      brightFrac.sub(brightHash).dot(brightFrac.sub(brightHash)),
    );
    // Only top ~2% of cells get a bright star
    const brightGate = smoothstep(0.96, 1.0, brightHash.x);
    const brightStar = smoothstep(0.06, 0.0, brightDist).mul(brightness).mul(brightGate).mul(2.5);

    // ── Final composition ──
    // Reduce overall brightness so the galactic band reads as a FAINT band of
    // light (like real night sky) instead of dominant cream clouds. Space is
    // dark — the MilkyWay should whisper, not shout.
    const col = baseColor.mul(galMask).mul(dustAttenuation).mul(0.35).toVar();
    col.addAssign(starTint.mul(starPoint).mul(0.6));
    col.addAssign(vec3(0.95, 0.97, 1.0).mul(brightStar));

    // ── Outer glow: tight Gaussian, very faint, stays near the plane ──
    // Previous: width 0.6, intensity 0.15 → purple haze over the whole sky.
    // Now: width 0.25, intensity 0.04 → only a hint of glow bordering the band.
    const outerGlow = exp(galLat.mul(galLat).negate().div(float(0.25)));
    const glowNoise = fbm3(vec3(galLon.mul(2.0), galLat.mul(4.0), float(5.0)));
    col.addAssign(vec3(0.04, 0.03, 0.05).mul(outerGlow.mul(float(0.3).add(glowNoise.mul(0.15)))));

    // ── Contrast boost: mild S-curve ──
    col.assign(smoothstep(vec3(0.0, 0.0, 0.0), vec3(1.0, 1.0, 1.0), col.mul(1.2)));

    return col;
  })();

  material.transparent = true;
  material.side = THREE.BackSide;
  material.depthWrite = false;
  material.blending = THREE.AdditiveBlending;

  return material;
}
