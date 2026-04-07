// src/components/PrimeRadiant/BorgCubeNode.ts
// Borg-cube-style mesh for constitution governance nodes.
// Inspired by BlenderKit "SciFi Cube Spaceship" by Dennis Hafemann.
//
// Supports two modes:
//   1. GLB model (preferred): loads /models/borg-cube.glb via GLTFLoader
//   2. Procedural fallback: generates geometry if GLB not found
//
// Integration in ForceRadiant.tsx:
//   import { createBorgCubeNode, preloadBorgCubeModel } from './BorgCubeNode';
//   // Call once at init: await preloadBorgCubeModel();
//   // In createNodeObject for constitution nodes:
//   if (node.type === 'constitution') return createBorgCubeNode(radius * 1.2, nodeColor);

import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';

// ---------------------------------------------------------------------------
// GLB Model Loader — loads actual Blender model when available
// ---------------------------------------------------------------------------

const GLB_PATH = '/models/borg-cube.glb';
let _cachedGLB: THREE.Group | null = null;
let _glbLoadAttempted = false;

/**
 * Pre-load the Borg cube GLB model. Call once at scene init.
 * If the file doesn't exist, silently falls back to procedural geometry.
 */
export async function preloadBorgCubeModel(): Promise<boolean> {
  if (_glbLoadAttempted) return _cachedGLB !== null;
  _glbLoadAttempted = true;

  try {
    // Quick HEAD check first to avoid loader errors when file is missing
    const check = await fetch(GLB_PATH, { method: 'HEAD' });
    if (!check.ok) {
      console.info('[BorgCube] No GLB at', GLB_PATH, '— using procedural fallback');
      return false;
    }

    const loader = new GLTFLoader();
    const gltf = await new Promise<THREE.Group>((resolve, reject) => {
      loader.load(GLB_PATH, (result) => resolve(result.scene), undefined, reject);
    });

    _cachedGLB = gltf;
    console.info('[BorgCube] GLB model loaded:', GLB_PATH);
    return true;
  } catch (err) {
    console.info('[BorgCube] GLB load failed, using procedural fallback:', err);
    return false;
  }
}

/**
 * Create a Borg cube node from the loaded GLB model.
 * Returns null if GLB not loaded — caller should fall back to procedural.
 */
function createFromGLB(size: number, healthColor?: THREE.Color): THREE.Group | null {
  if (!_cachedGLB) return null;

  const clone = _cachedGLB.clone(true);

  // Normalize the model to fit our size parameter
  const box = new THREE.Box3().setFromObject(clone);
  const modelSize = box.getSize(new THREE.Vector3());
  const maxDim = Math.max(modelSize.x, modelSize.y, modelSize.z);
  const scale = size / maxDim;
  clone.scale.setScalar(scale);

  // Center the model
  const center = box.getCenter(new THREE.Vector3()).multiplyScalar(scale);
  clone.position.sub(center);

  // Apply health-based emissive color to all materials
  if (healthColor) {
    clone.traverse((child) => {
      if ((child as THREE.Mesh).isMesh) {
        const mat = (child as THREE.Mesh).material;
        if (mat && 'emissive' in mat) {
          (mat as THREE.MeshStandardMaterial).emissive.copy(healthColor);
          (mat as THREE.MeshStandardMaterial).emissiveIntensity = 0.15;
        }
      }
    });
  }

  clone.name = 'borg-cube';
  return clone;
}

// ---------------------------------------------------------------------------
// Procedural Fallback
// ---------------------------------------------------------------------------

// Deterministic hash for procedural panel placement
function hash(x: number, y: number, z: number): number {
  let h = (x * 73856093) ^ (y * 19349663) ^ (z * 83492791);
  h = ((h >> 16) ^ h) * 0x45d9f3b;
  h = ((h >> 16) ^ h) * 0x45d9f3b;
  return ((h >> 16) ^ h) & 0x7fffffff;
}
function hashF(x: number, y: number, z: number): number {
  return (hash(x, y, z) % 10000) / 10000;
}

// ---------------------------------------------------------------------------
// Geometry — irregular multi-depth extruded panels on each face
// ---------------------------------------------------------------------------

