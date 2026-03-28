// src/components/PrimeRadiant/SolarSystem.ts
// Procedural mini solar system — Sun + 8 planets + all major natural satellites
// NASA/Solar System Scope 2K textures (CC BY 4.0) with procedural fallbacks
// Orbit trails, Kepler speeds, realistic proportions

import * as THREE from 'three';

// ── Texture paths (served from public/textures/planets/) ──
const TEX_BASE = '/textures/planets/';
const loader = new THREE.TextureLoader();

// ── Live weather cloud refresh interval (ms) ──
const CLOUD_REFRESH_MS = 15 * 60 * 1000; // 15 minutes

function loadTex(file: string): THREE.Texture {
  const tex = loader.load(TEX_BASE + file);
  tex.colorSpace = THREE.SRGBColorSpace;
  return tex;
}

// ── Shared vertex shader for procedural moons ──
const VERT = /* glsl */ `
  varying vec3 vPos;
  varying vec3 vNormal;
  varying vec2 vUv;
  void main() {
    vPos = position;
    vNormal = normalize(normalMatrix * normal);
    vUv = uv;
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  }
`;

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

// ── Planet definitions with realistic proportional distances ──
// Distances: inner planets clustered (3.8–8.5), gap, outer planets spread (16–48)
// Speeds: Kepler's 3rd law (∝ distance^-1.5), Earth = 3.0 at d=6.5

