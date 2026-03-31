// src/components/PrimeRadiant/CrystalEiffelTower.ts
// Procedural crystal Eiffel Tower made of paperclip-shaped tubes with
// electric sparks, lightning arcs, and floating crystal dust particles.

import * as THREE from 'three';

// ── Constants ──
const TOWER_HEIGHT = 40;
const BASE_WIDTH = 20;
const HALF_BASE = BASE_WIDTH / 2;
const TUBE_RADIUS = 0.12;
const TUBE_SEGMENTS = 24;
const TUBE_RADIAL = 6;
const SPARK_COUNT = 18;
const ARC_COUNT = 10;
const PARTICLE_COUNT = 500;
const ARC_REGEN_INTERVAL = 0.1; // seconds

// ── Platform heights (fractions of tower height) ──
const PLATFORM_FRACTIONS = [0.25, 0.5, 0.75];

// ── Crystal material config ──
function createCrystalMaterial(): THREE.MeshPhysicalMaterial {
  return new THREE.MeshPhysicalMaterial({
    color: 0x88ccff,
    metalness: 0.1,
    roughness: 0.05,
    transmission: 0.9,
    thickness: 0.5,
    ior: 2.0,
    envMapIntensity: 1.5,
    clearcoat: 1.0,
    clearcoatRoughness: 0.1,
    transparent: true,
    side: THREE.DoubleSide,
  });
}

// ── Paperclip curve: an elongated double-loop shape ──
function createPaperclipCurve(
  start: THREE.Vector3,
  end: THREE.Vector3,
  amplitude: number = 0.6,
): THREE.CatmullRomCurve3 {
  const mid = new THREE.Vector3().lerpVectors(start, end, 0.5);
  const dir = new THREE.Vector3().subVectors(end, start);
  const len = dir.length();
  dir.normalize();

  // perpendicular vector for the paperclip bends
  const up = new THREE.Vector3(0, 1, 0);
  const perp = new THREE.Vector3().crossVectors(dir, up);
  if (perp.lengthSq() < 0.001) perp.set(1, 0, 0);
  perp.normalize();

  const a = amplitude * Math.min(len * 0.15, 1.2);

  const points: THREE.Vector3[] = [
    start.clone(),
    new THREE.Vector3().lerpVectors(start, end, 0.15).add(perp.clone().multiplyScalar(a)),
    new THREE.Vector3().lerpVectors(start, end, 0.25).add(perp.clone().multiplyScalar(-a)),
    new THREE.Vector3().lerpVectors(start, end, 0.35).add(perp.clone().multiplyScalar(a * 0.5)),
    mid.clone(),
    new THREE.Vector3().lerpVectors(start, end, 0.65).add(perp.clone().multiplyScalar(-a * 0.5)),
    new THREE.Vector3().lerpVectors(start, end, 0.75).add(perp.clone().multiplyScalar(a)),
    new THREE.Vector3().lerpVectors(start, end, 0.85).add(perp.clone().multiplyScalar(-a)),
    end.clone(),
  ];

  return new THREE.CatmullRomCurve3(points, false, 'catmullrom', 0.5);
}

// ── Build one paperclip tube along a curve ──
function createPaperclipTube(
  curve: THREE.CatmullRomCurve3,
  material: THREE.Material,
): THREE.Mesh {
  const geo = new THREE.TubeGeometry(curve, TUBE_SEGMENTS, TUBE_RADIUS, TUBE_RADIAL, false);
  return new THREE.Mesh(geo, material);
}

// ── Leg profile: curved path from base corner to spire top ──
function createLegCurve(
  baseX: number,
  baseZ: number,
  height: number,
): THREE.CatmullRomCurve3 {
  const points: THREE.Vector3[] = [];
  const steps = 12;
  for (let i = 0; i <= steps; i++) {
    const t = i / steps;
    // parabolic taper: base spreads wide, narrows toward top
    const taper = 1 - t * t;
    const x = baseX * taper * 0.3;
    const z = baseZ * taper * 0.3;
    const y = t * height;
    points.push(new THREE.Vector3(x, y, z));
  }
  return new THREE.CatmullRomCurve3(points, false, 'catmullrom', 0.5);
}

// ── Build leg from stacked paperclips ──
function buildLeg(
  baseX: number,
  baseZ: number,
  height: number,
  material: THREE.Material,
  group: THREE.Group,
): void {
  const mainCurve = createLegCurve(baseX, baseZ, height);
  const segmentCount = 8;

  for (let i = 0; i < segmentCount; i++) {
    const t0 = i / segmentCount;
    const t1 = (i + 1) / segmentCount;
    const p0 = mainCurve.getPointAt(t0);
    const p1 = mainCurve.getPointAt(t1);
    const clip = createPaperclipCurve(p0, p1);
    group.add(createPaperclipTube(clip, material));
  }
}

