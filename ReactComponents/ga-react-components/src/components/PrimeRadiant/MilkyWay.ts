// src/components/PrimeRadiant/MilkyWay.ts
// Procedural Milky Way band — realistic galactic plane with core bulge,
// dust lanes, spiral arms, and concentrated star density.
// Renders on a large sphere behind the starfield for depth parallax.

import * as THREE from 'three';
import { createMilkyWayMaterialTSL } from './shaders/MilkyWayTSL';
import { createMilkyWayTextureMaterial } from './shaders/MilkyWayTextureTSL';
import { resolveTexturePath } from '../../assets/space';

const _MILKY_WAY_VERTEX = `
  varying vec3 vWorldPos;
  void main() {
    vWorldPos = position;
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  }
`;

const _MILKY_WAY_FRAGMENT = `
  varying vec3 vWorldPos;

  // --- Noise utilities ---
  vec3 hash33(vec3 p) {
    p = fract(p * vec3(443.897, 441.423, 437.195));
    p += dot(p, p.yzx + 19.19);
    return fract((p.xxy + p.yxx) * p.zyx);
  }

  float noise3d(vec3 p) {
    vec3 i = floor(p);
    vec3 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(
      mix(mix(dot(hash33(i), f), dot(hash33(i + vec3(1, 0, 0)), f - vec3(1, 0, 0)), f.x),
          mix(dot(hash33(i + vec3(0, 1, 0)), f - vec3(0, 1, 0)), dot(hash33(i + vec3(1, 1, 0)), f - vec3(1, 1, 0)), f.x), f.y),
      mix(mix(dot(hash33(i + vec3(0, 0, 1)), f - vec3(0, 0, 1)), dot(hash33(i + vec3(1, 0, 1)), f - vec3(1, 0, 1)), f.x),
          mix(dot(hash33(i + vec3(0, 1, 1)), f - vec3(0, 1, 1)), dot(hash33(i + vec3(1, 1, 1)), f - vec3(1, 1, 1)), f.x), f.y),
      f.z
    ) * 0.5 + 0.5;
  }

  float fbm(vec3 p) {
    float v = 0.0, a = 0.5;
    for (int i = 0; i < 5; i++) {
      v += a * noise3d(p);
      p *= 2.03;
      a *= 0.48;
    }
    return v;
  }

  float fbm3(vec3 p) {
    float v = 0.0, a = 0.5;
    for (int i = 0; i < 3; i++) {
      v += a * noise3d(p);
      p *= 2.1;
      a *= 0.5;
    }
    return v;
  }

  void main() {
    vec3 dir = normalize(vWorldPos);

    // --- Galactic coordinate transform ---
    // Tilt the galactic plane ~60 deg from horizontal (rotate around X then Z)
    float tiltAngle = 1.047;  // ~60 degrees
    float rollAngle = 0.35;   // slight roll for realism
    // Rotate around X
    vec3 g = vec3(
      dir.x,
      dir.y * cos(tiltAngle) - dir.z * sin(tiltAngle),
      dir.y * sin(tiltAngle) + dir.z * cos(tiltAngle)
    );
    // Rotate around Z
    g = vec3(
      g.x * cos(rollAngle) - g.y * sin(rollAngle),
      g.x * sin(rollAngle) + g.y * cos(rollAngle),
      g.z
    );

    // Galactic latitude (distance from plane) and longitude
    float galLat = asin(clamp(g.y, -1.0, 1.0));  // -PI/2 to PI/2
    float galLon = atan(g.z, g.x);                // -PI to PI

    // --- Core brightness: Gaussian falloff from galactic plane ---
    float bandWidth = 0.28;  // ~16 deg half-width
    float coreBrightness = exp(-galLat * galLat / (2.0 * bandWidth * bandWidth));

    // --- Core bulge (Sagittarius direction: galLon ~ 0) ---
    float bulgeWidth = 0.5;
    float bulgeLon = 0.0;  // center of galaxy
    float lonDist = galLon - bulgeLon;
    // Wrap around
    if (lonDist > 3.14159) lonDist -= 6.28318;
    if (lonDist < -3.14159) lonDist += 6.28318;
    float bulge = exp(-lonDist * lonDist / (2.0 * bulgeWidth * bulgeWidth));
    // Bulge widens the band and brightens it
    float bulgeFactor = 1.0 + 1.8 * bulge;
    float wideBand = exp(-galLat * galLat / (2.0 * bandWidth * bandWidth * bulgeFactor));

    // --- Spiral arm structure ---
    float armAngle = galLon + galLat * 0.3;
    float spiral1 = sin(armAngle * 2.0 + 1.5) * 0.5 + 0.5;
    float spiral2 = sin(armAngle * 2.0 - 1.0 + 3.14159) * 0.5 + 0.5;
    float arms = max(spiral1, spiral2);
    arms = smoothstep(0.3, 0.8, arms);

    // Noise modulation for arms
    float armNoise = fbm(g * 4.0 + vec3(2.3, 0.0, 1.7));
    arms *= (0.6 + 0.4 * armNoise);

    // --- Dust lanes — dark ribbons cutting through ---
    float dust1 = fbm(g * 8.0 + vec3(0.5, 3.2, -1.0));
    float dust2 = fbm(g * 12.0 + vec3(-2.1, 0.8, 4.3));
    float dustLane = smoothstep(0.42, 0.58, dust1) * smoothstep(0.38, 0.55, dust2);
    // Dust only visible within the band
    float dustMask = smoothstep(0.1, 0.5, wideBand);
    float dustDarken = 1.0 - 0.65 * dustLane * dustMask;

    // --- Combine brightness ---
    float brightness = wideBand * (0.5 + 0.5 * arms) * dustDarken;

    // --- Color: warm core, cool arms ---
    // Core: warm cream/gold
    vec3 coreColor = vec3(0.18, 0.14, 0.09);
    // Arms: cool blue-white
    vec3 armColor = vec3(0.10, 0.11, 0.15);
    // Mix based on bulge proximity
    vec3 bandColor = mix(armColor, coreColor, bulge * 0.7 + 0.3);

    // --- Pink nebulae spots (HII regions along the band) ---
    float nebSpot1 = fbm3(g * 15.0 + vec3(7.0, 1.0, 3.0));
    float nebSpot2 = fbm3(g * 18.0 + vec3(-3.0, 5.0, 8.0));
    float pinkNeb = smoothstep(0.6, 0.75, nebSpot1) * wideBand * 0.4;
    float pinkNeb2 = smoothstep(0.62, 0.78, nebSpot2) * wideBand * 0.3;
    vec3 pinkColor = vec3(0.15, 0.03, 0.06);

    // --- Star density along galactic plane ---
    // Hash-based point stars concentrated near the plane
    float starDensity = smoothstep(0.0, 0.6, wideBand);
    vec3 starHash = hash33(floor(dir * 800.0));
    float star = step(0.997 - 0.003 * starDensity, starHash.x);
    // Vary star brightness
    float starBright = star * (0.3 + 0.7 * starHash.y) * (0.5 + 0.5 * starDensity);
    vec3 starColor = mix(vec3(0.8, 0.85, 1.0), vec3(1.0, 0.9, 0.7), starHash.z);

    // --- Final compositing ---
    vec3 col = bandColor * brightness;
    col += pinkColor * pinkNeb;
    col += pinkColor * 0.8 * pinkNeb2;
    col += starColor * starBright * 0.15;

    // Soft outer glow beyond the main band
    float outerGlow = exp(-galLat * galLat / (2.0 * 0.6 * 0.6));
    col += vec3(0.008, 0.007, 0.012) * outerGlow * fbm3(g * 2.0);

    gl_FragColor = vec4(col, 1.0);
  }
`;