export function createBorgCubeGeometry(size: number): THREE.BufferGeometry {
  // High subdivision for detail — 8x8 panels per face
  const subdivisions = 8;
  const geo = new THREE.BoxGeometry(size, size, size, subdivisions, subdivisions, subdivisions);

  const pos = geo.attributes.position;
  const halfSize = size / 2;
  const cellSize = size / subdivisions;
  const maxExtrude = size * 0.08; // max panel extrusion depth

  for (let i = 0; i < pos.count; i++) {
    const x = pos.getX(i);
    const y = pos.getY(i);
    const z = pos.getZ(i);

    const ax = Math.abs(x), ay = Math.abs(y), az = Math.abs(z);
    const maxAx = Math.max(ax, ay, az);

    // Only modify face vertices
    if (Math.abs(maxAx - halfSize) > 0.01) continue;

    // Determine which face and the 2D grid cell on that face
    let faceId: number, cu: number, cv: number;
    if (ax === maxAx) {
      faceId = x > 0 ? 0 : 1;
      cu = Math.floor((y + halfSize) / cellSize);
      cv = Math.floor((z + halfSize) / cellSize);
    } else if (ay === maxAx) {
      faceId = y > 0 ? 2 : 3;
      cu = Math.floor((x + halfSize) / cellSize);
      cv = Math.floor((z + halfSize) / cellSize);
    } else {
      faceId = z > 0 ? 4 : 5;
      cu = Math.floor((x + halfSize) / cellSize);
      cv = Math.floor((y + halfSize) / cellSize);
    }

    // Deterministic random extrusion per panel cell
    const h = hashF(faceId * 100 + cu, cv, 42);

    // 30% of panels extrude outward, 40% are recessed, 30% are flat
    let depth: number;
    if (h < 0.3) {
      depth = maxExtrude * (0.3 + h * 2); // extrude outward
    } else if (h < 0.7) {
      depth = -maxExtrude * (0.2 + (h - 0.3) * 1.5); // recess inward
    } else {
      depth = 0; // flush panels
    }

    // Seam groove — vertices at cell boundaries get extra recess
    const uPos = (y + halfSize) / cellSize;
    const vPos = (z + halfSize) / cellSize;
    if (ax === maxAx) { /* u=y, v=z already set */ }
    else if (ay === maxAx) {
      // Recompute for this face
    }
    const uFrac = Math.abs((uPos % 1) - 0.5) * 2; // 0 at seam, 1 at center
    const vFrac = Math.abs((vPos % 1) - 0.5) * 2;
    const seamFactor = Math.min(uFrac, vFrac);
    const seamDepth = size * 0.015 * (1 - Math.min(1, seamFactor * 4));

    // Apply along face normal
    const nx = ax === maxAx ? Math.sign(x) : 0;
    const ny = ay === maxAx ? Math.sign(y) : 0;
    const nz = az === maxAx ? Math.sign(z) : 0;

    const totalDisp = depth - seamDepth;
    pos.setXYZ(i, x + nx * totalDisp, y + ny * totalDisp, z + nz * totalDisp);
  }

  geo.computeVertexNormals();
  return geo;
}

// ---------------------------------------------------------------------------
// Material — dark metallic hull
// ---------------------------------------------------------------------------

export function createBorgCubeMaterial(
  healthColor?: THREE.Color,
): THREE.MeshPhysicalMaterial {
  return new THREE.MeshPhysicalMaterial({
    color: 0x0d0d1a,            // very dark blue-black
    metalness: 0.95,
    roughness: 0.35,
    emissive: healthColor ?? new THREE.Color(0x00ff88),
    emissiveIntensity: 0.08,
    clearcoat: 0.3,
    clearcoatRoughness: 0.4,
  });
}

// ---------------------------------------------------------------------------
// Orange conduit wireframe — like circuit traces between panels
// ---------------------------------------------------------------------------

function createConduitLines(size: number): THREE.LineSegments {
  const segments: number[] = [];
  const halfSize = size / 2;
  const gridStep = size / 8;

  // Horizontal and vertical conduit lines on each face
  for (let face = 0; face < 6; face++) {
    for (let i = 0; i <= 8; i++) {
      const t = -halfSize + i * gridStep;
      // Only draw ~40% of possible lines (random selection for irregularity)
      if (hashF(face, i, 0) > 0.4) continue;

      const offset = halfSize * 1.005; // slightly off face
      switch (face) {
        case 0: // +X face — horizontal lines
          segments.push(offset, t, -halfSize, offset, t, halfSize);
          break;
        case 1: // -X face
          segments.push(-offset, t, -halfSize, -offset, t, halfSize);
          break;
        case 2: // +Y face
          segments.push(-halfSize, offset, t, halfSize, offset, t);
          break;
        case 3: // -Y face
          segments.push(-halfSize, -offset, t, halfSize, -offset, t);
          break;
        case 4: // +Z face
          segments.push(-halfSize, t, offset, halfSize, t, offset);
          break;
        case 5: // -Z face
          segments.push(-halfSize, t, -offset, halfSize, t, -offset);
          break;
      }
    }
  }

  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.Float32BufferAttribute(segments, 3));
  const mat = new THREE.LineBasicMaterial({
    color: 0xff8833,  // orange conduit color
    transparent: true,
    opacity: 0.35,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
  });
  return new THREE.LineSegments(geo, mat);
}