// ── Horizontal platform ring at a given height fraction ──
function buildPlatform(
  fraction: number,
  height: number,
  material: THREE.Material,
  group: THREE.Group,
): void {
  const y = fraction * height;
  const taper = 1 - fraction * fraction;
  const radius = HALF_BASE * taper * 0.35;
  const sides = 8;

  for (let i = 0; i < sides; i++) {
    const a0 = (i / sides) * Math.PI * 2;
    const a1 = ((i + 1) / sides) * Math.PI * 2;
    const p0 = new THREE.Vector3(Math.cos(a0) * radius, y, Math.sin(a0) * radius);
    const p1 = new THREE.Vector3(Math.cos(a1) * radius, y, Math.sin(a1) * radius);
    const clip = createPaperclipCurve(p0, p1, 0.4);
    group.add(createPaperclipTube(clip, material));
  }
}

// ── Build the spire at the top ──
function buildSpire(
  height: number,
  material: THREE.Material,
  group: THREE.Group,
): void {
  const spireBase = height * 0.85;
  const spireTop = height;
  const spireSegments = 4;

  for (let i = 0; i < spireSegments; i++) {
    const angle = (i / spireSegments) * Math.PI * 2;
    const r = 0.5;
    const p0 = new THREE.Vector3(Math.cos(angle) * r, spireBase, Math.sin(angle) * r);
    const p1 = new THREE.Vector3(0, spireTop, 0);
    const clip = createPaperclipCurve(p0, p1, 0.3);
    group.add(createPaperclipTube(clip, material));
  }
}

// ── Cross braces between legs ──
function buildCrossBraces(
  height: number,
  material: THREE.Material,
  group: THREE.Group,
): void {
  const legCorners = [
    [HALF_BASE, HALF_BASE],
    [HALF_BASE, -HALF_BASE],
    [-HALF_BASE, -HALF_BASE],
    [-HALF_BASE, HALF_BASE],
  ];

  const braceHeights = [0.1, 0.2, 0.35, 0.6];
  for (const bh of braceHeights) {
    const y = bh * height;
    const taper = 1 - bh * bh;
    for (let i = 0; i < 4; i++) {
      const [x0, z0] = legCorners[i];
      const [x1, z1] = legCorners[(i + 1) % 4];
      const p0 = new THREE.Vector3(x0 * taper * 0.3, y, z0 * taper * 0.3);
      const p1 = new THREE.Vector3(x1 * taper * 0.3, y, z1 * taper * 0.3);
      const clip = createPaperclipCurve(p0, p1, 0.3);
      group.add(createPaperclipTube(clip, material));
    }
  }
}

// ── Spark points (emissive meshes — no PointLights to save GPU) ──
interface SparkPoint {
  mesh: THREE.Mesh;
  material: THREE.MeshBasicMaterial;
  baseIntensity: number;
  phaseOffset: number;
  position: THREE.Vector3;
}

const sparkGeo = new THREE.SphereGeometry(0.15, 4, 4); // shared geometry

function createSparks(group: THREE.Group): SparkPoint[] {
  const sparks: SparkPoint[] = [];

  for (let i = 0; i < SPARK_COUNT; i++) {
    const fraction = Math.random();
    const taper = 1 - fraction * fraction;
    const angle = Math.random() * Math.PI * 2;
    const radius = HALF_BASE * taper * 0.3 * Math.random();
    const x = Math.cos(angle) * radius;
    const z = Math.sin(angle) * radius;
    const y = fraction * TOWER_HEIGHT;

    const mat = new THREE.MeshBasicMaterial({
      color: 0xaaddff,
      transparent: true,
      opacity: 0.8,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    });
    const mesh = new THREE.Mesh(sparkGeo, mat);
    mesh.position.set(x, y, z);
    group.add(mesh);

    sparks.push({
      mesh,
      material: mat,
      baseIntensity: 1.5 + Math.random() * 2,
      phaseOffset: Math.random() * Math.PI * 2,
      position: new THREE.Vector3(x, y, z),
    });
  }

  return sparks;
}

// ── Lightning arcs between nearby spark points ──
interface ArcLine {
  line: THREE.Line;
  sparkA: THREE.Vector3;
  sparkB: THREE.Vector3;
}

