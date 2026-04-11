import * as THREE from 'three';

/**
 * AsteroidLOD — Level-of-detail system for the asteroid belt.
 *
 * At overview zoom, the existing THREE.Points belt (3000 particles) carries the
 * visual. When the camera moves close to the belt, this module fades in ~50
 * procedurally displaced icosahedron rocks so the viewer sees actual 3D geometry
 * instead of sprites. The two layers co-exist; this module does not touch the
 * points belt.
 *
 * Fade range (distance from camera to belt center, scene units):
 *   >= FAR  → hidden (group.visible = false)
 *   <= NEAR → fully opaque rocks
 *   between → linear fade on MeshStandardMaterial.opacity
 */

export interface AsteroidLODHandle {
  group: THREE.Group;
  update(cameraWorldPos: THREE.Vector3, beltCenter: THREE.Vector3, time: number): void;
  dispose(): void;
}

const ROCK_COUNT = 50;
const FAR = 100;  // fully hidden beyond this distance
const NEAR = 30;  // fully visible at/under this distance

// Deterministic-ish pseudo-random per call site; we don't persist so Math.random is fine.
function rand(min: number, max: number): number {
  return min + Math.random() * (max - min);
}

/**
 * Create 50 detailed asteroid meshes distributed in the belt torus.
 * Each rock is an icosahedron with vertex-level noise displacement and a dark
 * grey-brown MeshStandardMaterial. Rotation axes and speeds are randomized for
 * per-asteroid tumbling.
 */
export function createAsteroidLOD(
  beltInnerRadius: number,
  beltOuterRadius: number,
  beltThickness: number,
): AsteroidLODHandle {
  const group = new THREE.Group();
  group.name = 'asteroid-lod';
  group.visible = false; // hidden until camera gets close

  const rockMeshes: THREE.Mesh[] = [];
  const materials: THREE.MeshStandardMaterial[] = [];
  const geometries: THREE.BufferGeometry[] = [];

  for (let i = 0; i < ROCK_COUNT; i++) {
    // Radius varies across the size range of large-to-medium asteroids.
    const radius = rand(0.05, 0.3);

    // Icosahedron with 1 subdivision = 42 vertices — enough to look rocky,
    // cheap enough to run 50 of them without a perf hit.
    const geo = new THREE.IcosahedronGeometry(radius, 1);

    // Vertex displacement: perturb each vertex outward/inward by a noise factor
    // so the silhouette looks irregular instead of obviously spherical.
    const posAttr = geo.getAttribute('position') as THREE.BufferAttribute;
    const tmp = new THREE.Vector3();
    for (let v = 0; v < posAttr.count; v++) {
      tmp.fromBufferAttribute(posAttr, v);
      // Displacement factor: [0.75, 1.25] of original radius — gives lumpy rocks
      // without breaking the normal direction too badly.
      const displacement = 0.75 + Math.random() * 0.5;
      tmp.multiplyScalar(displacement);
      posAttr.setXYZ(v, tmp.x, tmp.y, tmp.z);
    }
    posAttr.needsUpdate = true;
    geo.computeVertexNormals();
    geometries.push(geo);

    // Color variation: lean grey/brown for rocky S-type, darker for C-type carbonaceous.
    // HSL around a warm grey; lightness 0.18–0.42 for a believable rock palette.
    const isCarbonaceous = Math.random() < 0.4;
    const hue = isCarbonaceous ? 0.08 : 0.07;          // brown-ish
    const saturation = isCarbonaceous ? 0.05 : 0.15;
    const lightness = isCarbonaceous ? 0.18 : 0.3 + Math.random() * 0.12;
    const color = new THREE.Color().setHSL(hue, saturation, lightness);

    const mat = new THREE.MeshStandardMaterial({
      color,
      roughness: 0.95,
      metalness: 0.02,
      transparent: true,
      opacity: 0,         // starts hidden; update() drives the fade
      depthWrite: false,  // fade looks cleaner without writing depth
    });
    materials.push(mat);

    const mesh = new THREE.Mesh(geo, mat);

    // Distribute in the belt torus.
    const angle = Math.random() * Math.PI * 2;
    const beltRadius = beltInnerRadius + Math.random() * (beltOuterRadius - beltInnerRadius);
    const y = (Math.random() - 0.5) * beltThickness;
    mesh.position.set(Math.cos(angle) * beltRadius, y, Math.sin(angle) * beltRadius);

    // Slight non-uniform scale for extra silhouette variety on top of vertex noise.
    mesh.scale.set(
      1 + Math.random() * 0.3,
      0.8 + Math.random() * 0.3,
      1 + Math.random() * 0.3,
    );

    // Random rotation axis + speed per asteroid for tumbling motion.
    // Stored on userData so update() can animate without capturing a closure var per mesh.
    mesh.userData.rotSpeed = new THREE.Vector3(
      (Math.random() - 0.5) * 0.01,
      (Math.random() - 0.5) * 0.01,
      (Math.random() - 0.5) * 0.005,
    );

    // Random starting orientation so they aren't all pointing the same way.
    mesh.rotation.set(
      Math.random() * Math.PI * 2,
      Math.random() * Math.PI * 2,
      Math.random() * Math.PI * 2,
    );

    rockMeshes.push(mesh);
    group.add(mesh);
  }

  function update(cameraWorldPos: THREE.Vector3, beltCenter: THREE.Vector3, _time: number): void {
    const distToBelt = cameraWorldPos.distanceTo(beltCenter);

    // Linear fade: opacity 1 at NEAR, 0 at FAR.
    const opacity = THREE.MathUtils.clamp(
      (FAR - distToBelt) / (FAR - NEAR),
      0,
      1,
    );

    // Skip all per-mesh work when the group is effectively invisible.
    if (opacity <= 0.02) {
      if (group.visible) group.visible = false;
      return;
    }

    if (!group.visible) group.visible = true;

    // Push fade to every material (they share the same target opacity).
    for (let i = 0; i < materials.length; i++) {
      materials[i].opacity = opacity;
    }

    // Tumbling animation — cheap, no allocations.
    for (let i = 0; i < rockMeshes.length; i++) {
      const mesh = rockMeshes[i];
      const rs = mesh.userData.rotSpeed as THREE.Vector3;
      mesh.rotation.x += rs.x;
      mesh.rotation.y += rs.y;
      mesh.rotation.z += rs.z;
    }
  }

  function dispose(): void {
    for (const geo of geometries) geo.dispose();
    for (const mat of materials) mat.dispose();
    rockMeshes.length = 0;
    materials.length = 0;
    geometries.length = 0;
    // Detach children; caller owns group removal from its parent.
    while (group.children.length > 0) {
      group.remove(group.children[0]);
    }
  }

  return { group, update, dispose };
}
