// src/components/PrimeRadiant/EarthGlobe.ts
// Realistic Earth globe using procedural shaders (no external textures needed)
// Blue marble with continents, atmosphere, city lights, clouds

import * as THREE from 'three';

// ---------------------------------------------------------------------------
// Procedural Earth shader — no textures, pure math
// Continents via noise, ocean specular, atmosphere Fresnel, city lights at night
// ---------------------------------------------------------------------------
const earthVertexShader = /* glsl */ `
  varying vec3 vNormal;
  varying vec3 vViewDir;
  varying vec3 vWorldPos;
  varying vec2 vUv;
  varying vec3 vLocalPos;

  void main() {
    vUv = uv;
    vNormal = normalize(normalMatrix * normal);
    vLocalPos = position;
    vec4 worldPos = modelMatrix * vec4(position, 1.0);
    vWorldPos = worldPos.xyz;
    vViewDir = normalize(cameraPosition - worldPos.xyz);
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  }
`;

const earthFragmentShader = /* glsl */ `
  uniform float uTime;
  uniform vec3 uSunDir;

  varying vec3 vNormal;
  varying vec3 vViewDir;
  varying vec3 vWorldPos;
  varying vec2 vUv;
  varying vec3 vLocalPos;

  // Simple 3D noise
  vec3 hash33(vec3 p) {
    p = fract(p * vec3(443.897, 441.423, 437.195));
    p += dot(p, p.yzx + 19.19);
    return fract((p.xxy + p.yxx) * p.zyx);
  }

  float noise3d(vec3 p) {
    vec3 i = floor(p);
    vec3 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    float n = mix(
      mix(mix(dot(hash33(i), f), dot(hash33(i + vec3(1,0,0)), f - vec3(1,0,0)), f.x),
          mix(dot(hash33(i + vec3(0,1,0)), f - vec3(0,1,0)), dot(hash33(i + vec3(1,1,0)), f - vec3(1,1,0)), f.x), f.y),
      mix(mix(dot(hash33(i + vec3(0,0,1)), f - vec3(0,0,1)), dot(hash33(i + vec3(1,0,1)), f - vec3(1,0,1)), f.x),
          mix(dot(hash33(i + vec3(0,1,1)), f - vec3(0,1,1)), dot(hash33(i + vec3(1,1,1)), f - vec3(1,1,1)), f.x), f.y), f.z);
    return n * 0.5 + 0.5;
  }

  float fbm(vec3 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++) {
      v += a * noise3d(p);
      p *= 2.1;
      a *= 0.5;
    }
    return v;
  }

  void main() {
    vec3 n = normalize(vNormal);
    float sunDot = dot(n, uSunDir);
    float daylight = smoothstep(-0.1, 0.3, sunDot);

    // Continent mask from noise (threshold creates landmasses)
    vec3 noisePos = vLocalPos * 2.5;
    float continentNoise = fbm(noisePos);
    float isLand = smoothstep(0.48, 0.52, continentNoise);

    // Ocean color — deep blue with specular
    vec3 oceanDeep = vec3(0.01, 0.04, 0.12);
    vec3 oceanShallow = vec3(0.02, 0.08, 0.18);
    float oceanSpec = pow(max(dot(reflect(-uSunDir, n), vViewDir), 0.0), 64.0);
    vec3 ocean = mix(oceanDeep, oceanShallow, 0.3) + vec3(0.4, 0.5, 0.6) * oceanSpec * daylight;

    // Land color — green/brown/white (polar)
    float lat = abs(vLocalPos.y / length(vLocalPos));
    vec3 tropical = vec3(0.05, 0.12, 0.03);   // dark green
    vec3 temperate = vec3(0.08, 0.1, 0.04);    // olive
    vec3 desert = vec3(0.15, 0.12, 0.07);      // sandy
    vec3 polar = vec3(0.7, 0.72, 0.75);        // ice/snow

    float landVariation = noise3d(noisePos * 4.0);
    vec3 land = mix(tropical, temperate, smoothstep(0.1, 0.4, lat));
    land = mix(land, desert, landVariation * 0.5);
    land = mix(land, polar, smoothstep(0.7, 0.85, lat));

    // Combine land/ocean
    vec3 surface = mix(ocean, land, isLand);

    // Lighting
    vec3 lit = surface * (0.08 + 0.92 * daylight);

    // City lights on dark side (land only)
    float cityNoise = noise3d(vLocalPos * 20.0);
    float cities = isLand * (1.0 - daylight) * smoothstep(0.55, 0.7, cityNoise) * 0.8;
    vec3 cityColor = vec3(1.0, 0.85, 0.4) * cities;
    lit += cityColor;

    // Clouds — separate noise layer, brighter
    float clouds = fbm(vLocalPos * 3.0 + vec3(uTime * 0.01, 0.0, uTime * 0.005));
    clouds = smoothstep(0.45, 0.65, clouds) * 0.4 * daylight;
    lit += vec3(0.8, 0.82, 0.85) * clouds;

    // Atmosphere — Fresnel rim
    float fresnel = 1.0 - dot(n, vViewDir);
    fresnel = pow(fresnel, 3.0);
    vec3 atmoColor = mix(vec3(0.3, 0.5, 1.0), vec3(0.1, 0.2, 0.5), fresnel);
    lit += atmoColor * fresnel * 0.5;

    gl_FragColor = vec4(lit, 1.0);
  }
`;