function createJaggedArcGeometry(a: THREE.Vector3, b: THREE.Vector3): THREE.BufferGeometry {
  const points: THREE.Vector3[] = [a.clone()];
  const segments = 6 + Math.floor(Math.random() * 4);
  const dir = new THREE.Vector3().subVectors(b, a);

  for (let i = 1; i < segments; i++) {
    const t = i / segments;
    const base = new THREE.Vector3().lerpVectors(a, b, t);
    const jitter = dir.length() * 0.15;
    base.x += (Math.random() - 0.5) * jitter;
    base.y += (Math.random() - 0.5) * jitter;
    base.z += (Math.random() - 0.5) * jitter;
    points.push(base);
  }
  points.push(b.clone());

  return new THREE.BufferGeometry().setFromPoints(points);
}

const arcMaterial = new THREE.LineBasicMaterial({
  color: 0xccddff,
  transparent: true,
  opacity: 0.8,
  blending: THREE.AdditiveBlending,
});

function createArcs(sparks: SparkPoint[], group: THREE.Group): ArcLine[] {
  const arcs: ArcLine[] = [];

  for (let i = 0; i < ARC_COUNT && i < sparks.length - 1; i++) {
    const a = sparks[i].position;
    // find nearest other spark
    let nearest = sparks[(i + 1) % sparks.length].position;
    let bestDist = a.distanceTo(nearest);
    for (let j = 0; j < sparks.length; j++) {
      if (j === i) continue;
      const d = a.distanceTo(sparks[j].position);
      if (d < bestDist && d > 0.5) {
        bestDist = d;
        nearest = sparks[j].position;
      }
    }

    const geo = createJaggedArcGeometry(a, nearest);
    const line = new THREE.Line(geo, arcMaterial);
    group.add(line);
    arcs.push({ line, sparkA: a, sparkB: nearest });
  }

  return arcs;
}

function regenerateArcs(arcs: ArcLine[]): void {
  for (const arc of arcs) {
    const oldGeo = arc.line.geometry;
    arc.line.geometry = createJaggedArcGeometry(arc.sparkA, arc.sparkB);
    oldGeo.dispose();
  }
}

// ── Floating crystal dust particles ──
interface ParticleSystem {
  points: THREE.Points;
  velocities: Float32Array;
}

function createParticles(group: THREE.Group): ParticleSystem {
  const positions = new Float32Array(PARTICLE_COUNT * 3);
  const colors = new Float32Array(PARTICLE_COUNT * 3);
  const sizes = new Float32Array(PARTICLE_COUNT);
  const velocities = new Float32Array(PARTICLE_COUNT);

  const blue = new THREE.Color(0x88ccff);
  const gold = new THREE.Color(0xffd700);

  for (let i = 0; i < PARTICLE_COUNT; i++) {
    const angle = Math.random() * Math.PI * 2;
    const radius = 2 + Math.random() * 12;
    positions[i * 3] = Math.cos(angle) * radius;
    positions[i * 3 + 1] = Math.random() * TOWER_HEIGHT;
    positions[i * 3 + 2] = Math.sin(angle) * radius;

    const mix = Math.random();
    const c = new THREE.Color().lerpColors(blue, gold, mix);
    colors[i * 3] = c.r;
    colors[i * 3 + 1] = c.g;
    colors[i * 3 + 2] = c.b;

    sizes[i] = 0.1 + Math.random() * 0.2;
    velocities[i] = 0.3 + Math.random() * 0.7;
  }

  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
  geo.setAttribute('color', new THREE.BufferAttribute(colors, 3));
  geo.setAttribute('size', new THREE.BufferAttribute(sizes, 1));

  const mat = new THREE.PointsMaterial({
    size: 0.2,
    vertexColors: true,
    transparent: true,
    opacity: 0.6,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    sizeAttenuation: true,
  });

  const points = new THREE.Points(geo, mat);
  group.add(points);

  return { points, velocities };
}

function updateParticles(particles: ParticleSystem, dt: number): void {
  const posAttr = particles.points.geometry.getAttribute('position') as THREE.BufferAttribute;
  const positions = posAttr.array as Float32Array;

  for (let i = 0; i < PARTICLE_COUNT; i++) {
    positions[i * 3 + 1] += particles.velocities[i] * dt;

    // fade logic: respawn at bottom when above tower
    if (positions[i * 3 + 1] > TOWER_HEIGHT * 1.1) {
      const angle = Math.random() * Math.PI * 2;
      const radius = 2 + Math.random() * 12;
      positions[i * 3] = Math.cos(angle) * radius;
      positions[i * 3 + 1] = -1;
      positions[i * 3 + 2] = Math.sin(angle) * radius;
    }
  }

  posAttr.needsUpdate = true;
}

// ── Lighting setup ──
interface TowerLights {
  centerLight: THREE.PointLight;
  spotLeft: THREE.SpotLight;
  spotRight: THREE.SpotLight;
  ambient: THREE.AmbientLight;
}