/**
 * Create a Milky Way band mesh.
 *
 * Two paths:
 *  - Texture mode (default): photographic panorama from Solar System Scope
 *    (composite of real ESO/NASA imagery). Loaded from /textures/milky-way-8k.jpg.
 *  - Procedural fallback: the original TSL shader with spiral arms, HII regions,
 *    dust lanes. Used if texture loading fails.
 *
 * @param radius Sphere radius (should be > skybox sphere, e.g. 8000)
 * @param mode 'texture' | 'procedural' — default 'texture'
 */
export function createMilkyWay(
  radius: number = 8000,
  mode: 'texture' | 'procedural' = 'texture',
): THREE.Mesh {
  const geo = new THREE.SphereGeometry(radius, 48, 48);

  const mesh = new THREE.Mesh(geo, createMilkyWayMaterialTSL());
  if (mode === 'texture') {
    const canonicalPath = resolveTexturePath('milky-way', 'stars', '8k');
    if (!canonicalPath) {
      console.warn('[MilkyWay] canonical panorama missing, keeping procedural');
      mesh.name = 'milky-way';
      mesh.renderOrder = -3;
      mesh.rotation.x = 1.047;
      mesh.rotation.z = 0.35;
      return mesh;
    }

    // Hide mesh until texture finishes loading — default 1×1 white placeholder
    // would otherwise fill the whole view with white behind the scene.
    mesh.visible = false;
    new THREE.TextureLoader().load(
      canonicalPath,
      (tex) => {
        mesh.material = createMilkyWayTextureMaterial(tex, {
          brightness: 0.7,
          saturation: 0.9,
          flipU: true,
        });
        mesh.visible = true;
      },
      undefined,
      (err) => {
        console.warn('[MilkyWay] texture load failed, keeping procedural:', err);
        mesh.visible = true; // fall back to the procedural material we started with
      },
    );
  }
  mesh.name = 'milky-way';
  mesh.renderOrder = -3; // behind sky-nebula (-2)
  // Rotate sphere so galactic plane tilts ~60° (matches procedural version's
  // embedded transform, so switching modes doesn't reorient the sky).
  mesh.rotation.x = 1.047;
  mesh.rotation.z = 0.35;
  return mesh;
}
