// src/components/PrimeRadiant/SolarSystem.ts
// Procedural mini solar system — Sun + 8 planets + all major natural satellites
// NASA/Solar System Scope 2K textures (CC BY 4.0) with procedural fallbacks
// Orbit trails, Kepler speeds, realistic proportions

import * as THREE from 'three';
import { createAsteroidLOD, type AsteroidLODHandle } from './AsteroidLOD';
import { createSunMaterialTSL } from './shaders/SunMaterialTSL';
import { createPlanetSurfaceMaterialTSL, type AtmosphereType } from './shaders/PlanetSurfaceTSL';
import {
  createCoronaMaterialTSL,
  createAtmosphereMaterialTSL,
  createTitanAtmosphereMaterialTSL,
  createMarkerMaterialTSL,
} from './shaders/FresnelGlowTSL';
import { createSaturnRingsMaterialTSL } from './shaders/SaturnRingsTSL';
import { createOrbitTrailMaterialTSL } from './shaders/OrbitTrailTSL';
import { createAuroraMaterialTSL } from './shaders/AuroraTSL';
import { createRingGlowMaterialTSL } from './shaders/RingGlowTSL';
import { createStormVortexMaterialTSL } from './shaders/StormVortexTSL';
import {
  createRockyGreyMaterialTSL,
  createRockyDarkMaterialTSL,
  createRockyReddishMaterialTSL,
  createIcyWhiteMaterialTSL,
  createIcyBlueMaterialTSL,
  createIoMaterialTSL,
  createEuropaMaterialTSL,
  createGanymedeMaterialTSL,
  createCallistoMaterialTSL,
  createTitanMaterialTSL,
  createEnceladusMaterialTSL,
  createMimasMaterialTSL,
  createIapetusMaterialTSL,
  createTritonMaterialTSL,
  createMirandaMaterialTSL,
  createProceduralPlaceholderMaterialTSL,
} from './shaders/ProceduralMoonTSL';
import type { MeshBasicNodeMaterial } from 'three/webgpu';
import { resolveTexturePath } from '../../assets/space';

// ── Texture paths (served from public/textures/planets/) ──
const loader = new THREE.TextureLoader();

// ── Live weather cloud refresh interval (ms) ──
const CLOUD_REFRESH_MS = 15 * 60 * 1000; // 15 minutes
const ENABLE_EARTH_CLOUD_LAYERS = false;

function loadTex(path: string): THREE.Texture {
  const tex = loader.load(path);
  tex.colorSpace = THREE.SRGBColorSpace;
  tex.minFilter = THREE.LinearMipmapLinearFilter;
  tex.magFilter = THREE.LinearFilter;
  tex.anisotropy = 8; // sharper at oblique angles
  return tex;
}

function loadPlanetTexture(file: string): THREE.Texture {
  return loadTex(`/textures/planets/${file}`);
}

function loadCanonicalTexture(
  bodyId: 'sun' | 'mercury' | 'venus' | 'earth' | 'moon' | 'mars' | 'jupiter' | 'saturn' | 'uranus' | 'neptune' | 'stars',
  mapKind: 'albedo' | 'night' | 'clouds' | 'specular' | 'displacement' | 'alpha' | 'stars' | 'overlay',
  fallbackFile?: string,
): THREE.Texture | undefined {
  // Prefer 8K when available; resolveTexturePath falls back to 4K→2K→1K automatically
  const resolved = resolveTexturePath(bodyId, mapKind, '8k');
  if (resolved) return loadTex(resolved);
  if (fallbackFile) return loadPlanetTexture(fallbackFile);
  return undefined;
}

function getCanonicalBodyId(name: string):
  | 'sun'
  | 'mercury'
  | 'venus'
  | 'earth'
  | 'moon'
  | 'mars'
  | 'jupiter'
  | 'saturn'
  | 'uranus'
  | 'neptune'
  | 'stars'
  | undefined {
  switch (name) {
    case 'sun':
    case 'mercury':
    case 'venus':
    case 'earth':
    case 'moon':
    case 'mars':
    case 'jupiter':
    case 'saturn':
    case 'uranus':
    case 'neptune':
    case 'stars':
      return name;
    default:
      return undefined;
  }
}

// ── Shared vertex shader for procedural moons ──

// ── Astronomical data for planet tooltips ──
export interface PlanetAstroData {
  name: string;
  distanceAU: number;
  orbitalPeriodYears: number;
  diameterKm: number;
  type: string;
}

export const PLANET_ASTRO_DATA: Record<string, PlanetAstroData> = {
  sun: { name: 'Sun', distanceAU: 0, orbitalPeriodYears: 0, diameterKm: 1_392_700, type: 'Star (G2V)' },
  mercury: { name: 'Mercury', distanceAU: 0.39, orbitalPeriodYears: 0.24, diameterKm: 4_879, type: 'Terrestrial' },
  venus: { name: 'Venus', distanceAU: 0.72, orbitalPeriodYears: 0.62, diameterKm: 12_104, type: 'Terrestrial' },
  earth: { name: 'Earth', distanceAU: 1.0, orbitalPeriodYears: 1.0, diameterKm: 12_742, type: 'Terrestrial' },
  moon: { name: 'Moon', distanceAU: 1.0, orbitalPeriodYears: 0.075, diameterKm: 3_474, type: 'Satellite' },
  mars: { name: 'Mars', distanceAU: 1.52, orbitalPeriodYears: 1.88, diameterKm: 6_779, type: 'Terrestrial' },
  jupiter: { name: 'Jupiter', distanceAU: 5.2, orbitalPeriodYears: 11.86, diameterKm: 139_820, type: 'Gas giant' },
  saturn: { name: 'Saturn', distanceAU: 9.54, orbitalPeriodYears: 29.46, diameterKm: 116_460, type: 'Gas giant' },
  uranus: { name: 'Uranus', distanceAU: 19.2, orbitalPeriodYears: 84.01, diameterKm: 50_724, type: 'Ice giant' },
  neptune: { name: 'Neptune', distanceAU: 30.06, orbitalPeriodYears: 164.8, diameterKm: 49_244, type: 'Ice giant' },
};

interface MoonDef {
  name: string;
  radius: number;
  distance: number;
  speed: number;
  inclination?: number;
  fragment: string;
  texture?: string;              // optional texture file
  textureDisplacement?: string;  // height map for terrain displacement
  irregular?: {                  // non-spherical body shape
    elongation: [number, number, number]; // axis scale factors [x, y, z] (1.0 = sphere)
    roughness: number;            // noise amplitude (0-1)
    craters?: number;             // number of impact craters to carve
  };
}

interface PlanetDef {
  name: string;
  radius: number;
  distance: number;
  speed: number;
  tilt?: number;
  fragment: string;
  texture?: string;           // main color map
  textureNight?: string;      // night-side emission
  textureClouds?: string;     // cloud layer
  textureSpecular?: string;
  textureDisplacement?: string; // height/bump map for terrain displacement
  atmosphere?: { color: string; intensity: number; power: number };
  moons?: MoonDef[];
}

// ── Real orbital positions — J2000 mean longitude elements (NASA/JPL) ──
// Returns the current mean longitude in radians for a planet, so planets
// appear at their actual approximate positions in the sky right now.
function getRealOrbitalAngle(planetName: string): number {
  // J2000 epoch: 2000-01-01 12:00 TT
  const J2000 = Date.UTC(2000, 0, 1, 12, 0, 0);
  const now = Date.now();
  const daysSinceJ2000 = (now - J2000) / 86_400_000;
  const T = daysSinceJ2000 / 36525; // Julian centuries

  // Mean longitude at J2000 (L0, degrees) and rate (Ldot, degrees/century)
  // Source: Standish (1992) — JPL Planetary Ephemeris DE405
  const elements: Record<string, { L0: number; Ldot: number }> = {
    mercury:  { L0: 252.251,  Ldot: 149472.675 },
    venus:    { L0: 181.980,  Ldot:  58517.816 },
    earth:    { L0: 100.464,  Ldot:  35999.373 },
    mars:     { L0: 355.453,  Ldot:  19140.299 },
    jupiter:  { L0:  34.351,  Ldot:   3034.906 },
    saturn:   { L0:  49.558,  Ldot:   1222.114 },
    uranus:   { L0: 313.238,  Ldot:    428.267 },
    neptune:  { L0: 304.880,  Ldot:    218.486 },
  };

  const el = elements[planetName];
  if (!el) return 0;

  // Mean longitude in degrees, then convert to radians
  const L = (el.L0 + el.Ldot * T) % 360;
  return L * Math.PI / 180;
}

// ── Noise library for procedural moon shaders ──
const NOISE_LIB = `
float h(vec3 p){return fract(sin(dot(p,vec3(1.3,1.7,1.9)))*43758.5);}
float n(vec3 x){vec3 i=floor(x),f=fract(x);f=f*f*(3.-2.*f);return mix(mix(mix(h(i),h(i+vec3(1,0,0)),f.x),mix(h(i+vec3(0,1,0)),h(i+vec3(1,1,0)),f.x),f.y),mix(mix(h(i+vec3(0,0,1)),h(i+vec3(1,0,1)),f.x),mix(h(i+vec3(0,1,1)),h(i+vec3(1,1,1)),f.x),f.y),f.z);}
float fbm(vec3 p){float v=0.0,a=0.5;for(int i=0;i<5;i++,p*=2.){v+=a*n(p);a*=0.5;}return v;}
`;

// ── Procedural moon shader fragments ──
const ROCKY_GREY = `varying vec3 vPos;void main(){float c=fract(sin(dot(vPos*40.,vec3(12.99,78.23,45.16)))*43758.55);gl_FragColor=vec4(vec3(.55,.53,.5)*(.4+c*.6),1.);}`;
const ROCKY_DARK = `varying vec3 vPos;void main(){float c=fract(sin(dot(vPos*50.,vec3(9.1,37.2,71.8)))*43758.5);gl_FragColor=vec4(vec3(.3,.28,.26)*(.5+c*.5),1.);}`;
const ROCKY_REDDISH = `varying vec3 vPos;void main(){float c=fract(sin(dot(vPos*35.,vec3(15.7,42.3,8.9)))*43758.5);gl_FragColor=vec4(vec3(.5,.35,.25)*(.4+c*.6),1.);}`;
const ICY_WHITE = `varying vec3 vPos;void main(){float c=fract(sin(dot(vPos*30.,vec3(11.3,47.9,23.1)))*43758.5);gl_FragColor=vec4(vec3(.85,.87,.9)*(.7+c*.3),1.);}`;
const ICY_BLUE = `varying vec3 vPos;void main(){float c=fract(sin(dot(vPos*25.,vec3(7.3,31.7,59.1)))*43758.5);gl_FragColor=vec4(vec3(.6,.65,.75)*(.6+c*.4),1.);}`;

// Io — volcanic sulfur
const IO_FRAG = NOISE_LIB + `
  uniform float uTime;varying vec3 vPos;
  void main(){
    float v=fbm(vPos*4.);float spots=smoothstep(.55,.65,n(vPos*8.+uTime*.01));
    vec3 sulfur=mix(vec3(.9,.85,.2),vec3(.95,.6,.1),v);
    vec3 lava=vec3(.1,.05,.02);vec3 col=mix(sulfur,lava,spots);
    float hotspot=smoothstep(.7,.9,n(vPos*12.))*spots;
    col+=vec3(1.,.3,.05)*hotspot*.6;
    gl_FragColor=vec4(col,1.);}`;

// Europa — ice cracks
const EUROPA_FRAG = NOISE_LIB + `
  varying vec3 vPos;
  void main(){
    vec3 ice=vec3(.82,.85,.9);
    float crack1=abs(sin(vPos.x*15.+vPos.z*8.))*abs(cos(vPos.y*12.+vPos.x*6.));
    float crack2=abs(sin(vPos.z*18.+vPos.y*10.))*abs(cos(vPos.x*14.+vPos.z*7.));
    float cracks=smoothstep(.85,.95,max(crack1,crack2));
    vec3 col=mix(ice,vec3(.55,.3,.15),cracks*.7);
    col+=vec3(.05,.08,.12)*n(vPos*20.)*.3;
    gl_FragColor=vec4(col,1.);}`;

// Ganymede — grooved terrain
const GANYMEDE_FRAG = NOISE_LIB + `
  varying vec3 vPos;
  void main(){
    float t=fbm(vPos*3.);
    vec3 dark=vec3(.3,.28,.25);vec3 light=vec3(.7,.72,.75);
    vec3 col=mix(dark,light,smoothstep(.4,.6,t));
    float grooves=sin(vPos.y*30.+vPos.x*5.)*.5+.5;
    col*=.85+grooves*.15;
    gl_FragColor=vec4(col,1.);}`;

// Callisto — heavily cratered
const CALLISTO_FRAG = NOISE_LIB + `
  varying vec3 vPos;
  void main(){
    float t=fbm(vPos*5.);float craters=smoothstep(.6,.7,n(vPos*10.));
    vec3 col=vec3(.35,.32,.3)*(0.6+t*.4);
    col=mix(col,vec3(.55,.52,.5),craters*.4);
    gl_FragColor=vec4(col,1.);}`;

// Titan — orange haze
const TITAN_FRAG = NOISE_LIB + `
  uniform float uTime;varying vec3 vPos;
  void main(){
    float haze=fbm(vPos*2.5+vec3(uTime*.005,0,uTime*.003));
    vec3 col=mix(vec3(.7,.5,.15),vec3(.85,.65,.25),haze);
    float bands=sin(vPos.y*8.)*.5+.5;col*=.8+bands*.2;
    gl_FragColor=vec4(col,1.);}`;

// Enceladus — tiger stripes
const ENCELADUS_FRAG = NOISE_LIB + `
  varying vec3 vPos;
  void main(){
    vec3 ice=vec3(.92,.94,.97);
    float lat=vPos.y/length(vPos);
    float stripes=abs(sin(vPos.x*20.+vPos.z*15.))*smoothstep(-.8,-.5,lat);
    stripes=smoothstep(.85,.95,stripes);
    vec3 col=mix(ice,vec3(.4,.6,.85),stripes*.5);
    gl_FragColor=vec4(col,1.);}`;

// Mimas — Herschel crater
const MIMAS_FRAG = NOISE_LIB + `
  varying vec3 vPos;
  void main(){
    float c=n(vPos*20.)*.3;vec3 col=vec3(.6,.58,.55)*(.6+c);
    float crater=1.-smoothstep(.0,.25,length(vec2(vPos.x/length(vPos)-.7,vPos.y/length(vPos))));
    col=mix(col,vec3(.45,.42,.4),crater*.5);
    gl_FragColor=vec4(col,1.);}`;

// Iapetus — two-tone
const IAPETUS_FRAG = `
  varying vec3 vPos;
  void main(){
    float side=step(0.,vPos.z);
    vec3 dark=vec3(.12,.1,.08);vec3 bright=vec3(.75,.73,.7);
    gl_FragColor=vec4(mix(dark,bright,side),1.);}`;

// Triton — nitrogen ice
const TRITON_FRAG = NOISE_LIB + `
  varying vec3 vPos;
  void main(){
    float t=fbm(vPos*4.);
    vec3 ice=mix(vec3(.78,.7,.72),vec3(.85,.75,.78),t);
    float streaks=smoothstep(.6,.8,n(vPos*vec3(3,15,3)));
    vec3 col=mix(ice,vec3(.2,.15,.12),streaks*.4);
    gl_FragColor=vec4(col,1.);}`;

// Miranda — chaotic terrain
const MIRANDA_FRAG = NOISE_LIB + `
  varying vec3 vPos;
  void main(){
    float t=fbm(vPos*6.);float chaos=n(vPos*15.);
    vec3 col=vec3(.55,.53,.5)*(.5+t*.5);
    float chevron=smoothstep(.5,.7,abs(sin(vPos.x*10.+vPos.y*8.)));
    col=mix(col,vec3(.7,.68,.65),chevron*.3*chaos);
    gl_FragColor=vec4(col,1.);}`;

// ── Unused placeholder for procedural planet fallback ──
const PROC_PLACEHOLDER = `varying vec3 vPos;void main(){gl_FragColor=vec4(.5,.5,.5,1.);}`;

// ── Planet scaling ──
// Sizes: TRUE linear ratio to Earth (Jupiter really is 11x Earth).
//   Camera zoom handles visibility — no artificial inflation.
// Distances: sqrt(AU) compression — preserves relative ordering while keeping
//   the orrery visually compact. Linear AU makes Neptune 30x further than Earth,
//   which pushes the outer planets off-screen without extreme zoom.
// Speed: Kepler's 3rd law: angular speed ∝ AU^-1.5
// Orbits: Elliptical with real eccentricity from JPL data.
const EARTH_DIST = 6.5;     // 1 AU in scene units (sqrt(1) = 1)
const EARTH_RADIUS = 0.12;  // Earth's radius — small enough that inner orbits have visible gaps
const EARTH_SPEED = 3.0;    // Earth's orbital speed (animation, not real-time)

