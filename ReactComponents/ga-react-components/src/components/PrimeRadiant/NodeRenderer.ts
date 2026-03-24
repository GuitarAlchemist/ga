// src/components/PrimeRadiant/NodeRenderer.ts
// Renders governance nodes as distinct 3D shapes based on type

import * as THREE from 'three';
import type { GovernanceNode, GovernanceNodeType } from './types';
import { NODE_SCALES } from './types';

// ---------------------------------------------------------------------------
// Geometry factories — one per node type (high subdivision for smooth look)
// ---------------------------------------------------------------------------
function createConstitutionGeometry(): THREE.BufferGeometry {
  return new THREE.IcosahedronGeometry(1, 3);
}

function createPolicyGeometry(): THREE.BufferGeometry {
  return new THREE.OctahedronGeometry(1, 2);
}

function createPersonaGeometry(): THREE.BufferGeometry {
  return new THREE.SphereGeometry(1, 64, 64);
}

function createPipelineGeometry(): THREE.BufferGeometry {
  return new THREE.TorusGeometry(1, 0.3, 32, 64);
}

function createDepartmentGeometry(): THREE.BufferGeometry {
  return new THREE.DodecahedronGeometry(1, 2);
}

function createSchemaGeometry(): THREE.BufferGeometry {
  // Beveled cube effect via rounded box approximation
  const geo = new THREE.BoxGeometry(1, 1, 1, 4, 4, 4);
  return geo;
}

function createTestGeometry(): THREE.BufferGeometry {
  return new THREE.TetrahedronGeometry(1, 2);
}

function createIxqlGeometry(): THREE.BufferGeometry {
  return new THREE.CylinderGeometry(0.6, 0.6, 1.4, 32);
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
  constitution: 0.8,
  policy: 0.4,
  persona: 0.6,
  pipeline: 0.5,
  department: 0.3,
  schema: 0.3,
  test: 0.4,
  ixql: 0.4,
};

// ---------------------------------------------------------------------------
// PBR material factory — each node type gets distinct physical properties
// ---------------------------------------------------------------------------
function createNodeMaterial(node: GovernanceNode): THREE.Material {
  const color = new THREE.Color(node.color);
  const emissive = EMISSIVE_INTENSITY[node.type];

  switch (node.type) {
    case 'constitution':
      // Golden radiant — clearcoated metallic
      return new THREE.MeshPhysicalMaterial({
        color,
        emissive: color,
        emissiveIntensity: emissive,
        metalness: 0.3,
        roughness: 0.15,
        clearcoat: 1.0,
        clearcoatRoughness: 0.05,
        reflectivity: 1.0,
        transparent: true,
        opacity: 0.95,
      });

    case 'persona':
      // Iridescent organic feel
      return new THREE.MeshPhysicalMaterial({
        color,
        emissive: color,
        emissiveIntensity: emissive,
        metalness: 0.1,
        roughness: 0.2,
        clearcoat: 0.8,
        clearcoatRoughness: 0.1,
        sheen: 1.0,
        sheenRoughness: 0.3,
        sheenColor: new THREE.Color('#E0A0FF'),
        iridescence: 0.8,
        iridescenceIOR: 1.5,
        transparent: true,
        opacity: 0.92,
      });

    case 'schema':
      // Translucent glass-like
      return new THREE.MeshPhysicalMaterial({
        color,
        emissive: color,
        emissiveIntensity: emissive,
        metalness: 0.0,
        roughness: 0.05,
        transmission: 0.3,
        thickness: 1.0,
        ior: 1.5,
        transparent: true,
        opacity: 0.85,
      });

    case 'policy':
      // Semi-metallic green
      return new THREE.MeshStandardMaterial({
        color,
        emissive: color,
        emissiveIntensity: emissive,
        metalness: 0.5,
        roughness: 0.25,
        transparent: true,
        opacity: 0.92,
      });

    case 'test':
      // Matte red with slight roughness
      return new THREE.MeshStandardMaterial({
        color,
        emissive: color,
        emissiveIntensity: emissive,
        metalness: 0.2,
        roughness: 0.55,
        transparent: true,
        opacity: 0.92,
      });

    case 'department':
      // Warm metallic orange
      return new THREE.MeshStandardMaterial({
        color,
        emissive: color,
        emissiveIntensity: emissive,
        metalness: 0.4,
        roughness: 0.3,
        transparent: true,
        opacity: 0.92,
      });

    case 'pipeline':
      // Cool blue with clearcoat
      return new THREE.MeshPhysicalMaterial({
        color,
        emissive: color,
        emissiveIntensity: emissive,
        metalness: 0.3,
        roughness: 0.2,
        clearcoat: 0.6,
        clearcoatRoughness: 0.15,
        transparent: true,
        opacity: 0.92,
      });

    case 'ixql':
      // Warm orange with subtle sheen
      return new THREE.MeshPhysicalMaterial({
        color,
        emissive: color,
        emissiveIntensity: emissive,
        metalness: 0.35,
        roughness: 0.3,
        sheen: 0.5,
        sheenRoughness: 0.4,
        sheenColor: new THREE.Color('#FFB070'),
        transparent: true,
        opacity: 0.92,
      });

    default:
      return new THREE.MeshStandardMaterial({
        color,
        emissive: color,
        emissiveIntensity: emissive,
        metalness: 0.3,
        roughness: 0.4,
        transparent: true,
        opacity: 0.9,
      });
  }
}

// ---------------------------------------------------------------------------
// Create a single node mesh
// ---------------------------------------------------------------------------
export function createNodeMesh(node: GovernanceNode): THREE.Mesh {
  const geometry = getGeometry(node.type);
  const scale = NODE_SCALES[node.type];

  const material = createNodeMaterial(node);

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
