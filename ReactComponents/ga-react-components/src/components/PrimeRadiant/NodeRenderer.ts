// src/components/PrimeRadiant/NodeRenderer.ts
// Renders governance nodes as distinct 3D shapes based on type

import * as THREE from 'three';
import type { GovernanceNode, GovernanceNodeType } from './types';
import { NODE_SCALES } from './types';

// ---------------------------------------------------------------------------
// Geometry factories — one per node type
// ---------------------------------------------------------------------------
function createConstitutionGeometry(): THREE.BufferGeometry {
  return new THREE.IcosahedronGeometry(1, 1);
}

function createPolicyGeometry(): THREE.BufferGeometry {
  return new THREE.OctahedronGeometry(1, 0);
}

function createPersonaGeometry(): THREE.BufferGeometry {
  return new THREE.SphereGeometry(1, 24, 24);
}

function createPipelineGeometry(): THREE.BufferGeometry {
  return new THREE.TorusGeometry(1, 0.3, 12, 32);
}

function createDepartmentGeometry(): THREE.BufferGeometry {
  // Nebula cluster approximated by a dodecahedron
  return new THREE.DodecahedronGeometry(1, 1);
}

function createSchemaGeometry(): THREE.BufferGeometry {
  return new THREE.BoxGeometry(1, 1, 1);
}

function createTestGeometry(): THREE.BufferGeometry {
  return new THREE.TetrahedronGeometry(1, 0);
}

function createIxqlGeometry(): THREE.BufferGeometry {
  return new THREE.CylinderGeometry(0.6, 0.6, 1.4, 16);
}

const GEOMETRY_FACTORIES: Record<GovernanceNodeType, () => THREE.BufferGeometry> = {
  constitution: createConstitutionGeometry,
  policy: createPolicyGeometry,
  persona: createPersonaGeometry,
  pipeline: createPipelineGeometry,
  department: createDepartmentGeometry,
  schema: createSchemaGeometry,
  test: createTestGeometry,
  ixql: createIxqlGeometry,
};

// ---------------------------------------------------------------------------
// Cached geometries to avoid re-creation
// ---------------------------------------------------------------------------
const geometryCache = new Map<GovernanceNodeType, THREE.BufferGeometry>();

function getGeometry(type: GovernanceNodeType): THREE.BufferGeometry {
  let geo = geometryCache.get(type);
  if (!geo) {
    geo = GEOMETRY_FACTORIES[type]();
    geometryCache.set(type, geo);
  }
  return geo;
}

// ---------------------------------------------------------------------------
// Emissive intensity per node type
// ---------------------------------------------------------------------------
const EMISSIVE_INTENSITY: Record<GovernanceNodeType, number> = {
  constitution: 0.6,
  policy: 0.3,
  persona: 0.5,
  pipeline: 0.4,
  department: 0.2,
  schema: 0.2,
  test: 0.3,
  ixql: 0.3,
};

// ---------------------------------------------------------------------------
// Create a single node mesh
// ---------------------------------------------------------------------------
export function createNodeMesh(node: GovernanceNode): THREE.Mesh {
  const geometry = getGeometry(node.type);
  const color = new THREE.Color(node.color);
  const scale = NODE_SCALES[node.type];
  const emissive = EMISSIVE_INTENSITY[node.type];

  const material = new THREE.MeshStandardMaterial({
    color,
    emissive: color,
    emissiveIntensity: emissive,
    metalness: 0.3,
    roughness: 0.5,
    transparent: true,
    opacity: 0.9,
  });

  const mesh = new THREE.Mesh(geometry, material);
  mesh.scale.setScalar(scale);
  mesh.userData = { nodeId: node.id, nodeType: node.type };
  mesh.name = `node-${node.id}`;

  return mesh;
}