// Real orbital eccentricities (JPL/NASA)
const ECCENTRICITY: Record<string, number> = {
  mercury: 0.2056, venus: 0.0068, earth: 0.0167, mars: 0.0934,
  jupiter: 0.0489, saturn: 0.0565, uranus: 0.0457, neptune: 0.0113,
};

// Real orbital inclinations in radians (relative to ecliptic)
const INCLINATION: Record<string, number> = {
  mercury: 0.1222, venus: 0.0593, earth: 0, mars: 0.0323,
  jupiter: 0.0228, saturn: 0.0435, uranus: 0.0135, neptune: 0.0309,
};

function keplerDistance(au: number): number {
  // sqrt compression: preserves relative ordering, keeps outer planets on-screen.
  // Produces: Mercury 4.06, Venus 5.52, Earth 6.5, Mars 8.01, Jupiter 14.82,
  // Saturn 20.08, Uranus 28.48, Neptune 35.63 scene units.
  if (au <= 0) return 0;
  return EARTH_DIST * Math.sqrt(au);
}
function keplerRadius(diameterKm: number): number {
  // Linear ratio to Earth with a small minimum floor.
  // Jupiter is 11x Earth. Mercury is 0.38x Earth. Floor at 0.15x Earth prevents
  // the smallest moons from vanishing but keeps planets proportional.
  const ratio = diameterKm / 12_742;
  return EARTH_RADIUS * Math.max(ratio, 0.15);
}
function keplerSpeed(au: number): number {
  // Kepler's 3rd law: angular speed ∝ distance^-1.5
  if (au <= 0) return 0;
  return EARTH_SPEED * Math.pow(au, -1.5);
}

/** Solve Kepler's equation M = E - e*sin(E) via Newton's method */
function solveKepler(M: number, e: number): number {
  let E = M; // initial guess
  for (let i = 0; i < 10; i++) {
    const dE = (E - e * Math.sin(E) - M) / (1 - e * Math.cos(E));
    E -= dE;
    if (Math.abs(dE) < 1e-8) break;
  }
  return E;
}

/** Get position on elliptical orbit given mean anomaly and eccentricity */
function ellipticalPosition(meanAnomaly: number, e: number, semiMajor: number): { x: number; z: number } {
  const E = solveKepler(meanAnomaly, e);
  const cosE = Math.cos(E);
  const sinE = Math.sin(E);
  // Convert eccentric anomaly to heliocentric coords
  const x = semiMajor * (cosE - e);
  const z = semiMajor * Math.sqrt(1 - e * e) * sinE;
  return { x, z };
}

const PLANETS: PlanetDef[] = [
  {
    name: 'mercury',
    radius: keplerRadius(4_879),       // 0.22
    distance: keplerDistance(0.39),     // 4.06
    speed: keplerSpeed(0.39),          // 12.3 (fast!)
    texture: '2k_mercury.jpg',
    textureDisplacement: '2k_mercury_displacement.jpg',
    fragment: PROC_PLACEHOLDER,
  },
  {
    name: 'venus',
    radius: keplerRadius(12_104),      // 0.34
    distance: keplerDistance(0.72),     // 5.52
    speed: keplerSpeed(0.72),          // 4.91
    texture: '2k_venus_surface.jpg',
    textureDisplacement: '2k_venus_displacement.jpg',
    atmosphere: { color: '0.95, 0.75, 0.25', intensity: 0.45, power: 2.5 },
    fragment: PROC_PLACEHOLDER,
  },
  {
    name: 'earth',
    radius: EARTH_RADIUS,
    distance: EARTH_DIST,             // keplerDistance(1.0) = 6.5
    speed: EARTH_SPEED,               // keplerSpeed(1.0) = 3.0
    tilt: 0.4091,                     // 23.44° axial tilt in radians
    texture: '2k_earth_daymap.jpg',
    textureNight: '2k_earth_nightmap.jpg',
    textureClouds: '2k_earth_clouds.jpg',
    textureSpecular: '2k_earth_specular.jpg',
    textureDisplacement: '2k_earth_displacement.jpg',
    atmosphere: { color: '0.3, 0.6, 1.0', intensity: 0.55, power: 3.0 },
    fragment: PROC_PLACEHOLDER,
    moons: [
      { name: 'moon', radius: keplerRadius(3_474) * 2.5, distance: 0.5, speed: 2.0, texture: '2k_moon.jpg', textureDisplacement: '2k_moon_displacement.jpg', fragment: ROCKY_GREY },  // 2.5x size boost for orrery visibility (real ratio 0.27 is invisible)
    ],
  },
  {
    name: 'mars',
    radius: keplerRadius(6_779),       // 0.26
    distance: keplerDistance(1.52),     // 8.01
    speed: keplerSpeed(1.52),          // 1.60
    tilt: 0.4396,                      // 25.19° axial tilt
    texture: '2k_mars.jpg',
    textureDisplacement: '2k_mars_displacement.jpg',
    // No atmosphere shell — Mars's atmosphere is ~0.6% of Earth's, invisible from space
    fragment: PROC_PLACEHOLDER,
    moons: [
      { name: 'phobos', radius: 0.06, distance: 0.6, speed: 4.0, fragment: ROCKY_DARK },   // enlarged for visibility (real: 11km)
      { name: 'deimos', radius: 0.04, distance: 0.9, speed: 2.5, fragment: ROCKY_DARK },   // enlarged for visibility (real: 6km)
    ],
  },
  {
    name: 'jupiter',
    radius: keplerRadius(139_820),     // 1.16
    distance: keplerDistance(5.2),      // 14.82
    speed: keplerSpeed(5.2),           // 0.253
    texture: '2k_jupiter.jpg',
    atmosphere: { color: '0.9, 0.7, 0.4', intensity: 0.25, power: 3.5 },
    fragment: PROC_PLACEHOLDER,
    moons: [
      { name: 'io', radius: keplerRadius(3_643), distance: 1.8, speed: 3.5, fragment: IO_FRAG },
      { name: 'europa', radius: keplerRadius(3_122), distance: 2.3, speed: 2.8, fragment: EUROPA_FRAG },
      { name: 'ganymede', radius: keplerRadius(5_268), distance: 2.9, speed: 2.0, fragment: GANYMEDE_FRAG },
      { name: 'callisto', radius: keplerRadius(4_821), distance: 3.6, speed: 1.4, fragment: CALLISTO_FRAG },
      { name: 'amalthea', radius: 0.025, distance: 1.4, speed: 4.5, fragment: ROCKY_REDDISH },
      { name: 'thebe', radius: 0.015, distance: 1.55, speed: 4.2, fragment: ROCKY_DARK },
      { name: 'metis', radius: 0.01, distance: 1.25, speed: 5.0, fragment: ROCKY_DARK },
      { name: 'adrastea', radius: 0.008, distance: 1.28, speed: 4.9, fragment: ROCKY_DARK },
      { name: 'himalia', radius: 0.02, distance: 5.0, speed: 0.5, inclination: 0.5, fragment: ROCKY_DARK },
    ],
  },
  {
    name: 'saturn',
    radius: keplerRadius(116_460),     // 1.06
    distance: keplerDistance(9.54),     // 20.08
    speed: keplerSpeed(9.54),          // 0.102
    texture: '2k_saturn.jpg',
    atmosphere: { color: '0.85, 0.75, 0.5', intensity: 0.2, power: 3.5 },
    fragment: PROC_PLACEHOLDER,
    moons: [
      { name: 'mimas', radius: keplerRadius(396), distance: 1.6, speed: 4.0, fragment: MIMAS_FRAG, texture: 'moons/mimas.jpg' },
      { name: 'enceladus', radius: keplerRadius(504), distance: 1.9, speed: 3.5, fragment: ENCELADUS_FRAG, texture: 'moons/enceladus.jpg' },
      { name: 'tethys', radius: keplerRadius(1_066), distance: 2.2, speed: 3.0, fragment: ICY_WHITE, texture: 'moons/tethys.jpg' },
      { name: 'dione', radius: keplerRadius(1_123), distance: 2.6, speed: 2.5, fragment: ICY_WHITE, texture: 'moons/dione.jpg' },
      { name: 'rhea', radius: keplerRadius(1_527), distance: 3.1, speed: 2.0, fragment: ICY_WHITE, texture: 'moons/rhea.jpg' },
      { name: 'titan', radius: keplerRadius(5_150), distance: 3.8, speed: 1.3, fragment: TITAN_FRAG, texture: 'moons/titan.jpg' },
      { name: 'hyperion', radius: keplerRadius(270), distance: 4.3, speed: 1.1, fragment: ROCKY_GREY },
      { name: 'iapetus', radius: keplerRadius(1_469), distance: 5.0, speed: 0.7, inclination: 0.27, fragment: IAPETUS_FRAG, texture: 'moons/iapetus.jpg' },
      { name: 'phoebe', radius: 0.02, distance: 5.8, speed: 0.3, inclination: 2.7, fragment: ROCKY_DARK },
      { name: 'pan', radius: 0.006, distance: 1.15, speed: 5.5, fragment: ICY_WHITE },
      { name: 'atlas', radius: 0.006, distance: 1.18, speed: 5.4, fragment: ICY_WHITE },
      { name: 'prometheus', radius: 0.01, distance: 1.22, speed: 5.2, fragment: ICY_WHITE },
      { name: 'pandora', radius: 0.009, distance: 1.24, speed: 5.1, fragment: ICY_WHITE },
      { name: 'epimetheus', radius: 0.01, distance: 1.35, speed: 4.6, fragment: ICY_WHITE },
      { name: 'janus', radius: 0.012, distance: 1.36, speed: 4.5, fragment: ICY_WHITE },
      // Trojan moons (co-orbital with larger moons)
      { name: 'calypso', radius: 0.005, distance: 2.2, speed: 3.0, fragment: ICY_WHITE },    // Tethys trailing trojan
      { name: 'telesto', radius: 0.005, distance: 2.2, speed: 3.0, fragment: ICY_WHITE },    // Tethys leading trojan
      { name: 'helene', radius: 0.006, distance: 2.6, speed: 2.5, fragment: ICY_WHITE },     // Dione leading trojan
      { name: 'polydeuces', radius: 0.003, distance: 2.6, speed: 2.5, fragment: ICY_WHITE }, // Dione trailing trojan
      // Outer irregular moons
      { name: 'siarnaq', radius: 0.008, distance: 6.5, speed: 0.2, inclination: 0.8, fragment: ROCKY_DARK },
      { name: 'paaliaq', radius: 0.007, distance: 7.0, speed: 0.18, inclination: 0.7, fragment: ROCKY_DARK },
      { name: 'ymir', radius: 0.006, distance: 8.0, speed: -0.1, inclination: 2.8, fragment: ROCKY_DARK },   // retrograde
      { name: 'tarvos', radius: 0.005, distance: 7.5, speed: 0.15, inclination: 0.6, fragment: ROCKY_DARK },
      // Ring shepherds and inner small moons
      { name: 'daphnis', radius: 0.004, distance: 1.13, speed: 5.6, fragment: ICY_WHITE },   // Keeler gap shepherd
      { name: 'methone', radius: 0.003, distance: 1.7, speed: 3.8, fragment: ICY_WHITE },    // egg-shaped
      { name: 'anthe', radius: 0.002, distance: 1.75, speed: 3.7, fragment: ICY_WHITE },
      { name: 'pallene', radius: 0.003, distance: 1.8, speed: 3.6, fragment: ICY_WHITE },
      // Norse group (retrograde irregulars, >5km)
      { name: 'skathi', radius: 0.004, distance: 7.8, speed: -0.12, inclination: 2.6, fragment: ROCKY_DARK },
      { name: 'mundilfari', radius: 0.004, distance: 8.2, speed: -0.1, inclination: 2.7, fragment: ROCKY_DARK },
      { name: 'thrymr', radius: 0.004, distance: 8.5, speed: -0.09, inclination: 2.8, fragment: ROCKY_DARK },
      { name: 'narvi', radius: 0.004, distance: 8.8, speed: -0.08, inclination: 2.5, fragment: ROCKY_DARK },
      { name: 'suttungr', radius: 0.004, distance: 8.0, speed: -0.11, inclination: 2.9, fragment: ROCKY_DARK },
      { name: 'bergelmir', radius: 0.003, distance: 9.0, speed: -0.07, inclination: 2.7, fragment: ROCKY_DARK },
      { name: 'fornjot', radius: 0.003, distance: 9.5, speed: -0.06, inclination: 2.8, fragment: ROCKY_DARK },
      // Inuit group (prograde irregulars)
      { name: 'ijiraq', radius: 0.005, distance: 6.8, speed: 0.2, inclination: 0.8, fragment: ROCKY_DARK },
      { name: 'kiviuq', radius: 0.005, distance: 6.6, speed: 0.22, inclination: 0.8, fragment: ROCKY_DARK },
      // Gallic group
      { name: 'albiorix', radius: 0.008, distance: 7.2, speed: 0.16, inclination: 0.6, fragment: ROCKY_GREY },
      { name: 'erriapus', radius: 0.004, distance: 7.3, speed: 0.15, inclination: 0.6, fragment: ROCKY_DARK },
      { name: 'bebhionn', radius: 0.003, distance: 7.4, speed: 0.14, inclination: 0.6, fragment: ROCKY_DARK },
    ],
  },
  {
    name: 'uranus',
    radius: keplerRadius(50_724),      // 0.70
    distance: keplerDistance(19.2),     // 28.49
    speed: keplerSpeed(19.2),          // 0.0357
    tilt: 1.71,
    texture: '2k_uranus.jpg',
    atmosphere: { color: '0.4, 0.85, 0.75', intensity: 0.3, power: 3.0 },
    fragment: PROC_PLACEHOLDER,
    moons: [
      { name: 'miranda', radius: keplerRadius(472), distance: 1.0, speed: 3.5, fragment: MIRANDA_FRAG },
      { name: 'ariel', radius: keplerRadius(1_158), distance: 1.4, speed: 2.8, fragment: ICY_BLUE },
      { name: 'umbriel', radius: keplerRadius(1_170), distance: 1.8, speed: 2.2, fragment: ROCKY_DARK },
      { name: 'titania', radius: keplerRadius(1_578), distance: 2.3, speed: 1.6, fragment: ICY_BLUE },
      { name: 'oberon', radius: keplerRadius(1_523), distance: 2.8, speed: 1.2, fragment: ICY_BLUE },
      { name: 'puck', radius: 0.012, distance: 0.8, speed: 4.2, fragment: ROCKY_DARK },
      { name: 'portia', radius: 0.01, distance: 0.7, speed: 4.5, fragment: ROCKY_DARK },
    ],
  },
  {
    name: 'neptune',
    radius: keplerRadius(49_244),      // 0.69
    distance: keplerDistance(30.06),    // 35.63
    speed: keplerSpeed(30.06),         // 0.0182
    texture: '2k_neptune.jpg',
    atmosphere: { color: '0.2, 0.4, 1.0', intensity: 0.35, power: 3.0 },
    fragment: PROC_PLACEHOLDER,
    moons: [
      { name: 'triton', radius: keplerRadius(2_707), distance: 1.5, speed: -1.8, fragment: TRITON_FRAG },
      { name: 'proteus', radius: keplerRadius(420), distance: 0.9, speed: 3.0, fragment: ROCKY_DARK },
      { name: 'nereid', radius: 0.02, distance: 3.0, speed: 0.4, inclination: 0.5, fragment: ROCKY_GREY },
      { name: 'larissa', radius: 0.012, distance: 0.7, speed: 3.8, fragment: ROCKY_DARK },
      { name: 'despina', radius: 0.01, distance: 0.6, speed: 4.2, fragment: ROCKY_DARK },
      { name: 'galatea', radius: 0.01, distance: 0.65, speed: 4.0, fragment: ROCKY_DARK },
    ],
  },
];

// ── Moon tracking ──
interface MoonInstance {
  mesh: THREE.Mesh;
  def: MoonDef;
  planetMesh: THREE.Mesh;
  orbitGroup: THREE.Group;
}

// ── Planet shader: day/night terminator, bump mapping, specular, atmosphere, displacement ──
// All lighting computed in VIEW space so camera-parented groups work correctly.
// (World-space cameraPosition is the camera's own position — useless when the
//  solar system is a child of the camera.)

