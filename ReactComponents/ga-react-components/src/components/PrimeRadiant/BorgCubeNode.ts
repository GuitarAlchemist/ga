// src/components/PrimeRadiant/BorgCubeNode.ts
// Procedural Borg-cube-style mesh generator for constitution governance nodes.
// Dark metallic panels with glowing green/cyan sub-system lines.
// Designed via Octopus UI/UX Design Intelligence.
//
// Integration in ForceRadiant.tsx:
//
//   import { createBorgCubeGeometry, createBorgCubeNode } from './BorgCubeNode';
//
//   // Option A — geometry only (keeps existing material pipeline):
//   // In TYPE_GEOMETRY map (~line 451), replace constitution entry:
//   //   constitution: (r) => createBorgCubeGeometry(r * 1.4),
//
//   // Option B — full composite node (geometry + wireframe + sub-system dots):
//   // In createNodeObject (~line 486), for constitution nodes:
//   //   if (node.type === 'constitution') {
//   //     const borgGroup = createBorgCubeNode(radius * 2, nodeColor);
//   //     group.add(borgGroup);
//   //     // skip default core geometry — the borg cube IS the core
//   //   }

import * as THREE from 'three';

// ---------------------------------------------------------------------------
// Geometry — paneled box with recessed grid seams
// ---------------------------------------------------------------------------

/**
 * Generates a Borg-cube-style BufferGeometry for constitution governance nodes.
 * A subdivided box whose face vertices are pushed inward at grid intersections,
 * creating the characteristic recessed panel-line look.
 *
 * Compatible with the TYPE_GEOMETRY factory signature `(r: number) => BufferGeometry`.
 */
export function createBorgCubeGeometry(size: number): THREE.BufferGeometry {
  // 4x4 subdivisions per axis give us a 4-panel grid on each face
  const geo = new THREE.BoxGeometry(size, size, size, 4, 4, 4);

  const pos = geo.attributes.position;
  const halfSize = size / 2;
  const gridFreq = 4;
  const seamDepth = 0.03 * size;

  for (let i = 0; i < pos.count; i++) {
    const x = pos.getX(i);
    const y = pos.getY(i);
    const z = pos.getZ(i);

    const ax = Math.abs(x);
    const ay = Math.abs(y);
    const az = Math.abs(z);

    // Only displace vertices that sit on a face (within tolerance of halfSize)
    const onFace =
      Math.abs(ax - halfSize) < 0.01 ||
      Math.abs(ay - halfSize) < 0.01 ||
      Math.abs(az - halfSize) < 0.01;

    if (!onFace) continue;

    // Grid-space position: 0 at seam, 1 at panel center
    const gridScale = gridFreq / size;
    const gx = Math.abs((x * gridScale) % 1 - 0.5) * 2;
    const gy = Math.abs((y * gridScale) % 1 - 0.5) * 2;
    const gz = Math.abs((z * gridScale) % 1 - 0.5) * 2;

    const seamFactor = Math.min(gx, gy, gz);
    const inset = seamDepth * (1 - Math.min(1, seamFactor * 3));

    // Face normal — push inward along the dominant axis
    const maxAx = Math.max(ax, ay, az);
    const nx = ax === maxAx ? Math.sign(x) : 0;
    const ny = ay === maxAx ? Math.sign(y) : 0;
    const nz = az === maxAx ? Math.sign(z) : 0;

    pos.setXYZ(i, x - nx * inset, y - ny * inset, z - nz * inset);
  }

  geo.computeVertexNormals();
  return geo;
}

// ---------------------------------------------------------------------------
// Material — dark metallic hull with emissive sub-system glow
// ---------------------------------------------------------------------------

/**
 * Creates the Borg cube material: dark blue-black hull with high metalness,
 * subtle emissive glow, and clearcoat for that polished-panel look.
 *
 * The emissive color can be overridden to match the node's health-based color
 * from the existing CrystalNodeMaterials glow system.
 */
export function createBorgCubeMaterial(
  healthColor?: THREE.Color,
): THREE.MeshPhysicalMaterial {
  const emissiveColor = healthColor ?? new THREE.Color(0x00ff88);

  return new THREE.MeshPhysicalMaterial({
    color: 0x1a1a2e,
    metalness: 0.9,
    roughness: 0.3,
    emissive: emissiveColor,
    emissiveIntensity: 0.15,
    clearcoat: 0.5,
    clearcoatRoughness: 0.2,
    envMapIntensity: 0.3,
  });
}

// ---------------------------------------------------------------------------
// Wireframe overlay — glowing grid lines between panels
// ---------------------------------------------------------------------------

