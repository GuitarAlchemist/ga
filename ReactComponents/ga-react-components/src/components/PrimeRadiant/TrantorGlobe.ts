// src/components/PrimeRadiant/TrantorGlobe.ts
// Holographic Trantor globe — the ecumenopolis capital of the Galactic Empire
// Entirely city-covered planet with orbital ring and atmosphere glow
// Foundation-era aesthetic: gold wireframe, scanlines, holographic flicker

import * as THREE from 'three';

// ---------------------------------------------------------------------------
// Holographic planet shader — Fresnel glow + scanlines + flicker
// ---------------------------------------------------------------------------
const globeVertexShader = /* glsl */ `
  varying vec3 vNormal;
  varying vec3 vViewDir;
  varying vec3 vWorldPos;
  varying vec2 vUv;

  void main() {
    vUv = uv;
    vNormal = normalize(normalMatrix * normal);
    vec4 worldPos = modelMatrix * vec4(position, 1.0);
    vWorldPos = worldPos.xyz;
    vViewDir = normalize(cameraPosition - worldPos.xyz);
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  }
`;

const globeFragmentShader = /* glsl */ `
  uniform vec3 uColor;
  uniform float uTime;
  uniform float uOpacity;

  varying vec3 vNormal;
  varying vec3 vViewDir;
  varying vec3 vWorldPos;
  varying vec2 vUv;

  void main() {
    float fresnel = 1.0 - abs(dot(vNormal, vViewDir));
    fresnel = pow(fresnel, 1.8);

    // Latitude/longitude grid lines
    float lat = abs(sin(vWorldPos.y * 12.0));
    float lon = abs(sin(atan(vWorldPos.z, vWorldPos.x) * 8.0));
    float grid = smoothstep(0.92, 1.0, lat) + smoothstep(0.92, 1.0, lon);
    grid *= 0.3;

    // Scanlines
    float scanline = sin(vWorldPos.y * 30.0 - uTime * 2.0) * 0.5 + 0.5;
    scanline = pow(scanline, 6.0) * 0.15;

    // Holographic flicker
    float flicker = 1.0;
    float seed = fract(sin(floor(uTime * 8.0) * 43758.5453));
    if (seed > 0.93) flicker = 0.35;

    float alpha = (fresnel * 0.6 + 0.2 + grid + scanline) * uOpacity * flicker;
    alpha = clamp(alpha, 0.0, 1.0);

    vec3 col = uColor * (0.4 + fresnel * 0.7 + grid * 0.6 + scanline * 0.4);

    gl_FragColor = vec4(col, alpha);
  }
`;

// ---------------------------------------------------------------------------
// Atmosphere shader — blue-ish Fresnel rim glow
// ---------------------------------------------------------------------------
const atmosphereFragmentShader = /* glsl */ `
  uniform vec3 uColor;
  uniform float uTime;

  varying vec3 vNormal;
  varying vec3 vViewDir;
  varying vec3 vWorldPos;
  varying vec2 vUv;

  void main() {
    float fresnel = 1.0 - abs(dot(vNormal, vViewDir));
    fresnel = pow(fresnel, 3.0);

    // Subtle atmosphere flicker
    float flicker = 1.0 + sin(uTime * 1.5) * 0.05;

    float alpha = fresnel * 0.45 * flicker;
    vec3 col = uColor * fresnel * 1.2;

    gl_FragColor = vec4(col, alpha);
  }
`;