function createLighting(group: THREE.Group): TowerLights {
  // Warm gold center glow
  const centerLight = new THREE.PointLight(0xffd700, 3, 50, 1.5);
  centerLight.position.set(0, TOWER_HEIGHT * 0.4, 0);
  group.add(centerLight);

  // Blue uplighting spots
  const spotLeft = new THREE.SpotLight(0x4488ff, 5, 60, Math.PI / 4, 0.5, 1);
  spotLeft.position.set(-8, -2, -8);
  spotLeft.target.position.set(0, TOWER_HEIGHT * 0.5, 0);
  group.add(spotLeft);
  group.add(spotLeft.target);

  const spotRight = new THREE.SpotLight(0x4488ff, 5, 60, Math.PI / 4, 0.5, 1);
  spotRight.position.set(8, -2, 8);
  spotRight.target.position.set(0, TOWER_HEIGHT * 0.5, 0);
  group.add(spotRight);
  group.add(spotRight.target);

  // Very dim blue ambient
  const ambient = new THREE.AmbientLight(0x112244, 0.3);
  group.add(ambient);

  return { centerLight, spotLeft, spotRight, ambient };
}

// ── Main factory ──

export interface CrystalEiffelTowerHandle {
  group: THREE.Group;
  update: (time: number) => void;
  dispose: () => void;
}

export function createCrystalEiffelTower(scene: THREE.Scene): CrystalEiffelTowerHandle {
  const group = new THREE.Group();
  group.name = 'crystal-eiffel-tower';

  const material = createCrystalMaterial();

  // ── Build tower structure ──

  // Four legs
  const legCorners: [number, number][] = [
    [HALF_BASE, HALF_BASE],
    [HALF_BASE, -HALF_BASE],
    [-HALF_BASE, -HALF_BASE],
    [-HALF_BASE, HALF_BASE],
  ];
  for (const [bx, bz] of legCorners) {
    buildLeg(bx, bz, TOWER_HEIGHT, material, group);
  }

  // Three platform levels
  for (const frac of PLATFORM_FRACTIONS) {
    buildPlatform(frac, TOWER_HEIGHT, material, group);
  }

  // Cross braces
  buildCrossBraces(TOWER_HEIGHT, material, group);

  // Spire
  buildSpire(TOWER_HEIGHT, material, group);

  // ── Effects ──
  const sparks = createSparks(group);
  const arcs = createArcs(sparks, group);
  const particles = createParticles(group);
  const lights = createLighting(group);

  scene.add(group);

  // ── Animation state ──
  let lastArcRegen = 0;
  let prevTime = 0;

  function update(time: number): void {
    const dt = time - prevTime;
    prevTime = time;

    // Pulse spark lights
    for (const spark of sparks) {
      const pulse = Math.sin(time * 8 + spark.phaseOffset) * 0.5 + 0.5;
      spark.material.opacity = 0.3 + pulse * 0.7;
      const scale = 0.5 + pulse * 1.0;
      spark.mesh.scale.setScalar(scale);
      // Flicker color between blue and white
      const mix = pulse * 0.3;
      spark.material.color.setRGB(0.7 + mix, 0.85 + mix * 0.15, 1.0);
    }

    // Regenerate arcs periodically
    if (time - lastArcRegen > ARC_REGEN_INTERVAL) {
      regenerateArcs(arcs);
      lastArcRegen = time;
    }

    // Arc opacity flicker
    (arcMaterial as THREE.LineBasicMaterial).opacity = 0.5 + Math.sin(time * 12) * 0.3;

    // Particles drift upward
    updateParticles(particles, Math.max(dt, 0));

    // Center light gentle pulse
    lights.centerLight.intensity = 2.5 + Math.sin(time * 1.5) * 0.5;

    // Spot lights slow color shift
    const hue = (time * 0.02) % 1;
    lights.spotLeft.color.setHSL(0.6 + hue * 0.05, 0.8, 0.5);
    lights.spotRight.color.setHSL(0.6 - hue * 0.05, 0.8, 0.5);
  }

  function dispose(): void {
    // Dispose all geometries and materials in the group
    group.traverse((obj) => {
      if (obj instanceof THREE.Mesh) {
        obj.geometry.dispose();
        if (Array.isArray(obj.material)) {
          obj.material.forEach((m) => m.dispose());
        } else {
          obj.material.dispose();
        }
      }
      if (obj instanceof THREE.Line) {
        obj.geometry.dispose();
      }
      if (obj instanceof THREE.Points) {
        obj.geometry.dispose();
        if (obj.material instanceof THREE.Material) {
          obj.material.dispose();
        }
      }
    });

    scene.remove(group);
    arcMaterial.dispose();
    material.dispose();
  }

  return { group, update, dispose };
}