const PLANETS: PlanetDef[] = [
  {
    name: 'mercury', radius: 0.12, distance: 3.8, speed: 6.7,
    texture: '2k_mercury.jpg',
    textureDisplacement: '2k_mercury_displacement.jpg',
    fragment: PROC_PLACEHOLDER,
  },
  {
    name: 'venus', radius: 0.3, distance: 5.0, speed: 4.4,
    texture: '2k_venus_surface.jpg',
    textureDisplacement: '2k_venus_displacement.jpg',
    atmosphere: { color: '0.95, 0.75, 0.25', intensity: 0.45, power: 2.5 },
    fragment: PROC_PLACEHOLDER,
  },
  {
    name: 'earth', radius: 0.35, distance: 6.5, speed: 3.0,
    texture: '2k_earth_daymap.jpg',
    textureNight: '2k_earth_nightmap.jpg',
    textureClouds: '2k_earth_clouds.jpg',
    textureSpecular: '2k_earth_specular.jpg',
    textureDisplacement: '2k_earth_displacement.jpg',
    atmosphere: { color: '0.3, 0.6, 1.0', intensity: 0.55, power: 3.0 },
    fragment: PROC_PLACEHOLDER,
    moons: [
      { name: 'moon', radius: 0.1, distance: 1.0, speed: 2.0, texture: '2k_moon.jpg', textureDisplacement: '2k_moon_displacement.jpg', fragment: ROCKY_GREY },
    ],
  },
  {
    name: 'mars', radius: 0.18, distance: 8.5, speed: 2.0,
    texture: '2k_mars.jpg',
    textureDisplacement: '2k_mars_displacement.jpg',
    atmosphere: { color: '0.85, 0.45, 0.35', intensity: 0.2, power: 4.0 },
    fragment: PROC_PLACEHOLDER,
    moons: [
      { name: 'phobos', radius: 0.03, distance: 0.5, speed: 4.0, fragment: ROCKY_DARK },
      { name: 'deimos', radius: 0.02, distance: 0.75, speed: 2.5, fragment: ROCKY_DARK },
    ],
  },
  {
    name: 'jupiter', radius: 0.9, distance: 16, speed: 0.78,
    texture: '2k_jupiter.jpg',
    atmosphere: { color: '0.9, 0.7, 0.4', intensity: 0.25, power: 3.5 },
    fragment: PROC_PLACEHOLDER,
    moons: [
      { name: 'io', radius: 0.09, distance: 1.8, speed: 3.5, fragment: IO_FRAG },
      { name: 'europa', radius: 0.08, distance: 2.3, speed: 2.8, fragment: EUROPA_FRAG },
      { name: 'ganymede', radius: 0.12, distance: 2.9, speed: 2.0, fragment: GANYMEDE_FRAG },
      { name: 'callisto', radius: 0.11, distance: 3.6, speed: 1.4, fragment: CALLISTO_FRAG },
      { name: 'amalthea', radius: 0.025, distance: 1.4, speed: 4.5, fragment: ROCKY_REDDISH },
      { name: 'thebe', radius: 0.015, distance: 1.55, speed: 4.2, fragment: ROCKY_DARK },
      { name: 'metis', radius: 0.01, distance: 1.25, speed: 5.0, fragment: ROCKY_DARK },
      { name: 'adrastea', radius: 0.008, distance: 1.28, speed: 4.9, fragment: ROCKY_DARK },
      { name: 'himalia', radius: 0.02, distance: 5.0, speed: 0.5, inclination: 0.5, fragment: ROCKY_DARK },
    ],
  },
  {
    name: 'saturn', radius: 0.75, distance: 24, speed: 0.42,
    texture: '2k_saturn.jpg',
    atmosphere: { color: '0.85, 0.75, 0.5', intensity: 0.2, power: 3.5 },
    fragment: PROC_PLACEHOLDER,
    moons: [
      { name: 'mimas', radius: 0.04, distance: 1.6, speed: 4.0, fragment: MIMAS_FRAG },
      { name: 'enceladus', radius: 0.045, distance: 1.9, speed: 3.5, fragment: ENCELADUS_FRAG },
      { name: 'tethys', radius: 0.06, distance: 2.2, speed: 3.0, fragment: ICY_WHITE },
      { name: 'dione', radius: 0.065, distance: 2.6, speed: 2.5, fragment: ICY_WHITE },
      { name: 'rhea', radius: 0.08, distance: 3.1, speed: 2.0, fragment: ICY_WHITE },
      { name: 'titan', radius: 0.14, distance: 3.8, speed: 1.3, fragment: TITAN_FRAG },
      { name: 'hyperion', radius: 0.025, distance: 4.3, speed: 1.1, fragment: ROCKY_GREY },
      { name: 'iapetus', radius: 0.07, distance: 5.0, speed: 0.7, inclination: 0.27, fragment: IAPETUS_FRAG },
      { name: 'phoebe', radius: 0.02, distance: 5.8, speed: 0.3, inclination: 2.7, fragment: ROCKY_DARK },
      { name: 'pan', radius: 0.006, distance: 1.15, speed: 5.5, fragment: ICY_WHITE },
      { name: 'atlas', radius: 0.006, distance: 1.18, speed: 5.4, fragment: ICY_WHITE },
      { name: 'prometheus', radius: 0.01, distance: 1.22, speed: 5.2, fragment: ICY_WHITE },
      { name: 'pandora', radius: 0.009, distance: 1.24, speed: 5.1, fragment: ICY_WHITE },
      { name: 'epimetheus', radius: 0.01, distance: 1.35, speed: 4.6, fragment: ICY_WHITE },
      { name: 'janus', radius: 0.012, distance: 1.36, speed: 4.5, fragment: ICY_WHITE },
    ],
  },
  {
    name: 'uranus', radius: 0.45, distance: 36, speed: 0.23, tilt: 1.71,
    texture: '2k_uranus.jpg',
    atmosphere: { color: '0.4, 0.85, 0.75', intensity: 0.3, power: 3.0 },
    fragment: PROC_PLACEHOLDER,
    moons: [
      { name: 'miranda', radius: 0.03, distance: 1.0, speed: 3.5, fragment: MIRANDA_FRAG },
      { name: 'ariel', radius: 0.055, distance: 1.4, speed: 2.8, fragment: ICY_BLUE },
      { name: 'umbriel', radius: 0.055, distance: 1.8, speed: 2.2, fragment: ROCKY_DARK },
      { name: 'titania', radius: 0.07, distance: 2.3, speed: 1.6, fragment: ICY_BLUE },
      { name: 'oberon', radius: 0.068, distance: 2.8, speed: 1.2, fragment: ICY_BLUE },
      { name: 'puck', radius: 0.012, distance: 0.8, speed: 4.2, fragment: ROCKY_DARK },
      { name: 'portia', radius: 0.01, distance: 0.7, speed: 4.5, fragment: ROCKY_DARK },
    ],
  },
  {
    name: 'neptune', radius: 0.42, distance: 48, speed: 0.15,
    texture: '2k_neptune.jpg',
    atmosphere: { color: '0.2, 0.4, 1.0', intensity: 0.35, power: 3.0 },
    fragment: PROC_PLACEHOLDER,
    moons: [
      { name: 'triton', radius: 0.09, distance: 1.5, speed: -1.8, fragment: TRITON_FRAG },
      { name: 'proteus', radius: 0.03, distance: 0.9, speed: 3.0, fragment: ROCKY_DARK },
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
const PLANET_VERT = /* glsl */ `
  uniform sampler2D uDisplacementMap;
  uniform float uDisplacementScale;  // 0 = no displacement
  uniform float uHasDisplacement;

  varying vec3 vWorldPos;
  varying vec3 vWorldNormal;
  varying vec2 vUv;
  varying vec3 vViewDir;

  void main() {
    vUv = uv;

    // Displace vertex along normal based on height map
    vec3 displacedPos = position;
    if (uHasDisplacement > 0.5) {
      float height = texture2D(uDisplacementMap, uv).r;
      // Offset from 0.5 so mid-grey = no displacement, white = peak, black = valley
      displacedPos += normal * (height - 0.3) * uDisplacementScale;
    }

    vec4 worldPos = modelMatrix * vec4(displacedPos, 1.0);
    vWorldPos = worldPos.xyz;
    vWorldNormal = normalize((modelMatrix * vec4(normal, 0.0)).xyz);
    vViewDir = normalize(cameraPosition - worldPos.xyz);
    gl_Position = projectionMatrix * viewMatrix * worldPos;
  }
`;

// Day/night with smooth terminator, bump-derived shading, specular, atmosphere rim
const PLANET_FRAG = /* glsl */ `
  uniform sampler2D uMap;
  uniform sampler2D uNightMap;
  uniform sampler2D uSpecMap;
  uniform vec3 uSunPos;
  uniform float uHasNight;
  uniform float uHasSpec;
  uniform float uAtmoColor;   // 0=none, 1=blue(earth), 2=orange(venus/titan)
  uniform float uRoughness;
  uniform vec2 uTexelSize;    // 1/textureWidth, 1/textureHeight for bump
  uniform float uMonth;       // 1-12, current month (for seasonal snow on Earth)
  uniform float uIsEarth;     // 1.0 for Earth, 0.0 otherwise

  varying vec3 vWorldPos;
  varying vec3 vWorldNormal;
  varying vec2 vUv;
  varying vec3 vViewDir;

  vec3 getBumpNormal() {
    // Derive bump from luminance differences in texture
    float tl = dot(texture2D(uMap, vUv + vec2(-uTexelSize.x, uTexelSize.y)).rgb, vec3(0.299, 0.587, 0.114));
    float t  = dot(texture2D(uMap, vUv + vec2(0.0, uTexelSize.y)).rgb, vec3(0.299, 0.587, 0.114));
    float tr = dot(texture2D(uMap, vUv + vec2(uTexelSize.x, uTexelSize.y)).rgb, vec3(0.299, 0.587, 0.114));
    float l  = dot(texture2D(uMap, vUv + vec2(-uTexelSize.x, 0.0)).rgb, vec3(0.299, 0.587, 0.114));
    float r  = dot(texture2D(uMap, vUv + vec2(uTexelSize.x, 0.0)).rgb, vec3(0.299, 0.587, 0.114));
    float bl = dot(texture2D(uMap, vUv + vec2(-uTexelSize.x, -uTexelSize.y)).rgb, vec3(0.299, 0.587, 0.114));
    float b  = dot(texture2D(uMap, vUv + vec2(0.0, -uTexelSize.y)).rgb, vec3(0.299, 0.587, 0.114));
    float br = dot(texture2D(uMap, vUv + vec2(uTexelSize.x, -uTexelSize.y)).rgb, vec3(0.299, 0.587, 0.114));

    float dX = (tr + 2.0*r + br) - (tl + 2.0*l + bl);
    float dY = (bl + 2.0*b + br) - (tl + 2.0*t + tr);

    // Perturb normal (strength factor controls bump intensity)
    float bumpStrength = 1.5;
    vec3 N = normalize(vWorldNormal);
    vec3 T = normalize(cross(N, vec3(0.0, 1.0, 0.0001)));
    vec3 B = cross(N, T);
    return normalize(N + (T * dX + B * dY) * bumpStrength);
  }

  void main() {
    vec3 sunDir = normalize(uSunPos - vWorldPos);
    vec3 N = getBumpNormal();
    float NdotL = dot(N, sunDir);

    // Smooth terminator — gradual transition from day to night
    float dayFactor = smoothstep(-0.15, 0.2, NdotL);

    // Day color from texture
    vec3 dayColor = texture2D(uMap, vUv).rgb;

    // Seasonal snow/ice coverage (Earth only)
    if (uIsEarth > 0.5) {
      // Latitude from UV: 0.0 = south pole, 0.5 = equator, 1.0 = north pole
      float lat = (vUv.y - 0.5) * 2.0; // -1 (south) to +1 (north)
      float absLat = abs(lat);

      // Seasonal offset: northern winter = months 11,12,1,2 → snow pushes south
      // uMonth 1-12; convert to seasonal factor where 0 = summer solstice, 1 = winter solstice
      // For northern hemisphere: winter peak at month ~1 (Jan), summer at ~7 (Jul)
      float monthRad = (uMonth - 1.0) / 12.0 * 6.28318;
      float winterFactorN = (1.0 + cos(monthRad)) * 0.5;          // 1.0 in Jan, 0.0 in Jul
      float winterFactorS = (1.0 + cos(monthRad + 3.14159)) * 0.5; // opposite

      // Snow line: how far from pole snow extends (0.5 = 60deg, 0.7 = 45deg, 0.9 = near equator)
      // Permanent ice caps above ~75deg (absLat > 0.83)
      // Seasonal snow extends further in winter
      float snowLineN = 0.55 + winterFactorN * 0.25; // 0.55 (summer) to 0.80 (winter)
      float snowLineS = 0.55 + winterFactorS * 0.25;

      float snowAmount = 0.0;
      if (lat > 0.0) {
        // Northern hemisphere
        snowAmount = smoothstep(snowLineN - 0.15, snowLineN + 0.05, absLat);
      } else {
        // Southern hemisphere
        snowAmount = smoothstep(snowLineS - 0.15, snowLineS + 0.05, absLat);
      }

      // Permanent polar ice caps (always white above ~80deg)
      float iceCap = smoothstep(0.78, 0.88, absLat);
      snowAmount = max(snowAmount, iceCap);

      // Blend: snow is bright white with slight blue tint
      vec3 snowColor = vec3(0.92, 0.95, 1.0);
      dayColor = mix(dayColor, snowColor, snowAmount * 0.7);
    }

    // Diffuse shading (Lambert with slight ambient)
    float diffuse = max(NdotL, 0.0);
    vec3 litDay = dayColor * (0.03 + 0.97 * diffuse);

    // Specular (Blinn-Phong)
    vec3 halfDir = normalize(sunDir + vViewDir);
    float specAngle = max(dot(N, halfDir), 0.0);
    float specPower = mix(16.0, 64.0, 1.0 - uRoughness);
    float spec = pow(specAngle, specPower) * (1.0 - uRoughness) * 0.6;
    if (uHasSpec > 0.5) {
      float specMask = texture2D(uSpecMap, vUv).r;
      spec *= specMask;
    }
    litDay += vec3(1.0, 0.95, 0.9) * spec * dayFactor;

    // Night side
    vec3 nightColor = vec3(0.0);
    if (uHasNight > 0.5) {
      nightColor = texture2D(uNightMap, vUv).rgb * 0.8;
    }

    // Blend day/night across terminator
    vec3 surfaceColor = mix(nightColor, litDay, dayFactor);

    // Atmosphere rim glow
    float fresnel = pow(1.0 - abs(dot(normalize(vWorldNormal), vViewDir)), 3.0);
    if (uAtmoColor > 0.5 && uAtmoColor < 1.5) {
      // Earth — blue atmosphere
      surfaceColor += vec3(0.25, 0.45, 1.0) * fresnel * 0.35 * dayFactor;
      surfaceColor += vec3(0.05, 0.1, 0.3) * fresnel * 0.2 * (1.0 - dayFactor); // faint blue on night side
    } else if (uAtmoColor > 1.5) {
      // Venus — orange haze
      surfaceColor += vec3(1.0, 0.7, 0.2) * fresnel * 0.3;
    }

    gl_FragColor = vec4(surfaceColor, 1.0);
  }
`;

// ── Create a textured planet mesh with realistic shading ──
function createPlanetMesh(def: PlanetDef, scale: number): THREE.Mesh {
  // More segments when displacement is active for smoother terrain
  const baseSegments = def.radius > 0.5 ? 48 : def.radius > 0.2 ? 32 : 24;
  const segments = def.textureDisplacement ? Math.max(baseSegments, 64) : baseSegments;
  const geo = new THREE.SphereGeometry(def.radius * scale, segments, segments);

  if (def.texture) {
    const map = loadTex(def.texture);
    const texSize = 2048; // 2K textures

    const uniforms: Record<string, THREE.IUniform> = {
      uMap: { value: map },
      uNightMap: { value: def.textureNight ? loadTex(def.textureNight) : null },
      uSpecMap: { value: def.textureSpecular ? loadTex(def.textureSpecular) : null },
      uSunPos: { value: new THREE.Vector3(0, 0, 0) }, // updated per frame
      uHasNight: { value: def.textureNight ? 1.0 : 0.0 },
      uHasSpec: { value: def.textureSpecular ? 1.0 : 0.0 },
      uAtmoColor: { value: def.name === 'earth' ? 1.0 : def.name === 'venus' ? 2.0 : 0.0 },
      uRoughness: { value: def.name === 'earth' ? 0.6 : 0.85 },
      uTexelSize: { value: new THREE.Vector2(1 / texSize, 1 / texSize) },
      uMonth: { value: new Date().getMonth() + 1 }, // 1-12
      uIsEarth: { value: def.name === 'earth' ? 1.0 : 0.0 },
      uDisplacementMap: { value: def.textureDisplacement ? loadTex(def.textureDisplacement) : null },
      uHasDisplacement: { value: def.textureDisplacement ? 1.0 : 0.0 },
      // Scale displacement relative to planet radius — Mars gets more (Olympus Mons!)
      uDisplacementScale: { value: def.textureDisplacement ? def.radius * scale * (def.name === 'mars' ? 0.12 : 0.05) : 0.0 },
    };

    const mat = new THREE.ShaderMaterial({
      uniforms,
      vertexShader: PLANET_VERT,
      fragmentShader: PLANET_FRAG,
    });
    const mesh = new THREE.Mesh(geo, mat);
    mesh.userData.isPlanetShader = true;
    return mesh;
  }

  // Procedural fallback
  const mat = new THREE.ShaderMaterial({
    uniforms: { uTime: { value: 0 } },
    vertexShader: VERT,
    fragmentShader: def.fragment,
  });
  return new THREE.Mesh(geo, mat);
}

// ── Create a textured moon mesh, or fall back to procedural shader ──
function createMoonMesh(def: MoonDef, scale: number): THREE.Mesh {
  const baseSegments = def.radius * scale > 0.02 ? 16 : 8;
  const segments = def.textureDisplacement ? Math.max(baseSegments, 48) : baseSegments;
  const geo = new THREE.SphereGeometry(def.radius * scale, segments, segments);

  if (def.texture) {
    const map = loadTex(def.texture);
    const matOpts: THREE.MeshStandardMaterialParameters = { map, roughness: 0.95, metalness: 0 };
    if (def.textureDisplacement) {
      matOpts.displacementMap = loadTex(def.textureDisplacement);
      matOpts.displacementScale = def.radius * scale * 0.06;
      matOpts.displacementBias = -def.radius * scale * 0.02;
    }
    const mat = new THREE.MeshStandardMaterial(matOpts);
    return new THREE.Mesh(geo, mat);
  }

  const hasTime = def.fragment.includes('uTime');
  const mat = new THREE.ShaderMaterial({
    uniforms: hasTime ? { uTime: { value: 0 } } : {},
    vertexShader: VERT,
    fragmentShader: def.fragment,
  });
  return new THREE.Mesh(geo, mat);
}

// ── Create orbit trail with per-vertex fading ──
function createOrbitTrail(distance: number, scale: number, color: number = 0x334466): THREE.Line {
  const segments = 128;
  const positions = new Float32Array((segments + 1) * 3);
  const alphas = new Float32Array(segments + 1);
  for (let i = 0; i <= segments; i++) {
    const angle = (i / segments) * Math.PI * 2;
    positions[i * 3] = Math.cos(angle) * distance * scale;
    positions[i * 3 + 1] = 0;
    positions[i * 3 + 2] = Math.sin(angle) * distance * scale;
    alphas[i] = 0.08; // initial uniform alpha
  }
  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
  geo.setAttribute('aAlpha', new THREE.BufferAttribute(alphas, 1));

  const col = new THREE.Color(color);
  const mat = new THREE.ShaderMaterial({
    uniforms: {
      uColor: { value: col },
    },
    vertexShader: /* glsl */ `
      attribute float aAlpha;
      varying float vAlpha;
      void main() {
        vAlpha = aAlpha;
        gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
      }
    `,
    fragmentShader: /* glsl */ `
      uniform vec3 uColor;
      varying float vAlpha;
      void main() {
        gl_FragColor = vec4(uColor, vAlpha);
      }
    `,
    transparent: true,
    depthWrite: false,
  });
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
  // Smaller, delicate scale matching 512x128 canvas aspect ratio
  const labelScale = Math.max(scale * 5, 0.04);
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

  // ── Lighting for textured planets ──
  const sunLight = new THREE.PointLight(0xffffff, 1.5, 80 * scale);
  sunLight.position.set(0, 0, 0);
  group.add(sunLight);

  // Faint ambient so dark sides aren't pure black
  const ambient = new THREE.AmbientLight(0x111122, 0.15);
  group.add(ambient);

  // ── Sun (animated shader — roiling plasma with limb darkening) ──
  const sunGeo = new THREE.SphereGeometry(2 * scale, 48, 48);
  const sunTex = loadTex('2k_sun.jpg');
  const sunMat = new THREE.ShaderMaterial({
    uniforms: {
      uTime: { value: 0 },
      uSunTex: { value: sunTex },
    },
    vertexShader: /* glsl */ `
      varying vec3 vNormal;
      varying vec3 vViewDir;
      varying vec2 vUv;
      varying vec3 vPos;
      void main() {
        vUv = uv;
        vPos = position;
        vNormal = normalize(normalMatrix * normal);
        vec4 wp = modelMatrix * vec4(position, 1.0);
        vViewDir = normalize(cameraPosition - wp.xyz);
        gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
      }
    `,
    fragmentShader: /* glsl */ `
      uniform float uTime;
      uniform sampler2D uSunTex;
      varying vec3 vNormal;
      varying vec3 vViewDir;
      varying vec2 vUv;
      varying vec3 vPos;

      // Hash and noise functions
      float hash(vec3 p) { return fract(sin(dot(p, vec3(1.3, 1.7, 1.9))) * 43758.5); }
      float noise(vec3 x) {
        vec3 i = floor(x), f = fract(x);
        f = f * f * (3.0 - 2.0 * f);
        return mix(
          mix(mix(hash(i), hash(i + vec3(1,0,0)), f.x),
              mix(hash(i + vec3(0,1,0)), hash(i + vec3(1,1,0)), f.x), f.y),
          mix(mix(hash(i + vec3(0,0,1)), hash(i + vec3(1,0,1)), f.x),
              mix(hash(i + vec3(0,1,1)), hash(i + vec3(1,1,1)), f.x), f.y), f.z);
      }
      float fbm(vec3 p) {
        float v = 0.0, a = 0.5;
        for (int i = 0; i < 6; i++) { v += a * noise(p); p *= 2.1; a *= 0.48; }
        return v;
      }

      void main() {
        float t = uTime * 0.08;

        // Base texture color
        vec3 baseTex = texture2D(uSunTex, vUv).rgb;

        // Granulation — high-frequency convection cells
        float granulation = fbm(vPos * 12.0 + vec3(t * 0.3, t * 0.2, t * 0.25));

        // Large-scale turbulence — slow-rolling plasma currents
        float turbulence = fbm(vPos * 3.0 + vec3(t * 0.15, -t * 0.1, t * 0.12));

        // Sunspots — darker regions that drift slowly
        float spots = smoothstep(0.58, 0.65, fbm(vPos * 4.5 + vec3(t * 0.02, t * 0.015, -t * 0.01)));

        // Build surface color
        vec3 deepOrange = vec3(0.85, 0.35, 0.05);
        vec3 brightYellow = vec3(1.0, 0.92, 0.7);
        vec3 hotWhite = vec3(1.0, 0.98, 0.95);

        // Blend texture with procedural plasma
        vec3 plasma = mix(deepOrange, brightYellow, turbulence);
        plasma = mix(plasma, hotWhite, granulation * 0.3);
        vec3 col = mix(baseTex * 1.1, plasma, 0.55);

        // Apply sunspots (darker cooler regions)
        col = mix(col, vec3(0.4, 0.15, 0.02), spots * 0.6);

        // Flare hotspots — occasional bright blue-white flares
        float flare = smoothstep(0.72, 0.85, noise(vPos * 8.0 + vec3(t * 0.5, -t * 0.3, t * 0.4)));
        col += vec3(0.6, 0.7, 1.0) * flare * 0.35;

        // Limb darkening — edges darker than center
        float limbFactor = pow(max(dot(normalize(vNormal), normalize(vViewDir)), 0.0), 0.4);
        col *= mix(0.3, 1.0, limbFactor);

        // Pulsing emissive brightness
        float pulse = 1.0 + 0.04 * sin(uTime * 0.5) + 0.02 * sin(uTime * 1.3);
        col *= pulse;

        // Slight boost to overall brightness
        col *= 1.15;

        gl_FragColor = vec4(col, 1.0);
      }
    `,
  });
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

  // Generate an anamorphic streak texture (horizontal lens artifact)
  function createStreakTexture(w: number, h: number): THREE.CanvasTexture {
    const c = document.createElement('canvas');
    c.width = w; c.height = h;
    const cx = c.getContext('2d')!;
    const g = cx.createLinearGradient(0, 0, w, 0);
    g.addColorStop(0, 'rgba(255,200,100,0)');
    g.addColorStop(0.15, 'rgba(255,220,150,0.06)');
    g.addColorStop(0.4, 'rgba(255,240,200,0.2)');
    g.addColorStop(0.5, 'rgba(255,250,240,0.35)');
    g.addColorStop(0.6, 'rgba(255,240,200,0.2)');
    g.addColorStop(0.85, 'rgba(255,220,150,0.06)');
    g.addColorStop(1, 'rgba(255,200,100,0)');
    cx.fillStyle = g;
    cx.fillRect(0, 0, w, h);
    // Thin vertical center line
    const g2 = cx.createLinearGradient(0, 0, 0, h);
    g2.addColorStop(0, 'rgba(255,255,255,0)');
    g2.addColorStop(0.4, 'rgba(255,255,255,0.15)');
    g2.addColorStop(0.5, 'rgba(255,255,255,0.3)');
    g2.addColorStop(0.6, 'rgba(255,255,255,0.15)');
    g2.addColorStop(1, 'rgba(255,255,255,0)');
    cx.fillStyle = g2;
    cx.fillRect(0, 0, w, h);
    const t = new THREE.CanvasTexture(c);
    t.needsUpdate = true;
    return t;
  }

  // Primary glow (large, warm)
  const mainGlow = new THREE.Sprite(new THREE.SpriteMaterial({
    map: createFlareTexture(256, 'rgba(255,220,150,0.4)', 0.5),
    transparent: true, depthWrite: false, blending: THREE.AdditiveBlending,
  }));
  mainGlow.scale.set(8 * scale, 8 * scale, 1);
  flareGroup.add(mainGlow);

  // Inner hot core (small, white)
  const hotCore = new THREE.Sprite(new THREE.SpriteMaterial({
    map: createFlareTexture(128, 'rgba(255,255,240,0.6)', 0.3),
    transparent: true, depthWrite: false, blending: THREE.AdditiveBlending,
  }));
  hotCore.scale.set(4 * scale, 4 * scale, 1);
  flareGroup.add(hotCore);

  // Anamorphic horizontal streak
  const streak = new THREE.Sprite(new THREE.SpriteMaterial({
    map: createStreakTexture(512, 32),
    transparent: true, depthWrite: false, blending: THREE.AdditiveBlending,
  }));
  streak.scale.set(20 * scale, 0.8 * scale, 1);
  flareGroup.add(streak);

  // Secondary rainbow ghosts (displaced along view axis)
  const ghostColors = ['rgba(100,180,255,0.12)', 'rgba(255,150,100,0.08)', 'rgba(150,255,150,0.06)'];
  const ghostScales = [2.5, 1.5, 3.0];
  const ghostOffsets = [5, 8, 12];
  for (let i = 0; i < ghostColors.length; i++) {
    const ghost = new THREE.Sprite(new THREE.SpriteMaterial({
      map: createFlareTexture(128, ghostColors[i], 0.6),
      transparent: true, depthWrite: false, blending: THREE.AdditiveBlending,
    }));
    ghost.scale.set(ghostScales[i] * scale, ghostScales[i] * scale, 1);
    ghost.position.x = ghostOffsets[i] * scale;
    ghost.name = `flare-ghost-${i}`;
    flareGroup.add(ghost);
  }

  group.add(flareGroup);
  group.userData.flareGroup = flareGroup;

  // Sun corona glow
  const coronaGeo = new THREE.SphereGeometry(3 * scale, 16, 16);
  const coronaMat = new THREE.ShaderMaterial({
    uniforms: {},
    vertexShader: `varying vec3 vNormal;varying vec3 vViewDir;void main(){vNormal=normalize(normalMatrix*normal);vec4 wp=modelMatrix*vec4(position,1.);vViewDir=normalize(cameraPosition-wp.xyz);gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.);}`,
    fragmentShader: `varying vec3 vNormal;varying vec3 vViewDir;void main(){float f=1.-abs(dot(vNormal,vViewDir));f=pow(f,2.5);gl_FragColor=vec4(1.,.7,.2,f*.4);}`,
    transparent: true, side: THREE.BackSide, depthWrite: false, blending: THREE.AdditiveBlending,
  });
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
    const trail = createOrbitTrail(def.distance, scale);
    trail.name = `trail-${def.name}`;
    group.add(trail); // add to group root, not orbit (orbit rotates)
    orbitTrails.set(def.name, trail);

    const mesh = createPlanetMesh(def, scale);
    mesh.name = def.name;
    mesh.frustumCulled = false; // prevent disappearing on close zoom
    mesh.position.x = def.distance * scale;
    if (def.tilt) mesh.rotation.z = def.tilt;
    orbit.add(mesh);

    // Atmosphere shell — Fresnel glow on limb, transparent at center
    if (def.atmosphere) {
      const atmoSegs = def.radius > 0.5 ? 32 : 24;
      const atmoGeo = new THREE.SphereGeometry(def.radius * 1.05 * scale, atmoSegs, atmoSegs);
      const atmoMat = new THREE.ShaderMaterial({
        uniforms: {
          uColor: { value: new THREE.Vector3(...def.atmosphere.color.split(',').map(c => parseFloat(c.trim())) as [number, number, number]) },
          uIntensity: { value: def.atmosphere.intensity },
          uPower: { value: def.atmosphere.power },
        },
        vertexShader: /* glsl */ `
          varying vec3 vNormal;
          varying vec3 vViewDir;
          void main() {
            vNormal = normalize(normalMatrix * normal);
            vec4 wp = modelMatrix * vec4(position, 1.0);
            vViewDir = normalize(cameraPosition - wp.xyz);
            gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
          }
        `,
        fragmentShader: /* glsl */ `
          uniform vec3 uColor;
          uniform float uIntensity;
          uniform float uPower;
          varying vec3 vNormal;
          varying vec3 vViewDir;
          void main() {
            float fresnel = 1.0 - abs(dot(vNormal, vViewDir));
            float atmo = pow(fresnel, uPower) * uIntensity;
            gl_FragColor = vec4(uColor, atmo);
          }
        `,
        transparent: true,
        side: THREE.BackSide,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
      });
      const atmoMesh = new THREE.Mesh(atmoGeo, atmoMat);
      atmoMesh.name = `atmo-${def.name}`;
      atmoMesh.position.copy(mesh.position);
      orbit.add(atmoMesh);
    }

    // Planet name label (initially hidden)
    const label = createPlanetLabel(def.name, scale);
    label.position.copy(mesh.position);
    label.position.y += def.radius * scale + 0.03; // above the planet
    orbit.add(label);
    planetLabels.set(def.name, label);

    let cloudsMesh: THREE.Mesh | undefined;
    let cloudsHighMesh: THREE.Mesh | undefined;

    // Earth clouds — two layers for depth parallax
    if (def.name === 'earth' && def.textureClouds) {
      const cloudsTex = loadTex(def.textureClouds);

      // Primary cloud layer — alphaMap for defined cloud gaps
      const cloudsMat = new THREE.MeshStandardMaterial({
        map: cloudsTex,
        alphaMap: cloudsTex,
        transparent: true,
        opacity: 0.55,
        depthWrite: false,
      });
      cloudsMesh = new THREE.Mesh(
        new THREE.SphereGeometry((def.radius + 0.015) * scale, 32, 32),
        cloudsMat,
      );
      cloudsMesh.name = 'earth-clouds';
      cloudsMesh.position.copy(mesh.position);
      orbit.add(cloudsMesh);

      // High-altitude thin cloud layer — parallax depth
      const cloudsHighTex = loadTex(def.textureClouds);
      const cloudsHighMat = new THREE.MeshStandardMaterial({
        map: cloudsHighTex,
        alphaMap: cloudsHighTex,
        transparent: true,
        opacity: 0.15,
        depthWrite: false,
      });
      cloudsHighMesh = new THREE.Mesh(
        new THREE.SphereGeometry(def.radius * 1.08 * scale, 32, 32),
        cloudsHighMat,
      );
      cloudsHighMesh.name = 'earth-clouds-high';
      cloudsHighMesh.position.copy(mesh.position);
      orbit.add(cloudsHighMesh);
    }

    // Saturn rings (textured)
    if (def.name === 'saturn') {
      const ringGeo = new THREE.RingGeometry(1.3 * scale, 2.0 * scale, 128);
      // Adjust UVs so texture maps radially
      const pos = ringGeo.attributes.position;
      const uv = ringGeo.attributes.uv;
      for (let i = 0; i < pos.count; i++) {
        const x = pos.getX(i), z = pos.getZ(i);
        const r = Math.sqrt(x * x + z * z);
        const rNorm = (r - 1.3 * scale) / ((2.0 - 1.3) * scale);
        uv.setXY(i, rNorm, 0.5);
      }
      const ringTex = loadTex('2k_saturn_ring_alpha.png');
      const ringMat = new THREE.MeshBasicMaterial({
        map: ringTex,
        side: THREE.DoubleSide,
        transparent: true,
        opacity: 0.6,
        depthWrite: false,
      });
      const ring = new THREE.Mesh(ringGeo, ringMat);
      ring.rotation.x = Math.PI / 2.2;
      ring.position.copy(mesh.position);
      orbit.add(ring);
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
      ring.position.copy(mesh.position);
      orbit.add(ring);
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
      ring.position.copy(mesh.position);
      orbit.add(ring);
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

        // Titan atmosphere
        if (moonDef.name === 'titan') {
          const titanAtmoGeo = new THREE.SphereGeometry((moonDef.radius + 0.03) * scale, 12, 12);
          const titanAtmoMat = new THREE.ShaderMaterial({
            uniforms: {},
            vertexShader: `varying vec3 vN;varying vec3 vV;void main(){vN=normalize(normalMatrix*normal);vec4 w=modelMatrix*vec4(position,1.);vV=normalize(cameraPosition-w.xyz);gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.);}`,
            fragmentShader: `varying vec3 vN;varying vec3 vV;void main(){float f=pow(1.-abs(dot(vN,vV)),3.);gl_FragColor=vec4(0.85,0.6,0.2,f*.35);}`,
            transparent: true, side: THREE.BackSide, depthWrite: false, blending: THREE.AdditiveBlending,
          });
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

        orbit.add(moonOrbit);
        moonInstances.push({ mesh: moonMesh, def: moonDef, planetMesh: mesh, orbitGroup: moonOrbit });
      }
    }

    group.add(orbit);
    planetMeshes.push({ mesh, orbit, def, clouds: cloudsMesh, cloudsHigh: cloudsHighMesh });
  }

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
// UPDATE
// ─────────────────────────────────────────────────────────────
const _sunWorldPos = new THREE.Vector3();

export function updateSolarSystem(group: THREE.Group, time: number): void {
  const planets = group.userData.planets as { mesh: THREE.Mesh; orbit: THREE.Group; def: PlanetDef; clouds?: THREE.Mesh; cloudsHigh?: THREE.Mesh }[] | undefined;
  if (!planets) return;

  // Sun world position (group origin)
  group.getWorldPosition(_sunWorldPos);

  // Animate sun shader
  const sunMesh = group.getObjectByName('sun') as THREE.Mesh | undefined;
  if (sunMesh && sunMesh.material instanceof THREE.ShaderMaterial && sunMesh.material.uniforms.uTime) {
    sunMesh.material.uniforms.uTime.value = time;
  }

  const trails = group.userData.orbitTrails as Map<string, THREE.Line> | undefined;

  for (const { mesh, orbit, def, clouds, cloudsHigh } of planets) {
    // Orbit rotation
    orbit.rotation.y = time * def.speed * 0.1;

    // Planet self-rotation
    mesh.rotation.y = time * 0.5;

    // Primary clouds rotate noticeably faster than the planet
    if (clouds) {
      clouds.rotation.y = time * 0.62;
    }

    // High-altitude clouds rotate slower, opposite tilt for parallax
    if (cloudsHigh) {
      cloudsHigh.rotation.y = time * 0.45;
    }

    // Update planet shader uniforms — sun position for day/night + bump
    if (mesh.material instanceof THREE.ShaderMaterial) {
      const u = mesh.material.uniforms;
      if (u.uSunPos) u.uSunPos.value.copy(_sunWorldPos);
      if (u.uTime) u.uTime.value = time;
    }

    // ── Fading orbit trail — bright near planet, fades 180° behind ──
    const trail = trails?.get(def.name);
    if (trail) {
      const segments = trail.userData.trailSegments as number;
      const alphaAttr = trail.geometry.getAttribute('aAlpha') as THREE.BufferAttribute;
      // Current planet angle in its orbit
      const planetAngle = time * def.speed * 0.1; // matches orbit.rotation.y
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

  // ── Animate sun lens flare ──
  const flareGroup = group.userData.flareGroup as THREE.Group | undefined;
  if (flareGroup) {
    // Pulse the main glow subtly
    const pulse = 1.0 + 0.15 * Math.sin(time * 0.3) + 0.08 * Math.sin(time * 0.7);
    const mainGlow = flareGroup.children[0];
    if (mainGlow) mainGlow.scale.setScalar(8 * 0.03 * pulse); // scale * pulse

    // Rotate streak slowly for cinematic feel
    const streak = flareGroup.children[2];
    if (streak) {
      streak.material.rotation = time * 0.02;
    }
  }

  // ── Animate all moons ──
  const moons = group.userData.moonInstances as MoonInstance[] | undefined;
  if (!moons) return;

  for (const { mesh, def, orbitGroup } of moons) {
    orbitGroup.rotation.y = time * def.speed * 0.3;
    mesh.rotation.y = time * Math.abs(def.speed) * 0.15;

    if (mesh.material instanceof THREE.ShaderMaterial) {
      if (mesh.material.uniforms?.uTime) mesh.material.uniforms.uTime.value = time;
    }
  }
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

  const canvas = document.createElement('canvas');
  canvas.width = 2048;
  canvas.height = 1024;
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

    // Extract clouds: GIBS tiles are true-color satellite imagery (land + ocean + clouds).
    // Clouds are bright white; extract brightness and use as cloud alpha overlay.
    const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
    const data = imageData.data;
    for (let i = 0; i < data.length; i += 4) {
      const r = data[i], g = data[i + 1], b = data[i + 2];
      const brightness = (r + g + b) / 3;
      // Only treat bright pixels as clouds (threshold: > 160 brightness)
      // Below threshold = land/ocean = transparent
      const cloudAlpha = Math.max(0, (brightness - 140) / 115) * 255;
      data[i] = 255;
      data[i + 1] = 255;
      data[i + 2] = 255;
      data[i + 3] = Math.min(255, cloudAlpha * 1.3);
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

    // OWM alpha processing
    const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
    const data = imageData.data;
    for (let i = 0; i < data.length; i += 4) {
      const brightness = (data[i] + data[i + 1] + data[i + 2]) / 3;
      data[i] = 255;
      data[i + 1] = 255;
      data[i + 2] = 255;
      data[i + 3] = Math.min(255, brightness * 1.5);
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