// Day/night with smooth terminator, bump-derived shading, specular, atmosphere rim
// All varyings are in VIEW space (camera-parenting safe).

// ── Map from legacy GLSL constant reference to TSL factory ──
// Moons declare `fragment: IO_FRAG` (etc.) — match by reference equality since
// these are all module-level string constants.
function getMoonMaterialTSL(fragmentSrc: string): MeshBasicNodeMaterial {
  if (fragmentSrc === IO_FRAG) return createIoMaterialTSL();
  if (fragmentSrc === EUROPA_FRAG) return createEuropaMaterialTSL();
  if (fragmentSrc === GANYMEDE_FRAG) return createGanymedeMaterialTSL();
  if (fragmentSrc === CALLISTO_FRAG) return createCallistoMaterialTSL();
  if (fragmentSrc === TITAN_FRAG) return createTitanMaterialTSL();
  if (fragmentSrc === ENCELADUS_FRAG) return createEnceladusMaterialTSL();
  if (fragmentSrc === MIMAS_FRAG) return createMimasMaterialTSL();
  if (fragmentSrc === IAPETUS_FRAG) return createIapetusMaterialTSL();
  if (fragmentSrc === TRITON_FRAG) return createTritonMaterialTSL();
  if (fragmentSrc === MIRANDA_FRAG) return createMirandaMaterialTSL();
  if (fragmentSrc === ROCKY_GREY) return createRockyGreyMaterialTSL();
  if (fragmentSrc === ROCKY_DARK) return createRockyDarkMaterialTSL();
  if (fragmentSrc === ROCKY_REDDISH) return createRockyReddishMaterialTSL();
  if (fragmentSrc === ICY_WHITE) return createIcyWhiteMaterialTSL();
  if (fragmentSrc === ICY_BLUE) return createIcyBlueMaterialTSL();
  return createProceduralPlaceholderMaterialTSL();
}

// ── Create a textured planet mesh with realistic shading ──
function createPlanetMesh(def: PlanetDef, scale: number): THREE.Mesh {
  // High segment count for smooth edges at all zoom levels (8K textures need it)
  const baseSegments = def.radius > 0.5 ? 128 : def.radius > 0.1 ? 96 : 64;
  const segments = def.textureDisplacement ? Math.max(baseSegments, 96) : baseSegments;
  const geo = new THREE.SphereGeometry(def.radius * scale, segments, segments);
  geo.computeBoundingSphere(); // pre-compute for zoom-to-planet framing

  if (def.texture) {
    const canonicalBodyId = getCanonicalBodyId(def.name);
    const albedoMap = canonicalBodyId
      ? loadCanonicalTexture(canonicalBodyId, 'albedo', def.texture)
      : loadPlanetTexture(def.texture);
    const nightMap = canonicalBodyId && def.textureNight
      ? loadCanonicalTexture(canonicalBodyId, 'night', def.textureNight)
      : def.textureNight ? loadPlanetTexture(def.textureNight) : undefined;
    const specularMap = canonicalBodyId && def.textureSpecular
      ? loadCanonicalTexture(canonicalBodyId, 'specular', def.textureSpecular)
      : def.textureSpecular ? loadPlanetTexture(def.textureSpecular) : undefined;
    const displacementMap = canonicalBodyId && def.textureDisplacement
      ? loadCanonicalTexture(canonicalBodyId, 'displacement', def.textureDisplacement)
      : def.textureDisplacement ? loadPlanetTexture(def.textureDisplacement) : undefined;

    const atmoType: AtmosphereType =
      def.name === 'earth' ? 'blue' : def.name === 'venus' ? 'orange' : 'none';
    const dispScale = def.textureDisplacement
      ? def.radius * scale * (def.name === 'mars' ? 0.12 : 0.05)
      : 0;
    const mat = createPlanetSurfaceMaterialTSL({
      map: albedoMap!,
      nightMap,
      specularMap,
      displacementMap,
      displacementScale: dispScale,
      isEarth: def.name === 'earth',
      atmosphereType: atmoType,
      roughness: def.name === 'earth' ? 0.6 : 0.85,
      textureSize: 2048,
    });
    // Initialize uMonth to current month for Earth seasonal snow
    mat.userData.monthUniform.value = new Date().getMonth() + 1;
    const mesh = new THREE.Mesh(geo, mat);
    mesh.userData.isPlanetShader = true;
    // Preserve the dispScale the material was built with so the
    // update loop can attenuate it by camera altitude without
    // losing the far-field target value. Without this, close-zoom
    // attenuation would need to know the original scale and there
    // is no other source of truth.
    mesh.userData.baseDispScale = dispScale;
    // Same for bump strength — the uniform default is 0.4 (matches
    // the legacy hardcoded value) and the update loop scales it
    // down toward 0 as the camera approaches the planet.
    mesh.userData.baseBumpStrength = 0.4;
    return mesh;
  }

  // Procedural fallback — use TSL moon-style material
  const mat = getMoonMaterialTSL(def.fragment);
  return new THREE.Mesh(geo, mat);
}

// ── Create a textured moon mesh, or fall back to procedural shader ──
function createMoonMesh(def: MoonDef, scale: number): THREE.Mesh {
  const baseSegments = def.texture ? (def.radius > 0.02 ? 48 : 32) : (def.radius > 0.02 ? 24 : 16);
  const segments = def.textureDisplacement ? Math.max(baseSegments, 48) : baseSegments;
  const geo = new THREE.SphereGeometry(def.radius * scale, segments, segments);

  if (def.texture) {
    const canonicalBodyId = getCanonicalBodyId(def.name);
    const map = canonicalBodyId
      ? loadCanonicalTexture(canonicalBodyId, 'albedo', def.texture)
      : loadPlanetTexture(def.texture);
    // MeshLambertMaterial — airless bodies get direct sunlight only, no ambient
    // environment response that would look like false atmosphere
    const matOpts: THREE.MeshLambertMaterialParameters = { map };
    if (def.textureDisplacement) {
      // Lambert doesn't support displacement — skip for moons (too small to matter)
    }
    const mat = new THREE.MeshLambertMaterial(matOpts);
    return new THREE.Mesh(geo, mat);
  }

  const mat = getMoonMaterialTSL(def.fragment);
  return new THREE.Mesh(geo, mat);
}

// ── Create orbit trail with per-vertex fading ──
function createOrbitTrail(distance: number, scale: number, color: number = 0x334466, planetName?: string): THREE.Line {
  const segments = 128;
  const positions = new Float32Array((segments + 1) * 3);
  const alphas = new Float32Array(segments + 1);
  const e = planetName ? (ECCENTRICITY[planetName] ?? 0) : 0;
  const incl = planetName ? (INCLINATION[planetName] ?? 0) : 0;
  for (let i = 0; i <= segments; i++) {
    const angle = (i / segments) * Math.PI * 2;
    // Elliptical orbit: r = a(1-e²) / (1 + e*cos(θ))
    const r = distance * (1 - e * e) / (1 + e * Math.cos(angle));
    const x = Math.cos(angle) * r * scale;
    const y = Math.sin(angle) * r * scale * Math.sin(incl); // inclination
    const z = Math.sin(angle) * r * scale * Math.cos(incl);
    positions[i * 3] = x;
    positions[i * 3 + 1] = y;
    positions[i * 3 + 2] = z;
    alphas[i] = 0.08;
  }
  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
  geo.setAttribute('aAlpha', new THREE.BufferAttribute(alphas, 1));

  const mat = createOrbitTrailMaterialTSL(color);
  const line = new THREE.Line(geo, mat);
  line.userData.trailSegments = segments;
  line.userData.trailDistance = distance;
  return line;
}

// ── Create planet name label as a canvas-based sprite — minimalist sci-fi ──
function createPlanetLabel(name: string, scale: number): THREE.Sprite {
  const canvas = document.createElement('canvas');
  const W = 512;
  const H = 128;
  canvas.width = W;
  canvas.height = H;
  const ctx = canvas.getContext('2d')!;

  const text = name.toUpperCase();
  ctx.font = '200 28px "Courier New", "SF Mono", "Fira Code", monospace';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.letterSpacing = '6px';

  const cx = W / 2;
  const cy = H / 2 - 6;

  // Subtle cyan glow pass
  ctx.shadowColor = 'rgba(100, 200, 255, 0.6)';
  ctx.shadowBlur = 20;
  ctx.fillStyle = 'rgba(100, 200, 255, 0.15)';
  ctx.fillText(text, cx, cy);
  ctx.fillText(text, cx, cy); // double pass for glow buildup

  // Main text — blue-white, thin
  ctx.shadowColor = 'rgba(100, 200, 255, 0.3)';
  ctx.shadowBlur = 8;
  ctx.fillStyle = 'rgba(220, 235, 255, 0.9)';
  ctx.fillText(text, cx, cy);

  // Clear shadows for crisp elements
  ctx.shadowColor = 'transparent';
  ctx.shadowBlur = 0;

  // Measure text width for scan-line and dot placement
  const metrics = ctx.measureText(text);
  const textW = metrics.width;
  const lineY = cy + 18;

  // Thin scan-line underneath
  const grad = ctx.createLinearGradient(cx - textW / 2 - 10, 0, cx + textW / 2 + 10, 0);
  grad.addColorStop(0, 'rgba(100, 200, 255, 0)');
  grad.addColorStop(0.2, 'rgba(100, 200, 255, 0.35)');
  grad.addColorStop(0.5, 'rgba(180, 220, 255, 0.5)');
  grad.addColorStop(0.8, 'rgba(100, 200, 255, 0.35)');
  grad.addColorStop(1, 'rgba(100, 200, 255, 0)');
  ctx.strokeStyle = grad;
  ctx.lineWidth = 1;
  ctx.beginPath();
  ctx.moveTo(cx - textW / 2 - 10, lineY);
  ctx.lineTo(cx + textW / 2 + 10, lineY);
  ctx.stroke();

  // Small dot accent before the name
  ctx.fillStyle = 'rgba(100, 220, 255, 0.7)';
  ctx.beginPath();
  ctx.arc(cx - textW / 2 - 18, cy, 2.5, 0, Math.PI * 2);
  ctx.fill();

  const tex = new THREE.CanvasTexture(canvas);
  tex.needsUpdate = true;
  const mat = new THREE.SpriteMaterial({
    map: tex,
    transparent: true,
    depthWrite: false,
    depthTest: false,
  });
  const sprite = new THREE.Sprite(mat);
  // Scale proportional to planet size — label is ~1.5x planet diameter
  const labelScale = Math.max(scale * 1.2, 0.015);
  sprite.scale.set(labelScale, labelScale * 0.25, 1);
  sprite.visible = false;
  sprite.name = `label-${name}`;
  return sprite;
}