// ---------------------------------------------------------------------------
// Atmosphere glow (outer shell)
// ---------------------------------------------------------------------------
const atmoVertexShader = /* glsl */ `
  varying vec3 vNormal;
  varying vec3 vViewDir;
  void main() {
    vNormal = normalize(normalMatrix * normal);
    vec4 worldPos = modelMatrix * vec4(position, 1.0);
    vViewDir = normalize(cameraPosition - worldPos.xyz);
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  }
`;

const atmoFragmentShader = /* glsl */ `
  varying vec3 vNormal;
  varying vec3 vViewDir;
  void main() {
    float fresnel = 1.0 - dot(normalize(vNormal), vViewDir);
    fresnel = pow(fresnel, 2.5);
    vec3 col = vec3(0.3, 0.6, 1.0) * fresnel;
    gl_FragColor = vec4(col, fresnel * 0.6);
  }
`;

// ---------------------------------------------------------------------------
// Create Earth
// ---------------------------------------------------------------------------
export function createEarthGlobe(scale: number = 1): THREE.Group {
  const group = new THREE.Group();
  group.userData.isEarthGlobe = true;

  const sunDir = new THREE.Vector3(1, 0.3, 0.5).normalize();

  // Earth surface
  const earthGeo = new THREE.SphereGeometry(5 * scale, 64, 64);
  const earthMat = new THREE.ShaderMaterial({
    uniforms: {
      uTime: { value: 0 },
      uSunDir: { value: sunDir },
    },
    vertexShader: earthVertexShader,
    fragmentShader: earthFragmentShader,
    depthWrite: true,
  });
  const earth = new THREE.Mesh(earthGeo, earthMat);
  earth.name = 'earth-surface';
  group.add(earth);

  // Atmosphere glow
  const atmoGeo = new THREE.SphereGeometry(5.3 * scale, 32, 32);
  const atmoMat = new THREE.ShaderMaterial({
    uniforms: {},
    vertexShader: atmoVertexShader,
    fragmentShader: atmoFragmentShader,
    transparent: true,
    side: THREE.BackSide,
    depthWrite: false,
    blending: THREE.AdditiveBlending,
  });
  const atmosphere = new THREE.Mesh(atmoGeo, atmoMat);
  atmosphere.name = 'earth-atmosphere';
  group.add(atmosphere);

  group.userData.parts = { earth, atmosphere, scale };
  return group;
}

// ---------------------------------------------------------------------------
// Animate Earth
// ---------------------------------------------------------------------------
export function updateEarthGlobe(group: THREE.Group, time: number): void {
  const parts = group.userData.parts as { earth: THREE.Mesh; scale: number } | undefined;
  if (!parts) return;

  // Slow rotation
  group.rotation.y = time * 0.05;
  // Axial tilt
  group.rotation.z = 0.41; // 23.5 degrees

  // Update shader time (cloud movement)
  const mat = parts.earth.material as THREE.ShaderMaterial;
  if (mat.uniforms?.uTime) mat.uniforms.uTime.value = time;
}