// ---------------------------------------------------------------------------
// Department particle cloud (extra decoration for department nodes)
// ---------------------------------------------------------------------------
export function createDepartmentParticles(node: GovernanceNode): THREE.Points {
  const count = 60;
  const positions = new Float32Array(count * 3);
  const colors = new Float32Array(count * 3);
  const baseColor = new THREE.Color(node.color);
  const radius = NODE_SCALES.department * 2;

  for (let i = 0; i < count; i++) {
    const theta = Math.random() * Math.PI * 2;
    const phi = Math.acos(2 * Math.random() - 1);
    const r = radius * (0.5 + Math.random() * 0.5);

    positions[i * 3] = r * Math.sin(phi) * Math.cos(theta);
    positions[i * 3 + 1] = r * Math.sin(phi) * Math.sin(theta);
    positions[i * 3 + 2] = r * Math.cos(phi);

    const fade = 0.5 + Math.random() * 0.5;
    colors[i * 3] = baseColor.r * fade;
    colors[i * 3 + 1] = baseColor.g * fade;
    colors[i * 3 + 2] = baseColor.b * fade;
  }

  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
  geometry.setAttribute('color', new THREE.BufferAttribute(colors, 3));

  const material = new THREE.PointsMaterial({
    size: 0.15,
    vertexColors: true,
    transparent: true,
    opacity: 0.6,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
  });

  const points = new THREE.Points(geometry, material);
  points.userData = { nodeId: node.id, isDepartmentCloud: true };
  return points;
}

// ---------------------------------------------------------------------------
// Highlight / unhighlight
// ---------------------------------------------------------------------------
export function setNodeHighlight(mesh: THREE.Mesh, highlighted: boolean): void {
  const mat = mesh.material as THREE.MeshStandardMaterial;
  if (highlighted) {
    mat.emissiveIntensity = 1.0;
    mat.opacity = 1.0;
  } else {
    const type = mesh.userData.nodeType as GovernanceNodeType;
    mat.emissiveIntensity = EMISSIVE_INTENSITY[type] ?? 0.3;
    mat.opacity = 0.9;
  }
}

export function setNodeDimmed(mesh: THREE.Mesh, dimmed: boolean): void {
  const mat = mesh.material as THREE.MeshStandardMaterial;
  mat.opacity = dimmed ? 0.15 : 0.9;
}

// ---------------------------------------------------------------------------
// LOLLI decay effect — red particles emanating from unhealthy nodes
// ---------------------------------------------------------------------------
export function createLolliParticles(position: THREE.Vector3): THREE.Points {
  const count = 20;
  const positions = new Float32Array(count * 3);
  const colors = new Float32Array(count * 3);

  for (let i = 0; i < count; i++) {
    positions[i * 3] = (Math.random() - 0.5) * 2;
    positions[i * 3 + 1] = Math.random() * 3;
    positions[i * 3 + 2] = (Math.random() - 0.5) * 2;

    colors[i * 3] = 0.88;
    colors[i * 3 + 1] = 0.2 + Math.random() * 0.15;
    colors[i * 3 + 2] = 0.2 + Math.random() * 0.15;
  }

  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
  geometry.setAttribute('color', new THREE.BufferAttribute(colors, 3));

  const material = new THREE.PointsMaterial({
    size: 0.1,
    vertexColors: true,
    transparent: true,
    opacity: 0.7,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
  });

  const points = new THREE.Points(geometry, material);
  points.position.copy(position);
  points.userData = { isLolliDecay: true };
  return points;
}

// ---------------------------------------------------------------------------
// Animate LOLLI particles (call each frame)
// ---------------------------------------------------------------------------
export function animateLolliParticles(points: THREE.Points, dt: number): void {
  const pos = points.geometry.attributes.position as THREE.BufferAttribute;
  for (let i = 0; i < pos.count; i++) {
    let y = pos.getY(i);
    y += dt * 0.5;
    if (y > 3) y = 0;
    pos.setY(i, y);
  }
  pos.needsUpdate = true;
}

// ---------------------------------------------------------------------------
// Dispose cached geometries
// ---------------------------------------------------------------------------
export function disposeNodeGeometries(): void {
  for (const geo of geometryCache.values()) {
    geo.dispose();
  }
  geometryCache.clear();
}