// ---------------------------------------------------------------------------
// Green sub-system emissive dots — scattered across panels
// ---------------------------------------------------------------------------

function createSubSystemDots(size: number): THREE.Points {
  const dotsPerFace = 8; // more dots for denser look
  const faceCount = 6;
  const dotCount = faceCount * dotsPerFace;
  const positions = new Float32Array(dotCount * 3);
  const colors = new Float32Array(dotCount * 3);
  const halfSize = size * 0.5;

  const green = new THREE.Color(0x00ff88);
  const cyan = new THREE.Color(0x00ccff);
  const amber = new THREE.Color(0xffaa22);

  let idx = 0;
  for (let face = 0; face < faceCount; face++) {
    for (let d = 0; d < dotsPerFace; d++) {
      // Use deterministic placement so dots don't move on re-render
      const u = (hashF(face, d, 1) - 0.5) * size * 0.85;
      const v = (hashF(face, d, 2) - 0.5) * size * 0.85;
      const i3 = idx * 3;

      const offset = halfSize * 1.03;
      switch (face) {
        case 0: positions[i3] = offset;  positions[i3+1] = u; positions[i3+2] = v; break;
        case 1: positions[i3] = -offset; positions[i3+1] = u; positions[i3+2] = v; break;
        case 2: positions[i3] = u; positions[i3+1] = offset;  positions[i3+2] = v; break;
        case 3: positions[i3] = u; positions[i3+1] = -offset; positions[i3+2] = v; break;
        case 4: positions[i3] = u; positions[i3+1] = v; positions[i3+2] = offset;  break;
        case 5: positions[i3] = u; positions[i3+1] = v; positions[i3+2] = -offset; break;
      }

      // 60% green, 25% cyan, 15% amber (like the reference)
      const r = hashF(face, d, 3);
      const c = r < 0.6 ? green : r < 0.85 ? cyan : amber;
      colors[i3] = c.r; colors[i3+1] = c.g; colors[i3+2] = c.b;
      idx++;
    }
  }

  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
  geo.setAttribute('color', new THREE.BufferAttribute(colors, 3));

  return new THREE.Points(geo, new THREE.PointsMaterial({
    size: size * 0.06,
    vertexColors: true,
    transparent: true,
    opacity: 0.9,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    sizeAttenuation: true,
  }));
}

// ---------------------------------------------------------------------------
// Green wireframe grid overlay — panel edges glow
// ---------------------------------------------------------------------------

function createWireframeOverlay(size: number, color: THREE.Color): THREE.Mesh {
  const wireGeo = new THREE.BoxGeometry(size * 1.002, size * 1.002, size * 1.002, 8, 8, 8);
  return new THREE.Mesh(wireGeo, new THREE.MeshBasicMaterial({
    color,
    wireframe: true,
    transparent: true,
    opacity: 0.15,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
  }));
}

// ---------------------------------------------------------------------------
// Composite Borg cube node
// ---------------------------------------------------------------------------

export function createBorgCubeNode(
  size: number,
  healthColor?: THREE.Color,
): THREE.Group {
  // Try GLB model first (higher quality, artist-made)
  const glbNode = createFromGLB(size, healthColor);
  if (glbNode) return glbNode;

  // Procedural fallback
  const group = new THREE.Group();
  const wireColor = healthColor ?? new THREE.Color(0x00ff88);

  // 1. Hull — irregular extruded panels
  const hull = new THREE.Mesh(createBorgCubeGeometry(size), createBorgCubeMaterial(healthColor));
  group.add(hull);

  // 2. Wireframe — subtle green grid
  group.add(createWireframeOverlay(size, wireColor));

  // 3. Orange conduit lines — circuit traces between panels
  group.add(createConduitLines(size));

  // 4. Sub-system emissive dots — green/cyan/amber
  group.add(createSubSystemDots(size));

  group.name = 'borg-cube';
  return group;
}