// ─────────────────────────────────────────────────────────────
// CREATE
// ─────────────────────────────────────────────────────────────
export function createSolarSystem(scale: number): THREE.Group {
  const group = new THREE.Group();
  group.userData.isSolarSystem = true;
  group.userData.scale = scale;

  // ── Lighting for textured planets ──
  // Intensity bumped 1.5 → 4.0: planets were dim on their lit sides,
  // especially Mars/Mercury where texture albedo is low. Real sun is
  // overwhelmingly bright at these distances; 4.0 feels closer to that.
  // distance=0: infinite range so ALL moons receive sunlight.
  // decay=1: linear falloff (not inverse-square) so distant
  // moons still have a visible terminator. Planets are close
  // enough that linear vs quadratic is imperceptible.
  // Discovered by ix harness rendering-invariant auditor.
  const sunLight = new THREE.PointLight(0xffffff, 4.0, 0, 1);
  sunLight.position.set(0, 0, 0);
  group.add(sunLight);

  // Faint ambient so dark sides aren't pure black
  const ambient = new THREE.AmbientLight(0x111122, 0.15);
  group.add(ambient);

  // ── Sun (animated shader — roiling plasma with limb darkening) ──
  // Sun: visually larger than Jupiter but not engulfing inner orbits.
  // Real ratio is 10x Jupiter, but the orrery distance scale is compressed
  // (1 AU = 6.5 scene units, Sun radius at real scale = 5.7 units → swallows Earth).
  // Use 1.8x Jupiter at overview — Sun MUST be larger than Jupiter.
  // Real ratio is 10x but that would swallow inner orbits in the compressed orrery.
  // 1.8x gives a clearly dominant sun without overlapping Mercury's orbit.
  const jupiterVisualRadius = keplerRadius(139_820); // ~1.316
  const sunVisualRadius = jupiterVisualRadius * 1.8;  // Sun ~1.8x Jupiter at overview
  const sunGeo = new THREE.SphereGeometry(sunVisualRadius * scale, 48, 48);
  const sunTex = loadCanonicalTexture('sun', 'albedo', '2k_sun.jpg')!;

  // Sun material — TSL MeshBasicNodeMaterial (fully self-luminous, no lighting response).
  // Auto-compiles to GLSL on WebGL2 or WGSL on WebGPU.
  const sunMat = createSunMaterialTSL({ sunTexture: sunTex, quality: 'high' });
  const sun = new THREE.Mesh(sunGeo, sunMat);
  sun.name = 'sun';
  group.add(sun);

  // ── Sun lens flare — multi-element sprite flare with anamorphic streak ──
  const flareGroup = new THREE.Group();
  flareGroup.name = 'sun-flare';

  // Generate a radial gradient texture for flare elements
  function createFlareTexture(size: number, color: string, falloff: number): THREE.CanvasTexture {
    const c = document.createElement('canvas');
    c.width = c.height = size;
    const cx = c.getContext('2d')!;
    const g = cx.createRadialGradient(size/2, size/2, 0, size/2, size/2, size/2);
    g.addColorStop(0, color);
    g.addColorStop(falloff, color.replace(/[\d.]+\)$/, '0.15)'));
    g.addColorStop(1, 'rgba(0,0,0,0)');
    cx.fillStyle = g;
    cx.fillRect(0, 0, size, size);
    const t = new THREE.CanvasTexture(c);
    t.needsUpdate = true;
    return t;
  }

  // Primary glow — moderate halo, bloom amplifies. Opacity/size dialed
  // back from initial overzealous values per "too extreme" feedback.
  const mainGlow = new THREE.Sprite(new THREE.SpriteMaterial({
    map: createFlareTexture(256, 'rgba(255,220,150,0.12)', 0.4),
    transparent: true, depthWrite: false, blending: THREE.AdditiveBlending,
  }));
  mainGlow.scale.set(5 * scale, 5 * scale, 1);
  flareGroup.add(mainGlow);

  // Inner hot core — brighter central disc, still restrained.
  const hotCore = new THREE.Sprite(new THREE.SpriteMaterial({
    map: createFlareTexture(128, 'rgba(255,255,240,0.20)', 0.3),
    transparent: true, depthWrite: false, blending: THREE.AdditiveBlending,
  }));
  hotCore.scale.set(2.8 * scale, 2.8 * scale, 1);
  flareGroup.add(hotCore);

  // Warm outer aurora — very subtle broad glow; gives sun "presence"
  // when viewed from outer-planet distances without dominating the scene.
  const outerAurora = new THREE.Sprite(new THREE.SpriteMaterial({
    map: createFlareTexture(512, 'rgba(255,160,60,0.04)', 0.5),
    transparent: true, depthWrite: false, blending: THREE.AdditiveBlending,
  }));
  outerAurora.scale.set(8 * scale, 8 * scale, 1);
  flareGroup.add(outerAurora);

  // Anamorphic streak and rainbow ghosts removed — caused distracting
  // horizontal bar and halo artifacts at typical viewing distances.

  group.add(flareGroup);
  group.userData.flareGroup = flareGroup;

  // Sun corona glow (subtle — bloom post-process amplifies this)
  // Corona: just slightly larger than the Sun — subtle limb glow, not a giant halo
  const coronaGeo = new THREE.SphereGeometry(sunVisualRadius * 1.08 * scale, 16, 16);
  const coronaMat = createCoronaMaterialTSL();
  group.add(new THREE.Mesh(coronaGeo, coronaMat));

  // ── Planets + Moons ──
  const planetMeshes: { mesh: THREE.Mesh; orbit: THREE.Group; def: PlanetDef; clouds?: THREE.Mesh; cloudsHigh?: THREE.Mesh }[] = [];
  const moonInstances: MoonInstance[] = [];
  const planetLabels: Map<string, THREE.Sprite> = new Map();
  const orbitTrails: Map<string, THREE.Line> = new Map();

  for (const def of PLANETS) {
    const orbit = new THREE.Group();
    orbit.name = `orbit-${def.name}`;

    // Orbit trail (with per-vertex alpha for fading)
    const trail = createOrbitTrail(def.distance, scale, 0x334466, def.name);
    trail.name = `trail-${def.name}`;
    group.add(trail); // add to group root, not orbit (orbit rotates)
    orbitTrails.set(def.name, trail);

    const mesh = createPlanetMesh(def, scale);
    mesh.name = def.name;
    mesh.frustumCulled = false; // prevent disappearing on close zoom
    mesh.position.x = def.distance * scale;
    if (def.tilt) mesh.rotation.z = def.tilt;
    orbit.add(mesh);

    // Orbit path line — faint circle showing the orbital track
    // Elliptical orbit path with real eccentricity + inclination
    const orbitLineSegs = 128;
    const orbitLinePoints: THREE.Vector3[] = [];
    const eOrbit = ECCENTRICITY[def.name] ?? 0;
    const iOrbit = INCLINATION[def.name] ?? 0;
    for (let s = 0; s <= orbitLineSegs; s++) {
      const theta = (s / orbitLineSegs) * Math.PI * 2;
      // r = a(1-e²) / (1 + e·cos(θ)) — polar equation of ellipse
      const r = def.distance * (1 - eOrbit * eOrbit) / (1 + eOrbit * Math.cos(theta));
      const x = Math.cos(theta) * r * scale;
      const y = Math.sin(theta) * r * scale * Math.sin(iOrbit);
      const z = Math.sin(theta) * r * scale * Math.cos(iOrbit);
      orbitLinePoints.push(new THREE.Vector3(x, y, z));
    }
    const orbitLineGeo = new THREE.BufferGeometry().setFromPoints(orbitLinePoints);
    const orbitLineMat = new THREE.LineBasicMaterial({
      color: 0x4466aa,
      transparent: true,
      opacity: 0.35,
      depthWrite: false,
    });
    const orbitLine = new THREE.Line(orbitLineGeo, orbitLineMat);
    orbitLine.name = `${def.name}-orbit-path`;
    group.add(orbitLine); // add to group root (not orbit, since orbit rotates)

    // Real orbital position — compute mean longitude for current date (J2000 Keplerian elements)
    orbit.rotation.y = getRealOrbitalAngle(def.name);

    // Atmosphere shell — Fresnel glow on limb, transparent at center
    if (def.atmosphere) {
      const atmoSegs = def.radius > 0.5 ? 32 : 24;
      const atmoGeo = new THREE.SphereGeometry(def.radius * 1.06 * scale, atmoSegs, atmoSegs);
      const atmoColorParts = def.atmosphere.color.split(',').map(c => parseFloat(c.trim())) as [number, number, number];
      const atmoMat = createAtmosphereMaterialTSL({
        color: atmoColorParts,
        intensity: def.atmosphere.intensity,
        power: def.atmosphere.power,
      });
      const atmoMesh = new THREE.Mesh(atmoGeo, atmoMat);
      atmoMesh.name = `atmo-${def.name}`;
      // Parent to mesh so atmosphere follows planet through elliptical orbit
      mesh.add(atmoMesh);
    }

    // Planet name label (initially hidden)
    const label = createPlanetLabel(def.name, scale);
    label.position.copy(mesh.position);
    label.position.y += def.radius * scale + 0.03; // above the planet
    orbit.add(label);
    planetLabels.set(def.name, label);

    let cloudsMesh: THREE.Mesh | undefined;
    let cloudsHighMesh: THREE.Mesh | undefined;

    // Earth country borders overlay (initially hidden, toggled by user)
    if (def.name === 'earth') {
      const bordersTex = loadCanonicalTexture('earth', 'overlay');
      if (bordersTex) {
        bordersTex.colorSpace = THREE.SRGBColorSpace;
        const bordersMat = new THREE.MeshBasicMaterial({
          map: bordersTex,
          transparent: true,
          opacity: 0.7,
          depthWrite: false,
        });
        const bordersMesh = new THREE.Mesh(
          new THREE.SphereGeometry((def.radius + 0.005) * scale, 32, 32),
          bordersMat,
        );
        bordersMesh.name = 'earth-borders';
        bordersMesh.visible = false; // off by default
        bordersMesh.position.copy(mesh.position);
        orbit.add(bordersMesh);
      }
    }

    // Earth clouds — two layers for depth parallax
    // Cloud layers disabled — persistent z-fighting with surface at scale 0.06.
    // The planet shader's atmosphere (limb darkening + in-scattering) provides
    // the blue haze effect. Clouds caused 58% per-frame pixel changes at the limb.
    // Re-enable when WebGPURenderer provides proper depth precision.
    if (ENABLE_EARTH_CLOUD_LAYERS && def.name === 'earth' && def.textureClouds) {
      const cloudsTex = loadPlanetTexture(def.textureClouds);

      // Primary cloud layer — alphaMap for defined cloud gaps
      // At scale 0.06, Earth radius is ~0.007 units — need generous offset
      // to avoid z-fighting. polygonOffset provides additional GPU-level fix.
      const cloudsMat = new THREE.MeshStandardMaterial({
        map: cloudsTex,
        alphaMap: cloudsTex,
        transparent: true,
        opacity: 0.75,
        depthWrite: false,
        polygonOffset: true,
        polygonOffsetFactor: -1,
        polygonOffsetUnits: -1,
      });
      cloudsMesh = new THREE.Mesh(
        new THREE.SphereGeometry(def.radius * 1.04 * scale, 32, 32),
        cloudsMat,
      );
      cloudsMesh.name = 'earth-clouds';
      cloudsMesh.renderOrder = 1;
      // Parent to mesh so clouds follow Earth through elliptical orbit
      mesh.add(cloudsMesh);

      // High-altitude thin cloud layer — parallax depth
      const cloudsHighTex = loadPlanetTexture(def.textureClouds);
      const cloudsHighMat = new THREE.MeshStandardMaterial({
        map: cloudsHighTex,
        alphaMap: cloudsHighTex,
        transparent: true,
        opacity: 0.25,
        depthWrite: false,
        polygonOffset: true,
        polygonOffsetFactor: -2,
        polygonOffsetUnits: -2,
      });
      cloudsHighMesh = new THREE.Mesh(
        new THREE.SphereGeometry(def.radius * 1.07 * scale, 32, 32),
        cloudsHighMat,
      );
      cloudsHighMesh.name = 'earth-clouds-high';
      mesh.add(cloudsHighMesh);
    }

    // Saturn rings (textured)
    if (def.name === 'saturn') {
      // Saturn rings — lit ShaderMaterial with Cassini division, backlit glow, sun response
      const ringInner = 1.3, ringOuter = 2.2;
      const ringGeo = new THREE.RingGeometry(ringInner * scale, ringOuter * scale, 128, 1);
      // Remap UVs to radial 0-1 for ring texture
      const ringPos = ringGeo.attributes.position;
      const ringUv = ringGeo.attributes.uv;
      for (let i = 0; i < ringPos.count; i++) {
        const x = ringPos.getX(i), z = ringPos.getZ(i);
        const r = Math.sqrt(x * x + z * z);
        const rNorm = (r - ringInner * scale) / ((ringOuter - ringInner) * scale);
        ringUv.setXY(i, rNorm, 0.5);
      }
      const ringTex = loadCanonicalTexture('saturn', 'alpha', '2k_saturn_ring_alpha.png')!;
      const ringMat = createSaturnRingsMaterialTSL({ ringTexture: ringTex });
      const ring = new THREE.Mesh(ringGeo, ringMat);
      ring.name = 'saturn-ring';
      ring.rotation.x = Math.PI / 2; // flat equatorial plane
      mesh.add(ring);
    }

    // Uranus faint ring
    if (def.name === 'uranus') {
      const ringGeo = new THREE.RingGeometry(0.7 * scale, 0.9 * scale, 48);
      const ringMat = new THREE.MeshBasicMaterial({
        color: 0x99aabb, side: THREE.DoubleSide,
        transparent: true, opacity: 0.1, depthWrite: false,
      });
      const ring = new THREE.Mesh(ringGeo, ringMat);
      ring.rotation.x = Math.PI / 2;
      ring.rotation.z = 1.71;
      mesh.add(ring);
    }

    // Neptune faint ring
    if (def.name === 'neptune') {
      const ringGeo = new THREE.RingGeometry(0.65 * scale, 0.8 * scale, 48);
      const ringMat = new THREE.MeshBasicMaterial({
        color: 0x667799, side: THREE.DoubleSide,
        transparent: true, opacity: 0.06, depthWrite: false,
      });
      const ring = new THREE.Mesh(ringGeo, ringMat);
      ring.rotation.x = Math.PI / 2.05;
      mesh.add(ring);
    }

    // ── Create moons ──
    if (def.moons) {
      for (const moonDef of def.moons) {
        const moonOrbit = new THREE.Group();
        moonOrbit.name = `orbit-${moonDef.name}`;
        moonOrbit.position.copy(mesh.position);
        if (moonDef.inclination) moonOrbit.rotation.x = moonDef.inclination;

        const moonMesh = createMoonMesh(moonDef, scale);
        moonMesh.name = moonDef.name;
        moonMesh.position.x = moonDef.distance * scale;
        moonOrbit.add(moonMesh);

        // Moon label (initially hidden, shown on hover)
        const moonLabel = createPlanetLabel(moonDef.name, scale * 0.5);
        moonLabel.position.copy(moonMesh.position);
        moonLabel.position.y += moonDef.radius * scale + 0.015;
        moonOrbit.add(moonLabel);
        planetLabels.set(moonDef.name, moonLabel);

        // Titan atmosphere
        if (moonDef.name === 'titan') {
          const titanAtmoGeo = new THREE.SphereGeometry((moonDef.radius + 0.03) * scale, 12, 12);
          const titanAtmoMat = createTitanAtmosphereMaterialTSL();
          const titanAtmo = new THREE.Mesh(titanAtmoGeo, titanAtmoMat);
          titanAtmo.position.copy(moonMesh.position);
          moonOrbit.add(titanAtmo);
        }

        // Enceladus geyser glow
        if (moonDef.name === 'enceladus') {
          const geyserGeo = new THREE.ConeGeometry(0.015 * scale, 0.08 * scale, 8);
          const geyserMat = new THREE.MeshBasicMaterial({
            color: 0xaaddff, transparent: true, opacity: 0.25,
            blending: THREE.AdditiveBlending, depthWrite: false,
          });
          const geyser = new THREE.Mesh(geyserGeo, geyserMat);
          geyser.position.copy(moonMesh.position);
          geyser.position.y -= moonDef.radius * scale * 1.2;
          geyser.rotation.x = Math.PI;
          moonOrbit.add(geyser);
        }

        // Thin orbit track for moon — faint dashed circle
        const moonTrailSegments = 64;
        const moonTrailPositions = new Float32Array((moonTrailSegments + 1) * 3);
        for (let i = 0; i <= moonTrailSegments; i++) {
          const angle = (i / moonTrailSegments) * Math.PI * 2;
          moonTrailPositions[i * 3] = Math.cos(angle) * moonDef.distance * scale;
          moonTrailPositions[i * 3 + 1] = 0;
          moonTrailPositions[i * 3 + 2] = Math.sin(angle) * moonDef.distance * scale;
        }
        const moonTrailGeo = new THREE.BufferGeometry();
        moonTrailGeo.setAttribute('position', new THREE.BufferAttribute(moonTrailPositions, 3));
        const moonTrailMat = new THREE.LineBasicMaterial({
          color: 0x556688, transparent: true, opacity: 0.15, depthWrite: false,
        });
        const moonTrail = new THREE.Line(moonTrailGeo, moonTrailMat);
        moonOrbit.add(moonTrail);

        // Spread moons around orbit — deterministic phase from distance + speed
        moonOrbit.rotation.y = moonDef.distance * 3.14159 + moonDef.speed * 1.618;

        orbit.add(moonOrbit);
        moonInstances.push({ mesh: moonMesh, def: moonDef, planetMesh: mesh, orbitGroup: moonOrbit });
      }
    }

    group.add(orbit);
    planetMeshes.push({ mesh, orbit, def, clouds: cloudsMesh, cloudsHigh: cloudsHighMesh });
  }

  // ── Asteroid Belt — thousands of rocky fragments between Mars and Jupiter ──
  const BELT_INNER_AU = 2.1;
  const BELT_OUTER_AU = 3.3;
  const BELT_COUNT = 3000;
  const beltInner = keplerDistance(BELT_INNER_AU) * scale;
  const beltOuter = keplerDistance(BELT_OUTER_AU) * scale;

  const beltPositions = new Float32Array(BELT_COUNT * 3);
  const beltSizes = new Float32Array(BELT_COUNT);
  const beltColors = new Float32Array(BELT_COUNT * 3);

  for (let i = 0; i < BELT_COUNT; i++) {
    const angle = Math.random() * Math.PI * 2;
    const radius = beltInner + Math.random() * (beltOuter - beltInner);
    // Slight vertical spread (±5% of radius for belt thickness)
    const y = (Math.random() - 0.5) * radius * 0.05;

    const i3 = i * 3;
    beltPositions[i3] = Math.cos(angle) * radius;
    beltPositions[i3 + 1] = y;
    beltPositions[i3 + 2] = Math.sin(angle) * radius;

    // Size: mostly tiny, a few larger
    beltSizes[i] = (0.02 + Math.pow(Math.random(), 4) * 0.15) * scale;

    // Color: grey-brown rocky tones
    const grey = 0.3 + Math.random() * 0.3;
    beltColors[i3] = grey * 1.1;     // slightly warm
    beltColors[i3 + 1] = grey;
    beltColors[i3 + 2] = grey * 0.9; // slightly cool
  }

  const beltGeo = new THREE.BufferGeometry();
  beltGeo.setAttribute('position', new THREE.BufferAttribute(beltPositions, 3));
  beltGeo.setAttribute('size', new THREE.BufferAttribute(beltSizes, 1));
  beltGeo.setAttribute('color', new THREE.BufferAttribute(beltColors, 3));

  const beltMat = new THREE.PointsMaterial({
    size: 0.15 * scale,
    vertexColors: true,
    transparent: true,
    opacity: 0.6,
    sizeAttenuation: true,
    depthWrite: false,
  });
  const asteroidBelt = new THREE.Points(beltGeo, beltMat);
  asteroidBelt.name = 'asteroid-belt';
  group.add(asteroidBelt);

  // Named asteroids (larger, visible individually)
  const namedAsteroids = [
    { name: 'ceres', au: 2.77, radius: 0.04, color: 0x998877 },    // dwarf planet
    { name: 'vesta', au: 2.36, radius: 0.025, color: 0xaaaaaa },
    { name: 'pallas', au: 2.77, radius: 0.025, color: 0x887766 },
    { name: 'hygiea', au: 3.14, radius: 0.02, color: 0x776655 },
  ];
  for (const ast of namedAsteroids) {
    const dist = keplerDistance(ast.au) * scale;
    const angle = Math.random() * Math.PI * 2; // random orbital position
    const geo = new THREE.IcosahedronGeometry(ast.radius * scale, 0); // low-poly rock
    const mat = new THREE.MeshLambertMaterial({ color: ast.color });
    const mesh = new THREE.Mesh(geo, mat);
    mesh.name = ast.name;
    mesh.position.set(Math.cos(angle) * dist, 0, Math.sin(angle) * dist);
    // Irregular shape: random scale per axis
    mesh.scale.set(1 + Math.random() * 0.3, 0.7 + Math.random() * 0.3, 1 + Math.random() * 0.3);
    group.add(mesh);
  }

  // ── Asteroid LOD — detailed rock meshes fade in on close approach ──
  // The Points belt above handles far views; this adds ~50 displaced icosahedron
  // rocks that become visible when the camera is within ~100 units of the belt.
  const asteroidLOD = createAsteroidLOD(beltInner, beltOuter, beltInner * 0.05);
  group.add(asteroidLOD.group);
  group.userData.asteroidLOD = asteroidLOD;

  // ── Moon Transit Shadows (Galilean moons → Jupiter) ──
  // Dark disc meshes placed on Jupiter's surface where moons cast shadows.
  // Updated each frame in updateSolarSystem() based on sun-moon-planet geometry.
  const SHADOW_MOONS = ['io', 'europa', 'ganymede', 'callisto'];
  const jupiterPlanet = planetMeshes.find(p => p.def.name === 'jupiter');
  const shadowDiscs: Map<string, THREE.Mesh> = new Map();
  if (jupiterPlanet) {
    for (const moonName of SHADOW_MOONS) {
      const discGeo = new THREE.CircleGeometry(0.03 * scale, 16);
      const discMat = new THREE.MeshBasicMaterial({
        color: 0x000000, transparent: true, opacity: 0.6,
        depthWrite: false, side: THREE.DoubleSide,
      });
      const disc = new THREE.Mesh(discGeo, discMat);
      disc.name = `shadow-${moonName}`;
      disc.visible = false; // shown only when moon transits
      jupiterPlanet.mesh.add(disc);
      shadowDiscs.set(moonName, disc);
    }
  }
  group.userData.shadowDiscs = shadowDiscs;

  // ── Sun DirectionalLight for Lambert-material moons ──
  // MeshLambertMaterial computes shading from Three.js scene lights.
  // Planets use MeshBasicNodeMaterial (self-lit, ignores scene lights)
  // so this light only affects moons — giving them correct day/night
  // terminator shading without touching any planet shader.
  //
  // Discovered autonomously by the ix harness rendering-invariant
  // auditor (autonomous_render_audit.rs) via the belief-revision loop.
  const sunDirectional = new THREE.DirectionalLight(0xffffff, 1.5);
  sunDirectional.name = 'sun-directional';
  sunDirectional.position.set(0, 0, 0); // at the sun (group origin)
  group.add(sunDirectional);
  group.userData.sunDirectional = sunDirectional;

  group.userData.planets = planetMeshes;
  group.userData.moonInstances = moonInstances;
  group.userData.planetLabels = planetLabels;
  group.userData.orbitTrails = orbitTrails;
  return group;
}