// ---------------------------------------------------------------------------
// Create Trantor holographic globe
// ---------------------------------------------------------------------------
export function createTrantorGlobe(scale: number = 1): THREE.Group {
  const group = new THREE.Group();
  group.userData.isTrantorGlobe = true;
  const s = scale;

  // ── Planet wireframe — IcosahedronGeometry detail 3 ──
  const planetGeo = new THREE.IcosahedronGeometry(1.0 * s, 3);
  const planetMat = new THREE.ShaderMaterial({
    uniforms: {
      uTime: { value: 0 },
      uColor: { value: new THREE.Color('#FFD700') },
      uOpacity: { value: 0.5 },
    },
    vertexShader: globeVertexShader,
    fragmentShader: globeFragmentShader,
    transparent: true,
    wireframe: true,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    side: THREE.DoubleSide,
  });
  const planet = new THREE.Mesh(planetGeo, planetMat);
  planet.name = 'trantor-planet';
  group.add(planet);

  // ── City lights — scattered points on the surface ──
  const lightCount = 250;
  const lightPositions = new Float32Array(lightCount * 3);
  const lightColors = new Float32Array(lightCount * 3);
  const lightBaseOpacities = new Float32Array(lightCount);

  for (let i = 0; i < lightCount; i++) {
    // Random point on unit sphere
    const theta = Math.random() * Math.PI * 2;
    const phi = Math.acos(2 * Math.random() - 1);
    const r = 1.01 * s; // slightly above surface
    lightPositions[i * 3] = r * Math.sin(phi) * Math.cos(theta);
    lightPositions[i * 3 + 1] = r * Math.sin(phi) * Math.sin(theta);
    lightPositions[i * 3 + 2] = r * Math.cos(phi);

    // Gold/warm white city glow
    const warm = 0.7 + Math.random() * 0.3;
    lightColors[i * 3] = warm;
    lightColors[i * 3 + 1] = warm * 0.85;
    lightColors[i * 3 + 2] = warm * 0.4;

    lightBaseOpacities[i] = 0.3 + Math.random() * 0.7;
  }

  const lightGeo = new THREE.BufferGeometry();
  lightGeo.setAttribute('position', new THREE.BufferAttribute(lightPositions, 3));
  lightGeo.setAttribute('color', new THREE.BufferAttribute(lightColors, 3));
  const lightMat = new THREE.PointsMaterial({
    size: 0.04 * s,
    vertexColors: true,
    transparent: true,
    opacity: 0.8,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    sizeAttenuation: true,
  });
  const cityLights = new THREE.Points(lightGeo, lightMat);
  cityLights.name = 'trantor-city-lights';
  group.add(cityLights);

  // ── Atmosphere — larger transparent sphere with Fresnel rim ──
  const atmoGeo = new THREE.SphereGeometry(1.15 * s, 32, 32);
  const atmoMat = new THREE.ShaderMaterial({
    uniforms: {
      uTime: { value: 0 },
      uColor: { value: new THREE.Color('#4488CC') },
    },
    vertexShader: globeVertexShader,
    fragmentShader: atmosphereFragmentShader,
    transparent: true,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    side: THREE.BackSide,
  });
  const atmosphere = new THREE.Mesh(atmoGeo, atmoMat);
  atmosphere.name = 'trantor-atmosphere';
  group.add(atmosphere);

  // ── Orbital ring — thin torus (space station platform) ──
  const ringGeo = new THREE.TorusGeometry(1.5 * s, 0.015 * s, 8, 64);
  const ringMat = new THREE.MeshBasicMaterial({
    color: new THREE.Color('#FFD700'),
    transparent: true,
    opacity: 0.5,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
  });
  const orbitalRing = new THREE.Mesh(ringGeo, ringMat);
  orbitalRing.rotation.x = Math.PI * 0.35;
  orbitalRing.name = 'trantor-orbital-ring';
  group.add(orbitalRing);

  // Store references for animation
  group.userData.parts = {
    planet,
    cityLights,
    atmosphere,
    orbitalRing,
    lightBaseOpacities,
    scale: s,
  };

  return group;
}

// ---------------------------------------------------------------------------
// Animate the Trantor globe each frame
// ---------------------------------------------------------------------------
export function updateTrantorGlobe(group: THREE.Group, time: number): void {
  const parts = group.userData.parts as {
    planet: THREE.Mesh;
    cityLights: THREE.Points;
    atmosphere: THREE.Mesh;
    orbitalRing: THREE.Mesh;
    lightBaseOpacities: Float32Array;
    scale: number;
  } | undefined;
  if (!parts) return;

  const { planet, cityLights, atmosphere, orbitalRing } = parts;

  // ── Planet slow rotation ──
  planet.rotation.y = time * 0.15;
  cityLights.rotation.y = time * 0.15;

  // ── Orbital ring rotates at different speed ──
  orbitalRing.rotation.z = time * 0.3;
  orbitalRing.rotation.x = Math.PI * 0.35 + Math.sin(time * 0.1) * 0.05;

  // ── City lights twinkle — random opacity modulation ──
  const mat = cityLights.material as THREE.PointsMaterial;
  const twinkle = 0.6 + Math.sin(time * 2.0) * 0.2 + Math.sin(time * 5.7) * 0.1;
  mat.opacity = Math.min(1.0, twinkle);

  // ── Shader time updates ──
  const planetShader = planet.material as THREE.ShaderMaterial;
  if (planetShader.uniforms?.uTime) planetShader.uniforms.uTime.value = time;

  const atmoShader = atmosphere.material as THREE.ShaderMaterial;
  if (atmoShader.uniforms?.uTime) atmoShader.uniforms.uTime.value = time;

  // ── Holographic glitch ──
  const glitchSeed = Math.sin(Math.floor(time * 6) * 54321.9876);
  if (glitchSeed > 0.97) {
    group.position.x += (Math.random() - 0.5) * 0.02 * parts.scale;
    group.position.y += (Math.random() - 0.5) * 0.01 * parts.scale;
  }
}