function createWireframeOverlay(
  size: number,
  color: THREE.Color,
): THREE.Mesh {
  const wireGeo = new THREE.BoxGeometry(
    size * 1.001, size * 1.001, size * 1.001, 4, 4, 4,
  );
  const wireMat = new THREE.MeshBasicMaterial({
    color,
    wireframe: true,
    transparent: true,
    opacity: 0.3,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
  });
  return new THREE.Mesh(wireGeo, wireMat);
}

// ---------------------------------------------------------------------------
// Sub-system dots — small emissive points on each face
// ---------------------------------------------------------------------------

const _dotColorA = new THREE.Color(0x00ff88); // green sub-system
const _dotColorB = new THREE.Color(0x00ccff); // cyan sub-system

function createSubSystemDots(size: number): THREE.Points {
  const dotsPerFace = 4;
  const faceCount = 6;
  const dotCount = faceCount * dotsPerFace;
  const positions = new Float32Array(dotCount * 3);
  const colors = new Float32Array(dotCount * 3);
  const halfSize = size * 0.5;

  let idx = 0;
  for (let face = 0; face < faceCount; face++) {
    for (let d = 0; d < dotsPerFace; d++) {
      const u = (Math.random() - 0.5) * size * 0.7;
      const v = (Math.random() - 0.5) * size * 0.7;
      const i3 = idx * 3;

      // Place dot slightly off the face surface (1.02x) so it floats above
      switch (face) {
        case 0: positions[i3] = halfSize * 1.02;  positions[i3 + 1] = u; positions[i3 + 2] = v; break;
        case 1: positions[i3] = -halfSize * 1.02; positions[i3 + 1] = u; positions[i3 + 2] = v; break;
        case 2: positions[i3] = u; positions[i3 + 1] = halfSize * 1.02;  positions[i3 + 2] = v; break;
        case 3: positions[i3] = u; positions[i3 + 1] = -halfSize * 1.02; positions[i3 + 2] = v; break;
        case 4: positions[i3] = u; positions[i3 + 1] = v; positions[i3 + 2] = halfSize * 1.02;  break;
        case 5: positions[i3] = u; positions[i3 + 1] = v; positions[i3 + 2] = -halfSize * 1.02; break;
      }

      // Alternate green / cyan sub-system colors
      const c = Math.random() > 0.5 ? _dotColorA : _dotColorB;
      colors[i3] = c.r;
      colors[i3 + 1] = c.g;
      colors[i3 + 2] = c.b;
      idx++;
    }
  }

  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
  geo.setAttribute('color', new THREE.BufferAttribute(colors, 3));

  const mat = new THREE.PointsMaterial({
    size: size * 0.08,
    vertexColors: true,
    transparent: true,
    opacity: 0.8,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    sizeAttenuation: true,
  });

  return new THREE.Points(geo, mat);
}

// ---------------------------------------------------------------------------
// Composite node — hull + wireframe + sub-system dots
// ---------------------------------------------------------------------------

/**
 * Creates a complete Borg cube node: paneled hull, glowing wireframe grid
 * overlay, and emissive sub-system dots. Returns a THREE.Group that can be
 * added directly to the scene or used as a replacement for the default
 * constitution node in ForceRadiant's createNodeObject.
 *
 * The wireframe overlay is the key visual element -- it creates the visible
 * panel grid lines that define the Borg aesthetic. The sub-system dots add
 * life without being GPU-expensive (24 points total).
 *
 * @param size        Cube edge length (matches the radius-based sizing in ForceRadiant)
 * @param healthColor Optional color override for emissive/wireframe glow.
 *                    When omitted, defaults to Borg green (0x00ff88).
 *                    Pass the node's health-derived color to integrate with
 *                    the existing CrystalNodeMaterials glow system.
 */
export function createBorgCubeNode(
  size: number,
  healthColor?: THREE.Color,
): THREE.Group {
  const group = new THREE.Group();
  const wireColor = healthColor ?? new THREE.Color(0x00ff88);

  // 1. Main hull — paneled geometry + dark metallic material
  const geo = createBorgCubeGeometry(size);
  const mat = createBorgCubeMaterial(healthColor);
  const hull = new THREE.Mesh(geo, mat);
  group.add(hull);

  // 2. Wireframe overlay — glowing grid lines between panels
  group.add(createWireframeOverlay(size, wireColor));

  // 3. Sub-system glow points — small emissive dots on each panel
  group.add(createSubSystemDots(size));

  group.name = 'borg-cube';
  return group;
}