// ─────────────────────────────────────────────────────────────
// LABEL CONTROL
// ─────────────────────────────────────────────────────────────

/** Show a planet label by name, hide all others. Pass null to hide all. */
export function showPlanetLabel(group: THREE.Group, name: string | null): void {
  const labels = group.userData.planetLabels as Map<string, THREE.Sprite> | undefined;
  if (!labels) return;
  for (const [key, sprite] of labels) {
    sprite.visible = key === name;
  }
}

/**
 * Toggle a planet's atmosphere visibility.
 * When stripped, the planet shader's atmosphere rim is also disabled,
 * revealing the bare surface underneath.
 * Returns the new state (true = atmosphere visible).
 */
export function togglePlanetAtmosphere(group: THREE.Group, planetName: string): boolean {
  const atmoMesh = group.getObjectByName(`atmo-${planetName}`) as THREE.Mesh | undefined;
  if (atmoMesh) {
    atmoMesh.visible = !atmoMesh.visible;
  }

  // Also toggle the shader's atmosphere rim on the planet surface
  const planets = group.userData.planets as { mesh: THREE.Mesh; def: PlanetDef }[] | undefined;
  if (planets) {
    const entry = planets.find(p => p.def.name === planetName);
    if (entry?.mesh.material instanceof THREE.ShaderMaterial) {
      const u = entry.mesh.material.uniforms;
      if (u.uAtmoColor) {
        u.uAtmoColor.value = u.uAtmoColor.value > 0 ? 0.0 :
          planetName === 'earth' ? 1.0 : planetName === 'venus' ? 2.0 : 0.0;
      }
    }
  }

  return atmoMesh?.visible ?? false;
}

/** Get all planet mesh names (for raycasting targets). */
/**
 * Toggle Earth's cloud layers on/off. Returns new visibility state.
 */
export function toggleEarthClouds(group: THREE.Group): boolean {
  const clouds = group.getObjectByName('earth-clouds');
  const cloudsHigh = group.getObjectByName('earth-clouds-high');
  const newState = clouds ? !clouds.visible : false;
  if (clouds) clouds.visible = newState;
  if (cloudsHigh) cloudsHigh.visible = newState;
  return newState;
}

/**
 * Toggle Earth's country borders overlay on/off. Returns new visibility state.
 */
export function toggleEarthBorders(group: THREE.Group): boolean {
  const borders = group.getObjectByName('earth-borders');
  if (borders) {
    borders.visible = !borders.visible;
    return borders.visible;
  }
  return false;
}

/**
 * Toggle NASA GIBS MODIS snow cover overlay on Earth.
 * Loads MODIS_Terra_Snow_Cover tiles at zoom 2, composes into alphaMap.
 * Returns true if snow cover was enabled, false if removed.
 */
export function toggleSnowCover(group: THREE.Group): boolean {
  const planets = group.userData.planets as { mesh: THREE.Mesh; orbit: THREE.Group; def: PlanetDef }[] | undefined;
  const earth = planets?.find(p => p.def.name === 'earth');
  if (!earth) return false;

  // Check if already exists
  const existing = earth.orbit.getObjectByName('snowCoverMesh') as THREE.Mesh | undefined;
  if (existing) {
    earth.orbit.remove(existing);
    existing.geometry.dispose();
    const mat = existing.material as THREE.MeshBasicMaterial;
    if (mat.alphaMap) mat.alphaMap.dispose();
    mat.dispose();
    return false;
  }

  // Create snow cover sphere slightly above Earth
  const earthGeo = earth.mesh.geometry as THREE.SphereGeometry;
  const earthRadius = earthGeo.parameters.radius;
  const snowRadius = earthRadius * 1.005;

  const snowGeo = new THREE.SphereGeometry(snowRadius, 64, 64);
  // Placeholder white material; texture loaded async
  const snowMat = new THREE.MeshBasicMaterial({
    color: 0xffffff,
    transparent: true,
    opacity: 0.8,
    depthWrite: false,
    side: THREE.FrontSide,
  });
  const snowMesh = new THREE.Mesh(snowGeo, snowMat);
  snowMesh.name = 'snowCoverMesh';
  snowMesh.userData.snowCoverMesh = true;
  snowMesh.position.copy(earth.mesh.position);
  snowMesh.rotation.copy(earth.mesh.rotation);
  earth.orbit.add(snowMesh);

  // Load MODIS snow cover tiles asynchronously (EPSG:4326, zoom 2 = 4x2 tiles)
  const date = getYesterdayDate();
  const ZOOM = 2;
  const COLS = 1 << ZOOM;  // 4
  const ROWS = COLS / 2;   // 2 (EPSG:4326 is 2:1 aspect)
  const TILE_PX = 256;

  const canvas = document.createElement('canvas');
  canvas.width = COLS * TILE_PX;
  canvas.height = ROWS * TILE_PX;
  const ctx = canvas.getContext('2d')!;

  const fetches: Promise<{ x: number; y: number; img: HTMLImageElement | null }>[] = [];
  for (let y = 0; y < ROWS; y++) {
    for (let x = 0; x < COLS; x++) {
      const url = `https://gibs.earthdata.nasa.gov/wmts/epsg4326/best/MODIS_Terra_Snow_Cover/default/${date}/250m/${ZOOM}/${y}/${x}.png`;
      fetches.push(fetchTile(url).then(img => ({ x, y, img })));
    }
  }

  Promise.all(fetches).then(tiles => {
    // If mesh was removed while loading, bail out
    if (!snowMesh.parent) return;

    for (const t of tiles) {
      if (t.img) {
        ctx.drawImage(t.img, t.x * TILE_PX, t.y * TILE_PX, TILE_PX, TILE_PX);
      }
    }

    const tex = new THREE.CanvasTexture(canvas);
    tex.needsUpdate = true;
    snowMat.alphaMap = tex;
    snowMat.needsUpdate = true;
    console.info(`[SolarSystem] Snow cover: ${tiles.filter(t => t.img).length}/${COLS * ROWS} tiles loaded`);
  });

  return true;
}

export function getPlanetMeshes(group: THREE.Group): THREE.Mesh[] {
  const planets = group.userData.planets as { mesh: THREE.Mesh }[] | undefined;
  if (!planets) return [];
  // Include the sun mesh as well
  const sun = group.getObjectByName('sun');
  const meshes = planets.map(p => p.mesh);
  if (sun instanceof THREE.Mesh) meshes.unshift(sun);
  return meshes;
}

// ─────────────────────────────────────────────────────────────
// PLANET SHADER EFFECTS — Aurora, Ring Glow, Jupiter Storm
// ─────────────────────────────────────────────────────────────

/**
 * Toggle aurora borealis effect on Earth.
 * Creates a thin torus mesh near Earth's north pole (~65-75° latitude)
 * with animated green/purple curtain shader.
 * Returns true if aurora was enabled, false if disabled.
 */
export function toggleAurora(group: THREE.Group): boolean {
  // Remove if already present
  const existing = group.userData.auroraMesh as THREE.Mesh | undefined;
  if (existing) {
    existing.parent?.remove(existing);
    existing.geometry.dispose();
    (existing.material as THREE.ShaderMaterial).dispose();
    delete group.userData.auroraMesh;
    return false;
  }

  // Find Earth orbit to parent the aurora
  const planets = group.userData.planets as { mesh: THREE.Mesh; orbit: THREE.Group; def: PlanetDef }[] | undefined;
  if (!planets) return false;
  const earth = planets.find(p => p.def.name === 'earth');
  if (!earth) return false;

  const earthGeo = earth.mesh.geometry as THREE.SphereGeometry;
  const earthRadius = earthGeo.parameters.radius;

  // Torus at ~70° latitude (aurora oval)
  const auroraRadius = earthRadius * Math.cos(70 * Math.PI / 180); // horizontal radius at 70° lat
  const auroraHeight = earthRadius * Math.sin(70 * Math.PI / 180); // height above equator
  const tubeRadius = earthRadius * 0.06;

  const auroraGeo = new THREE.TorusGeometry(auroraRadius, tubeRadius, 16, 64);

  const auroraMat = createAuroraMaterialTSL();

  const auroraMesh = new THREE.Mesh(auroraGeo, auroraMat);
  auroraMesh.name = 'earth-aurora';
  // Position at Earth's north pole latitude, flat horizontal
  auroraMesh.rotation.x = Math.PI / 2;
  auroraMesh.position.copy(earth.mesh.position);
  auroraMesh.position.y += auroraHeight;

  earth.orbit.add(auroraMesh);
  group.userData.auroraMesh = auroraMesh;
  return true;
}

/**
 * Toggle a golden glow overlay on Saturn's rings.
 * Adds/removes a bloom-like emissive ring that pulses gently.
 * Returns true if glow was enabled, false if disabled.
 */
export function toggleRingGlow(group: THREE.Group): boolean {
  // Remove if already present
  const existing = group.userData.ringGlowMesh as THREE.Mesh | undefined;
  if (existing) {
    existing.parent?.remove(existing);
    existing.geometry.dispose();
    (existing.material as THREE.ShaderMaterial).dispose();
    delete group.userData.ringGlowMesh;
    return false;
  }

  // Find Saturn's orbit group and ring
  const planets = group.userData.planets as { mesh: THREE.Mesh; orbit: THREE.Group; def: PlanetDef }[] | undefined;
  if (!planets) return false;
  const saturn = planets.find(p => p.def.name === 'saturn');
  if (!saturn) return false;

  const saturnRing = saturn.orbit.getObjectByName('saturn-ring') as THREE.Mesh | undefined;
  if (!saturnRing) return false;

  // Create a slightly larger ring overlay for the glow
  const ringGeo = (saturnRing.geometry as THREE.RingGeometry);
  const params = ringGeo.parameters;
  const glowGeo = new THREE.RingGeometry(
    params.innerRadius * 0.98,
    params.outerRadius * 1.02,
    128,
  );

  const glowMat = createRingGlowMaterialTSL();

  const glowMesh = new THREE.Mesh(glowGeo, glowMat);
  glowMesh.name = 'saturn-ring-glow';
  // Match Saturn ring orientation and position
  glowMesh.rotation.copy(saturnRing.rotation);
  glowMesh.position.copy(saturnRing.position);

  saturn.orbit.add(glowMesh);
  group.userData.ringGlowMesh = glowMesh;
  return true;
}

/**
 * Toggle Jupiter's Great Red Spot storm effect.
 * Creates a small oval mesh at ~22°S latitude with animated swirl shader.
 * Returns true if storm was enabled, false if disabled.
 */
export function toggleJupiterStorm(group: THREE.Group): boolean {
  // Remove if already present
  const existing = group.userData.stormMesh as THREE.Mesh | undefined;
  if (existing) {
    existing.parent?.remove(existing);
    existing.geometry.dispose();
    (existing.material as THREE.ShaderMaterial).dispose();
    delete group.userData.stormMesh;
    return false;
  }

  // Find Jupiter
  const planets = group.userData.planets as { mesh: THREE.Mesh; orbit: THREE.Group; def: PlanetDef }[] | undefined;
  if (!planets) return false;
  const jupiter = planets.find(p => p.def.name === 'jupiter');
  if (!jupiter) return false;

  const jupiterGeo = jupiter.mesh.geometry as THREE.SphereGeometry;
  const jupiterRadius = jupiterGeo.parameters.radius;

  // Oval disc for the storm (~22°S latitude)
  const stormGeo = new THREE.CircleGeometry(jupiterRadius * 0.18, 32);
  // Stretch into oval
  const posAttr = stormGeo.attributes.position;
  for (let i = 0; i < posAttr.count; i++) {
    posAttr.setY(i, posAttr.getY(i) * 0.6); // flatten vertically
  }
  posAttr.needsUpdate = true;

  const stormMat = createStormVortexMaterialTSL();

  const stormMesh = new THREE.Mesh(stormGeo, stormMat);
  stormMesh.name = 'jupiter-storm';

  // Position at Great Red Spot: ~22°S latitude, on the surface
  const latRad = -22 * Math.PI / 180; // 22° south
  const lonRad = 0; // arbitrary longitude (it will rotate with Jupiter)
  const r = jupiterRadius * 1.01; // slightly above surface

  stormMesh.position.set(
    r * Math.cos(latRad) * Math.cos(lonRad),
    r * Math.sin(latRad),
    r * Math.cos(latRad) * Math.sin(lonRad),
  );

  // Orient the disc to face outward from Jupiter's center
  stormMesh.lookAt(0, 0, 0);
  stormMesh.rotateY(Math.PI); // face outward, not inward

  // Parent to Jupiter mesh so it rotates with the planet
  jupiter.mesh.add(stormMesh);
  group.userData.stormMesh = stormMesh;
  return true;
}

// ─────────────────────────────────────────────────────────────
// UPDATE
// ─────────────────────────────────────────────────────────────
export function updateSolarSystem(group: THREE.Group, time: number, camera?: THREE.Camera): void {
  const planets = group.userData.planets as { mesh: THREE.Mesh; orbit: THREE.Group; def: PlanetDef; clouds?: THREE.Mesh; cloudsHigh?: THREE.Mesh }[] | undefined;
  if (!planets) return;

  // Sun is at group origin (0,0,0 in local space).
  // Compute its world-space position for shader day/night terminator.
  group.updateWorldMatrix(true, false);
  const _sunWorldPos = new THREE.Vector3();
  group.getWorldPosition(_sunWorldPos);

  const trails = group.userData.orbitTrails as Map<string, THREE.Line> | undefined;

  const scale = group.userData.scale as number ?? 0.15;

  for (const { mesh, orbit, def, clouds, cloudsHigh } of planets) {
    // Elliptical orbit — position planet using Kepler's equation
    const realAngle = getRealOrbitalAngle(def.name);
    // Wrap meanAnomaly to [0, 2*PI] — unbounded time*speed loses Float32 precision
    const TAU = Math.PI * 2;
    // Speed 0.001: Mercury completes 1 orbit in ~5100 seconds (~85 min). Imperceptible per-frame.
    const meanAnomaly = ((realAngle + time * def.speed * 0.001) % TAU + TAU) % TAU;
    const e = ECCENTRICITY[def.name] ?? 0;
    const incl = INCLINATION[def.name] ?? 0;
    const pos = ellipticalPosition(meanAnomaly, e, def.distance * scale);
    // Apply inclination
    mesh.position.set(pos.x, pos.z * Math.sin(incl), pos.z * Math.cos(incl));
    // Reset orbit group rotation — position is now computed directly
    orbit.rotation.y = 0;

    if (def.name === 'earth') {
      // ── Real-time Earth rotation based on UTC ──
      // The orbit group rotates continuously (orbit.rotation.y), which changes
      // which direction Earth's local axes point. mesh.rotation.y is in the
      // orbit's local frame, so we must compensate for the orbit angle.
      //
      // Total rotation of texture = orbit.rotation.y + mesh.rotation.y
      // We need the subsolar meridian to face toward the Sun (at the group origin).
      // Earth is offset along +X in the orbit group, so the Sun is in the -X direction.
      // At total_rotation=0, Greenwich (u=0.5) faces +Z. The Sun is at -X = -π/2.
      // So: total_rotation = -π/2 + subsolarLon → mesh.rotation.y = -π/2 + subsolarLon - orbit.rotation.y
      const now = new Date();
      const utcHours = now.getUTCHours() + now.getUTCMinutes() / 60 + now.getUTCSeconds() / 3600;
      const subsolarLonRad = ((12.0 - utcHours) * 15.0) * Math.PI / 180;
      mesh.rotation.y = -Math.PI / 2 + subsolarLonRad - orbit.rotation.y;

      // Clouds drift slowly relative to surface
      if (clouds) clouds.rotation.y = mesh.rotation.y + time * 0.003;
      if (cloudsHigh) cloudsHigh.rotation.y = mesh.rotation.y - time * 0.002;

      // Sync ArcGIS overlays with Earth rotation AND position — Earth moves
      // every frame along its elliptical orbit, and overlays are parented to
      // the orbit group, not the planet. Without position sync they drift
      // and the user sees TWO Earths: the real planet and the orphaned overlay.
      const overlays = group.userData.arcgisOverlays as Record<string, THREE.Mesh> | undefined;
      if (overlays) {
        for (const overlay of Object.values(overlays)) {
          overlay.rotation.y = mesh.rotation.y;
          overlay.position.copy(mesh.position);
        }
      }

      // Sync focused patch with Earth rotation + position (same bug class)
      const focusedPatch = group.userData.focusedPatch as THREE.Mesh | undefined;
      if (focusedPatch) {
        focusedPatch.rotation.y = mesh.rotation.y;
        focusedPatch.position.copy(mesh.position);
      }

      // Sync location marker with Earth rotation
      const marker = group.userData.locationMarker as THREE.Mesh | undefined;
      if (marker && marker.userData.latLon) {
        const { lat, lon } = marker.userData.latLon as { lat: number; lon: number };
        const earthMesh = mesh;
        const earthGeo = earthMesh.geometry as THREE.SphereGeometry;
        const r = earthGeo.parameters.radius * 1.02;
        const phi = (90 - lat) * Math.PI / 180;
        const theta = (lon + 180) * Math.PI / 180;
        marker.position.set(
          -r * Math.sin(phi) * Math.cos(theta),
          r * Math.cos(phi),
          r * Math.sin(phi) * Math.sin(theta),
        );
        // Marker is child of orbit, so apply mesh rotation manually
        const rotY = mesh.rotation.y;
        const x = marker.position.x;
        const z = marker.position.z;
        marker.position.x = x * Math.cos(rotY) - z * Math.sin(rotY);
        marker.position.z = x * Math.sin(rotY) + z * Math.cos(rotY);
        // Add Earth mesh local position offset
        marker.position.add(mesh.position);
        // Marker TSL material uses built-in `time` node — no manual update needed.
        // Sync ring position with marker
        const ring = group.userData.locationRing as THREE.Mesh | undefined;
        if (ring) {
          ring.position.copy(marker.position);
        }
      }
    } else {
      // Other planets: animated rotation
      mesh.rotation.y = time * 0.08;
      if (clouds) clouds.rotation.y = time * 0.62;
      if (cloudsHigh) cloudsHigh.rotation.y = time * 0.45;
    }

    // Update planet TSL uniforms — sun position in world space for day/night + bump
    const planetUserData = mesh.material.userData;
    if (planetUserData?.sunPosUniform) {
      planetUserData.sunPosUniform.value.copy(_sunWorldPos);
    }

    // ── Altitude-driven displacement + bump attenuation ──
    //
    // The planet shaders use a 17×-exaggerated displacement map
    // and a 3x3 Sobel bump normal computed from the daymap's
    // luminance. Both look great at orbital distances and both
    // break at close zoom:
    //   - Displacement inflates mountains and continental shelves
    //     by hundreds of km of equivalent height, visibly
    //     distorting continent shapes
    //   - Sobel bump amplifies daymap JPEG artifacts and foliage
    //     variation into fake "terrain" shading
    //
    // Fade both to zero as the camera approaches the surface, so
    // the close-zoom view shows a clean sphere textured with the
    // 8k daymap. The fade band is defined as fractions of the
    // planet's own radius so it generalizes to other planets if
    // we extend this later.
    //
    // Fade curve (smoothstep of camera-altitude / planet-radius):
    //   >= 2.0 × radius above surface: full strength (1.0)
    //   <= 0.5 × radius above surface: zero strength (0.0)
    //   between: smoothstep
    if (camera && (mesh.userData.baseDispScale !== undefined || mesh.userData.baseBumpStrength !== undefined)) {
      const camWorld = new THREE.Vector3();
      camera.getWorldPosition(camWorld);
      const meshWorld = new THREE.Vector3();
      mesh.getWorldPosition(meshWorld);

      // Radius of this planet in world units (scaled).
      const geo = mesh.geometry as THREE.SphereGeometry;
      const radius = geo.parameters.radius;

      const altitude = camWorld.distanceTo(meshWorld) - radius;
      const altFrac = altitude / radius;  // in units of planet radius

      // smoothstep: 0 below 0.5, 1 above 2.0, cubic interp between
      const tRaw = Math.max(0, Math.min(1, (altFrac - 0.5) / (2.0 - 0.5)));
      const strengthFade = tRaw * tRaw * (3 - 2 * tRaw);  // Hermite smoothstep

      if (planetUserData?.dispScaleUniform && mesh.userData.baseDispScale !== undefined) {
        planetUserData.dispScaleUniform.value = mesh.userData.baseDispScale * strengthFade;
      }
      if (planetUserData?.bumpStrengthUniform && mesh.userData.baseBumpStrength !== undefined) {
        planetUserData.bumpStrengthUniform.value = mesh.userData.baseBumpStrength * strengthFade;
      }
    }

    // Update Saturn ring TSL uniform — sun position in WORLD space (ring uses positionWorld)
    if (def.name === 'saturn') {
      const satRing = mesh.getObjectByName('saturn-ring') as THREE.Mesh | undefined;
      const ringUserData = satRing?.material.userData;
      if (ringUserData?.sunPosUniform) {
        ringUserData.sunPosUniform.value.copy(_sunWorldPos);
      }
    }

    // ── Fading orbit trail — bright near planet, fades 180° behind ──
    const trail = trails?.get(def.name);
    if (trail) {
      const segments = trail.userData.trailSegments as number;
      const alphaAttr = trail.geometry.getAttribute('aAlpha') as THREE.BufferAttribute;
      // Current planet angle in its orbit
      const planetAngle = getRealOrbitalAngle(def.name) + time * def.speed * 0.1; // matches orbit.rotation.y
      for (let i = 0; i <= segments; i++) {
        const vertAngle = (i / segments) * Math.PI * 2;
        // Angular distance from planet (0 to PI)
        let diff = Math.abs(vertAngle - (planetAngle % (Math.PI * 2)));
        if (diff > Math.PI) diff = Math.PI * 2 - diff;
        // Bright (0.35) at planet, fades to dim (0.03) at 180° behind
        const alpha = 0.03 + 0.32 * Math.pow(1 - diff / Math.PI, 2.5);
        alphaAttr.setX(i, alpha);
      }
      alphaAttr.needsUpdate = true;
    }
  }

  // ── Dynamic sun size: realistic subjective diameter near a planet ──
  // From Earth (1 AU), the sun's angular diameter is 0.53°. In scene units,
  // EARTH_DIST=6.5 means realistic radius = 6.5 * tan(0.265°) ≈ 0.03 units —
  // invisible at orrery scale. So we blend: overview uses the "visible orrery"
  // 1.3x-Jupiter size; close approach scales toward realistic angular size.
  //
  // Blend zones (distance from sun/group-origin in world units):
  //   > 20: overview scale (1.0×)
  //   8-20: linear interpolation
  //   < 8: realistic-from-Earth angular size (~0.06 of overview)
  if (camera) {
    const sunMesh = group.getObjectByName('sun') as THREE.Mesh | undefined;
    if (sunMesh) {
      const camWorld = new THREE.Vector3();
      camera.getWorldPosition(camWorld);
      const camDist = camWorld.distanceTo(_sunWorldPos);
      // Smoothstep blend: 1.0 at far, realistic at close
      const blend = Math.max(0, Math.min(1, (camDist - 8) / (20 - 8)));
      // Realistic angular scale relative to overview. At dist=6.5 (Earth),
      // we want ~0.06× the overview sun size. That gives apparent diameter
      // close to real 0.53° (still a bit big for bloom headroom).
      const minScale = 0.08;
      const targetScale = minScale + (1.0 - minScale) * blend;
      // Smooth ease — lerp current scale toward target (tick-rate independent)
      const current = sunMesh.scale.x;
      const smoothed = current + (targetScale - current) * 0.12;
      sunMesh.scale.setScalar(smoothed);
      // Shrink corona sphere + flare glow proportionally — they're cosmetic
      // layers that should track sun visual size.
      const corona = group.children.find(c => (c as THREE.Mesh).geometry?.type === 'SphereGeometry' && c !== sunMesh && c.name !== 'sun') as THREE.Mesh | undefined;
      if (corona?.name === '') corona.scale.setScalar(smoothed);
    }
  }

  // ── Animate sun lens flare (quality-adaptive) ──
  const flareGroup = group.userData.flareGroup as THREE.Group | undefined;
  const quality = group.userData.qualityLevel as string | undefined;
  if (flareGroup) {
    if (quality === 'low') {
      // Low quality: hide flare entirely
      flareGroup.visible = false;
    } else if (quality === 'medium') {
      // Medium: show main glow only
      flareGroup.visible = true;
      for (let i = 0; i < flareGroup.children.length; i++) {
        flareGroup.children[i].visible = i === 0;
      }
      const mainGlow = flareGroup.children[0];
      if (mainGlow) mainGlow.scale.setScalar(6 * scale);
    } else {
      // High quality: glow + hot core (streak and ghosts removed)
      flareGroup.visible = true;
      for (const child of flareGroup.children) child.visible = true;

      const pulse = 1.0 + 0.15 * Math.sin(time * 0.3) + 0.08 * Math.sin(time * 0.7);
      const shimmer = 1.0 + 0.03 * Math.sin(time * 2.1) + 0.02 * Math.sin(time * 3.7);

      // Main glow — subtle pulsing
      const mainGlow = flareGroup.children[0];
      if (mainGlow) {
        const s = 4 * scale * pulse * shimmer;
        mainGlow.scale.set(s, s, 1);
      }

      // Hot core — inverse shimmer for contrast
      const hotCore = flareGroup.children[1];
      if (hotCore) {
        const s = 2.5 * scale * (2.0 - shimmer);
        hotCore.scale.set(s, s, 1);
      }
    }
  }

  // ── Animate all moons ──
  const moons = group.userData.moonInstances as MoonInstance[] | undefined;
  if (moons) {
    for (const { mesh, def, planetMesh, orbitGroup } of moons) {
      // Moon orbit follows planet mesh position (planet moves via elliptical calc)
      orbitGroup.position.copy(planetMesh.position);
      const moonPhase = def.distance * 3.14159 + def.speed * 1.618;
      orbitGroup.rotation.y = moonPhase + time * def.speed * 0.3;
      mesh.rotation.y = time * Math.abs(def.speed) * 0.15;
    }
  }

  // ── Asteroid belt slow orbital drift ──
  const belt = group.getObjectByName('asteroid-belt');
  if (belt) belt.rotation.y += 0.00005;

  // ── Asteroid LOD fade + tumble (camera-proximity driven) ──
  const lod = group.userData.asteroidLOD as AsteroidLODHandle | undefined;
  if (lod && camera) {
    const camPos = new THREE.Vector3();
    camera.getWorldPosition(camPos);
    const beltCenter = new THREE.Vector3();
    group.getWorldPosition(beltCenter);
    lod.update(camPos, beltCenter, time);
  }

  // ── Moon Transit Shadows on Jupiter ──
  // Parallel-ray approximation: sun is far enough that rays are effectively parallel.
  // For each Galilean moon, project its position along the sun direction onto Jupiter's
  // surface sphere. Shadow appears only when the moon is between the sun and Jupiter
  // AND the lateral offset is small enough to hit the planet disc.
  const shadowDiscs = group.userData.shadowDiscs as Map<string, THREE.Mesh> | undefined;
  if (shadowDiscs && moons) {
    const jupiterData = planets.find(p => p.def.name === 'jupiter');
    if (jupiterData) {
      const jupiterWorldPos = new THREE.Vector3();
      jupiterData.mesh.getWorldPosition(jupiterWorldPos);
      const scl = group.userData.scale as number ?? 1;
      const jupiterRadius = jupiterData.def.radius * scl;

      // Direction from Jupiter toward the sun (sun is at group origin)
      const sunWorldPos = new THREE.Vector3();
      group.getWorldPosition(sunWorldPos);
      const toSun = sunWorldPos.clone().sub(jupiterWorldPos).normalize();

      for (const [moonName, disc] of shadowDiscs) {
        const moonInstance = moons.find(m => m.def.name === moonName);
        if (!moonInstance) { disc.visible = false; continue; }

        const moonWorldPos = new THREE.Vector3();
        moonInstance.mesh.getWorldPosition(moonWorldPos);

        // Moon position relative to Jupiter center
        const moonRel = moonWorldPos.clone().sub(jupiterWorldPos);
        // Component along the sun direction (positive = moon is on sun side)
        const alongSun = moonRel.dot(toSun);

        if (alongSun > jupiterRadius * 0.1) {
          // Lateral offset from the Jupiter–Sun axis
          const lateral = moonRel.clone().sub(toSun.clone().multiplyScalar(alongSun));
          const lateralDist = lateral.length();

          if (lateralDist < jupiterRadius * 0.95) {
            // Shadow hits the surface — compute position on the sun-facing hemisphere
            const zComp = Math.sqrt(Math.max(0, jupiterRadius * jupiterRadius - lateralDist * lateralDist));
            // Shadow point in Jupiter-relative world coords (on the lit hemisphere)
            const shadowWorld = lateral.clone().add(toSun.clone().multiplyScalar(zComp * 1.005));

            // Convert to Jupiter mesh local space (disc is a child of the mesh)
            disc.position.copy(jupiterData.mesh.worldToLocal(jupiterWorldPos.clone().add(shadowWorld)));
            // Face outward from Jupiter center (local origin)
            disc.lookAt(0, 0, 0);

            // Scale: moon angular size projected onto surface
            const moonRadius = moonInstance.def.radius * scl;
            const shadowSize = Math.max(moonRadius * 1.5, 0.015);
            disc.scale.setScalar(shadowSize / 0.03); // 0.03 = CircleGeometry base radius
            disc.visible = true;
          } else {
            disc.visible = false; // moon lateral offset too far — misses Jupiter
          }
        } else {
          disc.visible = false; // moon behind Jupiter — no transit
        }
      }
    }
  }

  // Aurora / storm / ring glow TSL materials use built-in `time` node — no manual update needed.
}

// ─────────────────────────────────────────────────────────────
// LIVE SATELLITE IMAGERY — NASA GIBS (free, no API key)
// ─────────────────────────────────────────────────────────────

function getYesterdayDate(): string {
  const d = new Date();
  d.setDate(d.getDate() - 1); // yesterday — GIBS needs ~hours for latest
  return d.toISOString().slice(0, 10); // YYYY-MM-DD
}

function fetchTile(url: string): Promise<HTMLImageElement | null> {
  return new Promise(resolve => {
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => resolve(img);
    img.onerror = () => resolve(null);
    img.src = url;
  });
}

/**
 * Fetch real satellite cloud imagery and overlay on Earth.
 *
 * Primary: NASA GIBS VIIRS true-color satellite tiles (free, no API key).
 * Fallback: OpenWeatherMap cloud tiles (needs VITE_OWM_API_KEY).
 * Final fallback: static 2k_earth_clouds.jpg (already loaded).
 *
 * Call once after createSolarSystem. Returns a cleanup function.
 */
export function startLiveCloudUpdates(group: THREE.Group, apiKey?: string): () => void {
  const planets = group.userData.planets as { mesh: THREE.Mesh; def: PlanetDef; clouds?: THREE.Mesh; cloudsHigh?: THREE.Mesh }[] | undefined;
  if (!planets) return () => {};

  const earthEntry = planets.find(p => p.def.name === 'earth');
  if (!earthEntry?.clouds) return () => {};

  // Canvas matches Mercator tile grid (8x8 at zoom 3 = square)
  // Three.js SphereGeometry UV maps equirectangular, but Mercator tiles are square
  // Using 2048x2048 avoids vertical squishing artifacts and seam gaps
  const canvas = document.createElement('canvas');
  canvas.width = 2048;
  canvas.height = 2048;
  const ctx = canvas.getContext('2d')!;
  const canvasTexture = new THREE.CanvasTexture(canvas);
  canvasTexture.colorSpace = THREE.SRGBColorSpace;

  // Try NASA GIBS first (VIIRS true-color satellite, EPSG:3857 web Mercator)
  async function fetchGIBS(): Promise<boolean> {
    const date = getYesterdayDate();
    const layer = 'VIIRS_SNPP_CorrectedReflectance_TrueColor';
    const matrixSet = 'GoogleMapsCompatible_Level9';
    const ZOOM = 3;
    const TILES = 1 << ZOOM; // 8

    const fetches: Promise<{ x: number; y: number; img: HTMLImageElement | null }>[] = [];
    for (let y = 0; y < TILES; y++) {
      for (let x = 0; x < TILES; x++) {
        // GIBS uses {z}/{y}/{x} (TileMatrix/TileRow/TileCol)
        const url = `https://gibs.earthdata.nasa.gov/wmts/epsg3857/best/${layer}/default/${date}/${matrixSet}/${ZOOM}/${y}/${x}.jpg`;
        fetches.push(fetchTile(url).then(img => ({ x, y, img })));
      }
    }

    const results = await Promise.all(fetches);
    const loaded = results.filter(r => r.img);
    if (loaded.length < TILES * TILES * 0.5) return false; // too many failures

    ctx.clearRect(0, 0, canvas.width, canvas.height);
    const tileW = canvas.width / TILES;
    const tileH = canvas.height / TILES;

    for (const { x, y, img } of results) {
      if (!img) continue;
      ctx.drawImage(img, x * tileW, y * tileH, tileW, tileH);
    }

    // Extract clouds: GIBS tiles are true-color (land + ocean + clouds).
    // Clouds are bright AND low-saturation (white/gray). Land is bright but colored.
    const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
    const data = imageData.data;
    for (let i = 0; i < data.length; i += 4) {
      const r = data[i], g = data[i + 1], b = data[i + 2];
      const brightness = (r + g + b) / 3;
      const maxC = Math.max(r, g, b);
      const minC = Math.min(r, g, b);
      const saturation = maxC > 0 ? (maxC - minC) / maxC : 0;
      // Clouds: bright (>180) AND low saturation (<0.25) — filters out colored land
      // Deserts are bright but yellowish (high sat), ocean is dark, ice is white (kept)
      const isCloud = brightness > 180 && saturation < 0.25;
      const cloudAlpha = isCloud ? Math.min(255, (brightness - 180) / 75 * 255) : 0;
      data[i] = 255;
      data[i + 1] = 255;
      data[i + 2] = 255;
      data[i + 3] = cloudAlpha;
    }
    ctx.putImageData(imageData, 0, 0);

    canvasTexture.needsUpdate = true;
    console.info(`[SolarSystem] NASA GIBS cloud texture loaded (${loaded.length}/${TILES * TILES} tiles, date: ${date})`);
    return true;
  }

  // Fallback: OWM cloud tiles (needs API key)
  async function fetchOWM(): Promise<boolean> {
    const key = apiKey || (typeof import.meta !== 'undefined' && (import.meta as Record<string, Record<string, string>>).env?.VITE_OWM_API_KEY);
    if (!key) return false;

    const ZOOM = 3;
    const TILES = 1 << ZOOM;

    const fetches: Promise<{ x: number; y: number; img: HTMLImageElement | null }>[] = [];
    for (let y = 0; y < TILES; y++) {
      for (let x = 0; x < TILES; x++) {
        const url = `https://tile.openweathermap.org/map/clouds_new/${ZOOM}/${x}/${y}.png?appid=${key}`;
        fetches.push(fetchTile(url).then(img => ({ x, y, img })));
      }
    }

    const results = await Promise.all(fetches);
    const loaded = results.filter(r => r.img);
    if (loaded.length < TILES * TILES * 0.5) return false;

    ctx.clearRect(0, 0, canvas.width, canvas.height);
    const tileW = canvas.width / TILES;
    const tileH = canvas.height / TILES;

    for (const { x, y, img } of results) {
      if (!img) continue;
      ctx.drawImage(img, x * tileW, y * tileH, tileW, tileH);
    }

    // OWM alpha processing — OWM tiles are already cloud-only, just boost contrast
    const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
    const data = imageData.data;
    for (let i = 0; i < data.length; i += 4) {
      const brightness = (data[i] + data[i + 1] + data[i + 2]) / 3;
      // Only bright areas are clouds, cut threshold to avoid haze
      const alpha = brightness > 100 ? Math.min(255, (brightness - 100) / 155 * 255) : 0;
      data[i] = 255;
      data[i + 1] = 255;
      data[i + 2] = 255;
      data[i + 3] = alpha;
    }
    ctx.putImageData(imageData, 0, 0);

    canvasTexture.needsUpdate = true;
    console.info(`[SolarSystem] OWM cloud texture loaded (${loaded.length}/${TILES * TILES} tiles)`);
    return true;
  }

  function applyTexture() {
    if (earthEntry!.clouds) {
      const mat = earthEntry!.clouds.material as THREE.MeshStandardMaterial;
      mat.map = canvasTexture;
      mat.alphaMap = canvasTexture;
      mat.needsUpdate = true;
    }
    if (earthEntry!.cloudsHigh) {
      const mat = earthEntry!.cloudsHigh.material as THREE.MeshStandardMaterial;
      mat.map = canvasTexture;
      mat.alphaMap = canvasTexture;
      mat.needsUpdate = true;
    }
  }

  async function refresh() {
    // Try GIBS first (no key needed), then OWM
    const ok = await fetchGIBS() || await fetchOWM();
    if (ok) applyTexture();
    else console.warn('[SolarSystem] All cloud sources failed — keeping current texture');
  }

  // Initial fetch
  refresh().catch(err => {
    console.warn('[SolarSystem] Live cloud fetch failed:', err);
  });

  // Refresh every 15 minutes
  const interval = setInterval(() => {
    refresh().catch(err => console.warn('[SolarSystem] Cloud refresh failed:', err));
  }, CLOUD_REFRESH_MS);

  return () => clearInterval(interval);
}

// ─────────────────────────────────────────────────────────────
// ARCGIS TILE OVERLAY — composites web map tiles onto Earth sphere
// ─────────────────────────────────────────────────────────────

const ARCGIS_SERVICES: Record<string, string> = {
  imagery: 'https://services.arcgisonline.com/arcgis/rest/services/World_Imagery/MapServer/tile',
  streets: 'https://services.arcgisonline.com/arcgis/rest/services/World_Street_Map/MapServer/tile',
  topo: 'https://services.arcgisonline.com/arcgis/rest/services/World_Topo_Map/MapServer/tile',
  borders: 'https://services.arcgisonline.com/arcgis/rest/services/Reference/World_Boundaries_and_Places/MapServer/tile',
  darkgray: 'https://services.arcgisonline.com/arcgis/rest/services/Canvas/World_Dark_Gray_Base/MapServer/tile',
};

/**
 * Load ArcGIS tile layer onto the Earth sphere.
 * @param group - Solar system group from createSolarSystem
 * @param layer - 'imagery' | 'streets' | 'topo' | 'borders' | 'darkgray'
 * @returns cleanup function to remove the overlay
 */
export async function loadArcGISOverlay(
  group: THREE.Group,
  layer: keyof typeof ARCGIS_SERVICES = 'borders',
  zoom = 3,
): Promise<() => void> {
  const baseUrl = ARCGIS_SERVICES[layer];
  if (!baseUrl) throw new Error(`Unknown ArcGIS layer: ${layer}`);

  const planets = group.userData.planets as { mesh: THREE.Mesh; orbit: THREE.Group; def: PlanetDef }[] | undefined;
  const earth = planets?.find(p => p.def.name === 'earth');
  if (!earth) throw new Error('Earth not found in solar system');

  const ZOOM = zoom;
  const TILES = 1 << ZOOM;
  const TILE_PX = 256;
  const MERC_SIZE = TILES * TILE_PX;
  const EQ_W = MERC_SIZE;            // equirectangular output width
  const EQ_H = MERC_SIZE / 2;        // equirectangular is 2:1 aspect (but we use square for sphere UV)

  // Step 1: Composite tiles into a Mercator canvas
  const mercCanvas = document.createElement('canvas');
  mercCanvas.width = MERC_SIZE;
  mercCanvas.height = MERC_SIZE;
  const mercCtx = mercCanvas.getContext('2d')!;

  // ArcGIS tile URL format: {baseUrl}/{z}/{y}/{x}
  const fetches: Promise<{ x: number; y: number; img: HTMLImageElement | null }>[] = [];
  for (let y = 0; y < TILES; y++) {
    for (let x = 0; x < TILES; x++) {
      const url = `${baseUrl}/${ZOOM}/${y}/${x}`;
      fetches.push(fetchTile(url).then(img => ({ x, y, img })));
    }
  }

  const tiles = await Promise.all(fetches);
  const loaded = tiles.filter(t => t.img);

  if (loaded.length === 0) {
    console.warn(`[SolarSystem] No ArcGIS ${layer} tiles loaded`);
    return () => {};
  }

  for (const t of loaded) {
    if (t.img) {
      mercCtx.drawImage(t.img, t.x * TILE_PX, t.y * TILE_PX, TILE_PX, TILE_PX);
    }
  }

  // Step 2: Reproject Mercator → Equirectangular
  // Mercator Y maps latitude via: y_merc = 0.5 - ln(tan(π/4 + lat/2)) / (2π)  (normalized 0..1)
  // Equirectangular Y maps latitude linearly: y_eq = 0.5 - lat/π  (normalized 0..1)
  const CANVAS_SIZE = MERC_SIZE; // output same resolution
  const canvas = document.createElement('canvas');
  canvas.width = CANVAS_SIZE;
  canvas.height = CANVAS_SIZE;
  const ctx = canvas.getContext('2d')!;

  // Web Mercator max latitude ≈ 85.051°
  const MAX_LAT = 85.051 * Math.PI / 180;

  // For each row in the equirectangular output, find the corresponding Mercator source row
  for (let eqY = 0; eqY < CANVAS_SIZE; eqY++) {
    // Equirectangular: linear latitude mapping
    // eqY=0 → +90° (north pole), eqY=CANVAS_SIZE → -90° (south pole)
    const lat = Math.PI / 2 - (eqY / CANVAS_SIZE) * Math.PI;

    // Clamp to Mercator bounds
    const clampedLat = Math.max(-MAX_LAT, Math.min(MAX_LAT, lat));

    // Convert latitude to Mercator Y (0..1, 0=top=north)
    const mercYNorm = (1 - (Math.log(Math.tan(Math.PI / 4 + clampedLat / 2)) / Math.PI)) / 2;
    const srcY = Math.floor(mercYNorm * MERC_SIZE);

    if (srcY >= 0 && srcY < MERC_SIZE) {
      // Copy one row from Mercator canvas to equirectangular output
      ctx.drawImage(mercCanvas, 0, srcY, MERC_SIZE, 1, 0, eqY, CANVAS_SIZE, 1);
    }
  }

  console.info(`[SolarSystem] ArcGIS ${layer}: ${loaded.length}/${TILES * TILES} tiles loaded (reprojected)`);

  const tex = new THREE.CanvasTexture(canvas);
  tex.colorSpace = THREE.SRGBColorSpace;
  tex.needsUpdate = true;

  // Overlay opacity — borders are semi-transparent; base maps blend lightly
  const LAYER_OPACITY: Record<string, number> = {
    borders: 0.75,
    imagery: 0.6,
    streets: 0.4,
    topo: 0.5,
    darkgray: 0.5,
  };
  const isOverlay = layer === 'borders';

  const mat = new THREE.MeshBasicMaterial({
    map: tex,
    transparent: true,
    opacity: LAYER_OPACITY[layer] ?? 0.5,
    depthWrite: false,
    side: THREE.FrontSide,
  });

  // Create overlay sphere slightly above Earth surface
  const earthMesh = earth.mesh;
  const earthGeo = earthMesh.geometry as THREE.SphereGeometry;
  const earthRadius = earthGeo.parameters.radius;
  const overlayRadius = earthRadius * (isOverlay ? 1.01 : 1.005);

  const overlayGeo = new THREE.SphereGeometry(overlayRadius, 64, 64);
  const overlayMesh = new THREE.Mesh(overlayGeo, mat);
  overlayMesh.name = `earth-arcgis-${layer}`;
  overlayMesh.position.copy(earthMesh.position);
  // Match Earth's rotation
  overlayMesh.rotation.copy(earthMesh.rotation);

  earth.orbit.add(overlayMesh);

  // Store reference for animation sync
  group.userData.arcgisOverlays = group.userData.arcgisOverlays || {};
  (group.userData.arcgisOverlays as Record<string, THREE.Mesh>)[layer] = overlayMesh;

  return () => {
    earth.orbit.remove(overlayMesh);
    overlayGeo.dispose();
    mat.dispose();
    tex.dispose();
    delete (group.userData.arcgisOverlays as Record<string, THREE.Mesh>)[layer];
  };
}

/** Get available ArcGIS layer names */
export function getArcGISLayers(): string[] {
  return Object.keys(ARCGIS_SERVICES);
}

/** Remove a specific ArcGIS overlay layer from Earth */
export function removeArcGISOverlay(group: THREE.Group, layer: string): void {
  const overlays = group.userData.arcgisOverlays as Record<string, THREE.Mesh> | undefined;
  if (!overlays || !overlays[layer]) return;

  const mesh = overlays[layer];
  mesh.parent?.remove(mesh);
  (mesh.geometry as THREE.SphereGeometry).dispose();
  (mesh.material as THREE.MeshBasicMaterial).dispose();
  const mat = mesh.material as THREE.MeshBasicMaterial;
  if (mat.map) mat.map.dispose();
  delete overlays[layer];
}

/** Get currently active ArcGIS overlay layer names */
export function getActiveArcGISLayers(group: THREE.Group): string[] {
  const overlays = group.userData.arcgisOverlays as Record<string, THREE.Mesh> | undefined;
  return overlays ? Object.keys(overlays) : [];
}

/**
 * Add a "you are here" location marker on Earth.
 * Uses browser Geolocation API. Returns a cleanup function.
 */
export function addLocationMarker(group: THREE.Group): () => void {
  const planets = group.userData.planets as { mesh: THREE.Mesh; orbit: THREE.Group; def: PlanetDef }[] | undefined;
  const earth = planets?.find(p => p.def.name === 'earth');
  if (!earth) return () => {};

  // Create marker mesh — tiny glowing sphere, proportional to Earth
  const earthGeo = earth.mesh.geometry as THREE.SphereGeometry;
  const earthR = earthGeo.parameters.radius;
  const markerSize = earthR * 0.02; // 2% of Earth radius
  const markerGeo = new THREE.SphereGeometry(markerSize, 8, 8);
  const markerMat = createMarkerMaterialTSL();
  const marker = new THREE.Mesh(markerGeo, markerMat);
  marker.name = 'location-marker';

  // Outer glow ring
  const ringGeo = new THREE.RingGeometry(markerSize * 1.5, markerSize * 2.5, 16);
  const ringMat = new THREE.MeshBasicMaterial({
    color: 0x33ccff,
    transparent: true,
    opacity: 0.4,
    side: THREE.DoubleSide,
    depthWrite: false,
  });
  const ring = new THREE.Mesh(ringGeo, ringMat);
  ring.name = 'location-ring';

  // Default to (0,0) until geolocation resolves
  marker.userData.latLon = { lat: 0, lon: 0 };
  earth.orbit.add(marker);
  earth.orbit.add(ring);
  group.userData.locationMarker = marker;
  group.userData.locationRing = ring;

  // Request geolocation
  if (typeof navigator !== 'undefined' && navigator.geolocation) {
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        marker.userData.latLon = { lat: pos.coords.latitude, lon: pos.coords.longitude };
        console.info(`[SolarSystem] Location marker: ${pos.coords.latitude.toFixed(2)}°, ${pos.coords.longitude.toFixed(2)}°`);
      },
      (err) => {
        console.warn('[SolarSystem] Geolocation denied, marker at 0°,0°:', err.message);
      },
      { enableHighAccuracy: false, timeout: 10000 },
    );
  }

  return () => {
    earth.orbit.remove(marker);
    earth.orbit.remove(ring);
    markerGeo.dispose();
    markerMat.dispose();
    ringGeo.dispose();
    ringMat.dispose();
    delete group.userData.locationMarker;
    delete group.userData.locationRing;
  };
}

/**
 * Enable auto-LOD for Earth — loads higher-resolution ArcGIS satellite imagery
 * when the camera is close, removes it when zooming out.
 * Call this once; it returns a cleanup function.
 */
export function enableEarthAutoLOD(
  group: THREE.Group,
  getCamera: () => THREE.Camera,
): () => void {
  const planets = group.userData.planets as { mesh: THREE.Mesh; orbit: THREE.Group; def: PlanetDef }[] | undefined;
  const earth = planets?.find(p => p.def.name === 'earth');
  if (!earth) return () => {};

  // LOD tiers: zoom 4 (4K) for medium, zoom 5 (8K) for close
  let currentLOD: 'base' | 'mid' | 'high' | 'ultra' = 'base';
  let lastPatchCenter: { lat: number; lon: number } | null = null;
  let loading = false;
  const earthGeo = earth.mesh.geometry as THREE.SphereGeometry;
  const earthRadius = earthGeo.parameters.radius;
  // Use a unique layer key for LOD so it doesn't conflict with user-toggled imagery
  const LOD_KEY = '_lod_imagery';

  const checkInterval = setInterval(() => {
    if (loading) return;

    const cam = getCamera();
    const earthWorldPos = new THREE.Vector3();
    earth.mesh.getWorldPosition(earthWorldPos);
    const dist = cam.position.distanceTo(earthWorldPos);
    const relDist = dist / earthRadius;

    // Determine desired LOD
    let desired: 'base' | 'mid' | 'high' | 'ultra' = 'base';
    if (relDist < 1.5) desired = 'ultra';   // extreme close-up — focused street-level patch
    else if (relDist < 3) desired = 'high'; // very close — zoom 5
    else if (relDist < 8) desired = 'mid';  // medium — zoom 4

    if (desired === currentLOD) return;

    const overlays = (group.userData.arcgisOverlays ?? {}) as Record<string, THREE.Mesh>;
    group.userData.arcgisOverlays = overlays;
    const oldMesh = overlays[LOD_KEY] ?? null;

    if (desired === 'base') {
      // ── Smooth fade out ──
      if (oldMesh) {
        const oldMat = oldMesh.material as THREE.MeshBasicMaterial;
        const fadeOut = () => {
          oldMat.opacity -= 0.04;
          if (oldMat.opacity <= 0) {
            oldMesh.parent?.remove(oldMesh);
            oldMesh.geometry.dispose();
            if (oldMat.map) oldMat.map.dispose();
            oldMat.dispose();
            delete overlays[LOD_KEY];
          } else {
            requestAnimationFrame(fadeOut);
          }
        };
        fadeOut();
      }
      currentLOD = 'base';
      return;
    }

    // Load new LOD tier
    loading = true;
    const zoom = desired === 'high' ? 5 : 4;
    console.info(`[SolarSystem] Earth LOD: loading zoom ${zoom}...`);

    const svcUrl = ARCGIS_SERVICES.imagery;
    const nTiles = 1 << zoom;
    const tileSize = 256;
    const mercSize = nTiles * tileSize;

    const fetches: Promise<{ x: number; y: number; img: HTMLImageElement | null }>[] = [];
    for (let y = 0; y < nTiles; y++) {
      for (let x = 0; x < nTiles; x++) {
        fetches.push(fetchTile(`${svcUrl}/${zoom}/${y}/${x}`).then(img => ({ x, y, img })));
      }
    }

    Promise.all(fetches).then(results => {
      const loaded = results.filter(t => t.img);
      if (loaded.length === 0) { loading = false; return; }

      // Composite Mercator tiles
      const mercCanvas = document.createElement('canvas');
      mercCanvas.width = mercSize;
      mercCanvas.height = mercSize;
      const mercCtx = mercCanvas.getContext('2d')!;
      for (const t of loaded) {
        if (t.img) mercCtx.drawImage(t.img, t.x * tileSize, t.y * tileSize, tileSize, tileSize);
      }

      // Use Mercator texture directly — reprojection was causing main-thread
      // stalls (up to 8192 synchronous drawImage calls at zoom 5).
      // Slight polar stretching is acceptable for an orrery overlay.
      const eqCanvas = mercCanvas;

      const tex = new THREE.CanvasTexture(eqCanvas);
      tex.colorSpace = THREE.SRGBColorSpace;
      tex.needsUpdate = true;

      // ── Smooth crossfade: new overlay fades in from 0 ──
      const TARGET_OPACITY = 0.85;
      const newMat = new THREE.MeshBasicMaterial({
        map: tex, transparent: true, opacity: 0,
        depthWrite: false, side: THREE.FrontSide,
      });

      const overlayRadius = earthRadius * 1.004;
      const overlayGeo = new THREE.SphereGeometry(overlayRadius, 64, 64);
      const newMesh = new THREE.Mesh(overlayGeo, newMat);
      newMesh.name = `earth-arcgis-${LOD_KEY}`;
      newMesh.position.copy(earth.mesh.position);
      newMesh.rotation.copy(earth.mesh.rotation);
      earth.orbit.add(newMesh);
      // Register so the per-frame sync loop keeps it tracking Earth's position
      // and rotation — otherwise the overlay stays frozen while Earth orbits.
      {
        const reg = (group.userData.arcgisOverlays ?? {}) as Record<string, THREE.Mesh>;
        group.userData.arcgisOverlays = reg;
        reg[LOD_KEY] = newMesh;
      }

      // Crossfade animation
      const crossfade = () => {
        // Fade in new
        newMat.opacity = Math.min(newMat.opacity + 0.025, TARGET_OPACITY);
        // Fade out old
        if (oldMesh && oldMesh.parent) {
          const oldMat = oldMesh.material as THREE.MeshBasicMaterial;
          oldMat.opacity -= 0.04;
          if (oldMat.opacity <= 0) {
            oldMesh.parent.remove(oldMesh);
            oldMesh.geometry.dispose();
            if (oldMat.map) oldMat.map.dispose();
            oldMat.dispose();
          }
        }
        if (newMat.opacity < TARGET_OPACITY) {
          requestAnimationFrame(crossfade);
        }
      };
      requestAnimationFrame(crossfade);

      overlays[LOD_KEY] = newMesh;
      currentLOD = desired;
      console.info(`[SolarSystem] Earth LOD: zoom ${zoom} — ${loaded.length} tiles, crossfading...`);

    }).catch(() => {}).finally(() => { loading = false; });

    // For ultra tier, also load focused street-level patch
    if (desired === 'ultra') {
      const target = getCameraEarthTarget(group, getCamera());
      if (target) {
        const needsReload = !lastPatchCenter
          || Math.abs(target.lat - lastPatchCenter.lat) > 2
          || Math.abs(target.lon - lastPatchCenter.lon) > 2;
        if (needsReload) {
          lastPatchCenter = { ...target };
          loadFocusedPatch(group, target.lat, target.lon, 13, 8).catch(() => {});
        }
      }
    } else {
      // Remove focused patch when not ultra
      const patch = group.userData.focusedPatch as THREE.Mesh | undefined;
      if (patch) {
        // Fade out
        const pMat = patch.material as THREE.MeshBasicMaterial;
        const fadeOutPatch = () => {
          pMat.opacity -= 0.05;
          if (pMat.opacity <= 0) {
            patch.parent?.remove(patch);
            patch.geometry.dispose();
            if (pMat.map) pMat.map.dispose();
            pMat.dispose();
            delete group.userData.focusedPatch;
          } else {
            requestAnimationFrame(fadeOutPatch);
          }
        };
        fadeOutPatch();
        lastPatchCenter = null;
      }
    }
  }, 2000);

  return () => {
    clearInterval(checkInterval);
    // Clean up LOD overlay
    const overlays = group.userData.arcgisOverlays as Record<string, THREE.Mesh> | undefined;
    if (overlays?.[LOD_KEY]) {
      const mesh = overlays[LOD_KEY];
      mesh.parent?.remove(mesh);
      (mesh.geometry as THREE.BufferGeometry).dispose();
      const mat = mesh.material as THREE.MeshBasicMaterial;
      if (mat.map) mat.map.dispose();
      mat.dispose();
      delete overlays[LOD_KEY];
    }
    // Clean up focused patch
    const patch = group.userData.focusedPatch as THREE.Mesh | undefined;
    if (patch) {
      patch.parent?.remove(patch);
      (patch.geometry as THREE.BufferGeometry).dispose();
      const pm = patch.material as THREE.MeshBasicMaterial;
      if (pm.map) pm.map.dispose();
      pm.dispose();
      delete group.userData.focusedPatch;
    }
  };
}

// ─────────────────────────────────────────────────────────────
// FOCUSED HIGH-RES TILE PATCH — loads street-level tiles around camera target
// ─────────────────────────────────────────────────────────────

/** Convert lat/lon to Mercator tile coordinates at a given zoom */
function latLonToTile(lat: number, lon: number, zoom: number): { x: number; y: number } {
  const n = 1 << zoom;
  const x = Math.floor((lon + 180) / 360 * n);
  const latRad = lat * Math.PI / 180;
  const y = Math.floor((1 - Math.log(Math.tan(latRad) + 1 / Math.cos(latRad)) / Math.PI) / 2 * n);
  return { x: Math.max(0, Math.min(n - 1, x)), y: Math.max(0, Math.min(n - 1, y)) };
}

/** Convert tile coordinates back to lat/lon (north-west corner) */
function tileToLatLon(x: number, y: number, zoom: number): { lat: number; lon: number } {
  const n = 1 << zoom;
  const lon = x / n * 360 - 180;
  const latRad = Math.atan(Math.sinh(Math.PI * (1 - 2 * y / n)));
  return { lat: latRad * 180 / Math.PI, lon };
}

/**
 * Raycast from camera to Earth to find the lat/lon being looked at.
 * Returns null if camera isn't pointing at Earth.
 */
export function getCameraEarthTarget(
  group: THREE.Group,
  camera: THREE.Camera,
): { lat: number; lon: number } | null {
  const planets = group.userData.planets as { mesh: THREE.Mesh; orbit: THREE.Group; def: PlanetDef }[] | undefined;
  const earth = planets?.find(p => p.def.name === 'earth');
  if (!earth) return null;

  const raycaster = new THREE.Raycaster();
  raycaster.setFromCamera(new THREE.Vector2(0, 0), camera);

  const hits = raycaster.intersectObject(earth.mesh);
  if (hits.length === 0) return null;

  const hit = hits[0];
  // Convert hit point to Earth-local coordinates
  const localPoint = earth.mesh.worldToLocal(hit.point.clone());
  const earthGeo = earth.mesh.geometry as THREE.SphereGeometry;
  const r = earthGeo.parameters.radius;

  // Spherical coordinates → lat/lon
  const lat = Math.asin(localPoint.y / r) * 180 / Math.PI;
  const lon = Math.atan2(localPoint.z, localPoint.x) * 180 / Math.PI;

  return { lat, lon };
}

/**
 * Load a focused high-res tile patch on Earth around a given lat/lon.
 * Creates a small curved mesh with high-zoom satellite imagery.
 * Returns a cleanup function.
 */
export async function loadFocusedPatch(
  group: THREE.Group,
  lat: number,
  lon: number,
  zoom = 12,
  gridSize = 6,  // 6x6 tiles
): Promise<() => void> {
  const planets = group.userData.planets as { mesh: THREE.Mesh; orbit: THREE.Group; def: PlanetDef }[] | undefined;
  const earth = planets?.find(p => p.def.name === 'earth');
  if (!earth) return () => {};

  const earthGeo = earth.mesh.geometry as THREE.SphereGeometry;
  const earthRadius = earthGeo.parameters.radius;

  // Remove existing focused patch
  const existing = group.userData.focusedPatch as THREE.Mesh | undefined;
  if (existing) {
    existing.parent?.remove(existing);
    (existing.geometry as THREE.BufferGeometry).dispose();
    const em = existing.material as THREE.MeshBasicMaterial;
    if (em.map) em.map.dispose();
    em.dispose();
  }

  // Get center tile and surrounding grid
  const center = latLonToTile(lat, lon, zoom);
  const half = Math.floor(gridSize / 2);
  const tileCount = 1 << zoom;

  // Fetch tiles
  const baseUrl = ARCGIS_SERVICES.imagery;
  const fetches: Promise<{ tx: number; ty: number; img: HTMLImageElement | null }>[] = [];
  for (let dy = -half; dy < half; dy++) {
    for (let dx = -half; dx < half; dx++) {
      const tx = (center.x + dx + tileCount) % tileCount;
      const ty = Math.max(0, Math.min(tileCount - 1, center.y + dy));
      fetches.push(fetchTile(`${baseUrl}/${zoom}/${ty}/${tx}`).then(img => ({ tx: dx + half, ty: dy + half, img })));
    }
  }

  const tiles = await Promise.all(fetches);
  const loaded = tiles.filter(t => t.img);
  if (loaded.length === 0) return () => {};

  // Composite tiles
  const canvasSize = gridSize * 256;
  const canvas = document.createElement('canvas');
  canvas.width = canvasSize;
  canvas.height = canvasSize;
  const ctx = canvas.getContext('2d')!;
  for (const t of loaded) {
    if (t.img) ctx.drawImage(t.img, t.tx * 256, t.ty * 256, 256, 256);
  }

  // Calculate the lat/lon bounds of this tile grid
  const nwTile = {
    x: (center.x - half + tileCount) % tileCount,
    y: Math.max(0, center.y - half),
  };
  const seTile = {
    x: (center.x + half + tileCount) % tileCount,
    y: Math.min(tileCount - 1, center.y + half),
  };
  const nw = tileToLatLon(nwTile.x, nwTile.y, zoom);
  const se = tileToLatLon(seTile.x, seTile.y + 1, zoom);

  // Create a curved patch mesh (section of sphere)
  const latSteps = 32;
  const lonSteps = 32;
  const latStart = se.lat * Math.PI / 180;
  const latEnd = nw.lat * Math.PI / 180;
  const lonStart = nw.lon * Math.PI / 180;
  const lonEnd = se.lon * Math.PI / 180;
  const patchRadius = earthRadius * 1.003; // slightly above surface

  const vertices: number[] = [];
  const uvs: number[] = [];
  const indices: number[] = [];

  for (let j = 0; j <= latSteps; j++) {
    const latFrac = j / latSteps;
    const latAngle = latStart + (latEnd - latStart) * (1 - latFrac); // flip: top=north
    for (let i = 0; i <= lonSteps; i++) {
      const lonFrac = i / lonSteps;
      const lonAngle = lonStart + (lonEnd - lonStart) * lonFrac;

      // Spherical → Cartesian (Three.js: Y=up)
      const x = patchRadius * Math.cos(latAngle) * Math.cos(lonAngle);
      const y = patchRadius * Math.sin(latAngle);
      const z = patchRadius * Math.cos(latAngle) * Math.sin(lonAngle);
      vertices.push(x, y, z);
      uvs.push(lonFrac, latFrac);
    }
  }

  for (let j = 0; j < latSteps; j++) {
    for (let i = 0; i < lonSteps; i++) {
      const a = j * (lonSteps + 1) + i;
      const b = a + 1;
      const c = a + lonSteps + 1;
      const d = c + 1;
      indices.push(a, c, b, b, c, d);
    }
  }

  const patchGeo = new THREE.BufferGeometry();
  patchGeo.setAttribute('position', new THREE.Float32BufferAttribute(vertices, 3));
  patchGeo.setAttribute('uv', new THREE.Float32BufferAttribute(uvs, 2));
  patchGeo.setIndex(indices);
  patchGeo.computeVertexNormals();

  const tex = new THREE.CanvasTexture(canvas);
  tex.colorSpace = THREE.SRGBColorSpace;
  tex.needsUpdate = true;

  const TARGET_PATCH_OPACITY = 0.92;
  const patchMat = new THREE.MeshBasicMaterial({
    map: tex,
    transparent: true,
    opacity: 0, // start invisible, fade in
    depthWrite: false,
    side: THREE.DoubleSide,
  });

  // Smooth fade in
  const fadeInPatch = () => {
    patchMat.opacity = Math.min(patchMat.opacity + 0.03, TARGET_PATCH_OPACITY);
    if (patchMat.opacity < TARGET_PATCH_OPACITY) requestAnimationFrame(fadeInPatch);
  };
  requestAnimationFrame(fadeInPatch);

  const patchMesh = new THREE.Mesh(patchGeo, patchMat);
  patchMesh.name = 'earth-focused-patch';
  // Position relative to Earth mesh in orbit group
  patchMesh.position.copy(earth.mesh.position);
  earth.orbit.add(patchMesh);
  group.userData.focusedPatch = patchMesh;

  console.info(`[SolarSystem] Focused patch: ${loaded.length} tiles at zoom ${zoom}, ${nw.lat.toFixed(1)}°/${nw.lon.toFixed(1)}° to ${se.lat.toFixed(1)}°/${se.lon.toFixed(1)}°`);

  return () => {
    earth.orbit.remove(patchMesh);
    patchGeo.dispose();
    patchMat.dispose();
    tex.dispose();
    delete group.userData.focusedPatch;
  };
}
